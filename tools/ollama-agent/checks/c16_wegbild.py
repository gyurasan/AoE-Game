"""Abnahme C16 (W1a-W1c): die Wege liegen als Bild des erreichten Zeitalters im Bodenshader.

Der Nutzer (2026-10-16): „in der Dunkelzeit wären es wohl Trampelpfad danach Pflasterstraße
(Mehrschichtig: Stein, Zement), danach Mittelalter (Erde, Schlamm, Kies) am ende Verdichteter
Schotter“. Die drei Bilder kommen aus Qwen-Image (Gruppe wege in tools/bilder/bilder.json):
Boden/weg_feudal, Boden/weg_ritter, Boden/weg_imperial. Der Bodenshader legt das Bild der
erreichten Weg-Stufe dorthin, wo der Trampelpfad zur nackten Erde würde - mit Rauschen
ausgefranst statt als Kachelquadrat. Wie es aussieht, zeigen Fotos; hier die Struktur.

  py c16_wegbild.py shader    W1a: Boden.fx
  py c16_wegbild.py screen    W1b: RTSGameplayScreen übergibt Bild, Stufe und Maßstab
  py c16_wegbild.py bilder    W1c: Ladenamen und prozedurale Ersatzbilder
"""
import re
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).resolve().parent))
from _cs import lies, melde, methode  # noqa: E402

CONTENT = Path("AgeOfEvolutions/AgeOfEvolutions.Core/Content")
teil = sys.argv[1] if len(sys.argv) > 1 else "shader"
fehler = []

# Seit C16b (2026-10-16) malt nicht mehr der Bodenshader die Wege, sondern Effects/Wege.fx in
# einem eigenen Durchgang - die Teile shader und screen prüfen deshalb den neuen Aufbau
if teil in ("shader", "screen"):
    import subprocess
    ziel = "shader" if teil == "shader" else "zeichnen"
    raise SystemExit(subprocess.call([sys.executable, str(Path(__file__).resolve().parent / "c16b_wege.py"), ziel]))


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


if teil == "shader":
    roh = (CONTENT / "Effects/Boden.fx").read_text(encoding="utf-8-sig")
    fx = ohne_kommentare(roh)
    if "RUMPF" in roh:
        fehler.append("Boden.fx: ein Rumpf (RUMPF) ist noch nicht ersetzt")
    for dekl in (r"float\s+RoadScale\s*;", r"float\s+RoadOn\s*;", r"Texture2D\s+RoadTexture\s*;",
                 r"sampler2D\s+RoadSampler\s*="):
        if not re.search(dekl, fx):
            fehler.append(f"Boden.fx: Deklaration {dekl} fehlt")
    flaeche = rumpf(fx, "RoadSurface")
    if flaeche is None:
        fehler.append("Boden.fx: RoadSurface fehlt")
    else:
        for teilstueck in ("RoadSampler", "RoadScale", "nm.g", "Luma(", "0.78"):
            if teilstueck not in flaeche:
                fehler.append(f"RoadSurface verwendet {teilstueck} nicht")
    deckung = rumpf(fx, "SoilCover")
    if deckung is None:
        fehler.append("Boden.fx: SoilCover fehlt")
    else:
        if "smoothstep" not in deckung or "RoadOn" not in deckung or "wear" not in deckung:
            fehler.append("SoilCover: smoothstep über wear, abhängig von RoadOn, fehlt")
        for zahl in ("0.5", "0.56", "0.88", "0.72"):
            if not re.search(rf"(?<![\d.]){re.escape(zahl)}(?![\d])", deckung):
                fehler.append(f"SoilCover: Grenze {zahl} aus dem VERTRAG fehlt")
    haupt = rumpf(fx, "MainPS")
    if haupt is None:
        fehler.append("Boden.fx: MainPS fehlt")
    else:
        if not re.search(r"soil\s*=\s*lerp\(\s*soil\s*,\s*RoadSurface\(\s*tile\s*,\s*nm\s*\)\s*,\s*RoadOn\s*\)", haupt):
            fehler.append("MainPS: soil = lerp(soil, RoadSurface(tile, nm), RoadOn) fehlt")
        if not re.search(r"land\s*=\s*lerp\(\s*land\s*,\s*soil\s*,\s*SoilCover\(\s*wear\s*\)\s*\)", haupt):
            fehler.append("MainPS: land = lerp(land, soil, SoilCover(wear)) fehlt")
        if re.search(r"smoothstep\(\s*0\.5\s*,\s*0\.88\s*,\s*wear\s*\)", haupt):
            fehler.append("MainPS: die alte Grenze smoothstep(0.5, 0.88, wear) steht noch da")
        # Reihenfolge: erst die Erde zum Weg machen, dann über die Wiese legen
        a, b = haupt.find("RoadSurface("), haupt.find("SoilCover(")
        if a >= 0 and b >= 0 and a > b:
            fehler.append("MainPS: RoadSurface muss vor SoilCover stehen (erst soil, dann land)")
    melde(fehler, "OK: Boden.fx legt das Wegbild mit schärferem Rand auf die Wege")

