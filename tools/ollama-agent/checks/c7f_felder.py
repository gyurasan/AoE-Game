"""Abnahme C7f: Felder aus Qwen-Image - Acker, Weizen, der sichtbar wächst, Rand ums Feld.

Wie der Weizen beim Ernten und Nachwachsen aussieht, rechnet WheatLook; das
prüft tools/spielablauf, Gruppe feld. Hier: Bilder, Inhaltsliste, dass
DrawGround die Felder zeichnet und der alte Weg sie nicht noch einmal
übermalt, und dass 175 und 100 nur noch als Konstanten in TileMap stehen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
KARTE = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs")

text = lies()
karte = lies(KARTE)
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for name in ("acker", "weizen"):
    if not (CONTENT / f"Boden/{name}.png").is_file():
        fehler.append(f"Bild Boden/{name}.png fehlt")
    if f"/build:Boden/{name}.png" not in mgcb:
        fehler.append(f"Boden/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Boden/{name}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Boden/{name} nicht")

boden = methode(text, "DrawGround") or ""
if not re.search(r"tile\.Farm\b[^;]*\)\s*DrawField\s*\(", boden):
    fehler.append("DrawGround zeichnet Feldkacheln nicht mit DrawField")

feld = methode(text, "DrawField") or ""
if "WheatLook(" not in feld:
    fehler.append("DrawField nimmt Deckkraft und Farbe des Weizens nicht aus WheatLook")
if len(re.findall(r"IsField\s*\(", feld)) < 4:
    fehler.append("DrawField prüft nicht alle vier Kanten auf den Feldrand")

aussehen = methode(text, "WheatLook") or ""
for pflicht in ("FARM_FOOD", "FARM_REGROW_SECONDS", "FarmRegrow", "ResourceAmount"):
    if pflicht not in aussehen:
        fehler.append(f"WheatLook: {pflicht} fehlt")

nahrung = methode(text, "DrawTileMap") or ""
if not re.search(r"isFarm\s*&&\s*_soilTex\s*!=\s*null\s*&&\s*_wheatTex\s*!=\s*null\)\s*continue", nahrung):
    fehler.append("DrawTileMap übermalt die Felder noch mit der alten Farmtextur")

for konstante in (r"const\s+int\s+FARM_FOOD\s*=\s*175", r"const\s+float\s+FARM_REGROW_SECONDS\s*=\s*100f"):
    if not re.search(konstante, karte):
        fehler.append(f"TileMap: {konstante} fehlt")
for name in ("PlantCrop", "RegrowCrop"):
    rumpf = methode(karte, name) or ""
    if re.search(r"\b175\b|\b100f\b", rumpf):
        fehler.append(f"TileMap.{name} rechnet noch mit 175 oder 100f statt mit den Konstanten")

melde(fehler, "C7f erfuellt: Felder aus Acker und Weizen, der Weizen wächst sichtbar")
