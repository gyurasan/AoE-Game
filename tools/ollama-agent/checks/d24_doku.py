"""Abnahme D24: Doku kennt die übrigen Gebäude aus L1/L2.

README nennt sie im Spielstand und ihre Tasten in der Steuerung, die Projektstruktur
die zweite Tastenreihe und die Ablaufgruppe Neubauten, TODO.md hakt die Kaserne ab,
nennt L1 und L2 unter Erledigt und behauptet nicht mehr, nur der Wachturm sei
freigeschaltet. Zeilen- und Testzahlen prüfen d5, d9 und d15 (gemessen).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde  # noqa: E402

fehler = []
NAMEN = ("Kaserne", "Palisadenmauer", "Schießstand", "Stall", "Schmiede", "Markt", "Steinmauer",
         "Belagerungswerkstatt", "Universität", "Kloster", "Burg", "Wunder")
TASTEN = ("K", "P", "S", "L", "E", "R", "W", "Z", "X", "U", "O", "C", "N")

readme = Path("README.md").read_text(encoding="utf-8-sig")
spielstand = readme.split("## Spielstand", 1)[-1].split("\n## ", 1)[0]
for name in NAMEN:
    if name not in spielstand:
        fehler.append(f"README, Spielstand: {name} fehlt")
steuerung = readme.split("## Steuerung", 1)[-1].split("\n## ", 1)[0]
for taste in TASTEN:
    if not re.search(rf"^\|[^\n]*`{taste}`", steuerung, re.M):
        fehler.append(f"README, Steuerung: Taste `{taste}` fehlt in der Tabelle")

struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
ablauf = next((l for l in struktur.splitlines() if l.startswith("tools/spielablauf/")), "")
if "Neubauten" not in ablauf:
    fehler.append("PROJEKT_STRUKTUR.md: tools/spielablauf nennt die Gruppe Neubauten nicht")
screen = next((l for l in struktur.splitlines()
               if l.startswith("|") and "| AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs |" in l), "")
if not re.search(r"zweite[n]?\s+(Tasten)?[Rr]eihe|zweite[n]?\s+Tastenreihe", screen):
    fehler.append("PROJEKT_STRUKTUR.md: die Zeile zu RTSGameplayScreen.cs nennt die zweite Tastenreihe nicht")

lines = lies()
c13 = "\n".join(abschnitt(lines, r"### C13 "))
if not re.search(r"^\s*- \[x\][^\n]*\*\*Kaserne\*\*", c13, re.M):
    fehler.append("TODO C13: der Punkt Kaserne ist nicht abgehakt")
erledigt = "\n".join(abschnitt(lines, r"# Erledigt"))
for kennung in ("L1", "L2"):
    if not re.search(rf"\b{kennung}\b", erledigt):
        fehler.append(f"TODO, Erledigt: {kennung} fehlt")
wieder = "\n".join(abschnitt(lines, r"## Wiederaufnahme"))
if re.search(r"bisher\s+nur\s+der\s+Wachturm", wieder):
    fehler.append("TODO, Wiederaufnahme: behauptet noch, nur der Wachturm sei freigeschaltet")

melde(fehler, "D24 erfuellt: README, Projektstruktur und TODO kennen die übrigen Gebäude und die zweite Tastenreihe")
