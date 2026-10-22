"""Strukturprüfung (SZ1, zufällige Startplätze): die feste Ecke ist raus.

Das Verhalten (Abstand ≥ halbe Kartenbreite, Randzone, vier Dorfbewohner,
Zufälligkeit) prüfen tests/AoE.Tests/StartplaetzeTests.cs (dotnet test) und
tools/kartenpruefung -- startabstand. Diese Datei prüft am Quelltext, dass
keine Startlogik mehr auf den alten Ecken (3, 3) und (Width - 4, Height - 4)
steht und alle Stellen die gewürfelten Plätze lesen. Eigenschaften, nie
Schreibweisen; die See-Streuung in AddLakes darf Width - 4 weiter benutzen -
die Prüfung schaut nur in die Start-Methoden.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

TILEMAP = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs")
SCREEN = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs")
fehler = []


def methode_text(datei: Path, name: str) -> str:
    text = methode(lies(datei), name)
    if text is None:
        fehler.append(f"{datei.name}: Methode {name} nicht gefunden")
        return ""
    if "NotImplementedException" in text:
        fehler.append(f"{datei.name}: {name} ist noch leer (NotImplementedException)")
    return text


def frei_von(text: str, wo: str, *teile: str) -> None:
    for teil in teile:
        if teil in text:
            fehler.append(f"{wo}: „{teil}“ steht noch da - die alte Ecke muss durch die "
                          f"gewürfelten Startplätze ersetzt sein")


def benutzt(text: str, wo: str, *teile: str) -> None:
    for teil in teile:
        if teil not in text:
            fehler.append(f"{wo}: „{teil}“ fehlt - erwartet werden die gewürfelten "
                          f"Startplätze (_startP1/_startP2)")


# ---Würfel-Methode gefüllt, StartPosition als echte Eigenschaft da ---
wahl = methode_text(TILEMAP, "ChooseStartPositions")
ges = re.search(r"StartPosition\s*\(int owner\)\s*=>", lies(TILEMAP))
if ges is None:
    fehler.append("TileMap.cs: StartPosition(int owner) nicht als =>-Eigenschaft gefunden")

# --- Reihenfolge: gewürfelt wird vor den Rohstoffklumpen ---
init = methode_text(TILEMAP, "InitializeRandomMap")
if "ChooseStartPositions()" not in init:
    fehler.append("InitializeRandomMap: ChooseStartPositions() wird nicht aufgerufen")
elif init.find("ChooseStartPositions()") > init.find("AddResourcePatches()"):
    fehler.append("InitializeRandomMap: ChooseStartPositions() muss VOR AddResourcePatches() laufen")

# --- keine Startlogik hält mehr an der Ecke fest ---
frei_von(methode_text(TILEMAP, "PlaceStartingPositions"), "PlaceStartingPositions",
         "Width - 4", "Height - 4", "= 3;", "(3, 3)")
benutzt(methode_text(TILEMAP, "PlaceStartingPositions"), "PlaceStartingPositions",
        "_startP1", "_startP2")
frei_von(methode_text(TILEMAP, "TooCloseToStart"), "TooCloseToStart",
         "Width - 4", "Height - 4", "- 3)")
benutzt(methode_text(TILEMAP, "TooCloseToStart"), "TooCloseToStart", "_startP1", "_startP2")
frei_von(methode_text(TILEMAP, "NearStartArea"), "NearStartArea", "4.5f", "2.5f")
benutzt(methode_text(TILEMAP, "NearStartArea"), "NearStartArea", "_startP1", "_startP2")
pfs = methode_text(TILEMAP, "PlaceDeer")
frei_von(pfs, "PlaceDeer", "(x: 3, y: 3)", "Width - 4")
benutzt(pfs, "PlaceDeer", "_startP1", "_startP2")
arp = methode_text(TILEMAP, "AddResourcePatches")
frei_von(arp, "AddResourcePatches", "PlaceStartResources(3, 3", "PlaceStartResources(Width - 4")
benutzt(arp, "AddResourcePatches", "PlaceStartResources(_startP1", "PlaceStartResources(_startP2")

# --- Startblick der Kamera folgt dem gewürfelten Platz ---
schirm = lies(SCREEN)
frei_von(schirm, "RTSGameplayScreen.cs", "GridToWorld(new Vector2(3, 3))")
benutzt(schirm, "RTSGameplayScreen.cs", "StartPosition(0)")

melde(fehler, "Startplätze: Würfel-Methode gefüllt und vor den Klumpen aufgerufen, "
              "alle Startstellen lesen _startP1/_startP2, Kamerasicht folgt Spieler 1")
