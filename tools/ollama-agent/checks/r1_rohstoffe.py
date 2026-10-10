"""Abnahme R1: die Rohstoffsymbole der oberen Leiste als Bilder, doppelt so groß.

Wunsch des Nutzers 2026-10-10: die vier Anzeigen oben (Nahrung, Holz, Gold, Stein) sollen
Bilder statt farbiger 12-px-Quadrate zeigen, etwa doppelt so groß. Vier freigestellte Bilder
(Gruppe rohstoff_icons in tools/bilder/bilder.json), gezeichnet in RESOURCE_ICON_SIZE, mit dem
farbigen Quadrat als Rückfall. Wie es aussieht, zeigen Fotos.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
cs = lies()
fehler = []

tabelle = re.search(r"ResourceIcons\s*=\s*\{(.*?)\};", cs, re.S)
eintraege = re.findall(r"Resource\.(\w+)\s*,\s*\"(Icons/\w+)\"", tabelle.group(1)) if tabelle else []
if [r for r, _ in eintraege] != ["Food", "Wood", "Gold", "Stone"]:
    fehler.append("ResourceIcons nennt nicht Nahrung, Holz, Gold, Stein in dieser Reihenfolge")
for _, asset in eintraege:
    if not (CONTENT / f"{asset}.png").is_file():
        fehler.append(f"{asset}.png fehlt")
    if f"#begin {asset}.png" not in mgcb:
        fehler.append(f"{asset}.png steht nicht in AgeOfEvolutions.mgcb")

groesse = re.search(r"const\s+int\s+RESOURCE_ICON_SIZE\s*=\s*(\d+)\s*;", cs)
if not groesse or not 22 <= int(groesse.group(1)) <= 30:
    fehler.append("RESOURCE_ICON_SIZE fehlt oder ist nicht etwa doppelt so groß wie die alten 12 px (22 bis 30)")
if not re.search(r"_resourceIconTex\s*\[\s*\w+\s*\]\s*=\s*LoadOptional\(", cs):
    fehler.append("LoadContent lädt die Rohstoffsymbole nicht")

ui = methode(cs, "DrawUI") or ""
if "_resourceIconTex" not in ui or "RESOURCE_ICON_SIZE" not in ui:
    fehler.append("DrawUI zeichnet die Rohstoffsymbole nicht in RESOURCE_ICON_SIZE")
if re.search(r"var\s+(food|wood|gold|stone)Rect\s*=", ui):
    fehler.append("DrawUI hat noch die alten Rohstoff-Quadrate einzeln")
for res in ("Food", "Wood", "Gold", "Stone"):
    if f"Resource.{res}" not in cs:
        fehler.append(f"Resource.{res} wird nicht angezeigt")
if not re.search(r"player1\.Resources\s*\[", ui):
    fehler.append("DrawUI zeigt den Vorrat nicht mehr an")

melde(fehler, "R1 erfuellt: Rohstoffsymbole oben als Bilder, doppelt so groß")
