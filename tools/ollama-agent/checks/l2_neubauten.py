"""Abnahme L2: jede Taste des Baumenüs hat ihr Symbol, jedes Gebäude sein Bild je Zeitalter.

Wie Tasten, Reihen und Setzen wirken, prüft tools/spielablauf (Gruppe neubauten). Hier
die Verbindung zu den Bildern: ButtonIcons kennt zu jeder Taste aus BuildMenu ein Symbol,
das als Datei da und in AgeOfEvolutions.mgcb eingetragen ist. BuildingSprites kennt zu
jedem Gebäude außer Farm und Mühle (eigene Zeichnung mit Windrad) ein Bild, und ab dem
Zeitalter, das es freischaltet, liegt dieses Bild in jedem Zeitalter-Ordner blau und rot
vor, eingetragen in der Inhaltsliste.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
ORDNER = ["dunkel", "feudal", "ritter", "imperial"]
# Ab welchem Zeitalter (Index in ORDNER) ein Bild je Ordner da sein muss - wie AgeRules.RequiredAgeOf,
# nur das Stadtzentrum hat Bilder schon ab der Dunklen Zeit (das erste steht von Anfang an)
AB = {"Tower": 1, "ArcheryRange": 1, "Stable": 1, "Blacksmith": 1, "Market": 1, "StoneWall": 1,
      "SiegeWorkshop": 2, "University": 2, "Monastery": 2, "Castle": 2, "Wonder": 3}

text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

menue = re.findall(r'\(\s*Keys\.(\w+)\s*,\s*BuildingType\.(\w+)\s*,\s*"([^"]+)"', text)
typen = {typ for _, typ, _ in menue}
for pflicht in ("Barracks", "PalisadeWall", "ArcheryRange", "Stable", "Blacksmith", "Market", "StoneWall",
                "TownCenter", "SiegeWorkshop", "University", "Monastery", "Castle", "Wonder"):
    if pflicht not in typen:
        fehler.append(f"BuildMenu: kein Eintrag für BuildingType.{pflicht}")
tasten = [t for t, _, _ in menue]
for t in sorted(set(tasten)):
    if tasten.count(t) > 1:
        fehler.append(f"BuildMenu: Taste {t} mehrfach belegt")
for t in ("Q", "A"):
    if t in tasten:
        fehler.append(f"BuildMenu: Taste {t} gehört schon Dorfbewohner bzw. Zeitalter")

icons = dict(re.findall(r'\(\s*"([A-Z.])"\s*,\s*"(Icons/[a-z_]+)"\s*\)', text))
sprites = dict(re.findall(r'\(\s*"([^"]+)"\s*,\s*"(Gebaeude/[a-z_]+)"\s*\)', text))


def eingetragen(rel):
    return re.search(rf"^/build:{re.escape(rel)}\s*$", mgcb, re.M) is not None


for taste, typ, name in menue:
    symbol = icons.get(taste)
    if not symbol:
        fehler.append(f"Taste {taste} ({name}) hat kein Symbol in ButtonIcons")
    elif not (CONTENT / f"{symbol}.png").is_file():
        fehler.append(f"Symbol {symbol}.png für Taste {taste} fehlt")
    elif not eingetragen(f"{symbol}.png"):
        fehler.append(f"Symbol {symbol}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if typ in ("Farm", "Mill"):
        continue
    bild = sprites.get(name)
    if not bild:
        fehler.append(f"{name}: kein Bild in BuildingSprites")
        continue
    for i in range(AB.get(typ, 0), len(ORDNER)):
        for farbe in ("blau", "rot"):
            rel = bild.replace("Gebaeude/", f"Gebaeude/{ORDNER[i]}/") + f"_{farbe}.png"
            if not (CONTENT / rel).is_file():
                fehler.append(f"{name}: Bild {rel} fehlt")
            elif not eingetragen(rel):
                fehler.append(f"{name}: {rel} ist nicht in AgeOfEvolutions.mgcb eingetragen")

melde(fehler, f"L2 erfuellt: {len(menue)} Bautasten mit Symbol, jedes Gebäude mit Bild je Zeitalter")
