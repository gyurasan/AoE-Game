"""Schlagphasen eines Soldaten (C4n): den Waffenarm über ein Skelett schwingen lassen.

Aus dem Standbild wird der Waffenarm geschnitten - Oberarm (Schulter bis Ellbogen) und
Unterarm mit Faust und Waffe (Ellbogen bis Faust, die Waffe fest in der Hand) - und je Phase
um Schulter und Ellbogen gedreht. Den Rumpf liefert ein zweites Bild derselben Figur ohne
Waffenarm ("rumpf_bild", mit qwen_image.py neu gemalt): dort ist zu sehen, was Arm und Waffe
im Standbild verdeckten. Der Arm liegt über dem Rumpf; in Phasen mit "hinter_kopf" geht er
hinter dem Kopf durch (der Kopf aus dem Rumpfbild liegt darüber). Optional neigt sich der
Oberkörper leicht in den Schlag. Winkel stehen in schlag.json gegen das Standbild: + schwenkt
einen nach unten zeigenden Knochen nach vorn (rechts), - nach hinten; über 180 Grad ist
erlaubt, so geht ein Hieb von oben über den Kopf.

Ein ausgeholtes Schwert ragt weit über das Standbild hinaus. Die Schlagbilder bekommen deshalb
eine größere Leinwand im selben Maßstab: der Zuschnitt der Figur (die Vereinigung von Stand-
und Gehbildern, wie uebernehmen.ps1 sie rechnet) wird seitlich gleich weit und nach oben
erweitert, bis alle Phasen hineinpassen - die Unterkante bleibt, die Füße stehen also an
derselben Stelle. Zwei fast unsichtbare Eckpixel (Deckkraft 9) legen dieses Rechteck fest;
die passende Zielbreite gibt das Skript aus (für bilder.json, Gruppe schlag).

Ergebnis je Figur unter ausgabe/schlag/: <figur>_schlag<N>_1.png (Namensschema für
uebernehmen.ps1, Seed 1) und der Kontaktbogen <figur>_blatt.png.

Braucht numpy, scipy und Pillow - die Python von ComfyUI hat alles:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/schlag.py [figur ...]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

HIER = Path(__file__).resolve().parent
AUSGABE = HIER / "ausgabe"
RAND = 500          # so viel Platz rund um das 1024er Bild für das ausgeholte Schwert


def abstand_strecke(px, py, a, b):
    """Abstand jedes Pixels zur Strecke a-b."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))


def dreh(p, um, grad):
    """Dreht p um 'um' so, dass ein nach unten zeigender Vektor um +grad nach vorn schwenkt."""
    r = math.radians(grad)
    x, y = p[0] - um[0], p[1] - um[1]
    return (um[0] + x * math.cos(r) + y * math.sin(r), um[1] - x * math.sin(r) + y * math.cos(r))


def lade(pfad):
    """Bild auf die größere Leinwand gesetzt; halbtransparente Randpixel in der Farbe des
    nächsten deckenden Pixels - sonst würden sie beim Drehen als weiße Striche sichtbar."""
    a = np.array(Image.open(pfad).convert("RGBA"))
    halb = a[:, :, 3] < 200
    if halb.any() and (~halb).any():
        _, (ny, nx) = ndimage.distance_transform_edt(halb, return_indices=True)
        a[halb, :3] = a[ny[halb], nx[halb], :3]
    gross = np.zeros((a.shape[0] + 2 * RAND, a.shape[1] + 2 * RAND, 4), np.uint8)
    gross[RAND:RAND + a.shape[0], RAND:RAND + a.shape[1]] = a
    return gross


def zuschnitt(figur):
    """Das Rechteck der Figur wie in uebernehmen.ps1: Vereinigung der deckenden Flächen
    (Deckkraft über 8) aller Einträge mit dieser Figur und einem Ziel, in Rohbild-Pixeln."""
    katalog = json.loads((HIER / "bilder.json").read_text(encoding="utf-8"))
    box = None
    for gruppe, inhalt in katalog.items():
        if not isinstance(inhalt, dict):
            continue
        for e in inhalt.get("bilder", []):
            if e.get("figur") != figur or not e.get("ziel"):
                continue
            al = np.array(Image.open(AUSGABE / gruppe / f"{e['name']}_{e['seed']}.png").convert("RGBA"))[:, :, 3] > 8
            ys, xs = np.where(al)
            r = (xs.min(), ys.min(), xs.max() + 1, ys.max() + 1)
            box = r if box is None else (min(box[0], r[0]), min(box[1], r[1]), max(box[2], r[2]), max(box[3], r[3]))
    return box


