"""Abnahme C6g: die Gebäude erscheinen im laufenden Spiel als Sprites.

Startet die DesktopGL-Fassung mit --rts, fotografiert die Fensterfläche und
zählt im Kartenbereich die Pixel im Strohgold des Stadtzentrum-Sprites. Seit C4g
steht zu Beginn das Langhaus der Dunklen Zeit (Gebaeude/dunkel/stadtzentrum) mit
Strohdach; das frühere Ziegelrot gibt es erst ab der Ritterzeit. Die selbst
gezeichnete Fassung hat kein Strohgold, das Sprite am 2026-10-04 bei 3008 x 1692
gut 3500 Pixel, davon 3300 am Stadtzentrum; Wiese und Goldhaufen tragen wenige
hundert bei (Schwelle 1000, damit kleinere Fenster nicht durchfallen).

Vorher muss DesktopGL gebaut sein. Für einige Sekunden erscheint das Spielfenster.
"""

import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_bmp  # noqa: E402
from _cs import melde  # noqa: E402

EXE = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0/AgeOfEvolutions.exe").resolve()
FENSTER = Path(__file__).resolve().parent / "_fenster.ps1"
OBEN, UNTEN = 34, 200   # HUD_TOP_HEIGHT, HUD_BOTTOM_HEIGHT

bild = Path(tempfile.gettempdir()) / "c6g_gebaeude.bmp"
lauf = subprocess.run(["pwsh", "-NoProfile", "-File", str(FENSTER), "-Exe", str(EXE), "-Out", str(bild)],
                      capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
if lauf.returncode != 0 or not bild.is_file():
    melde([f"Bildschirmfoto gescheitert: {(lauf.stdout + lauf.stderr).strip()[:400]}"], "")

W, H, pixel = lies_bmp(bild)
stroh = 0
for y in range(OBEN, H - UNTEN):
    for x in range(W):
        r, g, b = pixel(x, y)
        if r > 150 and g > 100 and b < 90 and 30 <= r - g <= 100 and g - b >= 50:
            stroh += 1

fehler = []
if stroh < 1000:
    fehler.append(f"nur {stroh} strohgoldene Pixel auf der Karte - das Stadtzentrum-Sprite der Dunklen Zeit ist nicht zu sehen")
melde(fehler, f"C6g erfuellt: {stroh} strohgoldene Pixel (Stadtzentrum der Dunklen Zeit als Sprite)")
