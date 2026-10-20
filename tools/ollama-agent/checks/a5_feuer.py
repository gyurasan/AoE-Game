"""Strukturprüfung (A5): Feuer der Schmiede und Kaminrauch sind eingebunden und zeichnen nach Vertrag.

  py a5_feuer.py

Was SmokePuff, FireFlicker, FlameTongue und Spark rechnen, prüft tools/spielablauf (Gruppe feuer).
Die Zeichenrümpfe brauchen ein GraphicsDevice - hier wird nur nachgesehen, dass sie die
vereinbarten Bausteine benutzen (Leerzeichen und Zeilenumbrüche zählen nicht), und dass
DrawBuilding sie nach dem Gebäudebild aufruft. Gegen den Altstand rot, weil es die Methoden
und Aufrufe dort nicht gibt.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

HAUPT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs")
TEIL = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.Animationen.cs")


def dicht(text: str) -> str:
    """Ohne Leerraum - so stört weder Einrückung noch Zeilenumbruch den Vergleich."""
    return re.sub(r"\s+", "", text)


fehler = []
haupt, teil = lies(HAUPT), lies(TEIL)


def braucht(name: str, text: str, bausteine: list[str]):
    rumpf = methode(text, name)
    if rumpf is None:
        fehler.append(f"{name} nicht gefunden")
        return
    if "NotImplementedException" in rumpf:
        fehler.append(f"{name} ist noch leer")
        return
    d = dicht(rumpf)
    for b in bausteine:
        if dicht(b) not in d:
            fehler.append(f"{name}: es fehlt {b}")


# Einbindung: Kamin und Feuerstelle gehören zum Bild, gezeichnet nach dem Gebäudebild
braucht("LoadBuildingSprite", haupt, ["ChimneyTops.TryGetValue(asset", "_chimneys[sprite] =",
                                      "ForgeFires.TryGetValue(asset", "_forgeFires[sprite] ="])
zeichnen = methode(haupt, "DrawBuilding")
if zeichnen is None:
    fehler.append("DrawBuilding nicht gefunden")
else:
    d = dicht(zeichnen)
    stellen = [d.find(dicht(s)) for s in ("DrawMillSails(", "DrawForgeFire(spriteBatch, sprite, area, phase);",
                                          "DrawChimneySmoke(spriteBatch, sprite, area, new Vector2(b.X, b.Y), phase);",
                                          "switch (b.Core.BuildingType)")]
    if -1 in stellen[1:3]:
        fehler.append("DrawBuilding ruft DrawForgeFire und DrawChimneySmoke nicht wie vereinbart auf")
    elif not stellen[0] < stellen[1] < stellen[2] < stellen[3]:
        fehler.append("DrawBuilding: Feuer und Rauch nicht nach dem Windrad und vor den gezeichneten Gebäuden")

# Zeichenrümpfe: additive Farben (Alpha 0) fürs Feuer, Rauch je Wolke nach SmokePuff
braucht("DrawForgeFire", teil, ["_forgeFires.TryGetValue(sprite", "animationTime + phase", "FireFlicker(t)",
                                "PuffTexture()", "new Color(255, 120, 40, 0) * (0.18f + 0.22f * flicker)",
                                "FlameTexture()", "FLAME_TONGUES", "FlameTongue(t, j)",
                                "new Color(255, 110, 20, 0) * 0.55f", "new Color(255, 214, 110, 0) * 0.5f",
                                "SPARKS", "Spark(t, k)", "new Color(255, 196, 90, 0) * glow"])
braucht("DrawChimneySmoke", teil, ["_chimneys.TryGetValue(sprite", "Wind.Gust(tile, animationTime)",
                                   "SMOKE_PUFFS", "SmokePuff(animationTime + phase, k, gust)", "0.003f",
                                   "Color.Lerp(new Color(128, 124, 120), new Color(214, 212, 208), age) * alpha"])
braucht("FlameTexture", teil, ["_flame", "MathF.Pow(v, 2.4f)", "0.48f", "SetData(", "Color.White * a"])

melde(fehler, "A5: Feuer und Rauch eingebunden, Zeichenrümpfe nach Vertrag")
