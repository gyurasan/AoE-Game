"""Trägt die Gruppen gebaeude2_<zeitalter> (neue Gebäude je Zeitalter) in
tools/bilder/bilder.json ein. Mehrfach aufrufbar: ersetzt die eigenen Gruppen."""
import json
from pathlib import Path

DATEI = Path("D:/Apps/AgeOfEmpire/tools/bilder/bilder.json")
katalog = json.loads(DATEI.read_text(encoding="utf-8"))

basis = katalog["gebaeude_ritter"]
STIL = ("Game sprite for a top-down 2D strategy game, three-quarter top-down view from the south: "
        "the front facade faces the viewer and the roof is seen from above. Detailed 16-bit pixel art, "
        "crisp pixels, soft light from the upper left. Realistic proportions, believable building "
        "materials and natural colors, finely detailed pixel shading. One single building centered, "
        "fully visible, isolated on a plain white background, no ground plate, no shadow on the "
        "background, no text. Clearly visible square pixels, hard pixel edges, like a classic late "
        "1990s strategy game sprite.")
NEGATIV = basis["negativ"] + ", horses, animals, blue roof, blue walls"
FAHNE = "a small blue flag on a tall pole beside the building, the flag flying to the right"

ZEITALTER = {"dunkel": 5060, "feudal": 5160, "ritter": 5260, "imperial": 5360}
# name: (ab Zeitalter, Zielbreite, {zeitalter: prompt})
GEBAEUDE = {
    "kaserne": ("dunkel", 384, {
        "dunkel": f"A medieval barracks: a long low hall of wooden log posts and wattle and daub walls, a thatched roof, a wide wooden door, a rack of wooden spears and round shields in front and {FAHNE}.",
        "feudal": f"A medieval barracks: a long hall of wooden planks and timber beams on a fieldstone foundation, a wooden shingle roof, a wide wooden gate, a rack of spears and shields and a straw training dummy in front and {FAHNE}.",
        "ritter": f"A medieval barracks: a long two-storey building of grey stone masonry with a timber-framed upper floor, a red tiled roof, a heavy arched gate, a rack of spears, swords and shields in front and {FAHNE}.",
        "imperial": f"A grand late medieval barracks of pale cream stone with arched windows and a heavy arched gate, a dark slate roof with golden ornaments, a rack of halberds and steel shields in front and {FAHNE} on a golden pole.",
    }),
    "schiessstand": ("feudal", 384, {
        "feudal": f"A medieval archery range: an open wooden shed of planks and timber beams with a wooden shingle roof, a rack of longbows and quivers, three round straw archery targets with red rings on wooden stands beside it and {FAHNE}.",
        "ritter": f"A medieval archery range: a building of grey stone masonry with an open timber gallery and a red tiled roof, a rack of longbows and crossbows, three round straw archery targets with red rings on wooden stands beside it and {FAHNE}.",
        "imperial": f"A grand late medieval archery range of pale cream stone with an open arcade of arches and a dark slate roof with golden ornaments, racks of longbows and crossbows, three round straw archery targets with red rings beside it and {FAHNE} on a golden pole.",
    }),
    "stall": ("feudal", 384, {
        "feudal": f"A medieval horse stable: a long wooden barn of planks and timber beams on a fieldstone foundation, a wooden shingle roof, a row of half-open wooden stable doors, hay bales and a wooden water trough in front and {FAHNE}.",
        "ritter": f"A medieval horse stable: a long building of grey stone masonry with a timber-framed hay loft, a red tiled roof, a row of arched wooden stable doors, hay bales and a stone water trough in front and {FAHNE}.",
        "imperial": f"A grand late medieval royal stable of pale cream stone with a row of arched stable doors, a dark slate roof with a small cupola and golden ornaments, hay bales and a stone water trough in front and {FAHNE} on a golden pole.",
    }),
    "schmiede": ("feudal", 384, {
        "feudal": f"A medieval blacksmith workshop: a wooden building of planks on a fieldstone foundation, a wooden shingle roof, a big fieldstone chimney, an open front with a glowing orange forge, an iron anvil on a tree stump in front and {FAHNE}.",
        "ritter": f"A medieval blacksmith forge of grey stone masonry with a red tiled roof, a big stone chimney, an open arched front with a glowing orange forge, an iron anvil, hammers and tongs in front and {FAHNE}.",
        "imperial": f"A grand late medieval armory forge of pale cream stone with a dark slate roof, two tall stone chimneys, an open arched front with a glowing orange forge, an iron anvil and a rack of armor in front and {FAHNE} on a golden pole.",
    }),
    "markt": ("feudal", 512, {
        "feudal": f"A medieval market: a wooden market hall of timber beams on a fieldstone foundation with a wooden shingle roof and an open ground floor, market stalls with red and yellow striped cloth awnings, crates, barrels and sacks of grain and {FAHNE}.",
        "ritter": f"A medieval market hall: a timber-framed upper floor resting on stone arches, a red tiled roof, an open arcade below, market stalls with red and yellow striped cloth awnings, crates, barrels and sacks and {FAHNE}.",
        "imperial": f"A grand late medieval market hall of pale cream stone with an open arcade of arches, a dark slate roof with a small clock tower and golden ornaments, market stalls with red and yellow striped cloth awnings, crates and barrels and {FAHNE} on a golden pole.",
    }),
    "palisade": ("dunkel", 128, {
        "dunkel": "A short straight section of a palisade wall: a row of rough vertical wooden logs with sharpened tips, tied together with rope, seen from the front.",
        "feudal": "A square block of a palisade wall: vertical wooden logs with sharpened tips standing close together on all four sides, held together by a horizontal wooden beam, a plank walkway on top. No door, no roof.",
        "ritter": "A square block of a palisade wall: thick vertical wooden logs with sharpened tips standing close together on all four sides, held together by iron bands, on a low fieldstone base, a plank walkway on top. No door, no roof.",
        "imperial": "A square block of a palisade wall: neatly cut thick vertical wooden logs with sharpened tips standing close together on all four sides, held together by iron bands, on a low stone base, a plank walkway on top. No door, no roof.",
    }),
    "steinmauer": ("feudal", 128, {
        "feudal": "A solid square block of a fieldstone wall, as wide as it is deep, with a flat stone walkway on top. No door, no window, no roof.",
        "ritter": "A solid square block of a thick grey stone castle wall, as wide as it is deep, with battlements around the top edge. No door, no window, no roof.",
        "imperial": "A solid square block of a massive fortified wall of pale cream stone blocks, as wide as it is deep, with battlements around the top edge. No door, no window, no roof.",
    }),
    "belagerungswerkstatt": ("ritter", 512, {
        "ritter": f"A medieval siege workshop: a large open timber workshop on a grey stone foundation with a red tiled roof and a wide open gate, a half-built wooden catapult, big wooden wheels and stacked beams in front and {FAHNE}.",
        "imperial": f"A grand late medieval siege engine workshop of pale cream stone with a huge arched open gate and a dark slate roof, a wooden trebuchet frame, big wooden wheels and stacked beams in front and {FAHNE} on a golden pole.",
    }),
    "universitaet": ("ritter", 512, {
        "ritter": f"A medieval university: a large college building of grey stone masonry with tall arched windows, a red tiled roof, a small bell tower and a grand arched entrance with stone steps and {FAHNE}.",
        "imperial": f"A magnificent late medieval university of pale cream stone and white marble with tall arched windows, a central domed tower with a golden finial, a dark slate roof and a grand portal with stone steps and {FAHNE} on a golden pole.",
    }),
    "kloster": ("ritter", 384, {
        "ritter": f"A medieval monastery: a stone church of grey masonry with a red tiled roof, a square bell tower, arched windows, a round rose window above the wooden door and {FAHNE}.",
        "imperial": f"A grand late medieval abbey church of pale cream stone with a tall pointed bell tower, a dark slate roof with golden ornaments, tall gothic windows, a rose window above the portal and {FAHNE} on a golden pole.",
    }),
    "burg": ("ritter", 512, {
        "ritter": "A mighty medieval castle of grey stone masonry: thick curtain walls with battlements, four big round corner towers, a tall square central keep, a massive gatehouse with a portcullis and a big blue flag on a pole on top of the keep, the flag flying to the right.",
        "imperial": "A mighty late medieval fortress of pale cream stone: thick curtain walls with battlements, four big round corner towers with dark slate conical roofs, a tall central keep with golden ornaments, a massive gatehouse with a portcullis and a big blue flag on a golden pole on top of the keep, the flag flying to the right.",
    }),
    "wunder": ("imperial", 640, {
        "imperial": "A monumental gothic cathedral as a wonder of the world: pale cream stone and white marble, two tall twin spires with golden tops, a huge round rose window, flying buttresses, a grand portal and a big blue flag on a golden pole on top, the flag flying to the right.",
    }),
}
REIHENFOLGE = list(ZEITALTER)
# Gewählte Variante je Bild: Versatz zum Grund-Seed (--seeds 2 erzeugt +0 und +1)
WAHL = {("feudal", "kaserne"): 1, ("feudal", "schmiede"): 1,
        ("imperial", "kaserne"): 1, ("imperial", "schmiede"): 1, ("imperial", "markt"): 1,
        ("imperial", "belagerungswerkstatt"): 1, ("imperial", "universitaet"): 1, ("imperial", "burg"): 1,
        ("feudal", "palisade"): 1, ("ritter", "schmiede"): 2, ("imperial", "stall"): 2}
