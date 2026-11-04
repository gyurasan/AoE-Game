"""Bewegliche Teile einer Maschine (F15): Teile aus dem Standbild schneiden, je Phase drehen und schieben.

Für Belagerungswaffen, deren Bewegung kein Skelett braucht: der Wurfarm des Mangonels schwingt um
seine Achse, der Rammkopf stößt vor und zurück, das Gegengewicht des Trebuchets hängt am kurzen
Ende des Arms und fällt mit ihm. Ein Teil ist eine Maske (Formen wie in qwen_image.py: rechtecke,
ellipsen, linien, boegen, ohne) über dem freigestellten Standbild. Je Phase dreht es um seinen
"drehpunkt" ("winkel" in Grad, + im Uhrzeigersinn) und verschiebt sich um "versatz" [dx, dy].
Ein Teil mit "haengt_an": {"teil": name, "punkt": [x, y]} dreht sich nicht mit, sondern wird so
verschoben, dass sein Aufhängepunkt dem gedrehten Punkt des anderen Teils folgt (Gegengewicht).

Unter den Teilen liegt der Rumpf: "rumpf_bild" - dieselbe Maschine ohne die beweglichen Teile, mit
qwen_image.py neu gemalt -, sonst das Standbild mit ausgeschnittenen Teilen. Ein Teil mit
"hinten": true liegt hinter dem Rumpf: der Rammbalken verschwindet beim Zurückziehen im Schuppen.
"verlaengern": [dx, dy, n] zieht ein Teil n Pixel entgegen seiner Stoßrichtung nach (der Balken
reicht dann auch beim Vorstoß bis in den Schuppen, statt eine Lücke zu lassen). "durch": Formen (fest
auf der Leinwand) - dort liegt ein hinteres Teil doch vor dem Rumpf: der Rammbalken vor der dunklen
Schuppenöffnung, der Wurfarm vor dem hinteren Pfosten, aber hinter dem vorderen.

Ein hängendes Teil mit "tiefste": y (Rohbild) sinkt nie tiefer: reicht es mit dem gedrehten Arm
weiter hinab, wird es so weit gehoben und das Seil darüber gekürzt - alles über dem Aufhängepunkt
fällt weg, sonst stäche es oben aus dem Arm (das Gegengewicht schlüge sonst in den Boden).

Wie bei schlag.py bekommen die Phasen eine größere Leinwand im selben Maßstab: der Zuschnitt der
Figur (wie uebernehmen.ps1 ihn rechnet) wird seitlich gleich weit und nach oben erweitert, bis alle
Phasen hineinpassen; die Unterkante bleibt, die Räder stehen an derselben Stelle. Zwei fast
unsichtbare Eckpixel (Deckkraft 9) legen das Rechteck fest; die passende Zielbreite gibt das Skript
aus (bilder.json, Gruppe teilanim).

Gelenke und Masken in Pixeln des freigestellten Rohbilds (1024 x 1024), Einträge in teilanim.json.
Ergebnis je Figur unter ausgabe/teilanim/: <figur>_<art><N>_1.png (Namensschema für
uebernehmen.ps1, Seed 1), der Kontaktbogen <figur>_blatt.png und <figur>_teile.png (welcher
Bildpunkt zu welchem Teil gehört).

Braucht numpy, scipy und Pillow - die Python von ComfyUI hat alles:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/teilanim.py [figur ...]
"""
import json
import math
import sys
import tempfile
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

# Die eingebettete Python von ComfyUI nimmt den Ordner des Skripts nicht in den Suchpfad auf
sys.path.insert(0, str(Path(__file__).resolve().parent))
import qwen_image  # noqa: E402  - maske_zeichnen
import schlag  # noqa: E402  - lade, zuschnitt, RAND

HIER = Path(__file__).resolve().parent
AUSGABE = HIER / "ausgabe"
ZIEL = AUSGABE / "teilanim"
RAND = schlag.RAND
FARBEN = [(255, 0, 255), (0, 220, 255), (255, 200, 0), (0, 255, 0), (255, 90, 90)]


def maske(form, breite, hoehe):
    """Die Formen als boolesche Maske auf der großen Leinwand (Koordinaten des Rohbilds)."""
    datei = Path(tempfile.gettempdir()) / "teilanim_maske.png"
    qwen_image.maske_zeichnen(form, breite, hoehe, datei)
    klein = np.array(Image.open(datei).convert("L")) > 127
    gross = np.zeros((hoehe + 2 * RAND, breite + 2 * RAND), bool)
    gross[RAND:RAND + hoehe, RAND:RAND + breite] = klein
    return gross


