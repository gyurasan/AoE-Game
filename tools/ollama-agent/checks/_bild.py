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
