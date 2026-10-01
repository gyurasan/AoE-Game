"""Abnahme E10: Rohstoffklumpen sind abbaubar, und es gibt keine grauen Deko-Felsen mehr.

StampResource setzte Kacheltyp und Menge, aber nie ResourceType. Ein
Pruefprogramm ueber 20 erzeugte Karten fand am 2026-09-30: Stein 0 von 557
Kacheln abbaubar, Gold 0 von 235, Wald aus Klumpen zu einem Drittel nicht.
Das Verhalten prueft das Kartenpruefprogramm; hier geht es um die Stellen im Code.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde, methode  # noqa: E402

text = Path("AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
fehler = []

stempel = methode(text, "StampResource") or ""
if not re.search(r"\bt\.ResourceType\s*=\s*resource\s*;", stempel):
    fehler.append("StampResource setzt ResourceType nicht")
if not re.search(r"\bt\.Food\s*=\s*FoodSource\.None", stempel):
    fehler.append("StampResource raeumt ein vorheriges Nahrungsmerkmal (Food) nicht ab")

zufall = methode(text, "GetRandomTileType") or ""
if "TileType.Rock" in zufall:
    fehler.append("GetRandomTileType erzeugt noch graue Deko-Felsen ohne Ressource")
if "TileType.Forest" not in zufall:
    fehler.append("GetRandomTileType erzeugt keine Einzelbaeume mehr")

melde(fehler, "E10 erfuellt: Klumpen abbaubar, keine Deko-Felsen")
