"""Abnahme H1–H3: natürlicheres Gehen von Dorfbewohnern, Schafen und Wild.

Vorher kippelte ein gehender Dorfbewohner bei jedem Schritt um bis zu 3 Grad von
Seite zu Seite, seine Laufbilder wechselten im festen Takt der Uhr (bei anderem
Tempo rutschten die Füße), er sprang mit vollem Tempo los, blieb auf der Stelle
stehen und folgte dem Zickzack der Kachelmitten. Tiere glitten in genau einer
Sekunde gleichmäßig von Kachel zu Kachel. Jetzt rechnet Screens/Gait.cs Laufbild,
Wippen, Anfahren, Abbremsen, Vorlage und den weichen Tierschritt, und
TileMap.IsSegmentWalkable lässt Dorfbewohner über freies Land gerade gehen.

Was die Rechnungen tun, prüft tools/spielablauf (Gruppen gang, gehen, herde). Hier:
dass Spielbildschirm und Einheit sie benutzen.

  py h_gang.py h1 | h2 | h3
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
teil = sys.argv[1] if len(sys.argv) > 1 else "h1"
fehler = []

if teil == "h1":
    gait = lies(KERN / "Screens/Gait.cs")
    karte = lies(KERN / "Data/TileMap.cs")
    for name in ("WalkFrame", "Bob", "Approach", "ArrivalSpeed", "Lean", "Ease", "EaseSpeed"):
        if "NotImplementedException" in (methode(gait, name) or "NotImplementedException"):
            fehler.append(f"Gait.{name} ist noch der leere Rumpf")
    if "NotImplementedException" in (methode(karte, "IsSegmentWalkable") or "NotImplementedException"):
        fehler.append("TileMap.IsSegmentWalkable ist noch der leere Rumpf")

elif teil == "h2":
    rts = lies(KERN / "Screens/RTSGameplayScreen.cs")
    einheit = lies(KERN / "Data/Unit.cs")
    if not re.search(r"\bpublic float Pace\s*\{\s*get;\s*set;\s*\}", einheit):
        fehler.append("Unit.Pace fehlt")
    schritt = methode(rts, "StepAlongPath") or ""
    for muster, grund in ((r"tileMap\.IsSegmentWalkable\(", "überspringt keine Wegpunkte (IsSegmentWalkable) - Zickzack bleibt"),
                          (r"Gait\.Approach\(", "fährt nicht an (Gait.Approach)"),
                          (r"Gait\.ArrivalSpeed\(", "bremst nicht vor dem Ziel (Gait.ArrivalSpeed)"),
                          (r"\.Pace\b", "nutzt das Tempo der Einheit (Pace) nicht")):
        if not re.search(muster, schritt):
            fehler.append(f"StepAlongPath {grund}")
    dorf = methode(rts, "DrawVillager") or ""
    for muster, grund in ((r"Gait\.Bob\(", "wippt nicht nach der Strecke (Gait.Bob)"),
                          (r"Gait\.Lean\(", "neigt sich nicht nach vorn (Gait.Lean)"),
                          (r"\bWalked\b", "kennt die gelaufene Strecke (Walked) nicht")):
        if not re.search(muster, dorf):
            fehler.append(f"DrawVillager {grund}")
    if re.search(r"MathF\.Sin\(\s*step\s*\)\s*\*\s*0\.05f", dorf):
        fehler.append("DrawVillager kippelt noch von Seite zu Seite (MathF.Sin(step) * 0.05f)")
    if not re.search(r"Gait\.WalkFrame\(", methode(rts, "VillagerWalkPhase") or ""):
        fehler.append("VillagerWalkPhase wählt das Laufbild nicht nach der Strecke (Gait.WalkFrame)")
    if not re.search(r"\bWalked\s*\+=", methode(rts, "UpdateUnitMotion") or ""):
        fehler.append("UpdateUnitMotion zählt die gelaufene Strecke nicht (Walked +=)")

elif teil == "h3":
    rts = lies(KERN / "Screens/RTSGameplayScreen.cs")
    tier = methode(rts, "DrawAnimal") or ""
    # Seit T1a rechnet AnimalFoot die Hufe samt Rest des Schritts für DrawAnimal und die
    # Tiefenschicht - was dort steht, zählt für DrawAnimal mit
    if "AnimalFoot(" in tier:
        tier += methode(rts, "AnimalFoot") or ""
    for muster, grund in ((r"Gait\.Ease\(", "gleitet nicht weich (Gait.Ease)"),
                          (r"Gait\.EaseSpeed\(", "wippt nicht nach dem Tempo (Gait.EaseSpeed)"),
                          (r"\bGlide\b", "zeichnet den Schritt nicht (Glide)"),
                          (r"\bFromX\b", "kennt die Herkunft des Schritts nicht (FromX)"),
                          (r"\bWalkPhase\s*\(", "wählt das Laufbild nicht über WalkPhase")):
        if not re.search(muster, tier):
            fehler.append(f"DrawAnimal {grund}")
    if not re.search(r"Gait\.Ease\(", methode(rts, "WalkPhase") or ""):
        fehler.append("WalkPhase folgt nicht dem weichen Schritt (Gait.Ease) - die Beine rutschten")

melde(fehler, f"{teil.upper()} erfuellt")
