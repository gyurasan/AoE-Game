"""Strukturprüfung Raubtiere (RT): Bären und Wölfe sind eingebunden.

  py rt_raubtiere.py rt3a    PredatorKilled lässt den erlegten Wolf zu Boden sinken
  py rt_raubtiere.py rt3b    Bilder geladen und gezeichnet, Content eingetragen

Was die Raubtiere tun, prüfen die Tests (RaubtiereTests) und tools/spielablauf (Gruppe
raubtiere); wie sie aussehen, sieht nur ein Foto. Diese Datei fängt den vergessenen Schritt:
eine Einbindung, die keine Gruppe berührt, weil headless nichts gezeichnet wird.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import SCREEN, lies, methode, melde  # noqa: E402

RAUB = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.Raubtiere.cs")
CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
BILDER = [f"Tiere/{n}{z}.png" for n in ("baer", "baer2", "wolf", "wolf2")
          for z in ("", "_lauf1", "_lauf2", "_angriff")] + ["Tiere/fleisch_baer.png"]

stufe = sys.argv[1] if len(sys.argv) > 1 else "rt3b"
fehler = []
raub = lies(RAUB)

getoetet = methode(raub, "PredatorKilled") or ""
if "AddFallen" not in getoetet or "Wolf" not in getoetet:
    fehler.append("PredatorKilled ruft für einen Wolf nicht AddFallen auf")
if "EmitSound" not in getoetet:
    fehler.append("PredatorKilled spielt keinen Ton mehr (EmitSound \"tod\")")

if stufe == "rt3b":
    text = lies(SCREEN)
    lade = methode(text, "LoadContent") or ""
    if "LoadPredatorSprites()" not in lade:
        fehler.append("LoadContent ruft LoadPredatorSprites() nicht auf")
    sprites = methode(text, "AnimalSprites") or ""
    for art, feld in (("Bear", "_bearSprites"), ("Wolf", "_wolfSprites")):
        if not re.search(rf"FoodSource\.{art}\s*=>\s*{feld}", sprites):
            fehler.append(f"AnimalSprites: FoodSource.{art} liefert nicht {feld}")
    tier = methode(text, "DrawAnimal") or ""
    for stueck, grund in (("_bearMeat", "das Fleisch des Bären"), ("BEAR_WIDTH", "die Breite des Bären"),
                          ("WOLF_WIDTH", "die Breite des Wolfs"), ("_animalStrike", "das Angriffsbild"),
                          ("DrawPredatorHealth(", "den Lebensbalken")):
        if stueck not in tier:
            fehler.append(f"DrawAnimal zeichnet {grund} nicht ({stueck} fehlt)")
    zeichnen = methode(text, "Draw") or ""
    i, j = zeichnen.find("DrawTileMap(spriteBatch)"), zeichnen.find("DrawFallen(spriteBatch)")
    if j < 0 or i < 0 or not (i < j < zeichnen.find("DrawUnits(spriteBatch)")):
        fehler.append("Draw ruft DrawFallen nicht zwischen DrawTileMap und DrawUnits auf")
    einheiten = methode(text, "UpdateUnits") or ""
    if "UpdateFallen(dt)" not in einheiten:
        fehler.append("UpdateUnits ruft UpdateFallen(dt) nicht auf")
    mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
    for bild in BILDER:
        if f"#begin {bild}" not in mgcb or f"/build:{bild}" not in mgcb:
            fehler.append(f"AgeOfEvolutions.mgcb: {bild} nicht eingetragen")
        if not (CONTENT / bild).exists():
            fehler.append(f"Bild fehlt: Content/{bild}")

melde(fehler, f"Raubtiere ({stufe}): eingebunden")
