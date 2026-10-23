"""Strukturprüfung (C17): Mauszeiger-Hervorhebung - Vertrag und Einbindung.

  py c17_hover.py

Was HoverUnder trifft, prüft tools/spielablauf (Gruppe hover). Diese Datei sieht nach,
was ohne Grafik nicht zu prüfen ist: dass HandleRtsInput das Feld _hover jeden Bild
setzt (außer über der Leiste und im Setzmodus), dass der Aufhellungston Alpha 0 trägt
(sonst malt er statt zu hellen), und dass jede Zeichenschicht den hover-Schalter an
ihrer Aufrufstelle wirklich durchbekommt - Baum, Busch, Tier, Haufen, Gebäude, Figur.
Gegen den Altstand rot, weil die Teildatei und die Aufrufargumente dann fehlen.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

SCR = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
HAUPT = SCR / "RTSGameplayScreen.cs"
HOVER = SCR / "RTSGameplayScreen.Hover.cs"


def dicht(text: str) -> str:
    return re.sub(r"\s+", "", text)


fehler = []
haupt = lies(HAUPT)

# --- Teildatei: Vertrag und Ton -----------------------------------------------------
if not HOVER.exists():
    fehler.append(f"{HOVER} fehlt")
else:
    hover = lies(HOVER)
    HU = methode(hover, "HoverUnder")
    if HU is None:
        fehler.append("HoverUnder nicht gefunden")
    else:
        d = dicht(HU)
        for baustein in [
            "placing != null",                       # 1. Setzmodus hebt nichts
            "EnemyUnitUnder(worldPos)",              # 2. Gegner nur mit Auswahl
            "OwnUnitAt(worldPos)",                   # 3. eigene Figur
            "selectedUnits.Count == 0 && BuildingUnder(worldPos)",  # 4. Gebäude ohne Auswahl
            "OwnerId: not 0",                        # 5. fremdes Gebäude mit Auswahl
            "dmg.Health < dmg.MaxHealth && hasVillager",  # 7. Reparatur (C15)
            "PredatorAt(gridPos, 0)",                # 8. Raubtier (RT)
            "IsTileExplored(gx, gy, 0)",             # 9. Sammelgrund wie IssueCommand
        ]:
            if dicht(baustein) not in d:
                fehler.append(f"HoverUnder: es fehlt {baustein}")
    if "new Color(40, 40, 40, 0)" not in hover:
        fehler.append("HoverLift trägt nicht Alpha 0 - zweiter Durchgang würde übermalen statt addieren")

# --- HandleRtsInput setzt _hover ------------------------------------------------------
HI = methode(haupt, "HandleRtsInput")
d = "" if HI is None else dicht(HI)
if "_hover=" not in d:
    fehler.append("HandleRtsInput setzt _hover nicht")
if dicht("IsOverHud(mouse.Position)") not in d:
    fehler.append("HandleRtsInput: der Leistenrand schaltet die Hervorhebung nicht aus")
if dicht("placing != null") not in d:
    fehler.append("HandleRtsInput: der Setzmodus schaltet die Hervorhebung nicht aus")

# --- Einbindung: jede Zeichenschicht bekommt den Schalter -----------------------------
# Suchtexte dicht - so ist es egal, wo die Zeilen umbrechen.
dh = dicht(haupt)
for name, aufruf in [
    ("DrawTree", "DrawTree(spriteBatch, obj.X, obj.Y, obj.Spot, HoverWantsTile(obj.X, obj.Y));"),
    ("DrawBerryBush", "DrawBerryBush(spriteBatch, obj.X, obj.Y, HoverWantsTile(obj.X, obj.Y));"),
    ("DrawAnimal", "AnimalSprites(obj.Tile.Food), HoverWantsTile(obj.X, obj.Y));"),
    ("DrawBuilding", "DrawBuilding(spriteBatch, obj.Building, HoverWantsBuilding(obj.Building));"),
    ("DrawPile", "DrawPile(spriteBatch, x, y, PileSprites(tile.Type), HoverWantsTile(x, y));"),
]:
    if dicht(aufruf) not in dh:
        fehler.append(f"{name}: die Aufrufstelle übergibt den Hebeschalter nicht ({aufruf})")

for name in ["DrawTree", "DrawBerryBush", "DrawAnimal", "DrawPile", "DrawBuilding", "DrawVillager"]:
    signatur = re.search(rf"private void {name}\([^)]*bool hover = false\)", haupt)
    if signatur is None:
        fehler.append(f"{name}: der Signatur fehlt 'bool hover = false'")
    if "HoverLift" not in (methode(haupt, name) or ""):
        fehler.append(f"{name}: der Aufhellungsdurchgang (HoverLift) fehlt")

# Die Figur: der Tiefendurchgang merkt den Treffer und reicht ihn an DrawVillager
if "unitHover" not in haupt:
    fehler.append("DrawUnits: der Figurenzwig merkt sich den Figurtreffer nicht (unitHover)")

melde(fehler, "C17 Mauszeiger-Hervorhebung")
