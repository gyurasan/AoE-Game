"""Abnahme E9: das Stadtzentrum wird im Ganzen mit dem Zoom skaliert.

Vorher hatten Haus, Dach, Tuer, Fenster und Tuerme feste Pixelmasse; nur die
Grundflaeche wuchs mit. Bei Zoom 2 war das Haus ein kleiner Fleck im grossen
Erdplatz (Screenshot 2026-09-30).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

teil = methode(text, "BuildingPart")
if teil is None:
    fehler.append("Methode BuildingPart fehlt")
else:
    if "128" not in teil:
        fehler.append("BuildingPart rechnet nicht vom 128er-Entwurf aus")
    if "Math.Max(1" not in teil.replace(" ", ""):
        fehler.append("BuildingPart sichert keine Mindestgroesse von 1 Pixel")

gebaeude = methode(text, "DrawBuilding") or ""
if len(re.findall(r"BuildingPart\s*\(", gebaeude)) < 9:
    fehler.append("DrawBuilding zeichnet nicht alle Teile ueber BuildingPart")
if re.search(r"\bhouse\.", gebaeude):
    fehler.append("DrawBuilding rechnet noch mit festen Pixeln relativ zu 'house'")
if len(re.findall(r"new\s+Rectangle\s*\(", gebaeude)) > 1:
    fehler.append("DrawBuilding legt noch Rechtecke mit festen Pixelmassen an")
if "path.Height" in gebaeude:
    fehler.append("die tote Pfad-Bedingung (negative Hoehe) ist noch da")
if not re.search(r"BuildingPart\s*\(\s*rect\s*,\s*56\s*,\s*104", gebaeude):
    fehler.append("der Pfad vor der Tuer wird nicht gezeichnet")

melde(fehler, "E9 erfuellt: Stadtzentrum skaliert mit dem Zoom")
