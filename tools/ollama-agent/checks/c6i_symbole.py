"""Abnahme C6i: die Befehlstasten zeigen im laufenden Spiel ihre Symbole.

Startet die DesktopGL-Fassung mit --rts (ohne Auswahl: Tasten Q, A und .),
fotografiert die Fensterfläche und zählt in der ersten Taste (Q, Dorfbewohner)
die verschiedenen Farben. Ein Symbol aus Content/Icons bringt Hunderte mit; eine
Taste mit Text hat nur ihre Grundfarbe und die Kantenglättung der Schrift.
Dazu muss die Taste quadratisch sein (60 px), wie es mit Symbol vorgesehen ist.

Vorher muss DesktopGL gebaut sein. Für einige Sekunden erscheint das Spielfenster.
"""

import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402
from _bild import lies_bmp  # noqa: E402

EXE = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0/AgeOfEvolutions.exe").resolve()
FENSTER = Path(__file__).resolve().parent / "_fenster.ps1"
LEISTE = 200            # HUD_BOTTOM_HEIGHT
GRUND = (94, 76, 48)    # Farbe der unteren Leiste

bild = Path(tempfile.gettempdir()) / "c6i_symbole.bmp"
lauf = subprocess.run(["pwsh", "-NoProfile", "-File", str(FENSTER), "-Exe", str(EXE), "-Out", str(bild)],
                      capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
if lauf.returncode != 0 or not bild.is_file():
    melde([f"Bildschirmfoto gescheitert: {(lauf.stdout + lauf.stderr).strip()[:400]}"], "")

W, H, pixel = lies_bmp(bild)
oben = H - LEISTE
fehler = []

# Erste Taste: ab x = 10, y = Leistenoberkante + 14 - ihre Breite bis zur Leistenfarbe
zeile = oben + 14 + 30
rechts = 10
while rechts < 400 and pixel(rechts, zeile) != GRUND:
    rechts += 1
breite = rechts - 10
if not 56 <= breite <= 64:
    fehler.append(f"erste Taste {breite} px breit - mit Symbol quadratisch 60 px erwartet")

farben = {pixel(x, y) for y in range(oben + 18, oben + 70) for x in range(14, 66)}
if len(farben) < 150:
    fehler.append(f"erste Taste zeigt nur {len(farben)} Farben - kein Symbol zu sehen")

melde(fehler, f"C6i erfuellt: erste Taste {breite} px breit, {len(farben)} Farben (Symbol sichtbar)")
