"""Abnahme C5a: Bauregeln und Baustelle sind implementiert, der Vertrag unveraendert.

Das Verhalten pruefen die xUnit-Tests in ConstructionTests.cs (eigener verify-Schritt),
auch die beiden neuen Lager-Fabriken in Buildings.cs.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

text = Path("src/AoE.Core/Economy/Construction.cs").read_text(encoding="utf-8-sig")
fehler = []

if "NotImplementedException" in text:
    fehler.append("es ist noch mindestens eine Stelle offen (NotImplementedException)")
if re.search(r"^\s*using\s+Microsoft\.Xna|Microsoft\.Xna\.Framework\.", text, re.MULTILINE):
    fehler.append("AoE.Core darf keine MonoGame-Abhaengigkeit bekommen")

for pflicht in (
    r"public\s+static\s+class\s+BuildingRules",
    r"public\s+static\s+Dictionary<Resource\s*,\s*int>\s+CostOf\s*\(\s*BuildingType\s+type\s*\)",
    r"public\s+static\s+float\s+BuildSecondsOf\s*\(\s*BuildingType\s+type\s*\)",
    r"public\s+static\s+int\s+SizeOf\s*\(\s*BuildingType\s+type\s*\)",
    r"public\s+static\s+float\s+SpeedFactor\s*\(\s*int\s+builders\s*\)",
    r"public\s+sealed\s+class\s+Construction\b",
    r"public\s+Construction\s*\(\s*float\s+buildSeconds\s*\)",
    r"public\s+float\s+BuildSeconds\s*\{\s*get\s*;\s*\}",
    r"public\s+float\s+Progress\s*\{\s*get\s*;\s*private\s+set\s*;\s*\}",
    r"public\s+bool\s+IsComplete\b",
    r"public\s+bool\s+Update\s*\(\s*float\s+dt\s*,\s*int\s+builders\s*\)",
):
    if not re.search(pflicht, text):
        fehler.append(f"Vertrag veraendert, fehlt: {pflicht}")

melde(fehler, "C5a erfuellt: Bauregeln und Baustelle implementiert, Vertrag unveraendert")
