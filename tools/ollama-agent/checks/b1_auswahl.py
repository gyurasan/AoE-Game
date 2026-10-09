"""Abnahme B1: Gebäude auswählen - Status, Rahmen, Lebensbalken, Q/A am Stadtzentrum.

Bis 2026-10-06 ließ sich nur eine Einheit auswählen, und Q/A wirkten immer auf das
erste Stadtzentrum (Wunsch des Nutzers: "Das nächste Feature muss das Selektieren
eines Gebäudes haben ... auch hat es einen Status"). Wie Auswahl, Tasten und Status
wirken, prüft tools/spielablauf (Gruppe auswahl, dazu leiste und zeitalter). Hier:
die Rümpfe sind gefüllt, gezeichnet wird, und Q/A hängen am ausgewählten Stadtzentrum.
Ob Rahmen und Balken gut aussehen, zeigt ein Foto.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

if not re.search(r"\bData\.Building\s+selectedBuilding\s*;", text):
    fehler.append("Feld selectedBuilding fehlt")

for name in ("BuildingUnder", "SelectBuilding", "BuildingStatus"):
    rumpf = methode(text, name)
    if rumpf is None:
        fehler.append(f"{name} fehlt")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"{name}: Rumpf noch nicht gefüllt")
for name in ("DrawBuildingSelection", "DrawBuildingHealth"):
    rumpf = methode(text, name) or ""
    if "spriteBatch.Draw(" not in rumpf:
        fehler.append(f"{name} zeichnet nichts")

unter = methode(text, "BuildingUnder") or ""
for pflicht in ("BuildingSpriteHeight", "IsTileExplored"):
    if pflicht not in unter:
        fehler.append(f"BuildingUnder nutzt {pflicht} nicht")

def ohne_parameter(name: str) -> str:
    """Rumpf der Fassung ohne Parameter - die mit int ownerId ist die KI-Schnittstelle
    und steht bei AdvanceAge vor der menschlichen."""
    treffer = re.search(r"\bvoid\s+" + name + r"\(\)\s*\{", text)
    if not treffer:
        return ""
    tiefe = 0
    for j in range(treffer.end() - 1, len(text)):
        tiefe += {"{": 1, "}": -1}.get(text[j], 0)
        if tiefe == 0:
            return text[treffer.end() - 1:j + 1]
    return ""


for name in ("TrainVillager", "AdvanceAge"):
    rumpf = ohne_parameter(name)
    if "SelectedOwnTownCenter()" not in rumpf:
        fehler.append(f"{name}() fragt nicht das ausgewählte Stadtzentrum (SelectedOwnTownCenter)")
    if "TownCenterOf(0)" in rumpf:
        fehler.append(f"{name}() nimmt noch das erste Stadtzentrum (TownCenterOf(0))")

tasten = methode(text, "LayoutButtons") or ""
if "SelectedOwnTownCenter()" not in tasten or "_buttonsLayoutBuilding" not in tasten:
    fehler.append("LayoutButtons: Q/A hängen nicht am ausgewählten Stadtzentrum, oder die Tasten werden beim Wechsel nicht neu angelegt")
if "BuildingStatus(" not in (methode(text, "DrawUI") or ""):
    fehler.append("DrawUI zeigt den Gebäudestatus nicht")
# Der Auswahlrahmen liegt unter den Bildern (DrawTileMap); die Lebensbalken zeichnet seit T1a
# die Tiefenschicht über alle Bilder (DrawUnits) - in DrawTileMap lagen sie unter Bäumen
karte = methode(text, "DrawTileMap") or ""
if "DrawBuildingSelection(" not in karte:
    fehler.append("DrawTileMap ruft DrawBuildingSelection nicht auf")
if "DrawBuildingHealth(" not in karte + (methode(text, "DrawUnits") or ""):
    fehler.append("weder DrawTileMap noch DrawUnits ruft DrawBuildingHealth auf")
for name in ("SelectSingleUnit", "LeftClick"):
    if "BuildingUnder(" not in (methode(text, name) or ""):
        fehler.append(f"{name} wählt keine Gebäude aus (BuildingUnder)")

melde(fehler, "B1 erfuellt: Gebäude auswählbar, Status, Rahmen und Lebensbalken, Q/A am ausgewählten Stadtzentrum")
