"""Abnahme C7d: mehr Wild - die Prüfung selbst ist da, und die Karte setzt Startherden.

Wie viele Rehe es gibt und wo, prüft tools/kartenpruefung, Gruppe wild, über 50
Karten. Diese Gruppe entsteht in derselben Aufgabe - fehlte sie, liefe die
Abnahme ins Leere. Deshalb hier: es gibt sie, und PlaceDeer stellt Startherden
(NearStart) und sucht für Herden ohne Platz eine neue Stelle (PlaceDeerHerd).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

pruef = lies(Path("tools/kartenpruefung/Program.cs"))
karte = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs"))
settings = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/MapSettings.cs"))
fehler = []

if len(re.findall(r'gruppen\.Contains\("wild"\)', pruef)) < 3:
    fehler.append("tools/kartenpruefung hat keine Gruppe 'wild' (Rehe auf jeder Karte, Startherde, Verhältnis zu Schafen)")
rehe = methode(karte, "PlaceDeer") or ""
for aufruf in ("NearStart", "PlaceDeerHerd"):
    if not re.search(rf"\b{aufruf}\s*\(", rehe):
        fehler.append(f"PlaceDeer ruft {aufruf} nicht auf")
for name in ("DeerStartDistanceMin", "DeerStartDistanceMax"):
    if not re.search(rf"\bint\s+{name}\s*\{{", settings):
        fehler.append(f"MapSettings.{name} fehlt")

melde(fehler, "C7d erfuellt: kartenpruefung prüft das Wild, PlaceDeer setzt Startherden")
