"""Fällstufen der Bäume (Holz abbauen): aus jedem Baumbild in Content/Baeume vier Bilder.

    <art>_gefaellt.png   der Stumpf steht, der Baum liegt mit Krone daneben
    <art>_stamm2.png     der Stumpf, daneben zwei entastete Stammstücke
    <art>_stamm1.png     der Stumpf, daneben ein Stammstück
    <art>_stumpf.png     nur der Stumpf

Alles kommt aus dem stehenden Baum selbst - keine Grafikkarte, kein Modell: unten bleibt der
Wurzelteil als Stumpf stehen (mit heller Schnittfläche obendrauf), der Rest wird um 90° im
Uhrzeigersinn umgelegt (die Krone zeigt nach rechts, die vorher linke, beleuchtete Seite nach
oben - das Licht kommt weiter von links oben) und von schräg oben gesehen senkrecht gestaucht.
Die Stammstücke sind Rinde aus dem kahlen Stammabschnitt über dem Schnitt, zu Stücken
aneinandergesetzt, mit hellen Schnittflächen an den Enden. So bleibt jede Baumart erkennbar
(Birke weiß, Kiefer rot) und Licht, Pixelstil und Farben passen zum stehenden Baum.

Alle vier Bilder einer Art haben dieselbe Größe; der Stammfuß liegt in jedem bei ANKER (Pixel
von links oben). Das Spiel zeichnet sie mit FELL_TEX_SCALE = SKALA: ein Bildpunkt hier ist
1 / SKALA Bildpunkte des stehenden Baums. Fällt der Baum nach links, spiegelt das Spiel das
Bild (der Anker wandert mit: Breite - ANKER.x).

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/faellen.py [art ...] [--uebernehmen]

Ohne --uebernehmen landen die Bilder in tools/bilder/ausgabe/faellen, mit in Content/Baeume.
Dazu entsteht dort pruefbogen.png: jede Art stehend und in allen Stufen, in Spielgröße (Zoom 2)
auf Gras, darunter gespiegelt.
"""

import sys
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

HIER = Path(__file__).resolve().parent
CONTENT = HIER.parents[1] / "AgeOfEvolutions" / "AgeOfEvolutions.Core" / "Content" / "Baeume"
AUSGABE = HIER / "ausgabe" / "faellen"

# Art, Höhe im Spiel (Welteinheiten, wie TreeAssets in RTSGameplayScreen) - nur für den Prüfbogen
ARTEN = {
    "laubbaum": 72, "laubbaum2": 66, "buschbaum": 58, "buche": 74, "birke": 68,
    "nadelbaum": 82, "nadelbaum2": 78, "kiefer": 86,
    # Palmen der Ägypten-Karte (PalmAssets im Spiel)
    "palme": 90, "palme_gruppe": 80, "palme_dum": 84, "palme_kokos": 92,
}
STUFEN = ["gefaellt", "stamm2", "stamm1", "stumpf"]

SKALA = 0.5          # Ausgabe-Pixel je Pixel des stehenden Baums (FELL_TEX_SCALE im Spiel)
LAENGE = 0.8         # der umgelegte Teil ist kürzer als der stehende Baum hoch
STAUCHUNG = 0.7      # liegend, von schräg oben gesehen: senkrecht gestaucht
ANKER = (72, 176)    # Stammfuß im Ausgabebild (FELL_ANCHOR im Spiel)

HOLZ = (214, 176, 122)       # Schnittfläche
HOLZ_RING = (176, 134, 86)   # Jahresringe
HOLZ_KERN = (150, 108, 66)


def laeufe(zeile):
    """Zusammenhängende deckende Abschnitte einer Alpha-Zeile als (Anfang, Ende inkl.)."""
    d = np.concatenate([[0], (zeile > 128).astype(np.int8), [0]])
    k = np.diff(d)
    return list(zip(np.where(k == 1)[0], np.where(k == -1)[0] - 1))


def lauf_um(zeile, x):
    """Der deckende Abschnitt, der x enthält oder ihm am nächsten liegt."""
    best = None
    for a, b in laeufe(zeile):
        d = 0 if a <= x <= b else min(abs(a - x), abs(b - x))
        if best is None or d < best[0]:
            best = (d, a, b)
    return None if best is None else (best[1], best[2])


