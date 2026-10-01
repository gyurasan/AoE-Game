"""Abnahme C2a: Population und TrainingQueue sind implementiert, der Vertrag unveraendert.

Das Verhalten pruefen die xUnit-Tests in TrainingTests.cs (eigener verify-Schritt).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde  # noqa: E402

text = Path("src/AoE.Core/Economy/Training.cs").read_text(encoding="utf-8-sig")
fehler = []

if "NotImplementedException" in text:
    fehler.append("es ist noch mindestens eine Stelle offen (NotImplementedException)")
if re.search(r"^\s*using\s+Microsoft\.Xna|Microsoft\.Xna\.Framework\.", text, re.MULTILINE):
    fehler.append("AoE.Core darf keine MonoGame-Abhaengigkeit bekommen")

# Signaturen als Regex mit \s+ statt Leerzeichen: Zeilenumbrueche sind erlaubt
for pflicht in (
    r"public\s+static\s+class\s+Population",
    r"public\s+const\s+int\s+DEFAULT_LIMIT\s*=\s*200\s*;",
    r"public\s+const\s+int\s+SLOTS_PER_BUILDING\s*=\s*5\s*;",
    r"public\s+static\s+int\s+SlotsOf\s*\(\s*BuildingType\s+type\s*\)",
    r"public\s+static\s+int\s+Capacity\s*\(\s*IEnumerable<BuildingType>\s+buildings\s*,\s*int\s+limit\s*=\s*DEFAULT_LIMIT\s*\)",
    r"public\s+sealed\s+class\s+TrainingQueue<T>\s+where\s+T\s*:\s*notnull",
    r"public\s+const\s+int\s+MAX_LENGTH\s*=\s*15\s*;",
    r"public\s+int\s+Count\b",
    r"public\s+IReadOnlyList<T>\s+Units\b",
    r"public\s+float\s+Progress\b",
    r"public\s+bool\s+IsBlocked\s*\{\s*get\s*;\s*private\s+set\s*;\s*\}",
    r"public\s+bool\s+Enqueue\s*\(\s*T\s+unit\s*,\s*Dictionary<Resource\s*,\s*int>\s+cost\s*,\s*float\s+trainSeconds\s*,\s*ResourcePool\s+pool\s*\)",
    r"public\s+bool\s+Update\s*\(\s*float\s+dt\s*,\s*int\s+population\s*,\s*int\s+capacity\s*,\s*\[MaybeNullWhen\(false\)\]\s*out\s+T\s+finished\s*\)",
    r"public\s+bool\s+CancelLast\s*\(\s*ResourcePool\s+pool\s*\)",
):
    if not re.search(pflicht, text):
        fehler.append(f"Vertrag veraendert, fehlt: {pflicht}")

melde(fehler, "C2a erfuellt: Population und TrainingQueue implementiert, Vertrag unveraendert")
