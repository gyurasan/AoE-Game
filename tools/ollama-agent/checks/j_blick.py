"""Abnahme J: Dorfbewohner, Schafe und Wild blicken in Laufrichtung - in Seitenansicht.

Die Figuren gibt es nur von der Seite (nach rechts gemalt, nach links gespiegelt).
Vorher blickte ein fast senkrecht gehender Dorfbewohner in die Richtung, die er
zuletzt hatte, und er arbeitete oft genau über oder unter Baum, Stein oder Gold -
und schlug seitlich ins Leere. Jetzt entscheidet Gait.FacingLeft (Bewegung, Ziel,
bisheriger Blick), Arbeitsplätze liegen bevorzugt seitlich (StandCell,
SiteStandCell), Tiere machen kaum rein senkrechte Schritte (WanderStep).

Das Verhalten prüft tools/spielablauf, Gruppe blick. Hier: dass die Blickrichtung
über Gait.FacingLeft läuft und die alte Regel weg ist.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

KERN = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
gait = lies(KERN / "Gait.cs")
rts = lies(KERN / "RTSGameplayScreen.cs")
fehler = []

if "NotImplementedException" in (methode(gait, "FacingLeft") or "NotImplementedException"):
    fehler.append("Gait.FacingLeft ist noch der leere Rumpf")
bewegung = methode(rts, "UpdateUnitMotion") or ""
if not re.search(r"Gait\.FacingLeft\(", bewegung):
    fehler.append("UpdateUnitMotion setzt die Blickrichtung nicht über Gait.FacingLeft")
if re.search(r"Math\.Abs\(\s*delta\.X\s*\)\s*>\s*0\.01f", bewegung):
    fehler.append("UpdateUnitMotion hat noch die alte Regel (Math.Abs(delta.X) > 0.01f)")

melde(fehler, "J erfuellt: Blickrichtung über Gait.FacingLeft")