def vermessen(arr):
    """Stammmitte, halbe Stammbreite und Schnittzeile des stehenden Baums."""
    h, w = arr.shape[:2]
    a = arr[:, :, 3]
    # Stammmitte: Mitte des Abschnitts um die Bildmitte in 12-16 % Höhe
    mitten = []
    for y in range(int(h * 0.84), int(h * 0.88)):
        r = lauf_um(a[y], w // 2)
        if r:
            mitten.append((r[0] + r[1]) / 2)
    cx = int(round(np.median(mitten)))
    # Stammbreite: schmale Zeilen zwischen 8 und 40 % Höhe (Äste und Krone sind breiter)
    breiten = []
    for y in range(int(h * 0.60), int(h * 0.92)):
        r = lauf_um(a[y], cx)
        if r:
            breiten.append(r[1] - r[0] + 1)
    tw = float(np.percentile(breiten, 20))
    # Oberkante der Wurzeln: von der breitesten Zeile im untersten Zehntel (den ausgebreiteten
    # Wurzeln) nach oben, bis der Abschnitt nur noch so breit ist wie der Stamm. Von ganz unten
    # gesucht blieb die Suche an dünnen Wurzelspitzen hängen; von oben gesucht hielt sie die
    # unterste Astreihe der Fichten für Wurzeln.
    def breite(y):
        r = lauf_um(a[y], cx)
        return 0 if r is None else r[1] - r[0] + 1
    unten = max(range(int(h * 0.90), h), key=breite)
    wurzel = unten
    for y in range(unten, int(h * 0.70), -1):
        if breite(y) <= 1.5 * tw:
            wurzel = y
            break
    # Der Stumpf: ein Stück Stamm über dem Wurzelansatz, 8 bis 13 % der Baumhöhe
    schnitt = int(np.clip(wurzel - 0.025 * h, h * 0.87, h * 0.92))
    return cx, tw / 2, schnitt


def am_stamm(arr, cx, schnitt):
    """Nur was unter dem Schnitt mit dem Stamm zusammenhängt - keine Astspitzen daneben."""
    h, w = arr.shape[:2]
    deck = np.zeros((h, w), np.uint8)
    deck[schnitt:] = np.where(arr[schnitt:, :, 3] > 40, 255, 0)
    maske = Image.fromarray(deck, "L").copy()   # ohne copy schreibt floodfill nicht (Pillow 12)
    ImageDraw.floodfill(maske, (int(cx), schnitt + 1), 128)
    teil = np.array(maske) == 128
    aus = arr.copy()
    aus[~teil] = 0
    return aus


def schnittflaeche(bild, cx, cy, rx, ry, rinde):
    """Helle Schnittfläche mit Ringen als Ellipse (Pixelstil: ohne Kantenglättung)."""
    d = ImageDraw.Draw(bild)
    d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], fill=rinde + (255,))
    rx2, ry2 = rx * 0.82, ry * 0.78
    d.ellipse([cx - rx2, cy - ry2, cx + rx2, cy + ry2], fill=HOLZ + (255,))
    for f in (0.62, 0.38):
        d.ellipse([cx - rx2 * f, cy - ry2 * f, cx + rx2 * f, cy + ry2 * f], outline=HOLZ_RING + (255,),
                  width=max(1, int(min(rx, ry) * 0.07)))
    k = max(1.0, min(rx, ry) * 0.12)
    d.ellipse([cx - k, cy - k, cx + k, cy + k], fill=HOLZ_KERN + (255,))


def rindenfarbe(arr, cx, hw, y):
    """Dunkle Rindenfarbe am Schnitt (für den Rand der Schnittfläche)."""
    band = arr[max(0, y - 6):y + 6, int(cx - hw):int(cx + hw) + 1]
    pix = band[band[:, :, 3] > 200][:, :3].astype(float)
    if len(pix) == 0:
        return (90, 60, 40)
    # Mittlerer Rindenton, etwas abgedunkelt - der dunkelste war fast schwarz
    return tuple(int(v * 0.8) for v in np.median(pix, axis=0))


def skaliert(bild, breite, hoehe):
    """Größe ändern mit vormultipliziertem Alpha - sonst säumen helle Ränder die Kanten."""
    return bild.convert("RGBa").resize((max(1, int(round(breite))), max(1, int(round(hoehe)))),
                                       Image.LANCZOS).convert("RGBA")


