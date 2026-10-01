"""Abnahme C1a: GatherJob ist implementiert, der Vertrag unveraendert, AoE.Core MonoGame-frei.

Das Verhalten pruefen die xUnit-Tests in GatherJobTests.cs (eigener verify-Schritt).
Hier geht es nur darum, dass nichts offen blieb und die Schnittstelle nicht
nebenbei umgebaut wurde - die Tests laesst allow_write ohnehin nicht zu.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

text = Path("src/AoE.Core/Economy/GatherJob.cs").read_text(encoding="utf-8-sig")
fehler = []

if "NotImplementedException" in text:
    fehler.append("es ist noch mindestens eine Stelle offen (NotImplementedException)")
# Nur using-Direktiven und voll qualifizierte Typen - das Wort "MonoGame" steht
# absichtlich im Doku-Kommentar ("reine Logik ohne MonoGame").
if re.search(r"^\s*using\s+Microsoft\.Xna|Microsoft\.Xna\.Framework\.", text, re.MULTILINE):
    fehler.append("AoE.Core darf keine MonoGame-Abhaengigkeit bekommen")
for pflicht in (
    "public interface IGatherWorld",
    "public enum GatherPhase",
    "public const int CARRY_CAPACITY = 10;",
    "public const int SEARCH_RADIUS = 8;",
    "int AmountAt(Position cell, Resource resource);",
    "int Harvest(Position cell, Resource resource, int amount);",
    "Position? FindNearestSource(Position from, Resource resource, int maxDistance);",
    "Position? FindNearestDropOff(Position from, int ownerId, Resource resource);",
):
    if pflicht not in text:
        fehler.append(f"Vertrag veraendert, fehlt: {pflicht}")

melde(fehler, "C1a erfuellt: GatherJob implementiert, Vertrag unveraendert")
