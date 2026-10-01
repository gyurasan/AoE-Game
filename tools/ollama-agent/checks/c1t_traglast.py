"""Abnahme C1t: ein beladener Dorfbewohner behaelt seine Traglast, wenn er an eine
andere Quelle derselben Ressource geschickt wird.

Das Verhalten von GatherJob pruefen die xUnit-Tests (eigener verify-Schritt). Hier
geht es um die Verdrahtung im Spiel: IssueCommand muss die alte Traglast an den
neuen Auftrag weiterreichen - aber nur bei gleicher Ressource.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

fehler = []

job = Path("src/AoE.Core/Economy/GatherJob.cs").read_text(encoding="utf-8-sig")
if not re.search(r"public\s+GatherJob\s*\(\s*int\s+ownerId\s*,\s*Resource\s+resource\s*,"
                 r"\s*Position\s+source\s*,\s*int\s+carrying\s*=\s*0\s*\)", job):
    fehler.append("GatherJob-Konstruktor ohne optionalen Parameter 'int carrying = 0'")
if re.search(r"^\s*using\s+Microsoft\.Xna|Microsoft\.Xna\.Framework\.", job, re.MULTILINE):
    fehler.append("AoE.Core darf keine MonoGame-Abhaengigkeit bekommen")
for pflicht in (
    "public interface IGatherWorld",
    "public const int CARRY_CAPACITY = 10;",
    "public int Carrying { get; private set; }",
):
    if pflicht not in job:
        fehler.append(f"Vertrag veraendert, fehlt: {pflicht}")


def argumente(text: str, start: int) -> list[str]:
    """Argumente des Aufrufs, dessen '(' bei text[start] steht - per Klammerzaehlung."""
    tiefe, teile, aktuell = 0, [], ""
    for zeichen in text[start:]:
        if zeichen in "([{":
            tiefe += 1
            if tiefe == 1:
                continue
        elif zeichen in ")]}":
            tiefe -= 1
            if tiefe == 0:
                teile.append(aktuell.strip())
                return teile
        elif zeichen == "," and tiefe == 1:
            teile.append(aktuell.strip())
            aktuell = ""
            continue
        aktuell += zeichen
    return teile


rumpf = methode(lies(), "IssueCommand")
if rumpf is None:
    fehler.append("IssueCommand nicht gefunden")
else:
    aufrufe = [m for m in re.finditer(r"new\s+GatherJob\s*\(", rumpf)]
    if not aufrufe:
        fehler.append("IssueCommand erzeugt keinen GatherJob mehr")
    for m in aufrufe:
        if len(argumente(rumpf, m.end() - 1)) < 4:
            fehler.append("IssueCommand reicht dem neuen GatherJob keine Traglast weiter")
    if not re.search(r"\.Carrying\b", rumpf):
        fehler.append("IssueCommand liest die Traglast des alten Auftrags nicht (Job.Carrying)")
    if not re.search(r"\.Resource\b", rumpf):
        fehler.append("IssueCommand vergleicht die Ressource des alten Auftrags nicht (Job.Resource)")

melde(fehler, "C1t erfuellt: Traglast bleibt bei gleicher Ressource erhalten")
