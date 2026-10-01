"""Abnahme D7: docs/PROJEKT_STRUKTUR.md gibt den Stand vom 2026-09-30 wieder."""

import re
import sys
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
fehler = []

VERALTET = {
    r"kein einziges": "das Spiel nutzt AoE.Core seit Block B",
    r"im spiel nicht verwendet": "das Spiel nutzt AoE.Core seit Block B",
    r"15/15": "es sind 26 Tests",
    r"15 tests,\s*alle": "es sind 26 Tests",
    r"\b(227|376|461|1233)\b": "alte Zeilenzahl",
    r"zeile 241": "MapGrid beginnt in Zeile 268",
    r"mermaid": "kein Mermaid",
}
for muster, grund in VERALTET.items():
    # Leerzeichen im Muster stehen fuer beliebigen Leerraum, auch Zeilenumbrueche.
    if re.search(muster.replace(" ", r"\s+"), text, re.IGNORECASE):
        fehler.append(f"veraltet noch drin: /{muster}/ ({grund})")

# Die Zeilenzahlen standen hier einmal fest (238, 403, 554, 1326 ...). Seit D9
# misst d9_projekt_struktur.py sie an den Dateien selbst - feste Zahlen haette
# jede spaetere Aenderung gebrochen. Die Testzahl pruefen d8/d9.
# Das Datum im Kopf war hier fest "2026-09-30" - D14 setzte es zu Recht auf den
# 2026-10-01 und brach damit diese Pruefung. Verlangt wird jetzt mindestens der
# Stand von D7, wie in d5_todo_fakten.py.
kopfdaten = re.findall(r"\b(\d{4}-\d{2}-\d{2})\b", "\n".join(text.splitlines()[:5]))
if not any(datum >= "2026-09-30" for datum in kopfdaten):
    fehler.append("Stand im Kopf nicht mindestens 2026-09-30")
PFLICHT = [
    "Data/Unit.cs", "Data/CoreUnits.cs",
    "Attack", "DamageCalculator", "UpdateFogOfWarForPlayer", "BuildingEntity",
    "## Ordnerstruktur", "## Wichtige Dateien", "## Build-Status",
    "TODO.md", "AgeOfEmpires.md",
]
for pflicht in PFLICHT:
    if pflicht not in text:
        fehler.append(f"fehlt: {pflicht}")
if not re.search(r"nicht unterst(ü|ue)tzt", text):
    fehler.append("Hinweis, dass Android/iOS nicht unterstuetzt werden, fehlt")
if len(text) < 1500:
    fehler.append(f"nur {len(text)} Zeichen - Inhalt verloren?")

# Jeder Pfad in einer Tabelle muss existieren. Erfundene Pfade waren der
# Grund, warum die Datei in D1 neu geschrieben werden musste.
for zeile in text.splitlines():
    if not zeile.startswith("|"):
        continue
    for zelle in zeile.strip().strip("|").split("|"):
        pfad = zelle.strip().strip("`")
        if re.fullmatch(r"[\w.-]+(/[\w.-]+)+\.\w+", pfad) and not Path(pfad).exists():
            fehler.append(f"Pfad in Tabelle existiert nicht: {pfad}")

if fehler:
    print("NICHT ERFUELLT:")
    for eintrag in fehler:
        print("  -", eintrag)
    sys.exit(1)
print("D7 erfuellt: PROJEKT_STRUKTUR.md auf Stand 2026-09-30")
