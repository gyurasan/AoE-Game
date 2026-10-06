"""Abnahme D25: Doku kennt die Wiese aus vier Grassorten (G10).

README nennt sie im Spielstand und im Abschnitt Grafik, die Projektstruktur nennt
im Abschnitt Grafik die vier Grasbilder und Meadow, TODO.md nennt G10 in der
Wiederaufnahme. Zeilenzahlen prüft d9 (gemessen).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde  # noqa: E402

fehler = []
readme = Path("README.md").read_text(encoding="utf-8-sig")
spielstand = readme.split("## Spielstand", 1)[-1].split("\n## ", 1)[0]
if not re.search(r"Grassorten", spielstand):
    fehler.append("README, Spielstand: die Wiese aus mehreren Grassorten fehlt")
if re.search(r"Gras\s+mit\s+natürlichen\s+Farbschwankungen", spielstand):
    fehler.append("README, Spielstand: beschreibt noch das alte, nur getönte Gras")
grafik = readme.split("## Grafik", 1)[-1].split("\n## ", 1)[0]
if not re.search(r"vier\s+Sorten|Grassorten", grafik):
    fehler.append("README, Grafik: die vier Grassorten fehlen")

struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
sgrafik = struktur.split("## Grafik", 1)[-1].split("\n## ", 1)[0]
for pflicht in ("gras_trocken", "gras_dunkel", "gras_blumen", "Meadow"):
    if pflicht not in sgrafik:
        fehler.append(f"PROJEKT_STRUKTUR.md, Grafik: {pflicht} fehlt")

wieder = "\n".join(abschnitt(lies(), r"## Wiederaufnahme"))
if not re.search(r"\bG10\b", wieder):
    fehler.append("TODO, Wiederaufnahme: G10 fehlt")

melde(fehler, "D25 erfuellt: README, Projektstruktur und TODO kennen die Wiese aus vier Grassorten")
