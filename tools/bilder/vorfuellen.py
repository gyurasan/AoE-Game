"""Vorlage für ein Inpainting vorfüllen: die Maske mit den Farben ringsum zuschmieren.

Qwen-Image malt in eine Maske, was es dort erwartet - beim Ritter ohne Schwertarm acht Mal von
acht wieder einen Unterarm vor die Brust, ganz gleich, was Prompt und Negativliste sagen. Mit
vorgefüllter Vorlage und "staerke" unter 1 bleibt der Aufbau der Vorlage stehen (kein Arm), und
das Modell malt nur Stoff und Rüstung über die verschmierte Fläche.

Ein Eintrag in bilder.json bekommt dazu "vorfuellen": die Datei, aus der gefüllt wird (im
Ausgabeordner der Gruppe). Dieses Skript schreibt daraus seine "vorlage": jedes Pixel unter der
Maske ("maske" des Eintrags, wie qwen_image.py sie zeichnet) nimmt die Farbe des nächsten Pixels
außerhalb, danach wird die Fläche weichgezeichnet (Gauß, Halbmesser 6). Erst danach läuft
qwen_image.py für den Eintrag.

Braucht numpy, scipy und Pillow - die Python von ComfyUI hat alles:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/vorfuellen.py soldaten kavalier_ohne_arm_v
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image
from scipy import ndimage

# Die eingebettete Python von ComfyUI nimmt den Ordner des Skripts nicht in den Suchpfad auf
sys.path.insert(0, str(Path(__file__).resolve().parent))
import qwen_image  # noqa: E402


def main():
    gruppe, namen = sys.argv[1], sys.argv[2:]
    katalog = json.loads((qwen_image.HIER / "bilder.json").read_text(encoding="utf-8"))
    ordner = qwen_image.AUSGABE / gruppe
    for e in katalog[gruppe]["bilder"]:
        if e["name"] not in namen or "vorfuellen" not in e:
            continue
        quelle = np.array(Image.open(ordner / e["vorfuellen"]).convert("RGB")).astype(float)
        hoehe, breite = quelle.shape[:2]
        datei = qwen_image.maske_zeichnen(e["maske"], breite, hoehe, ordner / f"{e['name']}_maske.png")
        maske = np.array(Image.open(datei)) > 127
        _, (iy, ix) = ndimage.distance_transform_edt(maske, return_indices=True)
        voll = quelle.copy()
        voll[maske] = quelle[iy[maske], ix[maske]]
        weich = np.stack([ndimage.gaussian_filter(voll[..., k], 6) for k in range(3)], -1)
        voll[maske] = weich[maske]
        Image.fromarray(voll.clip(0, 255).astype(np.uint8)).save(ordner / e["vorlage"])
        print(f"  {gruppe}/{e['vorlage']} aus {e['vorfuellen']}")


if __name__ == "__main__":
    main()
