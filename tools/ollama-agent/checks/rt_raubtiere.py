"""Strukturprüfung Raubtiere (RT): Bären und Wölfe sind eingebunden.

  py rt_raubtiere.py rt3a    PredatorKilled lässt den erlegten Wolf zu Boden sinken
  py rt_raubtiere.py rt3b    Bilder geladen und gezeichnet, Content eingetragen
  py rt_raubtiere.py rt4     je acht Lauf- und Angriffsphasen (tieranim.py) geladen und gezeichnet

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
# Seit RT4 je acht Lauf- und Angriffsphasen aus tools/bilder/tieranim.py statt zweier Laufbilder
# und eines Angriffsbilds
BILDER = [f"Tiere/{n}{z}.png" for n in ("baer", "baer2", "wolf", "wolf2")
          for z in [""] + [f"_lauf{k}" for k in range(1, 9)] + [f"_angriff{k}" for k in range(1, 9)]]     + ["Tiere/fleisch_baer.png"]

stufe = sys.argv[1] if len(sys.argv) > 1 else "rt3b"
fehler = []
raub = lies(RAUB)

getoetet = methode(raub, "PredatorKilled") or ""
if "AddFallen" not in getoetet or "Wolf" not in getoetet:
    fehler.append("PredatorKilled ruft für einen Wolf nicht AddFallen auf")
if "EmitSound" not in getoetet:
    fehler.append("PredatorKilled spielt keinen Ton mehr (EmitSound \"tod\")")

if stufe in ("rt3b", "rt4"):
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
                          ("WOLF_WIDTH", "die Breite des Wolfs"), ("_animalAttack", "die Angriffsphasen"),
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

if stufe == "rt4":
    text = lies(SCREEN)
    tier = methode(text, "DrawAnimal") or ""
    for stueck, grund in (("PredatorAttackFrame(", "die Angriffsphase nicht am Schlagtakt (PredatorAttackFrame)"),
                          ("PredatorWalkFrame(", "die Laufphase nicht nach der Strecke (PredatorWalkFrame)"),
                          ("WalkPhase(", "die übrigen Tiere nicht mehr über WalkPhase"),
                          ("stand.Width", "das Angriffsbild nicht im Maßstab des Standbilds")):
        if stueck not in tier:
            fehler.append(f"DrawAnimal wählt {grund}")
    lade = methode(raub, "LoadPredatorSprites") or ""
    if not re.search(r'"_lauf"\s*\+', lade) or "Gait.WALK_FRAMES" not in lade:
        fehler.append("LoadPredatorSprites lädt nicht _lauf1 bis _lauf8 (Gait.WALK_FRAMES)")
    if not re.search(r'"_angriff"\s*\+', lade) or "PREDATOR_ATTACK_FRAMES" not in lade:
        fehler.append("LoadPredatorSprites lädt nicht _angriff1 bis _angriff8")
    if "_animalStrike" in raub or "_animalStrike" in text:
        fehler.append("das alte einzelne Angriffsbild (_animalStrike) ist noch eingebunden")
    if "tier.Engaged = " not in lies(Path("AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.Raubtiere.cs")):
        fehler.append("TileMap.UpdatePredators setzt WildAnimal.Engaged nicht")
    mgcb = (CONTENT / "AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
    for n in ("baer", "baer2", "wolf", "wolf2"):
        if f"Tiere/{n}_angriff.png" in mgcb or (CONTENT / f"Tiere/{n}_angriff.png").exists():
            fehler.append(f"Tiere/{n}_angriff.png (altes Einzelbild) ist noch da")
    katalog = __import__("json").loads(Path("tools/bilder/bilder.json").read_text(encoding="utf-8"))
    namen = {e["name"]: e for e in katalog.get("tieranim", {}).get("bilder", [])}
    for n in ("baer", "baer2", "wolf", "wolf2"):
        for art in ("lauf", "angriff"):
            for k in range(1, 9):
                e = namen.get(f"{n}_{art}{k}")
                if not e or e.get("ziel") != f"Tiere/{n}_{art}{k}.png":
                    fehler.append(f"bilder.json, Gruppe tieranim: {n}_{art}{k} fehlt oder ohne Ziel")
        for e in katalog["tiere_lauf"]["bilder"]:
            if e["name"].startswith(n + "_") and e.get("ziel"):
                fehler.append(f"bilder.json: {e['name']} (tiere_lauf) wird noch ins Spiel übernommen")

melde(fehler, f"Raubtiere ({stufe}): eingebunden")
