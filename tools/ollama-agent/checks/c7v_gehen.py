"""Abnahme C7v: Dorfbewohner gehen - zwei Laufbilder je Spielerfarbe, deckungsgleich mit dem Standbild.

In welcher Folge die Laufbilder im Takt des Wippens wechseln, prüft
tools/spielablauf, Gruppe gehen. Hier: die Bilder mit Alphakanal und genau der
Größe des Standbilds (sonst sprängen Figur und Werkzeug beim Wechsel), die
Inhaltsliste, dass der Spielbildschirm sie lädt und DrawVillager sie über
VillagerWalkPhase auswählt.
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


def kopf(pfad):
    d = pfad.read_bytes()[:26]
    return struct.unpack(">II", d[16:24]) + (d[25],)


for farbe in ("blau", "rot"):
    stand = CONTENT / f"Einheiten/dorfbewohner_{farbe}.png"
    if not stand.is_file():
        fehler.append(f"Standbild Einheiten/dorfbewohner_{farbe}.png fehlt")
        continue
    groesse = kopf(stand)[:2]
    for pose in ("lauf1", "lauf2"):
        name = f"dorfbewohner_{pose}_{farbe}"
        bild = CONTENT / f"Einheiten/{name}.png"
        if not bild.is_file():
            fehler.append(f"Laufbild Einheiten/{name}.png fehlt")
            continue
        b, h, typ = kopf(bild)
        if typ != 6:
            fehler.append(f"Einheiten/{name}.png hat keinen Alphakanal")
        if (b, h) != groesse:
            fehler.append(f"Einheiten/{name}.png ist {b}x{h}, das Standbild {groesse[0]}x{groesse[1]} - nicht deckungsgleich")
        if f"/build:Einheiten/{name}.png" not in mgcb:
            fehler.append(f"Einheiten/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
# Seit C4h lädt der Bildschirm die Laufbilder aus dem Ordner des Standbilds (Einheiten/
# oder Einheiten/<zeitalter>/) - der Name steht dann ohne Ordner im Quelltext. Seit C4m
# setzt LoadWalk die Namen _lauf1 bis _lauf8 aus Figur und Phasennummer zusammen
ueber_loadwalk = re.search(r'LoadWalk\s*\([^)]*"dorfbewohner"', text) and "_lauf" in (methode(text, "LoadWalk") or "")
for pose in ("lauf1", "lauf2"):
    if f'"Einheiten/dorfbewohner_{pose}"' not in text and f'"dorfbewohner_{pose}"' not in text and not ueber_loadwalk:
        fehler.append(f"der Spielbildschirm lädt Einheiten/dorfbewohner_{pose} nicht")

dorf = methode(text, "DrawVillager") or ""
if not re.search(r"\bVillagerWalkPhase\s*\(", dorf) or "_villagerWalk" not in dorf:
    fehler.append("DrawVillager wählt beim Gehen kein Laufbild (VillagerWalkPhase, _villagerWalk)")
if methode(text, "VillagerWalkPhase") is None:
    fehler.append("VillagerWalkPhase fehlt")

melde(fehler, "C7v erfuellt: zwei Laufbilder je Spielerfarbe, deckungsgleich, DrawVillager wechselt sie beim Gehen")