elif teil == "screen":
    cs = lies()
    wahl = methode(cs, "RoadTextureFor")
    if wahl is None or "NotImplementedException" in wahl:
        fehler.append("RoadTextureFor ist nicht umgesetzt")
    else:
        for teilstueck in ("EnsureRoadTextures()", "_road1Tex", "_road2Tex", "_road3Tex", "null"):
            if teilstueck not in wahl:
                fehler.append(f"RoadTextureFor: {teilstueck} fehlt")
    if not re.search(r"const\s+int\s+ROAD_TEXELS\s*=\s*\d+\s*;", cs):
        fehler.append("const int ROAD_TEXELS fehlt")
    zeichnen = methode(cs, "DrawGroundShaded") or ""
    for name in ("RoadTexture", "RoadOn", "RoadScale"):
        if not re.search(rf"\[\s*\"{name}\"\s*\]\s*\??\.SetValue\(", zeichnen):
            fehler.append(f"DrawGroundShaded übergibt {name} nicht")
    if "ROAD_TEXELS" not in zeichnen:
        fehler.append("DrawGroundShaded: RoadScale ohne ROAD_TEXELS")
    # Unter OpenGL hing mit dem Sandbild als Ersatz dieselbe Textur an zwei Samplern des
    # Bodenshaders, und der Nebel danach deckte die ganze Spielfläche schwarz (2026-10-16, W1d)
    if re.search(r"\[\s*\"RoadTexture\"\s*\]\s*\??\.SetValue\([^;]*_sandTex", zeichnen):
        fehler.append("DrawGroundShaded: RoadTexture bekommt ersatzweise das Sandbild - unter OpenGL wird "
                      "die Spielfläche dann schwarz")
    if not re.search(r"RoadTextureFor\(\s*Math\.Max\(\s*1\s*,\s*tileMap\.CurrentRoadLevel\s*\)\s*\)", zeichnen):
        fehler.append("DrawGroundShaded: RoadTexture ist nicht immer ein echtes Wegbild "
                      "(RoadTextureFor(Math.Max(1, tileMap.CurrentRoadLevel)))")
    # Seit G14 rechnet UpdateGroundMemory das Lebendbild, RefreshGroundLive lädt es nur hoch
    lebend = (methode(cs, "UpdateGroundMemory") or "") + (methode(cs, "RefreshGroundLive") or "")
    if not re.search(r"RoadLevel\s*(>=\s*1|>\s*0)", lebend):
        fehler.append("Lebendbild: Wegkacheln (RoadLevel >= 1) stehen nicht ganz ausgetreten darin")
    boden = methode(cs, "DrawGround") or ""
    if not re.search(r"if\s*\(\s*GroundShaded\s*&&\s*!tile\.Farm\s*\)\s*continue\s*;", boden):
        fehler.append("DrawGround: mit Bodenshader zeichnet es Wege noch einmal als Kachelquadrate "
                      "(erwartet: if (GroundShaded && !tile.Farm) continue;)")
    melde(fehler, "OK: der Bildschirm übergibt Wegbild, Stufe und Maßstab an den Bodenshader")

elif teil == "bilder":
    cs = lies()
    for bild in ("Boden/weg_feudal", "Boden/weg_ritter", "Boden/weg_imperial"):
        if not re.search(rf"LoadOptional\(\s*\"{re.escape(bild)}\"\s*\)", cs):
            fehler.append(f"RTSGameplayScreen lädt {bild} nicht")
    for alt in ("wege_feld", "wege_befestigt", "wege_pflaster"):
        if alt in cs:
            fehler.append(f"der alte Bildname {alt} steht noch im Bildschirm")
    bau = methode(cs, "BuildRoadTexture") or ""
    if not re.search(r"stufe\s*==\s*1\b", bau):
        fehler.append("BuildRoadTexture: Stufe 1 (Pflasterstraße) hat keinen eigenen Zweig")
    if re.search(r"stufe\s*==\s*3\s*\)\s*\{[^}]*Mörtel", bau):
        fehler.append("BuildRoadTexture: das Pflaster hängt noch an Stufe 3")
    if re.search(r"\bkante\b", bau) or re.search(r"b\.Set\(\s*i\s*,\s*0\s*,", bau):
        fehler.append("BuildRoadTexture: der Rahmen um das Bild ist noch da")
    melde(fehler, "OK: neue Bildnamen, Ersatzbilder in der Reihenfolge der Zeitalter, ohne Rahmen")
else:
    raise SystemExit(f"unbekannter Teil {teil}")
