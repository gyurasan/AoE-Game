"""Misst das Fahnentuch in den neuen Gebäudebildern (Content/Gebaeude/<zeitalter>/<name>_blau.png).

Fahnenstoff = kräftiges Blau (Ton 190-260 Grad, Sättigung >= 0,45, Helligkeit >= 0,25), wie
checks/c6a_bewegt.py es zählt. Das Tuch ist die größte zusammenhängende Blaufläche in der
oberen Bildhälfte; das Rechteck umschließt sie plus 2 px Kontur. Ein Tuch gilt nur als
wehbar, wenn hinter ihm Luft ist: im Rechteck sind höchstens 25 % der Pixel weder Stoff
noch Kontur noch durchsichtig - sonst schiene beim Wehen das Gebäude durch.
Ausgabe: fahnen.json {asset: [x, y, b, h]} und Ausschnitte fahnen_<name>.png zum Ansehen.
"""
import colorsys
import json
import sys
from pathlib import Path
from PIL import Image

HIER = Path(__file__).resolve().parent
CONTENT = Path("D:/Apps/AgeOfEmpire/AgeOfEvolutions/AgeOfEvolutions.Core/Content")
# Fahne vor der Fassade - beim Wehen schiene das Gebäude durch, also nicht vermessen
AUSNAHMEN = {"Gebaeude/ritter/universitaet"}
NAMEN = ["kaserne", "schiessstand", "stall", "schmiede", "markt", "belagerungswerkstatt",
         "universitaet", "kloster", "burg", "wunder"]


def blau(p):
    r, g, b, a = p
    if a < 128:
        return False
    ton, satt, hell = colorsys.rgb_to_hsv(r / 255, g / 255, b / 255)
    return 190 / 360 <= ton <= 260 / 360 and satt >= 0.45 and hell >= 0.25


ergebnis, bericht = {}, []
for zeitalter in ("dunkel", "feudal", "ritter", "imperial"):
    for name in NAMEN:
        datei = CONTENT / f"Gebaeude/{zeitalter}/{name}_blau.png"
        if not datei.is_file():
            continue
        bild = Image.open(datei).convert("RGBA")
        w, h = bild.size
        px = bild.load()
        maske = [[blau(px[x, y]) for x in range(w)] for y in range(h)]
        gesehen = [[False] * w for _ in range(h)]
        flaechen = []
        for y in range(h):
            for x in range(w):
                if maske[y][x] and not gesehen[y][x]:
                    stapel, punkte = [(x, y)], []
                    gesehen[y][x] = True
                    while stapel:
                        cx, cy = stapel.pop()
                        punkte.append((cx, cy))
                        for nx, ny in ((cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1),
                                       (cx + 1, cy + 1), (cx - 1, cy - 1), (cx + 1, cy - 1), (cx - 1, cy + 1)):
                            if 0 <= nx < w and 0 <= ny < h and maske[ny][nx] and not gesehen[ny][nx]:
                                gesehen[ny][nx] = True
                                stapel.append((nx, ny))
                    flaechen.append(punkte)
        oben = [f for f in flaechen if min(p[1] for p in f) < h * 0.7 and len(f) >= 15]
        asset = f"Gebaeude/{zeitalter}/{name}"
        if asset in AUSNAHMEN:
            bericht.append(f"{asset}: Fahne vor der Fassade, ausgelassen")
            continue
        if not oben:
            bericht.append(f"{asset}: keine Fahne gefunden")
            continue
        tuch = max(oben, key=len)
        x0 = max(0, min(p[0] for p in tuch) - 2); x1 = min(w - 1, max(p[0] for p in tuch) + 2)
        y0 = max(0, min(p[1] for p in tuch) - 2); y1 = min(h - 1, max(p[1] for p in tuch) + 2)
        stoff = set(tuch)
        fremd = 0
        for y in range(y0, y1 + 1):
            for x in range(x0, x1 + 1):
                r, g, b, a = px[x, y]
                if (x, y) in stoff or a < 128:
                    continue
                # Kontur: dunkel oder an den Stoff grenzend
                nah = any((x + dx, y + dy) in stoff for dx in (-2, -1, 0, 1, 2) for dy in (-2, -1, 0, 1, 2))
                if not nah:
                    fremd += 1
        anteil = fremd / ((x1 - x0 + 1) * (y1 - y0 + 1))
        rechteck = [x0, y0, x1 - x0 + 1, y1 - y0 + 1]
        frei = anteil <= 0.25
        bericht.append(f"{asset}: Tuch {rechteck}, {len(tuch)} px, fremd {anteil:.0%} -> {'wehen' if frei else 'NICHT (Wand dahinter)'}")
        if frei:
            ergebnis[asset] = rechteck
        rand = 12
        aus = bild.crop((max(0, x0 - rand), max(0, y0 - rand), min(w, x1 + rand), min(h, y1 + rand)))
        aus = aus.resize((aus.width * 4, aus.height * 4), Image.NEAREST)
        grund = Image.new("RGBA", aus.size, (120, 140, 90, 255))
        grund.alpha_composite(aus)
        grund.save(HIER / f"fahnen_{zeitalter}_{name}.png")

(HIER / "fahnen.json").write_text(json.dumps(ergebnis, indent=1), encoding="utf-8")
print("\n".join(bericht))
