"""Erzeugt Spielgrafiken mit Qwen-Image über die HTTP-Schnittstelle von ComfyUI.

ComfyUI (portable, NVIDIA) liegt unter D:/Apps/ComfyUI und muss laufen:

    D:/Apps/ComfyUI/ComfyUI_windows_portable/run_nvidia_gpu.bat

Aufruf:

    py tools/bilder/qwen_image.py menu                 # alle Bilder der Gruppe 'menu'
    py tools/bilder/qwen_image.py icons haus muehle    # nur diese aus der Gruppe 'icons'
    py tools/bilder/qwen_image.py menu --seeds 3       # drei Varianten je Bild

Prompts, Größe und Seed stehen in bilder.json neben diesem Skript - so lässt
sich jedes Bild später genau so wieder erzeugen. Ergebnisse landen unter
tools/bilder/ausgabe/<gruppe>/<name>_<seed>.png und werden NICHT automatisch
ins Spiel übernommen; welches Bild ins Content-Verzeichnis kommt, entscheidet
der Nutzer.

Vor dem Lauf das Ollama-Sprachmodell entladen (ollama stop qwen3.8:27b) - es
belegt sonst rund 27 GB Grafikspeicher.
"""

import argparse
import json
import random
import sys
import time
import urllib.parse
import urllib.request
from pathlib import Path

sys.stdout.reconfigure(encoding="utf-8", errors="replace")

SERVER = "http://127.0.0.1:8188"
HIER = Path(__file__).resolve().parent
AUSGABE = HIER / "ausgabe"

MODELL = "qwen_image_2512_fp8_e4m3fn.safetensors"
TEXT_ENCODER = "qwen_2.5_vl_7b_fp8_scaled.safetensors"
VAE = "qwen_image_vae.safetensors"


FREISTELLER = "birefnet.safetensors"   # models/background_removal, Comfy-Org/BiRefNet


def workflow(prompt: str, negativ: str, breite: int, hoehe: int, seed: int,
             schritte: int, cfg: float, praefix: str) -> dict:
    """Der Qwen-Image-Graph der ComfyUI-Vorlage im API-Format."""
    return {
        "1": {"class_type": "UNETLoader", "inputs": {"unet_name": MODELL, "weight_dtype": "default"}},
        "2": {"class_type": "CLIPLoader",
              "inputs": {"clip_name": TEXT_ENCODER, "type": "qwen_image", "device": "default"}},
        "3": {"class_type": "VAELoader", "inputs": {"vae_name": VAE}},
        "4": {"class_type": "ModelSamplingAuraFlow", "inputs": {"model": ["1", 0], "shift": 3.1}},
        "5": {"class_type": "CLIPTextEncode", "inputs": {"text": prompt, "clip": ["2", 0]}},
        "6": {"class_type": "CLIPTextEncode", "inputs": {"text": negativ, "clip": ["2", 0]}},
        "7": {"class_type": "EmptySD3LatentImage", "inputs": {"width": breite, "height": hoehe, "batch_size": 1}},
        "8": {"class_type": "KSampler",
              "inputs": {"model": ["4", 0], "seed": seed, "steps": schritte, "cfg": cfg,
                         "sampler_name": "euler", "scheduler": "simple",
                         "positive": ["5", 0], "negative": ["6", 0], "latent_image": ["7", 0],
                         "denoise": 1.0}},
        "9": {"class_type": "VAEDecode", "inputs": {"samples": ["8", 0], "vae": ["3", 0]}},
        "10": {"class_type": "SaveImage", "inputs": {"images": ["9", 0], "filename_prefix": praefix}},
    }


def freistell_graph(eingabe: str, praefix: str) -> dict:
    """BiRefNet stellt ein hochgeladenes Bild frei, das PNG bekommt einen Alphakanal.

    JoinImageWithAlpha rechnet alpha = 1 - Maske, RemoveBackground liefert aber
    die Vordergrundmaske - deshalb dazwischen InvertMask, sonst würde gerade das
    Motiv durchsichtig.
    """
    return {
        "1": {"class_type": "LoadImage", "inputs": {"image": eingabe}},
        "2": {"class_type": "LoadBackgroundRemovalModel", "inputs": {"bg_removal_name": FREISTELLER}},
        "3": {"class_type": "RemoveBackground", "inputs": {"bg_removal_model": ["2", 0], "image": ["1", 0]}},
        "4": {"class_type": "InvertMask", "inputs": {"mask": ["3", 0]}},
        "5": {"class_type": "JoinImageWithAlpha", "inputs": {"image": ["1", 0], "alpha": ["4", 0]}},
        "6": {"class_type": "SaveImage", "inputs": {"images": ["5", 0], "filename_prefix": praefix}},
    }


def anfrage(pfad: str, daten: dict | None = None):
    req = urllib.request.Request(SERVER + pfad, method="POST" if daten is not None else "GET")
    if daten is not None:
        req.add_header("Content-Type", "application/json")
        req.data = json.dumps(daten).encode("utf-8")
    with urllib.request.urlopen(req, timeout=60) as antwort:
        return antwort.read()


