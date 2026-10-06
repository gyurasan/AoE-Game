"""Trägt die Symbole der neuen Bautasten in die Gruppe icons von
tools/bilder/bilder.json ein. Mehrfach aufrufbar: ersetzt die eigenen Einträge."""
import json
from pathlib import Path

DATEI = Path("D:/Apps/AgeOfEmpire/tools/bilder/bilder.json")
katalog = json.loads(DATEI.read_text(encoding="utf-8"))

NEU = [
    ("stadtzentrum", "A large medieval town center hall of stone and timber with a red tiled roof, small corner towers and a blue banner above the gate."),
    ("kaserne", "A round wooden shield with an iron rim, a crossed sword and spear behind it."),
    ("schiessstand", "A wooden longbow with an arrow in front of a round straw archery target with red rings."),
    ("stall", "A brown horse head with a leather bridle above a small heap of golden hay."),
    ("schmiede", "An iron anvil with a blacksmith hammer lying on it and a few orange sparks."),
    ("markt", "A brass balance scale with gold coins in one pan and a small sack of grain in the other."),
    ("palisade", "A short section of a palisade wall of sharpened vertical wooden logs tied with rope."),
    ("steinmauer", "A short section of a grey stone castle wall with battlements on top."),
    ("belagerungswerkstatt", "A wooden medieval catapult on wheels with a round stone in its bucket."),
    ("universitaet", "An open old book with a feather quill pen and a small ink pot."),
    ("kloster", "A small medieval stone chapel with a bell tower and arched windows."),
    ("burg", "A mighty grey stone castle with four round towers, battlements and a small blue flag."),
    ("wunder", "A tall shining gothic cathedral of pale stone with two golden spires."),
]
icons = katalog["icons"]
namen = {n for n, _ in NEU}
icons["bilder"] = [b for b in icons["bilder"] if b["name"] not in namen]
for i, (name, prompt) in enumerate(NEU):
    # gewählte Variante: das Stadtzentrum mit Seed + 1 (Holzbau mit blauem Banner)
    seed = 2010 + i + (1 if name == "stadtzentrum" else 0)
    icons["bilder"].append({"name": name, "seed": seed, "ziel": f"Icons/{name}.png", "prompt": prompt})

with open(DATEI, "w", encoding="utf-8") as f:   # Textmodus: CRLF wie im Arbeitsbaum
    f.write(json.dumps(katalog, ensure_ascii=False, indent=1) + "\n")
print([(b["name"], b["seed"]) for b in icons["bilder"]])
