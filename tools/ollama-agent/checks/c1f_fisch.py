"""Abnahme C1f: Fischschwaerme an der Kueste werden angelegt und gezeichnet.

Ob die Karten stimmen - jeder Fisch im Wasser, mit Ufer daneben, auf fast
jeder Karte welcher -, prueft tools/kartenpruefung (Regel 'fisch'). Hier geht
es um die Stellen im Code, vor allem um die Grafik.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import SCREEN, lies, melde, methode  # noqa: E402

DATA = Path("AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data")
tile = (DATA / "Tile.cs").read_text(encoding="utf-8-sig")
karte = (DATA / "TileMap.cs").read_text(encoding="utf-8-sig")
screen = lies(SCREEN)
fehler = []

enum = re.search(r"enum\s+FoodSource\s*\{([^}]*)\}", tile)
if not enum or "Fish" not in enum.group(1):
    fehler.append("Tile.cs: FoodSource kennt Fish nicht")

fisch = methode(karte, "PlaceFish") or ""
for muster, grund in ((r"FoodSource\.Fish", "Food = FoodSource.Fish"), (r"ResourceAmount\s*=\s*200", "Menge 200"),
                      (r"Resource\.Food", "Ressource Nahrung"), (r"IsCoast\s*\(", "nur an der Kueste")):
    if not re.search(muster, fisch):
        fehler.append(f"PlaceFish: {grund} fehlt")
if methode(karte, "IsCoast") is None:
    fehler.append("Methode IsCoast fehlt")
flecken = methode(karte, "AddResourcePatches") or ""
strand, fische = flecken.find("AddBeaches("), flecken.find("PlaceFish(")
if fische < 0 or strand < 0 or fische < strand:
    fehler.append("AddResourcePatches ruft PlaceFish nicht nach AddBeaches auf")

zeichnen = methode(screen, "DrawTileMap") or ""
if not re.search(r"isFish\s*=\s*tile\.Food\s*==\s*FoodSource\.Fish", zeichnen):
    fehler.append("DrawTileMap unterscheidet Fisch nicht")
if not re.search(r"!\s*isFish\s*&&\s*tileTex\.TryGetValue\(\s*TileType\.Grassland", zeichnen):
    fehler.append("DrawTileMap legt unter den Fisch noch Gras - er muss auf dem Wasser schwimmen")
if "BuildFishTextureCached" not in zeichnen:
    fehler.append("DrawTileMap zeichnet keine Fischtextur")
if methode(screen, "BuildFishTexture") is None:
    fehler.append("Methode BuildFishTexture fehlt")

melde(fehler, "C1f erfuellt: Fischschwaerme angelegt und gezeichnet")
