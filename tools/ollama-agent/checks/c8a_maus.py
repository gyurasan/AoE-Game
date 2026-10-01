"""Abnahme C8a: rechts markieren, links ziehen verschiebt die Karte, kurzer Linksklick ist ein Befehl.

Geprueft wird, in welchem Block was steht - nicht, wie es formuliert ist:
der Rahmen entsteht nur im Block der rechten Taste, das Verschieben nur im
Block der linken, und der Befehl faellt beim Loslassen der linken Taste.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []


def block_nach(quelle: str, muster: str) -> str | None:
    """Der geschweifte Block direkt nach der ersten Fundstelle von 'muster'."""
    treffer = re.search(muster, quelle)
    if not treffer:
        return None
    start = quelle.find("{", treffer.end())
    if start < 0:
        return None
    tiefe = 0
    for i in range(start, len(quelle)):
        if quelle[i] == "{":
            tiefe += 1
        elif quelle[i] == "}":
            tiefe -= 1
            if tiefe == 0:
                return quelle[start:i + 1]
    return None


eingabe = methode(text, "HandleRtsInput") or ""
rechts = block_nach(eingabe, r"if\s*\(\s*mouse\.RightButton\s*==\s*ButtonState\.Pressed\s*\)")
links = block_nach(eingabe, r"if\s*\(\s*mouse\.LeftButton\s*==\s*ButtonState\.Pressed\s*\)")
loslassen = block_nach(eingabe, r"else\s+if\s*\(\s*dragStart\.HasValue\s*\)")

if rechts is None:
    fehler.append("kein Block 'if (mouse.RightButton == ButtonState.Pressed)' in HandleRtsInput")
elif "selectionStart" not in rechts:
    fehler.append("der Auswahlrahmen entsteht nicht im Block der rechten Taste")

if links is None:
    fehler.append("kein Block 'if (mouse.LeftButton == ButtonState.Pressed)' in HandleRtsInput")
else:
    if "selectionStart" in links:
        fehler.append("die linke Taste zieht noch einen Auswahlrahmen auf")
    for pflicht in ("dragStart", "dragCameraStart", "isDragging", "DRAG_THRESHOLD", "cameraZoom"):
        if pflicht not in links:
            fehler.append(f"Block der linken Taste: {pflicht} fehlt")

# Seit C8b entscheidet LeftClick, was ein Klick bedeutet (Gebaeude setzen, eigene
# Einheit waehlen, sonst Befehl). Der Befehl darf deshalb direkt im Loslass-Zweig
# stehen oder in LeftClick - die erste Fassung verlangte ihn woertlich im Zweig.
klick = methode(text, "LeftClick") or ""
befehl_im_zweig = loslassen is not None and re.search(r"IssueCommand\s*\(", loslassen)
befehl_ueber_klick = (loslassen is not None and re.search(r"LeftClick\s*\(", loslassen)
                      and re.search(r"IssueCommand\s*\(", klick))
if loslassen is None:
    fehler.append("kein Zweig 'else if (dragStart.HasValue)' fuer das Loslassen der linken Taste")
elif not (befehl_im_zweig or befehl_ueber_klick) or "isDragging" not in loslassen:
    fehler.append("beim Loslassen der linken Taste: Befehl nur ohne Ziehen (isDragging) fehlt")

if len(re.findall(r"IssueCommand\s*\(", eingabe)) + len(re.findall(r"IssueCommand\s*\(", klick)) != 1:
    fehler.append("IssueCommand soll beim Klick genau einmal erreichbar sein (HandleRtsInput oder LeftClick)")
for pflicht in ("SelectUnitsInRectangle", "SelectSingleUnit"):
    if not re.search(rf"{pflicht}\s*\(", eingabe):
        fehler.append(f"HandleRtsInput ruft {pflicht} nicht mehr auf")
if re.search(r"\brightClick\b", eingabe):
    fehler.append("der alte Rechtsklick-Befehl (rightClick) ist noch da")

for feld in (r"Vector2\?\s+dragStart", r"Vector2\s+dragCameraStart", r"bool\s+isDragging",
             r"const\s+float\s+DRAG_THRESHOLD"):
    if not re.search(feld, text):
        fehler.append(f"Feld fehlt: {feld}")

schwenk = methode(text, "PanCamera") or ""
if "dragStart" not in schwenk:
    fehler.append("PanCamera: Kantenscrollen laeuft auch waehrend des Ziehens")

for pflicht in ("Rechts: auswählen", "Links ziehen"):
    if pflicht not in text:
        fehler.append(f"Hilfetext: '{pflicht}' fehlt")

melde(fehler, "C8a erfuellt: rechts markieren, links ziehen oder klicken")
