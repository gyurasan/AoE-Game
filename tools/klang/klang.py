"""Erzeugt die Geräusche für Arbeit und Kampf - synthetisch, ohne Aufnahmen und ohne KI-Modell.

Jedes Geräusch entsteht aus physikalischen Bausteinen: gedämpfte Eigenschwingungen (Holz,
Metall, Stein schwingen in wenigen Moden und klingen verschieden schnell ab), gefiltertes
Rauschen für Aufprall, Splitter und Staub, und Karplus-Strong für die Bogensehne. Je Geräusch
entstehen mehrere Varianten mit anderem Zufallssamen, damit dreißig Holzfäller nicht wie
einer im Gleichschritt klingen.

Braucht numpy und scipy - die Python von ComfyUI hat beides:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/klang/klang.py [ziel] [name ...]

Ohne Ziel schreibt es nach AgeOfEvolutions/AgeOfEvolutions.Core/Content/Sounds. Ausgabe: 16 Bit,
mono, 44,1 kHz, Spitze bei -2 dBFS; Dateiname <name>_<n>.wav. Mit --vorschau entsteht statt dessen
tools/klang/ausgabe/vorschau.wav: alle Varianten hintereinander zum Anhören, mit Zeitplan.
"""
import sys
import wave
from pathlib import Path

import numpy as np
from scipy import signal

SR = 44100
VARIANTEN = 3


# ------------------------------------------------------------------ Bausteine
def zeit(dauer):
    return np.arange(int(dauer * SR)) / SR


def abklingen(t, tau):
    return np.exp(-t / tau)


def band(x, lo, hi, ordnung=2):
    sos = signal.butter(ordnung, [lo, hi], btype="bandpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def tief(x, grenze, ordnung=2):
    sos = signal.butter(ordnung, grenze, btype="lowpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def hoch(x, grenze, ordnung=2):
    sos = signal.butter(ordnung, grenze, btype="highpass", fs=SR, output="sos")
    return signal.sosfilt(sos, x)


def moden(t, freqs, taus, amps, rng):
    """Gedämpfte Eigenschwingungen mit zufälliger Phase - so klingt ein angeschlagener Körper."""
    x = np.zeros_like(t)
    for f, tau, a in zip(freqs, taus, amps):
        x += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 2 * np.pi)) * abklingen(t, tau)
    return x


def stoss(t, lo, hi, tau, rng):
    """Der Aufprall selbst: ein kurzer Rauschstoß im Band lo bis hi."""
    return band(rng.standard_normal(len(t)), lo, hi) * abklingen(t, tau)


def knistern(t, anzahl, bis, lo, hi, rng, abfall=0.08):
    """Splitter und Krümel: einzelne kurze Klicks in den ersten bis Sekunden, leiser werdend."""
    x = np.zeros_like(t)
    for _ in range(anzahl):
        start = rng.uniform(0.003, bis)
        i = int(start * SR)
        n = int(0.004 * SR)
        if i + n >= len(t):
            continue
        klick = rng.standard_normal(n) * np.exp(-np.arange(n) / (0.0008 * SR))
        x[i:i + n] += klick * rng.uniform(0.3, 1.0) * np.exp(-start / abfall)
    return band(x, lo, hi)


def anschlag(x, ms=1.0):
    """Weicher Einsatz und Ausklang ohne Knacken."""
    n = max(1, int(ms / 1000 * SR))
    x[:n] *= np.linspace(0, 1, n)
    m = int(0.012 * SR)
    x[-m:] *= np.linspace(1, 0, m)
    return x


def karplus(f, dauer, verlust, rng):
    """Gezupfte Saite (Karplus-Strong): Rauschen in einer Verzögerungsschleife mit Tiefpass."""
    n = int(dauer * SR)
    p = int(SR / f)
    puffer = rng.uniform(-1, 1, p)
    out = np.zeros(n)
    for i in range(n):
        out[i] = puffer[i % p]
        puffer[i % p] = verlust * 0.5 * (puffer[i % p] + puffer[(i + 1) % p])
    return out


# ------------------------------------------------------------------ Geräusche
def axt(rng):
    """Axt in Holz: harter Einschlag, dumpfer Holzkörper, Splittern."""
    t = zeit(0.38)
    f0 = rng.uniform(150, 210)
    x = moden(t, [f0, f0 * 2.3, f0 * 3.9, f0 * 5.4], [0.07, 0.04, 0.022, 0.012], [1.0, 0.55, 0.35, 0.2], rng)
    x += 0.7 * stoss(t, 1800, 9000, 0.004, rng)
    x += 0.45 * stoss(t, 500, 2600, 0.03, rng)
    x += 0.5 * knistern(t, 14, 0.06, 1500, 7000, rng, 0.03)
    return x


