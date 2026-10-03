"""Abnahme E11: Die WindowsDX-Fassung startet auch mit physischen Pixeln.

Absturz bis 2026-10-03: Ohne Angabe nutzt MonoGame das Profil Reach, unter
DirectX ein Geraet mit Feature Level 9_3 - der Bildpuffer darf dort hoechstens
4096 Pixel breit sein. Das Spiel fordert 80 % des Bildschirms an. Laeuft es mit
"Hohe DPI-Skalierung ueberschreiben: Anwendung", sind das auf dem
6K-Bildschirm 4812x2707 physische Pixel, und CreateSwapChain scheitert mit
E_INVALIDARG.

Geprueft wird das Verhalten: die frisch gebaute DX-Fassung startet mit
__COMPAT_LAYER=HIGHDPIAWARE (dieselbe Wirkung wie die Windows-Einstellung, nur
ohne Registry) und muss nach LAUFZEIT Sekunden noch laufen. Auf einem
Bildschirm bis 5120 Pixel Breite stuerzte auch die alte Fassung nicht ab - das
Skript meldet das, damit ein Gruen dort nicht mehr verspricht, als es prueft.

Vorher muss die DX-Fassung gebaut sein (Debug), das erledigt der verify-Schritt
davor. Fuer LAUFZEIT Sekunden erscheint das Spielfenster.
"""

import ctypes
import os
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde  # noqa: E402

SPIEL = Path("AgeOfEvolutions/AgeOfEvolutions.Core/AgeOfEvolutionsGame.cs")
EXE = Path("AgeOfEvolutions/AgeOfEvolutions.WindowsDX/bin/Debug/net10.0-windows/AgeOfEvolutions.exe")
LAUFZEIT = 10

fehler = []
text = lies(SPIEL)

if not re.search(r"\.GraphicsProfile\s*=\s*(?:[\w.]+\.)?GraphicsProfile\.HiDef\b", text):
    fehler.append("Grafikprofil wird nicht auf HiDef gestellt")
if re.search(r"GraphicsProfile\.Reach\b", text):
    fehler.append("GraphicsProfile.Reach steht noch im Spiel")
if "AOE_DX_DIAG" in text:
    fehler.append("Diagnoseblock AOE_DX_DIAG ist noch da")

# Physische Breite des Hauptbildschirms, unabhaengig von der DPI-Skalierung
user32 = ctypes.windll.user32
user32.SetProcessDpiAwarenessContext(ctypes.c_void_p(-4))
breite = user32.GetSystemMetrics(0)
if breite * 0.8 <= 4096:
    print(f"HINWEIS: Bildschirm {breite} Pixel breit - der alte Absturz trat erst ab 5121 auf, "
          "der Starttest ist hier zahnlos.")

if not EXE.is_file():
    fehler.append(f"{EXE} fehlt - DX-Fassung nicht gebaut")
else:
    umgebung = dict(os.environ, __COMPAT_LAYER="HIGHDPIAWARE")
    proz = subprocess.Popen([str(EXE)], cwd=EXE.parent, env=umgebung,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
        letzte = ausgabe.decode("utf-8", "replace").strip().splitlines()[:4]
        fehler.append(f"DX-Fassung nach weniger als {LAUFZEIT} s beendet (exit {proz.returncode}): "
                      + " | ".join(letzte))
    except subprocess.TimeoutExpired:
        proz.kill()
        proz.communicate()

melde(fehler, f"E11 erfuellt: DX-Fassung laeuft mit HIGHDPIAWARE {LAUFZEIT} s (Bildschirm {breite} px)")
