"""Worker-Agent gegen eine lokale Ollama-Instanz.

Der Agent bekommt ein eng umrissenes Ziel, ein paar Dateien als Kontext und vier
Werkzeuge: read_file, list_dir, write_file, finish. Mehr nicht - insbesondere
*keinen* Shell-Zugriff. Bauen, Testen und Zurueckrollen macht der Orchestrator,
damit ein lokales Modell die Abnahme nicht selbst faelschen kann.
"""

from __future__ import annotations

import json
import sys
import urllib.error
import urllib.request
from dataclasses import dataclass, field
from fnmatch import fnmatch
from pathlib import Path

OLLAMA_URL = "http://localhost:11434/api/chat"

TOOL_SPEC = [
    {
        "type": "function",
        "function": {
            "name": "read_file",
            "description": "Liest eine Textdatei relativ zur Repo-Wurzel und gibt sie mit Zeilennummern zurueck.",
            "parameters": {
                "type": "object",
                "properties": {
                    "path": {"type": "string", "description": "Pfad relativ zur Repo-Wurzel, z.B. src/AoE.Core/AoE.Core.csproj"}
                },
                "required": ["path"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "list_dir",
            "description": "Listet den Inhalt eines Verzeichnisses relativ zur Repo-Wurzel.",
            "parameters": {
                "type": "object",
                "properties": {"path": {"type": "string"}},
                "required": ["path"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "write_file",
            "description": (
                "Schreibt eine Datei KOMPLETT neu. Immer den vollstaendigen Inhalt uebergeben, "
                "niemals Ausschnitte oder Platzhalter wie '...'. Nur Pfade aus der Freigabeliste."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "path": {"type": "string"},
                    "content": {"type": "string", "description": "Der vollstaendige neue Dateiinhalt"},
                },
                "required": ["path", "content"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "replace_in_file",
            "description": (
                "Ersetzt einen exakten Textblock in einer Datei. Bevorzugte Methode fuer grosse "
                "Dateien - dabei kann nichts verlorengehen. 'old' muss exakt und genau einmal "
                "vorkommen, inklusive Einrueckung, aber ohne die Zeilennummern aus read_file."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "path": {"type": "string"},
                    "old": {"type": "string", "description": "Exakter bestehender Text, genau einmal vorkommend"},
                    "new": {"type": "string", "description": "Ersatztext"},
                },
                "required": ["path", "old", "new"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "delete_file",
            "description": "Loescht eine Datei. Nur Pfade aus der Freigabeliste.",
            "parameters": {
                "type": "object",
                "properties": {"path": {"type": "string"}},
                "required": ["path"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "rename_file",
            "description": (
                "Benennt eine Datei um. Quelle UND Ziel muessen auf der Freigabeliste stehen. "
                "Reine Gross-/Kleinschreibungsaenderungen werden korrekt ausgefuehrt."
            ),
            "parameters": {
                "type": "object",
                "properties": {
                    "path": {"type": "string", "description": "bisheriger Pfad"},
                    "new_path": {"type": "string", "description": "neuer Pfad"},
                },
                "required": ["path", "new_path"],
            },
        },
    },
    {
        "type": "function",
        "function": {
            "name": "finish",
            "description": "Aufrufen, wenn die Aufgabe fertig ist. Danach laeuft die Abnahme (Build + Tests).",
            "parameters": {
                "type": "object",
                "properties": {
                    "summary": {"type": "string", "description": "Was wurde geaendert und warum"}
                },
                "required": ["summary"],
            },
        },
    },
]

SYSTEM_PROMPT = """Du bist ein praeziser C#/.NET-Entwickler-Agent in einem MonoGame-Projekt
(Age-of-Empires-Klon, .NET 10). Du arbeitest genau eine eng umrissene Aufgabe ab.

Regeln:
- Arbeite ausschliesslich mit den Werkzeugen. Gib niemals Code in der Chat-Antwort aus.
- Lies eine Datei mit read_file, bevor du sie aenderst.
- Fuer Aenderungen an bestehenden Dateien nimm replace_in_file. Das ist chirurgisch und
  verliert nichts. write_file ist nur fuer neue oder sehr kleine Dateien gedacht.
- write_file schreibt die Datei KOMPLETT neu. Uebergib dann immer den ganzen Inhalt.
  Niemals '...', niemals 'Rest bleibt gleich', niemals nur den geaenderten Block.
- Die Zeilennummern aus read_file gehoeren NICHT in den Datei-Inhalt. Sie sind nur Anzeige.
- Aendere nur, was die Aufgabe verlangt. Keine Umformatierungen, keine Zusatzverbesserungen,
  keine neuen Abhaengigkeiten.
- Du hast keinen Shell-Zugriff. Bauen und Testen uebernimmt danach der Orchestrator.
- Wenn du fertig bist, rufe finish mit einer kurzen Zusammenfassung auf.
- Wenn die Aufgabe unmoeglich oder bereits erledigt ist, rufe finish auf und sage das.
"""


@dataclass
class AgentResult:
    task_id: str
    finished: bool
    summary: str
    written: list[str] = field(default_factory=list)
    turns: int = 0
    error: str | None = None


def _indent_of(line: str) -> int:
    return len(line) - len(line.lstrip())


def _shift_indent(line: str, shift: int) -> str:
    """Verschiebt die Einrueckung einer Zeile um 'shift' Zeichen."""
    if not line.strip():
        return line
    if shift >= 0:
        return " " * shift + line
    return line[min(-shift, _indent_of(line)):]


def _match_ignoring_indent(text: str, needle: str):
    """Sucht den Block zeilenweise ohne Beachtung des Leerraums.

    Liefert (start, end, shift) fuer genau einen Treffer, "MEHRDEUTIG" bei mehreren
    und None, wenn es auch so nicht passt. 'shift' ist die Differenz zwischen der
    Einrueckung in der Datei und der im gesuchten Text.
    """
    src_lines = text.split("\n")
    needle_lines = needle.split("\n")
    while needle_lines and not needle_lines[0].strip():
        needle_lines.pop(0)
    while needle_lines and not needle_lines[-1].strip():
        needle_lines.pop()
    if not needle_lines:
        return None

    stripped_needle = [line.strip() for line in needle_lines]
    size = len(needle_lines)
    hits = []
    for start in range(len(src_lines) - size + 1):
        window = src_lines[start:start + size]
        if [line.strip() for line in window] == stripped_needle:
            hits.append((start, start + size, window))
    if not hits:
        return None
    if len(hits) > 1:
        return "MEHRDEUTIG"

    start, end, window = hits[0]
    first_src = next((line for line in window if line.strip()), "")
    first_needle = next((line for line in needle_lines if line.strip()), "")
    return start, end, _indent_of(first_src) - _indent_of(first_needle)


class Sandbox:
    """Haelt alle Datei-Operationen innerhalb der Repo-Wurzel und der Freigabeliste."""

    def __init__(self, root: Path, allow_write: list[str]):
        self.root = root.resolve()
        self.allow_write = allow_write

    def _resolve_keeping_case(self, rel: str) -> Path:
        """Wie _resolve, behaelt aber die angefragte Schreibweise des Dateinamens.

        Noetig, weil Path.resolve() unter Windows auf den tatsaechlichen Namen
        normalisiert - damit waere eine reine Gross-/Kleinschreibungsaenderung
        nicht mehr erkennbar.
        """
        cleaned = rel.replace("\\", "/").lstrip("/")
        parent_rel, _, name = cleaned.rpartition("/")
        parent = self._resolve(parent_rel) if parent_rel else self.root
        return parent / name

    def _resolve(self, rel: str) -> Path:
        candidate = (self.root / rel.replace("\\", "/").lstrip("/")).resolve()
        if candidate != self.root and self.root not in candidate.parents:
            raise PermissionError(f"Pfad liegt ausserhalb der Repo-Wurzel: {rel}")
        return candidate

    def rel_of(self, path: Path) -> str:
        return path.relative_to(self.root).as_posix()

    def may_write(self, rel: str) -> bool:
        norm = rel.replace("\\", "/").lstrip("/")
        return any(fnmatch(norm, pattern) for pattern in self.allow_write)

    def read(self, rel: str) -> str:
        path = self._resolve(rel)
        if not path.is_file():
            return f"FEHLER: Datei existiert nicht: {rel}"
        raw = path.read_text(encoding="utf-8-sig", errors="replace")
        lines = raw.splitlines()
        # Trennzeichen '|' statt Leerzeichen: sonst kann das Modell nicht erkennen,
        # wo die Anzeige aufhoert und die echte Einrueckung anfaengt - und trifft
        # mit replace_in_file systematisch daneben.
        numbered = "\n".join(f"{i:5d} | {line}" for i, line in enumerate(lines, 1))
        return (
            f"--- {rel} ({len(lines)} Zeilen) ---\n"
            f"Format: Zeilennummer, dann ' | ', dann der Code. Alles bis einschliesslich\n"
            f"'| ' ist Anzeige und gehoert NICHT zum Dateiinhalt.\n{numbered}"
        )

    def listdir(self, rel: str) -> str:
        path = self._resolve(rel or ".")
        if not path.is_dir():
            return f"FEHLER: Kein Verzeichnis: {rel}"
        skip = {"bin", "obj", ".vs", ".git", "node_modules"}
        entries = []
        for item in sorted(path.iterdir(), key=lambda p: (p.is_file(), p.name.lower())):
            if item.name in skip:
                continue
            entries.append(f"{item.name}/" if item.is_dir() else item.name)
        return f"--- {rel or '.'} ---\n" + "\n".join(entries)

    def delete(self, rel: str) -> str:
        norm = rel.replace("\\", "/").lstrip("/")
        if not self.may_write(norm):
            return (
                f"ABGELEHNT: '{norm}' steht nicht auf der Freigabeliste dieser Aufgabe.\n"
                f"Erlaubt sind: {', '.join(self.allow_write)}"
            )
        path = self._resolve(norm)
        if not path.is_file():
            return f"FEHLER: Datei existiert nicht: {norm}"
        path.unlink()
        return f"OK: {norm} geloescht."

    def rename(self, rel: str, new_rel: str) -> str:
        src_norm = rel.replace("\\", "/").lstrip("/")
        dst_norm = new_rel.replace("\\", "/").lstrip("/")
        for candidate in (src_norm, dst_norm):
            if not self.may_write(candidate):
                return (
                    f"ABGELEHNT: '{candidate}' steht nicht auf der Freigabeliste dieser Aufgabe.\n"
                    f"Erlaubt sind: {', '.join(self.allow_write)}"
                )
        src = self._resolve(src_norm)
        dst = self._resolve_keeping_case(dst_norm)
        if not src.is_file():
            return f"FEHLER: Datei existiert nicht: {src_norm}"

        # Reine Gross-/Kleinschreibung: Windows haelt Quelle und Ziel fuer dieselbe
        # Datei, ein direktes rename bewirkt nichts. Deshalb ueber einen Zwischennamen.
        # Vergleich ueber str(): Path.__eq__ ist unter Windows case-insensitiv und
        # wuerde genau diesen Fall verschlucken.
        if str(src) != str(dst) and str(src).lower() == str(dst).lower():
            temp = src.with_name(src.name + ".tmprename")
            src.rename(temp)
            temp.rename(dst)
            return f"OK: {src_norm} -> {dst_norm} umbenannt (Gross-/Kleinschreibung)."

        if dst.exists():
            return f"ABGELEHNT: Ziel existiert bereits: {dst_norm}"
        dst.parent.mkdir(parents=True, exist_ok=True)
        src.rename(dst)
        return f"OK: {src_norm} -> {dst_norm} umbenannt."

    def replace(self, rel: str, old: str, new: str) -> str:
        norm = rel.replace("\\", "/").lstrip("/")
        if not self.may_write(norm):
            return (
                f"ABGELEHNT: '{norm}' steht nicht auf der Freigabeliste dieser Aufgabe.\n"
                f"Erlaubt sind: {', '.join(self.allow_write)}"
            )
        path = self._resolve(norm)
        if not path.is_file():
            return f"FEHLER: Datei existiert nicht: {norm}"
        if not old:
            return "ABGELEHNT: 'old' darf nicht leer sein."

        raw = path.read_bytes()
        bom = "﻿" if raw.startswith(b"\xef\xbb\xbf") else ""
        newline = "\r\n" if b"\r\n" in raw else "\n"
        text = raw.decode("utf-8-sig", errors="replace").replace("\r\n", "\n").replace("\r", "\n")

        needle = old.replace("\r\n", "\n").replace("\r", "\n")
        replacement = new.replace("\r\n", "\n").replace("\r", "\n")

        count = text.count(needle)
        if count > 1:
            return f"MEHRDEUTIG: '{needle[:60]}...' kommt {count}x in {norm} vor. Nimm mehr Kontext dazu."

        if count == 1:
            updated = text.replace(needle, replacement, 1)
        else:
            # Exakt danebengegriffen - fast immer eine verschobene Einrueckung.
            # Zweiter Versuch zeilenweise ohne Leerraum, dann auf die echte
            # Einrueckung der Fundstelle umgesetzt.
            match = _match_ignoring_indent(text, needle)
            if match is None:
                return (
                    f"NICHT GEFUNDEN in {norm}. Auch ohne Beachtung der Einrueckung kein Treffer. "
                    "Lies die Stelle erneut mit read_file und uebernimm den Code ohne die "
                    "Anzeige 'nummer | '."
                )
            if match == "MEHRDEUTIG":
                return f"MEHRDEUTIG: Der Block kommt mehrfach in {norm} vor. Nimm mehr Kontext dazu."
            start, end, shift = match
            src_lines = text.split("\n")
            new_lines = [_shift_indent(line, shift) for line in replacement.split("\n")]
            updated = "\n".join(src_lines[:start] + new_lines + src_lines[end:])
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write(bom + updated.replace("\n", newline))
        delta = len(updated.splitlines()) - len(text.splitlines())
        return f"OK: {norm} geaendert ({delta:+d} Zeilen)."

    def write(self, rel: str, content: str) -> str:
        norm = rel.replace("\\", "/").lstrip("/")
        if not self.may_write(norm):
            return (
                f"ABGELEHNT: '{norm}' steht nicht auf der Freigabeliste dieser Aufgabe.\n"
                f"Erlaubt sind: {', '.join(self.allow_write)}"
            )
        if "..." in content and len(content) < 400:
            return "ABGELEHNT: Der Inhalt sieht nach einem Ausschnitt aus. write_file braucht die KOMPLETTE Datei."
        path = self._resolve(norm)
        path.parent.mkdir(parents=True, exist_ok=True)

        # Zeilenenden und BOM der Vorgaengerdatei beibehalten - sonst rauscht
        # der halbe Diff durch reine Encoding-Aenderungen.
        bom, newline = "", "\r\n"
        if path.is_file():
            original = path.read_bytes()
            bom = "﻿" if original.startswith(b"\xef\xbb\xbf") else ""
            newline = "\r\n" if b"\r\n" in original else "\n"
        normalized = content.replace("\r\n", "\n").replace("\r", "\n")
        with open(path, "w", encoding="utf-8", newline="") as handle:
            handle.write(bom + normalized.replace("\n", newline))
        return f"OK: {norm} geschrieben ({len(normalized.splitlines())} Zeilen)."


def _post(payload: dict, timeout: int) -> dict:
    data = json.dumps(payload).encode("utf-8")
    request = urllib.request.Request(
        OLLAMA_URL, data=data, headers={"Content-Type": "application/json"}
    )
    with urllib.request.urlopen(request, timeout=timeout) as response:
        return json.loads(response.read().decode("utf-8"))


def run_agent(
    task: dict,
    root: Path,
    model: str,
    max_turns: int = 24,
    timeout: int = 900,
    think: bool = False,
    num_ctx: int = 65536,
    log=lambda msg: None,
) -> AgentResult:
    sandbox = Sandbox(root, task.get("allow_write", []))

    context_blocks = []
    for rel in task.get("context_files", []):
        context_blocks.append(sandbox.read(rel))
    context = "\n\n".join(context_blocks) if context_blocks else "(keine vorgeladenen Dateien)"

    user_prompt = (
        f"# Aufgabe {task['id']}: {task['title']}\n\n"
        f"{task['prompt']}\n\n"
        f"## Schreibrechte\nDu darfst ausschliesslich schreiben: "
        f"{', '.join(task.get('allow_write', [])) or '(nichts)'}\n\n"
        f"## Vorgeladener Kontext\n{context}\n"
    )

    messages = [
        {"role": "system", "content": SYSTEM_PROMPT},
        {"role": "user", "content": user_prompt},
    ]

    result = AgentResult(task_id=task["id"], finished=False, summary="")

    for turn in range(1, max_turns + 1):
        result.turns = turn
        payload = {
            "model": model,
            "messages": messages,
            "tools": TOOL_SPEC,
            "stream": False,
            "think": think,
            "options": {"temperature": 0.1, "num_ctx": num_ctx},
        }
        try:
            response = _post(payload, timeout)
        except (urllib.error.URLError, TimeoutError, OSError) as exc:
            result.error = f"Ollama nicht erreichbar oder Timeout: {exc}"
            return result

        message = response.get("message", {})
        messages.append(message)
        tool_calls = message.get("tool_calls") or []

        if not tool_calls:
            # Modell hat nur geredet - einmal zurueckstupsen, dann abbrechen.
            text = (message.get("content") or "").strip()
            log(f"    [{turn}] kein Tool-Call, Modell sagt: {text[:160]}")
            if turn >= max_turns - 1:
                result.error = "Modell hat aufgehoert, ohne finish aufzurufen."
                result.summary = text
                return result
            messages.append({
                "role": "user",
                "content": "Bitte arbeite mit den Werkzeugen weiter oder rufe finish auf.",
            })
            continue

        for call in tool_calls:
            function = call.get("function", {})
            name = function.get("name", "")
            args = function.get("arguments", {})
            if isinstance(args, str):
                try:
                    args = json.loads(args)
                except json.JSONDecodeError:
                    args = {}

            if name == "finish":
                result.finished = True
                result.summary = args.get("summary", "")
                log(f"    [{turn}] finish: {result.summary[:200]}")
                return result

            # Jede Werkzeug-Ausnahme wird zur Fehlermeldung an das Modell. Ein Ausbruchs-
            # versuch darf den Lauf nicht abbrechen, sondern soll korrigierbar sein.
            try:
                if name == "read_file":
                    output = sandbox.read(args.get("path", ""))
                    log(f"    [{turn}] read_file {args.get('path', '')}")
                elif name == "list_dir":
                    output = sandbox.listdir(args.get("path", ""))
                    log(f"    [{turn}] list_dir {args.get('path', '')}")
                elif name == "write_file":
                    rel = args.get("path", "")
                    output = sandbox.write(rel, args.get("content", ""))
                    log(f"    [{turn}] write_file {rel} -> {output[:80]}")
                    if output.startswith("OK:"):
                        norm = rel.replace("\\", "/").lstrip("/")
                        if norm not in result.written:
                            result.written.append(norm)
                elif name == "replace_in_file":
                    rel = args.get("path", "")
                    output = sandbox.replace(rel, args.get("old", ""), args.get("new", ""))
                    log(f"    [{turn}] replace_in_file {rel} -> {output[:80]}")
                    if output.startswith("OK:"):
                        norm = rel.replace("\\", "/").lstrip("/")
                        if norm not in result.written:
                            result.written.append(norm)
                elif name == "delete_file":
                    rel = args.get("path", "")
                    output = sandbox.delete(rel)
                    log(f"    [{turn}] delete_file {rel} -> {output[:80]}")
                    if output.startswith("OK:"):
                        norm = rel.replace("\\", "/").lstrip("/")
                        if norm not in result.written:
                            result.written.append(norm)
                elif name == "rename_file":
                    rel = args.get("path", "")
                    new_rel = args.get("new_path", "")
                    output = sandbox.rename(rel, new_rel)
                    log(f"    [{turn}] rename_file {rel} -> {new_rel}: {output[:60]}")
                    if output.startswith("OK:"):
                        for candidate in (rel, new_rel):
                            norm = candidate.replace("\\", "/").lstrip("/")
                            if norm not in result.written:
                                result.written.append(norm)
                else:
                    output = f"FEHLER: Unbekanntes Werkzeug '{name}'"
            except PermissionError as exc:
                output = f"ABGELEHNT: {exc}"
                log(f"    [{turn}] {name} abgewehrt: {exc}")
            except OSError as exc:
                output = f"FEHLER beim Dateizugriff: {exc}"
                log(f"    [{turn}] {name} Fehler: {exc}")

            messages.append({"role": "tool", "tool_name": name, "content": output})

    result.error = f"Limit von {max_turns} Zuegen erreicht, ohne finish."
    return result


if __name__ == "__main__":
    print("Dieses Modul wird von run_tasks.py verwendet.", file=sys.stderr)
    sys.exit(1)
