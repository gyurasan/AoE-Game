"""Abnahme D29: Doku kennt die Forschungen (P2).

Die Projektstruktur führt Research.cs mit eigener Tabellenzeile, die Ablaufgruppe Forschung
und die Tasten nach Researches; TODO.md nennt P2 in der Wiederaufnahme und hakt den Punkt
P2 Forschungen ab. Testzahl und Zeilenzahlen prüfen d5, d8 und d9 (gemessen).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde  # noqa: E402

fehler = []
struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
if not any(l.startswith("|") and "src/AoE.Core/Economy/Research.cs" in l for l in struktur.splitlines()):
    fehler.append("PROJEKT_STRUKTUR.md: Tabellenzeile für src/AoE.Core/Economy/Research.cs fehlt")
ablauf = next((l for l in struktur.splitlines() if l.startswith("tools/spielablauf/")), "")
if "Forschung" not in ablauf:
    fehler.append("PROJEKT_STRUKTUR.md: tools/spielablauf nennt die Gruppe Forschung nicht")
for name in ("Researches", "TechEffects", "Player.Techs"):
    if name not in struktur:
        fehler.append(f"PROJEKT_STRUKTUR.md: {name} fehlt")
if re.search(r"\bTechTree\b", struktur) and "ersetzt" not in struktur:
    fehler.append("PROJEKT_STRUKTUR.md: nennt den alten TechTree noch als vorhanden")

zeilen = lies()
wieder = "\n".join(abschnitt(zeilen, r"## Wiederaufnahme"))
if not re.search(r"\bP2\b", wieder):
    fehler.append("TODO, Wiederaufnahme: P2 fehlt")
for name in ("Researches", "TechRules", "ResearchTests"):
    if name not in wieder:
        fehler.append(f"TODO, Zuletzt fertiggestellt: {name} fehlt")
if not re.search(r"^\s*- \[x\][^\n]*P2\s+Forschungen", "\n".join(zeilen), re.MULTILINE):
    fehler.append("TODO: Punkt P2 Forschungen ist nicht abgehakt")

melde(fehler, "D29 erfuellt: Projektstruktur und TODO kennen die Forschungen (P2)")
