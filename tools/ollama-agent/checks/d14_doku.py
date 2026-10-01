"""Abnahme D14: Doku kennt C1t und C2 - Bevoelkerungsgrenze, Ausbildung, Haeuser.

Testzahlen und Zeilenzahlen werden gemessen, nicht abgeschrieben. Die Pruefung
sichert den Stand nach D14; eine spaetere Doku-Aufgabe darf sie brechen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur, testzahlen  # noqa: E402

lines = lies()
fehler = []


def block(kopf: str) -> str:
    return "\n".join(abschnitt(lines, kopf))


wieder = block(r"## Wiederaufnahme")
for pflicht in ("C2", "C1t", "Q", "Training.cs", "C5"):
    if pflicht not in wieder:
        fehler.append(f"Wiederaufnahme: {pflicht} fehlt")
if not re.search(r"Taste\s+H", wieder):
    fehler.append("Wiederaufnahme: Haeuser per Taste H nicht erwaehnt")
if "230012" not in wieder:
    fehler.append("Wiederaufnahme: Screenshot 230012 nicht erwaehnt")

c1 = block(r"### C1 ")
if re.search(r"^\s*- \[ \][^\n]*(beladen|Traglast)", c1, re.MULTILINE):
    fehler.append("C1: der Traglast-Punkt ist noch offen")
c2 = block(r"### C2 ")
if re.search(r"^\s*- \[ \]", c2, re.MULTILINE):
    fehler.append("C2: es ist noch ein Punkt offen")
if len(re.findall(r"^\s*- \[x\]", c2, re.MULTILINE)) < 3:
    fehler.append("C2: weniger als drei erledigte Punkte")
if not re.search(r"Original[^\n]*\b50\b", c2):
    fehler.append("C2: Hinweis fehlt, dass ein Dorfbewohner im Original 50 Nahrung kostet")
if not re.search(r"^\s*- \[x\][^\n]*Bevölkerung rot", block(r"### C6 "), re.MULTILINE):
    fehler.append("C6: 'Bevölkerung rot' nicht abgehakt")
if "C2" not in block(r"# Erledigt"):
    fehler.append("Erledigt-Liste nennt C2 nicht")

zahlen = testzahlen()
test_zeile = next((l for l in lines if l.startswith("| `tests/AoE.Tests`")), "")
for name, wort in (("TrainingTests.cs", "Ausbildung"), ("GatherJobTests.cs", "Sammelauftrag")):
    if not re.search(rf"{wort}[^|(]*\({zahlen[name]}\)", test_zeile):
        fehler.append(f"Ist-Zustand: {wort} ({zahlen[name]}) fehlt in der Testzeile (gezaehlt)")

struktur_text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
training = "src/AoE.Core/Economy/Training.cs"
soll = len(Path(training).read_text(encoding="utf-8-sig").splitlines())
zeile = next((l for l in struktur_text.splitlines() if l.startswith("|") and f"| {training} |" in l), "")
if not re.search(rf"\b{soll} Zeilen\b", zeile):
    fehler.append(f"PROJEKT_STRUKTUR.md: Tabellenzeile fuer Training.cs mit {soll} Zeilen fehlt (gemessen)")
anbindung = struktur_text.split("## Anbindung an AoE.Core", 1)[-1].split("Angebunden, aber nie aufgerufen", 1)[0]
for pflicht in ("Population", "TrainingQueue"):
    if pflicht not in anbindung:
        fehler.append(f"PROJEKT_STRUKTUR.md, Anbindung: {pflicht} fehlt")

fehler += struktur(lines, min_erledigt=95, min_offen=18, min_zeilen=585)
melde(fehler, "D14 erfuellt: Doku auf dem Stand nach C1t und C2")
