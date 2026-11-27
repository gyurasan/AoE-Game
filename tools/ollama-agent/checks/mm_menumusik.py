"""Abnahme MM1/MM2: das Haupt-Thema läuft im Menü als Hintergrundmusik.

Nutzer 2026-11-27: "Age of Evolutions - Main Theme.mp3 sollte im Menü als hintergrund musik
verwendet werden". Die Lautstärke-Formel und das Verhalten ohne Datei prüfen die
MenuMusicTests in tests/AoE.Tests. Hier steht, was die nicht sehen: das Lied ist in der
Content-Pipeline und für beide Plattformen gebaut, die Vertragsrümpfe sind gefüllt, der
BackgroundScreen bindet ein - und beim echten Start läuft das Lied (Logzeile im Spiel-Log).

  py mm_menumusik.py musik         MM1: MenuMusic (Volume, Start, SetVolume, Stop) + Content
  py mm_menumusik.py einbindung    MM2: BackgroundScreen (LoadContent, Update, UnloadContent)
  py mm_menumusik.py start         Start der DesktopGL-Exe ins Menü: Logzeile "läuft"
  py mm_menumusik.py start_dx      dasselbe mit der WindowsDX-Exe

Mehrere Teile gehen zusammen. Für start/start_dx muss die jeweilige Fassung gebaut sein.
Achtung: der Start spielt die Musik einige Sekunden hörbar ab (Einstellungen aus %APPDATA%).
"""
import re
import subprocess
import sys
import time
from datetime import date
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CORE = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
CONTENT = CORE / "Content"
MUSIC = CORE / "Audio/MenuMusic.cs"
BACKGROUND = CORE / "Screens/BackgroundScreen.cs"
GL = Path("AgeOfEvolutions/AgeOfEvolutions.DesktopGL/bin/Debug/net10.0")
DX = Path("AgeOfEvolutions/AgeOfEvolutions.WindowsDX/bin/Debug/net10.0-windows")
LAUFZEIT = 10
DAUER = 219  # Sekunden, ffprobe: 00:03:39.60

teile = set(sys.argv[1:]) or {"musik", "einbindung"}
fehler = []


def gefuellt(text, datei, namen):
    for name in namen:
        rumpf = methode(text, name)
        if rumpf is None:
            fehler.append(f"{datei}: Methode {name} nicht gefunden")
        elif "NotImplementedException" in rumpf:
            fehler.append(f"{datei}: {name} wirft noch NotImplementedException")


def ohne_kommentare(rumpf):
    return re.sub(r"//[^\n]*", "", rumpf or "")


# Content: Datei, Eintrag, gebaut (DX nur, wenn gebaut)
if "musik" in teile:
    mp3 = CONTENT / "Sounds/MainTheme.mp3"
    if not mp3.is_file():
        fehler.append(f"{mp3} fehlt")
    mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
    eintrag = ("#begin Sounds/MainTheme.mp3\n/importer:Mp3Importer\n/processor:SongProcessor\n"
               "/processorParam:Quality=Best\n/build:Sounds/MainTheme.mp3\n")
    if eintrag not in mgcb:
        fehler.append("Content-Pipeline: Sounds/MainTheme.mp3 nicht (als Mp3Importer/SongProcessor) eingetragen")
    for ordner, endung in ((GL, "ogg"), (DX, "wma")):
        if ordner == DX and not ordner.is_dir():
            continue
        for datei in (f"MainTheme.xnb", f"MainTheme.{endung}"):
            if not (ordner / "Content/Sounds" / datei).is_file():
                fehler.append(f"{ordner}/Content/Sounds/{datei} fehlt - nicht gebaut?")

    musik = lies(MUSIC)
    gefuellt(musik, "MenuMusic", ["Volume", "Start", "SetVolume", "Stop"])
    vol = ohne_kommentare(methode(musik, "Volume"))
    if vol.count("Math.Clamp") < 2 or "LEVEL" not in vol:
        fehler.append("MenuMusic.Volume: zweimal Math.Clamp (Prozent, Blende) und LEVEL erwartet")
    start = ohne_kommentare(methode(musik, "Start"))
    for muster, was in ((r"Load\s*<\s*Song\s*>\s*\(\s*ASSET", "content.Load<Song>(ASSET)"),
                        (r"MediaPlayer\.IsRepeating\s*=\s*true", "MediaPlayer.IsRepeating = true"),
                        (r"MediaPlayer\.Play\s*\(\s*_song\s*\)", "MediaPlayer.Play(_song)"),
                        (r"catch\s*\(\s*Exception", "catch (Exception ...)"),
                        (r"GameLog\.Info\s*\(", "GameLog.Info"),
                        (r"GameLog\.Warn\s*\(", "GameLog.Warn")):
        if not re.search(muster, start):
            fehler.append(f"MenuMusic.Start: {was} fehlt")
    stop = ohne_kommentare(methode(musik, "Stop"))
    if not re.search(r"MediaPlayer\.Stop\s*\(\s*\)", stop):
        fehler.append("MenuMusic.Stop ruft MediaPlayer.Stop() nicht auf")
    if not re.search(r"MediaPlayer\.Volume\s*=", ohne_kommentare(methode(musik, "SetVolume"))):
        fehler.append("MenuMusic.SetVolume setzt MediaPlayer.Volume nicht")

