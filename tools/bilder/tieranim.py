"""Bär und Wolf in Bewegung (RT): je acht Lauf- und Angriffsphasen aus Stand- und Angriffsbild.

Wie gang.py (C4m) stellt das Skript die Beine eines Standbilds über ein Skelett neu, aber für
Tiere mit Fell: statt starrer Glieder, die an Knie und Knöchel mit harten Schnitten aneinander
stoßen, wird jedes Bein weich gebogen - jeder Bildpunkt folgt allen Knochen seines Beins
anteilig nach seinem Abstand zu ihnen (Oberschenkel, Unterschenkel, Pfote), und oben geht das
Bein über "uebergang" Pixel stufenlos in den Rumpf über. So knicken Fell und Umriss an keinem
Gelenk. Die Beine der abgewandten Seite ("hinten": true) liegen hinter dem Rumpf, die
zugewandten davor; wo ein zugewandtes Bein im Bild ein abgewandtes verdeckt, wird das abgewandte
auf Wunsch ("ergaenzen": true) aus seinen eigenen Pixeln ergänzt - sonst klaffte dort ein Loch,
sobald die Beine auseinandergehen.

Gang: die Fußbahnen kommen aus gang.Bein (der Standfuß ruht und wandert relativ zur Hüfte nach
hinten, der Schwungfuß zieht im Bogen nach vorn; "versatz" je Bein gibt die Gangart - Trab beim
Wolf, Viertakt beim Bären). Der Körper bleibt in Standhöhe, solange die tragenden Standbeine
hinreichen, wippt "wippen" Pixel im Schritt und nickt auf Wunsch leicht ("nicken", Grad).

Angriff: acht Schlüsselposen in tieranim.json. Je Phase die Quelle - das Standbild oder das
Angriffsbild aus qwen_image.py (Gruppe tiere_lauf: Maul offen, Pranke erhoben, vorgestreckt) -,
Verschiebung und Neigung des Körpers ("koerper": [dx, dy, grad], gedreht um "um"; + hebt die
Vorderseite) und je Bein: "steht" (der Fuß bleibt, wo er im Bild steht, das Bein folgt per
Zwei-Knochen-Rechnung), {"fuss": [dx, dy, grad]} (der Fuß dorthin versetzt und geneigt) oder
{"mit": [oberschenkel, unterschenkel, pfote]} (Winkel relativ zum Körper, jeder zum vorigen
dazu). Im Angriffsbild stehen manche Beine anders als im Standbild - "angriff_beine" ersetzt
deren Gelenke (null: wie im Standbild; "fehlt": true - dort kein eigenes Bein, etwa die erhobene
Pranke des Bären: sie bleibt beim Rumpf und schlägt mit dessen Neigung zu).

Die Laufbilder bleiben auf der 1024er Leinwand des Standbilds - uebernehmen.ps1 schneidet sie mit
dem Standbild auf ein gemeinsames Rechteck zu (Figur). Die Angriffsbilder ragen beim Sprung und
Aufrichten darüber hinaus und bekommen wie bei schlag.py eine größere Leinwand im selben Maßstab:
dieses Rechteck seitlich gleich weit und nach oben erweitert, die Unterkante bleibt - die Pfoten
stehen also an derselben Stelle. Zwei fast unsichtbare Eckpixel (Deckkraft 9) legen es fest; die
passende Zielbreite gibt das Skript aus (bilder.json, Gruppe tieranim).

Gelenke in Pixeln des freigestellten Rohbilds (1024 x 1024). Ergebnis je Figur unter
ausgabe/tieranim/: <figur>_lauf<N>_1.png und <figur>_angriff<N>_1.png (Namensschema für
uebernehmen.ps1, Seed 1), Kontaktbögen <figur>_lauf_blatt.png und <figur>_angriff_blatt.png
und <figur>/teile_<quelle>.png (welcher Bildpunkt zu welchem Bein gehört - zum Prüfen der Gelenke).

Braucht numpy, scipy und Pillow - die Python von ComfyUI hat alles:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/tieranim.py [figur ...]
"""
import json
import math
import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw
from scipy import ndimage

HIER = Path(__file__).resolve().parent
sys.path.insert(0, str(HIER))
import gang  # noqa: E402  - Bein, GANG, winkel, abstand_strecke

