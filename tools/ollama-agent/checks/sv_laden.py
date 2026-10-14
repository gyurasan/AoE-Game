"""Ladeprobe (SV3): ein Spielstand startet im echten Spiel und läuft LAUFZEIT Sekunden.

tools/spielablauf (Gruppe speichern) schreibt mitten im Spiel einen Spielstand
(AOE_SPIELSTAND_DATEI); das gebaute Spiel startet damit über den Schalter --laden.
Build, Tests und Spielablauf zeichnen nichts - erst diese Probe zeigt, dass
LoadContent die geladene Welt auch wirklich aufbaut und zeichnet. Bestanden ist
sie nur, wenn der Spielbildschirm geladen hat ("[Klang]" steht in der Ausgabe -
das schreibt nur RTSGameplayScreen.LoadContent) und kein "[Laden]"-Fehler kam;
sonst wäre das Spiel einfach im Hauptmenü weitergelaufen.

  py sv_laden.py gl    DesktopGL-Fassung (bin/Debug)
  py sv_laden.py dx    WindowsDX-Fassung (bin/Debug)
"""
import os
import subprocess
import sys
import tempfile
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
datei = Path(tempfile.gettempdir()) / f"aoe_sv_laden_{os.getpid()}.json"
fehler = []
try:
    lauf = subprocess.run(["dotnet", "run", "--project", "tools/spielablauf", "-v", "q", "--nologo", "--", "speichern"],
                          env=dict(os.environ, AOE_SPIELSTAND_DATEI=str(datei)),
                          capture_output=True, timeout=900)
    if lauf.returncode != 0 or not datei.is_file():
        letzte = lauf.stdout.decode("utf-8", "replace").strip().splitlines()[-6:]
        fehler.append(f"spielablauf speichern: exit {lauf.returncode}, Spielstand "
                      f"{'da' if datei.is_file() else 'fehlt'}: " + " | ".join(letzte))
    elif not exe.is_file():
        fehler.append(f"{exe} fehlt - Fassung nicht gebaut")
    else:
        proz = subprocess.Popen([str(exe.resolve()), "--laden", str(datei)], cwd=exe.parent,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        try:
            ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
            letzte = ausgabe.decode("utf-8", "replace").strip().splitlines()[:6]
            fehler.append(f"{fassung}: das Spiel endete nach weniger als {LAUFZEIT} s (exit {proz.returncode}): "
                          + " | ".join(letzte))
        except subprocess.TimeoutExpired:
            proz.kill()
            ausgabe, _ = proz.communicate()
            text = ausgabe.decode("utf-8", "replace")
            if "[Laden]" in text:
                zeile = next(z for z in text.splitlines() if "[Laden]" in z)
                fehler.append(f"{fassung}: der Spielstand wurde nicht geladen: {zeile.strip()}")
            elif "[Klang]" not in text:
                fehler.append(f"{fassung}: kein Spielbildschirm geladen (keine [Klang]-Zeile) - "
                              "läuft das Spiel im Menü statt mit dem Spielstand?")
finally:
    datei.unlink(missing_ok=True)

melde(fehler, f"Ladeprobe {fassung}: ein Spielstand aus spielablauf läuft mit --laden {LAUFZEIT} s")
