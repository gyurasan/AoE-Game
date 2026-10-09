"""Abnahme C8b: ein Linksklick auf eine eigene Einheit waehlt sie aus, statt sie wegzuschicken.

Das Verhalten prueft tools/spielablauf (Gruppe linksklick, eigener verify-Schritt).
Hier geht es um die Struktur: die Entscheidung steht in LeftClick, die Reihenfolge
stimmt - erst Gebaeude setzen, dann eigene Einheit waehlen, sonst Befehl.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

klick = methode(text, "LeftClick")
if klick is None:
    fehler.append("Methode LeftClick fehlt")
else:
    stellen = {name: klick.find(name) for name in ("PlaceBuilding(", "OwnUnitAt(", "SelectSingleUnit(", "IssueCommand(")}
    for name, pos in stellen.items():
        if pos < 0:
            fehler.append(f"LeftClick ruft {name[:-1]} nicht auf")
    if all(p >= 0 for p in stellen.values()) and not (
            stellen["PlaceBuilding("] < stellen["OwnUnitAt("] < stellen["IssueCommand("]):
        fehler.append("LeftClick: Reihenfolge muss sein Gebaeude setzen, dann eigene Einheit, sonst Befehl")

einheit = methode(text, "OwnUnitAt")
if einheit is None:
    fehler.append("Methode OwnUnitAt fehlt")
# Eigenschaft, nicht Schreibweise: OwnerId mit 0 verglichen (== 0 als Bedingung oder
# != 0 als Ausschluss, seit T1a) und die Figurfläche UnitWorldRect
elif not re.search(r"OwnerId\s*[!=]=\s*0", einheit) or "UnitWorldRect" not in einheit:
    fehler.append("OwnUnitAt sucht nicht die eigene Einheit unter dem Zeiger (OwnerId == 0, UnitWorldRect)")
if not re.search(r"OwnUnitAt\s*\(", methode(text, "SelectSingleUnit") or ""):
    fehler.append("SelectSingleUnit nutzt OwnUnitAt nicht - zwei Fassungen derselben Suche")

if not re.search(r"LeftClick\s*\(", methode(text, "HandleRtsInput") or ""):
    fehler.append("HandleRtsInput ruft LeftClick nicht auf")

melde(fehler, "C8b erfuellt: Linksklick auf eigene Einheit waehlt aus, sonst Befehl")
