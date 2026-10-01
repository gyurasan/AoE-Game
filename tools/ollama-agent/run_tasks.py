"""Orchestrator: verteilt TODO-Punkte an lokale Ollama-Agents und nimmt sie ab.

Ablauf pro Aufgabe:

    Snapshot  ->  Agent laeuft  ->  post_steps  ->  Verifikation  ->  behalten / zurueckrollen

Die Verifikation (Build + Tests) laeuft im Orchestrator, nicht im Agent. Das Modell
hat keinen Shell-Zugriff und kann seine eigene Abnahme damit nicht faelschen.
Schlaegt sie fehl, wird der Snapshot zurueckgespielt und die Aufgabe bleibt offen.

Aufruf:
    py run_tasks.py --list
    py run_tasks.py --tasks H3 D2
    py run_tasks.py --auto              # alle Aufgaben mit "auto": true
    py run_tasks.py --tasks H3 --dry-run
"""

from __future__ import annotations

import argparse
import json
import shutil
import subprocess
import sys
import time
from datetime import datetime
from pathlib import Path

from ollama_agent import run_agent

HERE = Path(__file__).resolve().parent
ROOT = HERE.parent.parent
SNAPSHOT_DIR = HERE / ".snapshots"
REJECTED_DIR = HERE / ".rejected"
LOG_DIR = HERE / "logs"

# Harte Sperre: der Harness fasst die Git-History nicht an. Siehe README.
FORBIDDEN = ("commit", "push", "reset --hard", "clean -fd", "rebase")

# Geht die Ausgabe in eine Pipe, schreibt Python unter Windows cp1252. Ein "→"
# in der Zusammenfassung des Modells warf dann UnicodeEncodeError und brach D15
# nach den Aenderungen, aber vor der Abnahme ab. Die Konsole bekommt nicht
# darstellbare Zeichen jetzt als "?"; die Logdatei schreibt ohnehin UTF-8.
sys.stdout.reconfigure(errors="replace")


def log_line(text: str, handle=None) -> None:
    print(text, flush=True)
    if handle:
        handle.write(text + "\n")
        handle.flush()


def expand(patterns: list[str]) -> list[Path]:
    found: list[Path] = []
    for pattern in patterns:
        for path in ROOT.glob(pattern):
            if path.is_file() and path not in found:
                found.append(path)
    return found


def snapshot(task_id: str, patterns: list[str]) -> dict:
    """Sichert alle betroffenen Dateien. Merkt sich auch, was es noch nicht gab."""
    target = SNAPSHOT_DIR / task_id
    if target.exists():
        shutil.rmtree(target)
    target.mkdir(parents=True, exist_ok=True)

    saved = []
    for path in expand(patterns):
        rel = path.relative_to(ROOT).as_posix()
        dest = target / rel
        dest.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(path, dest)
        saved.append(rel)
    return {"dir": target, "saved": saved}


def keep_rejected(task_id: str, written: list[str]) -> Path | None:
    """Sichert die abgelehnte Fassung, bevor zurueckgerollt wird.

    Ohne das ist nach einem Fehlschlag nicht mehr feststellbar, ob das Modell
    Unsinn gebaut hat oder das Abnahmekriterium zu eng war.
    """
    if not written:
        return None
    target = REJECTED_DIR / task_id
    if target.exists():
        shutil.rmtree(target)
    for rel in written:
        source = ROOT / rel
        if source.is_file():
            dest = target / rel
            dest.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, dest)
    return target if target.exists() else None


def exists_exact(path: Path) -> bool:
    """True nur, wenn die Datei mit genau dieser Schreibweise existiert.

    Windows ist case-insensitiv, 'path.exists()' beantwortet die Frage also nicht.
    """
    if not path.parent.is_dir():
        return False
    return path.name in {p.name for p in path.parent.iterdir()}


def case_variant_of(path: Path) -> Path | None:
    """Findet eine anders geschriebene Fassung derselben Datei, falls vorhanden."""
    if not path.parent.is_dir():
        return None
    for item in path.parent.iterdir():
        if item.is_file() and item.name.lower() == path.name.lower() and item.name != path.name:
            return item
    return None


def restore(snap: dict, written: list[str]) -> list[str]:
    """Spielt den Snapshot zurueck und loescht neu angelegte Dateien."""
    reverted = []
    for rel in snap["saved"]:
        source = snap["dir"] / rel
        if source.is_file():
            target = ROOT / rel
            # Wurde die Datei nur anders geschrieben (Umbenennen), erst die
            # urspruengliche Schreibweise wiederherstellen - ueber einen
            # Zwischennamen, sonst passiert unter Windows nichts.
            if not exists_exact(target):
                variant = case_variant_of(target)
                if variant is not None:
                    temp = variant.with_name(variant.name + ".tmprestore")
                    variant.rename(temp)
                    temp.rename(target)
                    reverted.append(f"{rel} (Schreibweise zurueckgesetzt)")
            shutil.copy2(source, target)
            reverted.append(rel)
    # Neu angelegte Dateien wieder entfernen - aber nur, wenn sie nicht bloss die
    # andere Schreibweise einer gesicherten Datei sind. Sonst loescht ein
    # zurueckgerolltes Umbenennen unter Windows die Datei ganz.
    saved_lower = {rel.lower() for rel in snap["saved"]}
    for rel in written:
        if rel in snap["saved"] or rel.lower() in saved_lower:
            continue
        created = ROOT / rel
        if created.is_file():
            created.unlink()
            reverted.append(f"{rel} (geloescht)")
    return reverted


