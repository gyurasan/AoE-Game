"""Abnahme D26: Doku kennt das Abliefern an der Tür (T1).

Die Projektstruktur nennt die Ablaufgruppe Türen, TODO.md nennt T1 in der
Wiederaufnahme und unter "Zuletzt fertiggestellt". Zeilenzahlen prüft d9 (gemessen).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _todo import abschnitt, lies, melde  # noqa: E402

fehler = []
struktur = Path("docs/PROJEKT_STRUKTUR.md").read_text(encoding="utf-8-sig")
ablauf = next((l for l in struktur.splitlines() if l.startswith("tools/spielablauf/")), "")
if "Türen" not in ablauf:
    fehler.append("PROJEKT_STRUKTUR.md: tools/spielablauf nennt die Gruppe Türen nicht")

wieder = "\n".join(abschnitt(lies(), r"## Wiederaufnahme"))
if not re.search(r"\bT1\b", wieder):
    fehler.append("TODO, Wiederaufnahme: T1 fehlt")
if "DropOffStand" not in wieder:
    fehler.append("TODO, Zuletzt fertiggestellt: DropOffStand fehlt")

melde(fehler, "D26 erfuellt: Projektstruktur und TODO kennen das Abliefern an der Tür")
