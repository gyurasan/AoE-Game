"""Baut die Geräusche für Arbeit und Kampf aus echten Aufnahmen von Freesound - alle unter CC0
(gemeinfrei, ohne Namensnennung nutzbar; die Urheber stehen trotzdem in QUELLEN).

Bis 2026-10-13 waren die Geräusche synthetisch (gedämpfte Sinus-Eigenschwingungen und gefiltertes
Rauschen). Feedback: „die generierten sounds passen nicht zu den handlungen" - ein Holzfäller soll
nach Axt in Holz klingen, nicht nach einem Holzblock-Instrument. Seitdem schneidet das Skript aus
Aufnahmen der echten Handlung je Geräusch drei Einzelschläge heraus (SCHNITTE): Baumfällen mit der
Axt, Spitzhacke auf Stein, Hammer auf Nagel und Brett, Schaufel in Erde, Schnitt durch Pflanzen,
Klinge auf Klinge, Schlag auf Körper, Axt gegen Holztür, Bogensehne, Körper fällt, Holzhaus stürzt ein.

Ablauf je Schnitt: Freesound-Vorschau (OGG, 44,1 kHz) einmal nach tools/klang/ausgabe/roh laden,
mit ffmpeg nach mono dekodieren, ab dem Einsatz des Schlags ausschneiden (der Schlag sitzt am
Dateianfang, damit er mit dem Aufschlag der Animation zusammenfällt), Rumpeln unter 40 Hz
wegfiltern, das letzte Drittel ausblenden, auf die Lautheit des Geräuschs bringen (LAUTHEIT: die
lauteste 50-ms-Strecke, wie bei den alten Geräuschen gemessen) und die Spitze auf -1 dBFS begrenzen
(Begrenzer mit 4 ms Vorlauf, höchstens 6 dB).
Die Länge eines Schnitts bleibt unter dem Schlagabstand des Werkzeugs (ToolStyles.Rate im Screen).

Braucht numpy, scipy und ffmpeg im PATH - die Python von ComfyUI hat beides:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/klang/klang.py [ziel] [name ...]

Ohne Ziel schreibt es nach AgeOfEvolutions/AgeOfEvolutions.Core/Content/Sounds. Ausgabe: 16 Bit,
mono, 44,1 kHz; Dateiname <name>_<n>.wav. Mit --vorschau entsteht statt dessen
tools/klang/ausgabe/vorschau.wav: alle Varianten hintereinander zum Anhören, mit Zeitplan.
"""
import subprocess
import sys
import urllib.request
import wave
from pathlib import Path

import numpy as np
from scipy import ndimage, signal

SR = 44100
HIER = Path(__file__).resolve().parent
ROH = HIER / "ausgabe" / "roh"
CDN = "https://cdn.freesound.org/previews"

# Freesound-id: (Urheber, Titel, Vorschau ohne "-hq.ogg"). Seite: https://freesound.org/s/<id>/
QUELLEN = {
    401730: ("tomattka", "Felling a tree with an axe", "401/401730_6351775"),
    753925: ("xkeril", "Axe chopping wood", "753/753925_13504080"),
    184383: ("qubodup", "Gathering Stone Resources", "184/184383_71257"),
    431019: ("CamoMano", "Stone hit", "431/431019_5635287"),
    204946: ("CapsLok", "Hammering_Wood", "204/204946_850742"),
    540139: ("aidansamuel", "Hitting nails with Hammer", "540/540139_10965608"),
    503672: ("RavenWolfProds", "Shovel in dirt", "503/503672_9619518"),
    353907: ("dr19", "Shovel_dirt", "353/353907_5984825"),
    488378: ("wobesound", "Harvest Sounds", "488/488378_10523948"),
    327894: ("Kreastricon62", "Bush Cut", "327/327894_4336114"),
    364531: ("Christopherderp", "Swords Clash - High Quality #3", "364/364531_3908740"),
    442769: ("qubodup", "Sword Hit", "442/442769_71257"),
    471095: ("spycrah", "Sword clash 1", "471/471095_9856191"),
    797822: ("HECKFRICKER", "Thwack 02", "797/797822_12863902"),
    797821: ("HECKFRICKER", "Thwack 01", "797/797821_12863902"),
    420673: ("SypherZent", "Basic Melee Hit", "420/420673_6544098"),
    481105: ("craigsmith", "R27-18 Axe Hits Wood Door", "481/481105_2524442"),
    394179: ("saturdaysoundguy", "Longbow Release 2", "394/394179_7280678"),
    394185: ("saturdaysoundguy", "Arrow Loose", "394/394185_7280678"),
    384918: ("Ali_6868", "Bow Release (Bow and Arrow) 3", "384/384918_984733"),
    504626: ("leonelmail", "BODY FALL - V HVY - DIRT", "504/504626_4437257"),
    346694: ("deleted_user_2104797", "Body fall_02", "346/346694_2104797"),
    614064: ("magnuswaker", "Bodyfall", "614/614064_11537497"),
    675900: ("craigsmith", "S37-15 Wooden building collapses quickly", "675/675900_2524442"),
    721600: ("DasWaff", "Bakers house collapse", "721/721600_2617407"),
    487142: ("craigsmith", "R29-04 Crashing Collapse of Wood Building", "487/487142_2524442"),
}

