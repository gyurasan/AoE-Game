"""Abnahme G5: Boden aus einem Guss - Bodenshader, Trampelpfade, Seen nicht in den Startzonen.

Gras, Sand und Wasser zeichnet der Shader Content/Effects/Boden.fx in einem
Durchgang (RTSGameplayScreen.DrawGroundShaded); vorher kachelweise, mit eckigen
Ufern und Stränden und einem Wasserbild, dessen Wellenzeichen sich sichtbar
wiederholten. Wo Figuren laufen, tritt TileMap.Trample den Boden aus, und
TileMap.RegrowGrass lässt ihn nachwachsen - beides ruft das Spiel auf. Seen
würfelt AddLakes neu aus, wenn sie in eine Startzone reichen.

Das Verhalten der Trampelpfade prüft tools/spielablauf (Gruppe pfad), wie es
aussieht, prüfen Bildschirmfotos. Hier: dass alle Teile da und angeschlossen
sind - auch die Gruppe pfad selbst, deren Fehlen der Ablauf sonst still
übergehen würde.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
fx = KERN / "Content/Effects/Boden.fx"
mgcb = (KERN / "Content/AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
rts = lies(KERN / "Screens/RTSGameplayScreen.cs")
karte = lies(KERN / "Data/TileMap.cs")
kachel = lies(KERN / "Data/Tile.cs")
ablauf = lies(Path("tools/spielablauf/Program.cs"))
fehler = []

if not fx.is_file():
    fehler.append(f"{fx} fehlt")
else:
    shader = fx.read_text(encoding="utf-8-sig")
    for pflicht in ("technique", "LiveTexture", "GrassTexture", "SandTexture", "NoiseTexture", "MapTiles", "Time"):
        if pflicht not in shader:
            fehler.append(f"Boden.fx: {pflicht} fehlt")
if not re.search(r"#begin Effects/Boden\.fx\s+/importer:EffectImporter\s+/processor:EffectProcessor", mgcb) \
        or "/build:Effects/Boden.fx" not in mgcb:
    fehler.append("AgeOfEvolutions.mgcb baut Effects/Boden.fx nicht als Effekt")

if not re.search(r"\bpublic float Wear\s*\{\s*get;\s*set;\s*\}", kachel):
    fehler.append("Tile.Wear fehlt")
for name in ("Trample", "RegrowGrass", "NearStartArea"):
    if methode(karte, name) is None:
        fehler.append(f"TileMap.{name} fehlt")
if "NearStartArea(" not in (methode(karte, "AddLakes") or ""):
    fehler.append("AddLakes hält Seen nicht aus den Startzonen (NearStartArea)")

laden = methode(rts, "LoadContent") or ""
if not re.search(r"Content\.Load<Effect>\(\s*\"Effects/Boden\"\s*\)", laden):
    fehler.append("LoadContent lädt Effects/Boden nicht")
for name in ("DrawGroundShaded", "BuildGroundControl", "BuildNoiseTexture", "RefreshGroundLive"):
    if methode(rts, name) is None:
        fehler.append(f"RTSGameplayScreen.{name} fehlt")
if "DrawGroundShaded(" not in (methode(rts, "Draw") or ""):
    fehler.append("Draw ruft DrawGroundShaded nicht auf")
if not re.search(r"tileMap\.Trample\(", methode(rts, "StepAlongPath") or ""):
    fehler.append("StepAlongPath tritt den Boden nicht aus (tileMap.Trample)")
if not re.search(r"tileMap\.RegrowGrass\(", methode(rts, "Update") or ""):
    fehler.append("Update lässt das Gras nicht nachwachsen (tileMap.RegrowGrass)")

if not re.search(r"gruppen\.Contains\(\"pfad\"\)", ablauf) or methode(ablauf, "Pfad") is None:
    fehler.append("tools/spielablauf: Gruppe pfad fehlt")
if not re.search(r"\bMap\.RegrowGrass\(dt\)", methode(ablauf, "LaufeBis") or ""):
    fehler.append("tools/spielablauf: LaufeBis lässt das Gras nicht nachwachsen wie das Spiel")

melde(fehler, "G5 erfuellt: Bodenshader angeschlossen, Trampelpfade und Seen außerhalb der Startzonen")
