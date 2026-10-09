"""Abnahme D28: Doku kennt Gebäude auswählen und angreifen (B1, K1, K2) und die Soldaten (P1).

Die Projektstruktur nennt BuildingCombat und die Ablaufgruppen Gebäudeauswahl, Angriff und
Soldaten; TODO.md nennt die vier Punkte in der Wiederaufnahme und führt die Forschungen
(P2) als offenen Punkt. Testzahl und Zeilenzahlen prüfen d5, d8 und d9 (gemessen).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde  # noqa: E402

fehler = []
struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
if not any(l.startswith("|") and "src/AoE.Core/Combat/BuildingCombat.cs" in l for l in struktur.splitlines()):
    fehler.append("PROJEKT_STRUKTUR.md: Tabellenzeile für src/AoE.Core/Combat/BuildingCombat.cs fehlt")
ablauf = next((l for l in struktur.splitlines() if l.startswith("tools/spielablauf/")), "")
for gruppe in ("Gebäudeauswahl", "Angriff", "Soldaten"):
    if gruppe not in ablauf:
        fehler.append(f"PROJEKT_STRUKTUR.md: tools/spielablauf nennt die Gruppe {gruppe} nicht")
if "Products" not in struktur:
    fehler.append("PROJEKT_STRUKTUR.md: die Ausbildung nach Products fehlt")

zeilen = lies()
wieder = "\n".join(abschnitt(zeilen, r"## Wiederaufnahme"))
for punkt in ("B1", "K1", "K2", "P1"):
    if not re.search(rf"\b{punkt}\b", wieder):
        fehler.append(f"TODO, Wiederaufnahme: {punkt} fehlt")
for name in ("Products", "BuildingCombat", "RemoveBuilding"):
    if name not in wieder:
        fehler.append(f"TODO, Zuletzt fertiggestellt: {name} fehlt")
if not re.search(r"^\s*- \[[ x]\][^\n]*P2\s+Forschungen", "\n".join(zeilen), re.MULTILINE):
    fehler.append("TODO: Punkt P2 Forschungen fehlt")

melde(fehler, "D28 erfuellt: Projektstruktur und TODO kennen Gebäudeauswahl, Angriff, Soldaten und P2")
