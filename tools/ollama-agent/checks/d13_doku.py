"""Abnahme D13: Doku kennt E10, Startrohstoffe, Fischen, die neue Grafik und die Kartenpruefung."""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
fehler = []


def block(kopf: str) -> str:
    return "\n".join(abschnitt(lines, kopf))


wieder = block(r"## Wiederaufnahme")
for pflicht in ("E10", "C1r", "C1f", "G1", "kartenpruefung"):
    if pflicht not in wieder:
        fehler.append(f"Wiederaufnahme: {pflicht} fehlt")
if not re.search(r"Kampfschleife[\s\S]{0,200}zurückgestellt", wieder):
    fehler.append("Wiederaufnahme: Kampfschleife nicht als zurueckgestellt vermerkt")

pos = {k: [i for i, l in enumerate(lines) if re.match(k, l)]
       for k in (r"### E9 ", r"### E10 ", r"### Kein Fehler")}
if any(len(v) != 1 for v in pos.values()):
    fehler.append("E9, E10 und 'Kein Fehler' muessen je genau einmal als Ueberschrift da sein")
elif not pos[r"### E9 "][0] < pos[r"### E10 "][0] < pos[r"### Kein Fehler"][0]:
    fehler.append("E10 steht nicht zwischen E9 und 'Kein Fehler'")
e10 = block(r"### E10 ")
for pflicht in ("StampResource", "557", "kartenpruefung"):
    if pflicht not in e10:
        fehler.append(f"E10 nennt {pflicht} nicht")

c1 = block(r"### C1 ")
for punkt in ("Startrohstoffe", "Fisch"):
    if not re.search(rf"^\s*- \[x\][^\n]*{punkt}", c1, re.MULTILINE):
        fehler.append(f"C1: erledigter Punkt '{punkt}' fehlt")
c7 = block(r"### C7 ")
if re.search(r"^\s*- \[ \][^\n]*Wald", c7, re.MULTILINE):
    fehler.append("C7: der Wald-Punkt ist noch offen")
if not re.search(r"^\s*- \[x\][^\n]*Wasser", c7, re.MULTILINE):
    fehler.append("C7: erledigter Punkt zum Wasser fehlt")
if "E10" not in block(r"# Erledigt"):
    fehler.append("Erledigt-Liste nennt E10 nicht")

struktur_text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
if "tools/kartenpruefung" not in struktur_text:
    fehler.append("PROJEKT_STRUKTUR.md nennt tools/kartenpruefung nicht")

fehler += struktur(lines, min_erledigt=85, min_offen=19, min_zeilen=560)
melde(fehler, "D13 erfuellt: Doku auf dem Stand nach E10, C1r, C1f und G1-G4")
