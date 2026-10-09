"""Abnahme T1b: Bäume mit offener Krone, einzeln in der Tiefenschicht.

Wunsch des Nutzers 2026-10-09: die Bäume in hoher Qualität neu, mit offener Krone wie die
Tannen, damit sie den Hintergrund nicht ganz verdecken, und etwas größer. Jeder Baum ist
ein eigener Eintrag der Tiefenschicht mit seinem Stammfuß als Sohle (TreeSpot), nicht die
ganze Kachel mit dem unteren Kachelrand; Höhe je Baumart aus TreeAssets; Laub- und
Nadelbäume in Hainen. Wie es aussieht, zeigen Fotos; hier die Struktur.
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

# Baumtabelle: Bild, Höhe in Welteinheiten, Nadelbaum; alle Bilder im Content und in der mgcb
tabelle = re.search(r"\(string\s+Asset,\s*float\s+Height,\s*bool\s+Conifer\)\[\]\s+TreeAssets\s*=\s*\{(.*?)\};",
                    text, re.S)
if not tabelle:
    fehler.append("TreeAssets ist keine Tabelle (Asset, Height, Conifer)")
else:
    baeume = re.findall(r'\(\s*"(Baeume/\w+)"\s*,\s*([\d.]+)f?\s*,\s*(true|false)\s*\)', tabelle.group(1))
    if len(baeume) < 6:
        fehler.append(f"TreeAssets nennt nur {len(baeume)} Bäume")
    if {k for _, _, k in baeume} != {"true", "false"}:
        fehler.append("TreeAssets braucht Laub- und Nadelbäume")
    mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
    for asset, hoehe, _ in baeume:
        if not (CONTENT / f"{asset}.png").is_file():
            fehler.append(f"{asset}.png fehlt im Content")
        if f"#begin {asset}.png" not in mgcb:
            fehler.append(f"{asset}.png steht nicht in AgeOfEvolutions.mgcb")
        if not 48 <= float(hoehe) <= 110:
            fehler.append(f"{asset}: Höhe {hoehe} - ein Baum soll 2 bis 4 Dorfbewohner (24) hoch sein")

if not re.search(r"static\s+readonly\s*\(int X,\s*int Y\)\[\]\s+TreeSpots", text):
    fehler.append("TreeSpots (Stammfüße je Waldkachel) fehlt")
if re.search(r"_treeSprites\s*=\s*TreeAssets\.Select\(\s*LoadOptional\s*\)", text):
    fehler.append("LoadContent lädt die Bäume noch ohne Höhe")

spot = rumpf("TreeSpot")
for pflicht, grund in (("73856093", "Ortshash der Kachel"), ("TreeSpots", "Lage aus TreeSpots"),
                       ("Conifer", "Haine aus Laub- oder Nadelbäumen")):
    if spot and pflicht not in spot:
        fehler.append(f"TreeSpot: {grund} fehlt ({pflicht})")

baum = rumpf("DrawTree")
for pflicht, grund in ((r"\bWind\.TreeSway\s*\(", "wiegt sich nicht im Wind"),
                       (r"\banimationTime\b", "nimmt nicht die Spielzeit"),
                       (r"\bWind\.SwayStrips\s*\(", "zeichnet nicht in Streifen"),
                       (r"\bTreeSpot\s*\(", "fragt TreeSpot nicht"),
                       (r"\bcameraZoom\b", "wächst nicht mit dem Zoom"),
                       (r"\bscreenBounds\b", "cullt nicht am Fenster")):
    if baum and not re.search(pflicht, baum):
        fehler.append(f"DrawTree {grund}")
if methode(text, "DrawTrees") is not None:
    fehler.append("DrawTrees gibt es noch - DrawTree zeichnet einen Baum")
if "_treeSprites" in (methode(text, "DrawCrowns") or ""):
    fehler.append("DrawCrowns zeichnet noch Baumbilder - die stehen einzeln in der Tiefenschicht")

for pflicht, grund in ((r"\bDrawTree\s*\(", "zeichnet keine einzelnen Bäume (DrawTree)"),
                       (r"\bDrawCrowns\s*\(", "hat keinen Rückfall auf gezeichnete Kronen"),
                       (r"\bTreeSpot\s*\(", "sortiert Bäume nicht nach dem Stammfuß (TreeSpot)")):
    if not re.search(pflicht, einheiten):
        fehler.append(f"DrawUnits {grund}")
if not re.search(r"public\s+int\s+Spot\b", text):
    fehler.append("DepthEntry hat kein Feld Spot (welcher Baum der Kachel)")

melde(fehler, "T1b erfuellt: Bäume mit offener Krone einzeln nach Stammfuß, Höhe je Art, Haine")