def spitzhacke(rng):
    """Spitzhacke auf Stein oder Erz: Stahl klingt hell nach, Steinkrümel springen weg."""
    t = zeit(0.45)
    s = rng.uniform(0.95, 1.06)
    x = moden(t, [2150 * s, 3480 * s, 5230 * s, 7400 * s], [0.09, 0.06, 0.04, 0.025], [0.45, 0.4, 0.3, 0.18], rng)
    x += 1.0 * stoss(t, 1400, 7000, 0.007, rng)
    x += 0.35 * moden(t, [rng.uniform(85, 110)], [0.03], [1.0], rng)
    x += 0.6 * knistern(t, 18, 0.22, 2500, 9000, rng, 0.09)
    return x


def hammer(rng):
    """Hammer auf Balken und Nagel: kurzer, heller Holzklang mit leisem Metallnachklang."""
    t = zeit(0.3)
    f0 = rng.uniform(290, 370)
    x = moden(t, [f0, f0 * 2.6, f0 * 4.1], [0.045, 0.025, 0.014], [1.0, 0.5, 0.3], rng)
    x += 0.8 * stoss(t, 1200, 7000, 0.0025, rng)
    x += 0.18 * moden(t, [rng.uniform(2500, 3200)], [0.07], [1.0], rng)
    return x


def erde(rng):
    """Hacke in Erde: dumpf und weich, mit rieselnder Krume."""
    t = zeit(0.3)
    x = 0.9 * stoss(t, 120, 900, 0.035, rng)
    x += 0.4 * moden(t, [rng.uniform(70, 95)], [0.04], [1.0], rng)
    x += 0.3 * knistern(t, 10, 0.15, 1200, 5000, rng, 0.08)
    return x


def rascheln(rng):
    """Sichel durch Beerenbusch und Halme: kurzes Rascheln von Laub."""
    t = zeit(0.32)
    huelle = np.sin(np.pi * np.clip(t / 0.25, 0, 1)) ** 2
    rauschen = band(rng.standard_normal(len(t)), 2200, 8000)
    flattern = 0.6 + 0.4 * np.sin(2 * np.pi * rng.uniform(28, 40) * t)
    x = rauschen * huelle * flattern
    x += 0.4 * knistern(t, 12, 0.22, 3000, 9000, rng, 0.2)
    return x


def schwert(rng):
    """Klinge auf Klinge: ein angeschlagener Stab klingt in unharmonischen Teiltönen lange nach."""
    t = zeit(0.85)
    f1 = rng.uniform(720, 940)
    ratios = [1.0, 2.76, 5.40, 8.93, 13.34]
    freqs = [f1 * r for r in ratios]
    x = moden(t, freqs, [0.42, 0.26, 0.15, 0.09, 0.06], [0.55, 0.8, 0.5, 0.33, 0.2], rng)
    # zwei leicht verstimmte Klingen: Schwebung
    x += moden(t, [f * 1.004 + 2 for f in freqs[:3]], [0.35, 0.2, 0.12], [0.3, 0.45, 0.25], rng)
    x += 0.9 * stoss(t, 3000, 12000, 0.004, rng)
    x += 0.15 * stoss(t, 2000, 7000, 0.06, rng)
    return x


def treffer(rng):
    """Schlag auf Körper und Kleidung: dumpfer Stoß mit kurzem Zischen der Klinge."""
    t = zeit(0.3)
    f = np.linspace(rng.uniform(130, 160), 55, len(t))
    x = np.sin(2 * np.pi * np.cumsum(f) / SR) * abklingen(t, 0.06)
    x += 0.55 * tief(rng.standard_normal(len(t)), 1500) * abklingen(t, 0.025)
    x += 0.3 * stoss(t, 2000, 6000, 0.015, rng)
    return x


def gebaeude(rng):
    """Waffe gegen Wand und Balken: schwerer Holzschlag, es rappelt nach."""
    t = zeit(0.5)
    f0 = rng.uniform(85, 115)
    x = moden(t, [f0, f0 * 2.1, f0 * 3.3, f0 * 5.0], [0.12, 0.07, 0.04, 0.02], [1.0, 0.6, 0.4, 0.2], rng)
    x += 0.6 * stoss(t, 300, 1800, 0.03, rng)
    x += 0.5 * stoss(t, 1500, 7000, 0.004, rng)
    x += 0.35 * knistern(t, 16, 0.3, 800, 4000, rng, 0.12)
    return x


def bogen(rng):
    """Bogenschuss: die Sehne schnellt, der Pfeil zischt davon."""
    t = zeit(0.45)
    sehne = karplus(rng.uniform(140, 180), 0.45, 0.985, rng)
    sehne = tief(sehne, 2500) * abklingen(t, 0.09)
    zisch = rng.standard_normal(len(t))
    # das Band wandert nach oben, während der Pfeil wegfliegt
    zisch = np.concatenate([band(seg, lo, lo * 3) for seg, lo in
                            zip(np.array_split(zisch, 6), np.geomspace(700, 2600, 6))])
    huelle = np.clip((t - 0.02) / 0.05, 0, 1) * abklingen(np.clip(t - 0.07, 0, None), 0.08)
    return 1.0 * sehne + 0.45 * zisch * huelle + 0.4 * stoss(t, 1500, 6000, 0.003, rng)


