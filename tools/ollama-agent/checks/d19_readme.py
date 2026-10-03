"""Abnahme D19: README mit Bildern, Doku kennt die Grafik aus tools/bilder.

Jeder Bildverweis im README muss auf eine vorhandene Datei zeigen - ein toter
Verweis ist der typische Fehler, wenn Bilder umbenannt oder vergessen werden.
Dazu: das README nennt Steuerung und Spielstand, PROJEKT_STRUKTUR.md hat den
Abschnitt Grafik, TODO.md beschreibt tools/bilder und die nächsten Schritte.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

readme = Path("README.md").read_text(encoding="utf-8-sig")
struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
todo = Path("TODO.md").read_text(encoding="utf-8-sig")
fehler = []

bilder = re.findall(r"!\[[^\]]*\]\(([^)\s]+)\)", readme)
if len(bilder) < 2:
    fehler.append(f"README zeigt nur {len(bilder)} Bild(er)")
for pfad in bilder:
    if not Path(pfad).is_file():
        fehler.append(f"README verweist auf fehlendes Bild {pfad}")

for pflicht in ("## Steuerung", "## Spielstand", "tools/bilder"):
    if pflicht not in readme:
        fehler.append(f"README: '{pflicht}' fehlt")
if not re.search(r"^## Grafik\s*$", struktur, re.MULTILINE):
    fehler.append("PROJEKT_STRUKTUR.md: Abschnitt '## Grafik' fehlt")
wieder = todo.split("## Verifizierter Ist-Zustand", 1)[0]
for pflicht in ("tools/bilder", "qwen_image.py", "uebernehmen.ps1", "Qwen-Image"):
    if pflicht not in wieder:
        fehler.append(f"TODO.md Wiederaufnahme: '{pflicht}' fehlt")

melde(fehler, f"D19 erfuellt: README mit {len(bilder)} Bildern, Doku kennt tools/bilder")
