"""Abnahme P1: ausgewählte Gebäude bilden aus - Kaserne Miliz, Schießstand Bogenschütze, Stall Späher.

Wunsch des Nutzers (2026-10-06): "Bestimmte Dinge können hergestellt werden, wenn ein
Gebäude selektiert ist". Wie Tasten, Kosten, Ausbildungszeit, Standplatz und Angriff der
Soldaten wirken, prüft tools/spielablauf (Gruppe soldaten, dazu auswahl und leiste). Hier:
die Rümpfe sind gefüllt, Soldaten werden geladen und gezeichnet, Symbole nach dem Namen.
Wie die Soldaten im Spiel aussehen, zeigt erst ein Foto - die Startprobe zeichnet keine.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
karte = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
einheit = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/Unit.cs").read_text(encoding="utf-8-sig")
kern = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreUnits.cs").read_text(encoding="utf-8-sig")
fehler = []

for typ in ("Militia", "Scout"):
    if not re.search(rf"\b{typ}\s*,", einheit):
        fehler.append(f"UnitType.{typ} fehlt")

# CoreUnits: Kern, deutscher Name und Waffengattung je Soldat
erzeugen = methode(kern, "Create") or ""
# new Militia( steht schon für HeavyInfantry und ManAtArms da - deshalb auch der Typ selbst
for typ in ("Militia", "Scout"):
    if not re.search(rf"UnitType\.{typ}\b", erzeugen) \
            or not re.search(rf"\bnew\s+(AoE\.Core\.Entities\.)?{typ}\s*\(", erzeugen):
        fehler.append(f"CoreUnits.Create erzeugt für UnitType.{typ} keinen Kern {typ}")
name = methode(kern, "GermanName") or ""
for typ, deutsch in (("Militia", "Miliz"), ("Scout", "Späher")):
    if typ not in name or f'"{deutsch}"' not in name:
        fehler.append(f"CoreUnits.GermanName kennt {typ} ({deutsch}) nicht")
gattung = methode(kern, "CategoryOf") or ""
for typ, soll in (("Militia", "Infantry"), ("Scout", "Cavalry")):
    if typ not in gattung:
        fehler.append(f"CoreUnits.CategoryOf ordnet {typ} nicht zu ({soll})")

hinzu = methode(karte, "AddUnit")
if hinzu is None:
    fehler.append("TileMap.AddUnit fehlt")
elif "NotImplementedException" in hinzu:
    fehler.append("TileMap.AddUnit: Rumpf noch nicht gefüllt")
elif "Units.Add(" not in hinzu:
    fehler.append("TileMap.AddUnit nimmt die Einheit nicht in Units auf")

ausbilden = methode(text, "Train")
if ausbilden is None:
    fehler.append("Train fehlt")
elif "NotImplementedException" in ausbilden:
    fehler.append("Train: Rumpf noch nicht gefüllt")
else:
    for pflicht in ("Products", ".Enqueue(", "ShowHudMessage(", "Ages.Current"):
        if pflicht not in ausbilden:
            fehler.append(f"Train: {pflicht.rstrip('(')} fehlt")


def rumpf_ohne_parameter(name: str) -> str:
    """Rumpf der Fassung ohne Parameter - TrainVillager(int ownerId) ist die KI-Schnittstelle."""
    treffer = re.search(r"\bvoid\s+" + name + r"\(\)\s*\{", text)
    if not treffer:
        return ""
    tiefe = 0
    for j in range(treffer.end() - 1, len(text)):
        tiefe += {"{": 1, "}": -1}.get(text[j], 0)
        if tiefe == 0:
            return text[treffer.end() - 1:j + 1]
    return ""


if not re.search(r"\bTrain\s*\(", rumpf_ohne_parameter("TrainVillager")):
    fehler.append("TrainVillager() bildet nicht über Train(Gebäude, Einheit) aus")

eingabe = methode(text, "HandleRtsInput") or ""
if "Products" not in eingabe or not re.search(r"\bTrain\s*\(", eingabe):
    fehler.append("HandleRtsInput: die Tasten aus Products bilden nicht aus")
tasten = methode(text, "LayoutButtons") or ""
if "Products" not in tasten or not re.search(r"\bTrain\s*\(", tasten):
    fehler.append("LayoutButtons: keine Tasten aus Products für das ausgewählte Gebäude")
# AddButton gibt es zweimal (mit und ohne BuildingType?) - alle Rümpfe zusammen
knoepfe = "".join(methode(text[m.start():], "AddButton") or ""
                  for m in re.finditer(r"\bvoid\s+AddButton\s*\(", text))
if "_nameIcons" not in knoepfe:
    fehler.append("AddButton: Symbole nicht nach dem Namen (_nameIcons)")

laden = methode(text, "LoadContent") or ""
for feld, quelle in (("_nameIcons", "NameIcons"), ("_unitSprites", "SoldierSprites"), ("_unitWalk", "SoldierSprites")):
    if not re.search(rf"\b{feld}\s*\[[^\]]*\]\s*=", laden):
        fehler.append(f"LoadContent füllt {feld} nicht")
    if quelle not in laden:
        fehler.append(f"LoadContent liest {quelle} nicht")

if not re.search(r"tileMap\.AddUnit\s*\(", methode(text, "UpdateTraining") or ""):
    fehler.append("UpdateTraining setzt die fertige Einheit nicht per tileMap.AddUnit ab")
einheiten = methode(text, "DrawUnits") or ""
if "_unitSprites" not in einheiten or "SoldierSprites" not in einheiten:
    fehler.append("DrawUnits zeichnet keine Soldaten (_unitSprites, Größe aus SoldierSprites)")
figur = methode(text, "DrawVillager") or ""
if "CoreVillager" not in figur or "_unitWalk" not in figur:
    fehler.append("DrawVillager: Werkzeug nur für Dorfbewohner, Laufbilder der Soldaten aus _unitWalk")
if "GermanName(" not in (methode(text, "BuildingStatus") or ""):
    fehler.append("BuildingStatus nennt die Einheit nicht deutsch (CoreUnits.GermanName)")

melde(fehler, "P1 erfuellt: Kaserne, Schießstand und Stall bilden aus, Soldaten geladen und gezeichnet")
