"""Abnahme K: ein Klick ins Schwarze wirkt immer - die Wegsuche verrät nichts.

Bis 2026-10-05 plante ein Laufbefehl mit FindPath, also mit dem ganzen Wissen über die
Karte: lag unter einem Klick ins Schwarze Wasser oder ein fremdes Gebäude, kam ein
leerer Weg zurück, und die Einheit blieb stehen - der Nutzer sah daran, dass dort etwas
ist. Jetzt plant ein Laufbefehl mit TileMap.FindPathKnown nur, was der Spieler weiß;
stößt die Einheit unterwegs auf ein Hindernis, plant StepAlongPath neu, statt es zu
betreten.

Das Verhalten prüft tools/spielablauf, Gruppe dunkel. Hier: dass Laufbefehl und Schritt
die neue Wegsuche benutzen.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
karte = lies(KERN / "Data/TileMap.cs")
rts = lies(KERN / "Screens/RTSGameplayScreen.cs")
fehler = []

for name in ("IsKnownWalkable", "FindPathKnown"):
    rumpf = methode(karte, name)
    if rumpf is None:
        fehler.append(f"TileMap.{name} fehlt")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"TileMap.{name} ist noch der leere Rumpf")
if not re.search(r"IsKnownWalkable\(", methode(karte, "IsSegmentWalkable") or ""):
    fehler.append("IsSegmentWalkable berücksichtigt playerId nicht (IsKnownWalkable)")

# Der Laufbefehl steht in IssueCommand - LeftClick reicht nur dorthin weiter (die
# erste Fassung dieser Prüfung suchte in LeftClick und verwarf so korrekte Arbeit)
befehl = methode(rts, "IssueCommand") or ""
if not re.search(r"tileMap\.FindPathKnown\(\s*unit\.OwnerId", befehl):
    fehler.append("IssueCommand plant Laufbefehle nicht mit FindPathKnown")
schritt = methode(rts, "StepAlongPath") or ""
if not re.search(r"tileMap\.FindPathKnown\(", schritt):
    fehler.append("StepAlongPath plant nicht neu, wenn die Einheit auf ein Hindernis stößt (FindPathKnown)")
if not re.search(r"tileMap\.IsWalkable\(", schritt):
    fehler.append("StepAlongPath prüft nicht, ob der nächste Schritt begehbar ist")

melde(fehler, "K erfuellt: Laufbefehle planen mit dem Wissen des Spielers")
