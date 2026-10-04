"""Abnahme C7w: Schafe und Rehe gehen - zwei Laufbilder je Tier, deckungsgleich mit dem Standbild.

In welcher Folge die Laufbilder wechseln, prüft tools/spielablauf, Gruppe herde
(Gangbild). Hier: die Bilder mit Alphakanal und genau der Größe des Standbilds
(sonst spränge das Tier beim Wechsel), die Inhaltsliste, dass der Spielbildschirm
sie lädt und DrawAnimal sie über WalkPhase auswählt.
"""

import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []


def kopf(pfad: Path):
    daten = pfad.read_bytes()[:26]
    breite, hoehe = struct.unpack(">II", daten[16:24])
    return breite, hoehe, daten[25]


for tier in ("schaf", "schaf2", "reh", "reh2"):
    stand = CONTENT / f"Tiere/{tier}.png"
    if not stand.is_file():
        fehler.append(f"Standbild Tiere/{tier}.png fehlt")
        continue
    groesse = kopf(stand)[:2]
    for pose in ("lauf1", "lauf2"):
        name = f"{tier}_{pose}"
        bild = CONTENT / f"Tiere/{name}.png"
        if not bild.is_file():
            fehler.append(f"Laufbild Tiere/{name}.png fehlt")
            continue
        b, h, farbtyp = kopf(bild)
        if farbtyp != 6:
            fehler.append(f"Tiere/{name}.png hat keinen Alphakanal - nicht freigestellt")
        if (b, h) != groesse:
            fehler.append(f"Tiere/{name}.png ist {b}x{h}, das Standbild {groesse[0]}x{groesse[1]} - nicht deckungsgleich")
        if f"/build:Tiere/{name}.png" not in mgcb:
            fehler.append(f"Tiere/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")

for pose in ("lauf1", "lauf2"):
    if not re.search(rf'"_{pose}"', text):
        fehler.append(f"der Spielbildschirm lädt die Laufbilder _{pose} nicht")

tier = methode(text, "DrawAnimal") or ""
if not re.search(r"\bWalkPhase\s*\(", tier):
    fehler.append("DrawAnimal wählt das Laufbild nicht über WalkPhase")
if "_animalWalk" not in tier:
    fehler.append("DrawAnimal nimmt die Laufbilder nicht aus _animalWalk")
if methode(text, "WalkPhase") is None:
    fehler.append("WalkPhase fehlt")

melde(fehler, "C7w erfuellt: zwei Laufbilder je Tier, deckungsgleich, DrawAnimal wechselt sie über WalkPhase")
