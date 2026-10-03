"""Abnahme C6m: die Minimap ist im laufenden Spiel rechts unten zu sehen.

Bis 2026-10-03 sass sie links und war von weissen Rechtecken verdeckt: die
schraegen Kanten des Kameraausschnitts wurden als gefuellte Rechtecke
zwischen ihren Endpunkten gezeichnet. Lage, Groesse und Klick rechnet
`tools/spielablauf -- minimap` nach. Was tatsaechlich auf dem Bildschirm
landet, sieht nur ein Bildschirmfoto - deshalb startet diese Abnahme die
DesktopGL-Fassung mit --rts und zaehlt Pixel in der unteren Leiste:

- der dunkle Minimap-Grund (12, 12, 12) liegt rechts, ist bei mindestens 2300 px
  Fensterbreite mindestens 370 px breit und doppelt so breit wie hoch,
- darin ist erkundetes Gelaende zu sehen (Grasland in der Minimap-Farbe),
- und hoechstens ein kleiner Teil ist weiss (Kamerarahmen und Einheiten).

Vorher muss DesktopGL gebaut sein (verify-Schritt davor). Fuer einige Sekunden
erscheint das Spielfenster.
"""

import struct
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

EXE = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0/AgeOfEvolutions.exe").resolve()
FENSTER = Path(__file__).resolve().parent / "_fenster.ps1"
LEISTE = 200                       # HUD_BOTTOM_HEIGHT
GRUND = (12, 12, 12)               # Minimap-Grund
GRAS = (104, 148, 68)              # IsoCol Grasland
GRAS_DUNKEL = tuple(c * 96 // 255 for c in GRAS)   # erkundet, nicht in Sicht
WEISS = (255, 255, 255)


def lies_bmp(pfad: Path):
    """Breite, Hoehe und eine Funktion pixel(x, y) -> (r, g, b) fuer 24/32-Bit-BMP."""
    daten = pfad.read_bytes()
    start = struct.unpack_from("<I", daten, 10)[0]
    breite, hoehe = struct.unpack_from("<ii", daten, 18)
    bpp = struct.unpack_from("<H", daten, 28)[0]
    schritt = bpp // 8
    zeile = (breite * bpp + 31) // 32 * 4
    von_unten = hoehe > 0
    hoehe = abs(hoehe)

    def pixel(x, y):
        z = (hoehe - 1 - y) if von_unten else y
        i = start + z * zeile + x * schritt
        return daten[i + 2], daten[i + 1], daten[i]

    return breite, hoehe, pixel


fehler = []
if not EXE.is_file():
    melde([f"{EXE} fehlt - DesktopGL nicht gebaut"], "")

bild = Path(tempfile.gettempdir()) / "c6m_minimap.bmp"
lauf = subprocess.run(["pwsh", "-NoProfile", "-File", str(FENSTER), "-Exe", str(EXE), "-Out", str(bild)],
                      capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
if lauf.returncode != 0 or not bild.is_file():
    melde([f"Bildschirmfoto gescheitert: {(lauf.stdout + lauf.stderr).strip()[:400]}"], "")

W, H, pixel = lies_bmp(bild)
oben = H - LEISTE
grund = [(x, y) for y in range(oben, H) for x in range(W) if pixel(x, y) == GRUND]
if len(grund) < 1000:
    melde([f"kein Minimap-Grund in der unteren Leiste ({len(grund)} Pixel in {GRUND}) - "
           f"Fenster {W}x{H}"], "")

links = min(x for x, _ in grund)
rechts = max(x for x, _ in grund)
hoch = min(y for _, y in grund)
tief = max(y for _, y in grund)
breite, hoehe = rechts - links + 1, tief - hoch + 1

if links < W // 2 or W - rechts > 40:
    fehler.append(f"Minimap liegt bei x = {links}..{rechts}, nicht rechts im {W} px breiten Fenster")
mindestens = 370 if W >= 2300 else 205
if breite < mindestens:
    fehler.append(f"Minimap {breite} px breit, erwartet mindestens {mindestens} (Fenster {W} px)")
if not 0.4 <= hoehe / breite <= 0.6:
    fehler.append(f"Minimap-Feld {breite}x{hoehe} - erwartet doppelt so breit wie hoch")

feld = [pixel(x, y) for y in range(hoch, tief + 1) for x in range(links, rechts + 1)]
gras = sum(1 for p in feld if p in (GRAS, GRAS_DUNKEL))
weiss = sum(1 for p in feld if p == WEISS)
if gras < 50:
    fehler.append(f"kein erkundetes Gelaende auf der Minimap zu sehen ({gras} Gras-Pixel)")
if weiss > len(feld) * 0.15:
    fehler.append(f"Minimap zu {100 * weiss / len(feld):.0f} % weiss verdeckt")

melde(fehler, f"C6m erfuellt: Minimap {breite}x{hoehe} bei x = {links}..{rechts} im {W}x{H}-Fenster, "
              f"{gras} Gras-Pixel, {100 * weiss / len(feld):.1f} % weiss")
