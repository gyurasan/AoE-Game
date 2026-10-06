"""Abnahme T1: Abliefern direkt vor der Tür der Abgabestelle.

Bis 2026-10-06 stellte FollowJob die Dorfbewohner beim Abliefern per StandCell neben
die Ringkachel, die der Sammelauftrag gewählt hatte - bis zu vier Kacheln neben der
Tür und bis zu zwei Kacheln vom Gebäude (Hinweis des Nutzers: "Eigentlich sollten sie
immer bis zur Türe"). Wo sie jetzt stehen, prüft tools/spielablauf (Gruppe tuer). Hier:
die Türtabelle trägt die am Bild gemessenen Werte, die beiden Rümpfe sind gefüllt,
und FollowJob nutzt DropOffStand.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

GEMESSEN = {   # Türmitte in Anteilen der Bildbreite, am 2026-10-06 gemessen (±0,03)
    "stadtzentrum": (0.33, 0.32, 0.31, 0.39),
    "muehle_ohne": (0.33, 0.48, 0.50, 0.50),
    "holzfaellerlager": (0.30, 0.37, 0.33, 0.35),
    "bergbaulager": (0.31, 0.40, 0.29, 0.30),
}
ORDNER = ("dunkel", "feudal", "ritter", "imperial")

text = lies()
fehler = []

tabelle = dict((k, float(v)) for k, v in re.findall(r'\["(Gebaeude/[a-z]+/[a-z_]+)"\]\s*=\s*([0-9.]+)f', text))
for name, werte in GEMESSEN.items():
    for ordner, soll in zip(ORDNER, werte):
        schluessel = f"Gebaeude/{ordner}/{name}"
        if schluessel not in tabelle:
            fehler.append(f"DoorCenters: {schluessel} fehlt")
        elif abs(tabelle[schluessel] - soll) > 0.05:
            fehler.append(f"DoorCenters: {schluessel} = {tabelle[schluessel]}, gemessen {soll}")
if not re.search(r"Dictionary<string,\s*float>\s+DoorCenters\b", text):
    fehler.append("DoorCenters ist keine Dictionary<string, float>")

for name in ("DoorFraction", "DropOffStand"):
    rumpf = methode(text, name)
    if rumpf is None:
        fehler.append(f"{name} fehlt")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"{name}: Rumpf noch nicht gefüllt")
tuer = methode(text, "DoorFraction") or ""
if "DoorCenters" not in tuer or "AgeAsset" not in tuer:
    fehler.append("DoorFraction liest DoorCenters nicht über AgeAsset")
stand = methode(text, "DropOffStand") or ""
for pflicht in ("DoorFraction", "IsWalkable", "Accepts"):
    if pflicht not in stand:
        fehler.append(f"DropOffStand nutzt {pflicht} nicht")

folge = methode(text, "FollowJob") or ""
if "DropOffStand(" not in folge:
    fehler.append("FollowJob schickt die Einheit nicht per DropOffStand zur Tür")

welt = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMapGatherWorld.cs").read_text(encoding="utf-8-sig")
if re.search(r"private\s+static\s+bool\s+Accepts", welt):
    fehler.append("TileMapGatherWorld.Accepts ist noch private")

melde(fehler, "T1 erfuellt: Türtabelle gemessen, DoorFraction und DropOffStand gefüllt, FollowJob liefert vor der Tür ab")