AUSGABE = HIER / "ausgabe"
ZIEL = AUSGABE / "tieranim"
RAND = 220          # Platz rund um das 1024er Bild für Sprung und Aufrichten
GROESSE = 1024
DICHTE = 3          # je Bildpunkt DICHTE x DICHTE Proben beim Biegen - so entstehen keine Lücken
ZIELBREITE = 256    # Breite des Standbilds im Spiel (bilder.json, Gruppe tiere)
FARBEN = [(255, 0, 255), (0, 220, 255), (255, 200, 0), (0, 255, 0), (255, 90, 90)]


def dreh_matrix(grad):
    """Wie gang.dreh als Matrix: ein nach unten zeigender Vektor schwenkt um +grad nach vorn."""
    r = math.radians(grad)
    c, s = math.cos(r), math.sin(r)
    return np.array([[c, s], [-s, c]])


def verschoben(p):
    return (p[0] + RAND, p[1] + RAND)


def lade(pfad):
    """Bild auf die größere Leinwand; nicht ganz deckende Pixel in der Farbe des nächsten
    deckenden - sonst zögen Drehen und Biegen helle Säume aus dem Hintergrund mit."""
    bild = np.array(Image.open(AUSGABE / pfad).convert("RGBA"))
    a = np.zeros((GROESSE + 2 * RAND, GROESSE + 2 * RAND, 4), np.uint8)
    a[RAND:RAND + GROESSE, RAND:RAND + GROESSE] = bild
    halb = a[:, :, 3] < 200
    _, (ny, nx) = ndimage.distance_transform_edt(halb, return_indices=True)
    a[halb, :3] = a[ny[halb], nx[halb], :3]
    return a


def vieleck(punkte, form):
    """Maske eines Vielecks (Punkte in Rohbild-Pixeln)."""
    m = Image.new("L", (form[1], form[0]), 0)
    ImageDraw.Draw(m).polygon([verschoben(p) for p in punkte], fill=255)
    return np.array(m) > 0


