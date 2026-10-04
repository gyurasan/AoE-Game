"""Abnahme D23: das README zeigt die Zeitalter - neue Aufnahmen und ihre Bildtexte.

Momentaufnahme wie alle Doku-Prüfungen: die Spielszene zeigt seit C4g das Langhaus
der Dunklen Zeit mit Mühle, Haus und beiden Lagern, das neue Bild zeitalter.jpg
dieselbe Siedlung in allen vier Zeitaltern. Geprüft wird, dass Alternativtext und
Bildunterschrift das Gezeigte nennen und Spielstand und Grafik den Wechsel kennen.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

readme = Path("README.md").read_text(encoding="utf-8-sig")
fehler = []


def abschnitt(bild):
    """Alternativtext und die Bildunterschrift (kursive Zeile) direkt danach."""
    m = re.search(rf"!\[([^\]]*)\]\({re.escape(bild)}\)\s*\n\s*\n\*([^*]+)\*", readme)
    return (m.group(1) + " " + m.group(2)) if m else None


if not Path("docs/bilder/zeitalter.jpg").is_file():
    fehler.append("docs/bilder/zeitalter.jpg fehlt")
zeit = abschnitt("docs/bilder/zeitalter.jpg")
if zeit is None:
    fehler.append("zeitalter.jpg: Bild mit kursiver Bildunterschrift darunter fehlt")
else:
    for begriff in ("vier Zeitalter", "Dorfbewohner", "Langhaus", "Burg"):
        if begriff.lower() not in zeit.lower():
            fehler.append(f"zeitalter.jpg: Alternativtext oder Bildunterschrift nennt '{begriff}' nicht")
szene = abschnitt("docs/bilder/spielszene.jpg")
if szene is None:
    fehler.append("spielszene.jpg: Bild mit kursiver Bildunterschrift darunter fehlt")
else:
    for begriff in ("Dunklen Zeit", "Langhaus", "Mühle", "Lager"):
        if begriff.lower() not in szene.lower():
            fehler.append(f"spielszene.jpg: Alternativtext oder Bildunterschrift nennt '{begriff}' nicht")
if "wächst schon grün nach" in readme:
    fehler.append("die Spielszene beschreibt noch eine nachwachsende Kachel, die das neue Bild nicht zeigt")

stand = readme.split("## Spielstand", 1)[-1].split("\n## ", 1)[0]
if not re.search(r"Zeitalter[^\n]*Gebäude und Dorfbewohner", stand):
    fehler.append("Spielstand: dass Gebäude und Dorfbewohner je Zeitalter anders aussehen, fehlt")
grafik = readme.split("## Grafik", 1)[-1].split("\n## ", 1)[0]
if "je Zeitalter" not in grafik:
    fehler.append("Grafik: 'je Zeitalter' fehlt")

melde(fehler, "D23 erfuellt: README zeigt die vier Zeitalter, Bildtexte passen zu den neuen Aufnahmen")
