"""Abnahme D8a/D8b: TODO.md gibt den Stand nach C1, C10, E5 und E6 wieder.

Aufruf: py d8_todo.py a   (Wiederaufnahme, Ist-Zustand, Erledigt, Notizen)
        py d8_todo.py b   (Bloecke E, B6, C1, C3, C8, C10)

Jede Haelfte prueft nur ihre eigenen Abschnitte, damit D8a nicht an Dingen
scheitert, die erst D8b aendert.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur, testzahlen  # noqa: E402

teil = sys.argv[1] if len(sys.argv) > 1 else ""
lines = lies()
text = "\n".join(lines)
fehler = []


def block(kopf: str) -> str:
    return "\n".join(abschnitt(lines, kopf))


def weg(muster: str, grund: str, wo: str = None) -> None:
    ziel = text if wo is None else wo
    if re.search(muster.replace(" ", r"\s+"), ziel, re.IGNORECASE):
        fehler.append(f"veraltet noch drin: /{muster}/ ({grund})")


def da(muster: str, grund: str, wo: str = None) -> None:
    ziel = text if wo is None else wo
    if not re.search(muster.replace(" ", r"\s+"), ziel, re.IGNORECASE):
        fehler.append(f"fehlt: /{muster}/ ({grund})")


if teil == "a":
    wieder = block(r"## Wiederaufnahme")
    # Die Testzahl wird gezaehlt - bei D8a waren es 41, jeder neue Test aendert sie.
    n = sum(testzahlen().values())
    weg(r"26/26", f"es sind {n} Tests")
    weg(r"26 unit-tests", f"es sind {n} Tests")
    da(rf"dotnet test[^\n]*\n?[^\n]*{n}/{n}", "Testzeile der Wiederaufnahme", wieder)
    zeile = next((l for l in lines if l.startswith("| `tests/AoE.Tests`")), "")
    # Auch die Zahl je Datei wird gezaehlt: fest "Sammelauftrag (15)" brach, als
    # C1t drei Faelle zu GatherJobTests.cs hinzufuegte.
    sammeln = testzahlen()["GatherJobTests.cs"]
    if f"{n}/{n}" not in zeile or f"Sammelauftrag ({sammeln})" not in zeile:
        fehler.append(f"Tabellenzeile tests/AoE.Tests: {n}/{n} und 'Sammelauftrag ({sammeln})' erwartet")
    for pflicht in ("GatherJob", "TileMapGatherWorld", "Untätig", "Sichtprüfung", "E5", "E6", "125"):
        da(re.escape(pflicht), "Wiederaufnahme nennt den neuen Stand", wieder)
    weg(r"\*\*C1 Dorfbewohner-Loop\*\* — der Punkt mit der größten Wirkung", "C1 ist erledigt", wieder)
    # Bis 2026-10-03 hieß das „Stand vom 2026-09-30 nicht committet" - seit den
    # Commits des Nutzers falsch. Verlangt wird nur noch, dass die Wiederaufnahme
    # den Versionsstand nennt: committet mit Hash, oder ausdrücklich nicht.
    da(r"(committet[^\n]*\n?[^\n]*`[0-9a-f]{7}`|`[0-9a-f]{7}`[^\n]*\n?[^\n]*committet"
       r"|unversioniert|nicht committet|im Arbeitsbaum)",
       "Hinweis auf den Versionsstand (Commit-Hash oder 'nicht committet')", wieder)
    da(r"qwen3\.8:27b", "Modell der lokalen Agents")
    da(rf"{n} Unit-Tests", "Erledigt-Liste", block(r"# Erledigt"))
    da(r"Dorfbewohner-Loop \(C1\)", "Erledigt-Liste", block(r"# Erledigt"))
    weg(r"farbige 32×32-Quadrate", "gezeichnet wird prozedurale Pixelart")
    fehler += struktur(lines, min_erledigt=52, min_offen=30, min_zeilen=430)
    melde(fehler, "D8a erfuellt: Wiederaufnahme und Ueberblick auf dem Stand vom 2026-09-30")

elif teil == "b":
    # E5 und E6 zwischen E4 und dem Beruhigungs-Abschnitt, je genau einmal
    pos = {k: [i for i, l in enumerate(lines) if re.match(k, l)]
           for k in (r"### E4 ", r"### E5 ", r"### E6 ", r"### Kein Fehler")}
    if any(len(v) != 1 for v in pos.values()):
        fehler.append("E4, E5, E6 und 'Kein Fehler' muessen je genau einmal als Ueberschrift da sein")
    elif not pos[r"### E4 "][0] < pos[r"### E5 "][0] < pos[r"### E6 "][0] < pos[r"### Kein Fehler"][0]:
        fehler.append("Reihenfolge E4 < E5 < E6 < 'Kein Fehler' stimmt nicht")
    e = block(r"# Block E")
    weg(r"Alle vier behoben", "es sind jetzt sechs", e)
    da(r"ClearStartArea", "E5 nennt die Loesung", block(r"### E5 "))
    da(r"800", "E6 nennt die Geschwindigkeit", block(r"### E6 "))
    weg(r"TileMap\.cs:\d+", "Zeilenverweise veralten - Klasse beim Namen nennen")

    c1 = block(r"### C1 ")
    if len(re.findall(r"^\s*- \[x\]", c1, re.MULTILINE)) < 4:
        fehler.append("C1: die vier Punkte sind nicht abgehakt")
    for pflicht in ("GatherJob", "TileMapGatherWorld", r"70\s?%", "Sichtprüfung", "Traglast"):
        da(pflicht, "C1-Beschreibung", c1)
    if not re.search(r"^\s*- \[ \][^\n]*Sichtprüfung", c1, re.MULTILINE):
        fehler.append("C1: offene Checkbox fuer die Sichtpruefung fehlt")

    for kopf, name in ((r"### C3 ", "C3"), (r"### C8 ", "C8")):
        rumpf = block(kopf)
        if re.search(r"^\s*- \[ \]", rumpf, re.MULTILINE):
            fehler.append(f"{name}: es ist noch ein Punkt offen")
    if not re.search(r"^\s*- \[x\][^\n]*Kantenscrollen", block(r"### C10 "), re.MULTILINE):
        fehler.append("C10: Kantenscrollen nicht abgehakt")
    fehler += struktur(lines, min_erledigt=60, min_offen=25, min_zeilen=440)
    melde(fehler, "D8b erfuellt: Bloecke E, B6, C1, C3, C8, C10 auf dem Stand vom 2026-09-30")

else:
    sys.exit("Aufruf: py d8_todo.py a|b")
