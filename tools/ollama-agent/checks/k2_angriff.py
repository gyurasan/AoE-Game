"""Abnahme K2: eigene Einheiten greifen auf Befehl ein fremdes Gebäude an.

Wunsch des Nutzers (2026-10-06): "Wenn es angegriffen wird, sinkt die Gebäudestärke mit
der Zeit". Wie Angriff, Schlagtakt und Zerstörung wirken, prüft tools/spielablauf
(Gruppe angriff), den Schaden je Schlag die Tests (BuildingCombatTests). Hier: die
Rümpfe sind gefüllt und an den richtigen Stellen eingebunden.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
karte = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
einheit = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/Unit.cs").read_text(encoding="utf-8-sig")
fehler = []

for feld in ("AttackTarget", "AttackTimer"):
    if not re.search(rf"\bpublic\s+\w+\s+{feld}\s*\{{", einheit):
        fehler.append(f"Unit.{feld} fehlt")

entfernen = methode(karte, "RemoveBuilding") or ""
if "NotImplementedException" in entfernen or not entfernen:
    fehler.append("TileMap.RemoveBuilding: Rumpf nicht gefüllt")
for pflicht in ("Buildings.Remove", "Walkable", "NavGrid.RemoveBuilding", "_navDirty"):
    if pflicht not in entfernen:
        fehler.append(f"TileMap.RemoveBuilding: {pflicht} fehlt")

for name in ("AttackBuilding", "AttackStand", "DestroyBuilding"):
    rumpf = methode(text, name)
    if rumpf is None:
        fehler.append(f"{name} fehlt")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"{name}: Rumpf noch nicht gefüllt")
if "RemoveBuilding(" not in (methode(text, "DestroyBuilding") or ""):
    fehler.append("DestroyBuilding nimmt das Gebäude nicht von der Karte")

update = methode(text, "UpdateUnits") or ""
if not re.search(r"case\s+UnitState\.Attacking\s*:", update):
    fehler.append("UpdateUnits kennt den Zustand Attacking nicht")
for pflicht in ("BuildingCombat.Hit(", "BuildingCombat.RELOAD_SECONDS", "DestroyBuilding("):
    if pflicht not in update:
        fehler.append(f"UpdateUnits: {pflicht.rstrip('(')} fehlt")

befehl = re.search(r"internal\s+void\s+IssueCommand\s*\(\s*int\s+ownerId", text)
rumpf = text[befehl.start():befehl.start() + 3000] if befehl else ""
if "AttackBuilding(" not in rumpf:
    fehler.append("IssueCommand(ownerId, ...) schickt keine Einheiten in den Angriff")
if not re.search(r"AttackTarget\s*=\s*null", rumpf):
    fehler.append("IssueCommand(ownerId, ...) bricht einen Angriff bei anderen Befehlen nicht ab")

if "UnitState.Attacking" not in (methode(text, "ToolFor") or ""):
    fehler.append("ToolFor: Angreifer haben kein Werkzeug (Axt)")
if "UnitState.Attacking" not in (methode(text, "DrawVillager") or ""):
    fehler.append("DrawVillager: Angreifer holen nicht aus")

melde(fehler, "K2 erfuellt: Angriff per Befehl, Schlagtakt, Zerstörung, Kacheln frei, Axt-Animation")
