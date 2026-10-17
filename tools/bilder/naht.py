"""Nahtlos kachelbar per Image Quilting (Efros & Freeman) statt Überblenden - für Fugenmuster.

Das Bild wird um die halbe Breite und Höhe verschoben: die Außenkanten passen dann, die Nähte
liegen als Kreuz in der Mitte. Jeder Nahtstreifen wird durch einen Streifen aus dem Bild selbst
ersetzt, der an beiden Rändern am besten passt; an den Überlappungen schneidet ein Min-Cut-Pfad
(geschlossen über den Bildrand) entlang der Stellen, wo sich beide am wenigsten unterscheiden -
bei Pflaster meist in den Fugen. Überblenden gäbe dort Geistersteine.

    python naht.py quelle.png ziel.png [streifen] [ueberlappung] [steinaufschlag]

Streifen und Überlappung in Pixeln, sonst 24 % und 9 % der Bildbreite; der Steinaufschlag
(sonst 6000) bestimmt, wie stark der Schnitt die Fugen bevorzugt. uebernehmen.ps1 ruft das
Skript für Einträge mit "kachelbar": "naht" auf (Python aus ComfyUI: numpy, Pillow).
"""
import sys
import numpy as np
from PIL import Image


def schnitt(fehler):
    """Geschlossener Min-Cut-Pfad von oben nach unten durch fehler (H x B): je Zeile eine Spalte,
    Nachbarzeilen höchstens eine Spalte auseinander, erste und letzte Zeile ebenso (Umlauf)."""
    h, b = fehler.shape
    starts = np.arange(0, b, 2)
    inf = np.float64(1e18)
    # kosten[s, x]: billigster Weg vom Start s in Zeile 0 bis Spalte x der aktuellen Zeile
    kosten = np.full((len(starts), b), inf)
    kosten[np.arange(len(starts)), starts] = fehler[0, starts]
    herkunft = np.zeros((h, len(starts), b), dtype=np.int8)
    for y in range(1, h):
        links = np.concatenate([np.full((len(starts), 1), inf), kosten[:, :-1]], axis=1)
        rechts = np.concatenate([kosten[:, 1:], np.full((len(starts), 1), inf)], axis=1)
        stapel = np.stack([links, kosten, rechts])          # -1, 0, +1
        wahl = np.argmin(stapel, axis=0)
        kosten = np.take_along_axis(stapel, wahl[None], 0)[0] + fehler[y]
        herkunft[y] = wahl - 1
    # Umlauf: die letzte Zeile muss neben dem Start liegen
    beste, bester_s, bestes_x = inf, 0, 0
    for i, s in enumerate(starts):
        for x in (s - 1, s, s + 1):
            if 0 <= x < b and kosten[i, x] < beste:
                beste, bester_s, bestes_x = kosten[i, x], i, x
    pfad = np.zeros(h, dtype=np.int64)
    x = bestes_x
    for y in range(h - 1, -1, -1):
        pfad[y] = x
        x = x + herkunft[y, bester_s, x] if y > 0 else x
    return pfad


def fuge(teil):
    """1 wo der Bildpunkt Fuge ist (hell und grau - Mörtel), sonst 0."""
    hell = teil.mean(axis=2)
    saett = (teil.max(axis=2) - teil.min(axis=2)) / np.maximum(teil.max(axis=2), 1)
    return ((hell > MOERTEL_HELL) & (saett < 0.12)).astype(np.float64)


def kosten(a, b):
    """Schnittkosten: Unterschied der beiden Bilder, dazu ein Aufschlag, wo eines davon keine Fuge
    ist - ein Schnitt durch die Fugen beider Bilder bleibt unsichtbar, einer durch Steine nicht."""
    return ((a - b) ** 2).sum(axis=2) + STEIN_AUFSCHLAG * (2 - fuge(a) - fuge(b))


def streifen_senkrecht(bild, quelle, breite, ueber):
    """Ersetzt die Spalten um die Bildmitte durch den passendsten Streifen aus quelle."""
    h, w, _ = bild.shape
    x0 = w // 2 - breite // 2
    li, re = bild[:, x0:x0 + ueber], bild[:, x0 + breite - ueber:x0 + breite]
    bester, bestes_px = None, 0
    for px in range(0, w - breite, 4):
        p = quelle[:, px:px + breite]
        f = ((p[:, :ueber] - li) ** 2).sum() + ((p[:, -ueber:] - re) ** 2).sum()
        if bester is None or f < bester:
            bester, bestes_px = f, px
    p = quelle[:, bestes_px:bestes_px + breite].copy()
    pfad_l = schnitt(kosten(p[:, :ueber], li))
    pfad_r = schnitt(kosten(p[:, -ueber:], re))
    zeilen = np.arange(h)
    in_fuge = (fuge(li)[zeilen, pfad_l] * fuge(p[:, :ueber])[zeilen, pfad_l]).mean()
    print(f"  Schnitt links zu {in_fuge:.0%} in den Fugen beider Bilder")
    spalten = np.arange(ueber)[None, :]
    nimm_bild_l = spalten < pfad_l[:, None]          # links vom Schnitt bleibt das Bild
    nimm_bild_r = spalten > pfad_r[:, None]          # rechts vom Schnitt wieder das Bild
    p[:, :ueber][nimm_bild_l] = li[nimm_bild_l]
    p[:, -ueber:][nimm_bild_r] = re[nimm_bild_r]
    aus = bild.copy()
    aus[:, x0:x0 + breite] = p
    print(f"  Streifen aus Spalte {bestes_px}, Fehler je Pixel {bester / (2 * h * ueber):.0f}")
    return aus


quelle_pfad, ziel = sys.argv[1:3]
o = np.asarray(Image.open(quelle_pfad).convert("RGB"), dtype=np.float64)
breite = int(sys.argv[3]) if len(sys.argv) > 3 else round(0.24 * o.shape[1])
ueber = int(sys.argv[4]) if len(sys.argv) > 4 else round(0.09 * o.shape[1])
MOERTEL_HELL = np.percentile(o.mean(axis=2), 65)
STEIN_AUFSCHLAG = float(sys.argv[5]) if len(sys.argv) > 5 else 6000.0
h, w, _ = o.shape
s = np.roll(o, (h // 2, w // 2), axis=(0, 1))               # Nähte als Kreuz in der Mitte
# 1) senkrechte Naht: Streifen aus dem nur senkrecht verschobenen Bild (dort ohne senkrechte Naht)
i1 = streifen_senkrecht(s, np.roll(o, h // 2, axis=0), breite, ueber)
# 2) waagrechte Naht: dasselbe quer, Quelle i1 senkrecht verschoben (Naht dort am Rand)
quelle2 = np.roll(i1, h // 2, axis=0)
i2 = streifen_senkrecht(i1.transpose(1, 0, 2), quelle2.transpose(1, 0, 2), breite, ueber).transpose(1, 0, 2)
Image.fromarray(np.clip(i2, 0, 255).astype(np.uint8)).save(ziel)
print(ziel, (w, h))
