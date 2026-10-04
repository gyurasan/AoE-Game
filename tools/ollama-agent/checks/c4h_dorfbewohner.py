"""Abnahme C4h: die Dorfbewohner kleiden sich nach dem Zeitalter ihres Besitzers.

Prüft die Bilder (je Zeitalter ab der Feudalzeit Standbild und zwei Laufbilder in
beiden Spielerfarben, untereinander deckungsgleich, in der Inhaltsliste), dass der
Spielbildschirm sie je Zeitalter lädt und dass Standbild wie Laufbilder nach dem
Zeitalter des Besitzers gewählt werden. Weil jedes Zeitalter dieselbe Figur mit der
Faust an derselben Stelle umkleidet, gilt VillagerFist für alle - geprüft wird,
dass die Faust in jedem Standbild dort deckend ist.
"""

import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_png  # noqa: E402
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
ORDNER = ["feudal", "ritter", "imperial"]
text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []


def groesse(pfad):
    return struct.unpack(">II", pfad.read_bytes()[16:24])


faust = re.search(r"VillagerFist\s*=\s*new\s*\(\s*([\d.]+)f\s*,\s*([\d.]+)f\s*\)", text)
if not faust:
    fehler.append("VillagerFist nicht gefunden")

for ordner in ORDNER:
    for farbe in ("blau", "rot"):
        stand = CONTENT / f"Einheiten/{ordner}/dorfbewohner_{farbe}.png"
        if not stand.is_file():
            fehler.append(f"Standbild Einheiten/{ordner}/dorfbewohner_{farbe}.png fehlt")
            continue
        for name in ("dorfbewohner", "dorfbewohner_lauf1", "dorfbewohner_lauf2"):
            datei = f"Einheiten/{ordner}/{name}_{farbe}.png"
            if not (CONTENT / datei).is_file():
                fehler.append(f"Bild {datei} fehlt")
                continue
            if groesse(CONTENT / datei) != groesse(stand):
                fehler.append(f"{datei} ist nicht so groß wie das Standbild - nicht deckungsgleich")
            if f"/build:{datei}" not in mgcb:
                fehler.append(f"{datei} ist nicht in AgeOfEvolutions.mgcb eingetragen")
        if faust:
            breite, hoehe, pixel = lies_png(stand)
            fx, fy = float(faust.group(1)), float(faust.group(2))
            deckend = sum(1 for dx in (-2, 0, 2) for dy in (-2, 0, 2)
                          if pixel(int(fx * breite) + dx, int(fy * hoehe) + dy)[3] >= 128)
            if deckend < 7:
                fehler.append(f"Einheiten/{ordner}/dorfbewohner_{farbe}.png: an VillagerFist {fx, fy} "
                              f"keine Faust ({deckend} von 9 Pixeln deckend)")

# Laden je Zeitalter, nach dem Zeitalter des Besitzers zeichnen
for feld in ("_villagerSprites", "_villagerWalk"):
    if not re.search(rf"Dictionary<\(Age Age, int Owner\), Texture2D(\[\])?>\s+{feld}\b", text):
        fehler.append(f"{feld} ist nicht nach Zeitalter und Spieler geschlüsselt")
if not re.search(r'"Einheiten/"\s*\+\s*AgeFolders\[', text):
    fehler.append("der Spielbildschirm lädt die Dorfbewohner nicht aus den Ordnern der Zeitalter")
einheiten = methode(text, "DrawUnits") or ""
if not re.search(r"_villagerSprites\.TryGetValue\(\s*\(\s*AgeOf\(\s*unit\.OwnerId\s*\)", einheiten):
    fehler.append("DrawUnits wählt das Dorfbewohner-Bild nicht nach dem Zeitalter des Besitzers")
dorf = methode(text, "DrawVillager") or ""
if not re.search(r"_villagerWalk\.TryGetValue\(\s*\(\s*AgeOf\(\s*unit\.OwnerId\s*\)", dorf):
    fehler.append("DrawVillager wählt die Laufbilder nicht nach dem Zeitalter des Besitzers")

melde(fehler, "C4h erfuellt: Dorfbewohner je Zeitalter geladen und nach dem Zeitalter des Besitzers gekleidet")
