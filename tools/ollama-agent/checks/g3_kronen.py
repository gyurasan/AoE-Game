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

zeichnen = methode(text, "DrawUnits") or ""
if "DrawCrowns(" not in zeichnen:
    fehler.append("DrawUnits ruft DrawCrowns nicht auf")
elif not re.search(r"\.Sort\s*\(", zeichnen) or "Depth" not in zeichnen:
    fehler.append("DrawUnits sortiert nicht nach der Sohle (Depth)")
if "crownTex" not in (methode(text, "UnloadContent") or ""):
    fehler.append("UnloadContent gibt die Kronentexturen nicht frei")

melde(fehler, "G3 erfuellt: Kronen als eigene Figuren")
