"""Abnahme C6a: das Windrad der Mühle dreht sich, die Fahnen auf den Gebäuden wehen.

Wie Tuch und Windrad sich bewegen, prüft tools/spielablauf, Gruppe fahne. Hier:
die Bilder (Mühle ohne Flügel deckungsgleich mit der Mühle, Flügelkreuz), die
Inhaltsliste, dass DrawBuilding die Mühle ohne Flügel samt Kreuz und die Fahnen
wehend zeichnet - und dass jedes Fahnenrechteck in FlagCloth wirklich den blauen
Fahnenstoff seines Gebäudebilds umschließt, ohne viel mehr zu fassen.
"""

import colorsys
import re
import struct
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_png  # noqa: E402
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []


def groesse(pfad):
    return struct.unpack(">II", pfad.read_bytes()[16:24])


for name in ("muehle_ohne_blau", "muehle_ohne_rot", "muehle_fluegel"):
    if not (CONTENT / f"Gebaeude/{name}.png").is_file():
        fehler.append(f"Bild Gebaeude/{name}.png fehlt")
    if f"/build:Gebaeude/{name}.png" not in mgcb:
        fehler.append(f"Gebaeude/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
for farbe in ("blau", "rot"):
    ohne, mit = CONTENT / f"Gebaeude/muehle_ohne_{farbe}.png", CONTENT / f"Gebaeude/muehle_{farbe}.png"
    if ohne.is_file() and mit.is_file() and groesse(ohne) != groesse(mit):
        fehler.append(f"muehle_ohne_{farbe}.png ist {groesse(ohne)}, muehle_{farbe}.png {groesse(mit)} - nicht deckungsgleich")
for pflicht in ('"Gebaeude/muehle_ohne"', '"Gebaeude/muehle_fluegel"'):
    if pflicht not in text:
        fehler.append(f"der Spielbildschirm lädt {pflicht} nicht")

gebaeude = methode(text, "DrawBuilding") or ""
for aufruf in ("DrawWavingFlag", "DrawMillSails"):
    if not re.search(rf"\b{aufruf}\s*\(", gebaeude):
        fehler.append(f"DrawBuilding ruft {aufruf} nicht auf")
fluegel = methode(text, "DrawMillSails") or ""
if not re.search(r"\bMillSailAngle\s*\(", fluegel):
    fehler.append("DrawMillSails dreht das Flügelkreuz nicht (MillSailAngle)")
fahne = methode(text, "DrawWavingFlag") or ""
if not re.search(r"\bFlagWave\s*\(", fahne):
    fehler.append("DrawWavingFlag bewegt das Tuch nicht (FlagWave)")

# FlagCloth gegen die Bilder: der blaue Stoff liegt im Rechteck, das Rechteck ist kaum größer
eintraege = dict((n, tuple(int(v) for v in r)) for n, *r in re.findall(
    r'\["([^"]+)"\]\s*=\s*new\s*\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)', text))
bilder = {"Haus": "haus", "Mühle": "muehle_ohne", "Wachturm": "wachturm",
          "Bergbaulager": "bergbaulager", "Holzfällerlager": "holzfaellerlager"}
for gebaeude_name, datei in bilder.items():
    if gebaeude_name not in eintraege:
        fehler.append(f"FlagCloth: {gebaeude_name} fehlt")
        continue
    x, y, b, h = eintraege[gebaeude_name]
    breite, hoehe, pixel = lies_png(CONTENT / f"Gebaeude/{datei}_blau.png")
    blau = []
    for py in range(min(hoehe, y + h + 20)):
        for px in range(breite):
            r, g, bl, a = pixel(px, py)
            if a < 128:
                continue
            ton, satt, hell = colorsys.rgb_to_hsv(r / 255, g / 255, bl / 255)
            if 190 / 360 <= ton <= 260 / 360 and satt >= 0.45 and hell >= 0.25 and abs(px - (x + b / 2)) < b + 20:
                blau.append((px, py))
    drin = sum(1 for px, py in blau if x <= px < x + b and y <= py < y + h)
    if not blau or drin < 0.95 * len(blau):
        fehler.append(f"FlagCloth {gebaeude_name}: nur {drin} von {len(blau)} blauen Fahnenpixeln im Rechteck {x, y, b, h}")
    elif blau:
        bx = max(p[0] for p in blau) - min(p[0] for p in blau) + 1
        by = max(p[1] for p in blau) - min(p[1] for p in blau) + 1
        if b > bx + 8 or h > by + 8:
            fehler.append(f"FlagCloth {gebaeude_name}: Rechteck {b}x{h} viel größer als der Stoff {bx}x{by}")

melde(fehler, "C6a erfuellt: Windrad dreht sich um die Nabe, Fahnen wehen, Fahnenrechtecke passen zu den Bildern")