# Steinmauer ohne Tür: Seed des Ausbesserns und Maskenrechtecke [x, y, b, h] im Rohbild (1024 px),
# treppenförmig der schrägen Unterkante der linken Wand folgend
OHNE_TUER = {("feudal", "steinmauer"): (5180, [[215, 550, 85, 270], [300, 560, 80, 290]]),
             ("ritter", "steinmauer"): (5280, [[250, 630, 75, 190], [325, 630, 80, 225]])}
# Die Feudal-Mauer bekam mit dem Gebäude-Prompt wieder eine Tür in die Maske gemalt -
# dort beschreibt der Prompt nur das Mauerwerk
PROMPT_ZU = {"feudal": "Rough fieldstone masonry: rows of tan stone blocks with dark mortar lines, "
                       "one continuous flat wall surface without any opening."}
STIL_ZU = {"feudal": "Detailed 16-bit pixel art texture, crisp square pixels, hard pixel edges, "
                     "soft light from the upper left, natural colors."}
# Eigene Negativbegriffe je Bild: die Steinmauer bekam sonst trotz "No door" eine Tür
NEGATIV_BILD = {("feudal", "steinmauer"): "door, doorway, entrance, gate, arch, opening, window",
                ("ritter", "steinmauer"): "door, doorway, entrance, gate, arch, opening, window"}

