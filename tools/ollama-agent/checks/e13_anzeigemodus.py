"""Abnahme E13: der Start übernimmt den gespeicherten Anzeigemodus, das Menü zeigt den echten Zustand.

Dass das Fenster beim Start wirklich so aufgeht, wie gespeichert, ist per
Bildschirmfoto belegt (Fenstergröße mit FullScreen false und true). Hier: der
Desktop-Start setzt IsFullScreen aus settingsManager.Settings.FullScreen, nachdem
der Settings-Manager angelegt ist, randlos (HardwareModeSwitch = false), und die
Einstellungsseite überschreibt gdm.IsFullScreen nicht mehr ohne ApplyChanges.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

spiel = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/AgeOfEvolutionsGame.cs"))
seite = lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/SettingsScreen.cs"))
fehler = []

start = methode(spiel, "AgeOfEvolutionsGame") or ""
anlegen = start.find("new SettingsManager<AgeOfEvolutionsSettings>")
uebernahme = re.search(r"graphicsDeviceManager\.IsFullScreen\s*=\s*settingsManager\.Settings\.FullScreen", start)
if not uebernahme:
    fehler.append("der Start übernimmt settingsManager.Settings.FullScreen nicht in IsFullScreen")
elif anlegen < 0 or uebernahme.start() < anlegen:
    fehler.append("IsFullScreen wird aus den Einstellungen gesetzt, bevor der Settings-Manager sie geladen hat")
if not re.search(r"HardwareModeSwitch\s*=\s*false", start):
    fehler.append("Vollbild schaltet den Bildschirmmodus um (HardwareModeSwitch = false fehlt)")
laden = methode(seite, "LoadContent") or ""
if re.search(r"\bgdm\.IsFullScreen\s*=", laden):
    fehler.append("SettingsScreen.LoadContent setzt gdm.IsFullScreen noch - die Anzeige zeigte dann den gespeicherten statt des echten Zustands")

melde(fehler, "E13 erfuellt: Start nach gespeichertem Anzeigemodus, das Menü zeigt den echten Zustand")
