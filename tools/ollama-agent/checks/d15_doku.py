"""Abnahme D15: Doku kennt C5 - Bauregeln, Baumenue, Baustellen, Lager, Grafik.

Testzahlen und Zeilenzahlen werden gemessen, nicht abgeschrieben. Die Pruefung
sichert den Stand nach D15; eine spaetere Doku-Aufgabe darf sie brechen.
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
for pflicht in ("C5", "Construction.cs", "Mühle", "Holzfällerlager", "Bergbaulager"):
    if pflicht not in wieder:
        fehler.append(f"Wiederaufnahme: {pflicht} fehlt")
if re.search(r"^\d\.\s+\*\*C5 Bauen\*\*", wieder, re.MULTILINE):
    fehler.append("Wiederaufnahme: C5 steht noch als naechster Schritt da")
if re.search(r"sofort\s+fertig", wieder):
    fehler.append("Wiederaufnahme: Haeuser stehen laut Text noch sofort")

if re.search(r"bisher\s+steht\s+nur\s+das\s+Stadtzentrum", block(r"### C1 ")):
    fehler.append("C1: Abgabestellen-Punkt kennt die Lager nicht")
if re.search(r"steht\s+sofort", block(r"### C2 ")):
    fehler.append("C2: das Haus steht laut Text noch sofort")

c5 = block(r"### C5 ")
if not re.match(r"### C5 [^\n]*erledigt", c5):
    fehler.append("C5: Ueberschrift nicht als erledigt markiert")
if len(re.findall(r"^\s*- \[x\]", c5, re.MULTILINE)) < 5:
    fehler.append("C5: weniger als fuenf erledigte Punkte")
if not re.search(r"\(n\s*\+\s*2\)\s*/\s*3", c5):
    fehler.append("C5: die Formel fuer den abnehmenden Ertrag fehlt")
if re.search(r"^\s*- \[ \]\s*Bauzeiten", c5, re.MULTILINE):
    fehler.append("C5: Bauzeiten noch offen")
if "C5" not in block(r"# Erledigt"):
    fehler.append("Erledigt-Liste nennt C5 nicht")

zahlen = testzahlen()
test_zeile = next((l for l in lines if l.startswith("| `tests/AoE.Tests`")), "")
if not re.search(rf"Bauen[^|(]*\({zahlen['ConstructionTests.cs']}\)", test_zeile):
    fehler.append(f"Ist-Zustand: Bauen ({zahlen['ConstructionTests.cs']}) fehlt in der Testzeile (gezaehlt)")

struktur_text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
for pfad in ("src/AoE.Core/Economy/Construction.cs", "src/AoE.Core/Entities/Buildings.cs"):
    soll = len(Path(pfad).read_text(encoding="utf-8-sig").splitlines())
    zeile = next((l for l in struktur_text.splitlines() if l.startswith("|") and f"| {pfad} |" in l), "")
    if not re.search(rf"\b{soll} Zeilen\b", zeile):
        fehler.append(f"PROJEKT_STRUKTUR.md: Tabellenzeile fuer {pfad} mit {soll} Zeilen fehlt (gemessen)")
anbindung = struktur_text.split("## Anbindung an AoE.Core", 1)[-1].split("Angebunden, aber nie aufgerufen", 1)[0]
for pflicht in ("BuildingRules", "Construction"):
    if pflicht not in anbindung:
        fehler.append(f"PROJEKT_STRUKTUR.md, Anbindung: {pflicht} fehlt")

fehler += struktur(lines, min_erledigt=100, min_offen=18, min_zeilen=610)
melde(fehler, "D15 erfuellt: Doku auf dem Stand nach C5")
