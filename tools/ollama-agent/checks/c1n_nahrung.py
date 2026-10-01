"""Abnahme C1n: Schafe 100, Beeren 125, das Schaf wird am Merkmal erkannt statt an der Menge."""

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
if not enum or not all(w in enum.group(1) for w in ("None", "Sheep", "Berries")):
    fehler.append("Tile.cs: enum FoodSource { None, Sheep, Berries } fehlt")
if not re.search(r"public\s+FoodSource\s+Food\s*\{\s*get;\s*set;\s*\}", tile):
    fehler.append("Tile.cs: Eigenschaft 'public FoodSource Food { get; set; }' fehlt")

for name, menge, art in (("PlaceSheep", 100, "Sheep"), ("PlaceBerryBushes", 125, "Berries")):
    rumpf = methode(karte, name) or ""
    if not re.search(rf"ResourceAmount\s*=\s*{menge}\s*;", rumpf):
        fehler.append(f"{name}: ResourceAmount = {menge} fehlt")
    if not re.search(rf"Food\s*=\s*FoodSource\.{art}", rumpf):
        fehler.append(f"{name}: Food = FoodSource.{art} fehlt")

for name in ("ClearResource", "ClearStartArea"):
    rumpf = methode(karte, name)
    if rumpf is not None and not re.search(r"Food\s*=\s*FoodSource\.None", rumpf):
        fehler.append(f"{name}: Food = FoodSource.None fehlt - geraeumte Kachel behielte ihr Schaf")

if re.search(r"ResourceAmount\s*>=\s*1\s*&&\s*tile\.ResourceAmount\s*<=\s*2", screen):
    fehler.append("RTSGameplayScreen: Schaf wird noch an der Menge erkannt")
if not re.search(r"isSheep\s*=\s*tile\.Food\s*==\s*FoodSource\.Sheep", screen):
    fehler.append("RTSGameplayScreen: isSheep = tile.Food == FoodSource.Sheep fehlt")

melde(fehler, "C1n erfuellt: Nahrungsmengen laut Spezifikation, Schaf am Merkmal erkannt")