def ausgeben(name, cfg, bilder, W, H):
    # Leinwand: der Zuschnitt der Figur, seitlich gleich weit und nach oben erweitert, bis alle
    # Phasen hineinpassen; die Unterkante bleibt
    zx0, zy0, zx1, zy1 = (v + RAND for v in zuschnitt(cfg["figur"]))
    box = None
    for b in bilder:
        r = b.getchannel("A").point(lambda v: 255 if v > 8 else 0).getbbox()
        box = r if box is None else (min(box[0], r[0]), min(box[1], r[1]), max(box[2], r[2]), max(box[3], r[3]))
    mitte = (zx0 + zx1) / 2
    halb = max(mitte - min(zx0, box[0]), max(zx1, box[2]) - mitte) + 4
    x0, x1 = int(math.floor(mitte - halb)), int(math.ceil(mitte + halb))
    y0, y1 = min(zy0, box[1]) - 4, zy1
    ziel = AUSGABE / "schlag"
    ziel.mkdir(parents=True, exist_ok=True)
    for n, b in enumerate(bilder):
        arr = np.array(b)
        arr[y1:] = 0                       # nichts unter der Unterkante des Zuschnitts
        for px, py in ((x0, y0), (x1 - 1, y1 - 1)):
            if arr[py, px, 3] <= 8:
                arr[py, px] = (0, 0, 0, 9)   # Eckpixel: legen das Rechteck für uebernehmen.ps1 fest
        b = Image.fromarray(arr)
        b.crop((0, 0, W, H)).save(ziel / f"{name}_schlag{n + 1}_1.png")
        bilder[n] = b
    zielbreite = round(128 * (x1 - x0) / (zx1 - zx0))
    print(f"{name}: {len(bilder)} Schlagphasen, Leinwand {x1 - x0}x{y1 - y0} statt {zx1 - zx0}x{zy1 - zy0}, "
          f"Zielbreite {zielbreite} (Zuschnitt der Figur: 128)")

    sk = 0.3
    bw, bh = int((x1 - x0) * sk), int((y1 - y0) * sk)
    blatt = Image.new("RGBA", (bw * len(bilder), bh), (110, 150, 80, 255))
    for k, b in enumerate(bilder):
        blatt.alpha_composite(b.crop((x0, y0, x1, y1)).resize((bw, bh), Image.LANCZOS), (k * bw, 0))
    blatt.save(ziel / f"{name}_blatt.png")


