"""Abnahme C6m/C6d: die Minimap ist im laufenden Spiel rechts unten zu sehen,
in derselben Ansicht wie die Spielkarte.

Bis 2026-10-03 sass sie links und war von weissen Rechtecken verdeckt: die
schraegen Kanten des Kameraausschnitts wurden als gefuellte Rechtecke
zwischen ihren Endpunkten gezeichnet. Lage, Groesse und Klick rechnet
`tools/spielablauf -- minimap` nach. Was tatsaechlich auf dem Bildschirm
landet, sieht nur ein Bildschirmfoto - deshalb startet diese Abnahme die
DesktopGL-Fassung mit --rts und zaehlt Pixel in der unteren Leiste:

- der dunkle Minimap-Grund (12, 12, 12) liegt rechts unten, unten in der Leiste und
  seit C11 nach oben ueber sie hinaus, rund 346 px gross (80 % mehr als die fruehere,
  die in die Leiste passen musste) und quadratisch wie die Karte (C6d: Draufsicht),
- darin ist erkundetes Gelaende zu sehen (Grasland in der Minimap-Farbe), und
  zwar oben links - dort liegt das Startgebiet (Kachel 3, 3) auch auf der
  Spielkarte; in der frueheren Raute lag es oben in der Mitte,
- und hoechstens ein kleiner Teil ist weiss (Kamerarahmen und Einheiten).

Vorher muss DesktopGL gebaut sein (verify-Schritt davor). Fuer einige Sekunden
erscheint das Spielfenster.
"""

import json
import os
import subprocess
import sys
import tempfile
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_bmp  # noqa: E402
from _cs import melde  # noqa: E402

EXE = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0/AgeOfEvolutions.exe").resolve()
FENSTER = Path(__file__).resolve().parent / "_fenster.ps1"
LEISTE = 200                       # HUD_BOTTOM_HEIGHT
MINIMAP = 346                      # MINIMAP_SIZE seit C11, ragt ueber die Leiste
GRUND = (12, 12, 12)               # Minimap-Grund
GRAS = (104, 148, 68)              # IsoCol Grasland
GRAS_DUNKEL = tuple(c * 96 // 255 for c in GRAS)   # erkundet, nicht in Sicht
WEISS = (255, 255, 255)


fehler = []
if not EXE.is_file():
    melde([f"{EXE} fehlt - DesktopGL nicht gebaut"], "")

bild = Path(tempfile.gettempdir()) / "c6m_minimap.bmp"
# Im Vollbild liefert PrintWindow ein schwarzes Bild (gesehen 2026-10-04) - für die
# Aufnahme startet das Spiel deshalb im Fenster; die Einstellung des Nutzers wird
# danach in jedem Fall zurückgeschrieben
EINSTELLUNGEN = Path(os.environ.get("APPDATA", "")) / "AgeOfEvolutions" / "settings.json"
gesichert = EINSTELLUNGEN.read_bytes() if EINSTELLUNGEN.is_file() else None
try:
    if gesichert is not None:
        werte = json.loads(gesichert.decode("utf-8-sig"))
        werte["FullScreen"] = False
        EINSTELLUNGEN.write_text(json.dumps(werte, indent=2), encoding="utf-8")
    lauf = subprocess.run(["pwsh", "-NoProfile", "-File", str(FENSTER), "-Exe", str(EXE), "-Out", str(bild)],
                          capture_output=True, text=True, encoding="utf-8", errors="replace", timeout=90)
finally:
    if gesichert is not None:
        EINSTELLUNGEN.write_bytes(gesichert)
if lauf.returncode != 0 or not bild.is_file():
    melde([f"Bildschirmfoto gescheitert: {(lauf.stdout + lauf.stderr).strip()[:400]}"], "")

W, H, pixel = lies_bmp(bild)
oben = H - LEISTE - (MINIMAP - LEISTE) - 40   # Leiste und das erhoehte Feld darueber
grund = [(x, y) for y in range(oben, H) for x in range(W) if pixel(x, y) == GRUND]
if len(grund) < 1000:
    melde([f"kein Minimap-Grund rechts unten ({len(grund)} Pixel in {GRUND}) - "
           f"Fenster {W}x{H}"], "")

links = min(x for x, _ in grund)
rechts = max(x for x, _ in grund)
hoch = min(y for _, y in grund)
tief = max(y for _, y in grund)
breite, hoehe = rechts - links + 1, tief - hoch + 1

if links < W // 2 or W - rechts > 40:
    fehler.append(f"Minimap liegt bei x = {links}..{rechts}, nicht rechts im {W} px breiten Fenster")
if hoehe < MINIMAP * 0.95:
    fehler.append(f"Minimap nur {hoehe} px hoch - erwartet rund {MINIMAP} px (80 % mehr als die fruehere)")
if not 0.95 <= hoehe / breite <= 1.05:
    fehler.append(f"Minimap-Feld {breite}x{hoehe} - erwartet quadratisch wie die 64x64-Karte")

feld = [pixel(x, y) for y in range(hoch, tief + 1) for x in range(links, rechts + 1)]
gras_orte = [(x, y) for y in range(hoch, tief + 1) for x in range(links, rechts + 1)
             if pixel(x, y) in (GRAS, GRAS_DUNKEL)]
gras = len(gras_orte)
weiss = sum(1 for p in feld if p == WEISS)
if gras < 50:
    fehler.append(f"kein erkundetes Gelaende auf der Minimap zu sehen ({gras} Gras-Pixel)")
else:
    # Startgebiet oben links wie auf der Spielkarte - Schwerpunkt im linken oberen Viertel
    sx = (sum(x for x, _ in gras_orte) / gras - links) / breite
    sy = (sum(y for _, y in gras_orte) / gras - hoch) / hoehe
    if sx > 0.25 or sy > 0.25:
        fehler.append(f"Startgebiet liegt bei {sx:.0%}/{sy:.0%} der Minimap, auf der Spielkarte "
                      "oben links - nicht dieselbe Ansicht")
if weiss > len(feld) * 0.15:
    fehler.append(f"Minimap zu {100 * weiss / len(feld):.0f} % weiss verdeckt")

melde(fehler, f"C6m erfuellt: Minimap {breite}x{hoehe} bei x = {links}..{rechts} im {W}x{H}-Fenster, "
              f"{gras} Gras-Pixel, {100 * weiss / len(feld):.1f} % weiss")
