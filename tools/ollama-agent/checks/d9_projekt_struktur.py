"""Abnahme D9: docs/PROJEKT_STRUKTUR.md gibt den Stand nach C1 wieder.

Die Zeilenzahlen werden nicht abgeschrieben, sondern an den Dateien gemessen:
die Pruefung zaehlt selbst und verlangt genau diese Zahl in der Tabellenzeile.
"""

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
fehler = []


def zeilen(pfad: str) -> int:
    return len(Path(pfad).read_text(encoding="utf-8-sig").splitlines())


if re.search(r"Zeile\s+520", text):
    fehler.append("veraltet noch drin: Zeile 520 (Zeilenverweise veralten - Klasse beim Namen nennen)")

# Testzahlen werden gezaehlt, nicht festgeschrieben: gesamt und je Datei
sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import testzahlen  # noqa: E402

zahlen = testzahlen()   # zaehlt wie 'dotnet test': [Fact] und jede [InlineData]
gesamt = sum(zahlen.values())
for pflicht in (f"{gesamt}/{gesamt}", f"{gesamt} Tests"):
    if pflicht not in text:
        fehler.append(f"fehlt: {pflicht} (gezaehlt)")
for name, anzahl in zahlen.items():
    if anzahl and f"{name} ({anzahl})" not in text:
        fehler.append(f"Testliste: '{name} ({anzahl})' fehlt (gezaehlt)")

# Jede genannte Datei mit ihrer gemessenen Zeilenzahl in ihrer Tabellenzeile
TABELLE = [
    "src/AoE.Core/Economy/GatherJob.cs",
    "src/AoE.Core/Entities/UnitTypes.cs",
    "src/AoE.Core/Map/VisibilitySystem.cs",
    "AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs",
    "AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMapGatherWorld.cs",
    "AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs",
    "AgeOfEvolutions/AgeOfEvolutions.Core/Data/Unit.cs",
    "AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs",
]
for pfad in TABELLE:
    zeile = next((l for l in text.splitlines() if l.startswith("|") and f"| {pfad} |" in l), None)
    if zeile is None:
        fehler.append(f"Tabellenzeile fuer {pfad} fehlt")
        continue
    soll = zeilen(pfad)
    if not re.search(rf"\b{soll} Zeilen\b", zeile):
        fehler.append(f"{pfad}: Tabelle nennt nicht die gemessenen {soll} Zeilen")

anbindung = text.split("## Anbindung an AoE.Core", 1)[-1]
if "GatherJob" not in anbindung.split("Angebunden, aber nie aufgerufen", 1)[0]:
    fehler.append("Anbindung: GatherJob fehlt bei dem, was zur Laufzeit aktiv ist")

# Jeder Pfad in einer Tabelle muss existieren
for zeile in text.splitlines():
    if zeile.startswith("|"):
        for zelle in zeile.strip().strip("|").split("|"):
            pfad = zelle.strip().strip("`")
            if re.fullmatch(r"[\w.-]+(/[\w.-]+)+\.\w+", pfad) and not Path(pfad).exists():
                fehler.append(f"Pfad in Tabelle existiert nicht: {pfad}")

if fehler:
    print("NICHT ERFUELLT:")
    for eintrag in fehler:
        print("  -", eintrag)
    sys.exit(1)
print("D9 erfuellt: PROJEKT_STRUKTUR.md auf dem Stand nach C1")
