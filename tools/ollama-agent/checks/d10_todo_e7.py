"""Abnahme D10: TODO.md kennt E7 und den wahren Stand von C7."""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
text = "\n".join(lines)
fehler = []

pos = {k: [i for i, l in enumerate(lines) if re.match(k, l)]
       for k in (r"### E6 ", r"### E7 ", r"### Kein Fehler")}
if any(len(v) != 1 for v in pos.values()):
    fehler.append("E6, E7 und 'Kein Fehler' muessen je genau einmal als Ueberschrift da sein")
elif not pos[r"### E6 "][0] < pos[r"### E7 "][0] < pos[r"### Kein Fehler"][0]:
    fehler.append("E7 steht nicht zwischen E6 und 'Kein Fehler'")
e7 = "\n".join(abschnitt(lines, r"### E7 "))
for pflicht in ("TileScreenRect", "GridToWorld", "halbe Kachel"):
    if pflicht not in e7:
        fehler.append(f"E7-Abschnitt nennt {pflicht} nicht")
if not re.search(r"^\s*- \[x\]", e7, re.MULTILINE):
    fehler.append("E7: keine erledigte Checkbox")

if not re.search(r"E5\s+bis\s+E7", "\n".join(abschnitt(lines, r"# Block E"))):
    fehler.append("Einleitung von Block E nennt nicht 'E5 bis E7'")
wieder = "\n".join(abschnitt(lines, r"## Wiederaufnahme"))
# Bei D10 stand E7 zweimal da: erledigt und noch ohne Sichtpruefung. Seit dem
# Screenshot vom 2026-09-30 ist die Sichtpruefung erbracht - einmal genuegt.
if not re.search(r"\bE7\b", wieder):
    fehler.append("Wiederaufnahme nennt E7 nicht")

c7 = "\n".join(abschnitt(lines, r"### C7 "))
for punkt in ("Bäume", "Goldminen"):
    if not re.search(rf"^\s*- \[x\][^\n]*{punkt}", c7, re.MULTILINE):
        fehler.append(f"C7: der Punkt mit '{punkt}' ist nicht abgehakt - die Texturen gibt es laengst")
# Bei D10 war der Punkt offen, seit E9 ist er erledigt - beides ist richtig,
# solange er dasteht.
if not re.search(r"^\s*- \[[ x]\][^\n]*Stadtzentrum", c7, re.MULTILINE):
    fehler.append("C7: Checkbox zum Stadtzentrum und Zoom fehlt")

fehler += struktur(lines, min_erledigt=65, min_offen=25, min_zeilen=450)
melde(fehler, "D10 erfuellt: E7 dokumentiert, C7 auf dem wahren Stand")
