"""Abnahme G12: die Wiese aus fünf Grassorten in drei Höhen und ausgedörrter Erde.

Der Nutzer fand das Gras nach G11 zu eintönig, wie frisch gemäht (2026-10-09): Gras ist in
Wirklichkeit zwischen kurz und hüfthoch, drei bis vier Arten wären gut, und an einigen Stellen
darf es ausgedörrt kahl sein. Jetzt: Grundgras (ungemähte Wiese), kurzes Kleegras, hohes Gras,
hohes trockenes Gras, Blumenwiese und rissige Erde (Gruppe gras in tools/bilder/bilder.json).
Das hohe Gras wächst, wo das Spiel es sagt (Alphakanal des Lebendbilds = TallGrassAt) - dort
stehen auch die Büschel (G13). Wie es aussieht, zeigen Fotos; hier die Struktur.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
fx = re.sub(r"//[^\n]*", "", (CONTENT / "Effects/Boden.fx").read_text(encoding="utf-8-sig"))
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
cs = lies()
fehler = []


def rumpf(name):
    for treffer in re.finditer(rf"\b{name}\s*\([^)]*\)\s*(?::\s*\w+\s*)?\{{", fx):
        start, tiefe = treffer.end() - 1, 0
        for j in range(start, len(fx)):
            tiefe += {"{": 1, "}": -1}.get(fx[j], 0)
            if tiefe == 0:
                return fx[start:j + 1]
    return None


for bild, textur, sampler in (("Boden/gras_hoch", "TallGrassTexture", "TallSampler"),
                              ("Boden/erde_trocken", "BareEarthTexture", "BareSampler")):
    if not (CONTENT / f"{bild}.png").is_file():
        fehler.append(f"{bild}.png fehlt")
    if f"#begin {bild}.png" not in mgcb:
        fehler.append(f"{bild}.png steht nicht in AgeOfEvolutions.mgcb")
    if not re.search(rf"\bTexture2D\s+{textur}\s*;", fx):
        fehler.append(f"Boden.fx: Texture2D {textur} fehlt")
    dekl = re.search(rf"\bsampler2D\s+{sampler}\s*=\s*sampler_state\s*\{{([^}}]*)\}}", fx)
    if not dekl or not re.search(rf"Texture\s*=\s*<\s*{textur}\s*>", dekl.group(1)) \
            or not re.search(r"AddressU\s*=\s*Wrap", dekl.group(1)):
        fehler.append(f"Boden.fx: {sampler} fehlt, liest nicht {textur} oder wiederholt nicht (Wrap)")
    if not re.search(rf"LoadOptional\(\s*\"{re.escape(bild)}\"\s*\)", cs):
        fehler.append(f"RTSGameplayScreen lädt {bild} nicht")
    zuweisung = re.search(rf"\[\s*\"{textur}\"\s*\]\s*\??\.SetValue\(([^;]*)\);", methode(cs, "DrawGroundShaded") or "")
    if not zuweisung or "??" not in zuweisung.group(1):
        fehler.append(f"DrawGroundShaded übergibt {textur} nicht oder ohne Rückfall (??)")

meadow = rumpf("Meadow")
if meadow is None:
    fehler.append("Boden.fx: Meadow fehlt")
else:
    for sampler in ("GrassSampler", "DrySampler", "LushSampler", "FlowerSampler", "TallSampler", "BareSampler"):
        if sampler not in meadow:
            fehler.append(f"Meadow tastet {sampler} nicht ab")
    if meadow.count("GrassHeight(") < 6:
        fehler.append("Meadow überblendet nicht alle sechs Flächen nach Halmhöhe (GrassHeight)")
    if not re.search(r"\btallZone\b", meadow):
        fehler.append("Meadow nimmt die Zone des hohen Grases (tallZone) nicht")
if not re.search(r"Meadow\s*\([^;]*\blive\.a\b", rumpf("MainPS") or ""):
    fehler.append("MainPS gibt Meadow die Zone des hohen Grases nicht mit (live.a)")

hoch = methode(cs, "TallGrassAt") or ""
if not hoch or "NotImplementedException" in hoch:
    fehler.append("TallGrassAt fehlt oder ist leer")
else:
    for pflicht, grund in (("Grassland", "nur auf Wiese"), ("Building", "nicht unter Gebäuden"),
                           ("Wear", "auf Trampelpfaden niedergetreten"), ("ValueNoise(", "in Flecken aus Rauschen")):
        if pflicht not in hoch:
            fehler.append(f"TallGrassAt: {grund} fehlt ({pflicht})")
if "TallGrassAt(" not in (methode(cs, "RefreshGroundLive") or ""):
    fehler.append("RefreshGroundLive schreibt TallGrassAt nicht ins Lebendbild (Alpha)")

melde(fehler, "G12 erfuellt: Wiese aus fünf Grassorten in drei Höhen und ausgedörrter Erde")
