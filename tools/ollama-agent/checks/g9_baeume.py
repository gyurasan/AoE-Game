"""Abnahme G9: Bäume, die sich im Wind wiegen.

Die Bäume standen starr, während der Weizen daneben im Wind wogte (G7). Jetzt
zeichnet DrawTrees jeden Baum in Streifen (Wind.SwayStrips): der Stammfuß steht,
die Krone schwingt aus - so weit, wie Wind.TreeSway es für diesen Baum und diesen
Augenblick sagt, in denselben Böen wie der Weizen (Wind.Gust).

Was Wind rechnet, prüft tools/spielablauf (Gruppe wind), wie es aussieht, prüfen
Bildschirmfotos. Hier: die Rümpfe in Wind.cs sind gefüllt, und DrawTrees benutzt
sie mit der Spielzeit.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
wind = lies(KERN / "Wind.cs")
rts = lies(KERN / "RTSGameplayScreen.cs")
ablauf = lies(Path("tools/spielablauf/Program.cs"))
fehler = []

for name in ("Gust", "TreeSway", "SwayStrips"):
    rumpf = methode(wind, name)
    if rumpf is None:
        fehler.append(f"Wind.{name} fehlt")
    elif "NotImplementedException" in rumpf:
        fehler.append(f"Wind.{name} ist noch der leere Rumpf")
if "Math.Pow(" not in (methode(wind, "Gust") or "") and "MathF.Pow(" not in (methode(wind, "Gust") or ""):
    fehler.append("Wind.Gust schärft die Wellen nicht (pow wie in WheatWind)")

# Seit T1 zeichnet DrawTree je einen Baum (vorher DrawTrees alle einer Kachel)
baeume = methode(rts, "DrawTree") or methode(rts, "DrawTrees") or ""
if not re.search(r"\bWind\.TreeSway\s*\(", baeume):
    fehler.append("DrawTree fragt Wind.TreeSway nicht")
if not re.search(r"\banimationTime\b", baeume):
    fehler.append("DrawTree nimmt nicht die Spielzeit (animationTime) - die Bäume stünden still")
if not re.search(r"\bWind\.SwayStrips\s*\(", baeume):
    fehler.append("DrawTree zeichnet die Bäume nicht in Streifen (Wind.SwayStrips)")
if re.search(r"spriteBatch\.Draw\(\s*tex\s*,\s*new Rectangle\(", baeume):
    fehler.append("DrawTree zeichnet den Baum noch als Ganzes in ein Rechteck")

if not re.search(r"gruppen\.Contains\(\"wind\"\)", ablauf) or methode(ablauf, "WindProbe") is None:
    fehler.append("tools/spielablauf: Gruppe wind fehlt")

melde(fehler, "G9 erfuellt: Wind gefüllt, DrawTree biegt die Bäume im Wind")