class Quelle:
    """Ein Bild (Stand oder Angriff) mit seinem Skelett, zerlegt in Rumpf und Beine. Die Proben
    der Beine samt ihren Knochengewichten hängen nicht an der Pose und werden einmal gerechnet."""

    def __init__(self, name, a, beine_cfg, cfg):
        self.name, self.a = name, a
        H, W = a.shape[:2]
        yy, xx = np.mgrid[0:H, 0:W]
        deckend = a[:, :, 3] > 8
        gangart = dict(gang.GANG, **cfg.get("gang", {}))
        self.cfg = []
        for c in beine_cfg:
            c = dict(c)
            for n in ("huefte", "knie", "knoechel", "zehe"):
                c[n] = verschoben(c[n])
            c["saum"] = c["saum"] + RAND
            if "boden" in c:
                c["boden"] += RAND
            elif c.get("fehlt"):
                c["boden"] = c["knoechel"][1]
            self.cfg.append(c)
        self.beine = [gang.Bein(c, cfg["schritt"], dict(gangart, **c.get("gang", {})), c["saum"], deckend)
                      for c in self.cfg]
        # Zu welcher Seite das mittlere Gelenk knickt, steht im Bild: die Lösung der Zwei-Knochen-
        # Rechnung, die das Knie des Bilds wiedergibt - die andere klappte schon im Stand um
        for b, c in zip(self.beine, self.cfg):
            # Tierbeine stehen im Bild fast gestreckt: setzte der Fuß mitten im Stand dort auf, wo ihn
            # das Bild zeigt, reichte das Bein vorn nicht mehr zum Boden und der Rumpf müsste tief
            # sinken. "mitte_versatz" (Pixel, meist negativ) schiebt den Stand dafür nach hinten
            b.mitte += c.get("mitte_versatz", cfg.get("mitte_versatz", 0.0))
            b.knick_hinten = False
            vorn = math.dist(b.knie(b.h, b.a), b.k)
            b.knick_hinten = True
            b.knick_hinten = math.dist(b.knie(b.h, b.a), b.k) < vorn

        # Welches Bein: der nächste Knochen entscheidet, nur unter dem Saum des Beins und nicht
        # weiter als "breite" vom Knochen. Vielecke ("maske") weisen Pixel fest einem Bein zu,
        # "rumpf" fest dem Rumpf
        n = len(self.beine)
        knochen = [[gang.abstand_strecke(xx, yy, p, q).astype(np.float32) for p, q in ((b.h, b.k), (b.k, b.a), (b.a, b.z))]
                   for b in self.beine]
        # Ein Bein mit "fehlt" gibt es in diesem Bild nicht als Bein - etwa die erhobene Pranke des
        # Bären im Angriffsbild: sie bleibt beim Rumpf und schlägt mit seiner Neigung zu
        self.abstand = [np.full((H, W), np.inf, np.float32) if c.get("fehlt") else np.minimum.reduce(k)
                        for k, c in zip(knochen, self.cfg)]
        frei = np.full((n, H, W), np.inf, np.float32)
        for i, (b, c) in enumerate(zip(self.beine, self.cfg)):
            if c.get("fehlt"):
                continue
            ok = deckend & (yy >= b.saum) & (self.abstand[i] < c.get("breite", cfg.get("breite", 60)))
            frei[i][ok] = self.abstand[i][ok]
            for punkte in c.get("maske", []):
                frei[i][vieleck(punkte, (H, W)) & deckend] = -1.0
        for punkte in cfg.get("rumpf", {}).get(name, []):
            frei[:, vieleck(punkte, (H, W))] = np.inf
        bester = frei.argmin(axis=0)
        bein_von = np.where(np.isfinite(frei.min(axis=0)), bester, -1)

        # Was vom Rumpf abgetrennt unter einem Saum liegt (eine Kralle, ein Fellbüschel zwischen
        # den Beinen), schwebte sonst stehen - es gehört zum nächsten Bein
        rumpf = deckend & (bein_von < 0)
        teile, anzahl = ndimage.label(rumpf)
        if anzahl > 1:
            groessen = ndimage.sum(rumpf, teile, range(1, anzahl + 1))
            haupt = 1 + int(np.argmax(groessen))
            unter = rumpf & (teile != haupt) & (yy >= min(b.saum for b in self.beine))
            bein_von[unter] = np.stack(self.abstand)[:, unter].argmin(axis=0)
        # Umgekehrt: Bruchstücke eines Beins, die nicht an ihm hängen, gehören zum Rumpf
        for i in range(n):
            mein = bein_von == i
            teile, anzahl = ndimage.label(mein)
            if anzahl > 1:
                groessen = ndimage.sum(mein, teile, range(1, anzahl + 1))
                haupt = 1 + int(np.argmax(groessen))
                klein = mein & (teile != haupt)
                klein &= np.isin(teile, 1 + np.where(groessen < cfg.get("splitter", 400))[0])
                bein_von[klein] = -1
        self.bein_von = bein_von

        # Abgewandte Beine ergänzen: was ein zugewandtes Bein im Bild von ihnen verdeckt, aus den
        # nächsten eigenen Pixeln, in der Breite des Beins um seine Knochen
        farbe = a.copy()
        self.masken = []
        naechstes = np.stack(self.abstand).argmin(axis=0)
        for i, (b, c) in enumerate(zip(self.beine, self.cfg)):
            # Ein paar Zeilen über dem Saum gehören dem Bein zusätzlich (sie bleiben auch im Rumpf und
            # bewegen sich dort wie er) - sonst blitzte am Saum eine Haarlinie Hintergrund auf
            kragen = (deckend & (bein_von < 0) & (naechstes == i) & (yy >= b.saum - 4) & (yy < b.saum)
                      & (self.abstand[i] < c.get("breite", cfg.get("breite", 60))))
            mein = (bein_von == i) | kragen
            if c.get("hinten") and c.get("ergaenzen") and mein.any():
                halbbreite = np.percentile(self.abstand[i][mein], 90)
                vorn = np.isin(bein_von, [j for j, cj in enumerate(self.cfg) if not cj.get("hinten")])
                loch = vorn & (self.abstand[i] < halbbreite) & (yy >= b.saum)
                if loch.any():
                    _, (ny, nx) = ndimage.distance_transform_edt(~mein, return_indices=True)
                    eigen = np.zeros_like(a)
                    eigen[mein] = a[mein]
                    eigen[loch] = a[ny[loch], nx[loch]]
                    eigen[loch, 3] = 255
                    mein = mein | loch
                    self.masken.append((mein, eigen))
                    continue
            self.masken.append((mein, farbe))

        # Proben je Bein: DICHTE x DICHTE je Pixel, Farbe vormultipliziert, und wie weit jede Probe
        # den Drehungen an Hüfte, Knie und Knöchel folgt (0 bis 1, weich über das Gelenk)
        versatz = (np.arange(DICHTE) + 0.5) / DICHTE - 0.5
        ox, oy = np.meshgrid(versatz, versatz)
        ox, oy = ox.ravel(), oy.ravel()
        self.proben = []
        for i, (b, c) in enumerate(zip(self.beine, self.cfg)):
            mein, quelle = self.masken[i]
            py, px = np.nonzero(mein)
            f = quelle[py, px].astype(np.float32)
            f[:, :3] *= f[:, 3:4] / 255.0
            def stufe(s, r):
                s = np.clip((s + r) / (2 * r), 0.0, 1.0)
                return s * s * (3 - 2 * s)

            def einheit(p, q):
                v = np.array(q, float) - np.array(p, float)
                return v / np.linalg.norm(v)

            def nach_winkel(gelenk, oben, unten):
                # Anteil nach der Richtung vom Gelenk aus: 0 zum oberen Knochen hin, 1 zum unteren,
                # weich dazwischen. Eine Grenze als Linie zöge Bildpunkte weit vom Gelenk über einen
                # schmalen Streifen auseinander; nach dem Winkel dehnt sich alles gleich
                rx, ry = px - gelenk[0], py - gelenk[1]
                lang = np.maximum(np.hypot(rx, ry), 1e-6)
                zu = [np.arccos(np.clip((rx * e[0] + ry * e[1]) / lang, -1.0, 1.0))
                      for e in (einheit(gelenk, oben), einheit(gelenk, unten))]
                anteil = zu[0] / np.maximum(zu[0] + zu[1], 1e-6)
                return stufe(anteil - 0.5, band)

            band = c.get("band", cfg.get("band", 0.4))
            # Hüfte: unter dem Saum über "uebergang" Pixel; Knie und Knöchel nach dem Winkel
            fh = stufe(py - b.saum - c.get("uebergang", cfg.get("uebergang", 60)) / 2,
                       c.get("uebergang", cfg.get("uebergang", 60)) / 2)
            fk = nach_winkel(b.k, b.h, b.a)
            # Was im Bild unter dem Knöchel liegt (Ballen, Krallen), gehört ganz zur Pfote - sonst zöge
            # der Unterschenkel es beim Beugen mit und drückte die Krallen in den Boden
            fa = np.maximum(nach_winkel(b.a, b.k, b.z), stufe(py - b.a[1] - 12, 10))
            fk = np.maximum(fk, fa)
            fh = np.maximum(fh, fk)
            anteil = np.stack([fh, fk, fa], axis=1)
            pos = np.stack([(px[:, None] + ox[None, :]).ravel(), (py[:, None] + oy[None, :]).ravel()], axis=1)
            n = DICHTE * DICHTE
            self.proben.append((pos, np.repeat(f, n, axis=0), np.repeat(anteil, n, axis=0)))

        rumpfbild = a.copy()
        rumpfbild[bein_von >= 0] = 0
        rumpfbild[bein_von >= 0, :3] = a[bein_von >= 0, :3]
        self.rumpf = Image.fromarray(rumpfbild)

    def teile_bild(self):
        """Prüfbild: Rumpf grau, jedes Bein in seiner Farbe, Gelenke als Linien."""
        H, W = self.bein_von.shape
        t = np.full((H, W, 3), 235, np.uint8)
        t[self.a[:, :, 3] > 8] = (120, 120, 120)
        for i, (mein, _) in enumerate(self.masken):
            t[mein] = FARBEN[i % len(FARBEN)]
        bild = Image.fromarray(t)
        z = ImageDraw.Draw(bild)
        for i, b in enumerate(self.beine):
            z.line([b.h, b.k, b.a, b.z], fill=(0, 0, 0), width=3)
            z.line([(0, b.saum), (W, b.saum)], fill=FARBEN[i % len(FARBEN)], width=1)
            z.text((b.h[0] + 6, b.h[1] - 14), f"B{i}", fill=(0, 0, 0))
        return bild.crop((RAND, RAND, RAND + GROESSE, RAND + GROESSE))

    def stelle(self, koerper, lagen):
        """Das Bild in einer Pose. koerper = (dx, dy, grad, um); lagen = je Bein die neuen
        Gelenke (Hüfte, Knie, Knöchel, Zehe)."""
        H, W = self.bein_von.shape
        dx, dy, grad, um = koerper
        R = dreh_matrix(grad)
        um = np.array(um, float)
        t = np.array([dx, dy], float)

        def rumpf_ab(p):
            return R @ (np.array(p, float) - um) + um + t

        # Rumpf: affin um "um" gedreht und verschoben (Pillow will die Umkehrung)
        Ri = np.linalg.inv(R)
        off = um - Ri @ (um + t)
        rumpf = self.rumpf.transform((W, H), Image.AFFINE, (Ri[0, 0], Ri[0, 1], off[0], Ri[1, 0], Ri[1, 1], off[1]),
                                     resample=Image.BICUBIC)
        hinten, vorn = Image.new("RGBA", (W, H)), Image.new("RGBA", (W, H))
        def drehe(v, um_punkt, grad_je):
            # v um um_punkt (je Probe) um grad_je (je Probe) drehen, wie gang.dreh
            r = np.radians(grad_je)[:, None]
            rel = v - um_punkt
            return np.concatenate([rel[:, :1] * np.cos(r) + rel[:, 1:] * np.sin(r),
                                   -rel[:, :1] * np.sin(r) + rel[:, 1:] * np.cos(r)], axis=1) + um_punkt

        for i, b in enumerate(self.beine):
            pos, farbe, anteil = self.proben[i]
            h2, k2, a2, z2 = (np.array(p, float) for p in lagen[i])
            # Drehung jedes Knochens gegenüber dem Bild, dann je Gelenk die Drehung des unteren
            # gegenüber dem oberen Knochen
            dreh = [grad] + [gang.winkel(p2, q2) - gang.winkel(p, q)
                             for (p, q), (p2, q2) in (((b.h, b.k), (h2, k2)), ((b.k, b.a), (k2, a2)),
                                                      ((b.a, b.z), (a2, z2)))]
            gelenk_dreh = [(dreh[j + 1] - dreh[j] + 180.0) % 360.0 - 180.0 for j in range(3)]
            # Wie eine Gliederkette: erst mit dem Rumpf, dann um Hüfte, Knie und Knöchel, jedes Mal nur
            # so weit, wie die Probe dem Gelenk folgt. Folgt sie allen ganz, liegt sie genau wie ihr
            # Knochen; dazwischen biegt sich das Bein, ohne dünner zu werden
            ziel = (pos - um) @ R.T + um + t
            knie = np.tile(rumpf_ab(b.k), (len(pos), 1))
            knoechel = np.tile(rumpf_ab(b.a), (len(pos), 1))
            huefte = np.tile(h2, (len(pos), 1))
            w = anteil[:, 0] * gelenk_dreh[0]
            ziel, knie, knoechel = drehe(ziel, huefte, w), drehe(knie, huefte, w), drehe(knoechel, huefte, w)
            w = anteil[:, 1] * gelenk_dreh[1]
            ziel, knoechel = drehe(ziel, knie, w), drehe(knoechel, knie, w)
            ziel = drehe(ziel, knoechel, anteil[:, 2] * gelenk_dreh[2])
            lage = Image.fromarray(verteile(ziel, farbe, H, W))
            if self.cfg[i].get("hinten"):
                hinten = Image.alpha_composite(hinten, lage)
            else:
                vorn = Image.alpha_composite(vorn, lage)
        bild = np.array(Image.alpha_composite(Image.alpha_composite(hinten, rumpf), vorn))
        # Einzelne verirrte Pixel (wo sich das Fell an einem stark gebeugten Gelenk faltet) weg
        teile, anzahl = ndimage.label(bild[:, :, 3] > 8)
        if anzahl > 1:
            groessen = ndimage.sum(np.ones_like(teile), teile, range(1, anzahl + 1))
            bild[np.isin(teile, 1 + np.where(groessen < 40)[0]), 3] = 0
        return Image.fromarray(bild), rumpf_ab


