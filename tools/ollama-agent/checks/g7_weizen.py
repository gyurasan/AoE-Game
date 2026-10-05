"""Abnahme G7: Weizen, der sich im Wind bewegt.

Die reifen Felder standen still. Jetzt zeichnet RTSGameplayScreen.DrawWheat die
Weizenschicht jeder Feldkachel mit dem Effekt Effects/Weizen.fx: die Ähren neigen
sich mit dem Wind, Böen laufen als helle Bänder über das Feld, der Zaun am Rand
steht still. Den Wind rechnet WheatWind (Vertrag als Kommentar darüber); den
nackten Acker darunter zeichnet DrawField wie bisher.

Wie es aussieht, zeigen Bildschirmfotos. Hier: WheatWind ist gefüllt und hält die
prüfbaren Teile seines Vertrags ein, MainPS ist unverändert, der Effekt wird
gebaut, geladen und angeschlossen, und ohne ihn bleibt der Weizen wie bisher.
Dass der Shader für OpenGL und DirectX übersetzt, prüfen die Builds davor.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
FX = KERN / "Content/Effects/Weizen.fx"
fx = FX.read_text(encoding="utf-8-sig") if FX.is_file() else ""
mgcb = (KERN / "Content/AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
rts = lies(KERN / "Screens/RTSGameplayScreen.cs")
fehler = []

if not fx:
    fehler.append(f"{FX} fehlt")
wind = re.sub(r"//[^\n]*", "", methode(fx, "WheatWind") or "")
if not wind:
    fehler.append("WheatWind fehlt in Weizen.fx")
else:
    anweisungen = [a.strip() for a in wind.strip().strip("{}").split(";") if a.strip()]
    if all(a.startswith("return") for a in anweisungen):
        fehler.append("WheatWind ist noch der leere Rumpf")
    if re.search(r"\btex2D\w*\s*\(", wind):
        fehler.append("WheatWind greift auf eine Textur zu - der Vertrag erlaubt das nicht")
    if re.search(r"\b(for|while|do)\b", wind):
        fehler.append("WheatWind enthält eine Schleife")
    if re.search(r"\b(frac|floor)\s*\(", wind):
        fehler.append("WheatWind benutzt frac oder floor - der Wind wäre nicht stetig")
    for name in ("world", "t"):
        if not re.search(rf"\b{name}\b", wind):
            fehler.append(f"WheatWind benutzt {name} nicht")
    if not re.search(r"\bsin\s*\(|\bcos\s*\(", wind):
        fehler.append("WheatWind hat keine Wellen (kein sin/cos)")
    if not re.search(r"0\.35", wind):
        fehler.append("WheatWind: Windrichtung nicht wie im Vertrag (normalize(float2(1.0, 0.35)))")

# MainPS bleibt, wie sie ist
haupt = fx.split("float4 MainPS(", 1)[-1].split("\ntechnique", 1)[0]
for zeile in ("float3 wind = WheatWind(world, Time);",
              "float inner = smoothstep(0.07, 0.12, edge);",
              "color.rgb = min(color.rgb * lerp(1.0, wind.z, inner), color.a);"):
    if zeile not in haupt:
        fehler.append(f"Weizen.fx MainPS: '{zeile}' fehlt - MainPS sollte unverändert bleiben")

if not re.search(r"#begin Effects/Weizen\.fx\s+/importer:EffectImporter\s+/processor:EffectProcessor", mgcb) \
        or "/build:Effects/Weizen.fx" not in mgcb:
    fehler.append("AgeOfEvolutions.mgcb baut Effects/Weizen.fx nicht als Effekt")

if not re.search(r"\bprivate Effect _wheatEffect\b", rts):
    fehler.append("Feld _wheatEffect fehlt")
if not re.search(r"_wheatEffect\s*=\s*ScreenManager\.Game\.Content\.Load<Effect>\(\s*\"Effects/Weizen\"\s*\)",
                 methode(rts, "LoadContent") or ""):
    fehler.append("LoadContent lädt Effects/Weizen nicht nach _wheatEffect")
weizen = methode(rts, "DrawWheat")
if weizen is None:
    fehler.append("RTSGameplayScreen.DrawWheat fehlt")
else:
    for pflicht, grund in ((r"SpriteSortMode\.Immediate", "zeichnet nicht in SpriteSortMode.Immediate - PartRect und TileOrigin gälten sonst nicht je Kachel"),
                           (r"_wheatEffect\b", "benutzt _wheatEffect nicht"),
                           (r"\"PartRect\"", "setzt PartRect nicht"),
                           (r"\"TileOrigin\"", "setzt TileOrigin nicht"),
                           (r"\"Time\"", "setzt Time nicht"),
                           (r"\bWheatLook\s*\(", "nimmt Deckkraft und Ton nicht aus WheatLook"),
                           (r"\bFieldPart\s*\(", "zeichnet nicht den Teil des Feldbilds (FieldPart)"),
                           (r"\.Farm\b", "fragt nicht nach Feldkacheln (tile.Farm)")):
        if not re.search(pflicht, weizen):
            fehler.append(f"DrawWheat {grund}")
zeichnen = methode(rts, "Draw") or ""
if "DrawWheat(" not in zeichnen:
    fehler.append("Draw ruft DrawWheat nicht auf")
else:
    # DrawWheat öffnet einen eigenen SpriteBatch: erst nach dem End() des Bodens.
    # Die erste Fassung rief es dazwischen auf - Absturz beim ersten Bild
    boden = zeichnen.find("DrawGround(spriteBatch);")
    ende = zeichnen.find("spriteBatch.End();", boden)
    if boden < 0 or ende < 0 or zeichnen.find("DrawWheat(") < ende:
        fehler.append("Draw ruft DrawWheat vor spriteBatch.End() des Bodens auf - Begin in einem offenen SpriteBatch stürzt ab")
if weizen is not None and not re.search(r"///\s*</summary>[ \t]*\r?\n[ \t]*private void DrawWheat\b", rts):
    fehler.append("DrawWheat hat keinen XML-Kommentar")
feld = methode(rts, "DrawField") or ""
if "_wheatEffect" not in feld:
    fehler.append("DrawField zeichnet den Weizen weiter selbst, auch wenn DrawWheat ihn zeichnet")

melde(fehler, "G7 erfuellt: WheatWind gefüllt, Weizen-Effekt gebaut, geladen und angeschlossen")
