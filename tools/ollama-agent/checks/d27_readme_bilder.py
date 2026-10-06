"""Abnahme D27: das README zeigt die Aufnahmen vom 2026-10-06 - Wiese aus vier Grassorten,
ein ausgewähltes Stadtzentrum mit Status und das neue Bild aller Gebäude.

Jeder Bildtext nennt das Gezeigte (Alternativtext oder kursive Bildunterschrift direkt
darunter), nicht einen bestimmten Wortlaut. Momentaufnahme wie alle Doku-Prüfungen.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

readme = Path("README.md").read_text(encoding="utf-8-sig")
fehler = []


def abschnitt(bild):
    m = re.search(rf"!\[([^\]]*)\]\({re.escape(bild)}\)\s*\n\s*\n\*([^*]+)\*", readme)
    return (m.group(1) + " " + m.group(2)).lower() if m else None


for bild, begriffe in {
    "docs/bilder/spielszene.jpg": ("ausgewählt", "status", "bildet", "grassorten"),
    "docs/bilder/karte.jpg": ("grassorten",),
    "docs/bilder/gebaeude.jpg": ("imperialzeit", "mauer", "tastenreihe", "wunder"),
}.items():
    if not Path(bild).is_file():
        fehler.append(f"{bild} fehlt")
    text = abschnitt(bild)
    if text is None:
        fehler.append(f"{bild}: Bild mit kursiver Bildunterschrift darunter fehlt")
        continue
    for begriff in begriffe:
        if begriff not in text:
            fehler.append(f"{bild}: Alternativtext oder Bildunterschrift nennt '{begriff}' nicht")

reihenfolge = re.findall(r"!\[[^\]]*\]\((docs/bilder/[a-z]+\.jpg)\)", readme)
if reihenfolge[:5] != ["docs/bilder/hauptmenue.jpg", "docs/bilder/spielszene.jpg", "docs/bilder/karte.jpg",
                       "docs/bilder/zeitalter.jpg", "docs/bilder/gebaeude.jpg"]:
    fehler.append(f"Bilder in unerwarteter Reihenfolge: {reihenfolge}")

melde(fehler, "D27 erfuellt: README zeigt die neuen Aufnahmen mit passenden Bildtexten")
