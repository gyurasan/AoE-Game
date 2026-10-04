"""Abnahme C4g: die Gebäude sehen in jedem Zeitalter anders aus.

Prüft die Bilder (je Zeitalter-Gruppe in tools/bilder/bilder.json beide
Spielerfarben in Content und in der Inhaltsliste), dass der Spielbildschirm sie
je Zeitalter lädt und nach dem Zeitalter des Besitzers zeichnet, und dass
Fahnentuch und Mühlennabe je Bild gemessen sind statt je Gebäudetyp: jede Mühle
eines Zeitalters hat ihre Nabe, und jede Nabe liegt auf dem Mühlenbild.
Ob die Fahnenrechtecke den blauen Stoff fassen, prüft c6a_bewegt.py.
"""

import json
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_png  # noqa: E402
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
ORDNER = ["dunkel", "feudal", "ritter", "imperial"]   # Reihenfolge des Enums Age
text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
katalog = json.loads(Path("tools/bilder/bilder.json").read_text(encoding="utf-8"))
fehler = []

# Bilder und Inhaltsliste
for ordner in ORDNER:
    gruppe = katalog.get(f"gebaeude_{ordner}")
    if not gruppe:
        fehler.append(f"bilder.json: Gruppe gebaeude_{ordner} fehlt")
        continue
    namen = {e["name"] for e in gruppe["bilder"] if e.get("ziel")}
    for pflicht in ("stadtzentrum", "haus", "muehle_ohne", "holzfaellerlager", "bergbaulager"):
        if pflicht not in namen:
            fehler.append(f"gebaeude_{ordner}: kein Bild {pflicht}")
    for e in gruppe["bilder"]:
        if not e.get("ziel"):
            continue
        basis = e["ziel"][:-len(".png")]
        for farbe in ("blau", "rot"):
            datei = f"{basis}_{farbe}.png"
            if not (CONTENT / datei).is_file():
                fehler.append(f"Bild {datei} fehlt - uebernehmen.ps1 laufen lassen")
            if f"/build:{datei}" not in mgcb:
                fehler.append(f"{datei} ist nicht in AgeOfEvolutions.mgcb eingetragen")

# Der Bildschirm kennt die Ordner in der Reihenfolge der Zeitalter
ordner_code = re.search(r"AgeFolders\s*=\s*\{([^}]*)\}", text)
if not ordner_code or re.findall(r'"(\w+)"', ordner_code.group(1)) != ORDNER:
    fehler.append(f"AgeFolders fehlt oder nennt nicht {ORDNER} in dieser Reihenfolge")

# Gezeichnet wird nach dem Zeitalter des Besitzers
alter = methode(text, "AgeOf") or ""
if not re.search(r"\.Ages\.Current\b", alter):
    fehler.append("AgeOf liest nicht das aktuelle Zeitalter des Spielers (Ages.Current)")
zeichnen = methode(text, "DrawBuilding") or ""
if not re.search(r"\bAgeOf\s*\(\s*b\.OwnerId\s*\)", zeichnen):
    fehler.append("DrawBuilding fragt nicht das Zeitalter des Besitzers ab (AgeOf(b.OwnerId))")
if re.search(r"_buildingSprites\.TryGetValue\(\s*\(\s*b\.Type\s*,\s*b\.OwnerId\s*\)", zeichnen):
    fehler.append("DrawBuilding sucht das Bild noch ohne Zeitalter")
if re.search(r"FlagCloth\.TryGetValue\(\s*b\.Type", zeichnen):
    fehler.append("DrawBuilding sucht das Fahnentuch noch nach Gebäudetyp statt nach Bild")

# Fahnentuch und Nabe hängen am Bild
laden = methode(text, "LoadBuildingSprite") or ""
for feld in ("FlagCloth", "MillHubs"):
    if not re.search(rf"\b{feld}\.TryGetValue\s*\(\s*asset\b", laden):
        fehler.append(f"LoadBuildingSprite merkt sich {feld} nicht je Bild")

naben = {name: (float(x), float(y), float(g)) for name, x, y, g in re.findall(
    r'\["([^"]+)"\]\s*=\s*\(\s*new\s*\(\s*([\d.]+)f\s*,\s*([\d.]+)f\s*\)\s*,\s*([\d.]+)f\s*\)', text)}
for ordner in ORDNER:
    asset = f"Gebaeude/{ordner}/muehle_ohne"
    if asset not in naben:
        fehler.append(f"MillHubs: {asset} fehlt - ohne Nabe dreht sich auf dieser Mühle kein Windrad")
for asset, (hx, hy, groesse) in naben.items():
    datei = CONTENT / f"{asset}_blau.png"
    if not datei.is_file():
        fehler.append(f"MillHubs: Bild {asset}_blau.png fehlt")
        continue
    if not (0 < hx < 1 and 0 < hy < 1 and 0.3 <= groesse <= 1.5):
        fehler.append(f"MillHubs {asset}: Nabe {hx, hy} oder Größe {groesse} außerhalb des Bilds")
        continue
    breite, hoehe, pixel = lies_png(datei)
    if pixel(int(hx * breite), int(hy * hoehe))[3] < 128:
        fehler.append(f"MillHubs {asset}: die Nabe {hx, hy} liegt auf durchsichtigem Grund, nicht auf der Mühle")

melde(fehler, "C4g erfuellt: Gebäude je Zeitalter geladen und nach dem Zeitalter des Besitzers gezeichnet")
