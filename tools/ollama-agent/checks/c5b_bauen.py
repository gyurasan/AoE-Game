"""Abnahme C5b: Bauen im Spiel - Baumenue, Baustellen, Bauarbeiter, fertige Lager.

Prueft die Verdrahtung im Quelltext. Bauzeit und abnehmenden Ertrag pruefen die
xUnit-Tests (ConstructionTests.cs); wie es im Spiel aussieht, zeigt erst ein
Screenshot. Die Zeichensatz-Pruefung fuer Strings macht c2b_ausbildung.py.
Loest c2c_haus.py ab: der Sonderweg nur fuer Haeuser ist ins Baumenue aufgegangen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

fehler = []
DATA = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data")


def datei(name: str) -> str:
    return (DATA / name).read_text(encoding="utf-8-sig")


def rumpf(text: str, name: str) -> str:
    r = methode(text, name)
    if r is None:
        fehler.append(f"Methode {name} fehlt")
        return ""
    return r


def verlangt(text: str, muster: str, was: str) -> None:
    if not re.search(muster, text):
        fehler.append(was)


# --- Daten ---------------------------------------------------------------
tilemap = datei("TileMap.cs")
verlangt(tilemap, r"public\s+Building\s+AddBuilding\s*\(", "TileMap.AddBuilding gibt das Gebaeude nicht zurueck")
verlangt(rumpf(tilemap, "AddBuilding"), r"return\s+building\s*;", "AddBuilding gibt das neue Gebaeude nicht zurueck")
verlangt(tilemap, r"Construction\s+Construction\s*\{\s*get\s*;\s*set\s*;\s*\}", "Building hat keine Baustelle (Construction)")
verlangt(tilemap, r"bool\s+IsComplete\s*=>[^;]*Construction\s*==\s*null", "Building.IsComplete fehlt oder gilt nicht fuer fertige Gebaeude")
if re.search(r"class\s+ConstructionSite\b", tilemap):
    fehler.append("die ungenutzte Klasse ConstructionSite steht noch in TileMap.cs")

unit = datei("Unit.cs")
verlangt(unit, r"public\s+Building\s+BuildSite\s*\{\s*get\s*;\s*set\s*;\s*\}", "Unit.BuildSite fehlt")
for alt in ("IsBuilding", "ConstructionProgress"):
    if re.search(rf"\b{alt}\b", unit):
        fehler.append(f"das ungenutzte Feld Unit.{alt} steht noch da")

core = datei("CoreBuildings.cs")
verlangt(core, r"\"Holzfällerlager\"\s*=>\s*BuildingEntity\.CreateLumberCamp\s*\(", "CoreBuildings kennt das Holzfaellerlager nicht")
verlangt(core, r"\"Bergbaulager\"\s*=>\s*BuildingEntity\.CreateMiningCamp\s*\(", "CoreBuildings kennt das Bergbaulager nicht")

verlangt(rumpf(datei("TileMapGatherWorld.cs"), "FindNearestDropOff"), r"\.IsComplete\b",
         "eine Baustelle zaehlt schon als Abgabestelle")

# --- Bildschirm ----------------------------------------------------------
screen = lies()
for taste, typ in (("H", "House"), ("M", "Mill"), ("F", "LumberCamp"), ("B", "MiningCamp")):
    verlangt(screen, rf"\(\s*Keys\.{taste}\s*,\s*BuildingType\.{typ}\s*,\s*\"[^\"]+\"\s*\)",
             f"Baumenue: Taste {taste} fuer {typ} fehlt")
for alt in ("placingHouse", "HouseCost", "HOUSE_SIZE", "PlaceHouse", "CanPlaceHouse"):
    if re.search(rf"\b{alt}\b", screen):
        fehler.append(f"Reste des Haus-Sonderwegs: {alt}")

eingabe = rumpf(screen, "HandleRtsInput")
verlangt(eingabe, r"foreach\s*\(\s*var\s+\w+\s+in\s+BuildMenu\s*\)", "die Tasten des Baumenues werden nicht abgefragt")
verlangt(eingabe, r"previousKeyboard\.IsKeyUp\s*\(\s*\w+\.Key\s*\)", "Baumenue-Tasten loesen bei gehaltener Taste jeden Frame aus")
# Seit C8b steht die Klick-Entscheidung in LeftClick; die erste Fassung verlangte
# PlaceBuilding woertlich in HandleRtsInput.
klick = methode(screen, "LeftClick") or ""
if not (re.search(r"PlaceBuilding\s*\(", eingabe)
        or (re.search(r"LeftClick\s*\(", eingabe) and re.search(r"PlaceBuilding\s*\(", klick))):
    fehler.append("der Linksklick legt keine Baustelle an")

setzen = rumpf(screen, "PlaceBuilding")
reihenfolge = [setzen.find(s) for s in ("CanPlace(", "PayCost(", "AddBuilding(")]
if -1 in reihenfolge:
    fehler.append("PlaceBuilding prueft, bezahlt oder legt nicht an")
elif reihenfolge != sorted(reihenfolge):
    fehler.append("PlaceBuilding muss erst pruefen, dann bezahlen, dann anlegen")
verlangt(setzen, r"new\s+Construction\s*\(\s*BuildingRules\.BuildSecondsOf\s*\(", "die Baustelle bekommt keine Bauzeit aus BuildingRules")
verlangt(setzen, r"CostOf\s*\(", "die Kosten kommen nicht aus BuildingRules")
verlangt(setzen, r"AssignBuilder\s*\(", "die ausgewaehlten Dorfbewohner gehen nicht bauen")

platz = rumpf(screen, "CanPlace")
for muster, was in (
    (r"CanPlaceBuilding\s*\(", "CanPlace nutzt TileMap.CanPlaceBuilding nicht"),
    (r"IsTileExplored\s*\(", "CanPlace erlaubt Bauen im Unerforschten"),
    (r"SizeOf\s*\(", "CanPlace kennt die Groesse des Gebaeudes nicht"),
    (r"\bunits\b", "CanPlace prueft nicht auf Einheiten"),
):
    verlangt(platz, muster, was)

verlangt(rumpf(screen, "AssignBuilder"), r"SiteStandCell\s*\(", "AssignBuilder sucht keinen Platz am Rand")
verlangt(rumpf(screen, "SiteStandCell"), r"IsWalkable\s*\(", "SiteStandCell prueft die Begehbarkeit nicht")

bau = rumpf(screen, "UpdateConstruction")
verlangt(bau, r"\.Construction\.Update\s*\(", "UpdateConstruction baut nicht")
verlangt(bau, r"UnitState\.Building", "UpdateConstruction zaehlt die Bauarbeiter nicht nach ihrem Zustand")
verlangt(bau, r"FinishConstruction\s*\(", "fertige Gebaeude schicken ihre Erbauer nicht weiter")
verlangt(rumpf(screen, "Update"), r"UpdateConstruction\s*\(", "Update baut nicht (UpdateConstruction)")

fertig = rumpf(screen, "FinishConstruction")
verlangt(fertig, r"FindNearestSource\s*\(", "nach einem Lager wird keine Ressource gesucht")
verlangt(fertig, r"new\s+GatherJob\s*\(", "nach einem Lager wird nicht gesammelt")
verlangt(fertig, r"BuildSite\s*=\s*null", "fertige Erbauer behalten ihre Baustelle")
verlangt(rumpf(screen, "ResourcesFor"), r"LumberCamp[\s\S]*MiningCamp[\s\S]*Mill|Mill[\s\S]*LumberCamp",
         "ResourcesFor ordnet die Lager nicht zu")

verlangt(rumpf(screen, "UpdatePopulationLimits"), r"\.IsComplete\b", "eine Baustelle zaehlt schon als Wohnraum")

befehl = rumpf(screen, "IssueCommand")
verlangt(befehl, r"BuildSite\s*=\s*null", "ein neuer Befehl beendet das Bauen nicht")
verlangt(befehl, r"AssignBuilder\s*\(", "Dorfbewohner koennen nicht an einer Baustelle mitbauen")

verlangt(rumpf(screen, "UpdateUnits"), r"UnitState\.Building", "UpdateUnits kennt den Zustand Building nicht")
zeichnen = rumpf(screen, "Draw")
verlangt(zeichnen, r"placing\b", "Draw zeigt keine Vorschau beim Setzen")
verlangt(zeichnen, r"SizeOf\s*\(", "die Vorschau kennt die Groesse des Gebaeudes nicht")
verlangt(rumpf(screen, "DrawUI"), r"BuildMenu", "die untere Leiste zeigt das Baumenue nicht")

melde(fehler, "C5b erfuellt: Baumenue, Baustellen mit Bauarbeitern, nur fertige Gebaeude zaehlen")
