"""Abnahme P2: Forschungen im Spiel verdrahtet.

Das Verhalten prüfen die xUnit-Tests (ResearchTests, AoE.Core) und die Gruppe forschung in
tools/spielablauf: Tasten, Kosten, Dauer, Wirkung, Abbruch. Hier steht nur, was der Ablauf
nicht sieht - er ruft UpdateResearch selbst auf statt über Update, und er zeichnet nicht -,
und dass vom Vertrag nichts übrig ist.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

fehler = []

research = Path("src/AoE.Core/Economy/Research.cs").read_text(encoding="utf-8-sig")
if "NotImplementedException" in research:
    fehler.append("Research.cs: noch ein Rumpf mit NotImplementedException")
if re.search(r"\bclass\s+TechTree\b|\benum\s+TechType\b", Path("src/AoE.Core/Entities/Buildings.cs").read_text(encoding="utf-8-sig")):
    fehler.append("Buildings.cs: der alte TechTree (TechType, Technology) steht noch da")

screen = lies()


def rumpf(text: str, name: str) -> str:
    r = methode(text, name)
    if r is None:
        fehler.append(f"Methode {name} fehlt")
        return ""
    return r


for name in ("Research", "UpdateResearch"):
    if "NotImplementedException" in rumpf(screen, name):
        fehler.append(f"{name} wirft noch NotImplementedException")
if not re.search(r"\bUpdateResearch\s*\(", rumpf(screen, "Update")):
    fehler.append("Update ruft UpdateResearch nicht auf - im Spiel forscht nichts")
if not re.search(r"\.FarmFood\b", rumpf(screen, "WheatLook")):
    fehler.append("WheatLook misst den Weizen noch an FARM_FOOD statt am Vorrat der Kachel (FarmFood)")

tilemap = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs").read_text(encoding="utf-8-sig")
if not re.search(r"\.FarmFood\s*=", rumpf(tilemap, "PlantCrop")):
    fehler.append("PlantCrop setzt Tile.FarmFood nicht")
if not re.search(r"\.FarmFood\b", rumpf(tilemap, "RegrowCrop")):
    fehler.append("RegrowCrop wächst nicht bis Tile.FarmFood nach")

melde(fehler, "P2 erfuellt: Forschungen im Spiel verdrahtet, kein Vertragsrest")
