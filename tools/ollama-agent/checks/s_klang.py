"""Abnahme S1-S3: die Geräusche aus tools/klang sind im Spiel eingebaut.

Nutzer 2026-10-11: "die sound effekte sind scheinbar generiert aber noch nicht eingebaut".
Was wann klingt, prüfen die Gruppen klangbrett, arbeitsklang und kampfklang in
tools/spielablauf. Hier steht, was die nicht sehen: die Dateien sind in der Content-Pipeline
und gebaut, die Vertragsrümpfe sind gefüllt, Update und LoadContent binden ein - und beim
echten Start lädt das Spiel alle Varianten (ohne Spielfenster gibt es kein Audiogerät).

  py s_klang.py brett          S1: SoundBoard (Load, Count, Allow, Play)
  py s_klang.py arbeit         S2: dazu EmitSound, SoundMix, StrikeLanded, WorkSoundFor,
                               UpdateWorkSounds, die Einbindung und der Start (gl)
  py s_klang.py kampf          S3: dazu UnitHitSound

Mehrere Teile gehen zusammen (py s_klang.py brett arbeit kampf). Vorher muss die
DesktopGL-Fassung gebaut sein.
"""
import re
import subprocess
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
BOARD = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Audio/SoundBoard.cs")
GL = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0")
NAMEN = ["axt", "spitzhacke", "hammer", "erde", "rascheln",
         "schwert", "treffer", "gebaeude", "bogen", "tod", "einsturz"]
VARIANTEN = 3
LAUFZEIT = 8

teile = set(sys.argv[1:]) or {"brett", "arbeit", "kampf"}
fehler = []

# Die Dateien: erzeugt, eingetragen, gebaut
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
for name in NAMEN:
    for n in range(1, VARIANTEN + 1):
        rel = f"Sounds/{name}_{n}.wav"
        if not (CONTENT / rel).is_file():
            fehler.append(f"{rel} fehlt - tools/klang/klang.py nicht gelaufen")
        if f"#begin {rel}\n" not in mgcb or f"/build:{rel}\n" not in mgcb:
            fehler.append(f"Content-Pipeline: {rel} nicht eingetragen")
        if not (GL / f"Content/Sounds/{name}_{n}.xnb").is_file():
            fehler.append(f"{GL}/Content/Sounds/{name}_{n}.xnb fehlt - DesktopGL nicht gebaut?")


def gefuellt(text, datei, namen):
    for name in namen:
        rumpf = methode(text, name)
        if rumpf is None:
            fehler.append(f"{datei}: Methode {name} nicht gefunden")
        elif "NotImplementedException" in rumpf:
            fehler.append(f"{datei}: {name} wirft noch NotImplementedException")


board = lies(BOARD)
gefuellt(board, "SoundBoard", ["Load", "Count", "Allow", "Play"])
if not re.search(r"Load\s*<\s*SoundEffect\s*>", methode(board, "Load") or ""):
    fehler.append("SoundBoard.Load lädt nicht per content.Load<SoundEffect>")
if not re.search(r"\.Play\s*\(", methode(board, "Play") or ""):
    fehler.append("SoundBoard.Play ruft SoundEffect.Play nicht auf")

screen = lies()
if "arbeit" in teile:
    gefuellt(screen, "RTSGameplayScreen",
             ["EmitSound", "SoundMix", "StrikeLanded", "WorkSoundFor", "UpdateWorkSounds"])
    if not re.search(r"\bUpdateWorkSounds\s*\(", methode(screen, "Update") or ""):
        fehler.append("Update ruft UpdateWorkSounds nicht auf")
    laden = methode(screen, "LoadContent") or ""
    if not re.search(r"new\s+SoundBoard\s*\(", laden) or not re.search(r"\.Load\s*\(", laden):
        fehler.append("LoadContent legt kein SoundBoard an oder lädt es nicht")
    if not re.search(r"_sounds\s*\.\s*Play\s*\(", methode(screen, "EmitSound") or ""):
        fehler.append("EmitSound spielt nicht über _sounds.Play")
if "kampf" in teile:
    gefuellt(screen, "RTSGameplayScreen", ["UnitHitSound"])

# Der echte Start: das Spiel meldet, wie viele Varianten es geladen hat
if "arbeit" in teile and not fehler:
    exe = GL / "AgeOfEvolutions.exe"
    if not exe.is_file():
        fehler.append(f"{exe} fehlt - DesktopGL nicht gebaut")
    else:
        proz = subprocess.Popen([str(exe.resolve()), "--rts"], cwd=exe.parent,
                                stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
        try:
            ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
            fehler.append(f"das Spiel endete nach weniger als {LAUFZEIT} s (exit {proz.returncode}): "
                          + " | ".join(ausgabe.decode("utf-8", "replace").strip().splitlines()[:6]))
        except subprocess.TimeoutExpired:
            proz.kill()
            ausgabe, _ = proz.communicate()
            text = ausgabe.decode("utf-8", "replace")
            soll = len(NAMEN) * VARIANTEN
            gemeldet = re.search(r"\[Klang\]\s*(\d+)\s+von\s+(\d+)", text)
            if not gemeldet:
                fehler.append("beim Start keine Zeile '[Klang] <n> von <m> ...' - LoadContent meldet nicht, was es geladen hat")
            elif (int(gemeldet.group(1)), int(gemeldet.group(2))) != (soll, soll):
                fehler.append(f"beim Start geladen: {gemeldet.group(1)} von {gemeldet.group(2)} Varianten, erwartet {soll} von {soll}")

melde(fehler, f"Klang ({', '.join(sorted(teile))}): {len(NAMEN) * VARIANTEN} Dateien eingetragen und gebaut, "
              f"Rümpfe gefüllt" + (", beim Start alle geladen" if "arbeit" in teile else ""))