for zeitalter, seed in ZEITALTER.items():
    gruppe = {k: v for k, v in basis.items() if k != "bilder"}
    gruppe["stil"] = STIL
    gruppe["negativ"] = NEGATIV
    bilder = []
    for i, (name, (ab, breite, prompts)) in enumerate(GEBAEUDE.items()):
        if REIHENFOLGE.index(zeitalter) < REIHENFOLGE.index(ab):
            continue
        bilder.append({"name": name, "seed": seed + 3 * i + WAHL.get((zeitalter, name), 0), "prompt": prompts[zeitalter],
                       "ziel": f"Gebaeude/{zeitalter}/{name}.png", "zielbreite": breite})
        if (zeitalter, name) in NEGATIV_BILD:
            bilder[-1]["negativ"] = NEGATIV_BILD[(zeitalter, name)]
        if (zeitalter, name) in OHNE_TUER:
            # Tür per Ausbessern entfernen: die Maske deckt Tür und Rahmen, der Rest bleibt
            basis_bild = bilder[-1]
            ziel = {k: basis_bild.pop(k) for k in ("ziel", "zielbreite")}
            seed_zu, rechtecke = OHNE_TUER[(zeitalter, name)]
            bilder.append({"name": f"{name}_zu", "seed": seed_zu,
                           "vorlage": f"{name}_{basis_bild['seed']}_roh.png",
                           "maske": {"rechtecke": rechtecke},
                           "prompt": PROMPT_ZU.get(zeitalter, basis_bild["prompt"] + " The wall face is one continuous surface of stone blocks."),
                           "negativ": NEGATIV_BILD[(zeitalter, name)], **ziel})
        if (zeitalter, name) in OHNE_TUER and zeitalter in STIL_ZU:
            bilder[-1]["stil"] = STIL_ZU[zeitalter]
    gruppe["bilder"] = bilder
    katalog[f"gebaeude2_{zeitalter}"] = gruppe

with open(DATEI, "w", encoding="utf-8") as f:   # Textmodus: CRLF wie im Arbeitsbaum
    f.write(json.dumps(katalog, ensure_ascii=False, indent=1) + "\n")
for k in katalog:
    if k.startswith("gebaeude2_"):
        print(k, [(b["name"], b["seed"]) for b in katalog[k]["bilder"]])