if "einbindung" in teile:
    bild = lies(BACKGROUND)
    gefuellt(bild, "BackgroundScreen", ["SoundVolumePercent"])
    laden = ohne_kommentare(methode(bild, "LoadContent"))
    if not re.search(r"settingsManager\s*=\s*ScreenManager\.Game\.Services\.GetService\s*<\s*SettingsManager\s*<\s*AgeOfEvolutionsSettings\s*>\s*>\s*\(\s*\)", laden):
        fehler.append("BackgroundScreen.LoadContent holt settingsManager nicht per GetService")
    if not re.search(r"music\.Start\s*\(\s*content\s*,\s*MenuMusic\.Volume\s*\(\s*SoundVolumePercent\s*\(\s*\)\s*,\s*TransitionAlpha\s*\)\s*\)", laden):
        fehler.append("BackgroundScreen.LoadContent: music.Start(content, MenuMusic.Volume(SoundVolumePercent(), TransitionAlpha)) fehlt")
    elif laden.find("music.Start") < laden.find("content = new ContentManager"):
        fehler.append("BackgroundScreen.LoadContent: music.Start vor dem Anlegen des ContentManagers")
    upd = ohne_kommentare(methode(bild, "Update"))
    if not re.search(r"music\.SetVolume\s*\(\s*MenuMusic\.Volume\s*\(\s*SoundVolumePercent\s*\(\s*\)\s*,\s*TransitionAlpha\s*\)\s*\)", upd):
        fehler.append("BackgroundScreen.Update: music.SetVolume(MenuMusic.Volume(SoundVolumePercent(), TransitionAlpha)) fehlt")
    elif upd.find("music.SetVolume") < upd.find("base.Update"):
        fehler.append("BackgroundScreen.Update: SetVolume vor base.Update - die Blende wäre ein Bild alt")
    ent = ohne_kommentare(methode(bild, "UnloadContent"))
    i_stop, i_unload = ent.find("music.Stop()"), ent.find("content.Unload()")
    if i_stop < 0:
        fehler.append("BackgroundScreen.UnloadContent ruft music.Stop() nicht auf")
    elif i_unload < 0 or i_stop > i_unload:
        fehler.append("BackgroundScreen.UnloadContent: music.Stop() muss VOR content.Unload() stehen (Unload gibt das Lied frei)")


def startprobe(ordner, name):
    exe = ordner / "AgeOfEvolutions.exe"
    if not exe.is_file():
        fehler.append(f"{exe} fehlt - {name} nicht gebaut")
        return
    log = ordner / "logs" / f"spiel-{date.today():%Y-%m-%d}.log"
    vorher = log.stat().st_size if log.is_file() else 0
    proz = subprocess.Popen([str(exe.resolve())], cwd=exe.parent,
                            stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    try:
        ausgabe, _ = proz.communicate(timeout=LAUFZEIT)
        fehler.append(f"{name}: das Spiel endete nach weniger als {LAUFZEIT} s (exit {proz.returncode}): "
                      + " | ".join(ausgabe.decode("utf-8", "replace").strip().splitlines()[:6]))
        return
    except subprocess.TimeoutExpired:
        proz.kill()
        proz.communicate()
    time.sleep(0.5)
    neu = log.read_bytes()[vorher:].decode("utf-8", "replace") if log.is_file() else ""
    if "Menümusik:" in neu and "nicht abspielbar" in neu:
        fehler.append(f"{name}: " + next(z for z in neu.splitlines() if "nicht abspielbar" in z).strip())
    elif not re.search(rf"Menümusik: Sounds/MainTheme läuft \({DAUER} s\)", neu):
        fehler.append(f"{name}: im Spiel-Log keine Zeile 'Menümusik: Sounds/MainTheme läuft ({DAUER} s)' - "
                      f"neu im Log: {' | '.join(neu.strip().splitlines()[-4:]) or 'nichts'}")


if "start" in teile and not fehler:
    startprobe(GL, "DesktopGL")
if "start_dx" in teile and not fehler:
    startprobe(DX, "WindowsDX")

melde(fehler, f"Menümusik ({', '.join(sorted(teile))}): "
              + "Lied eingetragen und gebaut, Rümpfe gefüllt"
              + (", Einbindung im BackgroundScreen" if "einbindung" in teile else "")
              + (", läuft beim Start" if teile & {"start", "start_dx"} else ""))