def figur(name, cfg):
    verschoben = lambda p: (p[0] + RAND, p[1] + RAND)   # noqa: E731 - Koordinaten auf der Leinwand
    a = lade(AUSGABE / cfg["bild"])
    rumpf = lade(AUSGABE / cfg["rumpf_bild"])
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    deckend = a[:, :, 3] > 8
    s, e, h = (verschoben(cfg["arm"][n]) for n in ("schulter", "ellbogen", "hand"))
    # Der Arm: nah am Ober- und Unterarm, dazu die Waffe als Linien; die Schulterkappe bleibt
    oberarm = deckend & (abstand_strecke(xx, yy, s, e) < cfg["oberarm_breite"])
    unterarm = deckend & (abstand_strecke(xx, yy, e, h) < cfg["unterarm_breite"])
    waffe = np.zeros((H, W), bool)
    for x1, y1, x2, y2, dicke in cfg["waffe"]:
        waffe |= deckend & (abstand_strecke(xx, yy, verschoben((x1, y1)), verschoben((x2, y2))) < dicke / 2)
    for x0, y0, x1, y1 in cfg.get("nicht_arm", []):
        oberarm[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND] = False
        unterarm[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND] = False
    unterarm |= waffe
    oberarm &= ~unterarm
    teil_ober = np.zeros_like(a)
    teil_ober[oberarm] = a[oberarm]
    # Den Oberarm unter der Schulterkappe gespiegelt verlängern: die Zeilen unter dem Schnitt
    # noch einmal darüber - so bleibt der Ärmel an der Schulter, auch wenn der Arm hoch schwingt
    schnitt = cfg["schulter_schnitt"] + RAND
    for k in range(1, cfg.get("verlaengerung", 60)):
        spalten = np.where(oberarm[schnitt + k])[0]
        teil_ober[schnitt - k, spalten] = a[schnitt + k, spalten]
    teil_unter = np.zeros_like(a)
    teil_unter[unterarm] = a[unterarm]
    # Scheibe am Ellbogen im Unterarm, damit das Gelenk bei jedem Winkel geschlossen bleibt
    scheibe = (oberarm | unterarm) & (np.hypot(xx - e[0], yy - e[1]) < cfg["unterarm_breite"] * 0.8)
    teil_unter[scheibe] = a[scheibe]
    # Der Kopf aus dem Rumpfbild, für Phasen, in denen der Arm hinter ihm durchgeht, und die
    # Schulterkappe, die immer über dem Arm liegt
    def ellipse(cx, cy, rx, ry):
        teil = rumpf.copy()
        teil[((xx - cx - RAND) / rx) ** 2 + ((yy - cy - RAND) / ry) ** 2 > 1] = 0
        return teil
    kopf = ellipse(*cfg["kopf"])
    kappe = ellipse(*cfg["schulterkappe"])
    ober_img, unter_img = Image.fromarray(teil_ober), Image.fromarray(teil_unter)
    huefte = verschoben(cfg["huefte"])

    bilder = []
    for ph in cfg["phasen"]:
        neig = ph.get("rumpf", 0.0)
        # Oberkörper (über der Hüfte) leicht in den Schlag neigen, die Beine bleiben
        # Ober- und Unterkörper überlappen an der Hüfte, sonst bliebe beim Neigen eine Naht
        oben, unten = rumpf.copy(), rumpf.copy()
        oben[int(huefte[1]) + 10:] = 0
        unten[:int(huefte[1]) - 10] = 0
        # "rumpf" positiv neigt nach vorn: der Kopf geht nach rechts, also im Uhrzeigersinn
        oben_img = Image.fromarray(oben).rotate(-neig, resample=Image.BICUBIC, center=huefte) if neig else Image.fromarray(oben)
        koerper = Image.alpha_composite(Image.fromarray(unten), oben_img)
        s2 = dreh(s, huefte, -neig) if neig else s
        e2 = dreh(e, s, ph["oberarm"])
        e2 = (e2[0] - s[0] + s2[0], e2[1] - s[1] + s2[1])
        arm = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        for teil, um, nach, grad in ((ober_img, s, s2, ph["oberarm"]), (unter_img, e, e2, ph["oberarm"] + ph["unterarm"])):
            gedreht = teil.rotate(grad, resample=Image.BICUBIC, center=um)
            lage = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            lage.paste(gedreht, (int(round(nach[0] - um[0])), int(round(nach[1] - um[1]))), gedreht)
            arm = Image.alpha_composite(arm, lage)
        bild = Image.alpha_composite(koerper, arm)
        for teil, immer in ((kappe, True), (kopf, ph.get("hinter_kopf", False))):
            if immer:
                teil_img = Image.fromarray(teil)
                bild = Image.alpha_composite(bild, teil_img.rotate(-neig, resample=Image.BICUBIC, center=huefte) if neig else teil_img)
        bilder.append(bild)

    ausgeben(name, cfg, bilder, W, H)


