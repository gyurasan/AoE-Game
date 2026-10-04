"""Abnahme C7u: uebernehmen.ps1 beschneidet alle Bilder einer Figur auf dasselbe Rechteck.

Prüft das Verhalten: ein Mini-Katalog in einem Temp-Ordner mit zwei Gruppen,
Bilder mit bekannten sichtbaren Flächen, das echte Skript läuft darüber (einmal
je Gruppe, wie mit -Gruppe), danach werden die PNGs dekodiert. Eine Figur muss
in allen ihren Bildern dieselbe Größe haben und an derselben Stelle stehen;
ein Eintrag ohne Figur wird weiter auf sich selbst beschnitten.
"""

import json
import shutil
import struct
import subprocess
import sys
import tempfile
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

SKRIPT = Path("tools/bilder/uebernehmen.ps1").resolve()
fehler = []


def png_schreiben(pfad: Path, breite: int, hoehe: int, sichtbar: tuple[int, int, int, int]):
    """RGBA-PNG, durchsichtig bis auf das Rechteck (x, y, b, h) in Rot."""
    x0, y0, b, h = sichtbar
    zeilen = b""
    for y in range(hoehe):
        zeile = bytearray(b"\0")
        for x in range(breite):
            zeile += bytes((200, 30, 30, 255)) if x0 <= x < x0 + b and y0 <= y < y0 + h else bytes(4)
        zeilen += bytes(zeile)

    def abschnitt(art, daten):
        return struct.pack(">I", len(daten)) + art + daten + struct.pack(">I", zlib.crc32(art + daten))

    kopf = struct.pack(">IIBBBBB", breite, hoehe, 8, 6, 0, 0, 0)
    pfad.parent.mkdir(parents=True, exist_ok=True)
    pfad.write_bytes(b"\x89PNG\r\n\x1a\n" + abschnitt(b"IHDR", kopf)
                     + abschnitt(b"IDAT", zlib.compress(zeilen)) + abschnitt(b"IEND", b""))


def png_alpha(pfad: Path):
    """Breite, Höhe und das Rechteck der deckenden Pixel (x, y, b, h) - mit allen PNG-Filtern."""
    daten = pfad.read_bytes()
    i, idat = 8, b""
    while i < len(daten):
        laenge, art = struct.unpack(">I4s", daten[i:i + 8])
        inhalt = daten[i + 8:i + 8 + laenge]
        if art == b"IHDR":
            breite, hoehe, tiefe, farbtyp = struct.unpack(">IIBB", inhalt[:10])
        elif art == b"IDAT":
            idat += inhalt
        i += 12 + laenge
    bpp = {6: 4, 2: 3}[farbtyp]
    roh = zlib.decompress(idat)
    stride = breite * bpp
    vorher = bytearray(stride)
    xs, ys = [], []
    for y in range(hoehe):
        filt = roh[y * (stride + 1)]
        zeile = bytearray(roh[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for k in range(stride):
            a = zeile[k - bpp] if k >= bpp else 0
            b = vorher[k]
            c = vorher[k - bpp] if k >= bpp else 0
            if filt == 1:
                zeile[k] = (zeile[k] + a) & 255
            elif filt == 2:
                zeile[k] = (zeile[k] + b) & 255
            elif filt == 3:
                zeile[k] = (zeile[k] + (a + b) // 2) & 255
            elif filt == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                zeile[k] = (zeile[k] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        for x in range(breite):
            if farbtyp == 6 and zeile[x * 4 + 3] > 128:
                xs.append(x)
                ys.append(y)
        vorher = zeile
    rahmen = (min(xs), min(ys), max(xs) - min(xs) + 1, max(ys) - min(ys) + 1) if xs else None
    return breite, hoehe, rahmen


tmp = Path(tempfile.mkdtemp(prefix="c7u_"))
try:
    bilder = tmp / "tools" / "bilder"
    bilder.mkdir(parents=True)
    shutil.copyfile(SKRIPT, bilder / "uebernehmen.ps1")
    (tmp / "AgeOfEvolutions" / "AgeOfEvolutions.Core" / "Content").mkdir(parents=True)
    katalog = {
        "a": {"zuschneiden": True, "bilder": [
            {"name": "stand", "seed": 1, "ziel": "T/stand.png", "figur": "tier"},
            {"name": "solo", "seed": 1, "ziel": "T/solo.png"}]},
        "b": {"zuschneiden": True, "bilder": [
            {"name": "lauf", "seed": 1, "ziel": "T/lauf.png", "figur": "tier"}]},
    }
    (bilder / "bilder.json").write_text(json.dumps(katalog), encoding="utf-8")
    png_schreiben(bilder / "ausgabe/a/stand_1.png", 64, 64, (10, 10, 10, 20))
    png_schreiben(bilder / "ausgabe/a/solo_1.png", 64, 64, (10, 10, 10, 20))
    png_schreiben(bilder / "ausgabe/b/lauf_1.png", 64, 64, (30, 20, 20, 20))
    for gruppe in ("a", "b"):
        lauf = subprocess.run(["pwsh", "-NoProfile", "-File", str(bilder / "uebernehmen.ps1"), "-Gruppe", gruppe],
                              capture_output=True, text=True, encoding="utf-8", errors="replace")
        if lauf.returncode != 0:
            fehler.append(f"uebernehmen.ps1 -Gruppe {gruppe} bricht ab: {(lauf.stderr or lauf.stdout).strip()[:300]}")
    content = tmp / "AgeOfEvolutions" / "AgeOfEvolutions.Core" / "Content" / "T"
    erwartet = {   # Vereinigung von (10, 10, 10, 20) und (30, 20, 20, 20) = (10, 10, 40, 30)
        "stand.png": (40, 30, (0, 0, 10, 20)),
        "lauf.png": (40, 30, (20, 10, 20, 20)),
        "solo.png": (10, 20, (0, 0, 10, 20)),
    }
    for name, (b, h, rahmen) in erwartet.items():
        pfad = content / name
        if not pfad.is_file():
            fehler.append(f"{name} wurde nicht geschrieben")
            continue
        ist = png_alpha(pfad)
        if ist != (b, h, rahmen):
            fehler.append(f"{name}: {ist[0]}x{ist[1]}, Figur bei {ist[2]} - erwartet {b}x{h}, Figur bei {rahmen}")
finally:
    shutil.rmtree(tmp, ignore_errors=True)

melde(fehler, "C7u erfuellt: alle Bilder einer Figur auf dasselbe Rechteck beschnitten, auch über Gruppen")