def verteile(pos, farbe, H, W):
    """Proben bilinear auf das Pixelraster verteilen und mitteln (vormultipliziert); die
    Deckkraft am Rand ergibt sich aus der Dichte der Proben."""
    x, y = pos[:, 0], pos[:, 1]
    x0, y0 = np.floor(x).astype(np.int64), np.floor(y).astype(np.int64)
    fx, fy = x - x0, y - y0
    summe = np.zeros(H * W)
    acc = np.zeros((4, H * W))
    for ddx, ddy, w in ((0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)), (0, 1, (1 - fx) * fy), (1, 1, fx * fy)):
        xi, yi = x0 + ddx, y0 + ddy
        ok = (xi >= 0) & (xi < W) & (yi >= 0) & (yi < H)
        idx = yi[ok] * W + xi[ok]
        summe += np.bincount(idx, w[ok], H * W)
        for c in range(4):
            acc[c] += np.bincount(idx, w[ok] * farbe[ok, c], H * W)
    mit = summe > 1e-6
    deck = np.clip(summe / (0.3 * DICHTE * DICHTE), 0.0, 1.0)
    out = np.zeros((H * W, 4))
    out[mit] = (acc[:, mit] / summe[mit]).T
    out[:, 3] *= deck
    out[:, :3] *= deck[:, None]
    a = out[:, 3:4]
    rgb = np.where(a > 0.5, out[:, :3] * 255.0 / np.maximum(a, 1e-6), 0.0)
    return np.concatenate([np.clip(rgb, 0, 255), np.clip(a, 0, 255)], axis=1).reshape(H, W, 4).astype(np.uint8)


