"""Abnahme G6: Fische, die man als Fische erkennt - im Bodenshader gezeichnet.

Bis 2026-10-05 waren die Fischschwärme drei dunkle Ellipsen, die im Kreis zogen,
und davor ein gezeichnetes Kachelbild mit drei hellen Strichen; der Nutzer
erkannte beides nicht als Fische. Jetzt zeichnet Boden.fx jeden Fisch mit
Körper, gegabelter Schwanzflosse, Brustflossen und Schwanzschlag (FishMask), auf
einer eigenen geschwungenen Bahn (FishPath), mit Schatten auf dem Grund und
gelegentlichen Ringen an der Oberfläche (FishRing). Die Verträge stehen als
Kommentar über den drei Funktionen; MainPS bindet sie ein.

Ob die Fische wie Fische aussehen, zeigen Bildschirmfotos. Hier: die drei Rümpfe
sind gefüllt und halten die prüfbaren Teile ihres Vertrags ein, und die
Einbindung in MainPS ist unverändert. Dass der Shader für OpenGL und DirectX
übersetzt, prüfen die Builds davor.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import melde, methode  # noqa: E402

FX = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content/Effects/Boden.fx")
text = FX.read_text(encoding="utf-8-sig")
fehler = []


def rumpf(name: str) -> str:
    teil = methode(text, name)
    if teil is None:
        fehler.append(f"{name} fehlt in Boden.fx")
        return ""
    # Kommentare zählen nicht als Code
    return re.sub(r"//[^\n]*", "", teil)


def nur_return(teil: str) -> bool:
    anweisungen = [a.strip() for a in teil.strip().strip("{}").split(";") if a.strip()]
    return all(a.startswith("return") or a.startswith("dir =") for a in anweisungen)


pfad = rumpf("FishPath")
maske = rumpf("FishMask")
ring = rumpf("FishRing")

for name, teil in (("FishPath", pfad), ("FishMask", maske), ("FishRing", ring)):
    if teil and nur_return(teil):
        fehler.append(f"{name} ist noch der leere Rumpf")
    if re.search(r"\btex2D\w*\s*\(", teil):
        fehler.append(f"{name} greift auf eine Textur zu - der Vertrag erlaubt das nicht")

if pfad:
    for name in ("t", "seed", "k"):
        if not re.search(rf"\b{name}\b", pfad):
            fehler.append(f"FishPath benutzt {name} nicht - die Bahnen hingen nicht von Zeit, Schwarm und Fisch ab")
    # normalize, oder durch die Länge teilen - auch über eine Zwischenvariable
    # (dl = length(d); dir = dl > 0.0001 ? d / dl : ...). Die erste Fassung dieser
    # Prüfung verlangte "/ max(length" und verwarf so eine korrekte Lösung
    if not re.search(r"\bnormalize\s*\(|\blength\s*\(|\brsqrt\s*\(", pfad):
        fehler.append("FishPath bringt dir nicht auf Länge 1 (normalize oder Division durch die Länge)")
    if not re.search(r"\b(sin|cos)\s*\(", pfad):
        fehler.append("FishPath hat keine geschwungene Bahn (kein sin/cos)")

if maske:
    if re.search(r"\b(for|while|do)\b", maske):
        fehler.append("FishMask enthält eine Schleife - sie läuft achtmal je Bildpunkt")
    for name in ("beat", "dir"):
        if not re.search(rf"\b{name}\b", maske):
            fehler.append(f"FishMask benutzt {name} nicht")
    if not re.search(r"\b(smoothstep|saturate)\s*\(", maske):
        fehler.append("FishMask hat keinen weichen Rand (smoothstep oder saturate)")
    if "0.035" not in maske:
        fehler.append("FishMask: Schwanzschlag nicht wie im Vertrag (beat * 0.035 * s * s)")

if ring:
    for name in ("t", "seed", "f"):
        if not re.search(rf"\b{name}\b", ring):
            fehler.append(f"FishRing benutzt {name} nicht")

# Die Einbindung in MainPS bleibt, wie sie ist. methode() findet MainPS nicht: dort
# steht zwischen Parameterliste und Rumpf noch ": COLOR"
haupt = text.split("float4 MainPS(", 1)[-1].split("\ntechnique", 1)[0]
for zeile in ("float2 pos = FishPath(Time, seed, k, dir);",
              "fish = max(fish, FishMask(f - pos, dir, beat));",
              "shadow = max(shadow, FishMask(f - pos - shadowOffset, dir, beat));",
              "FishRing(f, Time, seed)"):
    if zeile not in haupt:
        fehler.append(f"MainPS: '{zeile}' fehlt - die Einbindung sollte unverändert bleiben")

melde(fehler, "G6 erfuellt: FishPath, FishMask und FishRing gefüllt, Einbindung unverändert")
