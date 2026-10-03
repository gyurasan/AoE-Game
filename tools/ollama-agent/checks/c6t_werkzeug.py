"""Abnahme C6t: die Dorfbewohner schwingen ihr Werkzeug, statt als Ganzes zu kippen.

Vorher war die Hacke ins Sprite gemalt; beim Arbeiten kippte DrawVillager die
ganze Figur vor und zurück (Hinweis des Nutzers, 2026-10-03). Jetzt ist das
Werkzeug ein eigenes Sprite hinter der Figur, um die Faust gedreht. Das
Verhalten (Werkzeug je Arbeit, Schlagverlauf) prüft tools/spielablauf, Gruppe
werkzeug - hier geht es um Bilder, Inhaltsliste und Zeichenreihenfolge.
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
WERKZEUGE = ["hacke", "axt", "spitzhacke", "hammer", "sichel", "angel"]

text = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for name in WERKZEUGE:
    if not (CONTENT / f"Werkzeuge/{name}.png").is_file():
        fehler.append(f"Bild Werkzeuge/{name}.png fehlt")
    if f"/build:Werkzeuge/{name}.png" not in mgcb:
        fehler.append(f"Werkzeuge/{name}.png ist nicht in AgeOfEvolutions.mgcb eingetragen")
    if f'"Werkzeuge/{name}"' not in text:
        fehler.append(f"der Spielbildschirm kennt Werkzeuge/{name} nicht")

dorf = methode(text, "DrawVillager") or ""
werkzeug, figur = dorf.find("DrawTool("), dorf.find("spriteBatch.Draw(figure")
if werkzeug < 0:
    fehler.append("DrawVillager zeichnet kein Werkzeug")
elif figur < 0 or werkzeug > figur:
    fehler.append("das Werkzeug liegt nicht hinter der Figur (DrawTool vor spriteBatch.Draw(figure))")
if not re.search(r"working\s*&&\s*!\s*hasTool", dorf):
    fehler.append("die Figur kippt beim Arbeiten auch mit Werkzeug - das Kippen gehört nur zum Fall ohne Werkzeugbild")
if werkzeug >= 0 and not re.search(r"DrawTool\([^;]*[,\s]motion\.FacingLeft\s*\)\s*;", dorf):
    fehler.append("DrawTool bekommt nicht die Blickrichtung der Figur - das Werkzeug zeigte sonst in die Gegenrichtung")
if "SwingAngle(" not in dorf or "TOOL_REST" not in dorf:
    fehler.append("DrawVillager wählt nicht zwischen Schwung (SwingAngle) und Ruhe (TOOL_REST)")

zeichnen = methode(text, "DrawTool") or ""
for pflicht, grund in (("VillagerFist", "dreht nicht um die Faust"),
                       ("tex.Width - grip.X", "spiegelt den Griffpunkt nicht mit"),
                       ("FlipHorizontally", "spiegelt das Werkzeug nicht")):
    if pflicht not in zeichnen:
        fehler.append(f"DrawTool {grund}")

griff = methode(text, "ToolGrip") or ""
if "GetData" not in griff:
    fehler.append("ToolGrip liest den Griffpunkt nicht aus dem Bild")

melde(fehler, "C6t erfuellt: Werkzeug als eigenes Sprite hinter der Figur, die Figur kippt nicht mehr")