def knie_zu(b, h, a):
    """Knie zu Hüfte h und Knöchel a; reicht das Bein nicht so weit, rückt der Knöchel heran."""
    d = math.dist(h, a)
    lang = (b.l1 + b.l2) * 0.995
    if d > lang:
        a = (h[0] + (a[0] - h[0]) * lang / d, h[1] + (a[1] - h[1]) * lang / d)
    return b.knie(h, a), a


def bein_lage(b, art, rumpf_ab, grad):
    """Neue Gelenke eines Beins zu einer Angabe aus tieranim.json (siehe Kopf)."""
    h2 = tuple(rumpf_ab(b.h))
    if art == "steht":
        k2, a2 = knie_zu(b, h2, b.a)
        return h2, k2, a2, (a2[0] + b.z[0] - b.a[0], a2[1] + b.z[1] - b.a[1])
    if "fuss" in art:
        fx, fy, neig = art["fuss"]
        k2, a2 = knie_zu(b, h2, (b.a[0] + fx, b.a[1] + fy))
        return h2, k2, a2, tuple(a2 + dreh_matrix(neig) @ (np.array(b.z) - b.a))
    d1, d2, d3 = art["mit"]
    k2 = tuple(h2 + dreh_matrix(grad + d1) @ (np.array(b.k) - b.h))
    a2 = tuple(k2 + dreh_matrix(grad + d1 + d2) @ (np.array(b.a) - b.k))
    return h2, k2, a2, tuple(a2 + dreh_matrix(grad + d1 + d2 + d3) @ (np.array(b.z) - b.a))


