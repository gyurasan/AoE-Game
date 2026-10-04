"""Abnahme D22: die Bildtexte im README beschreiben die neuen Aufnahmen (Feld, Ernte, Tiere).

Momentaufnahme wie alle Doku-Prüfungen: sie sichert die Abnahme von D22, eine
spätere Aufnahme darf sie brechen. Geprüft wird je Bild, dass Alternativtext
und Bildunterschrift das Gezeigte nennen, nicht der Wortlaut.
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


szene = abschnitt("docs/bilder/spielszene.jpg")
if szene is None:
    fehler.append("spielszene.jpg: Bild mit kursiver Bildunterschrift darunter fehlt")
else:
    for begriff in ("Feld", "ernte", "Schafe", "Reh", "Nebel"):
        if begriff.lower() not in szene.lower():
            fehler.append(f"spielszene.jpg: Alternativtext oder Bildunterschrift nennt '{begriff}' nicht")
karte = abschnitt("docs/bilder/karte.jpg")
if karte is None:
    fehler.append("karte.jpg: Bild mit kursiver Bildunterschrift darunter fehlt")
else:
    for begriff in ("zwei Seen", "Schafe", "ohne Nebel"):
        if begriff.lower() not in karte.lower():
            fehler.append(f"karte.jpg: Alternativtext oder Bildunterschrift nennt '{begriff}' nicht")
if "Spielbeginn: Stadtzentrum und Dorfbewohner, rundherum noch Nebel" in readme:
    fehler.append("der alte Bildtext der Spielszene steht noch da")

melde(fehler, "D22 erfuellt: die README-Bildtexte beschreiben Feld, Ernte und Tiere")