def hochladen(datei: Path) -> str:
    """Lädt ein Bild in den input-Ordner von ComfyUI (POST /upload/image)."""
    grenze = "----aoe" + str(random.randrange(10**12))
    kopf = (f"--{grenze}\r\nContent-Disposition: form-data; name=\"overwrite\"\r\n\r\ntrue\r\n"
            f"--{grenze}\r\nContent-Disposition: form-data; name=\"image\"; filename=\"{datei.name}\"\r\n"
            f"Content-Type: image/png\r\n\r\n").encode("utf-8")
    rumpf = kopf + datei.read_bytes() + f"\r\n--{grenze}--\r\n".encode("utf-8")
    req = urllib.request.Request(SERVER + "/upload/image", data=rumpf, method="POST",
                                 headers={"Content-Type": f"multipart/form-data; boundary={grenze}"})
    with urllib.request.urlopen(req, timeout=60) as antwort:
        return json.loads(antwort.read())["name"]


def ausfuehren(graph: dict, ziel: Path) -> float:
    """Schickt einen Graphen an ComfyUI, wartet auf das Bild und speichert es."""
    antwort = json.loads(anfrage("/prompt", {"prompt": graph}))
    if antwort.get("node_errors"):
        raise RuntimeError(f"ComfyUI lehnt den Graphen ab: {antwort['node_errors']}")
    kennung = antwort["prompt_id"]

    beginn = time.time()
    while True:
        verlauf = json.loads(anfrage(f"/history/{kennung}"))
        if kennung in verlauf:
            eintrag_verlauf = verlauf[kennung]
            status = eintrag_verlauf.get("status", {})
            if status.get("status_str") == "error":
                raise RuntimeError(f"Fehler in ComfyUI: {status.get('messages')}")
            bilder = [b for knoten in eintrag_verlauf["outputs"].values() for b in knoten.get("images", [])]
            if bilder:
                break
        if time.time() - beginn > 900:
            raise TimeoutError("kein Bild nach 15 Minuten")
        time.sleep(1.0)

    bild = bilder[0]
    abfrage = urllib.parse.urlencode({"filename": bild["filename"], "subfolder": bild["subfolder"],
                                      "type": bild["type"]})
    ziel.parent.mkdir(parents=True, exist_ok=True)
    ziel.write_bytes(anfrage(f"/view?{abfrage}"))
    return time.time() - beginn


def erzeuge(eintrag: dict, gruppe: dict, seed: int, ziel: Path) -> Path:
    stil = gruppe.get("stil", "")
    prompt = f"{eintrag['prompt']} {stil}".strip()
    breite, hoehe = eintrag.get("groesse", gruppe["groesse"])
    # Ein Eintrag kann eigene Negativbegriffe mitbringen, zusätzlich zu denen der Gruppe
    negativ = ", ".join(n for n in (gruppe.get("negativ", ""), eintrag.get("negativ", "")) if n)
    graph = workflow(prompt, negativ, breite, hoehe, seed,
                     gruppe.get("schritte", 30), gruppe.get("cfg", 3.0), f"aoe_{eintrag['name']}")
    dauer = ausfuehren(graph, ziel)
    print(f"  {ziel.relative_to(HIER)}  ({dauer:.0f} s, Seed {seed})")
    return ziel


def freistellen(roh: Path, ziel: Path) -> Path:
    dauer = ausfuehren(freistell_graph(hochladen(roh), f"aoe_frei_{ziel.stem}"), ziel)
    print(f"  {ziel.relative_to(HIER)}  freigestellt ({dauer:.0f} s)")
    return ziel


def main() -> None:
    teile = argparse.ArgumentParser(description=__doc__.split("\n")[0])
    teile.add_argument("gruppe")
    teile.add_argument("namen", nargs="*", help="nur diese Einträge der Gruppe")
    teile.add_argument("--seeds", type=int, default=1, help="Varianten je Bild (Seed, Seed+1, ...)")
    teile.add_argument("--nur-freistellen", action="store_true",
                       help="vorhandene Rohbilder nur freistellen, nicht neu erzeugen")
    args = teile.parse_args()

    katalog = json.loads((HIER / "bilder.json").read_text(encoding="utf-8"))
    gruppe = katalog[args.gruppe]
    eintraege = [e for e in gruppe["bilder"] if not args.namen or e["name"] in args.namen]
    if not eintraege:
        sys.exit(f"keine Einträge {args.namen} in Gruppe {args.gruppe}")

    # Gruppen mit "freistellen": das Rohbild heißt <name>_<seed>_roh.png, das
    # freigestellte <name>_<seed>.png. Freigestellt wird in einem zweiten
    # Durchgang, nachdem Qwen-Image den Grafikspeicher geräumt hat: liegen beide
    # Modelle zugleich darin, bricht BiRefNet den Server mit "Fatal Python error:
    # Aborted" ab (ComfyUI 0.38, 2026-10-03).
    frei = gruppe.get("freistellen", False)
    auftraege = [(e, e.get("seed", random.randrange(2**31)) + k) for e in eintraege for k in range(args.seeds)]
    ordner = AUSGABE / args.gruppe
    print(f"{len(eintraege)} Bild(er) x {args.seeds} Seed(s), Gruppe {args.gruppe}")
    if not args.nur_freistellen:
        for e, seed in auftraege:
            erzeuge(e, gruppe, seed, ordner / f"{e['name']}_{seed}{'_roh' if frei else ''}.png")
    if frei:
        anfrage("/free", {"unload_models": True, "free_memory": True})
        for e, seed in auftraege:
            freistellen(ordner / f"{e['name']}_{seed}_roh.png", ordner / f"{e['name']}_{seed}.png")


if __name__ == "__main__":
    main()