def sichtbar(bild):
    """Rechteck der Pixel mit Deckkraft über 8 (wie Sichtbar in uebernehmen.ps1): x0, y0, x1, y1."""
    return Image.fromarray((np.array(bild)[:, :, 3] > 8).astype(np.uint8) * 255).getbbox()


def vereinige(r, s):
    return (min(r[0], s[0]), min(r[1], s[1]), max(r[2], s[2]), max(r[3], s[3]))


def blatt(bilder, datei, linien=()):
    """Kontaktbogen: alle Phasen nebeneinander, halbe Größe, auf Gras, mit Bodenlinien."""
    box = sichtbar(bilder[0])
    for b in bilder[1:]:
        box = vereinige(box, sichtbar(b))
    x0, y0, x1, y1 = box[0] - 10, box[1] - 10, box[2] + 10, box[3] + 10
    s = 0.5
    bw, bh = int((x1 - x0) * s), int((y1 - y0) * s)
    bogen = Image.new("RGBA", (bw * len(bilder), bh), (110, 150, 80, 255))
    for p, b in enumerate(bilder):
        bogen.alpha_composite(b.crop((x0, y0, x1, y1)).resize((bw, bh), Image.LANCZOS), (p * bw, 0))
    z = ImageDraw.Draw(bogen)
    for y in linien:
        z.line([(0, (y - y0) * s), (bogen.width, (y - y0) * s)], fill=(255, 0, 0, 150))
    bogen.save(datei)


