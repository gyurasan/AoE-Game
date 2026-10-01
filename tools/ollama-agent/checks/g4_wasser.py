"""Abnahme G4: Wasser in Varianten mit zwei ueberlagerten Wellen.

Aussehen entscheidet der Screenshot; hier stehen die Eigenschaften im Code.
Loest die Wassertextur aus G2 ab; die Uferlinie aus G2 bleibt.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

if "waterFrames" in text:
    fehler.append("'waterFrames' kommt noch vor - ersetzt durch waterTex[Variante, Frame]")
if not re.search(r"Texture2D\[,\]\s+waterTex", text):
    fehler.append("waterTex ist kein zweidimensionales Feld [Variante, Frame]")
for konstante in ("WATER_VARIANTS", "WATER_FRAMES"):
    if not re.search(rf"const\s+int\s+{konstante}\s*=", text):
        fehler.append(f"Konstante {konstante} fehlt")

wasser = methode(text, "BuildWaterTexture") or ""
if not re.search(r"BuildWaterTexture\s*\(\s*GraphicsDevice\s+\w+\s*,\s*int\s+frame\s*,\s*int\s+variant\s*\)", text):
    fehler.append("BuildWaterTexture nimmt keine Variante entgegen")
if wasser.count("Math.Sin") < 2:
    fehler.append("BuildWaterTexture ueberlagert keine zwei Wellen")
if "_texRng" in wasser:
    fehler.append("BuildWaterTexture wuerfelt das Rauschen je Frame neu")

auswahl = methode(text, "GetTileTexture") or ""
if "waterTex[" not in auswahl or "WATER_VARIANTS" not in auswahl:
    fehler.append("GetTileTexture waehlt keine Wasservariante")
if "WATER_FRAMES" not in (methode(text, "Update") or ""):
    fehler.append("Update zaehlt die Frames nicht ueber WATER_FRAMES")
if "waterTex" not in (methode(text, "UnloadContent") or ""):
    fehler.append("UnloadContent gibt die Wassertexturen nicht frei")
if methode(text, "DrawShore") is None:
    fehler.append("die Uferlinie aus G2 ist verschwunden")

melde(fehler, "G4 erfuellt: Wasser mit zwei Wellen in Varianten")
