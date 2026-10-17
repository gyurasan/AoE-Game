"""Abnahme G14: der Boden im Nebel zeigt den zuletzt gesehenen Stand.

Der Nutzer (2026-10-16): „Sichtbares gelende verändert den gras typ obwohl man niemanden dort
sieht. Das sollte nicht sein. Auch wenn tiere durchlaufen die man nicht sieht sollte sich das
Gras nicht ändern.“ Ursache: RefreshGroundLive übertrug alle 0,25 s jede Kachel ins Lebendbild
des Bodenshaders, und die Büschel des hohen Grases fragten TallGrassAt live ab - das auf Kacheln
mit Tier 0 meldet. Das Verhalten prüft tools/spielablauf (Gruppe nebelboden); hier, dass das
Spiel es auch benutzt: RefreshGroundLive lädt das Gedächtnis hoch, der Spielbeginn füllt es
ganz, und die Büschel lesen das Gesehene.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

cs = lies()
fehler = []

for name in ("UpdateGroundMemory", "SeenTallGrassAt"):
    rumpf = methode(cs, name)
    if rumpf is None or "NotImplementedException" in rumpf:
        fehler.append(f"{name} ist nicht umgesetzt")

auffrischen = methode(cs, "RefreshGroundLive") or ""
if not re.search(r"UpdateGroundMemory\(\s*all\s*\)", auffrischen):
    fehler.append("RefreshGroundLive ruft UpdateGroundMemory(all) nicht auf")
if "SetData(_groundLiveData)" not in auffrischen.replace(" ", ""):
    fehler.append("RefreshGroundLive lädt _groundLiveData nicht hoch (SetData)")
if re.search(r"TallGrassAt\(|tile\.Wear", auffrischen):
    fehler.append("RefreshGroundLive rechnet die Kacheln noch selbst - das gehört in UpdateGroundMemory")
if not re.search(r"RefreshGroundLive\(\s*true\s*\)", cs):
    fehler.append("beim Anlegen des Lebendbilds fehlt der Komplettabgleich RefreshGroundLive(true)")

figuren = methode(cs, "DrawUnits") or ""
if not re.search(r"SeenTallGrassAt\(\s*x\s*,\s*y\s*\)", figuren):
    fehler.append("DrawUnits: die Büschel des hohen Grases lesen nicht SeenTallGrassAt(x, y)")
if re.search(r"(?<!Seen)TallGrassAt\(\s*x\s*,\s*y\s*\)", figuren):
    fehler.append("DrawUnits fragt für die Büschel noch TallGrassAt(x, y) live ab")

melde(fehler, "OK: Lebendbild und Büschel zeigen im Nebel den zuletzt gesehenen Boden")
