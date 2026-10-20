"""Startprobe Hauptmenü (U2b): das frisch gebaute Spiel startet OHNE Schalter ins
Hauptmenü und läuft LAUFZEIT Sekunden.

start_rts.py springt mit --rts direkt ins Spiel - das Hauptmenü zeichnet dabei nie.
Diese Probe zeichnet es wirklich: Draw ruft RefreshUpdateEntry, EntryVisible,
EntryEnabled, VersionRect und UpdateService.VersionLine auf; wirft einer davon, endet
das Spiel vor der Zeit. Daneben laufen die Update-Prüfung (echtes Netz, ohne Netz
„nicht möglich“) und CleanupAfterUpdate über den bin-Ordner - beides harmlos.

  py start_menue.py gl    DesktopGL-Fassung (bin/Debug)
  py start_menue.py dx    WindowsDX-Fassung (bin/Debug)

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
    proz = subprocess.Popen([str(exe.resolve())], cwd=exe.parent,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
        letzte = ausgabe.decode("utf-8", "replace").strip().splitlines()[:6]
        fehler.append(f"{fassung}: das Hauptmenü endete nach weniger als {LAUFZEIT} s (exit {proz.returncode}): "
                      + " | ".join(letzte))
    except subprocess.TimeoutExpired:
        proz.kill()
        proz.communicate()

melde(fehler, f"Startprobe {fassung}: das Hauptmenü läuft {LAUFZEIT} s")
