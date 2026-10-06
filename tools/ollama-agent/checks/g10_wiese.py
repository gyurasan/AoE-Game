"""Abnahme G10: die Wiese aus vier Grassorten statt eines getönten Grasbilds.

Bis 2026-10-06 war das Gras ein einziges Pixelkunst-Bild mit gleichmäßig
verstreuten Blütenpunkten, großflächig gelblich oder dunkler getönt - der Nutzer
fand es künstlich. Jetzt malt Qwen-Image vier Sorten (Grundgras, trockenes Gras,
dunkles Gras mit Klee, Blumenwiese, Gruppe `gras` in tools/bilder/bilder.json),
und Boden.fx blendet sie in Meadow nach Halmhöhe ineinander.

Wie die Wiese aussieht, zeigen Bildschirmfotos. Hier: die Bilder liegen vor und
stehen in der Inhaltsliste, der Shader deklariert und nutzt alle vier Sorten, der
Rumpf von Meadow ist gefüllt und überblendet nach Höhe (max), MainPS holt das Gras
nur noch über Meadow, und das Spiel lädt die Bilder und übergibt sie dem Shader.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
FX = CONTENT / "Effects/Boden.fx"
fx = re.sub(r"//[^\n]*", "", FX.read_text(encoding="utf-8-sig"))   # Kommentare zählen nicht
mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
cs = lies()
fehler = []

SORTEN = {"Boden/gras_trocken": ("DryGrassTexture", "DrySampler"),
          "Boden/gras_dunkel": ("LushGrassTexture", "LushSampler"),
          "Boden/gras_blumen": ("FlowerGrassTexture", "FlowerSampler")}

for bild, (textur, sampler) in SORTEN.items():
    if not (CONTENT / f"{bild}.png").is_file():
        fehler.append(f"{bild}.png fehlt")
    if not re.search(rf"^/build:{re.escape(bild)}\.png\s*$", mgcb, re.M):
        fehler.append(f"{bild}.png steht nicht in AgeOfEvolutions.mgcb")
    if not re.search(rf"\bTexture2D\s+{textur}\s*;", fx):
        fehler.append(f"Boden.fx: Texture2D {textur} fehlt")
    deklaration = re.search(rf"\bsampler2D\s+{sampler}\s*=\s*sampler_state\s*\{{([^}}]*)\}}", fx)
    if not deklaration:
        fehler.append(f"Boden.fx: sampler2D {sampler} fehlt")
    else:
        if not re.search(rf"Texture\s*=\s*<\s*{textur}\s*>", deklaration.group(1)):
            fehler.append(f"Boden.fx: {sampler} liest nicht {textur}")
        if not re.search(r"AddressU\s*=\s*Wrap", deklaration.group(1)):
            fehler.append(f"Boden.fx: {sampler} wiederholt das Bild nicht (AddressU = Wrap)")
    if not re.search(rf"LoadOptional\(\s*\"{re.escape(bild)}\"\s*\)", cs):
        fehler.append(f"RTSGameplayScreen lädt {bild} nicht (LoadOptional)")
    zuweisung = re.search(rf"\[\s*\"{textur}\"\s*\]\s*\??\.SetValue\(([^;]*)\);", methode(cs, "DrawGroundShaded") or "")
    if not zuweisung:
        fehler.append(f"DrawGroundShaded übergibt {textur} nicht")
    elif "_grassTex" not in zuweisung.group(1):
        fehler.append(f"DrawGroundShaded: für {textur} fehlt der Rückfall aufs Grundgras (_grassTex)")

def rumpf(name: str) -> str | None:
    """Rumpf einer HLSL-Funktion; anders als in C# darf zwischen Parameterliste und
    Rumpf eine Semantik stehen (MainPS(...) : COLOR) - daran scheitert _cs.methode."""
    for treffer in re.finditer(rf"\b{name}\s*\([^)]*\)\s*(?::\s*\w+\s*)?\{{", fx):
        start, tiefe = treffer.end() - 1, 0
        for j in range(start, len(fx)):
            tiefe += {"{": 1, "}": -1}.get(fx[j], 0)
            if tiefe == 0:
                return fx[start:j + 1]
    return None


meadow = rumpf("Meadow")
if meadow is None:
    fehler.append("Boden.fx: Meadow fehlt")
else:
    for sampler in ("GrassSampler", "DrySampler", "LushSampler", "FlowerSampler"):
        if sampler not in meadow:
            fehler.append(f"Meadow tastet {sampler} nicht ab")
    if meadow.count("GrassSampler") < 2:
        fehler.append("Meadow tastet das Grundgras nur einmal ab (zweite, gedrehte Lage fehlt)")
    if "GrassHeight(" not in meadow:
        fehler.append("Meadow überblendet nicht nach Halmhöhe (GrassHeight)")
    if "max(" not in meadow:
        fehler.append("Meadow sucht keine stärkste Sorte (max)")
hoehe = rumpf("GrassHeight")
if hoehe is None or "Luma(" not in hoehe:
    fehler.append("Boden.fx: GrassHeight fehlt oder rechnet nicht mit der Helligkeit (Luma)")

hauptteil = rumpf("MainPS") or ""
if "Meadow(" not in hauptteil:
    fehler.append("MainPS ruft Meadow nicht auf")
if "GrassSampler" in hauptteil:
    fehler.append("MainPS tastet das Grasbild noch selbst ab - das Gras kommt aus Meadow")
if not re.search(r"\bforest\b", hauptteil):
    fehler.append("MainPS rechnet die Waldnähe (forest) nicht mehr")

melde(fehler, "G10 erfuellt: Wiese aus vier Grassorten, nach Halmhöhe überblendet, im Spiel geladen")