def figur(name, cfg):
    ziel = ZIEL / name
    ziel.mkdir(parents=True, exist_ok=True)
    stand = Quelle("stand", lade(cfg["bild"]), cfg["beine"], cfg)
    stand.teile_bild().save(ziel / "teile_stand.png")
    quellen = {"stand": stand}
    if "angriff" in cfg:
        ersatz = cfg.get("angriff_beine", [None] * len(cfg["beine"]))
        beine = [dict(c, **e) if e else c for c, e in zip(cfg["beine"], ersatz)]
        quellen["angriff"] = Quelle("angriff", lade(cfg["angriff_bild"]), beine, cfg)
        quellen["angriff"].teile_bild().save(ziel / "teile_angriff.png")

    # Gang: Fußbahnen aus gang.Bein, Hub aus den tragenden Standbeinen, dazu Nicken im Schritt
    phasen = cfg.get("phasen", 8)
    versatz = [c.get("versatz", 0.0) for c in cfg["beine"]]
    mitte = np.mean([b.h for b in stand.beine], axis=0)
    lauf, hube = [], []
    for p in range(phasen):
        phis = [(p / phasen + versatz[i]) % 1.0 for i in range(len(stand.beine))]
        fl = [b.fusslage(phis[i]) for i, b in enumerate(stand.beine)]
        # Der Rumpf bleibt in Standhöhe und sinkt nur, wenn ein tragendes Standbein selbst gestreckt
        # nicht mehr zum Boden reicht (anders als gang.py, das ihn so hoch hebt, wie die Beine im Stand
        # reichen - bei schräg gestellten Tierbeinen hüpfte er so um ein Zwanzigstel seiner Höhe); dazu
        # ein leichtes Wippen, zweimal je Doppelschritt
        hub = max((y - math.sqrt(max((b.g["streckung"] * (b.l1 + b.l2)) ** 2 - x ** 2, 1.0)) - b.h[1]
                   for b, (x, y, _), phi in zip(stand.beine, fl, phis) if b.steht(phi) and b.traegt), default=0.0)
        hub = max(hub, 0.0) + cfg.get("wippen", 0.0) * (1 - math.cos(4 * math.pi * p / phasen)) / 2
        nicken = cfg.get("nicken", 0.0) * math.sin(4 * math.pi * p / phasen + cfg.get("nicken_phase", 0.0))
        R = dreh_matrix(nicken)

        def rumpf_ab(q, R=R, hub=hub):
            return R @ (np.array(q, float) - mitte) + mitte + np.array([0.0, hub])

        lagen = []
        for b, (x, y, neig), phi in zip(stand.beine, fl, phis):
            h2 = tuple(rumpf_ab(b.h))
            k2, a2 = knie_zu(b, h2, (b.h[0] + x, y))
            # Im Schwung hängt die Pfote am Mittelfuß, statt waagerecht zu bleiben (am stärksten mitten
            # im Schwung); nie mehr als "pfote_max" Grad gegen ihn geknickt - mehr faltete das Fell
            mittelfuss = gang.winkel(k2, a2) - gang.winkel(b.k, b.a)
            if not b.steht(phi):
                u = (phi - b.g["stand"]) / (1 - b.g["stand"])
                neig += (mittelfuss - neig) * math.sin(math.pi * u)
            grenze = cfg.get("pfote_max", 40.0)
            neig = mittelfuss + max(-grenze, min(grenze, neig - mittelfuss))
            lagen.append((h2, k2, a2, tuple(a2 + dreh_matrix(neig) @ (np.array(b.z) - b.a))))
        bild, _ = stand.stelle((0.0, hub, nicken, mitte), lagen)
        lauf.append(bild)
        hube.append(hub)

    # Rechteck der Figur im Spiel: Standbild und Laufbilder (so rechnet uebernehmen.ps1 es auch)
    figur_r = sichtbar(stand.a)
    for b in lauf:
        figur_r = vereinige(figur_r, sichtbar(b))
    innen = (RAND, RAND, RAND + GROESSE, RAND + GROESSE)
    if vereinige(figur_r, innen) != innen:
        print(f"WARNUNG {name}: ein Laufbild ragt über die 1024er Leinwand hinaus: {figur_r}")
    for p, b in enumerate(lauf):
        b.crop(innen).save(ZIEL / f"{name}_lauf{p + 1}_1.png")
    blatt(lauf, ZIEL / f"{name}_lauf_blatt.png", sorted({b.boden for b in stand.beine}))
    breite_r = figur_r[2] - figur_r[0]
    print(f"{name}: {phasen} Laufphasen, Rechteck {figur_r[2] - figur_r[0]} x {figur_r[3] - figur_r[1]}, "
          f"Schritt {cfg['schritt']} px = {cfg['schritt'] / breite_r:.4f} Bildbreiten, Hub {' '.join(f'{h:+.0f}' for h in hube)}")

    # Angriff: Schlüsselposen
    angriff = cfg.get("angriff")
    if not angriff:
        return
    bilder = []
    for ph in angriff["phasen"]:
        q = quellen[ph.get("quelle", "stand")]
        dx, dy, grad = ph.get("koerper", [0, 0, 0])
        um = verschoben(ph.get("um", angriff["um"]))
        R = dreh_matrix(grad)
        umv = np.array(um, float)

        def rumpf_ab(p, R=R, umv=umv, dx=dx, dy=dy):
            return R @ (np.array(p, float) - umv) + umv + np.array([dx, dy], float)

        lagen = [bein_lage(b, art, rumpf_ab, grad) for b, art in zip(q.beine, ph["beine"])]
        bild, _ = q.stelle((dx, dy, grad, um), lagen)
        bilder.append(bild)
    # Größere Leinwand: seitlich gleich weit, nach oben so weit wie nötig, Unterkante fest
    alle = sichtbar(bilder[0])
    for b in bilder[1:]:
        alle = vereinige(alle, sichtbar(b))
    seitlich = max(figur_r[0] - alle[0], alle[2] - figur_r[2], 0)
    rahmen = (figur_r[0] - seitlich, min(figur_r[1], alle[1]), figur_r[2] + seitlich, figur_r[3])
    if alle[3] > figur_r[3]:
        tiefer = [sichtbar(b)[3] - figur_r[3] for b in bilder]
        print(f"WARNUNG {name}: Angriffsbilder reichen unter die Pfoten und werden abgeschnitten (Pixel je Phase: {tiefer})")
    if rahmen[0] < 0 or rahmen[1] < 0 or rahmen[2] > GROESSE + 2 * RAND:
        raise SystemExit(f"{name}: Angriff passt nicht auf die Leinwand (RAND vergrößern): {rahmen}")
    for p, b in enumerate(bilder):
        a = np.array(b)
        a[rahmen[3]:, :, 3] = 0
        for x, y in ((rahmen[0], rahmen[1]), (rahmen[2] - 1, rahmen[3] - 1)):
            if a[y, x, 3] <= 8:
                a[y, x] = (a[y, x, 0], a[y, x, 1], a[y, x, 2], 9)
        Image.fromarray(a).save(ZIEL / f"{name}_angriff{p + 1}_1.png")
        bilder[p] = Image.fromarray(a)
    blatt(bilder, ZIEL / f"{name}_angriff_blatt.png", [figur_r[3]])
    zielbreite = round(ZIELBREITE * (rahmen[2] - rahmen[0]) / breite_r)
    print(f"{name}: {len(bilder)} Angriffsphasen, Leinwand {rahmen[2] - rahmen[0]} x {rahmen[3] - rahmen[1]}"
          f" - zielbreite {zielbreite} (bilder.json, Gruppe tieranim)")


def main():
    alle = json.loads((HIER / "tieranim.json").read_text(encoding="utf-8"))
    namen = sys.argv[1:] or [n for n in alle if not n.startswith("_")]
    for name in namen:
        figur(name, alle[name])


if __name__ == "__main__":
    main()
