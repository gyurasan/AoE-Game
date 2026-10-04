"""Abnahme C7s: ein geschlachtetes Schaf oder Reh zeigt nur noch Fleisch und bleibt liegen.

Dass es am Sammelbeginn geschlachtet wird und danach nicht mehr wandert, prüft
tools/spielablauf, Gruppe herde (Schlachten). Hier: die Fleischbilder mit
Alphakanal, die Inhaltsliste, dass UpdateSheepClaims beim Sammeln schlachtet,
UpdateWild geschlachtete Tiere stehen lässt und DrawAnimal für sie das Fleisch
zeichnet.
"""

import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
text = lies()
karte = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs"))
kachel = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/Tile.cs"))
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for name in ("fleisch_schaf", "fleisch_reh"):
    bild = CONTENT / f"Tiere/{name}.png"
    if not bild.is_file():
        fehler.append(f"Bild Tiere/{name}.png fehlt")
    elif bild.read_bytes()[25] != 6:
        fehler.append(f"Tiere/{name}.png hat keinen Alphakanal - nicht freigestellt")
    if f"/build:Tiere/{name}.png" not in mgcb:
        fehler.append(f"Tiere/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Tiere/{name}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Tiere/{name} nicht")

if not re.search(r"\bbool\s+Slaughtered\s*\{\s*get;\s*set;\s*\}", kachel):
    fehler.append("WildAnimal.Slaughtered fehlt")
schlachten = methode(karte, "Slaughter") or ""
if "Slaughtered" not in schlachten:
    fehler.append("TileMap.Slaughter setzt Slaughtered nicht")
wandern = methode(karte, "UpdateWild") or ""
if "Slaughtered" not in wandern:
    fehler.append("UpdateWild lässt geschlachtete Tiere nicht stehen")
claims = methode(text, "UpdateSheepClaims") or ""
if not re.search(r"GatherPhase\.Gathering", claims) or not re.search(r"\bSlaughter\s*\(", claims):
    fehler.append("UpdateSheepClaims schlachtet nicht, wenn ein Dorfbewohner am Tier sammelt")
tier = methode(text, "DrawAnimal") or ""
if "Slaughtered" not in tier or not re.search(r"_(sheep|deer)Meat", tier):
    fehler.append("DrawAnimal zeichnet für ein geschlachtetes Tier nicht das Fleisch")

melde(fehler, "C7s erfuellt: geschlachtete Tiere zeigen Fleisch und bleiben liegen")