def tod(rng):
    """Eine Figur fällt: dumpfer Aufschlag am Boden, die Ausrüstung scheppert kurz."""
    t = zeit(0.45)
    x = moden(t, [rng.uniform(60, 80), rng.uniform(140, 170)], [0.09, 0.05], [1.0, 0.4], rng)
    x += 0.7 * tief(rng.standard_normal(len(t)), 700) * abklingen(t, 0.06)
    x += 0.25 * knistern(t, 8, 0.12, 2000, 8000, rng, 0.08)
    return x


def einsturz(rng):
    """Ein Gebäude bricht zusammen: Grollen, splitternde Balken, rieselnder Schutt."""
    t = zeit(1.8)
    grollen = tief(rng.standard_normal(len(t)), 180, 4) * np.clip(t / 0.06, 0, 1) * abklingen(t, 0.55)
    x = 2.2 * grollen
    for _ in range(26):
        start = rng.uniform(0.0, 1.0) ** 1.6
        dauer = 0.3
        i = int(start * SR)
        tt = zeit(dauer)
        f0 = rng.uniform(120, 320)
        knack = moden(tt, [f0, f0 * 2.4, f0 * 4.0], [0.05, 0.03, 0.015], [1.0, 0.5, 0.3], rng)
        knack += 0.8 * stoss(tt, 1500, 8000, 0.004, rng)
        knack *= rng.uniform(0.3, 1.0) * np.exp(-start / 0.8)
        n = min(len(knack), len(x) - i)
        x[i:i + n] += 0.6 * knack[:n]
    x += 0.35 * band(rng.standard_normal(len(t)), 900, 5000) * np.clip(t / 0.1, 0, 1) * abklingen(t, 0.5)
    return x


GERAEUSCHE = {
    "axt": axt, "spitzhacke": spitzhacke, "hammer": hammer, "erde": erde, "rascheln": rascheln,
    "schwert": schwert, "treffer": treffer, "gebaeude": gebaeude, "bogen": bogen, "tod": tod,
    "einsturz": einsturz,
}


def schreibe(pfad, x):
    x = anschlag(np.asarray(x, dtype=np.float64))
    x = x - np.mean(x)
    x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (-2 / 20)
    daten = (x * 32767).astype("<i2").tobytes()
    with wave.open(str(pfad), "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(SR)
        w.writeframes(daten)


def vorschau(pfad):
    """Alle Geräusche mit allen Varianten hintereinander in eine Datei zum Anhören - je Variante
    0,4 s Pause, zwischen den Geräuschen 1 s. Gibt aus, wann welches Geräusch beginnt."""
    stille = lambda s: np.zeros(int(s * SR))
    teile, start = [], 0.0
    for name, erzeuge in GERAEUSCHE.items():
        print(f"{start:6.1f} s  {name}")
        for n in range(1, VARIANTEN + 1):
            x = anschlag(np.asarray(erzeuge(np.random.default_rng(sum(map(ord, name)) * 100 + n)), dtype=np.float64))
            x = x / (np.max(np.abs(x)) + 1e-9) * 10 ** (-2 / 20)
            teile += [x, stille(0.4)]
            start += len(x) / SR + 0.4
        teile.append(stille(1.0))
        start += 1.0
    pfad.parent.mkdir(parents=True, exist_ok=True)
    schreibe(pfad, np.concatenate(teile))
    print(f"{pfad} ({start:.0f} s)")


def main():
    args = sys.argv[1:]
    if args[:1] == ["--vorschau"]:
        vorschau(Path(__file__).resolve().parent / "ausgabe" / "vorschau.wav")
        return
    ziel = Path(args.pop(0)) if args and not args[0] in GERAEUSCHE else \
        Path(__file__).resolve().parents[2] / "AgeOfEvolutions/AgeOfEvolutions.Core/Content/Sounds"
    namen = args or list(GERAEUSCHE)
    ziel.mkdir(parents=True, exist_ok=True)
    for name in namen:
        for n in range(1, VARIANTEN + 1):
            # fester Samen je Geräusch und Variante: jeder Lauf ergibt dieselben Dateien
            rng = np.random.default_rng(sum(map(ord, name)) * 100 + n)
            schreibe(ziel / f"{name}_{n}.wav", GERAEUSCHE[name](rng))
        print(f"{name}: {VARIANTEN} Varianten")


if __name__ == "__main__":
    main()
