"""Abnahme C7m: qwen_image.py malt Rechtecke in die Maske und reicht "staerke" als denoise durch.

Prüft das Verhalten, nicht die Schreibweise: die Maske wird wirklich gezeichnet
und als PNG zurückgelesen, und erzeuge() wird mit abgeklemmtem Server
(hochladen, ausfuehren) aufgerufen, um den Graphen abzufangen.
"""

import shutil
import struct
import sys
import zlib
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

BILDER = Path("tools/bilder").resolve()
sys.path.insert(0, str(BILDER))
import qwen_image  # noqa: E402

fehler = []
ORDNER = BILDER / "ausgabe" / "_c7m_pruefung"
ORDNER.mkdir(parents=True, exist_ok=True)


def png_lesen(pfad: Path) -> tuple[int, int, bytes]:
    """Graustufen-PNG (8 bit, nur Filter 0) wie maske_zeichnen es schreibt."""
    daten = pfad.read_bytes()
    i, idat, breite, hoehe = 8, b"", 0, 0
    while i < len(daten):
        laenge, art = struct.unpack(">I4s", daten[i:i + 8])
        inhalt = daten[i + 8:i + 8 + laenge]
        if art == b"IHDR":
            breite, hoehe = struct.unpack(">II", inhalt[:8])
        elif art == b"IDAT":
            idat += inhalt
        i += 12 + laenge
    roh = zlib.decompress(idat)
    zeilen = b"".join(roh[y * (breite + 1) + 1:(y + 1) * (breite + 1)] for y in range(hoehe))
    return breite, hoehe, zeilen


try:
    # --- Rechtecke in der Maske ------------------------------------------
    maske = qwen_image.maske_zeichnen({"rechtecke": [[10, 20, 30, 5]], "ellipsen": [[50, 50, 4, 4]]},
                                      64, 64, ORDNER / "maske.png")
    b, h, px = png_lesen(maske)
    wert = lambda x, y: px[y * b + x]  # noqa: E731
    for (x, y), soll in {(10, 20): 255, (39, 24): 255, (25, 22): 255, (9, 22): 0, (40, 22): 0,
                         (25, 19): 0, (25, 25): 0, (50, 50): 255, (0, 0): 0}.items():
        if wert(x, y) != soll:
            fehler.append(f"Maske mit Rechteck [10, 20, 30, 5]: Pixel ({x}, {y}) ist {wert(x, y)} statt {soll}")
    _, _, nur_rechteck = png_lesen(qwen_image.maske_zeichnen({"rechtecke": [[10, 20, 30, 5]]}, 64, 64,
                                                              ORDNER / "rechteck.png"))
    weiss = sum(1 for v in nur_rechteck if v == 255)
    if weiss != 150:
        fehler.append(f"Maske: {weiss} weiße Pixel statt 150 - das Rechteck 30 x 5 deckt nicht genau seine Fläche")

    # --- staerke kommt als denoise im Graphen an ---------------------------
    gefangen = []
    qwen_image.hochladen = lambda pfad: pfad.name
    qwen_image.ausfuehren = lambda graph, ziel: gefangen.append(graph) or 0.0
    gruppe = {"groesse": [64, 64], "stil": "s"}
    eintrag = {"name": "probe", "prompt": "p", "vorlage": "vorlage.png",
               "maske": {"rechtecke": [[0, 0, 8, 8]]}}
    for staerke in (None, 0.55):
        e = dict(eintrag)
        if staerke is not None:
            e["staerke"] = staerke
        qwen_image.erzeuge(e, gruppe, 7, ORDNER / "probe_7.png")
        knoten = gefangen[-1]["8"]["inputs"]
        soll = 1.0 if staerke is None else staerke
        if knoten.get("denoise") != soll:
            fehler.append(f"erzeuge mit staerke={staerke}: denoise im KSampler ist {knoten.get('denoise')} statt {soll}")
        if gefangen[-1]["10"]["inputs"]["images"] != ["15", 0]:
            fehler.append("ausbesser_graph speichert nicht mehr das in die Vorlage eingesetzte Bild (Knoten 15)")
    graph = qwen_image.ausbesser_graph("p", "n", "v.png", "m.png", 1, 30, 3.0, "x")
    if graph["8"]["inputs"].get("denoise") != 1.0:
        fehler.append("ausbesser_graph ohne staerke entrauscht nicht voll (denoise 1.0)")
    # Ohne Vorlage bleibt alles beim Alten
    qwen_image.erzeuge({"name": "frei", "prompt": "p"}, gruppe, 7, ORDNER / "frei_7.png")
    if gefangen[-1]["8"]["inputs"].get("denoise") != 1.0 or "11" in gefangen[-1]:
        fehler.append("ein Eintrag ohne Vorlage läuft nicht mehr als reines Erzeugen")
finally:
    shutil.rmtree(ORDNER, ignore_errors=True)

melde(fehler, "C7m erfuellt: Rechtecke in der Maske, staerke wird als denoise durchgereicht")
