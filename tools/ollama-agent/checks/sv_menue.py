"""Strukturprüfung (SV3): Speichern im Pausenmenü, Laden im Hauptmenü, die Einbindung in
RTSGameplayScreen.LoadContent und den Startschalter --laden.

Die Rümpfe selbst prüfen tools/spielablauf (Gruppen speichern und menue) und
checks/sv_laden.py; was nur mit Grafik und ScreenManager läuft - das Pausenmenü speichert,
das Hauptmenü lädt -, prüft diese Datei am Quelltext: die Aufrufe aus dem VERTRAG müssen da
sein, in der richtigen Reihenfolge.
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

SCREENS = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
fehler = []


def rumpf(datei: Path, name: str) -> str:
    text = methode(lies(datei), name)
    if text is None:
        fehler.append(f"{datei.name}: Methode {name} nicht gefunden")
        return ""
    if "NotImplementedException" in text:
        fehler.append(f"{datei.name}: {name} ist noch leer (NotImplementedException)")
    return text


def enthaelt(text: str, wo: str, *teile: str) -> None:
    for teil in teile:
        if teil not in text:
            fehler.append(f"{wo}: „{teil}“ fehlt")


def reihenfolge(text: str, wo: str, *teile: str) -> None:
    stellen = [text.find(t) for t in teile]
    if all(s >= 0 for s in stellen) and stellen != sorted(stellen):
        fehler.append(f"{wo}: Reihenfolge stimmt nicht - erwartet {' vor '.join(teile)}")


# Pausenmenü: speichern, Meldung, Fehler abfangen
pause = rumpf(SCREENS / "PauseScreen.cs", "SaveGameMenuEntrySelected")
enthaelt(pause, "PauseScreen.SaveGameMenuEntrySelected", "OfType<RTSGameplayScreen>()", "CaptureSave()",
         "new SaveGameStore()", ".Save(", "ShowToast(\"Spiel gespeichert\")", "catch",
         "\"Speichern fehlgeschlagen: \"")
reihenfolge(pause, "PauseScreen.SaveGameMenuEntrySelected", ".Save(", "ShowToast(\"Spiel gespeichert\")", "catch")

# Hauptmenü: Eintrag freischalten, laden, Fehler zeigen
menue = SCREENS / "MainMenuScreen.cs"
enthaelt(rumpf(menue, "LoadContent"), "MainMenuScreen.LoadContent", "RefreshLoadEntry()")
auswahl = rumpf(menue, "HandleMenuSelect")
enthaelt(auswahl, "MainMenuScreen.HandleMenuSelect", "case LOAD_ENTRY", "LoadSavedGame()")
enthaelt(rumpf(menue, "RefreshLoadEntry"), "MainMenuScreen.RefreshLoadEntry", "saves.Exists", "selectedIndex")
laden = rumpf(menue, "LoadSavedGame")
enthaelt(laden, "MainMenuScreen.LoadSavedGame", "saves.Load()", "SaveGameMapper.Restore(", "LoadingScreen.Load(",
         "new RTSGameplayScreen(", "catch", "\"Spielstand nicht lesbar: \"", "menuOptions[LOAD_ENTRY]",
         "selectedIndex = 0", "new MessageBoxScreen(")
reihenfolge(laden, "MainMenuScreen.LoadSavedGame", "saves.Load()", "SaveGameMapper.Restore(", "LoadingScreen.Load(", "catch")

# Spielbildschirm: geladene Welt statt neuer Karte, gespeicherte Kamera nach dem Startblick
spiel = rumpf(SCREENS / "RTSGameplayScreen.cs", "LoadContent")
enthaelt(spiel, "RTSGameplayScreen.LoadContent", "RestoreWorld(_loadedSave)", "RestoreCamera(_loadedSave)",
         "new TileMap(", "gatherWorld = new TileMapGatherWorld(tileMap)")
reihenfolge(spiel, "RTSGameplayScreen.LoadContent", "ApplyStoredGameSettings()", "RestoreWorld(_loadedSave)",
            "units = tileMap.Units", "if (AiEnabled)")
reihenfolge(spiel, "RTSGameplayScreen.LoadContent", "cameraZoom = ZoomScale;", "RestoreCamera(_loadedSave)",
            "tileMap.UpdateFogOfWarForPlayer(0, units);")
if spiel.count("gatherWorld = new TileMapGatherWorld(tileMap)") != 1:
    fehler.append("RTSGameplayScreen.LoadContent: gatherWorld soll genau einmal angelegt werden - für beide Wege")

# Startschalter --laden
spielklasse = Path("AgeOfEvolutions/AgeOfEvolutions.Core/AgeOfEvolutionsGame.cs")
start = rumpf(spielklasse, "Initialize")
enthaelt(start, "AgeOfEvolutionsGame.Initialize", "ScreenFromCommandLine(args)")
reihenfolge(start, "AgeOfEvolutionsGame.Initialize", "ScreenFromCommandLine(args)", "args.Contains(\"--rts\")")
schalter = rumpf(spielklasse, "ScreenFromCommandLine")
enthaelt(schalter, "AgeOfEvolutionsGame.ScreenFromCommandLine", "\"--laden\"", "SaveGameStore(", "SaveGameMapper.Restore(",
         "new RTSGameplayScreen(", "catch", "\"[Laden] Spielstand nicht lesbar: \"")

melde(fehler, "Spielstand-Einbindung: Pausenmenü speichert, Hauptmenü lädt, LoadContent und --laden stimmen")