# Je Geräusch drei Varianten: (Freesound-id, Beginn in s, Länge in s). Der Beginn liegt 4 ms vor
# dem Einsatz des Schlags in der Aufnahme.
SCHNITTE = {
    # Axt in den Stamm (Rate 1,0/s)
    "axt": [(401730, 2.937, 0.50), (401730, 13.735, 0.45), (753925, 5.869, 0.50)],
    # Spitzhacke auf Stein, mit wegspringendem Geröll (Rate 0,9/s)
    "spitzhacke": [(184383, 6.170, 0.55), (184383, 7.662, 0.55), (431019, 0.039, 0.31)],
    # Hammer auf Nagel und Brett (Rate 1,6/s)
    "hammer": [(204946, 2.036, 0.30), (204946, 8.962, 0.30), (540139, 2.153, 0.40)],
    # Hacke auf dem Feld: Blatt sticht in die Erde (Rate 0,9/s)
    "erde": [(503672, 2.861, 0.38), (353907, 0.320, 0.32), (503672, 5.232, 0.38)],
    # Sichel durch den Beerenbusch (Rate 1,3/s)
    "rascheln": [(488378, 1.561, 0.45), (327894, 0.046, 0.42), (488378, 4.604, 0.45)],
    # Klinge auf Klinge
    "schwert": [(364531, 0.019, 0.70), (442769, 0.000, 0.70), (471095, 0.992, 0.70)],
    # Schlag auf Körper und Kleidung
    "treffer": [(797822, 0.000, 0.17), (797821, 0.000, 0.21), (420673, 0.000, 0.17)],
    # Waffe gegen Holzwand
    "gebaeude": [(481105, 1.263, 0.60), (481105, 11.617, 0.60), (481105, 19.814, 0.60)],
    # Bogensehne beim Schuss
    "bogen": [(394179, 0.102, 0.19), (394185, 0.092, 0.17), (384918, 0.077, 0.40)],
    # Eine Figur fällt zu Boden
    "tod": [(504626, 0.330, 1.10), (346694, 0.420, 0.80), (614064, 0.020, 0.90)],
    # Ein Holzhaus stürzt ein
    "einsturz": [(675900, 0.078, 3.00), (721600, 0.080, 3.00), (487142, 0.000, 3.00)],
}

# Lautheit je Geräusch in dBFS: die lauteste 50-ms-Strecke (RMS). Grundlage sind die alten
# synthetischen Geräusche, damit die Mischung im Spiel gleich bleibt; Feld und Busch etwas leiser.
LAUTHEIT = {
    "axt": -12, "spitzhacke": -13, "hammer": -13, "erde": -15, "rascheln": -17,
    "schwert": -12, "treffer": -11, "gebaeude": -11, "bogen": -14, "tod": -11, "einsturz": -11,
}
SPITZE = 10 ** (-1 / 20)


