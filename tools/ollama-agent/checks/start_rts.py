"""Startprobe: das frisch gebaute Spiel startet mit --rts und läuft LAUFZEIT Sekunden.

Entstanden am 2026-10-05 (G7): eine Änderung rief DrawWheat - das selbst
spriteBatch.Begin aufruft - innerhalb eines offenen SpriteBatch auf. Build, Tests,
Spielablauf und die Strukturprüfungen waren grün, denn keine davon zeichnet; das
Spiel wäre beim ersten Bild der Karte abgestürzt. Diese Probe zeichnet wirklich.

  py start_rts.py gl    DesktopGL-Fassung (bin/Debug)
  py start_rts.py dx    WindowsDX-Fassung (bin/Debug)

Vorher muss die Fassung gebaut sein. Für LAUFZEIT Sekunden erscheint das Spielfenster.
"""
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

EXE = {
    "gl": Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0/AgeOfEvolutions.exe"),
    "dx": Path("AgeOfEvolutions/AgeOfEvolutions.WindowsDX/bin/Debug/net10.0-windows/AgeOfEvolutions.exe"),
}
LAUFZEIT = 10

fassung = sys.argv[1] if len(sys.argv) > 1 else "gl"
exe = EXE[fassung]
fehler = []
if not exe.is_file():
    fehler.append(f"{exe} fehlt - Fassung nicht gebaut")
else:
    proz = subprocess.Popen([str(exe.resolve()), "--rts"], cwd=exe.parent,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
        letzte = ausgabe.decode("utf-8", "replace").strip().splitlines()[:6]
        fehler.append(f"{fassung}: das Spiel endete nach weniger als {LAUFZEIT} s (exit {proz.returncode}): "
                      + " | ".join(letzte))
    except subprocess.TimeoutExpired:
        proz.kill()
        proz.communicate()

melde(fehler, f"Startprobe {fassung}: das Spiel läuft mit --rts {LAUFZEIT} s")
