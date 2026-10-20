"""Strukturprüfung (U1a, U1b, U2a): die genannten Vertragsrümpfe sind geschrieben.

  py u_vertrag.py <Datei> <Name> [<Name> ...]

Jeder Name muss als Methode oder Eigenschaft in der Datei stehen, und sein Rumpf
darf kein NotImplementedException mehr enthalten. Was die Rümpfe tun, prüft
tools/spielablauf (Gruppen updatehilfen, updatedienst, update) - diese Datei
fängt nur den vergessenen Schritt (Lehre aus C4m: bei vielen Schritten lässt das
Modell gern einen aus, und eine Gruppe, die ihn nicht berührt, bleibt grün).
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402


def eigenschaft(text: str, name: str) -> str | None:
    """Rumpf einer Eigenschaft 'Typ Name => ...;' oder 'Typ Name { ... }', sonst None."""
    m = re.search(rf"\b{name}\b\s*(=>|\{{)", text)
    if not m:
        return None
    if m.group(1) == "=>":
        ende = text.find(";", m.end())
        return text[m.start():ende + 1]
    start = m.end() - 1
    tiefe = 0
    for j in range(start, len(text)):
        if text[j] == "{":
            tiefe += 1
        elif text[j] == "}":
            tiefe -= 1
            if tiefe == 0:
                return text[start:j + 1]
    return None


datei = Path(sys.argv[1])
namen = sys.argv[2:]
text = lies(datei)
fehler = []
for name in namen:
    rumpf = methode(text, name)
    if rumpf is None:
        rumpf = eigenschaft(text, name)
    if rumpf is None:
        fehler.append(f"{datei.name}: {name} nicht gefunden")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"{datei.name}: {name} ist noch leer (NotImplementedException)")
melde(fehler, f"{datei.name}: {', '.join(namen)} geschrieben")