# ------------------------------------------------------------------ Aufnahmen
def aufnahme(sid):
    """Die Vorschau zu sid als mono float64 bei SR - beim ersten Mal von Freesound geladen."""
    ogg = ROH / f"{sid}.ogg"
    if not ogg.exists():
        ROH.mkdir(parents=True, exist_ok=True)
        anfrage = urllib.request.Request(f"{CDN}/{QUELLEN[sid][2]}-hq.ogg", headers={"User-Agent": "Mozilla/5.0"})
        ogg.write_bytes(urllib.request.urlopen(anfrage, timeout=60).read())
    roh = subprocess.run(["ffmpeg", "-v", "error", "-i", str(ogg), "-ac", "1", "-ar", str(SR), "-f", "f32le", "-"],
                         capture_output=True, check=True).stdout
    return np.frombuffer(roh, "<f4").astype(np.float64)


def lautheit(x):
    """Die lauteste 50-ms-Strecke als RMS."""
    h = int(0.05 * SR)
    m = len(x) // h
    if m == 0:
        return np.sqrt(np.mean(x ** 2))
    return np.sqrt(np.mean(x[:m * h].reshape(m, h) ** 2, axis=1)).max()


def schnitt(name, sid, beginn, laenge):
    """Ein Schlag aus der Aufnahme, gefiltert, ausgeblendet und auf die Lautheit von name gebracht."""
    x = aufnahme(sid)
    i = int(beginn * SR)
    x = x[i:i + int(laenge * SR)].copy()
    x = signal.sosfilt(signal.butter(2, 40, btype="highpass", fs=SR, output="sos"), x)
    n = int(0.002 * SR)
    x[:n] *= np.linspace(0, 1, n)
    m = int(len(x) / 3)
    x[-m:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, m))
    # Auf die Lautheit bringen, aber höchstens 6 dB über das, was die Spitze ohne Begrenzer erlaubt -
    # den Rest fängt der Begrenzer ab, sonst klänge ein Schlag mit scharfer Spitze leiser als seine
    # Geschwister
    spitze = np.max(np.abs(x)) + 1e-9
    x *= min(10 ** (LAUTHEIT[name] / 20) / (lautheit(x) + 1e-9), 2 * SPITZE / spitze)
    x = begrenzer(x)
    spitze = np.max(np.abs(x))
    return x * (SPITZE / spitze) if spitze > SPITZE else x


def begrenzer(x, vorlauf=0.004):
    """Drückt Spitzen über SPITZE mit 4 ms Vorlauf herunter: die nötige Absenkung je Abtastwert,
    über ±vorlauf das Minimum genommen und über vorlauf geglättet - so liegt die Absenkung an
    jeder Spitze vollständig an und setzt weich ein und aus."""
    n = int(vorlauf * SR)
    g = np.minimum(1.0, SPITZE / (np.abs(x) + 1e-12))
    g = ndimage.minimum_filter1d(g, 2 * n + 1)
    g = ndimage.uniform_filter1d(g, n + 1)
    return x * g


def schreibe(pfad, x):
    daten = (np.clip(x, -1, 1) * 32767).astype("<i2").tobytes()
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
    for name, varianten in SCHNITTE.items():
        print(f"{start:6.1f} s  {name}")
        for sid, beginn, laenge in varianten:
            x = schnitt(name, sid, beginn, laenge)
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
        vorschau(HIER / "ausgabe" / "vorschau.wav")
        return
    ziel = Path(args.pop(0)) if args and not args[0] in SCHNITTE else \
        HIER.parents[1] / "AgeOfEvolutions/AgeOfEvolutions.Core/Content/Sounds"
    namen = args or list(SCHNITTE)
    ziel.mkdir(parents=True, exist_ok=True)
    for name in namen:
        for n, (sid, beginn, laenge) in enumerate(SCHNITTE[name], 1):
            schreibe(ziel / f"{name}_{n}.wav", schnitt(name, sid, beginn, laenge))
        print(f"{name}: {len(SCHNITTE[name])} Varianten")


if __name__ == "__main__":
    main()
