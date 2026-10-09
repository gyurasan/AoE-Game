"""Abnahme H1: Leisten im Stoff des Zeitalters.

Wunsch des Nutzers 2026-10-09: die untere Leiste mit den Tasten bekommt einen Hintergrund,
der zur Epoche passt - in der Dunklen Zeit Holz. Vier kachelbare Bilder (Gruppe leiste in
tools/bilder/bilder.json): rohe Holzbohlen, Eichenbohlen mit Eisenbeschlägen, Burgmauer,
dunkler Marmor mit Goldfugen. DrawPanel legt sie gekachelt (am Rand abgeschnitten, nicht
gestaucht) unter obere und untere Leiste und die Fläche hinter der Minimap; ein dunkles Feld
hält den Hilfetext auf hellem Stein lesbar. Wie es aussieht, zeigen Fotos.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
cs = lies()
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
fehler = []

for zeit in ("dunkel", "feudal", "ritter", "imperial"):
    if not (CONTENT / f"Leiste/{zeit}.png").is_file():
        fehler.append(f"Leiste/{zeit}.png fehlt")
    if f"#begin Leiste/{zeit}.png" not in mgcb:
        fehler.append(f"Leiste/{zeit}.png steht nicht in AgeOfEvolutions.mgcb")
reihe = re.search(r"PanelAssets\s*=\s*\{([^}]*)\}", cs)
if not reihe or re.findall(r'"Leiste/(\w+)"', reihe.group(1)) != ["dunkel", "feudal", "ritter", "imperial"]:
    fehler.append("PanelAssets nennt nicht die vier Leistenbilder in der Reihenfolge der Zeitalter")

if not re.search(r"_panelTex\s*\[\s*\w+\s*\]\s*=\s*LoadOptional\(\s*PanelAssets\s*\[", cs):
    fehler.append("LoadContent lädt die Leistenbilder nicht (LoadOptional(PanelAssets[i]))")

panel = methode(cs, "DrawPanel")
if panel is None:
    fehler.append("DrawPanel fehlt")
else:
    for pflicht, grund in ((r"AgeOf\(\s*0\s*\)", "richtet sich nicht nach dem Zeitalter von Spieler 0"),
                           (r"\bPANEL_TILE\b", "kachelt nicht in PANEL_TILE"),
                           (r"Math\.Min\(", "schneidet die letzte Kachel nicht ab"),
                           (r"new Color\(\s*94\s*,\s*76\s*,\s*48\s*\)", "hat keinen braunen Rückfall ohne Bild"),
                           (r"\bfor\s*\(|\bwhile\s*\(", "legt keine Kacheln in einer Schleife")):
        if not re.search(pflicht, panel):
            fehler.append(f"DrawPanel {grund}")
    if "NotImplementedException" in panel:
        fehler.append("DrawPanel wirft noch NotImplementedException")

ui = methode(cs, "DrawUI") or ""
aufrufe = re.findall(r"\bDrawPanel\s*\(\s*spriteBatch\s*,\s*(\w+)\s*\)", ui)
for flaeche in ("topBarRect", "bottomBarRect", "minimapPanel"):
    if flaeche not in aufrufe:
        fehler.append(f"DrawUI legt {flaeche} nicht mit DrawPanel")
if re.search(r"Draw\(\s*px\s*,\s*(bottomBarRect|minimapPanel|topBarRect)\s*,\s*new Color\(\s*94", ui):
    fehler.append("DrawUI malt eine Leiste noch einfarbig braun")
if "PanelEdge[" not in ui:
    fehler.append("DrawUI färbt die hellen Kanten nicht nach dem Zeitalter (PanelEdge)")
if not re.search(r"\*\s*0\.4\d*f", ui):
    fehler.append("DrawUI legt kein dunkles, halbdurchsichtiges Feld hinter den Hilfetext")

melde(fehler, "H1 erfuellt: Leisten in Holz, Eiche, Burgmauer und Marmor je Zeitalter")
