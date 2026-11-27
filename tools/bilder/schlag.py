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
from PIL import Image, ImageDraw
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
    # "flicken": Rechtecke, die im Rumpf aus dem Standbild kommen - überdeckt kleine Malfehler
    # des neu gemalten Rumpfs, wo das Standbild dort genauso aussähe (etwa am Rücken)
    for x0, y0, x1, y1 in cfg.get("flicken", []):
        rumpf[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND] = a[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND]
    # "radieren": Rechtecke, in denen der Rumpf durchsichtig wird - etwa ein Ärmelstummel, den der
    # neu gemalte Rumpf an der Schulter stehen ließ und der beim erhobenen Arm als Keil absteht;
    # dort liegt in den übrigen Phasen ohnehin das Armstück
    # "radieren_ohne": Ellipsen [cx, cy, rx, ry], die darin stehen bleiben - so endet der Stummel
    # rund statt flach an der Maskenkante
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    bleibt = np.zeros((H, W), bool)
    for cx, cy, rx, ry in cfg.get("radieren_ohne", []):
        bleibt |= ((xx - cx - RAND) / rx) ** 2 + ((yy - cy - RAND) / ry) ** 2 <= 1
    for x0, y0, x1, y1 in cfg.get("radieren", []):
        weg = np.zeros((H, W), bool)
        weg[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND] = True
        rumpf[weg & ~bleibt] = 0
    deckend = a[:, :, 3] > 8
    s, e, h = (verschoben(cfg["arm"][n]) for n in ("schulter", "ellbogen", "hand"))
    # Der Arm: nah am Ober- und Unterarm, dazu die Waffe als Linien; die Schulterkappe bleibt
    ab_ober, ab_unter = abstand_strecke(xx, yy, s, e), abstand_strecke(xx, yy, e, h)
    oberarm = deckend & (ab_ober < cfg["oberarm_breite"])
    unterarm = deckend & (ab_unter < cfg["unterarm_breite"])
    if "arm_differenz" in cfg:
        # "arm_differenz": Schwelle (Summe der Farbabstände) - der Arm ist, was im Standbild
        # deckt und im Rumpfbild fehlt oder anders aussieht. So folgt der Schnitt dem Umriss des
        # Ärmels; das Band um das Skelett schnitt weite Ärmel (Leinen, Polster) gerade ab. Von den
        # Flächen bleiben nur die, die das Band berühren - Rauschen am Körperrand fällt weg
        rgb_a, rgb_r = a[:, :, :3].astype(np.int16), rumpf[:, :, :3].astype(np.int16)
        anders = deckend & ((rumpf[:, :, 3] <= 8) | (np.abs(rgb_a - rgb_r).sum(axis=2) > cfg["arm_differenz"]))
        anders = ndimage.binary_opening(anders, iterations=2)
        flaechen, _ = ndimage.label(anders)
        beruehrt = np.unique(flaechen[(oberarm | unterarm) & anders])
        arm_ganz = np.isin(flaechen, beruehrt[beruehrt > 0])
        unterarm = arm_ganz & (ab_unter < ab_ober)
        oberarm = arm_ganz & ~unterarm
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
    # "im_umriss": die gespiegelte Verlängerung bleibt im Umriss des Standbilds - ein weiter Ärmel
    # ist unten breiter als an der Schulter, gespiegelt stünde seine Ecke beim Schwingen als Spitze ab
    if cfg.get("im_umriss"):
        teil_ober[:schnitt][~deckend[:schnitt]] = 0
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
    # "schaft_fuellen": unter der Faust fehlt dem Schaft seine Textur - gleitet er weit durch die
    # Hand (Speerkämpfer), wanderte die Lücke sichtbar mit. Jedes Pixel des Schafts (erste Linie)
    # unter der Faust nimmt das nächste Schaftpixel dahinter, entgegen der Richtung zur Spitze -
    # mit 14 Pixeln Abstand zur Faust, sonst käme ihr dunkler Umriss als Streifen in den Schaft
    if cfg.get("schaft_fuellen"):
        x1, y1, x2, y2, dicke = cfg["speer"][0]
        offen = haende & (abstand_strecke(xx, yy, unten, spitze) < dicke / 2)
        vorlage = schaft & ~ndimage.binary_dilation(haende, iterations=14)
        for k in range(1, int(2 * max(max(rx, ry) for _, _, rx, ry in cfg["haende"])) + 30):
            ys, xs = np.where(offen)
            if not len(ys):
                break
            qy = np.clip(np.round(ys - k * u[1]).astype(int), 0, H - 1)
            qx = np.clip(np.round(xs - k * u[0]).astype(int), 0, W - 1)
            treffer = vorlage[qy, qx]
            teil_speer[ys[treffer], xs[treffer]] = a[qy[treffer], qx[treffer]]
            offen[ys[treffer], xs[treffer]] = False
    # Die Faust aus dem Bild ohne Speer - aus dem Standbild käme ein Stück Schaft im alten Winkel mit.
    # "faust_aus_standbild": doch aus dem Standbild, wenn das Bild ohne Speer keine Faust mehr zeigt
    # (offene Hand); die Ellipse liegt dann eng um die Faust, die Schaftenden davor und dahinter
    # bleiben draußen und gleiten mit dem Speer
    quelle = a if cfg.get("faust_aus_standbild") else rumpf
    teil_haende = np.zeros_like(rumpf)
    faust = haende & (quelle[:, :, 3] > 8)
    teil_haende[faust] = quelle[faust]
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


