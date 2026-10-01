"""Abnahme D5: TODO.md gibt den geprueften Stand vom 2026-09-30 wieder.

Geprueft werden Eigenschaften, keine Formulierungen. Die Sperrmuster sind
bewusst die *alten Saetze* und nicht einzelne Woerter - sonst trifft die
Sperre auch eine korrekte Neufassung wie "die Sichtlogik laeuft gar nicht".
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur, testzahlen  # noqa: E402

lines = lies()
text = "\n".join(lines)
fehler = []

VERALTET = {
    r"nichts ist committet": "Commit 4cb57d4 existiert",
    r"im arbeitsbaum entfernt, aber noch nicht aus dem git-index": "Loeschung ist committet",
    r"es gibt \**keine\s*`?\.gitignore": ".gitignore existiert",
    r"15/15": "es sind 26 Tests",
    r"15 unit-tests": "es sind 26 Tests",
    r"tilemap\.cs:427": "Building steht in Zeile 520",
    r"noch ungenutzt\s*(→|->)\s*block b": "AoE.Core wird seit Block B genutzt",
    r"sichtlogik l(ä|ae)uft seit": "UpdateFogOfWarForPlayer wird nirgends aufgerufen",
    r"die logik l(ä|ae)uft, die darstellung fehlt": "UpdateFogOfWarForPlayer wird nirgends aufgerufen",
    r"mehrere tausend": "es sind 501 Dateien",
}
for muster, grund in VERALTET.items():
    # Leerzeichen im Muster stehen fuer beliebigen Leerraum: die alten Saetze
    # sind in TODO.md teils ueber zwei eingerueckte Zeilen umbrochen.
    if re.search(muster.replace(" ", r"\s+"), text, re.IGNORECASE):
        fehler.append(f"veraltet noch drin: /{muster}/ ({grund})")

# Mindestens der Stand von D5 - spaetere Doku-Aufgaben setzen das Datum weiter
# (D14 auf 2026-10-01); ein fester Vergleich auf den 30. hat das gebrochen.
kopfdaten = re.findall(r"\b(\d{4}-\d{2}-\d{2})\b", "\n".join(lines[:5]))
if not any(datum >= "2026-09-30" for datum in kopfdaten):
    fehler.append("Stand im Kopf nicht mindestens 2026-09-30")
# Frueher verlangt, inzwischen zu Recht verschwunden: "TileMap.cs:520" (D8b hat
# die Zeilenangabe gestrichen) und der Befund "UpdateFogOfWarForPlayer wird
# nirgends aufgerufen" (seit C7n behoben). Dass die falsche Behauptung
# "Sichtlogik laeuft seit B5" nicht zurueckkommt, pruefen die Sperrmuster oben.
for pflicht, grund in (
    ("4cb57d4", "Commit nicht erwaehnt"),
):
    if pflicht not in text:
        fehler.append(f"{pflicht}: {grund}")
# Die Testzahl wird gezaehlt, nicht festgeschrieben - bei D5 waren es 26,
# seit C1 sind es 41, und jeder neue Test haette eine feste Zahl gebrochen.
anzahl = sum(testzahlen().values())   # zaehlt wie 'dotnet test', siehe _todo.py
if not re.search(rf"\b{anzahl}\s+Unit-Tests", text):
    fehler.append(f"Erledigt-Liste nennt nicht die gezaehlten {anzahl} Unit-Tests")

# Tabelle 'Verifizierter Ist-Zustand'
test_zeile = next((l for l in lines if l.startswith("| `tests/AoE.Tests`")), "")
if f"{anzahl}/{anzahl}" not in test_zeile:
    fehler.append(f"Tabellenzeile tests/AoE.Tests nennt nicht die gezaehlten {anzahl}/{anzahl}")
dx_zeile = next((l for l in lines if l.startswith("| `AgeOfEmpiresClone.WindowsDX`")), "")
zellen = [z.strip() for z in dx_zeile.strip().strip("|").split("|")]
if len(zellen) < 3 or not re.search(r"ok|0 fehler", zellen[2], re.IGNORECASE):
    fehler.append(f"WindowsDX: Build-Spalte nicht auf OK ({dx_zeile!r})")

# H1: der per Trockenlauf gepruefte Befehl, der alle 501 Dateien erfasst
h1 = abschnitt(lines, r"### H1 ")
if not any("git rm" in l and "--cached" in l and "**/bin/**" in l
           and "**/obj/**" in l and "**/.vs/**" in l for l in h1):
    fehler.append("H1: vollstaendiger git-rm-Befehl mit **/bin/**, **/obj/**, **/.vs/** fehlt")
if "501" not in "\n".join(h1):
    fehler.append("H1: Anzahl 501 fehlt")

# H4: offene Checkbox fuer die alte .sln
if not any(re.match(r"\s*- \[ \].*AgeOfEmpiresClone\.sln", l) for l in abschnitt(lines, r"### H4 ")):
    fehler.append("H4: offene Checkbox zur alten AgeOfEmpiresClone.sln fehlt")

fehler += struktur(lines, min_erledigt=52, min_offen=36, min_zeilen=425)
melde(fehler, "D5 erfuellt: TODO.md auf Stand 2026-09-30")
