"""Bildschirmfotos in Abnahmen lesen - BMP, weil Python das ohne Pillow kann.

Aufnahme per _fenster.ps1 (PrintWindow, DPI-unbewusster Thread).
"""

import struct
from pathlib import Path


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


def lies_png(pfad: Path):
    """Breite, Hoehe und eine Funktion pixel(x, y) -> (r, g, b, a) fuer 8-Bit-PNG (RGB/RGBA), alle Filter."""
    import zlib
    daten = Path(pfad).read_bytes()
    i, idat = 8, b""
    while i < len(daten):
        laenge, art = struct.unpack(">I4s", daten[i:i + 8])
        inhalt = daten[i + 8:i + 8 + laenge]
        if art == b"IHDR":
            breite, hoehe, _, farbtyp = struct.unpack(">IIBB", inhalt[:10])
        elif art == b"IDAT":
            idat += inhalt
        i += 12 + laenge
    bpp = {6: 4, 2: 3}[farbtyp]
    roh = zlib.decompress(idat)
    stride = breite * bpp
    zeilen, vorher = [], bytearray(stride)
    for y in range(hoehe):
        filt = roh[y * (stride + 1)]
        z = bytearray(roh[y * (stride + 1) + 1:(y + 1) * (stride + 1)])
        for k in range(stride):
            a = z[k - bpp] if k >= bpp else 0
            b = vorher[k]
            c = vorher[k - bpp] if k >= bpp else 0
            if filt == 1:
                z[k] = (z[k] + a) & 255
            elif filt == 2:
                z[k] = (z[k] + b) & 255
            elif filt == 3:
                z[k] = (z[k] + (a + b) // 2) & 255
            elif filt == 4:
                p = a + b - c
                pa, pb, pc = abs(p - a), abs(p - b), abs(p - c)
                z[k] = (z[k] + (a if pa <= pb and pa <= pc else b if pb <= pc else c)) & 255
        zeilen.append(bytes(z))
        vorher = z

    def pixel(x, y):
        o = x * bpp
        z = zeilen[y]
        return (z[o], z[o + 1], z[o + 2], z[o + 3] if bpp == 4 else 255)

    return breite, hoehe, pixel
