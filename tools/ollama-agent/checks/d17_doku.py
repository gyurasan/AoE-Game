"""Abnahme D17: Doku kennt C5d, C8b, tools/spielablauf und den Screenshot 225659.

Momentaufnahme: eine spaetere Doku-Aufgabe darf sie zu Recht brechen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde, struktur  # noqa: E402

lines = lies()
fehler = []


def block(kopf: str) -> str:
    return "\n".join(abschnitt(lines, kopf))


wieder = block(r"## Wiederaufnahme")
for pflicht in ("C5d", "C8b", "225659", "tools/spielablauf"):
    if pflicht not in wieder:
        fehler.append(f"Wiederaufnahme: {pflicht} fehlt")

if not re.search(r"^\s*- \[x\][^\n]*(Liegengebliebene|liegengebliebene)[\s\S]{0,300}?C5d", block(r"### C5 "), re.MULTILINE):
    fehler.append("C5: erledigter Punkt zu liegengebliebenen Baustellen (C5d) fehlt")
if not re.search(r"^\s*- \[x\][^\n]*Linksklick[\s\S]{0,300}?C8b", block(r"### C8 "), re.MULTILINE):
    fehler.append("C8: erledigter Punkt zum Linksklick auf eigene Einheiten (C8b) fehlt")
if not re.search(r"C5d[\s\S]*C8b|C8b[\s\S]*C5d", block(r"# Erledigt")):
    fehler.append("Erledigt-Liste nennt C5d und C8b nicht")

struktur_text = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
if "tools/spielablauf" not in struktur_text:
    fehler.append("PROJEKT_STRUKTUR.md nennt tools/spielablauf nicht")
if not Path("tools/spielablauf/Program.cs").exists():
    fehler.append("tools/spielablauf/Program.cs fehlt - die Doku verweist darauf")

fehler += struktur(lines, min_erledigt=104, min_offen=18, min_zeilen=640)
melde(fehler, "D17 erfuellt: Doku auf dem Stand nach C5d, C8b und tools/spielablauf")
