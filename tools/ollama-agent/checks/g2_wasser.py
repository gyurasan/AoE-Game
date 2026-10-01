"""Abnahme G2: ruhiges Wasser ohne Flimmern, mit Uferlinie.

Wie das Wasser aussieht, entscheidet ein Screenshot. Hier geht es um die
Eigenschaften, die dafuer im Code stehen muessen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

# Die Wassertextur hat G4 ersetzt (zwei Wellen, Varianten); die Uferlinie
# aus G2 gilt weiter und wird unten auf jeden Fall geprueft.
abgeloest = "waterTex" in text
wasser = methode(text, "BuildWaterTexture") or ""
if not abgeloest and "Math.Sin" not in wasser:
    fehler.append("BuildWaterTexture: keine weiche Wellenform (Math.Sin)")
if "_texRng" in wasser:
    fehler.append("BuildWaterTexture wuerfelt das Rauschen je Frame neu - das flimmert")
if re.search(r"phase\s*<\s*2", wasser):
    fehler.append("BuildWaterTexture: das harte Saegezahnmuster ist noch da")

ufer = methode(text, "DrawShore")
if ufer is None:
    fehler.append("Methode DrawShore fehlt")
elif len(re.findall(r"IsLand\s*\(", ufer)) < 4:
    fehler.append("DrawShore prueft nicht alle vier Kanten auf Land")
if methode(text, "IsLand") is None:
    fehler.append("Methode IsLand fehlt")
if not re.search(r"DrawShore\s*\(", methode(text, "DrawTileMap") or ""):
    fehler.append("DrawTileMap zeichnet keine Uferlinie")

melde(fehler, "G2 erfuellt: ruhiges Wasser mit Uferlinie")
