"""Abnahme C7r: Sand, Wasser, Stein und Gold aus Qwen-Image.

Sand und Wasser kommen wie das Gras aus großen kachelbaren Bildern, das Wasser
in zwei treibenden Lagen. Der Ausschnitt einer treibenden Lage greift über den
Bildrand hinaus - mit der üblichen Abtastung (LinearClamp) würde dort der
Randpixel gestreckt, ein Streifen alle acht Kacheln. Deshalb läuft der Boden
in einem eigenen SpriteBatch mit SamplerState.LinearWrap. Stein und Gold sind
Haufen-Sprites auf Gras, zeilenweise mit den Bäumen gezeichnet.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
ASSETS = ["Boden/sand", "Boden/wasser", "Rohstoffe/stein", "Rohstoffe/stein2", "Rohstoffe/gold", "Rohstoffe/gold2"]

text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for asset in ASSETS:
    if not (CONTENT / f"{asset}.png").is_file():
        fehler.append(f"Bild {asset}.png fehlt im Content-Ordner")
    if f"/build:{asset}.png" not in mgcb:
        fehler.append(f"{asset}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"{asset}"' not in text:
        fehler.append(f"der Spielbildschirm lädt {asset} nicht")

# Draw: erst der Boden mit wiederholender Abtastung, dann der Rest
draw = methode(text, "Draw") or ""
wrap = re.search(r"Begin\s*\([^)]*SamplerState\.LinearWrap[^)]*\)\s*;\s*DrawGround\s*\(\s*spriteBatch\s*\)\s*;"
                 r"\s*spriteBatch\.End\s*\(\s*\)", draw)
if not wrap:
    fehler.append("Draw zeichnet den Boden nicht in eigenem SpriteBatch mit LinearWrap (Begin, DrawGround, End)")
elif draw.find("DrawTileMap(") < wrap.end():
    fehler.append("Draw ruft DrawTileMap vor dem Boden auf")

boden = methode(text, "DrawGround") or ""
for pflicht in ("DrawWater(", "_sandTex", "_grassTex", "DrawShore(", "PileSprites(", "GetTileTexture("):
    if pflicht not in boden:
        fehler.append(f"DrawGround: {pflicht} fehlt")
if "DrawGrass(" in text:
    fehler.append("DrawGrass ist noch da - es heißt jetzt DrawGroundImage")

bild = methode(text, "DrawGroundImage") or ""
if "GROUND_TEXELS" not in bild or bild.count("% size") < 2 or "drift" not in bild:
    fehler.append("DrawGroundImage: Ausschnitt nicht aus GROUND_TEXELS, drift und % size")

wasser = methode(text, "DrawWater") or ""
lagen = re.findall(r"DrawGroundImage\s*\(", wasser)
if len(lagen) < 2 or "animationTime" not in wasser:
    fehler.append("DrawWater: nicht zwei mit animationTime treibende Lagen")

karte = methode(text, "DrawTileMap") or ""
haufen, gebaeude = karte.find("DrawPile("), karte.find("DrawBuilding(")
if haufen < 0:
    fehler.append("DrawTileMap zeichnet keine Stein- und Goldhaufen")
elif not (karte.find("DrawCrowns(") < haufen < gebaeude):
    fehler.append("die Haufen kommen nicht zusammen mit den Bäumen vor den Gebäuden")
if "DrawShore(" in karte or "GetTileTexture(" in karte:
    fehler.append("DrawTileMap zeichnet die Bodenkacheln noch selbst")

pile = methode(text, "DrawPile") or ""
for pflicht in ("TileScreenRect(", "73856093", "screenBounds"):
    if pflicht not in pile:
        fehler.append(f"DrawPile: {pflicht} fehlt")

melde(fehler, "C7r erfuellt: Sand und treibendes Wasser aus Bildern, Stein und Gold als Haufen")