def _drehen(teil, um, grad, versatz=(0.0, 0.0)):
    """RGBA-Feld um 'um' (Leinwandpixel) im Uhrzeigersinn drehen, danach verschieben (wie
    teilanim.drehen; eigene Kopie, weil teilanim dieses Modul einbindet)."""
    bild = Image.fromarray(teil)
    r = math.radians(grad)
    c, s = math.cos(r), math.sin(r)
    ux, uy = um
    x0, y0 = ux + versatz[0], uy + versatz[1]
    koeff = (c, s, ux - c * x0 - s * y0, -s, c, uy + s * x0 - c * y0)
    return np.array(bild.transform(bild.size, Image.AFFINE, koeff, resample=Image.BICUBIC))


def oberkoerper_strecken(bild, ob):
    """Den Oberkörper um ob["faktor"] vergrößern, die Beine bleiben: unterhalb der Zeile ob["bis"]
    (Rohbild-Pixel) bleibt alles, darüber wächst der Maßstab über ob["uebergang"] Zeilen von 1 auf
    den Faktor und bleibt dann dort - waagrecht um die Spalte ob["um_x"] (Mitte der Beine), senkrecht
    nach oben. So passt ein neu gemalter, kleiner geratener Oberkörper (Schützen mit gespanntem
    Bogen) wieder auf die Höhe des Standbilds: Kopf und Gürtel springen beim Schuss nicht."""
    k, c, b, ux = ob["faktor"], ob["bis"] + RAND, ob["uebergang"], ob["um_x"] + RAND
    a = np.array(bild).astype(np.float32)
    H, W = a.shape[:2]
    # Maßstab je Quellzeile und die Lage jeder Quellzeile im Ergebnis (von der Zeile c aufwärts summiert)
    ys = np.arange(H, dtype=np.float64)
    s = np.where(ys >= c, 1.0, np.where(ys <= c - b, k, 1.0 + (k - 1.0) * (c - ys) / b))
    y_aus = np.empty(H)
    y_aus[c:] = ys[c:]
    for y in range(c - 1, -1, -1):
        y_aus[y] = y_aus[y + 1] - (s[y] + s[y + 1]) / 2
    # Rückwärts: für jede Zielzeile die Quellzeile (y_aus steigt streng), dazu ihr Maßstab
    y_quelle = np.interp(ys, y_aus, ys, left=-1.0)
    s_quelle = np.interp(y_quelle, ys, s)
    yy, xx = np.mgrid[0:H, 0:W].astype(np.float64)
    qy = np.broadcast_to(y_quelle[:, None], (H, W))
    qx = ux + (xx - ux) / s_quelle[:, None]
    # vormultipliziert abtasten, sonst bekommen die Ränder einen dunklen Saum
    vor = a.copy()
    vor[:, :, :3] *= a[:, :, 3:4] / 255.0
    aus = np.stack([ndimage.map_coordinates(vor[:, :, i], [qy, qx], order=1, cval=0.0) for i in range(4)], axis=2)
    alpha = np.maximum(aus[:, :, 3:4], 1e-3)
    aus[:, :, :3] = np.where(aus[:, :, 3:4] > 0.5, aus[:, :, :3] * 255.0 / alpha, 0.0)
    aus[qy < 0] = 0
    return Image.fromarray(np.clip(aus + 0.5, 0, 255).astype(np.uint8))


