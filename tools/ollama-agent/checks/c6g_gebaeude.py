"""Abnahme C6g: die Gebäude erscheinen im laufenden Spiel als Sprites.

Startet die DesktopGL-Fassung mit --rts, fotografiert die Fensterfläche und
zählt im Kartenbereich die Pixel im Ziegelrot des Stadtzentrum-Sprites. Die
selbst gezeichnete Fassung hat kein solches Rot - am 2026-10-03 null Pixel -,
das Sprite mit seinem Ziegeldach je nach Zoom und Fenster gut zweitausend
(Schwelle 500, damit kleinere Fenster nicht durchfallen).

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
ziegel = 0
for y in range(OBEN, H - UNTEN):
    for x in range(W):
        r, g, b = pixel(x, y)
        if r > 140 and r - g > 60 and g < 120 and b < 90:
            ziegel += 1

fehler = []
if ziegel < 500:
    fehler.append(f"nur {ziegel} ziegelrote Pixel auf der Karte - das Stadtzentrum-Sprite ist nicht zu sehen")
melde(fehler, f"C6g erfuellt: {ziegel} ziegelrote Pixel (Stadtzentrum als Sprite)")
