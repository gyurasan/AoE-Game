"""Abnahme C7n: der Nebel des Krieges wird gerechnet und gezeichnet.

Die Sichtrechnung selbst pruefen die xUnit-Tests (FogOfWarTests). Hier geht es
darum, dass das Spiel sie aufruft und das Ergebnis zeigt - vorher existierte
alles, und nichts davon lief.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

if not re.search(r"const\s+float\s+FOG_INTERVAL", text):
    fehler.append("Konstante FOG_INTERVAL fehlt")

update = methode(text, "Update") or ""
if not re.search(r"UpdateFogOfWarForPlayer\s*\(\s*0\s*,\s*units\s*\)", update) or "FOG_INTERVAL" not in update:
    fehler.append("Update rechnet die Sicht nicht im Takt FOG_INTERVAL neu")
laden = methode(text, "LoadContent") or ""
if not re.search(r"UpdateFogOfWarForPlayer\s*\(\s*0\s*,\s*units\s*\)", laden):
    fehler.append("LoadContent rechnet die Sicht nicht vorab - der erste Frame waere schwarz")

nebel = methode(text, "DrawFog")
if nebel is None:
    fehler.append("Methode DrawFog fehlt")
else:
    for pflicht in ("IsTileExplored", "IsTileVisible", "TileScreenRect", "Color.Black"):
        if pflicht not in nebel:
            fehler.append(f"DrawFog: {pflicht} fehlt")

zeichnen = methode(text, "Draw") or ""
reihe = [zeichnen.find(n) for n in ("DrawUnits(", "DrawFog(", "DrawUI(")]
if -1 in reihe or not reihe[0] < reihe[1] < reihe[2]:
    fehler.append("Draw: Reihenfolge DrawUnits -> DrawFog -> DrawUI stimmt nicht")

einheiten = methode(text, "DrawUnits") or ""
if "IsTileVisible" not in einheiten or not re.search(r"OwnerId\s*!=\s*0", einheiten):
    fehler.append("DrawUnits zeigt fremde Einheiten auch im Nebel")

befehl = methode(text, "IssueCommand") or ""
if "IsTileExplored" not in befehl:
    fehler.append("IssueCommand laesst unerforschte Ressourcen gezielt abernten")

melde(fehler, "C7n erfuellt: Nebel wird gerechnet und gezeichnet")
