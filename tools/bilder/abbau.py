"""Vorlage für eine Abbaustufe: den vollen Stein- oder Goldhaufen auf eine kleinere Hügelform abtragen.

Ein Haufen im Spiel zeigt, wie viel noch in ihm steckt (Rohstoffe/stein_75, _50, _25 ...). Jede
Stufe entsteht aus dem vollen Haufen, damit die unteren Steine dieselben bleiben und der Haufen im
Spiel an derselben Stelle kleiner wird, statt gegen einen anderen getauscht zu werden.

Ein Eintrag in bilder.json bekommt dazu "abbau": {"von": Rohbild des vollen Haufens im
Ausgabeordner der Gruppe, "ellipse": [cx, cy, rx, ry]}. Dieses Skript schreibt daraus seine
"vorlage": alles außerhalb der Ellipse wird weiß (der Hintergrund der Rohbilder), nur was in ihr
liegt, bleibt stehen. cy ist die Unterkante des Haufens, die obere Ellipsenhälfte also der neue
Umriss - ry seine Höhe, rx die halbe Breite am Fuß. Danach malt qwen_image.py für den Eintrag den
Schnittrand neu ("maske" mit "boegen" entlang derselben Ellipse), damit die angeschnittenen Steine
oben wieder rund abschließen.

Braucht numpy und Pillow - die Python von ComfyUI hat beides:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/python_embeded/python.exe tools/bilder/abbau.py rohstoffe stein_75 stein_50
"""
import json
import sys
from pathlib import Path

import numpy as np
from PIL import Image

# Die eingebettete Python von ComfyUI nimmt den Ordner des Skripts nicht in den Suchpfad auf
sys.path.insert(0, str(Path(__file__).resolve().parent))
import qwen_image  # noqa: E402


def abtragen(quelle: np.ndarray, cx: float, cy: float, rx: float, ry: float) -> np.ndarray:
    """Das Bild mit Weiß überall außerhalb der Ellipse (cx, cy, rx, ry)."""
    hoehe, breite = quelle.shape[:2]
    y, x = np.mgrid[0:hoehe, 0:breite]
    drin = ((x - cx) / rx) ** 2 + ((y - cy) / ry) ** 2 <= 1.0
    bild = quelle.copy()
    bild[~drin] = 255
    return bild


def main():
    gruppe, namen = sys.argv[1], sys.argv[2:]
    katalog = json.loads((qwen_image.HIER / "bilder.json").read_text(encoding="utf-8"))
    ordner = qwen_image.AUSGABE / gruppe
    for e in katalog[gruppe]["bilder"]:
        if (namen and e["name"] not in namen) or "abbau" not in e:
            continue
        quelle = np.array(Image.open(ordner / e["abbau"]["von"]).convert("RGB"))
        Image.fromarray(abtragen(quelle, *e["abbau"]["ellipse"])).save(ordner / e["vorlage"])
        print(f"  {gruppe}/{e['vorlage']} aus {e['abbau']['von']}")


if __name__ == "__main__":
    main()
