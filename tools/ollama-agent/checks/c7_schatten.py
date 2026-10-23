"""Strukturprüfung (C7sch3): Schlagschatten - Rümpfe und Einbindung, in vier Stufen.

  py c7_schatten.py teil1|teil2|einbau1|einbau2|fertig

Was HalfAlpha, BakeShadowMask und ShadowDest rechnen, prüft tools/spielablauf (Gruppe
schatten). Diese Datei sieht nach, was ohne Grafik nicht zu prüfen ist: dass die
Grafikrümpfe nach Vertrag gebaut sind (Maske gemerkt, eigener Batch mit Maximum,
Render-Target, Bildpuffer bleibt erhalten) und dass jedes Objekt seinen Schatten direkt
vor dem eigenen Bild wirft. Jede Stufe enthält die davor:
  teil1    HalfAlpha, BakeShadowMask, ShadowDest geschrieben (C7sch3a)
  teil2    ShadowMaskFor, BeginCastShadows, EndCastShadows, DrawCastShadow (C7sch3b)
  einbau1  Draw, Bildpuffer, Gebäude (C7sch3c)
  einbau2  Haufen, Feld, Baustelle, Baum, Busch (C7sch3d)
  fertig   Tier, Figur; keine Ovale mehr (C7sch3e)
Gegen den Rumpf rot, weil die Rümpfe leer sind.
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, methode, melde  # noqa: E402

SCR = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Screens")
HAUPT, TEIL, ANIM = SCR / "RTSGameplayScreen.cs", SCR / "RTSGameplayScreen.Schatten.cs", SCR / "RTSGameplayScreen.Animationen.cs"
SPIEL = Path("AgeOfEvolutions/AgeOfEvolutions.Core/AgeOfEvolutionsGame.cs")
STUFEN = ["teil1", "teil2", "einbau1", "einbau2", "fertig"]
stufe = sys.argv[1] if len(sys.argv) > 1 else "fertig"
assert stufe in STUFEN, f"Stufe {stufe} unbekannt - {STUFEN}"
bis = STUFEN.index(stufe)


def dicht(text: str) -> str:
    """Ohne Leerraum - so stört weder Einrückung noch Zeilenumbruch den Vergleich."""
    return re.sub(r"\s+", "", text)


fehler = []
haupt, teil, anim = lies(HAUPT), lies(TEIL), lies(ANIM)


def rumpf(name: str, text: str) -> str | None:
    r = methode(text, name)
    if r is None:
        fehler.append(f"{name} nicht gefunden")
        return None
    if "NotImplementedException" in r:
        fehler.append(f"{name} ist noch leer")
        return None
    return dicht(r)


def braucht(name: str, text: str, bausteine: list[str]) -> str | None:
    d = rumpf(name, text)
    if d is not None:
        for b in bausteine:
            if dicht(b) not in d:
                fehler.append(f"{name}: es fehlt {b}")
    return d


def vor(name: str, d: str | None, erst: str, dann: str, warum: str):
    """erst muss in d vor dann stehen (beides dicht)."""
    if d is None:
        return
    i, j = d.find(dicht(erst)), d.find(dicht(dann))
    if i < 0 or j < 0 or i > j:
        fehler.append(f"{name}: {erst} muss vor {dann} stehen - {warum}")


# --- teil1: reine Rechnungen ---------------------------------------------------------
braucht("HalfAlpha", teil, ["(width + 1) / 2", "(height + 1) / 2", "hiddenTop", "width - 1 -"])
braucht("BakeShadowMask", teil, ["MathF.Ceiling(height * SHADOW_SKEW)", "height * SHADOW_FLATTEN + width * SHADOW_DEPTH / 2f", ">= 128",
                                 "SHADOW_DEPTH", "SHADOW_SOFT", "MathF.Sqrt"])
braucht("ShadowDest", teil, ["target.Width * maskWidth * 2 / texWidth", "target.Height * maskHeight * 2 / texHeight"])
braucht("SwayShadowBands", teil, ["texHeight", "strips", "Hoehenteil", "SHADOW_FLATTEN", "SHADOW_DEPTH", "SHADOW_SOFT",
                                  "1f - (oben + hoehe / 2f) / texHeight"])

# --- teil2: Grafikrümpfe ---------------------------------------------------------------
if bis >= 1:
    braucht("ShadowMaskFor", teil, ["_shadowMasks", "GetData", "HalfAlpha(", "BakeShadowMask(", "out int sh", "SetData",
                                    "new Texture2D(graphicsDevice", "SwayBands"])
    braucht("BeginCastShadows", teil, ["new SpriteBatch(graphicsDevice)", "SpriteSortMode.Deferred, ShadowUnion",
                                       "_castShadowsOpen = true"])
    ende = braucht("EndCastShadows", teil, ["_castShadowsOpen = false", "BackBufferWidth", "BackBufferHeight",
                                            "GetRenderTargets()", "SetRenderTarget(_shadowTarget)",
                                            "Clear(Color.Transparent)", "SetRenderTargets(previous)", "SHADOW_TINT"])
    vor("EndCastShadows", ende, "SetRenderTarget(_shadowTarget)", "_castShadowBatch.End()",
        "die Masken gehören ins Render-Target, nicht auf den Bildschirm")
    vor("EndCastShadows", ende, "_castShadowBatch.End()", "SetRenderTargets(previous)",
        "erst die Masken ins Render-Target, dann zurück")
    braucht("DrawCastShadow", teil, ["ShadowMaskFor(", "ShadowDest(", "SpriteEffects.FlipHorizontally",
                                     "Color.White * density", "SHADOW_TINT * density"])
    braucht("DrawSwayCastShadow", teil, ["ShadowMaskFor(", "SwayBands", "MathF.Pow(hoehenteil, 1.5f)",
                                         "target.Height * 2f / tex.Height", "Color.White * density",
                                         "SHADOW_TINT * density", "_castShadowsOpen",
                                         # MonoGame: erst Ziel, dann Quelle - vertauscht lag jedes Band
                                         # als Rechteck am linken Bildrand (errors/Schattenprobleme*.png)
                                         "info.Tex, ziel, quelle"])

# --- einbau1: Draw, Bildpuffer, Gebäude -------------------------------------------------
if bis >= 2:
    draw = methode(haupt, "Draw")
    if draw is None:
        fehler.append("Draw nicht gefunden")
    else:
        d = dicht(draw)
        if "BeginCastShadows();" not in d or "EndCastShadows();" not in d:
            fehler.append("Draw: BeginCastShadows() und EndCastShadows() fehlen")
        else:
            vor("Draw", d, "BeginCastShadows();spriteBatch.Begin();", "DrawTileMap(spriteBatch);",
                "der Schattenbatch öffnet vor dem Batch der Objekte und vor den Haufen")
            vor("Draw", d, "DrawUnits(spriteBatch);EndCastShadows();", "DrawArrows(spriteBatch);",
                "die Schatten kommen gleich nach den stehenden Objekten auf den Bildschirm")
    gebaeude = braucht("DrawBuilding", haupt, ["DrawCastShadow(spriteBatch, sprite, area, SpriteEffects.None, true)"])
    vor("DrawBuilding", gebaeude, "DrawCastShadow(", "_flagCloths.TryGetValue", "der Schatten vor dem Bild")
    if gebaeude is not None and "DrawGroundShadow" in gebaeude:
        fehler.append("DrawBuilding: das alte Oval (DrawGroundShadow) steht noch drin")
    spiel = dicht(lies(SPIEL))
    if "PreparingDeviceSettings" not in spiel or "RenderTargetUsage.PreserveContents" not in spiel:
        fehler.append("AgeOfEvolutionsGame: der Bildpuffer muss seinen Inhalt behalten (PreparingDeviceSettings, RenderTargetUsage.PreserveContents)")

# --- einbau2: Haufen, Feld, Baustelle, Baum, Busch ---------------------------------------
if bis >= 3:
    haufen = braucht("DrawPile", haupt, ["DrawCastShadow(spriteBatch, tex, target, SpriteEffects.None, true)"])
    vor("DrawPile", haufen, "DrawCastShadow(", "spriteBatch.Draw(tex, target, Color.White)", "der Schatten vor dem Bild")
    if haufen is not None and "DrawGroundShadow" in haufen:
        fehler.append("DrawPile: das alte Oval (DrawGroundShadow) steht noch drin")
    feld = rumpf("DrawField", haupt)
    if feld is not None and ("DrawGroundShadow" in feld or "FieldShadowStand" in feld):
        fehler.append("DrawField: ein Feld ist flach und wirft keinen Schatten mehr - DrawGroundShadow/FieldShadowStand raus")
    bau = braucht("DrawConstructionStages", anim, ["DrawCastShadow(spriteBatch, sprite, area, SpriteEffects.None, true, source.Y)"])
    vor("DrawConstructionStages", bau, "DrawCastShadow(", "spriteBatch.Draw(sprite, new Rectangle(area.X, area.Bottom - shown",
        "der Schatten vor dem Bild")
    baum = braucht("DrawTree", haupt, ["DrawSwayCastShadow(spriteBatch, tex, target, SpriteEffects.None, 0, TREE_SHADOW_DENSITY, sway)"])
    vor("DrawTree", baum, "Wind.TreeSway(", "DrawSwayCastShadow(", "sway muss vor dem Schatten da sein")
    vor("DrawTree", baum, "DrawSwayCastShadow(", "Wind.SwayStrips(", "der Schatten vor dem Baum")
    busch = braucht("DrawBerryBush", haupt, ["DrawCastShadow(spriteBatch, tex, target, effekt, false, 0, TREE_SHADOW_DENSITY)"])
    vor("DrawBerryBush", busch, "DrawCastShadow(", "spriteBatch.Draw(tex, target, null", "der Schatten vor dem Busch")

# --- fertig: Tier, Figur; keine Ovale mehr --------------------------------------------------
if bis >= 4:
    tier = braucht("DrawAnimal", haupt, ["DrawCastShadow(spriteBatch, tex, new Rectangle(target.X, (int)(foot.Y - height), width, height)",
                                         "tier.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, false)"])
    vor("DrawAnimal", tier, "DrawCastShadow(", "spriteBatch.Draw(tex, target, null", "der Schatten vor dem Tier")
    figur = braucht("DrawVillager", haupt, ["DrawCastShadow(spriteBatch, figure,",
                                            "motion.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, false)"])
    vor("DrawVillager", figur, "DrawCastShadow(", "spriteBatch.Draw(figure,", "der Schatten vor der Figur")
    for name, text in (("RTSGameplayScreen.cs", haupt), ("RTSGameplayScreen.Schatten.cs", teil)):
        for alt in ("_shadowTex", "BuildShadowTexture", "GroundShadowRect", "FieldShadowStand", "DrawGroundShadow"):
            if alt in text:
                fehler.append(f"{name}: {alt} gibt es noch - die Ovale fallen ganz weg")

melde(fehler, f"C7 Schlagschatten, Stufe {stufe}: Rümpfe nach Vertrag" + (", Schatten direkt vor jedem Bild" if bis >= 2 else ""))
