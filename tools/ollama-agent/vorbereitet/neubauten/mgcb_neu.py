"""Trägt die neuen Bilder in AgeOfEvolutions.mgcb ein: Gebäude nach dem letzten
Gebaeude-Eintrag, Symbole nach dem letzten Icons-Eintrag. Mehrfach aufrufbar -
schon eingetragene Dateien werden übersprungen.

    py mgcb_neu.py [wurzel]   (Standard D:/Apps/AgeOfEmpire)
"""
import re
import sys
from pathlib import Path

WURZEL = Path(sys.argv[1] if len(sys.argv) > 1 else "D:/Apps/AgeOfEmpire")
CONTENT = WURZEL / "AgeOfEvolutions/AgeOfEvolutions.Core/Content"
MGCB = CONTENT / "AgeOfEvolutions.mgcb"

GEBAEUDE = ["kaserne", "palisade", "schiessstand", "stall", "schmiede", "markt", "steinmauer",
            "belagerungswerkstatt", "universitaet", "kloster", "burg", "wunder"]
ICONS = ["stadtzentrum"] + GEBAEUDE


def block(rel):
    return (f"#begin {rel}\n/importer:TextureImporter\n/processor:TextureProcessor\n"
            "/processorParam:ColorKeyColor=255,0,255,255\n/processorParam:ColorKeyEnabled=False\n"
            "/processorParam:GenerateMipmaps=True\n/processorParam:PremultiplyAlpha=True\n"
            "/processorParam:ResizeToPowerOfTwo=False\n/processorParam:MakeSquare=False\n"
            f"/processorParam:TextureFormat=Color\n/build:{rel}\n\n")


roh = MGCB.read_bytes().decode("utf-8")
bom = roh.startswith("\ufeff")
crlf = "\r\n" in roh
text = roh.lstrip("\ufeff").replace("\r\n", "\n")


def einfuegen(text, praefix, dateien):
    neu = [d for d in dateien if (CONTENT / d).is_file() and f"/build:{d}\n" not in text]
    fehlt = [d for d in dateien if not (CONTENT / d).is_file()]
    letzte = list(re.finditer(rf"^#begin {re.escape(praefix)}.*?\n\n", text, re.M | re.S))[-1]
    text = text[:letzte.end()] + "".join(block(d) for d in neu) + text[letzte.end():]
    return text, neu, fehlt


gebaeude = [f"Gebaeude/{z}/{n}_{f}.png" for z in ("dunkel", "feudal", "ritter", "imperial")
            for n in GEBAEUDE for f in ("blau", "rot")]
text, neu_g, _ = einfuegen(text, "Gebaeude/", gebaeude)
text, neu_i, fehlt_i = einfuegen(text, "Icons/", [f"Icons/{n}.png" for n in ICONS])
if crlf:
    text = text.replace("\n", "\r\n")
MGCB.write_bytes((("\ufeff" if bom else "") + text).encode("utf-8"))
print(f"{len(neu_g)} Gebäudebilder und {len(neu_i)} Symbole eingetragen; Symbole ohne Datei: {fehlt_i}")
