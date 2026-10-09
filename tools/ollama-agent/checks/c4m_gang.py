"""Abnahme C4m: natürliches Gehen - acht Gehphasen je Figur, im Spiel nach der Strecke gezeigt.

Die Bilder kommen aus tools/bilder/gang.py (Bein-Skelett über dem Standbild). Geprüft wird:
jede Figur hat lauf1 bis lauf8 in beiden Spielerfarben, eingetragen in der Content-Pipeline
und genau so groß wie ihr Standbild (deckungsgleich); der Oberkörper jeder Phase ist das
Standbild, nur senkrecht um das Auf und Ab versetzt - Kleidung, Kopf und Waffe springen
nicht. Im Code: die Phasen werden geladen (LoadWalk, Gait.WALK_FRAMES) und nach der Strecke
gewählt; mit Gehphasen wippt nicht mehr die ganze Figur, Werkzeug und Traglast gehen um
VillagerWalkHub mit. Ob es natürlich aussieht, zeigt erst die Fotoserie.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_png  # noqa: E402
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
FIGUREN = ["Einheiten/dorfbewohner", "Einheiten/feudal/dorfbewohner", "Einheiten/ritter/dorfbewohner",
           "Einheiten/imperial/dorfbewohner", "Einheiten/miliz", "Einheiten/bogenschuetze", "Einheiten/spaeher"]
fehler = []
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")

for figur in FIGUREN:
    for farbe in ("blau", "rot"):
        stand = CONTENT / f"{figur}_{farbe}.png"
        if not stand.is_file():
            fehler.append(f"{stand} fehlt")
            continue
        sw, sh, sp = lies_png(stand)
        for n in range(1, 9):
            rel = f"{figur}_lauf{n}_{farbe}.png"
            if f"#begin {rel}\n" not in mgcb:
                fehler.append(f"Content-Pipeline: {rel} nicht eingetragen")
            if not (CONTENT / rel).is_file():
                fehler.append(f"{rel} fehlt")
                continue
            w, h, p = lies_png(CONTENT / rel)
            if (w, h) != (sw, sh):
                fehler.append(f"{rel} ist {w}x{h}, das Standbild {sw}x{sh} - nicht deckungsgleich")
                continue
            if farbe != "blau":
                continue
            # Oberkörper (oberste 35 %) gleich dem Standbild, nur senkrecht versetzt
            punkte = [(x, y) for y in range(2, int(sh * 0.35), 2) for x in range(0, sw, 2) if sp(x, y)[3] > 200]
            beste = None
            for s in range(-12, 13):
                summe = n_ = 0
                for x, y in punkte:
                    if 0 <= y + s < h:
                        a, b = sp(x, y), p(x, y + s)
                        summe += abs(a[0] - b[0]) + abs(a[1] - b[1]) + abs(a[2] - b[2])
                        n_ += 3
                if n_ and (beste is None or summe / n_ < beste[0]):
                    beste = (summe / n_, s)
            if beste is None or beste[0] > 14:
                fehler.append(f"{rel}: der Oberkörper weicht vom Standbild ab (bestenfalls {beste[0]:.1f} je Farbwert)")

text = lies()
lade = methode(text, "LoadWalk") or ""
if "NotImplementedException" in lade or not re.search(r"Gait\.WALK_FRAMES", lade) or "_lauf" not in lade:
    fehler.append("LoadWalk lädt nicht die Gait.WALK_FRAMES Gehphasen _lauf1 bis _lauf8")
if not re.search(r"\bLoadWalk\s*\(", methode(text, "VillagerWalkCycle") or ""):
    fehler.append("VillagerWalkCycle nimmt nicht LoadWalk")
if not re.search(r"\bLoadWalk\s*\(", methode(text, "LoadContent") or ""):
    fehler.append("LoadContent lädt die Gehphasen der Soldaten nicht über LoadWalk")
if not re.search(r"Gait\.WALK_FRAMES", methode(text, "VillagerWalkPhase") or ""):
    fehler.append("VillagerWalkPhase wählt nicht unter Gait.WALK_FRAMES Phasen")
figur = methode(text, "DrawVillager") or ""
if "VillagerWalkHub" not in figur:
    fehler.append("DrawVillager: Werkzeug und Traglast gehen nicht mit dem Auf und Ab der Gehphasen mit (VillagerWalkHub)")
for treffer in re.finditer(r"Gait\.Bob\s*\(", figur):
    # Gait.Bob nur im Zweig ohne Gehphasen: davor eine Bedingung auf walk und null - entweder
    # "walk == null" / "walk is null" oder "walk != null" mit einem else dahinter
    davor = figur[:treffer.start()]
    bedingung = max(davor.rfind("walk == null"), davor.rfind("walk is null"), davor.rfind("walk != null"),
                    davor.rfind("walk is not null"), davor.rfind("walk is { }"))
    if bedingung < 0 or ("!= null" in davor[bedingung:bedingung + 14] or "not null" in davor[bedingung:bedingung + 18]
                         or "{ }" in davor[bedingung:bedingung + 12]) and "else" not in davor[bedingung:]:
        fehler.append("DrawVillager: Gait.Bob wippt die Figur auch mit Gehphasen - das Auf und Ab steckt im Bild")
gait = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/Gait.cs").read_text(encoding="utf-8-sig")
if "NotImplementedException" in (methode(gait, "WalkFrame") or ""):
    fehler.append("Gait.WalkFrame wirft noch NotImplementedException")

melde(fehler, "C4m erfuellt: acht deckungsgleiche Gehphasen je Figur, Oberkörper ruhig, im Spiel nach der Strecke")
