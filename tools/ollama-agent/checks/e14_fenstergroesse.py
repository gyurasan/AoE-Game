"""Abnahme E14: Vollbild -> Fenster kehrt mit der Fenstergröße zurück, nicht mit der des Bildschirms.

Fehler bis 2026-10-05 (Hinweis des Nutzers, bemerkt unter GL): Im randlosen
Vollbild setzt OnClientSizeChanged den Bildpuffer auf Bildschirmgröße. Beim
Zurückschalten übernahm MonoGame genau diese Größe für das Fenster - dessen
Innenfläche bedeckte den ganzen Bildschirm, die Titelleiste lag oben außerhalb,
und es sah aus, als hätte das Umschalten nicht gewirkt. Ein Nachbau des
Desktop-Starts gegen MonoGame 3.8.5.1 zeigte das unter DesktopGL (3008x1692 an
0,0) wie unter WindowsDX (3008x1673, Titelleiste bei y=-34); mit Merkgröße kam
das Fenster in beiden mit 2406x1353 mittig zurück.

Geprüft wird der Aufbau: das Spiel merkt sich die Fenstergröße beim Start und
bei jeder Größenänderung im Fenster (nicht im Vollbild), stellt sie vor dem
Verlassen des Vollbilds wieder ein, und das Einstellungsmenü schaltet über das
Spiel um statt direkt am GraphicsDeviceManager.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

spiel = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/AgeOfEvolutionsGame.cs"))
seite = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/SettingsScreen.cs"))
fehler = []

umschalten = methode(spiel, "ToggleFullScreen")
merk = None
if umschalten is None:
    fehler.append("AgeOfEvolutionsGame hat keine Methode ToggleFullScreen")
else:
    breite = re.search(r"graphicsDeviceManager\.PreferredBackBufferWidth\s*=\s*(\w+)\.X\s*;", umschalten)
    aufruf = umschalten.find("graphicsDeviceManager.ToggleFullScreen()")
    if not breite:
        fehler.append("ToggleFullScreen stellt die gemerkte Fensterbreite nicht wieder ein")
    else:
        merk = breite.group(1)
        if not re.search(rf"graphicsDeviceManager\.PreferredBackBufferHeight\s*=\s*{merk}\.Y\s*;", umschalten):
            fehler.append(f"ToggleFullScreen stellt die Höhe nicht aus {merk}.Y wieder ein")
        if not re.search(r"if\s*\([^)]*graphicsDeviceManager\.IsFullScreen", umschalten):
            fehler.append("ToggleFullScreen stellt die Fenstergröße nicht nur beim Verlassen des Vollbilds ein")
        if aufruf < 0 or aufruf < breite.start():
            fehler.append("ToggleFullScreen schaltet nicht erst nach dem Einstellen der Größe um")

if merk:
    start = methode(spiel, "AgeOfEvolutionsGame") or ""
    if not re.search(rf"\b{merk}\s*=\s*new Point\(\s*graphicsDeviceManager\.PreferredBackBufferWidth", start):
        fehler.append(f"der Start merkt sich die anfängliche Fenstergröße nicht in {merk}")
    groesse = methode(spiel, "OnClientSizeChanged") or ""
    if not re.search(rf"if\s*\(\s*!\s*graphicsDeviceManager\.IsFullScreen\s*\)\s*\{{?\s*{merk}\s*=\s*new Point\(\s*bounds\.Width,\s*bounds\.Height\s*\)", groesse):
        fehler.append(f"OnClientSizeChanged merkt sich die Fenstergröße nicht (nur außerhalb des Vollbilds) in {merk}")

auswahl = methode(seite, "FullScreenMenuEntrySelected") or ""
if re.search(r"\bgdm\.ToggleFullScreen\(", auswahl):
    fehler.append("das Einstellungsmenü schaltet noch direkt am GraphicsDeviceManager um")
if not re.search(r"\(\s*\(\s*(?:[\w.]+\.)?AgeOfEvolutionsGame\s*\)\s*ScreenManager\.Game\s*\)\s*\.ToggleFullScreen\(\)", auswahl):
    fehler.append("das Einstellungsmenü schaltet nicht über AgeOfEvolutionsGame.ToggleFullScreen um")

melde(fehler, "E14 erfuellt: Vollbild -> Fenster stellt die gemerkte Fenstergröße wieder her")
