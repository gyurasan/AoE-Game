"""Strukturprüfung (U2b): der Eintrag „Update“ ist im Hauptmenü eingebunden.

Das Verhalten prüft tools/spielablauf (Gruppe update) per Reflection - was dort
ohne ScreenManager nicht läuft, prüft diese Datei am Quelltext: die alte
Update-Zeile (StatusRect, OpenUpdateRow) ist weg, Update() ruft ExitAfterUpdate,
LoadContent räumt per CleanupAfterUpdate auf, HandleInput gleicht den Eintrag ab,
Maus und Tasten fragen EntryEnabled/EntryVisible, HandleMenuSelect startet das Update.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

DATEI = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/MainMenuScreen.cs")
text = lies(DATEI)
fehler = []

for alt in ("StatusRect", "OpenUpdateRow"):
    if re.search(rf"\b{alt}\b", text):
        fehler.append(f"die alte Update-Zeile ist noch da: {alt} kommt vor")


def braucht(name: str, *teile: str) -> None:
    rumpf = methode(text, name)
    if rumpf is None:
        fehler.append(f"Methode {name} nicht gefunden")
        return
    if "NotImplementedException" in rumpf:
        fehler.append(f"{name} ist noch leer (NotImplementedException)")
    for teil in teile:
        if not re.search(teil, rumpf):
            fehler.append(f"{name}: {teil} fehlt")


braucht("Update", r"base\.Update\s*\(", r"ExitAfterUpdate\s*\(")
braucht("LoadContent", r"CleanupAfterUpdate\s*\(", r"CheckLatest\s*\(")
braucht("HandleInput", r"RefreshUpdateEntry\s*\(\s*\)")
braucht("MoveSelection", r"EntryEnabled\s*\(")
braucht("EvaluateMouse", r"EntryEnabled\s*\(")
braucht("EntryAt", r"EntryVisible\s*\(")
braucht("HandleMenuSelect", r"EntryEnabled\s*\(", r"UPDATE_ENTRY", r"BeginUpdate\s*\(\s*\)")
if not re.search(r"override\s+void\s+Update\s*\(", text):
    fehler.append("MainMenuScreen überschreibt Update(...) nicht")
melde(fehler, "MainMenuScreen: Eintrag „Update“ eingebunden, alte Update-Zeile entfernt")
