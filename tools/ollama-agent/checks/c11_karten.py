"""Abnahme C11: drei Kartengrößen, im Hauptmenü wählbar.

Wie die Karten aussehen und ob sie gleich dicht besetzt sind, prüfen
tools/kartenpruefung (groessen) und tools/spielablauf (karten, menue) - beide
Gruppen entstehen bzw. wachsen in derselben Aufgabe. Hier deshalb unabhängig:
es gibt sie, das Hauptmenü reicht die gewählte Größe an das Spiel weiter, und der
Spielbildschirm baut die Karte in dieser Größe mit MapSettings.ForSize.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
screen = lies()
menue = lies(KERN / "Screens/MainMenuScreen.cs")
settings = lies(KERN / "Data/MapSettings.cs")
einst = lies(KERN / "Settings/AgeOfEvolutionsSettings.cs")
ablauf = lies(Path("tools/spielablauf/Program.cs"))
pruef = lies(Path("tools/kartenpruefung/Program.cs"))
fehler = []

if not re.search(r"enum\s+MapSize\s*\{\s*Standard\s*,\s*Large\s*,\s*Max\s*\}", settings):
    fehler.append("enum MapSize { Standard, Large, Max } fehlt")
for zahl in ("64", "90", "128"):
    if not re.search(rf"=>\s*{zahl}\s*,", methode(settings, "Side") or ""):
        fehler.append(f"MapSizes.Side kennt die Seitenlänge {zahl} nicht")
if methode(settings, "ForSize") is None:
    fehler.append("MapSettings.ForSize fehlt")
laden = methode(screen, "LoadContent") or ""
if not re.search(r"new\s+TileMap\s*\(\s*side\s*,\s*side\s*,\s*32\s*,\s*MapSettings\.ForSize\s*\(\s*_mapSize\s*\)\s*\)", laden):
    fehler.append("RTSGameplayScreen baut die Karte nicht in der gewählten Größe (MapSizes.Side, MapSettings.ForSize)")
if "new TileMap(64, 64" in screen:
    fehler.append("RTSGameplayScreen baut noch fest eine 64x64-Karte")
waehlen = methode(menue, "HandleMenuSelect") or ""
if not re.search(r"new\s+RTSGameplayScreen\s*\(\s*mapSize\s*\)", waehlen):
    fehler.append("das Hauptmenü startet das Spiel nicht mit der gewählten Kartengröße")
if not re.search(r"Settings\.MapSize\s*=", waehlen) or "Save()" not in waehlen:
    fehler.append("das Hauptmenü speichert die gewählte Kartengröße nicht")
if not re.search(r"\bint\s+MapSize\s*\{", einst):
    fehler.append("AgeOfEvolutionsSettings.MapSize fehlt")
if not re.search(r'gruppen\.Contains\("karten"\)', ablauf):
    fehler.append("tools/spielablauf hat keine Gruppe 'karten'")
if not re.search(r'gruppen\.Contains\("groessen"\)', pruef):
    fehler.append("tools/kartenpruefung hat keine Gruppe 'groessen'")

melde(fehler, "C11 erfuellt: drei Kartengrößen, im Hauptmenü gewählt, gespeichert und an das Spiel weitergereicht")