def run_command(command: str, timeout: int = 900) -> tuple[int, str]:
    lowered = command.lower()
    for bad in FORBIDDEN:
        if bad in lowered:
            return 1, f"ABGELEHNT: Kommando enthaelt '{bad}' - der Harness aendert keine Git-History."
    try:
        completed = subprocess.run(
            command, cwd=ROOT, shell=True, capture_output=True,
            text=True, encoding="utf-8", errors="replace", timeout=timeout,
        )
        return completed.returncode, (completed.stdout or "") + (completed.stderr or "")
    except subprocess.TimeoutExpired:
        return 1, f"TIMEOUT nach {timeout}s: {command}"


def verify(task: dict, handle) -> tuple[bool, str]:
    for command in task.get("verify", []):
        log_line(f"    pruefe: {command}", handle)
        code, output = run_command(command)
        tail = "\n".join(output.strip().splitlines()[-12:])
        if code != 0:
            log_line(f"    FEHLGESCHLAGEN (exit {code})", handle)
            return False, f"$ {command}\n{tail}"
        log_line("    ok", handle)
    return True, ""


def process(task: dict, model: str, handle, dry_run: bool, num_ctx: int) -> dict:
    task_id = task["id"]
    log_line(f"\n=== {task_id} · {task['title']} ===", handle)

    if dry_run:
        log_line("  (dry-run, kein Agent-Lauf)", handle)
        return {"id": task_id, "status": "dry-run"}

    snap = snapshot(task_id, task.get("allow_write", []))
    log_line(f"  Snapshot: {len(snap['saved'])} Datei(en) gesichert", handle)

    started = time.time()
    result = run_agent(
        task, ROOT, model,
        max_turns=task.get("max_turns", 24),
        think=task.get("think", False),
        num_ctx=task.get("num_ctx", num_ctx),
        log=lambda message: log_line(message, handle),
    )
    elapsed = time.time() - started
    log_line(f"  Agent: {result.turns} Zuege, {elapsed:.0f}s, "
             f"{len(result.written)} Datei(en) geschrieben", handle)

    if result.error:
        log_line(f"  Agent-Fehler: {result.error}", handle)
        kept = keep_rejected(task_id, result.written)
        reverted = restore(snap, result.written)
        if kept:
            log_line(f"  Fassung aufbewahrt: {kept}", handle)
        return {"id": task_id, "status": "agent-fehler", "detail": result.error,
                "reverted": reverted, "aufbewahrt": str(kept) if kept else None,
                "seconds": round(elapsed)}

    if not result.written:
        log_line("  Keine Datei geaendert.", handle)
        return {"id": task_id, "status": "keine-aenderung",
                "detail": result.summary, "seconds": round(elapsed)}

    for command in task.get("post_steps", []):
        log_line(f"  post_step: {command}", handle)
        code, output = run_command(command)
        if code != 0:
            log_line(f"    fehlgeschlagen: {output.strip()[:300]}", handle)

    ok, detail = verify(task, handle)
    if not ok:
        kept = keep_rejected(task_id, result.written)
        reverted = restore(snap, result.written)
        log_line(f"  ABGELEHNT -> {len(reverted)} Datei(en) zurueckgerollt", handle)
        if kept:
            log_line(f"  Fassung zur Analyse aufbewahrt: {kept}", handle)
        return {"id": task_id, "status": "abgelehnt", "detail": detail,
                "written": result.written, "reverted": reverted,
                "aufbewahrt": str(kept) if kept else None, "seconds": round(elapsed)}

    log_line(f"  ANGENOMMEN: {', '.join(result.written)}", handle)
    return {"id": task_id, "status": "angenommen", "detail": result.summary,
            "written": result.written, "seconds": round(elapsed)}


# Was --recheck erneut ausfuehrt: die gepflegten Pruefskripte, Build, Test und die
# Kartenpruefung. Nicht dabei sind die Einzeiler aus der ersten Sitzung (H*, D1-D4) -
# Momentaufnahmen, teils fuer laengst geloeschte Projekte - und "dotnet restore",
# das mit Laufzeitkennung obj/ umschreibt.
RECHECK_PREFIXES = (
    "py tools/ollama-agent/checks/",
    "dotnet build ",
    "dotnet test ",
    "dotnet run --project tools/kartenpruefung",
    "dotnet run --project tools/spielablauf",
)