def rindenstueck(arr, cx, hw, schnitt):
    """Ein kahles Stück Stamm über dem Schnitt (am wenigsten Grün), waagerecht gelegt."""
    h = arr.shape[0]
    x0, x1 = int(cx - hw), int(cx + hw) + 1
    s = max(8, int(h * 0.06))
    best = None
    for y0 in range(int(schnitt - h * 0.30), schnitt - s, 2):
        band = arr[y0:y0 + s, x0:x1].astype(float)
        deck = band[:, :, 3] > 128
        if deck.mean() < 0.85:
            continue
        r, g, b = band[:, :, 0], band[:, :, 1], band[:, :, 2]
        gruen = ((g > r * 1.12) & (g > b) & deck).mean()
        if best is None or gruen < best[0]:
            best = (gruen, y0)
    y0 = best[1] if best else schnitt - s
    stueck = arr[y0:y0 + s, x0:x1].copy()
    stueck[:, :, 3] = np.where(stueck[:, :, 3] > 40, 255, 0)
    return np.rot90(stueck, -1)   # Stammachse waagerecht


def stamm(stueck, laenge, rinde):
    """Stammstück der Länge laenge (Pixel, Maßstab des stehenden Baums), schon gestaucht."""
    teile, n, i = [], 0, 0
    while n < laenge:
        t = stueck if i % 2 == 0 else stueck[:, ::-1]
        teile.append(t)
        n += t.shape[1]
        i += 1
    roh = np.concatenate(teile, axis=1)[:, :int(laenge)]
    hoehe = roh.shape[0] * STAUCHUNG
    bild = skaliert(Image.fromarray(roh), roh.shape[1] * LAENGE, hoehe)
    # Schnittflächen an beiden Enden: schmal, weil sie zur Seite zeigen
    ry = hoehe / 2
    rx = max(2.0, ry * 0.38)
    rand = Image.new("RGBA", (int(bild.width + 2 * rx + 2), bild.height), (0, 0, 0, 0))
    rand.alpha_composite(bild, (int(rx), 0))
    schnittflaeche(rand, rx, ry - 0.5, rx, ry - 0.5, rinde)
    schnittflaeche(rand, rx + bild.width, ry - 0.5, rx, ry - 0.5, rinde)
    return rand


def stufen(art):
    """Die vier Fällstufen einer Art in Ausgabegröße, dazu Kennzahlen."""
    baum = Image.open(CONTENT / f"{art}.png").convert("RGBA")
    arr = np.array(baum)
    h, w = arr.shape[:2]
    cx, hw, schnitt = vermessen(arr)
    rinde = rindenfarbe(arr, cx, hw, schnitt)

    ax, ay = ANKER[0] / SKALA, ANKER[1] / SKALA
    ox, oy = ax - w / 2, ay - h        # Lage des stehenden Bilds auf der Leinwand
    boden = oy + h * 0.97              # Bodenlinie am Stammfuß

    # Stumpf: der Wurzelteil unter dem Schnitt, oben die Schnittfläche
    stumpf = Image.fromarray(am_stamm(arr, cx, schnitt))
    # Schnittfläche so breit wie der Stamm (am Schnitt beginnt schon der Wurzelansatz)
    rx = hw * 1.08
    schnittflaeche(stumpf, cx, schnitt, rx, rx * 0.4, rinde)

    # Umgelegter Baum: oberer Teil im Uhrzeigersinn gedreht, gekürzt und gestaucht
    oben = np.rot90(arr[:schnitt], -1)          # Zeile = alte Spalte, Spalte 0 = Schnitt
    liegend = skaliert(Image.fromarray(oben), oben.shape[1] * LAENGE, oben.shape[0] * STAUCHUNG)
    achse = cx * STAUCHUNG                       # Stammachse im liegenden Bild
    lx = ox + cx + hw * 1.05                     # Stammende knapp rechts neben dem Stumpf
    ly = boden - hw * STAUCHUNG - achse          # der Stamm liegt auf dem Boden
    fry = hw * STAUCHUNG
    schnittflaeche(liegend, max(2.0, fry * 0.38), achse, max(2.0, fry * 0.38), fry, rinde)

    # Stammstücke aus dem kahlen Stamm über dem Schnitt
    stueck = rindenstueck(arr, cx, hw, schnitt)
    lang = schnitt * 0.42
    vorn = stamm(stueck, lang, rinde)
    hinten = stamm(stueck, lang * 0.9, rinde)
    sy = boden - vorn.height                     # vorderes Stück liegt auf dem Boden
    hy = sy - vorn.height * 0.62                 # hinteres dahinter, leicht erhöht

    leinwand = (int(ax + w * 1.6), int(ay + w * 0.6))

    def neu():
        return Image.new("RGBA", leinwand, (0, 0, 0, 0))

    def stumpf_drauf(bild):
        bild.alpha_composite(stumpf, (int(round(ox)), int(round(oy))))
        return bild

    gefaellt = neu()
    gefaellt.alpha_composite(liegend, (int(round(lx)), int(round(ly))))
    stumpf_drauf(gefaellt)

    stamm2 = neu()
    stamm2.alpha_composite(hinten, (int(round(lx + lang * LAENGE * 0.12)), int(round(hy))))
    stamm2.alpha_composite(vorn, (int(round(lx)), int(round(sy))))
    stumpf_drauf(stamm2)

    stamm1 = neu()
    stamm1.alpha_composite(vorn, (int(round(lx)), int(round(sy))))
    stumpf_drauf(stamm1)

    nur = stumpf_drauf(neu())

    bilder = {"gefaellt": gefaellt, "stamm2": stamm2, "stamm1": stamm1, "stumpf": nur}
    # Gemeinsamer Zuschnitt rechts und unten; links und oben bleibt der Anker fest
    rahmen = [b.getbbox() for b in bilder.values()]
    x0 = min(r[0] for r in rahmen)
    y0 = min(r[1] for r in rahmen)
    if x0 < 2 or y0 < 2:
        raise SystemExit(f"{art}: ragt links/oben aus der Leinwand ({x0}, {y0}) - ANKER vergrößern")
    x1 = max(r[2] for r in rahmen) + 2
    y1 = max(r[3] for r in rahmen) + 2
    aus = {}
    for name, b in bilder.items():
        b = b.crop((0, 0, x1, y1))
        aus[name] = skaliert(b, b.width * SKALA, b.height * SKALA)
    kenn = f"Stamm {2 * hw:.0f} px, Schnitt bei {100 * (h - schnitt) / h:.1f} % Höhe, Leinwand {aus['gefaellt'].size}"
    return baum, aus, kenn


