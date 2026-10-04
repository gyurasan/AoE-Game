"""Abnahme C7f/C7h: Felder aus Qwen-Image - ein Bild je Feld, der Weizen wächst sichtbar.

C7f legte Acker und Weizen als kachelbare Bodenbilder an; seit C7h zeigen
Felder/weizen.png und Felder/acker.png jedes das ganze 3x3-Feld samt Zaun, und
jede Kachel schneidet ihren Teil heraus (FieldPart). Wie der Weizen beim Ernten
und Nachwachsen aussieht und welchen Bildteil jede Kachel zeigt, prüft
tools/spielablauf, Gruppe feld. Hier: Bilder, Inhaltsliste, dass DrawGround
die Felder zeichnet und der alte Weg sie nicht noch einmal übermalt, und dass
175 und 100 nur noch als Konstanten in TileMap stehen.
"""

import re
import struct
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

groessen = {}
for name in ("acker", "weizen"):
    bild = CONTENT / f"Felder/{name}.png"
    if not bild.is_file():
        fehler.append(f"Bild Felder/{name}.png fehlt")
    else:
        groessen[name] = struct.unpack(">II", bild.read_bytes()[16:24])
    if f"/build:Felder/{name}.png" not in mgcb:
        fehler.append(f"Felder/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Felder/{name}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Felder/{name} nicht")
    if f"/build:Boden/{name}.png" in mgcb:
        fehler.append(f"das alte Bodenbild Boden/{name}.png ist noch in AgeOfEvolutions.mgcb eingetragen")
if len(set(groessen.values())) > 1:
    fehler.append(f"Acker- und Weizenbild sind verschieden groß {groessen} - die Reihen lägen nicht übereinander")
for name, (b, h) in groessen.items():
    if b != h or b % 3:
        fehler.append(f"Felder/{name}.png ist {b}x{h} - ein 3x3-Feld braucht ein Quadrat, durch 3 teilbar")

boden = methode(text, "DrawGround") or ""
if not re.search(r"tile\.Farm\b[^;]*\)\s*DrawField\s*\(", boden):
    fehler.append("DrawGround zeichnet Feldkacheln nicht mit DrawField")

feld = methode(text, "DrawField") or ""
if "WheatLook(" not in feld:
    fehler.append("DrawField nimmt Deckkraft und Farbe des Weizens nicht aus WheatLook")
if len(re.findall(r"FieldPart\s*\(", feld)) < 2:
    fehler.append("DrawField schneidet Acker und Weizen nicht beide mit FieldPart aus dem Feldbild")
teil = methode(text, "FieldPart") or ""
for pflicht in ("FarmCol", "FarmRow", "FarmSize"):
    if pflicht not in teil:
        fehler.append(f"FieldPart: {pflicht} fehlt")

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
pflanzen = methode(karte, "PlantCrop") or ""
for pflicht in ("FarmCol", "FarmRow", "FarmSize"):
    if not re.search(rf"\b{pflicht}\s*=", pflanzen):
        fehler.append(f"PlantCrop setzt {pflicht} nicht")

melde(fehler, "C7f/C7h erfuellt: ein Bild je Feld, jede Kachel zeigt ihren Teil, der Weizen wächst sichtbar")
