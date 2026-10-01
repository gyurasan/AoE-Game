"""Abnahme G3: Baumkronen sind eigene Figuren, die ueber Kachelgrenzen ragen duerfen.

Aussehen entscheidet der Screenshot; hier stehen die Eigenschaften im Code.
Loest G1 ab (Kronen in der Kacheltextur zeigten das Kachelraster).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

for weg in ("forestTex", "FOREST_VARIANTS", "BuildForestTexture("):
    if weg in text:
        fehler.append(f"'{weg}' ist noch da - der Wald besteht jetzt aus Boden plus Kronen")
if methode(text, "BuildForestFloorTexture") is None:
    fehler.append("Methode BuildForestFloorTexture fehlt")
if not re.search(r"BuildCrownTexture\s*\(\s*GraphicsDevice\s+\w+\s*,\s*int\s+variant\s*\)", text):
    fehler.append("BuildCrownTexture(GraphicsDevice, int variant) fehlt")

kronen = methode(text, "DrawCrowns")
if kronen is None:
    fehler.append("Methode DrawCrowns fehlt")
else:
    for pflicht in ("WorldToScreen", "cameraZoom", "crownTex", "CrownSpots", "73856093"):
        if pflicht not in kronen:
            fehler.append(f"DrawCrowns: {pflicht} fehlt")
    if "new[]" in kronen:
        fehler.append("DrawCrowns legt je Aufruf ein neues Array an - pro Waldkachel und Frame")
if not re.search(r"static\s+readonly\s*\([^)]*\)\[\]\s+CrownSpots", text):
    fehler.append("CrownSpots ist kein statisches Feld")

karte = methode(text, "DrawTileMap") or ""
aufruf = karte.find("DrawCrowns(")
if aufruf < 0:
    fehler.append("DrawTileMap ruft DrawCrowns nicht auf")
else:
    vorher = karte[:aufruf]
    # Zeilenweise von oben: die aeussere Schleife vor DrawCrowns laeuft ueber y,
    # die innere ueber x (die Kachel- und Nahrungsschleifen davor sind umgekehrt)
    if not vorher.rfind("for (int y") < vorher.rfind("for (int x"):
        fehler.append("die Kronen werden nicht zeilenweise gezeichnet (aeussere Schleife ueber y)")
    if aufruf > karte.find("DrawBuilding("):
        fehler.append("die Kronen werden erst nach den Gebaeuden gezeichnet")
if "crownTex" not in (methode(text, "UnloadContent") or ""):
    fehler.append("UnloadContent gibt die Kronentexturen nicht frei")

melde(fehler, "G3 erfuellt: Kronen als eigene Figuren")