def bogen(name, cfg):
    """Bogenschuss (Modus "bogen"): die Zughand zieht die Sehne bis an die Wange, der Pfeil fliegt.

    "bild" zeigt den Schützen mit voll gespanntem Bogen, "rumpf_bild" dasselbe Bild ohne Zugarm,
    Sehne und Pfeil (qwen_image.py mit vorfuellen). Der Zugarm ist, was im Standbild deckt und im
    Rumpf fehlt oder anders aussieht ("arm_differenz"), nah am Skelett "arm" (schulter, ellbogen,
    hand) - Oberarm und Unterarm samt Faust. Ohne Pfeil und Sehne: was auf der Pfeillinie rechts
    der Nocke liegt und was auf den Sehnenlinien außerhalb der Faust ("faust", Ellipse) liegt.

    Je Phase "zug" (0 = Sehne in Ruhe, 1 = voll gespannt): die Nocke liegt auf der Pfeillinie
    zwischen der geraden Sehne (Bogenspitzen "sehne" oben/unten) und ihrer Lage im Standbild, die
    Faust geht mit ihr, der Ellbogen folgt aus beiden Knochenlängen und bleibt auf derselben Seite
    wie im Standbild. "pfeil": false lässt den Pfeil weg; "sehne": "ruhe" spannt die Sehne gerade
    statt zur Nocke (nach dem Schuss); "hand_versatz" [dx, dy] schiebt die Faust zusätzlich (sie
    schnellt beim Loslassen zurück). Der Pfeil wird aus dem Standbild geschnitten ("pfeil": Linie
    von der Nocke zur Spitze und Dicke), Lücken im Schaft (unter der Bogenhand) mit der nächsten
    Spalte links gefüllt; die Bogenhand ("bogenhand", Ellipsen aus dem Rumpf) liegt über dem Pfeil.
    Schichten: Rumpf, Sehne, Pfeil, Bogenhand, Oberarm, Unterarm."""
    verschoben = lambda p: (p[0] + RAND, p[1] + RAND)   # noqa: E731
    a = lade(AUSGABE / cfg["bild"])
    rumpf = lade(AUSGABE / cfg["rumpf_bild"])
    H, W = a.shape[:2]
    yy, xx = np.mgrid[0:H, 0:W]
    deckend = a[:, :, 3] > 8
    # Den Rumpf ausbessern: "rumpf_weg" (Linien [x1, y1, x2, y2, Dicke]) macht durchsichtig, was das
    # Inpainting über dem Hintergrund stehen ließ (ein neu gemalter Pfeil, Sehnenreste); ein sechster
    # Wert ist eine Helligkeitsschwelle - dann geht nur, was heller ist (kleinster Farbkanal darüber):
    # so verschwindet eine helle Sehne vor der Kapuze, ohne die dunkle Kapuzenkante anzuschneiden;
    # "rumpf_weg_rechtecke" [x0, y0, x1, y1] macht ganze Rechtecke durchsichtig (Pfeilspitze vor dem
    # Bogen); "rumpf_kopie" [x0, y0, x1, y1, dx, dy] holt das Rechteck aus dem um (dx, dy)
    # verschobenen Ausschnitt desselben Rumpfs (Pfeilfedern an der Wange: Schal von weiter unten);
    # "rumpf_flicken_hell" [x0, y0, x1, y1, Schwelle] füllt helle Reste (Pfeilfedern vor der Wange)
    # mit der Farbe des nächsten dunkleren Pixels im Rechteck
    for x1, y1, x2, y2, dicke, *hell in cfg.get("rumpf_weg", []):
        ab = abstand_strecke(xx, yy, (x1 + RAND, y1 + RAND), (x2 + RAND, y2 + RAND))
        weg = ab < dicke / 2
        if hell and hell[0] > 0:
            weg &= rumpf[:, :, :3].min(axis=2) > hell[0]
        elif hell:
            # Schwelle 0: dazu die halbdurchsichtigen Kantenpixel bis zur doppelten Dicke (eine Sehne
            # über dem Hintergrund ließe sonst eine blasse Spur stehen)
            weg |= (ab < dicke) & (rumpf[:, :, 3] < 250)
        rumpf[weg, 3] = 0
    # "rumpf_fuellen" [x1, y1, x2, y2, Dicke, Schwelle]: was auf der Linie liegt (bei Schwelle > 0 nur
    # Helleres, wie bei rumpf_weg), bekommt Farbe und Deckkraft des nächsten Pixels neben der Linie -
    # über dem Hintergrund wird es durchsichtig, über dem Kittel blau (eine Sehne quer über die Brust)
    for x1, y1, x2, y2, dicke, hell in cfg.get("rumpf_fuellen", []):
        ab = abstand_strecke(xx, yy, (x1 + RAND, y1 + RAND), (x2 + RAND, y2 + RAND))
        loch = ab < dicke / 2
        if hell:
            loch &= rumpf[:, :, :3].min(axis=2) > hell
        else:
            # halbdurchsichtige Kantenpixel der Sehne (bis zur doppelten Dicke) gehören mit ins Loch -
            # sonst würden genau sie als nächste Nachbarn in die Linie kopiert (eine Punktreihe)
            loch |= (ab < dicke) & (rumpf[:, :, 3] < 250)
        if loch.any():
            _, (ny, nx) = ndimage.distance_transform_edt(loch, return_indices=True)
            rumpf[loch] = rumpf[ny[loch], nx[loch]]
    for x0, y0, x1, y1 in cfg.get("rumpf_weg_rechtecke", []):
        rumpf[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND, 3] = 0
    for x0, y0, x1, y1, dx, dy, *grau in cfg.get("rumpf_kopie", []):
        ziel = rumpf[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND]
        quelle = rumpf[y0 + dy + RAND:y1 + dy + RAND, x0 + dx + RAND:x1 + dx + RAND].copy()
        # ein siebter Wert: nur graue Pixel ersetzen (Farbspanne darunter) - Federn und Schaft,
        # nicht Kinn und Schal daneben; so folgt die Kante dem Bild statt dem Rechteck
        nur = np.ptp(ziel[:, :, :3].astype(np.int16), axis=2) < grau[0] if grau else np.ones(ziel.shape[:2], bool)
        ziel[nur] = quelle[nur]
    for x0, y0, x1, y1, schwelle in cfg.get("rumpf_flicken_hell", []):
        aus = rumpf[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND]
        hell = aus[:, :, :3].min(axis=2) > schwelle
        if hell.any() and (~hell).any():
            _, (ny, nx) = ndimage.distance_transform_edt(hell, return_indices=True)
            aus[hell] = aus[ny[hell], nx[hell]]

    def ellipsen(liste):
        m = np.zeros((H, W), bool)
        for cx, cy, rx, ry in liste:
            m |= ((xx - cx - RAND) / rx) ** 2 + ((yy - cy - RAND) / ry) ** 2 <= 1
        return m

    S, E0, H0 = (np.array(verschoben(cfg["arm"][n]), float) for n in ("schulter", "ellbogen", "hand"))
    N0 = np.array(verschoben(cfg["nocke"]), float)
    px1, py1, px2, py2, pdicke = cfg["pfeil"]
    P1, P2 = np.array(verschoben((px1, py1)), float), np.array(verschoben((px2, py2)), float)
    oben, unten = (np.array(verschoben(cfg["sehne"][n]), float) for n in ("oben", "unten"))

    # Was zum Zugarm gehört: anders als im Rumpf und nah am Skelett
    rgb_a, rgb_r = a[:, :, :3].astype(np.int16), rumpf[:, :, :3].astype(np.int16)
    anders = deckend & ((rumpf[:, :, 3] <= 8) | (np.abs(rgb_a - rgb_r).sum(axis=2) > cfg["arm_differenz"]))
    faust = ellipsen([cfg["faust"]])
    auf_pfeil = (abstand_strecke(xx, yy, P1, P2) < pdicke / 2) & (xx > N0[0] + 2)
    auf_sehne = ((abstand_strecke(xx, yy, oben, N0) < 6) | (abstand_strecke(xx, yy, unten, N0) < 6)) & ~faust
    anders_arm = ndimage.binary_opening(anders & ~auf_pfeil & ~auf_sehne, iterations=2)
    ab_ober, ab_unter = abstand_strecke(xx, yy, S, E0), abstand_strecke(xx, yy, E0, H0)
    band = (ab_ober < cfg["oberarm_breite"]) | (ab_unter < cfg["unterarm_breite"]) | faust
    for x0, y0, x1, y1 in cfg.get("nicht_arm", []):
        band[y0 + RAND:y1 + RAND, x0 + RAND:x1 + RAND] = False
    flaechen, _ = ndimage.label(anders_arm)
    beruehrt = np.unique(flaechen[band & anders_arm])
    arm = np.isin(flaechen, beruehrt[beruehrt > 0]) & band
    # "arm_ohne_bunt": deutlich blaue oder grüne Pixel (ein Kanal um mehr als den Wert über den
    # beiden anderen) gehören nicht zum Zugarm - Ärmelrand und Schal, die am Ellbogen mitgeschnitten
    # werden und beim Strecken als Krümel neben dem Gelenk liegen; dunkle Umrisse bleiben
    if "arm_ohne_bunt" in cfg:
        r_, g_, b_ = (a[:, :, i].astype(np.int16) for i in range(3))
        grenze = cfg["arm_ohne_bunt"]
        arm &= ~(((b_ - np.maximum(r_, g_)) > grenze) | ((g_ - np.maximum(r_, b_)) > grenze))
    unterarm = arm & ((ab_unter < ab_ober) | faust)
    oberarm = arm & ~unterarm
    # Gelenkscheibe am Ellbogen im Unterarm - so bleibt das Gelenk bei jedem Winkel geschlossen
    unterarm |= arm & (np.hypot(xx - E0[0], yy - E0[1]) < cfg["unterarm_breite"] * 0.8)

    def schicht(maske, quelle=a):
        t = quelle.copy()          # Farbe auch außerhalb (lade hat sie ausgemalt): sauberes Drehen
        t[~maske, 3] = 0
        return t

    # "ohne_oberarm": der Oberarm kommt ganz aus dem Rumpf (liegt er unter Umhang oder Schal, sind
    # vom Zugarm dort nur verstreute Fetzen anders - gedreht lägen sie als Krümel unter dem Unterarm)
    if cfg.get("ohne_oberarm"):
        oberarm[:] = False
    teil_ober, teil_unter = schicht(oberarm), schicht(unterarm)
    # Der Pfeil: auf der Linie, anders als im Rumpf, ohne die Bogenhand; Lücken im Schaft füllt die
    # nächste Spalte links (der Pfeil liegt fast waagrecht)
    bogenhand = ellipsen(cfg.get("bogenhand", []))
    pfeil = deckend & (abstand_strecke(xx, yy, P1, P2) < pdicke / 2) & anders & ~bogenhand & (xx >= N0[0] - 4)
    teil_pfeil = schicht(pfeil)
    spalten = np.nonzero(pfeil.any(axis=0))[0]
    if len(spalten):
        y_lo, y_hi = int(min(P1[1], P2[1]) - pdicke), int(max(P1[1], P2[1]) + pdicke)
        letzte = None
        for x in range(int(spalten.min()), int(spalten.max()) + 1):
            if pfeil[y_lo:y_hi, x].any():
                letzte = x
            elif letzte is not None:
                teil_pfeil[y_lo:y_hi, x] = teil_pfeil[y_lo:y_hi, letzte]
    teil_bogenhand = schicht(bogenhand & (rumpf[:, :, 3] > 8), rumpf)

    # Nocke in Ruhe: Schnitt der Pfeillinie mit der geraden Sehne
    d_pf = (P2 - P1) / np.linalg.norm(P2 - P1)
    d_se = unten - oben
    # N0 + t*d_pf = oben + u*d_se
    mat = np.array([[d_pf[0], -d_se[0]], [d_pf[1], -d_se[1]]])
    t_ruhe = np.linalg.solve(mat, oben - N0)[0]
    N_ruhe = N0 + t_ruhe * d_pf
    L1, L2 = np.linalg.norm(E0 - S), np.linalg.norm(H0 - E0)
    def kreuz(u, v):
        return u[0] * v[1] - u[1] * v[0]

    seite = np.sign(kreuz(H0 - S, E0 - S))

    def winkel(v):
        return math.degrees(math.atan2(v[1], v[0]))

    sf = cfg.get("sehne_farbe", [[52, 38, 28], [146, 116, 82]])
    bilder = []
    for ph in cfg["phasen"]:
        N = N_ruhe + ph["zug"] * (N0 - N_ruhe)
        Hn = N + (H0 - N0) + np.array(ph.get("hand_versatz", (0, 0)), float)
        # Reicht der gestreckte Arm nicht bis zur Hand (Sehne in Ruhe beim stark gebogenen Bogen),
        # schiebt sich die Schulter um den Rest vor; der Oberarm geht mit
        d = np.linalg.norm(Hn - S)
        Sp = S + max(0.0, d - (L1 + L2 - 1.0)) * (Hn - S) / d
        d = np.linalg.norm(Hn - Sp)
        ach = (Hn - Sp) / d
        aa = (L1 ** 2 - L2 ** 2 + d ** 2) / (2 * d)
        hh = math.sqrt(max(L1 ** 2 - aa ** 2, 0.0))
        senk = np.array([-ach[1], ach[0]])
        E = Sp + aa * ach + hh * senk
        if np.sign(kreuz(Hn - Sp, E - Sp)) != seite:
            E = Sp + aa * ach - hh * senk
        lage_ober = _drehen(teil_ober, S, winkel(E - Sp) - winkel(E0 - S), Sp - S)
        lage_unter = _drehen(teil_unter, E0, winkel(Hn - E) - winkel(H0 - E0), E - E0)

        bild = Image.fromarray(rumpf.copy())
        sehne = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        zeich = ImageDraw.Draw(sehne)
        punkte = [tuple(oben), tuple(unten)] if ph.get("sehne") == "ruhe" else [tuple(oben), tuple(N), tuple(unten)]
        zeich.line(punkte, fill=tuple(sf[0]) + (255,), width=cfg.get("sehne_dicke", 6), joint="curve")
        zeich.line(punkte, fill=tuple(sf[1]) + (255,), width=max(1, cfg.get("sehne_dicke", 6) // 2), joint="curve")
        bild = Image.alpha_composite(bild, sehne)
        if ph.get("pfeil", True):
            v = N - N0
            lage_pfeil = np.roll(np.roll(teil_pfeil, int(round(v[1])), axis=0), int(round(v[0])), axis=1)
            bild = Image.alpha_composite(bild, Image.fromarray(lage_pfeil))
        bild = Image.alpha_composite(bild, Image.fromarray(teil_bogenhand))
        bild = Image.alpha_composite(bild, Image.fromarray(lage_ober))
        bild = Image.alpha_composite(bild, Image.fromarray(lage_unter))
        if "oberkoerper_strecken" in cfg:
            bild = oberkoerper_strecken(bild, cfg["oberkoerper_strecken"])
        bilder.append(bild)

    # Kontrollbild: Oberarm rot, Unterarm grün, Pfeil gelb, Bogenhand blau
    probe = a.copy()
    for m, farbe in ((oberarm, (255, 0, 0)), (unterarm, (0, 255, 0)), (pfeil, (255, 230, 0)), (bogenhand, (0, 90, 255))):
        probe[m, :3] = (0.45 * probe[m, :3] + 0.55 * np.array(farbe)).astype(np.uint8)
    (AUSGABE / "schlag").mkdir(parents=True, exist_ok=True)
    Image.fromarray(probe[RAND:H - RAND, RAND:W - RAND]).save(AUSGABE / "schlag" / f"{name}_teile.png")
    ausgeben(name, cfg, bilder, W, H)


def main():
    alle = json.loads((HIER / "schlag.json").read_text(encoding="utf-8"))
    modi = {"speer": speer, "bogen": bogen}
    for name in sys.argv[1:] or list(alle):
        modi.get(alle[name].get("modus"), figur)(name, alle[name])


if __name__ == "__main__":
    main()
