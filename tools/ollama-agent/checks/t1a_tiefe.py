"""Abnahme T1a: Befunde aus der Durchsicht der Tiefenschicht (DrawUnits), 2026-10-09.

- Die Sortierung ist nicht stabil (List.Sort): ein Eintragsindex Seq entscheidet bei
  gleicher Sohle und Art, sonst tauschten überlappende Bäume von Bild zu Bild.
- Die Lebensbalken der Gebäude kamen in DrawTileMap und damit UNTER die Bäume und
  Gebäude der Tiefenschicht - jetzt zeichnet sie DrawUnits über alle Bilder.
- Tiere werden nach ihren Hufen auch mitten im Schritt sortiert (AnimalFoot).
- OwnUnitAt nimmt die vorderste Figur (Füße am weitesten unten), nicht die letzte der
  Liste - die Tiefenschicht zeichnet nicht mehr in Listenreihenfolge.
- DrawUnits läuft nur über Kacheln nahe am Fenster, nicht über die ganze Karte.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
text = lies()
fehler = []


def rumpf(name):
    r = methode(text, name)
    if r is None:
        fehler.append(f"{name} fehlt")
        return ""
    if "NotImplementedException" in r:
        fehler.append(f"{name} wirft noch NotImplementedException")
    return r


einheiten = methode(text, "DrawUnits") or ""

fuss = rumpf("AnimalFoot")
if fuss and not ("Gait.Ease" in fuss and "FromY" in fuss and "Look" in fuss):
    fehler.append("AnimalFoot rechnet Versatz und Rest des Schritts nicht mit (Look, Gait.Ease, FromY)")
if not re.search(r"\bAnimalFoot\s*\(", methode(text, "DrawAnimal") or ""):
    fehler.append("DrawAnimal nimmt die Hufe nicht aus AnimalFoot")

for pflicht, grund in ((r"\bAnimalFoot\s*\(", "sortiert Tiere nicht nach den Hufen (AnimalFoot)"),
                       (r"\bDrawBuildingHealth\s*\(", "zeichnet die Lebensbalken der Gebäude nicht"),
                       (r"\bSeq\s*=", "setzt den Eintragsindex Seq nicht"),
                       (r"\.Seq\b", "vergleicht Seq bei gleicher Sohle und Art nicht"),
                       (r"\bScreenToWorld\s*\(", "begrenzt die Kacheln nicht aufs Fenster")):
    if not re.search(pflicht, einheiten):
        fehler.append(f"DrawUnits {grund}")
if re.search(r"for\s*\(\s*int\s+y\s*=\s*0\s*;\s*y\s*<\s*tileMap\.Height", einheiten):
    fehler.append("DrawUnits läuft noch über alle Zeilen der Karte")
zeichnen = einheiten.find("DrawBuilding(spriteBatch")
balken = einheiten.find("DrawBuildingHealth(")
if zeichnen >= 0 and balken >= 0 and balken < zeichnen:
    fehler.append("DrawUnits zeichnet die Lebensbalken vor den Gebäuden")
if re.search(r"\bDrawBuildingHealth\s*\(", methode(text, "DrawTileMap") or ""):
    fehler.append("DrawTileMap zeichnet die Lebensbalken noch unter die Tiefenschicht")
if not re.search(r"public\s+int\s+Seq\b", text):
    fehler.append("DepthEntry hat kein Feld Seq")

wahl = methode(text, "OwnUnitAt") or ""
if not re.search(r"Position\.Y", wahl):
    fehler.append("OwnUnitAt wählt nicht die vorderste Figur (Position.Y)")

melde(fehler, "T1a erfuellt: stabile Tiefenordnung, Lebensbalken oben, Tiere nach Hufen, vorderste Figur")