def pruefbogen(alle, ziel):
    """Jede Art stehend und in allen Stufen in Spielgröße (Zoom 2) auf Gras, darunter gespiegelt."""
    zoom = 2.0
    zeilen = []
    for art, (baum, aus, _) in alle.items():
        einheit = ARTEN[art] / baum.height          # Welteinheiten je Pixel des stehenden Baums
        px = einheit * zoom                           # Bildschirmpixel je Pixel des stehenden Baums
        for spiegel in (False, True):
            stuecke = [skaliert(baum, baum.width * px, baum.height * px)]
            anker = [(stuecke[0].width / 2, stuecke[0].height)]
            for s in STUFEN:
                b = aus[s]
                f = px / SKALA
                t = skaliert(b, b.width * f, b.height * f)
                axp, ayp = ANKER[0] * f, ANKER[1] * f
                if spiegel:
                    t = t.transpose(Image.FLIP_LEFT_RIGHT)
                    axp = t.width - axp
                stuecke.append(t)
                anker.append((axp, ayp))
            zeilen.append((stuecke, anker))
    platz = 64 * 3                                    # drei Kacheln je Feld
    hoch = int(max(max(a[1] for a in an) + max(t.height - a[1] for t, a in zip(st, an)) for st, an in zeilen)) + 16
    bogen = Image.new("RGBA", (platz * 5 + 40, hoch * len(zeilen) + 20), (86, 118, 58, 255))
    d = ImageDraw.Draw(bogen)
    for i, (stuecke, anker) in enumerate(zeilen):
        oben = 10 + i * hoch
        fuss_y = oben + int(max(a[1] for a in anker)) + 6
        for j, (t, (axp, ayp)) in enumerate(zip(stuecke, anker)):
            fx = 20 + j * platz + platz // 2
            # Kachelraster (64 px = eine Kachel bei Zoom 2)
            d.rectangle([fx - 32, fuss_y - 32, fx + 32, fuss_y + 32], outline=(70, 100, 46, 255))
            bogen.alpha_composite(t, (int(fx - axp), int(fuss_y - ayp)))
    bogen.convert("RGB").save(ziel)


def main():
    args = [a for a in sys.argv[1:] if not a.startswith("--")]
    uebernehmen = "--uebernehmen" in sys.argv
    arten = args or list(ARTEN)
    ziel = CONTENT if uebernehmen else AUSGABE
    ziel.mkdir(parents=True, exist_ok=True)
    AUSGABE.mkdir(parents=True, exist_ok=True)
    alle = {}
    for art in arten:
        baum, aus, kenn = stufen(art)
        alle[art] = (baum, aus, kenn)
        for name, b in aus.items():
            b.save(ziel / f"{art}_{name}.png")
        print(f"{art}: {kenn}")
    pruefbogen(alle, AUSGABE / "pruefbogen.png")
    print(f"Prüfbogen: {AUSGABE / 'pruefbogen.png'}")


if __name__ == "__main__":
    main()