def ausmalen(teil):
    """Durchsichtige Pixel eines Teils in der Farbe des nächsten deckenden - sonst zöge die
    bikubische Drehung am Rand Schwarz herein (wie schlag.lade für das ganze Bild)."""
    leer = teil[:, :, 3] < 200
    if leer.any() and (~leer).any():
        _, (ny, nx) = ndimage.distance_transform_edt(leer, return_indices=True)
        teil = teil.copy()
        teil[leer, :3] = teil[ny[leer], nx[leer], :3]
    return teil


def drehen(teil, um, grad, versatz):
    """Dreht ein RGBA-Bild um 'um' (Leinwandpixel) im Uhrzeigersinn und verschiebt es danach."""
    bild = Image.fromarray(teil)
    r = math.radians(grad)
    c, s = math.cos(r), math.sin(r)
    ux, uy = um
    dx, dy = versatz
    # Rückwärtsabbildung: Zielpixel (x, y) -> Quelle = Drehung um -grad von (x - d) um 'um'
    a, b = c, s
    d, e = -s, c
    x0, y0 = ux + dx, uy + dy
    koeff = (a, b, ux - a * x0 - b * y0, d, e, uy - d * x0 - e * y0)
    return np.array(bild.transform(bild.size, Image.AFFINE, koeff, resample=Image.BICUBIC))


def punkt_gedreht(p, um, grad, versatz):
    r = math.radians(grad)
    x, y = p[0] - um[0], p[1] - um[1]
    return (um[0] + x * math.cos(r) - y * math.sin(r) + versatz[0],
            um[1] + x * math.sin(r) + y * math.cos(r) + versatz[1])


def verlaengern(teil, dx, dy, n):
    """Zieht ein Teil n Pixel in Richtung (dx, dy) nach: jede Kopie füllt nur, wo noch nichts ist."""
    laenge = math.hypot(dx, dy) or 1
    ex, ey = dx / laenge, dy / laenge
    erg = teil.copy()
    for t in range(1, n + 1):
        sx, sy = int(round(ex * t)), int(round(ey * t))
        kopie = np.roll(np.roll(teil, sy, axis=0), sx, axis=1)
        leer = erg[:, :, 3] == 0
        erg[leer] = kopie[leer]
    return erg


def zielbreite_der_figur(figur):
    """Zielbreite des Standbilds der Figur aus bilder.json (Eintrag oder Gruppe)."""
    katalog = json.loads((HIER / "bilder.json").read_text(encoding="utf-8"))
    for inhalt in katalog.values():
        if not isinstance(inhalt, dict):
            continue
        for e in inhalt.get("bilder", []):
            if e.get("figur") == figur and e.get("ziel"):
                return e.get("zielbreite", inhalt.get("zielbreite", 128))
    return 128


