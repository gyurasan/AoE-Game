"""Abnahme E7: Kacheln und Gebaeude werden aus den Weltecken gezeichnet, nicht ab dem Mittelpunkt.

GridToWorld liefert den Kachelmittelpunkt. Wer damit die linke obere Ecke
setzt, verschiebt die Karte um eine halbe Kachel gegen Maus und Einheiten.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

rechteck = methode(text, "TileScreenRect")
if rechteck is None:
    fehler.append("Methode TileScreenRect fehlt")
else:
    if "GridToWorld" in rechteck:
        fehler.append("TileScreenRect nutzt GridToWorld - das ist der Mittelpunkt, nicht die Ecke")
    if rechteck.count("TileSize") < 4:
        fehler.append("TileScreenRect rechnet nicht beide Ecken aus Kachelgroesse und Koordinaten")
    if rechteck.count("WorldToScreen") < 2:
        fehler.append("TileScreenRect rechnet nicht beide Ecken in Bildschirmkoordinaten um")

# Seit C7r zeichnet DrawGround den Boden in einem eigenen Durchgang, DrawTileMap
# alles darauf - beide zusammen sind die Karte
karte = (methode(text, "DrawTileMap") or "") + (methode(text, "DrawGround") or "")
if "GridToWorld" in karte:
    fehler.append("DrawTileMap nutzt noch GridToWorld")
if len(re.findall(r"TileScreenRect\s*\(\s*x\s*,\s*y\s*\)", karte)) < 2:
    fehler.append("DrawTileMap: Kacheln und Nahrungsobjekte zeichnen nicht beide ueber TileScreenRect(x, y)")
if re.search(r"new\s+Rectangle\([^;]*\b32\s*,\s*32\s*\)", karte):
    fehler.append("DrawTileMap: noch feste 32x32-Rechtecke - die wachsen beim Zoomen nicht mit")

gebaeude = methode(text, "DrawBuilding") or ""
if "GridToWorld" in gebaeude:
    fehler.append("DrawBuilding nutzt noch GridToWorld")
if not re.search(r"TileScreenRect\s*\(\s*b\.X\s*,\s*b\.Y\s*,\s*b\.Width\s*,\s*b\.Height\s*\)", gebaeude):
    fehler.append("DrawBuilding: Grundflaeche nicht ueber TileScreenRect(b.X, b.Y, b.Width, b.Height)")

melde(fehler, "E7 erfuellt: Karte deckungsgleich mit Maus und Einheiten, bei jedem Zoom lueckenlos")