def speer(name, cfg):
    """Speerstoß vom Pferd (Modus "speer"): der Speer dreht um die Faust ("winkel", negativ senkt
    die Spitze nach vorn), gleitet durch die Hand nach hinten und stößt nach vorn ("schub" in
    Pixeln entlang des Speers); der Oberkörper
    des Reiters ("oberkoerper", ein Rechteck über dem Sattel) legt sich mit "rumpf" Grad in den
    Stoß, das Pferd steht. Rumpf und Pferd kommen aus dem Bild ohne Speer ("rumpf_bild"), die
    Fäuste ebenso - sie liegen über dem Speer, als umfassten sie ihn."""
    verschoben = lambda p: (p[0] + RAND, p[1] + RAND)   # noqa: E731
    a = lade(AUSGABE / cfg["bild"])
    rumpf = lade(AUSGABE / cfg["rumpf_bild"])
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    deckend = a[:, :, 3] > 8
    # Der Speer als Linien [x1, y1, x2, y2, Dicke] - der Schaft dünn, die Spitze breiter; die
    # erste Linie gibt die Richtung, in der er gleitet
    x1, y1, x2, y2, _ = cfg["speer"][0]
    unten, spitze = verschoben((x1, y1)), verschoben((x2, y2))
    laenge = math.dist(unten, spitze)
    u = ((spitze[0] - unten[0]) / laenge, (spitze[1] - unten[1]) / laenge)
    haende = np.zeros((H, W), bool)
    for cx, cy, rx, ry in cfg["haende"]:
        haende |= ((xx - cx - RAND) / rx) ** 2 + ((yy - cy - RAND) / ry) ** 2 <= 1
    schaft = np.zeros((H, W), bool)
    for x1, y1, x2, y2, dicke in cfg["speer"]:
        schaft |= abstand_strecke(xx, yy, verschoben((x1, y1)), verschoben((x2, y2))) < dicke / 2
    schaft &= deckend & ~haende
    teil_speer = np.zeros_like(a)
    teil_speer[schaft] = a[schaft]
    # Die Faust aus dem Bild ohne Speer - aus dem Standbild käme ein Stück Schaft im alten Winkel mit
    teil_haende = np.zeros_like(rumpf)
    faust = haende & (rumpf[:, :, 3] > 8)
    teil_haende[faust] = rumpf[faust]
    ox0, oy0, ox1, oy1 = (v + RAND for v in cfg["oberkoerper"])
    huefte = verschoben(cfg["huefte"])
    griff = verschoben(cfg["griff"])
    speer_img, haende_img = Image.fromarray(teil_speer), Image.fromarray(teil_haende)

    bilder = []
    for ph in cfg["phasen"]:
        neig, schub = ph.get("rumpf", 0.0), ph.get("schub", 0.0)
        # Oberkörper des Reiters (mit Speer und Händen) und alles andere (Pferd, Beine) getrennt;
        # sie überlappen am Sattel, sonst bliebe beim Neigen eine Naht
        oben = np.zeros_like(rumpf)
        oben[oy0:oy1 + 10, ox0:ox1] = rumpf[oy0:oy1 + 10, ox0:ox1]
        rest = rumpf.copy()
        rest[oy0:oy1 - 10, ox0:ox1] = 0
        reiter = Image.fromarray(oben)
        # Erst um die Faust drehen ("winkel" negativ senkt die Spitze nach vorn), dann entlang
        # der gedrehten Richtung gleiten
        w = ph.get("winkel", 0.0)
        gedreht = speer_img.rotate(w, resample=Image.BICUBIC, center=griff) if w else speer_img
        ziel_u = dreh((griff[0] + u[0], griff[1] + u[1]), griff, w)
        uw = (ziel_u[0] - griff[0], ziel_u[1] - griff[1])
        lage = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        lage.paste(gedreht, (int(round(schub * uw[0])), int(round(schub * uw[1]))), gedreht)
        reiter = Image.alpha_composite(Image.alpha_composite(reiter, lage), haende_img)
        if neig:
            # "rumpf" positiv neigt nach vorn: im Uhrzeigersinn um den Sattel
            reiter = reiter.rotate(-neig, resample=Image.BICUBIC, center=huefte)
        bilder.append(Image.alpha_composite(Image.fromarray(rest), reiter))
    ausgeben(name, cfg, bilder, W, H)


def main():
    alle = json.loads((HIER / "schlag.json").read_text(encoding="utf-8"))
    for name in sys.argv[1:] or list(alle):
        (speer if alle[name].get("modus") == "speer" else figur)(name, alle[name])


if __name__ == "__main__":
    main()