# Laeuft das Spiel, sperrt es seine .exe, und der Build von DesktopGL scheitert beim
# Kopieren (MSB3027/MSB3021) - das sagt nichts ueber den Code.
LOCKED_MARKERS = ("MSB3027", "MSB3021")


def recheck(tasks: list[dict]) -> int:
    """Fuehrt jede gepflegte Abnahme aus tasks.json einmal aus, ohne Agent und ohne Snapshot.

    Entstanden, nachdem drei Pruefungen (d7, d8a, e5) still gebrochen waren: eine
    spaetere Aufgabe hatte sie zu Recht ungueltig gemacht, aber niemand liess sie
    erneut laufen. Ein Fehlschlag heisst deshalb nicht, dass die alte Aufgabe falsch
    war - meist prueft die Abnahme eine Schreibweise oder einen Zustand statt einer
    Eigenschaft (README, Pruefskripte).
    """
    commands: dict[str, list[str]] = {}
    for task in tasks:
        for command in task.get("verify", []):
            if command.startswith(RECHECK_PREFIXES):
                commands.setdefault(command, []).append(task["id"])

    failed = locked = 0
    for command, ids in commands.items():
        code, output = run_command(command)
        if code == 0:
            log_line(f"  ok     {command}")
            continue
        if any(marker in output for marker in LOCKED_MARKERS):
            locked += 1
            log_line(f"  GESPERRT {command}   (die .exe ist in Benutzung - laeuft das Spiel?)")
            continue
        failed += 1
        log_line(f"  FEHLER {command}   (Aufgaben {', '.join(ids)})")
        for line in output.strip().splitlines()[-6:]:
            log_line(f"         {line}")
    gesperrt = f", {locked} gesperrt" if locked else ""
    log_line(f"{len(commands)} Abnahmen, {failed} fehlgeschlagen{gesperrt}")
    return 1 if failed else 0


def main() -> int:
    parser = argparse.ArgumentParser(description="Lokale Ollama-Agents auf TODO-Punkte ansetzen.")
    parser.add_argument("--tasks", nargs="*", help="Aufgaben-IDs, z.B. H3 D2")
    parser.add_argument("--auto", action="store_true", help="alle Aufgaben mit \"auto\": true")
    parser.add_argument("--list", action="store_true", help="Aufgaben auflisten")
    parser.add_argument("--model", default="qwen3.8:27b")
    parser.add_argument(
        "--num-ctx", type=int, default=262144,
        help="Kontextfenster. 262144 ist das Trainingsmaximum von qwen3.8:27b und passt mit "
             "q8_0-KV-Cache gerade noch ganz in 32 GB VRAM. Ist das Modell mit anderer Groesse "
             "geladen, laedt Ollama es neu.",
    )
    parser.add_argument("--dry-run", action="store_true")
    parser.add_argument(
        "--recheck", action="store_true",
        help="alle gepflegten Abnahmen aus tasks.json erneut ausfuehren, ohne Agent - "
             "findet Pruefungen, die eine spaetere Aufgabe gebrochen hat",
    )
    args = parser.parse_args()

    tasks = json.loads((HERE / "tasks.json").read_text(encoding="utf-8"))["tasks"]
    by_id = {task["id"]: task for task in tasks}

    if args.recheck:
        return recheck(tasks)

    if args.list:
        for task in tasks:
            flag = "auto" if task.get("auto") else "    "
            print(f"  [{flag}] {task['id']:4s} {task['title']}")
        return 0

    if args.auto:
        selected = [task for task in tasks if task.get("auto")]
    elif args.tasks:
        unknown = [t for t in args.tasks if t not in by_id]
        if unknown:
            print(f"Unbekannte Aufgaben-IDs: {', '.join(unknown)}", file=sys.stderr)
            return 2
        selected = [by_id[t] for t in args.tasks]
    else:
        parser.print_help()
        return 2

    LOG_DIR.mkdir(parents=True, exist_ok=True)
    stamp = datetime.now().strftime("%Y%m%d-%H%M%S")
    log_path = LOG_DIR / f"run-{stamp}.log"

    results = []
    with open(log_path, "w", encoding="utf-8") as handle:
        log_line(f"Modell: {args.model} · num_ctx {args.num_ctx} · Repo: {ROOT} · "
                 f"{len(selected)} Aufgabe(n)", handle)
        for task in selected:
            results.append(process(task, args.model, handle, args.dry_run, args.num_ctx))

        log_line("\n" + "=" * 60, handle)
        log_line("ZUSAMMENFASSUNG", handle)
        for entry in results:
            line = f"  {entry['id']:4s} {entry['status']}"
            if entry.get("seconds"):
                line += f"  ({entry['seconds']}s)"
            log_line(line, handle)
        log_line(f"\nLog: {log_path}", handle)

    (LOG_DIR / f"run-{stamp}.json").write_text(
        json.dumps(results, indent=2, ensure_ascii=False), encoding="utf-8"
    )
    return 0 if all(r["status"] in ("angenommen", "dry-run") for r in results) else 1


if __name__ == "__main__":
    sys.exit(main())
