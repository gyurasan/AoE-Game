"""Abnahme C4n: Schwertschlag und Speerstoß - acht Schlagphasen für Miliz und Späher, nach dem Schlagtakt.

Die Bilder kommen aus tools/bilder/schlag.py. Geprüft wird je Figur: _schlag1 bis _schlag8 in beiden
Spielerfarben, eingetragen in der Content-Pipeline, alle gleich groß, größer als das Standbild
(das ausgeholte Schwert ragt hinaus) und die Füße unten (deckende Pixel in der untersten
Zeile wie beim Standbild). Im Code: geladen über LoadAttack, beim Angriff nach AttackPhase
gewählt, im Maßstab des Standbilds gezeichnet. Welches Bild wann kommt, prüft die Gruppe
schlag in tools/spielablauf; wie es aussieht, die Fotoserie.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _bild import lies_png  # noqa: E402
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
fehler = []
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")


def unten_deckend(w, h, p):
    return any(p(x, y)[3] > 100 for y in range(h - 3, h) for x in range(w))


for figur, farbe in ((f, c) for f in ("miliz", "spaeher") for c in ("blau", "rot")):
    sw, sh, sp = lies_png(CONTENT / f"Einheiten/{figur}_{farbe}.png")
    groesse = None
    for n in range(1, 9):
        rel = f"Einheiten/{figur}_schlag{n}_{farbe}.png"
        if f"#begin {rel}\n" not in mgcb:
            fehler.append(f"Content-Pipeline: {rel} nicht eingetragen")
        if not (CONTENT / rel).is_file():
            fehler.append(f"{rel} fehlt")
            continue
        w, h, p = lies_png(CONTENT / rel)
        if groesse is None:
            groesse = (w, h)
        elif (w, h) != groesse:
            fehler.append(f"{rel} ist {w}x{h}, die anderen Schlagbilder {groesse[0]}x{groesse[1]}")
        if w <= sw or h <= sh:
            fehler.append(f"{rel} ({w}x{h}) ist nicht größer als das Standbild ({sw}x{sh}) - das ausgeholte Schwert fehlt")
        if not unten_deckend(w, h, p):
            fehler.append(f"{rel}: in der untersten Zeile steht nichts - die Füße stehen nicht unten wie beim Standbild")

text = lies()
for name, muster, was in (
    ("AttackPhase", r"AttackTimeline", "AttackPhase wählt nicht aus AttackTimeline"),
    ("LoadAttack", r'_schlag', 'LoadAttack lädt nicht _schlag1 bis _schlag8'),
):
    rumpf = methode(text, name) or ""
    if "NotImplementedException" in rumpf or not re.search(muster, rumpf):
        fehler.append(was)
if not re.search(r"\bLoadAttack\s*\(", methode(text, "LoadContent") or ""):
    fehler.append("LoadContent lädt die Schlagphasen nicht über LoadAttack")
figur = methode(text, "DrawVillager") or ""
if not re.search(r"\bAttackPhase\s*\(\s*unit\.AttackTimer\s*\)", figur) or "_unitAttack" not in figur:
    fehler.append("DrawVillager wählt beim Angriff kein Schlagbild (_unitAttack, AttackPhase(unit.AttackTimer))")
if not re.search(r"scale\s*=[^;]*_unitSprites\[[^;]*\.Height", figur):
    fehler.append("DrawVillager zeichnet die Schlagbilder nicht im Maßstab des Standbilds (_unitSprites[...].Height)")

melde(fehler, "C4n erfuellt: acht Schlagphasen für Miliz und Späher, im Maßstab des Standbilds, nach dem Schlagtakt gezeichnet")
