"""Abnahme G11: das Gras feiner als Sand und Wasser (eigener Maßstab GrassScale).

Der Nutzer fand am 2026-10-09 das Gras im Verhältnis zu den Bäumen zu groß gezeichnet und
einige Sorten nicht gut genug. Die vier Grasbilder sind neu (1328 Pixel, viel feinere Halme,
großflächig ausgeglichen, Gruppe gras in tools/bilder/bilder.json); der Bodenshader legt sie
mit eigenem Maßstab GRASS_TEXELS (mehr Bildpixel je Welteinheit als GROUND_TEXELS für Sand und
Wasser). Wie fein es wirkt, zeigen Fotos; hier die Struktur.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
fx = re.sub(r"//[^\n]*", "", (CONTENT / "Effects/Boden.fx").read_text(encoding="utf-8-sig"))
cs = lies()
fehler = []

konstanten = dict(re.findall(r"const\s+int\s+(GROUND_TEXELS|GRASS_TEXELS)\s*=\s*(\d+)\s*;", cs))
if "GRASS_TEXELS" not in konstanten:
    fehler.append("GRASS_TEXELS fehlt")
elif int(konstanten["GRASS_TEXELS"]) <= int(konstanten.get("GROUND_TEXELS", "4")):
    fehler.append("GRASS_TEXELS ist nicht größer als GROUND_TEXELS - das Gras würde nicht feiner")

boden = methode(cs, "DrawGroundShaded") or ""
gras = re.search(r'\[\s*"GrassScale"\s*\]\s*\??\.SetValue\(([^;]*)\);', boden)
if not gras or "GRASS_TEXELS" not in gras.group(1) or "_grassTex" not in gras.group(1):
    fehler.append("DrawGroundShaded übergibt GrassScale nicht aus GRASS_TEXELS und der Breite des Grasbilds")
detail = re.search(r'\[\s*"DetailScale"\s*\]\s*\??\.SetValue\(([^;]*)\);', boden)
if not detail or "_sandTex" not in detail.group(1):
    fehler.append("DetailScale rechnet nicht mit der Breite des Sandbilds - Sand und Wasser würden mit wachsen")

if not re.search(r"\bfloat\s+GrassScale\s*;", fx):
    fehler.append("Boden.fx: float GrassScale fehlt")
if not re.search(r"Meadow\s*\(\s*tile\s*\*\s*GrassScale\b", fx):
    fehler.append("Boden.fx: MainPS ruft Meadow nicht mit tile * GrassScale auf")

breiten = set()
for name in ("gras", "gras_trocken", "gras_dunkel", "gras_blumen"):
    pfad = CONTENT / f"Boden/{name}.png"
    if not pfad.is_file():
        fehler.append(f"Boden/{name}.png fehlt")
        continue
    kopf = pfad.read_bytes()[16:24]   # IHDR: Breite, Höhe - ohne das ganze Bild zu entpacken
    w, h = int.from_bytes(kopf[:4], "big"), int.from_bytes(kopf[4:], "big")
    breiten.add((w, h))
    if w < 1300 or w != h:
        fehler.append(f"Boden/{name}.png ist {w}x{h} - erwartet die neuen, quadratischen 1328er Bilder")
if len(breiten) > 1:
    fehler.append(f"die vier Grasbilder sind verschieden groß ({breiten}) - Meadow legt sie im selben Maßstab")

melde(fehler, "G11 erfuellt: Gras im eigenen, feineren Maßstab, Sand und Wasser unverändert")
