"""Abnahme G13: hohes Gras als stehende Büschel, B2: Beerenbüsche als Sprites.

Der Nutzer 2026-10-09: Gras kann auch hüfthoch sein, etwa ein Drittel so hoch wie ein
Dorfbewohner - eine flache Bodentextur zeigt keine Höhe. Jetzt stehen in den Flecken mit
hohem Gras (TallGrassAt) Büschel als eigene Einträge der Tiefenschicht, etwa 8 Welteinheiten
hoch (ein Dorfbewohner 24), sie wiegen sich im Wind; wer dahinter steht, steckt bis zur Hüfte
im Gras. Dazu die Beerenbüsche in hoher Qualität (Gruppe beeren) statt des gezeichneten
Kachelbilds, ebenfalls stehend in der Tiefenschicht.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
cs = lies()
wind = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/Wind.cs"))
fehler = []


def bilder(feld, praefix):
    reihe = re.search(rf"{feld}\s*=\s*\{{([^}}]*)\}}", cs)
    namen = re.findall(rf'"({praefix}/\w+)"', reihe.group(1)) if reihe else []
    if len(namen) < 3:
        fehler.append(f"{feld} nennt nur {len(namen)} Bilder")
    for n in namen:
        if not (CONTENT / f"{n}.png").is_file():
            fehler.append(f"{n}.png fehlt")
        if f"#begin {n}.png" not in mgcb:
            fehler.append(f"{n}.png steht nicht in AgeOfEvolutions.mgcb")


bilder("TuftAssets", "Gras")
bilder("BerryAssets", "Nahrung")

hoehe = re.search(r"const\s+float\s+TUFT_HEIGHT\s*=\s*([\d.]+)f", cs)
if not hoehe or not 6 <= float(hoehe.group(1)) <= 10:
    fehler.append("TUFT_HEIGHT fehlt oder ist nicht etwa ein Drittel eines Dorfbewohners (6 bis 10)")


def rumpf(name, *pflichten):
    r = methode(cs, name)
    if r is None or "NotImplementedException" in r:
        fehler.append(f"{name} fehlt oder ist leer")
        return ""
    for pflicht, grund in pflichten:
        if not re.search(pflicht, r):
            fehler.append(f"{name}: {grund}")
    return r


rumpf("TuftSpot", (r"73856093", "nicht fest aus dem Ortshash"))
rumpf("DrawGrassTuft", (r"\bTuftSpot\s*\(", "fragt TuftSpot nicht"), (r"\bDrawTuftAt\s*\(", "zeichnet nicht mit DrawTuftAt"))
rumpf("DrawTuftAt", (r"\bWind\.TreeSway\s*\(", "wiegt sich nicht im Wind"),
      (r"\bWind\.SwayStrips\s*\([^;]*\bsway\s*,", "gibt SwayStrips keine Streifenzahl"),
      (r"\bTUFT_HEIGHT\b", "Höhe nicht aus TUFT_HEIGHT"), (r"\bTUFT_MIN_PIXELS\b", "keine Mindestgröße (TUFT_MIN_PIXELS)"),
      (r"\bcameraZoom\b", "wächst nicht mit dem Zoom"), (r"\bscreenBounds\b", "cullt nicht am Fenster"),
      (r"\balpha\b", "keine Deckkraft (alpha)"))
rumpf("DrawGrassAroundFeet", (r"\bTallGrassAt\s*\(", "fragt TallGrassAt nicht"),
      (r"\bDrawTuftAt\s*\(", "zeichnet nicht mit DrawTuftAt"))
rumpf("BerryFoot", (r"TileScreenRect\s*\(", "nicht in der Kachel"))
rumpf("DrawBerryBush", (r"\bBerryFoot\s*\(", "steht nicht auf BerryFoot"), (r"\bBERRY_WIDTH\b", "Breite nicht aus BERRY_WIDTH"))

einheiten = methode(cs, "DrawUnits") or ""
for pflicht, grund in ((r"\bDrawGrassTuft\s*\(", "zeichnet keine Büschel"),
                       (r"\bTallGrassAt\s*\(", "stellt die Büschel nicht nach TallGrassAt"),
                       (r"\bTUFTS_PER_TILE\b", "Anzahl der Büschel nicht aus TUFTS_PER_TILE"),
                       (r"\bDrawBerryBush\s*\(", "zeichnet keine Beerenbüsche"),
                       (r"\bBerryFoot\s*\(", "sortiert Beerenbüsche nicht nach ihrem Fuß"),
                       (r"\bDrawGrassAroundFeet\s*\(\s*spriteBatch\s*,\s*unit\.Position",
                        "lässt Figuren nicht im hohen Gras stecken (DrawGrassAroundFeet)")):
    if not re.search(pflicht, einheiten):
        fehler.append(f"DrawUnits {grund}")
karte = methode(cs, "DrawTileMap") or ""
if not re.search(r"FoodSource\.Berries[^;]*_berrySprites\.Length|_berrySprites\.Length[^;]*FoodSource\.Berries", karte):
    fehler.append("DrawTileMap zeichnet den gezeichneten Beerenbusch noch, obwohl die Bilder geladen sind")

streifen = methode(wind, "SwayStrips") or ""
if not re.search(r"SwayStrips\s*\(\s*Rectangle\s+target\s*,\s*int\s+texWidth\s*,\s*int\s+texHeight\s*,\s*float\s+sway\s*,\s*int\s+strips\s*=\s*TREE_STRIPS",
                 wind):
    fehler.append("Wind.SwayStrips hat keinen Parameter strips (Vorgabe TREE_STRIPS)")
elif re.search(r"\bTREE_STRIPS\b", streifen):
    fehler.append("Wind.SwayStrips rechnet im Rumpf noch mit TREE_STRIPS statt strips")

melde(fehler, "G13/B2 erfuellt: Büschel hohes Gras stehen in der Tiefenschicht, Beerenbüsche als Sprites")
