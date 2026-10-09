"""Gehphasen aus dem Standbild einer Figur (C4m): Beine über ein Skelett neu stellen.

Der Rumpf bleibt Pixel für Pixel, jedes Bein wird in drei Glieder geschnitten (Oberschenkel,
Unterschenkel, Fuß - beim Pferd Unterarm oder Unterschenkel, Röhre, Fessel mit Huf) und je
Phase neu gestellt. Vorgegeben sind nicht Gelenkwinkel, sondern die Fußbahnen: der Standfuß
ruht auf dem Boden und wandert relativ zur Hüfte gleichmäßig nach hinten - so rutscht er
nicht -, rollt zuerst über die Ferse ab und hebt zuletzt über die Zehen ab; der Schwungfuß
zieht im flachen Bogen nach vorn. Der Körper steht so hoch, wie die Standbeine es fast
gestreckt erlauben, daraus folgt das Auf und Ab. Das mittlere Gelenk ergibt sich aus den
beiden Knochenlängen (Knie nach vorn, beim Pferd hinten das Sprunggelenk nach hinten).

Zwei Beine laufen eine halbe Periode versetzt; ein Pferd geht im Viertakt (jedes Bein um ein
Viertel versetzt, Feld "versatz" je Bein). Der Oberschenkel reicht unter dem Saum gespiegelt
nach oben weiter, damit beim Schwingen kein Hintergrund unter dem Kittel aufblitzt; was er
dabei über den Saum schiebt, bleibt unsichtbar.

Gelenke, Saum und Schrittlänge je Figur stehen in gang.json neben diesem Skript, in Pixeln
des freigestellten Rohbilds (1024 x 1024 aus qwen_image.py) - gearbeitet wird in voller
Auflösung, verkleinert erst beim Übernehmen. Ergebnis je Figur unter ausgabe/gang/:
<figur>/phase<N>.png (freigestellt), <figur>/phase<N>_roh.png (auf Weiß), <figur>/gelenke.json
(Gelenke je Phase), <figur>_lauf<N+1>_1.png (dieselben Phasen im Namensschema, das
uebernehmen.ps1 erwartet, Seed 1) und der Kontaktbogen <figur>_blatt.png mit den Bodenlinien.

Braucht numpy, scipy und Pillow - die Python von ComfyUI hat alles:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/gang.py [figur ...]
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

# Gangart eines Menschen; eine Figur kann jeden Wert in gang.json unter "gang" ersetzen
GANG = {
    "stand": 0.62,        # Anteil der Standphase an der Periode
    "ferse_ende": 0.10,   # bis hier rollt der Fuß über die Ferse ab
    "zehe_anfang": 0.30,  # ab hier hebt die Ferse, der Fuß dreht um die Zehen
    "ferse_grad": 18.0,   # Fußspitze beim Aufsetzen angehoben
    "zehe_grad": -38.0,   # Ferse beim Abdruck angehoben (Fußspitze nach unten)
    "streckung": 0.985,   # so weit streckt sich ein Standbein höchstens
    "schwung_hub": 0.10,  # Fußhub im Schwung, Anteil der Beinlänge
}


def abstand_strecke(px, py, a, b):
    """Abstand jedes Pixels zur Strecke a-b."""
    ax, ay = a
    bx, by = b
    dx, dy = bx - ax, by - ay
    t = np.clip(((px - ax) * dx + (py - ay) * dy) / (dx * dx + dy * dy), 0, 1)
    return np.hypot(px - (ax + t * dx), py - (ay + t * dy))


def winkel(a, b):
    """Richtung a->b gegen die Senkrechte nach unten in Grad, + nach rechts (vorn)."""
    return math.degrees(math.atan2(b[0] - a[0], b[1] - a[1]))


def dreh(p, um, grad):
    """Dreht p um 'um' so, dass ein nach unten zeigender Vektor um +grad nach vorn schwenkt."""
    r = math.radians(grad)
    x, y = p[0] - um[0], p[1] - um[1]
    return (um[0] + x * math.cos(r) + y * math.sin(r), um[1] - x * math.sin(r) + y * math.cos(r))


def glatt(t):
    t = min(max(t, 0.0), 1.0)
    return t * t * (3 - 2 * t)


class Bein:
    """Ein Bein aus zwei Knochen und starrem Fuß, Maße aus dem Standbild."""

    def __init__(self, c, schritt, gang, saum, deckend):
        self.h, self.k, self.a, self.z = (tuple(c[n]) for n in ("huefte", "knie", "knoechel", "zehe"))
        self.l1, self.l2 = math.dist(self.h, self.k), math.dist(self.k, self.a)
        self.g = gang
        self.saum = c.get("saum", saum)
        self.knick_hinten = c.get("knick") == "hinten"
        if "boden" in c:
            self.boden = c["boden"]
        else:
            # Unterkante des Fußes: das tiefste deckende Pixel zwischen Ferse und Zehe
            x0, x1 = int(min(self.a[0], self.z[0]) - 40), int(max(self.a[0], self.z[0]) + 30)
            ys = np.where(deckend[int(self.a[1]):, x0:x1].any(axis=1))[0]
            self.boden = int(self.a[1]) + int(ys.max())
        # Fuß starr: Ferse und Zehe als Sohlenpunkte relativ zum Knöchel; Ruhelage gilt als flach
        self.ferse = (c.get("ferse_x", -20), self.boden - self.a[1])
        self.spitze = (self.z[0] - self.a[0], self.boden - self.a[1])
        self.L = schritt
        # Größte Hüft-Knöchel-Strecke im Stand: so hoch wie im Standbild, wenn der Fuß flach
        # unter der Hüfte steht - so liegt die Hüfte mitten im Stand jedes Beins gleich hoch
        self.lmax = min((self.boden - self.ferse[1]) - self.h[1], gang["streckung"] * (self.l1 + self.l2))

    def steht(self, phi):
        return phi < self.g["stand"]

    def fusslage(self, phi):
        """(Knöchel relativ zur Hüfte in x, Knöchel absolut in y, Fußneigung) bei Phase phi."""
        g = self.g
        weg = 2 * self.L                             # Körperweg je Periode
        ferse_x0 = g["stand"] * weg / 2              # Ferse beim Aufsetzen, vor der Hüfte

        def stand(p):
            # Der Fuß ruht; seine Bodenpunkte wandern relativ zur Hüfte mit -weg * p
            if p < g["ferse_ende"]:
                neig = g["ferse_grad"] * (1 - glatt(p / g["ferse_ende"]))
                ferse = (ferse_x0 - weg * p, self.boden)
                knoe = dreh((ferse[0] - self.ferse[0], ferse[1] - self.ferse[1]), ferse, neig)
                return knoe[0], knoe[1], neig
            flach_x = ferse_x0 - self.ferse[0] - weg * p
            if p < g["zehe_anfang"]:
                return flach_x, self.boden - self.ferse[1], 0.0
            neig = g["zehe_grad"] * glatt((p - g["zehe_anfang"]) / (g["stand"] - g["zehe_anfang"]))
            zehe = (flach_x + self.spitze[0], self.boden)
            knoe = dreh((zehe[0] - self.spitze[0], zehe[1] - self.spitze[1]), zehe, neig)
            return knoe[0], knoe[1], neig

        if phi < g["stand"]:
            return stand(phi)
        # Schwung: vom Abheben bis zum nächsten Aufsetzen, im flachen Bogen
        u = (phi - g["stand"]) / (1 - g["stand"])
        x0, y0, n0 = stand(g["stand"] - 1e-6)
        x1, y1, n1 = stand(0.0)
        s = glatt(u)
        hub = (self.l1 + self.l2) * g["schwung_hub"] * math.sin(math.pi * min(1.0, u * 1.15)) ** 1.5
        return x0 + (x1 - x0) * s, y0 + (y1 - y0) * s - hub, n0 + (n1 - n0) * glatt(u * 1.2)

    def knie(self, h, a):
        """Mittleres Gelenk zu Hüfte h und Knöchel a: nach vorn, beim Sprunggelenk nach hinten."""
        d = min(math.dist(h, a), self.l1 + self.l2 - 1e-3)
        cos_a = (self.l1 ** 2 + d ** 2 - self.l2 ** 2) / (2 * self.l1 * d)
        al = math.degrees(math.acos(max(-1.0, min(1.0, cos_a))))
        w = winkel(h, a)
        k1 = (h[0] + self.l1 * math.sin(math.radians(w + al)), h[1] + self.l1 * math.cos(math.radians(w + al)))
        k2 = (h[0] + self.l1 * math.sin(math.radians(w - al)), h[1] + self.l1 * math.cos(math.radians(w - al)))
        vorn, hinten = (k1, k2) if k1[0] > k2[0] else (k2, k1)
        return hinten if self.knick_hinten else vorn


def figur(name, cfg):
    bild = Image.open(AUSGABE / cfg["bild"]).convert("RGBA")
    a = np.array(bild)
    H, W = a.shape[:2]
    # Halbtransparente Randpixel der Freistellung tragen noch das Weiß des Hintergrunds; beim
    # Drehen der Glieder würden sie als weiße Striche sichtbar. Sie bekommen die Farbe des
    # nächsten deckenden Pixels, ihre Deckkraft bleibt
    halb = a[:, :, 3] < 200
    if halb.any() and (~halb).any():
        _, (ny, nx) = ndimage.distance_transform_edt(halb, return_indices=True)
        a[halb, :3] = a[ny[halb], nx[halb], :3]
    yy, xx = np.mgrid[0:H, 0:W]
    deckend = a[:, :, 3] > 8
    saum, breite, phasen = cfg["saum"], cfg["breite"], cfg.get("phasen", 8)
    gang = dict(GANG, **cfg.get("gang", {}))
    beine = [Bein(c, cfg["schritt"], gang, saum, deckend) for c in cfg["beine"]]
    versatz = [c.get("versatz", [0.5, 0.0][i] if len(beine) == 2 else 0.0) for i, c in enumerate(cfg["beine"])]

    # Welches Bein: der nächste Knochen entscheidet
    knochen = []
    for i, b in enumerate(beine):
        knochen += [(i, 0, b.h, b.k), (i, 1, b.k, b.a), (i, 2, b.a, b.z)]
    d = np.stack([abstand_strecke(xx, yy, k[2], k[3]) for k in knochen])
    naechster = d.argmin(axis=0)
    bein_von = np.array([k[0] for k in knochen])[naechster]
    unter_saum = np.zeros((H, W), bool)
    for i, b in enumerate(beine):
        unter_saum |= (bein_von == i) & (yy >= b.saum)
    # Bein ist unter dem Saum, was nah an einem Knochen liegt, und unten bei den Füßen alles
    # (auch eine weit ausladende Stiefelferse). Weit abseits liegende Schatten unter dem Saum
    # bleiben beim Rumpf - drehten sie mit, klappten sie beim Schwingen heraus
    fuesse = min((b.k[1] + b.a[1]) / 2 for b in beine)
    beinbereich = deckend & unter_saum & ((d.min(axis=0) < breite) | (yy >= fuesse))
    # Was sicher zum Rumpf gehört (die herabhängende Hand, ein Mantelzipfel, eine Schwertklinge
    # vor dem Bein), bleibt beim Rumpf: Rechtecke [x0, y0, x1, y1] und Linien [x1, y1, x2, y2, Dicke]
    geschuetzt = np.zeros((H, W), bool)
    for x0, y0, x1, y1 in cfg.get("rumpf", []):
        geschuetzt[y0:y1, x0:x1] = True
    for x1, y1, x2, y2, dicke in cfg.get("rumpf_linien", []):
        geschuetzt |= abstand_strecke(xx, yy, (x1, y1), (x2, y2)) < dicke / 2
    beinbereich &= ~geschuetzt
    # Was danach vom Rumpf abgetrennt unter dem Saum liegt (etwa die Kante einer Stiefelferse),
    # schwebte im Bild stehen - es gehört zum Bein
    rest = deckend & ~beinbereich
    teile_rest, anzahl = ndimage.label(rest)
    if anzahl > 1:
        groessen = ndimage.sum(rest, teile_rest, range(1, anzahl + 1))
        haupt = 1 + int(np.argmax(groessen))
        beinbereich |= rest & (teile_rest != haupt) & unter_saum
    rumpf = a.copy()
    rumpf[beinbereich] = 0
    # Unter dem Geschützten fehlt dem Bein seine Textur - schwänge es aus, wanderte ein Loch in
    # Form der Hand oder Klinge mit. Dort mit der Farbe des nächsten Beinpixels auffüllen, nur wo
    # das Geschützte deckt (daneben liegt Hintergrund); das Geschützte liegt im Bild darüber
    unter = geschuetzt & deckend & unter_saum & (d.min(axis=0) < breite * 0.8)
    if unter.any():
        _, (ny, nx) = ndimage.distance_transform_edt(~beinbereich, return_indices=True)
        a = a.copy()
        a[unter] = a[ny[unter], nx[unter]]
        a[unter, 3] = 255
        beinbereich = beinbereich | unter

    # Jedes Bein in drei Glieder schneiden, quer zum Knochen: am Knie entlang der Senkrechten auf
    # die mittlere Richtung beider Knochen, am Knöchel quer zum unteren Knochen. Das untere Glied
    # bekommt eine runde Gelenkscheibe aus den Pixeln rund ums Gelenk und liegt unter dem oberen -
    # so klafft bei keinem Winkel eine Lücke, und nichts steht ab
    def einheit(p, q):
        v = np.array(q, float) - np.array(p, float)
        return v / np.linalg.norm(v)

    teile = {}
    for i, b in enumerate(beine):
        mein = beinbereich & (bein_von == i)

        def jenseits(punkt, normale, versatz=0.0):
            return (xx - punkt[0]) * normale[0] + (yy - punkt[1]) * normale[1] > versatz

        def scheibe(punkt, normale):
            # Breite des Beins entlang des Schnitts, halbiert, ist der Radius der Gelenkscheibe
            band = mein & (np.abs((xx - punkt[0]) * normale[0] + (yy - punkt[1]) * normale[1]) < 2)
            radius = band.sum() / 8 + 3
            return mein & (np.hypot(xx - punkt[0], yy - punkt[1]) < radius)

        n_knie = einheit(b.h, b.k) + einheit(b.k, b.a)
        n_knie /= np.linalg.norm(n_knie)
        n_knoechel = einheit(b.k, b.a)
        unter_knie = jenseits(b.k, n_knie)
        unter_knoechel = jenseits(b.a, n_knoechel, -cfg.get("knoechel_hoch", 12))
        glieder = [mein & ~unter_knie,
                   (mein & unter_knie & ~unter_knoechel) | scheibe(b.k, n_knie),
                   (mein & unter_knoechel) | scheibe(b.a, n_knoechel)]
        for j, maske in enumerate(glieder):
            t = np.zeros_like(a)
            t[maske] = a[maske]
            if j == 0:
                # Oberschenkel unter dem Saum nach oben verlängern, gespiegelt: die Zeilen unter dem
                # Saum noch einmal darüber. Schwingt der Oberschenkel, füllt das die Lücke unter
                # dem Saum mit Stoff statt mit Hintergrund
                for k in range(1, cfg.get("verlaengerung", 70)):
                    quelle, ziel_y = b.saum + k, b.saum - k
                    spalten = np.where(maske[quelle])[0]
                    t[ziel_y, spalten] = a[quelle, spalten]
            teile[(i, j)] = Image.fromarray(t)
    ruhe = {(i, 0): winkel(b.h, b.k) for i, b in enumerate(beine)}
    ruhe.update({(i, 1): winkel(b.k, b.a) for i, b in enumerate(beine)})

    ziel = AUSGABE / "gang" / name
    ziel.mkdir(parents=True, exist_ok=True)
    gelenke, bilder = [], []
    for p in range(phasen):
        phis = [(p / phasen + versatz[i]) % 1.0 for i in range(len(beine))]
        lagen = [b.fusslage(phis[i]) for i, b in enumerate(beine)]
        # Körper so hoch, wie die Standbeine es fast gestreckt erlauben (gemeinsamer Hub dy)
        dy = max(y - math.sqrt(max(b.lmax ** 2 - x ** 2, 1.0)) - b.h[1]
                 for b, (x, y, _), phi in zip(beine, lagen, phis) if b.steht(phi))
        bildp = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        punkte = []
        for i, b in enumerate(beine):
            x, y, neig = lagen[i]
            h = (b.h[0], b.h[1] + dy)
            an = (b.h[0] + x, y)
            kn = b.knie(h, an)
            punkte.append({"huefte": h, "knie": kn, "knoechel": an})
            bein = Image.new("RGBA", (W, H), (0, 0, 0, 0))
            # Fuß, Unterschenkel, Oberschenkel - der Oberschenkel liegt am Knie oben
            for j, von, nach, grad in ((2, b.a, an, neig), (1, b.k, kn, winkel(kn, an) - ruhe[(i, 1)]),
                                       (0, b.h, h, winkel(h, kn) - ruhe[(i, 0)])):
                gedreht = teile[(i, j)].rotate(grad, resample=Image.BICUBIC, center=von)
                lage = Image.new("RGBA", (W, H), (0, 0, 0, 0))
                lage.paste(gedreht, (int(round(nach[0] - von[0])), int(round(nach[1] - von[1]))), gedreht)
                bein = Image.alpha_composite(bein, lage)
            # Über dem Saum ist Rumpf: was ein schwingender Oberschenkel dort seitlich über den
            # Kittelrand hinausschiebt, bleibt unsichtbar
            arr = np.array(bein)
            arr[:max(0, int(round(b.saum + dy))), :, 3] = 0
            bildp = Image.alpha_composite(bildp, Image.fromarray(arr))
        r = Image.fromarray(rumpf)
        lage = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        lage.paste(r, (0, int(round(dy))), r)
        bildp = Image.alpha_composite(bildp, lage)
        bildp.save(ziel / f"phase{p}.png")
        bildp.save(AUSGABE / "gang" / f"{name}_lauf{p + 1}_1.png")
        weiss = Image.new("RGBA", (W, H), (255, 255, 255, 255))
        weiss.alpha_composite(bildp)
        weiss.convert("RGB").save(ziel / f"phase{p}_roh.png")
        gelenke.append({"hub": dy, "beine": punkte})
        bilder.append(bildp)
    (ziel / "gelenke.json").write_text(json.dumps(gelenke, indent=1), encoding="utf-8")

    # Kontaktbogen mit den Bodenlinien
    box = Image.new("L", (W, H))
    for b in bilder:
        box.paste(b.getchannel("A"), (0, 0), b.getchannel("A"))
    x0, y0, x1, y1 = box.getbbox()
    x0, y0, x1, y1 = x0 - 10, y0 - 10, x1 + 10, y1 + 10
    s = 0.5
    bw, bh = int((x1 - x0) * s), int((y1 - y0) * s)
    blatt = Image.new("RGBA", (bw * phasen, bh), (110, 150, 80, 255))
    for p, b in enumerate(bilder):
        blatt.alpha_composite(b.crop((x0, y0, x1, y1)).resize((bw, bh), Image.LANCZOS), (p * bw, 0))
    zeichner = ImageDraw.Draw(blatt)
    for b in beine:
        zeichner.line([(0, (b.boden - y0) * s), (blatt.width, (b.boden - y0) * s)], fill=(255, 0, 0, 150))
    blatt.save(AUSGABE / "gang" / f"{name}_blatt.png")
    print(f"{name}: {phasen} Phasen, Hub {min(g['hub'] for g in gelenke):+.0f} bis "
          f"{max(g['hub'] for g in gelenke):+.0f} px, Boden {[b.boden for b in beine]}")


def main():
    alle = json.loads((HIER / "gang.json").read_text(encoding="utf-8"))
    namen = sys.argv[1:] or list(alle)
    for name in namen:
        figur(name, alle[name])


if __name__ == "__main__":
    main()
