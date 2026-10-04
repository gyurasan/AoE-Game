"""Abnahme C7k: Kaninchen und Wildschweine als weiteres Wild, mit Stand-, Lauf- und Fleischbildern.

Wandern, Schlachten und den Bestand prüfen tools/spielablauf (herde) und
tools/kartenpruefung (wild) - beide Gruppen erweitert diese Aufgabe selbst. Hier
deshalb unabhängig davon: die Bilder (Laufbilder deckungsgleich mit dem Standbild),
die Inhaltsliste, die neuen Nahrungsquellen und dass beide Prüfprogramme die
neuen Arten wirklich kennen.
"""

import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
text = lies()
kachel = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/Tile.cs"))
karte = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs"))
ablauf = lies(Path("tools/spielablauf/Program.cs"))
pruef = lies(Path("tools/kartenpruefung/Program.cs"))
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []


def kopf(pfad):
    d = pfad.read_bytes()[:26]
    return struct.unpack(">II", d[16:24]) + (d[25],)


for tier in ("kaninchen", "kaninchen2", "wildschwein", "wildschwein2"):
    stand = CONTENT / f"Tiere/{tier}.png"
    groesse = kopf(stand)[:2] if stand.is_file() else None
    for name in (tier, f"{tier}_lauf1", f"{tier}_lauf2"):
        bild = CONTENT / f"Tiere/{name}.png"
        if not bild.is_file():
            fehler.append(f"Bild Tiere/{name}.png fehlt")
            continue
        b, h, typ = kopf(bild)
        if typ != 6:
            fehler.append(f"Tiere/{name}.png hat keinen Alphakanal")
        if groesse and (b, h) != groesse:
            fehler.append(f"Tiere/{name}.png ist {b}x{h}, das Standbild {groesse[0]}x{groesse[1]} - nicht deckungsgleich")
        if f"/build:Tiere/{name}.png" not in mgcb:
            fehler.append(f"Tiere/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Tiere/{tier}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Tiere/{tier} nicht")
for name in ("fleisch_kaninchen", "fleisch_wildschwein"):
    if not (CONTENT / f"Tiere/{name}.png").is_file():
        fehler.append(f"Bild Tiere/{name}.png fehlt")
    if f"/build:Tiere/{name}.png" not in mgcb:
        fehler.append(f"Tiere/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Tiere/{name}"' not in text:
        fehler.append(f"der Spielbildschirm lädt Tiere/{name} nicht")

if not re.search(r"enum\s+FoodSource\s*\{[^}]*\bRabbit\b[^}]*\bBoar\b", kachel):
    fehler.append("FoodSource kennt Rabbit und Boar nicht")
if not re.search(r"\bIsWild\s*\(\s*this\s+FoodSource", kachel):
    fehler.append("FoodSources.IsWild fehlt")
update = methode(text, "Update") or ""
for aufruf in ("UpdateRabbits", "UpdateBoars"):
    if not re.search(rf"\b{aufruf}\s*\(", karte):
        fehler.append(f"TileMap.{aufruf} fehlt")
    if not re.search(rf"\.{aufruf}\s*\(", text):
        fehler.append(f"der Spielbildschirm ruft {aufruf} nicht auf")
    if not re.search(rf"\.{aufruf}\s*\(", ablauf):
        fehler.append(f"tools/spielablauf ruft {aufruf} nicht auf - die Abläufe sähen das neue Wild nie wandern")
for quelle in ("FoodSource.Rabbit", "FoodSource.Boar"):
    if quelle not in pruef:
        fehler.append(f"tools/kartenpruefung zählt {quelle} nicht")
    if quelle not in ablauf:
        fehler.append(f"tools/spielablauf prüft {quelle} nicht")
tier = methode(text, "AnimalSprites") or ""
if "_rabbitSprites" not in tier or "_boarSprites" not in tier:
    fehler.append("AnimalSprites liefert keine Bilder für Kaninchen und Wildschweine")

melde(fehler, "C7k erfuellt: Kaninchen und Wildschweine mit Bildern, Wanderung und Prüfungen")
