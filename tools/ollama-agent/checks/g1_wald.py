"""Abnahme G1: Wald als dichtes Blaetterdach in mehreren Varianten.

Wie der Wald aussieht, entscheidet ein Screenshot. Hier geht es um die
Eigenschaften, die dafuer im Code stehen muessen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

# G3 hat den Wald neu aufgebaut: Boden als Kachel, Kronen als eigene Figuren.
# Die Varianten aus G1 gibt es dann bewusst nicht mehr.
if "crownTex" in text:
    print("G1 abgeloest durch G3 (Kronen als eigene Figuren)")
    sys.exit(0)

anzahl = re.search(r"const\s+int\s+FOREST_VARIANTS\s*=\s*(\d+)", text)
if not anzahl or int(anzahl.group(1)) < 3:
    fehler.append("FOREST_VARIANTS fehlt oder ist kleiner als 3")
if not re.search(r"BuildForestTexture\s*\(\s*GraphicsDevice\s+\w+\s*,\s*int\s+variant\s*\)", text):
    fehler.append("BuildForestTexture nimmt keine Variante entgegen")
wald = methode(text, "BuildForestTexture") or ""
if wald.count("FillCircle") < 3:
    fehler.append("BuildForestTexture zeichnet keine runden Kronen mit Schatten und Licht")
if "_texRng" in wald or "new Random(" not in wald:
    fehler.append("BuildForestTexture nutzt keinen eigenen, festen Zufall je Variante")
if methode(text, "DrawTree") is not None or "DrawTree(" in text:
    fehler.append("DrawTree ist noch da - der Wald besteht jetzt aus Kronen")
auswahl = methode(text, "GetTileTexture") or ""
if "forestTex" not in auswahl or "TileType.Forest" not in auswahl:
    fehler.append("GetTileTexture waehlt keine Waldvariante")
if "forestTex" not in (methode(text, "UnloadContent") or ""):
    fehler.append("UnloadContent gibt die Waldtexturen nicht frei")

melde(fehler, "G1 erfuellt: Wald als Blaetterdach in Varianten")
