"""Abnahme E8: Auswahl, Auswahlrahmen und Lebensbalken beziehen sich auf die ganze Figur.

Eine Einheit steht mit den Fuessen auf ihrer Position und ist 24 Welteinheiten
breit und hoch. Vorher waren Trefferflaeche (16 x 16) und Rahmen auf die Fuesse
zentriert: ein Klick auf den Kopf traf nichts, der Rahmen sass eine halbe
Figur zu tief (Screenshot 2026-09-30).
"""

import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

text = lies()
fehler = []

flaeche = methode(text, "UnitWorldRect")
if flaeche is None:
    fehler.append("Methode UnitWorldRect fehlt")
elif not all(re.search(m, flaeche) for m in (r"Position\.X\s*-\s*12", r"Position\.Y\s*-\s*24", r"24\s*,\s*24")):
    fehler.append("UnitWorldRect beschreibt nicht die Figur (X-12, Y-24, 24 x 24)")

rahmen = methode(text, "SelectUnitsInRectangle") or ""
if "UnitWorldRect" not in rahmen:
    fehler.append("SelectUnitsInRectangle prueft nicht gegen UnitWorldRect")
einzel_kopf = re.search(r"void\s+SelectSingleUnit\s*\(\s*Vector2\s+worldPos\s*\)", text)
einzel = methode(text, "SelectSingleUnit") or ""
if not einzel_kopf:
    fehler.append("SelectSingleUnit nimmt nicht die Weltposition (Vector2 worldPos)")
# Seit C8b steht die Trefferpruefung in OwnUnitAt, das SelectSingleUnit und der
# Linksklick gemeinsam nutzen; die erste Fassung verlangte sie woertlich hier.
treffer = einzel
if re.search(r"OwnUnitAt\s*\(", einzel):
    treffer += methode(text, "OwnUnitAt") or ""
if "UnitWorldRect" not in treffer or ".Contains(" not in treffer:
    fehler.append("SelectSingleUnit prueft nicht, ob der Zeiger in UnitWorldRect liegt")
if "GridToWorld" in treffer:
    fehler.append("SelectSingleUnit rechnet noch ueber die Kachel statt ueber den Zeiger")
if re.search(r"Position\.X\s*-\s*8\s*\)", text):
    fehler.append("noch eine 16x16-Trefferflaeche um die Fuesse")
eingabe = methode(text, "HandleRtsInput") or ""
if not re.search(r"SelectSingleUnit\s*\(\s*ScreenToWorld\s*\(", eingabe):
    fehler.append("HandleRtsInput uebergibt SelectSingleUnit nicht die Weltposition des Zeigers")

zeichnen = methode(text, "DrawUnits") or ""
if re.search(r"screenPos\.Y\s*-\s*size\s*/\s*2", zeichnen):
    fehler.append("DrawUnits: Rahmen oder Balken noch auf die Fuesse zentriert (screenPos.Y - size / 2)")
if not re.search(r"screenPos\.Y\s*-\s*size\s*\)", zeichnen):
    fehler.append("DrawUnits: Rahmen beginnt nicht am Kopf (screenPos.Y - size)")
if re.search(r",\s*20\s*,\s*3\s*\)", zeichnen):
    fehler.append("DrawUnits: Lebensbalken noch fest 20 x 3 Pixel")
if not re.search(r"3\s*\*\s*cameraZoom", zeichnen):
    fehler.append("DrawUnits: Lebensbalken waechst nicht mit dem Zoom")

melde(fehler, "E8 erfuellt: Auswahl und Anzeige beziehen sich auf die ganze Figur")
