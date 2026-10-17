"""Abnahme C16b: Wege als Linien in eigenem Shader statt Flecken im Bodenshader.

Der Nutzer (2026-10-16): „in der Feudalzeit sehen die "Feldstrassen" nicht wirklich wie strassen
aus. Hier muss noch sehr nachbessert werden und auch die anderen epochen prüfen bitte“ und „es muss
schon zum rest des games passen und nach "strasse" oder Pfad aussehen“. Neu: Effects/Wege.fx zieht
Linien fester Breite zwischen den Kantenmitten benachbarter Wegkacheln (Treppen werden Diagonalen,
Ecken Kurven), je Zeitalter Pflaster mit Randsteinen, Erdweg mit Fahrspuren, gewölbter Schotter;
ein Weg entsteht erst ab sechs Tritten (ROAD_WEAR_MIN). Das Verhalten prüfen tools/spielablauf
(strassen, wegkarte, nebelboden) und Fotos; hier die Struktur.

  py c16b_wege.py karte      W3a: TileMap - Weg erst ab ROAD_WEAR_MIN
  py c16b_wege.py daten      W3b: RoadDrawnAt, RoadDrawData, UpdateGroundMemory
  py c16b_wege.py zeichnen   W3c: DrawRoads, ReleaseTextureUnits, Laden und Reihenfolge
  py c16b_wege.py shader     W3d: Effects/Wege.fx
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CORE = Path("AgeOfEvolutions/AgeOfEvolutions.Core")
teil = sys.argv[1] if len(sys.argv) > 1 else "zeichnen"
fehler = []


def ohne_kommentare(text):
    return re.sub(r"//[^\n]*", "", text)


def rumpf(text, name):
    for treffer in re.finditer(rf"\b{name}\s*\([^)]*\)\s*(?::\s*\w+\s*)?\{{", text):
        start, tiefe = treffer.end() - 1, 0
        for j in range(start, len(text)):
            tiefe += {"{": 1, "}": -1}.get(text[j], 0)
            if tiefe == 0:
                return text[start:j + 1]
    return None


def umgesetzt(cs, name):
    r = methode(cs, name)
    if r is None or "NotImplementedException" in r:
        fehler.append(f"{name} ist nicht umgesetzt")
        return ""
    return r


if teil == "karte":
    tm = lies(CORE / "Data/TileMap.cs")
    if not re.search(r"const\s+float\s+ROAD_WEAR_MIN\s*=\s*0\.3f", tm):
        fehler.append("TileMap.ROAD_WEAR_MIN = 0.3f fehlt")
    tritt = methode(tm, "Trample") or ""
    if "ROAD_WEAR_MIN" not in tritt:
        fehler.append("Trample macht die Kachel nicht erst ab ROAD_WEAR_MIN zum Weg")
    aufstieg = methode(tm, "UpgradeRoads") or ""
    if "ROAD_WEAR_MIN" not in aufstieg:
        fehler.append("UpgradeRoads hebt nicht nur Trampelpfade ab ROAD_WEAR_MIN an")
    if re.search(r"Wear\s*>\s*0f?\s*\|\|", aufstieg):
        fehler.append("UpgradeRoads nimmt noch jede einmal betretene Kachel (Wear > 0)")
    melde(fehler, "OK: ein Weg entsteht erst ab sechs Tritten")

elif teil == "daten":
    cs = lies()
    gezeichnet = umgesetzt(cs, "RoadDrawnAt")
    for teilstueck in ("RoadLevel", "Grassland", "Sand", "Building", "Farm", "IsWild"):
        if gezeichnet and teilstueck not in gezeichnet:
            fehler.append(f"RoadDrawnAt: {teilstueck} fehlt")
    karte = umgesetzt(cs, "RoadDrawData")
    if karte:
        if "Array.Copy" not in karte:
            fehler.append("RoadDrawData kopiert _roadMapData nicht (Array.Copy)")
        if re.search(r"new\s*\[\]|new\s+int\s*\[|new\s+List<", karte):
            fehler.append("RoadDrawData legt je Kachel Arrays oder Listen an (läuft alle 0,25 s)")
    gedaechtnis = methode(cs, "UpdateGroundMemory") or ""
    if "_roadMapData" not in gedaechtnis or "RoadDrawnAt" not in gedaechtnis:
        fehler.append("UpdateGroundMemory führt die Wegkarte (_roadMapData, RoadDrawnAt) nicht")
    if not re.search(r"0\.35f", gedaechtnis):
        fehler.append("UpdateGroundMemory begrenzt rot unter Wegen nicht auf 0.35f")
    if re.search(r"RoadLevel\s*>=\s*1\s*\?\s*1f\s*:\s*tile\.Wear", gedaechtnis):
        fehler.append("UpdateGroundMemory setzt Wegkacheln noch ganz ausgetreten (rot 1)")
    melde(fehler, "OK: Wegkarte mit Gedächtnis, schräge Lücken gefüllt, unter Wegen nur niedergetretenes Gras")

elif teil == "zeichnen":
    cs = lies()
    frei = umgesetzt(cs, "ReleaseTextureUnits")
    if frei and not re.search(r"Textures\s*\[\s*\w+\s*\]\s*=\s*null", frei):
        fehler.append("ReleaseTextureUnits setzt graphicsDevice.Textures[i] nicht auf null")
    wege = umgesetzt(cs, "DrawRoads")
    for name in ("MapTiles", "RoadTexture", "NoiseTexture", "RoadScale", "HalfWidth", "Curb", "Ruts",
                 "Crown", "Fray", "Tint", "VergeColor"):
        if wege and not re.search(rf"\[\s*\"{name}\"\s*\]\s*\??\.SetValue\(", wege):
            fehler.append(f"DrawRoads übergibt {name} nicht")
    if wege and ("ReleaseTextureUnits()" not in wege or "_roadEffect" not in wege):
        fehler.append("DrawRoads zeichnet nicht mit _roadEffect oder gibt die Texture Units nicht frei")
    boden = methode(cs, "DrawGroundShaded") or ""
    if re.search(r"\"Road(Texture|On|Scale)\"", boden):
        fehler.append("DrawGroundShaded übergibt noch Wegparameter an den Bodenshader")
    if "ReleaseTextureUnits()" not in boden:
        fehler.append("DrawGroundShaded gibt die Texture Units danach nicht frei")
    if not re.search(r"DrawGroundShaded\(\s*spriteBatch\s*\)\s*;\s*(//[^\n]*\s*)*DrawRoads\(\s*spriteBatch\s*\)\s*;", cs):
        fehler.append("Draw ruft DrawRoads nicht direkt nach DrawGroundShaded")
    if not re.search(r"Load<Effect>\(\s*\"Effects/Wege\"\s*\)", cs):
        fehler.append("LoadContent lädt Effects/Wege nicht")
    if not re.search(r"_roadMap\s*=\s*new\s+Texture2D\(", cs):
        fehler.append("LoadContent legt _roadMap nicht an")
    auffrischen = methode(cs, "RefreshGroundLive") or ""
    if not re.search(r"_roadMap\s*\??\.SetData\(\s*RoadDrawData\(\)\s*\)", auffrischen):
        fehler.append("RefreshGroundLive lädt die Wegkarte nicht hoch (_roadMap?.SetData(RoadDrawData()))")
    mgcb = (CORE / "Content/AgeOfEvolutions.mgcb").read_text(encoding="utf-8-sig")
    if "#begin Effects/Wege.fx" not in mgcb:
        fehler.append("Effects/Wege.fx steht nicht in AgeOfEvolutions.mgcb")
    fx = (CORE / "Content/Effects/Boden.fx").read_text(encoding="utf-8-sig")
    if re.search(r"RoadSampler|RoadOn", fx):
        fehler.append("Boden.fx malt noch Wege (RoadSampler/RoadOn) - das macht jetzt Wege.fx")
    melde(fehler, "OK: Wege als eigener Durchgang, Texture Units danach frei")

elif teil == "shader":
    roh = (CORE / "Content/Effects/Wege.fx").read_text(encoding="utf-8-sig")
    fx = ohne_kommentare(roh)
    if "RUMPF" in roh:
        fehler.append("Wege.fx: ein Rumpf (RUMPF) ist noch nicht ersetzt")
    seg = rumpf(fx, "Seg") or ""
    for teilstueck in ("saturate", "dot", "length", "1e-5"):
        if teilstueck not in seg:
            fehler.append(f"Seg: {teilstueck} fehlt")
    netz = rumpf(fx, "CellNet") or ""
    if netz.count("Seg(") < 7:
        fehler.append(f"CellNet prüft {netz.count('Seg(')} statt 7 Strecken (Sackgasse, 2 gerade, 4 Kurven)")
    if re.search(r"\bif\b|\bfor\b", netz):
        fehler.append("CellNet soll ohne if und Schleife auskommen (ps_3_0 unter OpenGL)")
    haupt = rumpf(fx, "MainPS") or ""
    if haupt.count("CellNet(") != 4:
        fehler.append("MainPS ruft CellNet nicht für die vier Kacheln um die Ecke auf")
    for teilstueck in ("MapAt(", "RoadSampler", "NoiseSampler", "fwidth", "Tint", "Crown", "Ruts", "Curb",
                       "VergeColor", "floor(tile + 0.5)"):
        if teilstueck not in haupt:
            fehler.append(f"MainPS: {teilstueck} fehlt")
    if not re.search(r"float4\(\s*col\s*\*\s*a\s*,\s*a\s*\)", haupt):
        fehler.append("MainPS gibt nicht vormultipliziert float4(col * a, a) zurück")
    melde(fehler, "OK: Wege.fx zieht das Wegenetz mit Breite, Kante und Aussehen je Zeitalter")
else:
    raise SystemExit(f"unbekannter Teil {teil}")
