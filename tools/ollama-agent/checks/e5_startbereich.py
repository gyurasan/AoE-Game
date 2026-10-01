"""Abnahme E5: Dorfbewohner entstehen ausserhalb des Stadtzentrums, der Startplatz ist frei.

Die Spawnpunkte werden aus dem Code nachgerechnet: jede AddVillager-Stelle
relativ zur Ecke des Stadtzentrums darf nicht in dessen 4x4-Grundflaeche liegen,
muss aber im freigeraeumten Rand (2 Kacheln) bleiben.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde, methode  # noqa: E402

text = Path("AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
fehler = []

start = methode(text, "PlaceStartingPositions") or ""
bau = methode(text, "AddBuilding") or ""

# Das Stadtzentrum ist 4x4: entweder setzt AddBuilding das woertlich, oder es
# nimmt die Kantenlaenge als Parameter mit Vorgabe 4 (seit C2c) und die Startplaetze
# geben keine andere an. Die erste Fassung verlangte "Width = 4" woertlich und
# schlug seit C2c an, obwohl das Stadtzentrum unveraendert 4x4 war.
signatur = re.search(r"public\s+\w+\s+AddBuilding\s*\(([^)]*)\)", text)
vorgabe = re.search(r"int\s+(\w+)\s*=\s*4\b", signatur.group(1)) if signatur else None


def kante(achse: str) -> bool:
    if re.search(rf"{achse}\s*=\s*4\b", bau):
        return True
    return bool(vorgabe and re.search(rf"{achse}\s*=\s*{vorgabe.group(1)}\b", bau))


if not (kante("Width") and kante("Height")):
    fehler.append("AddBuilding legt das Stadtzentrum nicht mehr als 4x4 an - Pruefung passt nicht mehr")
for px, py in (("p1x", "p1y"), ("p2x", "p2y")):
    aufruf = re.search(rf"AddBuilding\(\s*{px}\s*,\s*{py}\s*,\s*\"Stadtzentrum\"\s*,\s*\d+\s*(?:,\s*([^)]+?))?\s*\)", start)
    if aufruf and aufruf.group(1) and aufruf.group(1).strip() != "4":
        fehler.append(f"Stadtzentrum bei ({px}, {py}) wird nicht als 4x4 gesetzt")


def versatz(ausdruck: str) -> int:
    ausdruck = ausdruck.replace(" ", "")
    return int(ausdruck) if ausdruck else 0


for spieler, (px, py) in {0: ("p1x", "p1y"), 1: ("p2x", "p2y")}.items():
    muster = rf"AddVillager\(\s*{px}\s*([+-]\s*\d+)?\s*,\s*{py}\s*([+-]\s*\d+)?\s*,\s*{spieler}\s*\)"
    treffer = re.findall(muster, start)
    if len(treffer) != 4:
        fehler.append(f"Spieler {spieler}: {len(treffer)} Dorfbewohner statt 4 gefunden")
    for dx, dy in treffer:
        ox, oy = versatz(dx), versatz(dy)
        if 0 <= ox <= 3 and 0 <= oy <= 3:
            fehler.append(f"Spieler {spieler}: Dorfbewohner bei ({px}{ox:+d}, {py}{oy:+d}) steht im Stadtzentrum")
        if not (-2 <= ox <= 5 and -2 <= oy <= 5):
            fehler.append(f"Spieler {spieler}: Dorfbewohner bei ({px}{ox:+d}, {py}{oy:+d}) liegt ausserhalb des Startplatzes")

raeumen = methode(text, "ClearStartArea")
if raeumen is None:
    fehler.append("Methode ClearStartArea fehlt")
else:
    for pflicht in (r"TileType\.Grassland", r"ResourceType\s*=\s*null", r"ResourceAmount\s*=\s*0",
                    r"Walkable\s*=\s*true", r"Buildable\s*=\s*true", r"-\s*2", r"\+\s*5"):
        if not re.search(pflicht, raeumen):
            fehler.append(f"ClearStartArea: {pflicht} fehlt")
    for px, py in (("p1x", "p1y"), ("p2x", "p2y")):
        aufruf = re.search(rf"ClearStartArea\(\s*{px}\s*,\s*{py}\s*\)", start)
        gebaeude = re.search(rf"AddBuilding\(\s*{px}\s*,\s*{py}\s*,", start)
        if not aufruf:
            fehler.append(f"ClearStartArea({px}, {py}) wird nicht aufgerufen")
        elif gebaeude and aufruf.start() > gebaeude.start():
            fehler.append(f"ClearStartArea({px}, {py}) laeuft erst nach AddBuilding")

melde(fehler, "E5 erfuellt: Startplatz frei, Dorfbewohner ausserhalb des Stadtzentrums")
