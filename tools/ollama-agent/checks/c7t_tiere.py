"""Abnahme C7t: Schafe und Rehe als freigestellte Sprites, gezeichnet von DrawAnimal.

Wie die Tiere wandern (eigener Takt je Tier, Schrittdauer, Blickrichtung)
prüft tools/spielablauf, Gruppe herde. Hier: Bilder mit Alphakanal,
Inhaltsliste, dass der Spielbildschirm sie lädt und DrawAnimal sie in der
Zeilenschicht zeichnet - gespiegelt in Laufrichtung und auf dem Weg zwischen
alter und neuer Kachel -, und dass DrawDeer und die Wandertakte je Tierart weg sind.
"""

import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
KARTE = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs")
TILE = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/Tile.cs")

text = lies()
karte = lies(KARTE)
kachel = lies(TILE)
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for name in ("schaf", "schaf2", "reh", "reh2"):
    bild = CONTENT / f"Tiere/{name}.png"
    if not bild.is_file():
        fehler.append(f"Bild Tiere/{name}.png fehlt")
    else:
        kopf = bild.read_bytes()[:26]
        breite, hoehe = struct.unpack(">II", kopf[16:24])
        if kopf[25] != 6:
            fehler.append(f"Tiere/{name}.png hat keinen Alphakanal (PNG-Farbtyp {kopf[25]}) - nicht freigestellt")
        if not (48 <= breite <= 256 and 24 <= hoehe <= 256):
            fehler.append(f"Tiere/{name}.png ist {breite}x{hoehe} - als Sprite zu groß oder zu klein")
    if f"/build:Tiere/{name}.png" not in mgcb:
        fehler.append(f"Tiere/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Tiere/{name}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Tiere/{name} nicht")
if re.search(r'"Rohstoffe/reh', text):
    fehler.append("der Spielbildschirm sucht die Rehe noch unter Rohstoffe/")

tier = methode(text, "DrawAnimal") or ""
if not tier:
    fehler.append("DrawAnimal fehlt")
for pflicht, grund in (("FlipHorizontally", "spiegelt das Tier nicht in Laufrichtung"),
                       ("FacingLeft", "fragt die Blickrichtung nicht ab"),
                       ("Glide", "zeichnet den Schritt zwischen den Kacheln nicht"),
                       ("FromX", "kennt die Herkunft des Schritts nicht"),
                       ("FromY", "kennt die Herkunft des Schritts nicht"),
                       ("Look", "nimmt Bild und Versatz nicht aus dem Aussehen des Tiers")):
    if tier and pflicht not in tier:
        fehler.append(f"DrawAnimal {grund} ({pflicht} fehlt)")

zeichnen = methode(text, "DrawUnits") or ""
if not re.search(r"\bDrawAnimal\s*\(", zeichnen):
    fehler.append("DrawUnits ruft DrawAnimal nicht auf")
if methode(text, "DrawDeer") is not None:
    fehler.append("DrawDeer gibt es noch - DrawAnimal zeichnet Schafe und Rehe")

if not re.search(r"class\s+WildAnimal\b", kachel):
    fehler.append("Tile.cs: class WildAnimal fehlt")
if not re.search(r"\bWildAnimal\s+Animal\s*\{\s*get;\s*set;\s*\}", kachel):
    fehler.append("Tile.cs: Tile.Animal fehlt")
if re.search(r"_(sheep|deer)WanderTimer\b", karte):
    fehler.append("TileMap: es gibt noch einen gemeinsamen Wandertakt je Tierart")
if not re.search(r"const\s+float\s+WILD_STEP_SECONDS\s*=", karte):
    fehler.append("TileMap: WILD_STEP_SECONDS fehlt")

melde(fehler, "C7t erfuellt: Schafe und Rehe als Sprites, gespiegelt in Laufrichtung, mit sichtbarem Schritt")
