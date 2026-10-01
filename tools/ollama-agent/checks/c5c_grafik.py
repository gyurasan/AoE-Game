"""Abnahme C5c: Baustellen und die neuen Gebaeude haben eigene Grafik.

Prueft nur, dass die Zeichenwege bestehen und mit dem Zoom skalieren. Wie es
aussieht, zeigt die Vorschau (c5c_vorschau.py im Scratchpad) und dann ein Screenshot.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

fehler = []
screen = lies()


def rumpf(name: str) -> str:
    r = methode(screen, name)
    if r is None:
        fehler.append(f"Methode {name} fehlt")
        return ""
    return r


gebaeude = rumpf("DrawBuilding")
if not re.search(r"!\s*b\.IsComplete[\s\S]*?DrawConstructionSite\s*\([^;]*\.Construction\.Progress", gebaeude):
    fehler.append("DrawBuilding zeichnet Baustellen nicht mit ihrem Fortschritt")
for typ, methode_name in (("House", "DrawHouse"), ("Mill", "DrawMill"),
                          ("LumberCamp", "DrawLumberCamp"), ("MiningCamp", "DrawMiningCamp")):
    if not re.search(rf"BuildingType\.{typ}\b[\s\S]{{0,80}}?{methode_name}\s*\(", gebaeude):
        fehler.append(f"DrawBuilding ruft {methode_name} fuer {typ} nicht auf")
# Baustellen vor allem anderen: eine unfertige Muehle darf nicht als Muehle erscheinen
i_bau, i_haus = gebaeude.find("DrawConstructionSite"), gebaeude.find("DrawHouse")
if i_bau >= 0 and i_haus >= 0 and i_haus < i_bau:
    fehler.append("DrawBuilding prueft die Baustelle erst nach den fertigen Gebaeuden")

for name in ("DrawConstructionSite", "DrawMill", "DrawShed", "DrawLumberCamp", "DrawMiningCamp"):
    if not re.search(r"BuildingPart\s*\(", rumpf(name)):
        fehler.append(f"{name} skaliert nicht mit dem Zoom (BuildingPart)")
baustelle = rumpf("DrawConstructionSite")
if len(re.findall(r"\bprogress\b", baustelle)) < 2:
    fehler.append("DrawConstructionSite nutzt den Fortschritt nicht fuer Mauern und Balken")
for name in ("DrawLumberCamp", "DrawMiningCamp"):
    if not re.search(r"DrawShed\s*\(", rumpf(name)):
        fehler.append(f"{name} zeichnet den Unterstand nicht (DrawShed)")

melde(fehler, "C5c erfuellt: Baustelle mit Fortschritt, Muehle und Lager mit eigener Grafik")
