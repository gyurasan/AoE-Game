"""Strukturprüfung (SV5/SV6, mehrere Spielstand-Plätze): die Platz-Statik im
SaveGameStore, die beiden Listenmenüs und ihre Einbindung.

Das Verhalten der Platz-Statik prüft tests/AoE.Tests/SaveSlotsTests.cs (dotnet test);
diese Datei prüft am Quelltext, was ohne Grafik und ScreenManager nicht ausführbar ist:
dass LoadGameScreen und SaveGameScreen die Vereinbarungen ihrer VERTRÄGE tatsächlich
aufrufen und die Menüs sie öffnen. Wie in sv_menue.py: Eigenschaften (welche Aufrufe
vorkommen), nie Schreibweisen.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

SCREENS = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
fehler = []


def text_oder(datei: Path, name: str) -> str:
    text = methode(lies(datei), name)
    if text is None:
        fehler.append(f"{datei.name}: Methode/Konstruktor {name} nicht gefunden")
        return ""
    if "NotImplementedException" in text:
        fehler.append(f"{datei.name}: {name} ist noch leer (NotImplementedException)")
    return text


def enthaelt(text: str, wo: str, *teile: str) -> None:
    for teil in teile:
        if teil not in text:
            fehler.append(f"{wo}: „{teil}“ fehlt")


# --- SaveGameStore: die Platz-Statik ist gefüllt, SavesFolder ist eine echte Rechnung ---
store = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/SaveGameStore.cs")
speicher = lies(store)
for meth in ("ReadHeader", "ListSaves", "NextSavePath"):
    text_oder(store, meth)
ordner = re.search(r"SavesFolder\s*=>(.*)", speicher)
if ordner is None:
    fehler.append("SaveGameStore.cs: SavesFolder nicht als =>-Eigenschaft gefunden")
elif "NotImplementedException" in ordner.group(1):
    fehler.append("SaveGameStore.cs: SavesFolder ist noch leer (NotImplementedException)")
enthaelt(speicher, "SaveGameStore.cs", "record SaveSlot", "public string Label")
enthaelt(speicher, "SaveGameStore.cs", "public static void DeleteSlot", "SLOT_NAME.IsMatch",
         "File.Delete(", "FileNotFoundException", "ArgumentException")
if "NotImplementedException" in speicher:
    fehler.append("SaveGameStore.cs: es steht noch ein NotImplementedException darin")

# --- LoadGameScreen: Liste laden, Fehler zeigen, zurück; Plätze löschen (SV7) ---
laden = SCREENS / "LoadGameScreen.cs"
konstruktor = text_oder(laden, "LoadGameScreen")
enthaelt(konstruktor, "LoadGameScreen-Konstruktor", "BaueEintraege(")
ganze_laden = lies(laden)
enthaelt(ganze_laden, "LoadGameScreen-Eintraege", "ListSaves(", ".Label", "new MenuEntry(",
         "MenuEntries.Add", "Zurück", "OnCancel", "Selected +=", "Deletable = true",
         "DeleteSelected +=")
enthaelt(ganze_laden, "LoadGameScreen", "LoadSlot(", "BaueEintraege(", "NeuAufbauen(",
         "ClampSelectedEntry(", "FragLoeschen(", "LoeschePlatz(", "SaveGameStore.DeleteSlot(",
         "Spielstand löschen", "Spielstand gelöscht")
slot = text_oder(laden, "LoadSlot")
enthaelt(slot, "LoadGameScreen.LoadSlot", "new SaveGameStore(", ".Load(", "SaveGameMapper.Restore(",
         "LoadingScreen.Load(", "new RTSGameplayScreen(", "catch", "MessageBoxScreen(",
         "Spielstand nicht lesbar")

# --- SaveGameScreen: neu oder überschreiben, Plätze löschen, Meldung, zurück ---
sp = SCREENS / "SaveGameScreen.cs"
konstruktor = text_oder(sp, "SaveGameScreen")
enthaelt(konstruktor, "SaveGameScreen-Konstruktor", "BaueEintraege(")
ganze_sp = lies(sp)
enthaelt(ganze_sp, "SaveGameScreen-Eintraege", "Neuer Spielstand", "ListSaves(", ".Label",
         "new MenuEntry(", "MenuEntries.Add", "Zurück", "OnCancel", "SaveTo(",
         "Deletable = true", "DeleteSelected +=")
enthaelt(ganze_sp, "SaveGameScreen", "BaueEintraege(", "NeuAufbauen(",
         "ClampSelectedEntry(", "FragLoeschen(", "LoeschePlatz(", "SaveGameStore.DeleteSlot(",
         "Spielstand löschen", "Spielstand gelöscht")
speichern = text_oder(sp, "SaveTo")
enthaelt(speichern, "SaveGameScreen.SaveTo", "OfType<RTSGameplayScreen>", "LastOrDefault",
         "CaptureSave(", "NextSavePath(", ".Save(", "ShowToast(\"Spiel gespeichert\")",
         "ExitScreen(", "catch", "Speichern fehlgeschlagen")

# --- Einbindung: die Menüs öffnen die Listen ---
enthaelt(lies(SCREENS / "MainMenuScreen.cs"), "MainMenuScreen.cs", "new LoadGameScreen(", "ListSaves(")
enthaelt(lies(SCREENS / "PauseScreen.cs"), "PauseScreen.cs", "new SaveGameScreen(")

# --- Lösch-Flow (SV7): Eintrag trägt die Kennung, die Tafel leitet, der Input kennt die Taste ---
enthaelt(lies(SCREENS / "MenuEntry.cs"), "MenuEntry.cs", "Deletable", "DeleteSelected",
         "OnDeleteSelected")
enthaelt(lies(SCREENS / "MenuScreen.cs"), "MenuScreen.cs", "IsMenuDelete(",
         "OnDeleteSelected", "ClampSelectedEntry(")
enthaelt(lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Inputs/InputState.cs")), "InputState.cs",
         "IsMenuDelete", "Keys.Delete", "Buttons.Y")

melde(fehler, "Platz-Menüs: Store-Statik gefüllt (mit DeleteSlot), LoadGameScreen lädt und "
              "zeigt Fehler, SaveGameScreen speichert neu oder überschreibt, beide löschen "
              "nach Ja/Nein, beide Menüs öffnen sie")