def figur(name, cfg):
    verschoben = lambda p: (p[0] + RAND, p[1] + RAND)   # noqa: E731 - Koordinaten auf der Leinwand
    stand = schlag.lade(AUSGABE / cfg["bild"])
    H, W = stand.shape[:2]
    roh_b, roh_h = W - 2 * RAND, H - 2 * RAND
    deckend = stand[:, :, 3] > 8

    teile = {}
    durch = {}
    belegt = np.zeros((H, W), bool)
    for t in cfg["teile"]:
        m = maske(t["maske"], roh_b, roh_h) & deckend
        if "durch" in t:
            durch[t["name"]] = maske(t["durch"], roh_b, roh_h)
        bild = np.zeros_like(stand)
        bild[m] = stand[m]
        if "verlaengern" in t:
            bild = verlaengern(bild, *t["verlaengern"])
        teile[t["name"]] = (t, ausmalen(bild))
        belegt |= m
    if cfg.get("rumpf_bild"):
        rumpf = schlag.lade(AUSGABE / cfg["rumpf_bild"])
    else:
        rumpf = stand.copy()
        rumpf[belegt] = 0

    # Kontrollbild: welcher Bildpunkt gehört zu welchem Teil
    probe = stand.copy()
    for k, (t, bild) in enumerate(teile.values()):
        m = maske(t["maske"], roh_b, roh_h) & deckend
        probe[m, :3] = (0.5 * probe[m, :3] + 0.5 * np.array(FARBEN[k % len(FARBEN)])).astype(np.uint8)
    ZIEL.mkdir(parents=True, exist_ok=True)
    Image.fromarray(probe[RAND:RAND + roh_h, RAND:RAND + roh_b]).save(ZIEL / f"{name}_teile.png")

    bilder = []
    for phase in cfg["phasen"]:
        lagen = {}
        for tname, (t, bild) in teile.items():
            p = phase.get(tname, {})
            if "haengt_an" in t:
                eltern = t["haengt_an"]
                et, _ = teile[eltern["teil"]]
                ep = phase.get(eltern["teil"], {})
                hp = verschoben(eltern["punkt"])
                ziel = punkt_gedreht(hp, verschoben(et["drehpunkt"]), ep.get("winkel", 0), ep.get("versatz", (0, 0)))
                v = p.get("versatz", (0, 0))
                lage = drehen(bild, hp, p.get("winkel", 0), (ziel[0] - hp[0] + v[0], ziel[1] - hp[1] + v[1]))
                if "tiefste" in t:
                    zeilen = np.nonzero((lage[:, :, 3] > 8).any(axis=1))[0]
                    zuviel = int(zeilen.max()) - (t["tiefste"] + RAND) if len(zeilen) else 0
                    if zuviel > 0:
                        lage = np.roll(lage, -zuviel, axis=0)
                        lage[-zuviel:] = 0
                        lage[:int(round(ziel[1] + v[1]))] = 0     # Seil über dem Aufhängepunkt
                lagen[tname] = lage
            else:
                lagen[tname] = drehen(bild, verschoben(t["drehpunkt"]), p.get("winkel", 0), p.get("versatz", (0, 0)))
        leinwand = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for tname, (t, _) in teile.items():
            if t.get("hinten"):
                leinwand.alpha_composite(Image.fromarray(lagen[tname]))
        leinwand.alpha_composite(Image.fromarray(rumpf))
        for tname, (t, _) in teile.items():
            if not t.get("hinten"):
                leinwand.alpha_composite(Image.fromarray(lagen[tname]))
        for tname, fenster in durch.items():
            vorn = lagen[tname].copy()
            vorn[~fenster] = 0
            leinwand.alpha_composite(Image.fromarray(vorn))
        bilder.append(leinwand)
    ausgeben(name, cfg, bilder, W, H)


def ausgeben(name, cfg, bilder, W, H):
    # Leinwand wie bei schlag.py: Zuschnitt der Figur seitlich gleich weit und nach oben erweitert
    zx0, zy0, zx1, zy1 = (v + RAND for v in schlag.zuschnitt(cfg["figur"]))
    box = None
    for b in bilder:
        r = b.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        box = r if box is None else (min(box[0], r[0]), min(box[1], r[1]), max(box[2], r[2]), max(box[3], r[3]))
    mitte = (zx0 + zx1) / 2
    halb = max(mitte - min(zx0, box[0]), max(zx1, box[2]) - mitte) + 4
    x0, x1 = int(math.floor(mitte - halb)), int(math.ceil(mitte + halb))
    y0, y1 = min(zy0, box[1]) - 4, zy1
    art = cfg.get("art", "angriff")
    for n, b in enumerate(bilder):
        arr = np.array(b)
        arr[y1:] = 0                       # nichts unter der Unterkante des Zuschnitts
        for px, py in ((x0, y0), (x1 - 1, y1 - 1)):
            if arr[py, px, 3] <= 8:
                arr[py, px] = (0, 0, 0, 9)   # Eckpixel: legen das Rechteck für uebernehmen.ps1 fest
        b = Image.fromarray(arr)
        b.save(ZIEL / f"{name}_{art}{n + 1}_1.png")
        bilder[n] = b
    basis = zielbreite_der_figur(cfg["figur"])
    zielbreite = round(basis * (x1 - x0) / (zx1 - zx0))
    print(f"{name}: {len(bilder)} Phasen ({art}), Leinwand {x1 - x0}x{y1 - y0} statt {zx1 - zx0}x{zy1 - zy0}, "
          f"Zielbreite {zielbreite} (Standbild: {basis})")

    sk = 0.3
    bw, bh = int((x1 - x0) * sk), int((y1 - y0) * sk)
    blatt = Image.new("RGBA", (bw * len(bilder), bh), (110, 150, 80, 255))
    for k, b in enumerate(bilder):
        blatt.alpha_composite(b.crop((x0, y0, x1, y1)).resize((bw, bh), Image.LANCZOS), (k * bw, 0))
    blatt.save(ZIEL / f"{name}_blatt.png")


def main():
    katalog = json.loads((HIER / "teilanim.json").read_text(encoding="utf-8"))
    namen = sys.argv[1:] or [n for n in katalog if not n.startswith("_")]
    for n in namen:
        figur(n, katalog[n])


if __name__ == "__main__":
    main()
