"""Abnahme C2b: Bevoelkerungsgrenze und Dorfbewohner-Ausbildung sind im Spiel verdrahtet.

Die Logik (Population, TrainingQueue) pruefen die xUnit-Tests. Hier geht es darum,
dass das Spiel sie aufruft - Taste Q, Grenze aus den Gebaeuden, Ausbildung je Frame,
Anzeige - und dass jeder String im Bildschirm mit der HUD-Schrift zeichenbar ist.
Ob es im laufenden Spiel richtig aussieht, zeigt erst ein Screenshot.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import SCREEN, lies, melde, methode  # noqa: E402

fehler = []

tilemap = Path("AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
if not re.search(r"TrainingQueue<UnitType>\s+Training\b", tilemap):
    fehler.append("Building hat keine TrainingQueue<UnitType> Training")
if "ProductionQueue" in tilemap:
    fehler.append("die alte ProductionQueue steht noch in Building")
if not re.search(r"public\s+Unit\s+AddVillager\s*\(", tilemap):
    fehler.append("TileMap.AddVillager ist nicht oeffentlich oder gibt die Einheit nicht zurueck")

player = Path("AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data/Player.cs").read_text(encoding="utf-8-sig")
if re.search(r"PopulationLimit\s*\{\s*get;\s*set;\s*\}\s*=", player):
    fehler.append("Player.PopulationLimit hat noch einen festen Startwert")

screen = lies()


def rumpf(name: str) -> str:
    r = methode(screen, name)
    if r is None:
        fehler.append(f"Methode {name} fehlt")
        return ""
    return r


def verlangt(text: str, muster: str, was: str) -> None:
    if not re.search(muster, text):
        fehler.append(was)


update = rumpf("Update")
verlangt(update, r"UpdatePopulationLimits\s*\(", "Update rechnet die Bevoelkerungsgrenze nicht")
verlangt(update, r"UpdateTraining\s*\(", "Update bildet nicht aus (UpdateTraining)")
verlangt(rumpf("LoadContent"), r"UpdatePopulationLimits\s*\(", "LoadContent rechnet die Startgrenze nicht")

eingabe = rumpf("HandleRtsInput")
verlangt(eingabe, r"IsKeyUp\s*\(\s*Keys\.Q\s*\)", "Taste Q fehlt oder loest bei gehaltener Taste jeden Frame aus")
verlangt(eingabe, r"TrainVillager\s*\(", "Taste Q ruft TrainVillager nicht auf")

verlangt(rumpf("TrainVillager"), r"\.Enqueue\s*\(", "TrainVillager reiht nichts ein")
verlangt(rumpf("UpdatePopulationLimits"), r"Capacity\s*\(", "die Grenze kommt nicht aus Population.Capacity")
training = rumpf("UpdateTraining")
for muster, was in (
    (r"\.Training\.Update\s*\(", "UpdateTraining ruft TrainingQueue.Update nicht auf"),
    (r"SpawnCell\s*\(", "UpdateTraining sucht keine freie Kachel"),
    (r"AddVillager\s*\(", "UpdateTraining setzt keinen Dorfbewohner auf die Karte"),
    (r"AddUnit\s*\(", "der neue Dorfbewohner zaehlt nicht zur Bevoelkerung (AddUnit)"),
):
    verlangt(training, muster, was)
verlangt(rumpf("SpawnCell"), r"IsWalkable\s*\(", "SpawnCell prueft die Begehbarkeit nicht")

verlangt(screen, r"VillagerCost\s*=\s*new\b[^;]*Resource\.Food\s*\]\s*=\s*25\b", "Dorfbewohner kostet nicht 25 Nahrung")
verlangt(screen, r"VILLAGER_TRAIN_SECONDS\s*=\s*25(\.0)?f\s*;", "Ausbildungszeit ist nicht 25 s")

hud = rumpf("DrawUI")
verlangt(hud, r"PopulationCount\s*>=\s*player1\.PopulationLimit", "die Bevoelkerung wird am Limit nicht hervorgehoben")
verlangt(hud, r"\.IsBlocked\b", "die obere Leiste zeigt den Stillstand nicht an")


# Jedes String-Literal muss mit der HUD-Schrift zeichenbar sein: Zeichen 32-254
# (Hud.spritefont, ohne DefaultCharacter - ein fehlendes Zeichen wirft beim Zeichnen).
def literale(zeile: str):
    i, n = 0, len(zeile)
    while i < n:
        if zeile.startswith("//", i):
            return
        if zeile[i] == '"':
            j = i + 1
            while j < n and zeile[j] != '"':
                j += 2 if zeile[j] == "\\" else 1
            yield zeile[i + 1:j]
            i = j + 1
        elif zeile[i] == "'" and i + 2 < n:
            j = zeile.find("'", i + 1)
            i = (j + 1) if j > 0 else n
        else:
            i += 1


for nr, zeile in enumerate(screen.splitlines(), 1):
    for literal in literale(zeile):
        schlecht = sorted({c for c in literal if not 32 <= ord(c) <= 254})
        if schlecht:
            fehler.append(f"{SCREEN.name}:{nr}: String enthaelt Zeichen ausserhalb der HUD-Schrift: "
                          + ", ".join(f"U+{ord(c):04X}" for c in schlecht))

melde(fehler, "C2b erfuellt: Grenze aus Gebaeuden, Taste Q bildet aus, Stillstand sichtbar")
