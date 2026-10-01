"""Abnahme D12: TODO.md kennt E8, E9 und was der Screenshot vom 2026-09-30 bestaetigt hat."""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
fehler = []


def block(kopf: str) -> str:
    return "\n".join(abschnitt(lines, kopf))


pos = {k: [i for i, l in enumerate(lines) if re.match(k, l)]
       for k in (r"### E7 ", r"### E8 ", r"### E9 ", r"### Kein Fehler")}
if any(len(v) != 1 for v in pos.values()):
    fehler.append("E7, E8, E9 und 'Kein Fehler' muessen je genau einmal als Ueberschrift da sein")
elif not pos[r"### E7 "][0] < pos[r"### E8 "][0] < pos[r"### E9 "][0] < pos[r"### Kein Fehler"][0]:
    fehler.append("Reihenfolge E7 < E8 < E9 < 'Kein Fehler' stimmt nicht")
if "UnitWorldRect" not in block(r"### E8 "):
    fehler.append("E8 nennt UnitWorldRect nicht")
if "BuildingPart" not in block(r"### E9 "):
    fehler.append("E9 nennt BuildingPart nicht")
if not re.search(r"E8\s+und\s+E9", block(r"# Block E")):
    fehler.append("Einleitung von Block E nennt E8 und E9 nicht")

c7 = block(r"### C7 ")
if not re.search(r"^\s*- \[x\][^\n]*Stadtzentrum", c7, re.MULTILINE):
    fehler.append("C7: Stadtzentrum nicht abgehakt")
# Bei D12 war der Wald-Punkt offen, seit G1/G3 ist er erledigt - beides richtig
if not re.search(r"^\s*- \[[ x]\][^\n]*Wald", c7, re.MULTILINE):
    fehler.append("C7: Punkt zur Wald-Grafik fehlt")

wieder = block(r"## Wiederaufnahme")
for pflicht in ("224900", "E8", "E9", "Wald"):
    if pflicht not in wieder:
        fehler.append(f"Wiederaufnahme: {pflicht} fehlt")
for alt in (r"Screenshots vom Sammeln und vom Nebel fehlen", r"\*\*Stadtzentrum-Grafik\*\*"):
    if re.search(alt.replace(" ", r"\s+"), wieder):
        fehler.append(f"Wiederaufnahme: ueberholt: /{alt}/")

erledigt = block(r"# Erledigt")
if "E8" not in erledigt or "E9" not in erledigt:
    fehler.append("Erledigt-Liste nennt E8 und E9 nicht")

fehler += struktur(lines, min_erledigt=78, min_offen=25, min_zeilen=525)
melde(fehler, "D12 erfuellt: E8, E9 und die Sichtpruefung dokumentiert")
