"""Strukturprüfung (F1): Gebäude schießen - Vertrag und Einbindung.

  py f1_beschuss.py

Was der Beschuss im Ablauf tut, prüft tools/spielablauf (Gruppe beschuss). Diese
Datei sieht nach, was ohne Grafik nicht zu prüfen ist: dass UpdateBombard in der
Einheitenschleife hängt und dabei unvollendete Gebäude und die Prüfwelt aussperrt,
dass die Werte in AoE.Core stehen (Stadtzentrum 5/6, Takt 2 s), dass der Schaden
über den allgemeinen DamageCalculator läuft (Fernkampf gegen Fernkampf-Rüstung),
dass Befiederte Pfeile die schießenden Gebäude mitnehmen, und dass die KI bewehrte
Gebäude kennt (IsArmed von der Brücke bis zur Wellenauswahl).
Gegen den Altstand rot, weil Teildatei, Werte und IsArmed dann fehlen.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E404


def dicht(text: str) -> str:
    return re.sub(r"\s+", "", text)


SCR = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
BESCHUSS = SCR / "RTSGameplayScreen.Beschuss.cs"
HAUPT = SCR / "RTSGameplayScreen.cs"
BUILDINGCOMBAT = Path("src/AoE.Core/Combat/BuildingCombat.cs")
BUILDINGS = Path("src/AoE.Core/Entities/Buildings.cs")
RESEARCH = Path("src/AoE.Core/Economy/Research.cs")
IAIWORLD = Path("src/AoE.Core/Ai/IAiWorld.cs")
ECONOMYAI = Path("src/AoE.Core/Ai/EconomyAi.cs")
BRIDGE = Path("AgeOfEvolutions/AgeOfEvolutions.Core/AI/AiBridge.cs")
TILEMAP = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs")

fehler = []

# --- Teildatei Beschuss: Takt, Ziel, Schaden, Klang ----------------------------------
if not BESCHUSS.exists():
    fehler.append(f"{BESCHUSS} fehlt")
else:
    text = lies(BESCHUSS)
    UB = methode(text, "UpdateBombard")
    if UB is None:
        fehler.append("UpdateBombard nicht gefunden")
    else:
        d = dicht(UB)
        for brocken, grund in [
            ("BuildingCombat.Shoots(b.Core)", "schießt ohne die Schuss-Regel zu prüfen"),
            ("!b.IsComplete", "schießt ohne die IsComplete-Wache (Baustellen schießen mit)"),
            ("!TestBombard", "kennt den Prüfschalter der Prüfwelt nicht"),
            ("BuildingCombat.DamageToUnit", "rechnet den Schaden nicht über AoE.Core"),
            ('"bogen"', "klingt nicht wie ein Bogen"),
            ("_arrows.Add", "schickt keinen Pfeil durch die Luft (A3-Bahn)"),
            ("BombardTarget(b)", "sucht kein Ziel"),
        ]:
            if dicht(brocken) not in d:
                fehler.append(f"UpdateBombard: {dicht(brocken)} fehlt - {grund}")
        if "RegisterDamageTaken" not in UB:
            fehler.append("UpdateBombard meldet den Angriff nicht (RegisterDamageTaken)")
    BT = methode(text, "BombardTarget")
    if BT is None:
        fehler.append("BombardTarget nicht gefunden")
    else:
        d = dicht(BT)
        if "IsTileVisible" not in BT:
            fehler.append("BombardTarget: keine Sichtprüfung - der Nebel wäre Wissensvorteil")
        if "UnitState.Dead" not in BT:
            fehler.append("BombardTarget: Tote wären zulässige Ziele")

# --- Einbindung in die Einheitenschleife ----------------------------------------------
haupt = lies(HAUPT)
uu = methode(haupt, "UpdateUnits")
if uu is None or "UpdateBombard(dt)" not in uu:
    fehler.append("UpdateBombard hängt nicht in UpdateUnits (kein Schuss im Spiel)")

# --- Werte in AoE.Core -----------------------------------------------------------------
bc = lies(BUILDINGCOMBAT)
if "BOMBARD_SECONDS=2f" not in dicht(bc):
    fehler.append("BuildingCombat: BOMBARD_SECONDS ist nicht 2 s")
sh = methode(bc, "Shoots")
if sh is None or "BaseAttack>0" not in dicht(sh) or "Range>0" not in dicht(sh):
    fehler.append("BuildingCombat: Shoots prüft nicht Angriff und Reichweite")
dt = methode(bc, "DamageToUnit")
if dt is None or "DamageCalculator" not in dt:
    fehler.append("BuildingCombat: DamageToUnit rechnet nicht über DamageCalculator")

tc = lies(BUILDINGS)
m = re.search(r"CreateTownCenter[\s\S]*?return new BuildingEntity", tc)
if m is None or "BaseAttack = 5" not in m.group(0) or "Range = 6" not in m.group(0):
    fehler.append("Stadtzentrum: kein eigener Angriff (5) auf Reichweite 6")

res = lies(RESEARCH)
fl = methode(res, "Apply")
if fl is None or "BuildingEntity{Stats.Range:>0}" not in dicht(fl):
    fehler.append("TechEffects.Apply: Befiederte Pfeile heben die schießenden Gebäude nicht")

# --- Die KI kennt bewehrte Gebäude -----------------------------------------------------
if "IsArmed" not in lies(IAIWORLD):
    fehler.append("EnemyInfo: kein IsArmed")
bridge = lies(BRIDGE)
if "IsArmed:" not in bridge or "Shoots(b.Core)" not in bridge:
    fehler.append("AiBridge: IsArmed wird nicht gesetzt")
ai = lies(ECONOMYAI)
if "!e.IsBuilding||e.IsArmed" not in dicht(ai):
    fehler.append("EconomyAi.Raid: bewehrte Gebäude werden nicht gemieden")

# --- Der Takt liegt am Gebäude ----------------------------------------------------------
tm = lies(TILEMAP)
if "BombardTimer" not in tm:
    fehler.append("Building: kein BombardTimer (der Takt fiele jeden Bild neu an)")

melde(fehler, "f1: Wachturm, Stadtzentrum und Burg schießen - Vertrag und Einbindung")
