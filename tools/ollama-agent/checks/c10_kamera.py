"""Abnahme C10: Kantenscrollen, Pfeiltasten in die richtige Richtung, bildratenunabhaengig.

Die Richtung laesst sich ohne laufendes Spiel nur am Code festmachen. Geprueft
wird deshalb die Eigenschaft der Formel: WorldToScreen = (welt + kamera) * zoom,
also muss die Kamera *entgegen* der Blickrichtung verschoben werden.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

pan = methode(text, "PanCamera")
if pan is None:
    fehler.append("Methode PanCamera fehlt")
else:
    for pflicht in ("Keys.Left", "Keys.Right", "Keys.Up", "Keys.Down", "EDGE_SCROLL_MARGIN",
                    "CAMERA_PAN_SPEED", "selectionStart", "screenBounds.Width",
                    "screenBounds.Height", "cameraZoom", "mouse.X", "mouse.Y"):
        if pflicht not in pan:
            fehler.append(f"PanCamera verwendet {pflicht} nicht")
    if not re.search(r"cameraPosition\s*-=|cameraPosition\s*=\s*cameraPosition\s*-", pan):
        fehler.append("PanCamera verschiebt die Kamera nicht entgegen der Blickrichtung (-=)")
    if "Normalize" not in pan:
        fehler.append("Richtung wird nicht normiert - diagonal waere schneller")

eingabe = methode(text, "HandleRtsInput") or ""
if not re.search(r"PanCamera\s*\(", eingabe):
    fehler.append("HandleRtsInput ruft PanCamera nicht auf")
if re.search(r"cameraPosition\.[XY]\s*[-+]=\s*10\b", text):
    fehler.append("alte bildratenabhaengige Pfeiltasten-Verschiebung (+-= 10) noch da")
if not re.search(r"const\s+int\s+EDGE_SCROLL_MARGIN", text):
    fehler.append("Konstante EDGE_SCROLL_MARGIN fehlt")
if not re.search(r"const\s+float\s+CAMERA_PAN_SPEED", text):
    fehler.append("Konstante CAMERA_PAN_SPEED fehlt")
if "Bildrand" not in text:
    fehler.append("Hilfetext nennt den Bildrand nicht")
if not re.search(r"ClampCamera\s*\(\s*\)", eingabe):
    fehler.append("HandleRtsInput klemmt die Kamera nicht mehr")

melde(fehler, "C10 erfuellt: Kantenscrollen und Pfeiltasten")
