# Bilder lokal erzeugen – am Beispiel Gras

Alle Spielgrafiken (Boden, Bäume, Gebäude, Figuren, Symbole …) kommen aus
**Qwen-Image**, das lokal in **ComfyUI** auf der eigenen Grafikkarte läuft. Es
gibt keinen Cloud-Dienst und keinen Schlüssel – nur drei Werkzeuge in
`tools/bilder/`:

```
bilder.json          Prompt, Seed, Größe und Ziel je Bild (das "Rezept")
      |
      |  py tools/bilder/qwen_image.py <gruppe>        -> ComfyUI malt
      v
tools/bilder/ausgabe/<gruppe>/<name>_<seed>.png       (Rohbilder, nicht im Git)
      |
      |  Seed des besten Bilds in bilder.json eintragen
      |  pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe <gruppe>
      v
AgeOfEvolutions/AgeOfEvolutions.Core/Content/<Ziel>.png   (kachelbar, zugeschnitten …)
      |
      |  dotnet build
      v
im Spiel
```

Wie ein Bild vom Content-Ordner ins Spiel kommt (mgcb, `LoadOptional`), steht in
[Bilder-einbinden.md](Bilder-einbinden.md).

## Was du brauchst

| Was | Wo / Wert |
|---|---|
| ComfyUI (portable, NVIDIA) | `D:\Apps\ComfyUI\ComfyUI_windows_portable` |
| Bildmodell | `ComfyUI\models\diffusion_models\qwen_image_2512_fp8_e4m3fn.safetensors` |
| Text-Encoder | `ComfyUI\models\text_encoders\qwen_2.5_vl_7b_fp8_scaled.safetensors` |
| VAE | `ComfyUI\models\vae\qwen_image_vae.safetensors` |
| Freisteller (für Sprites) | `ComfyUI\models\background_removal\birefnet.safetensors` |
| Python für `qwen_image.py` | `py` (ab 3.10), nur Standardbibliothek – kein Pillow nötig |
| PowerShell 7 für `uebernehmen.ps1` | `pwsh` |
| Python mit numpy/Pillow (nur `naht.py`, Kachelprobe) | `D:\Apps\ComfyUI\ComfyUI_windows_portable\python_embeded\python.exe` |

Die Dateinamen der Modelle stehen fest in `qwen_image.py` (`MODELL`,
`TEXT_ENCODER`, `VAE`, `FREISTELLER`). Wer ComfyUI auf einem anderen Rechner
einrichtet, legt die Dateien unter genau diesen Namen in die genannten Ordner
(die Qwen-Image-Vorlage in ComfyUI nennt die Downloads von Comfy-Org).

**Grafikspeicher:** Die RTX 5090 hat 32 GB. Qwen-Image und das lokale
Ollama-Sprachmodell (`qwen3.8:27b`, rund 27 GB) passen **nicht gleichzeitig**
hinein – immer nur eins von beiden.

## Kurzfassung: Grundgras neu erzeugen

```powershell
ollama stop qwen3.8:27b                                   # Grafikspeicher frei
Start-Process -WorkingDirectory D:\Apps\ComfyUI\ComfyUI_windows_portable `
    D:\Apps\ComfyUI\ComfyUI_windows_portable\run_nvidia_gpu.bat   # ComfyUI starten

py tools/bilder/qwen_image.py gras gras --seeds 2         # zwei Varianten malen
# Bilder in tools/bilder/ausgabe/gras/ ansehen, besten Seed in bilder.json eintragen
pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe gras      # nach Content/ übernehmen
dotnet build                                              # .xnb bauen, Spiel starten
```

Die einzelnen Schritte im Detail:

## Schritt für Schritt

### 1. Grafikspeicher freimachen

```powershell
ollama ps                    # zeigt, ob ein Modell geladen ist
ollama stop qwen3.8:27b
```

Ohne das bricht ComfyUI mit „CUDA out of memory“ ab oder wird sehr langsam.

### 2. ComfyUI starten

Entweder `run_nvidia_gpu.bat` im Explorer doppelklicken oder:

```powershell
Start-Process -WorkingDirectory D:\Apps\ComfyUI\ComfyUI_windows_portable `
    D:\Apps\ComfyUI\ComfyUI_windows_portable\run_nvidia_gpu.bat
```

Wichtig: Die Batchdatei ruft `.\python_embeded\python.exe` relativ auf – aus dem
Spielordner heraus direkt gestartet findet sie Python nicht. Daher das
`-WorkingDirectory`.

Das Konsolenfenster offen lassen. Bereit ist der Server, wenn dort
`To see the GUI go to: http://127.0.0.1:8188` steht. Prüfen geht auch so:

```powershell
Invoke-RestMethod http://127.0.0.1:8188/system_stats
```

Die Weboberfläche brauchst du nicht – `qwen_image.py` schickt den fertigen
Graphen über die HTTP-Schnittstelle.

### 3. Bilder erzeugen

Aufruf: `py tools/bilder/qwen_image.py <gruppe> [namen …] [--seeds N]`

```powershell
py tools/bilder/qwen_image.py gras                        # alle sechs Grasflächen
py tools/bilder/qwen_image.py gras gras_blumen            # nur die Blumenwiese
py tools/bilder/qwen_image.py gras gras gras_hoch --seeds 3   # je drei Varianten
```

`--seeds 3` nimmt den Seed aus `bilder.json` und die zwei folgenden, bei
`"seed": 5601` also 5601, 5602 und 5603. Ergebnisse landen in
`tools/bilder/ausgabe/gras/gras_5601.png`, `gras_5602.png` …

Die Grasgruppe malt 1328 × 1328 px mit 50 Schritten – das dauert pro Bild
deutlich länger als ein 1024er-Bild mit 30 Schritten (das liegt bei rund einer
halben Minute). Erst 1–2 Testbilder machen und ansehen, dann viele Seeds.

Gleicher Prompt + gleicher Seed + gleiche Einstellungen ergeben (mit denselben
Modellen und derselben ComfyUI-Version) wieder dasselbe Bild. Deshalb ist
`bilder.json` das eigentliche Archiv; `ausgabe/` steht in `.gitignore` und kann
jederzeit neu erzeugt werden.

### 4. Ansehen und auswählen

Die PNGs in `tools/bilder/ausgabe/gras/` im Bildbetrachter öffnen. Bei
Bodenbildern auf Folgendes achten:

- **Gleichmäßig über die ganze Fläche?** Ein heller oder dunkler Fleck, ein Weg
  oder ein Wirbel in der Mitte fällt gekachelt als Raster auf.
- **Keine Objekte:** Büsche, Steine, Wege, Pfützen.
- **Passt die Farbe zu den anderen Sorten?** Das Spiel mischt sechs Grasbilder
  (`Meadow` in `Content/Effects/Boden.fx`) – eine Sorte, die zu hell oder zu
  gelb ist, sticht als Flecken heraus.

Kachelprobe (vier Kopien nebeneinander, nach Schritt 6 auf das übernommene,
kachelbar gemachte Bild angewendet):

```powershell
& D:\Apps\ComfyUI\ComfyUI_windows_portable\python_embeded\python.exe -I -c "import sys; from PIL import Image; b = Image.open(sys.argv[1]); w, h = b.size; k = Image.new('RGB', (2 * w, 2 * h)); [k.paste(b, (x * w, y * h)) for x in (0, 1) for y in (0, 1)]; k.save(sys.argv[2])" AgeOfEvolutions\AgeOfEvolutions.Core\Content\Boden\gras.png $env:TEMP\gras_2x2.png
start $env:TEMP\gras_2x2.png
```

### 5. Seed eintragen

In `tools/bilder/bilder.json`, Gruppe `gras`, beim Eintrag den `seed` auf die
gewählte Variante setzen:

```json
{"name": "gras", "seed": 5602, "ziel": "Boden/gras.png", "prompt": "Natural unmown meadow ..."}
```

`uebernehmen.ps1` nimmt genau die Datei `<name>_<seed>.png`. Neue Varianten
probierst du, indem du einen neuen Seed-Bereich einträgst (bisherige Konvention:
je Runde ein neuer Hunderter, je Sorte ein Zehner – `gras` 5601,
`gras_dunkel` 5612, `gras_blumen` 5642 …), mit `--seeds 2` erzeugst und danach
den besten eintragst.

### 6. Ins Spiel übernehmen

```powershell
pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe gras
```

Das Skript arbeitet **jeden Eintrag der Gruppe mit `ziel`** ab, nicht nur den
geänderten. Für jeden muss `ausgabe/gras/<name>_<seed>.png` existieren, sonst
bricht es mit `fehlt: … - erst qwen_image.py gras laufen lassen` ab. Auf einem
frischen Klon also zuerst `py tools/bilder/qwen_image.py gras` laufen lassen.

Bei der Grasgruppe passiert dabei:

1. `ausgleichen` 0.06 – großflächige Helligkeit ausgleichen (Halbmesser 6 % der
   Bildbreite), feine Halme bleiben.
2. `kachelbar` – um eine halbe Breite verschieben, damit die Außenkanten
   zusammenpassen, und die Naht in der Mitte weich überdecken.
3. Speichern nach `Content/Boden/gras.png` usw.

Ohne Angabe von `-Gruppe` übernimmt das Skript **alle** Gruppen – das
überschreibt auch Bilder, an denen du gerade nicht arbeitest.

### 7. Bauen und ansehen

```powershell
dotnet build
```

Die sechs Grasbilder sind schon in `AgeOfEvolutions.mgcb` eingetragen und
werden im Code geladen (`RTSGameplayScreen.cs`, `LoadOptional("Boden/gras")` …).
Beim Ersetzen eines vorhandenen Bilds ist also **nichts am Code** zu tun.
Danach das Spiel starten und das Gras im Spiel beurteilen – im Spiel wirkt es
anders als im Bildbetrachter (verkleinert, gemischt, gekachelt).

### 8. Aufräumen

Bevor wieder das Ollama-Modell laufen soll, ComfyUI schließen (Konsolenfenster
zu) oder nur den Grafikspeicher freigeben:

```powershell
Invoke-RestMethod -Method Post http://127.0.0.1:8188/free -ContentType application/json `
    -Body '{"unload_models": true, "free_memory": true}'
```

## Aufbau von `bilder.json`

Die Datei ist ein Objekt aus **Gruppen**. Eine Gruppe legt Stil und
Nachbearbeitung fest, ihre `bilder`-Liste die einzelnen Motive. Die Grasgruppe
(gekürzt):

```json
"gras": {
  "groesse": [1328, 1328],
  "schritte": 50,
  "cfg": 3.0,
  "stil": "Seamless ground texture for a top-down 2D strategy game, seen from above, finely detailed hand-painted pixel art, natural colors, ...",
  "negativ": "text, letters, logo, watermark, frame, border, perspective, horizon, sky, trees, rocks, path, ...",
  "kachelbar": true,
  "ausgleichen": 0.06,
  "bilder": [
    {"name": "gras", "seed": 5601, "ziel": "Boden/gras.png",
     "prompt": "Natural unmown meadow seen from above: grass of uneven length ..."},
    {"name": "gras_hoch", "seed": 5682, "ziel": "Boden/gras_hoch.png",
     "prompt": "Dense long wild grass seen from directly above: ...",
     "negativ": "side view, stems from below, wheat, ... swirl, vortex, spiral, radial, center, circle"}
  ]
}
```

An Qwen-Image geht: **`prompt` des Eintrags + Leerzeichen + `stil` der Gruppe**,
als Negativ **`negativ` der Gruppe + `negativ` des Eintrags**.

### Felder für die Erzeugung (`qwen_image.py`)

| Feld | Ebene | Bedeutung |
|---|---|---|
| `name` | Eintrag | Dateiname in `ausgabe/<gruppe>/` |
| `seed` | Eintrag | Startwert; zugleich der gewählte Seed für das Übernehmen |
| `prompt` | Eintrag | Motiv, Englisch |
| `stil` | Gruppe oder Eintrag | wird an den Prompt gehängt; im Eintrag ersetzt er den Gruppenstil |
| `negativ` | beide | was nicht ins Bild soll; Eintrag ergänzt die Gruppe |
| `negativ_allein` | Eintrag | `true`: nur das eigene Negativ, ohne das der Gruppe |
| `groesse` | beide | `[Breite, Höhe]` in Pixeln |
| `schritte`, `cfg` | Gruppe | Sampler-Schritte (sonst 30) und Prompt-Treue (sonst 3.0) |
| `freistellen` | Gruppe | `true`: zweiter Durchgang mit BiRefNet, Hintergrund wird durchsichtig |
| `vorlage`, `maske`, `staerke` | Eintrag | nur einen Teil eines vorhandenen Bilds neu malen (s. u.) |

### Felder für das Übernehmen (`uebernehmen.ps1`)

| Feld | Bedeutung |
|---|---|
| `ziel` | Pfad unter `Content/`; Einträge **ohne** `ziel` bleiben reine Versuche und werden nie übernommen |
| `kachelbar` | `true` für Bodenbilder; `"naht"` für Fugenmuster (Pflaster) über `naht.py` |
| `ausgleichen` | großflächige Helligkeit ausgleichen (Anteil der Bildbreite) |
| `ausschnitt` | `[x, y, b, h]` zuerst auf dieses Rechteck beschneiden |
| `zuschneiden` | auf den sichtbaren Bereich (Alpha) beschneiden – für Sprites |
| `zielbreite` / `zielgroesse` | verkleinern auf Breite bzw. genaue Größe |
| `loecher` | weiße Lücken (z. B. zwischen Halmen, Ästen) durchsichtig machen |
| `spielerfarben`, `blau_saettigung` | `_blau.png` und umgefärbte `_rot.png` erzeugen |
| `figur` | alle Bilder einer Figur auf dasselbe Rechteck beschneiden |

Die vollständige Beschreibung steht jeweils im Kopfkommentar der beiden Skripte.

## Eine eigene Grassorte

**Vorhandene Sorte ersetzen** (z. B. eine schönere Blumenwiese): nur `prompt`
und/oder `seed` des Eintrags ändern, Schritte 3–7 – kein Code.

**Größe nicht ändern:** Alle Grasbilder bleiben 1328 × 1328 px. Der Bodenshader
legt alle Sorten im Maßstab von `Boden/gras` aus (`GrassScale` aus
`GRASS_TEXELS` und der Breite von `gras.png`); eine Sorte in anderer Größe wäre
im Spiel gröber oder feiner als die übrigen.

**Ganz neue, siebte Sorte:** Ein neuer Eintrag mit eigenem `name`, `seed` und
`ziel` (z. B. `"Boden/gras_moos.png"`) ist schnell gemacht – im Spiel erscheint
das Bild aber erst, wenn

1. ein Block in `AgeOfEvolutions.mgcb` steht (Nachbar `Boden/gras.png` kopieren,
   beide Pfade ändern, inklusive der Zeile `/build:Boden/gras_moos.png`),
2. `RTSGameplayScreen.cs` es per `LoadOptional` lädt und dem Shader übergibt und
3. `Meadow` in `Boden.fx` es in die Mischung aufnimmt.

Das ist Programmierarbeit, kein Bildtausch.

## Grasbüschel (Sprites mit Freistellen)

Die hohen Büschel um die Füße der Figuren sind eigene Sprites, Gruppe
`bueschel` (Ziele `Gras/bueschel_*.png`). Sie werden auf **weißem Hintergrund**
gemalt und danach freigestellt:

```powershell
py tools/bilder/qwen_image.py bueschel bueschel_hoch --seeds 2
```

Ablauf im Skript: erst alle Rohbilder (`bueschel_hoch_8101_roh.png`), dann wird
Qwen-Image aus dem Speicher geworfen und BiRefNet stellt jedes frei
(`bueschel_hoch_8101.png` mit Alphakanal). Beides zugleich im Grafikspeicher
bringt ComfyUI zum Absturz. Ist das Freistellen trotzdem abgebrochen, nur
diesen Teil wiederholen:

```powershell
py tools/bilder/qwen_image.py bueschel bueschel_hoch --seeds 2 --nur-freistellen
```

Beim Übernehmen schneidet `uebernehmen.ps1` auf den sichtbaren Bereich zu,
macht weiße Lücken zwischen den Halmen durchsichtig und verkleinert auf 256 px.

## Nur einen Teil neu malen (Inpainting)

Ein Eintrag mit `vorlage` und `maske` malt nur die Maskenfläche eines
vorhandenen Bilds aus `ausgabe/<gruppe>/` neu; alles außerhalb bleibt Pixel für
Pixel gleich. Beispiel aus der Gruppe `gebaeude` (Mühle ohne Flügel):

```json
{"name": "muehle_ohne", "seed": 3324, "vorlage": "muehle_3003_roh.png",
 "maske": {"rechtecke": [[0, 0, 410, 200], [0, 200, 720, 824]]},
 "prompt": "A round medieval stone mill tower ... The tower has no sails, no blades and no arms at all.",
 "negativ": "sails, blades, windmill arms, spokes, ..."}
```

Masken gibt es als `rechtecke` `[x, y, b, h]`, `ellipsen` `[x, y, rx, ry]` und
`linien` `[x1, y1, x2, y2, Dicke]`. Für Bodenbilder taugt das selten – die
Maskenkanten sieht man in der Fläche. Für Gras lieber einen neuen Seed.

## Prompt-Tipps für Gras

Gesammelt aus den bisherigen Runden (Gruppen `gras`, `bueschel`, `wege`):

- **Kurz bleiben.** Lange Materialbeschreibungen kippen von Pixelkunst ins
  Gemalte. Das Material als Stichwort reicht.
- **Feine Halme:** „seen from (high/directly) above“ und „very fine tiny
  blades“. Das Wort **„tufts“ vermeiden** – es gibt runde Buckel, die gekachelt
  als Muster wiederkehren.
- **Hohes Gras von oben** malt gern Wirbel: „swirl, vortex, spiral, radial,
  center, circle“ ins Negativ (steht schon bei `gras_hoch`/`gras_trocken`).
- **Grüntöne:** Der Stil „hand-painted pixel art, natural colors“ zieht ins
  Oliv-Braune; „fresh green, grass green, some olive“ plus „No bushes“ hilft.
  „Blue-green“ für schattiges Gras wird fast schwarz-türkis – „deep green“
  schreiben.
- **„patches“, „puddles“ meiden:** Qwen ordnet sie als Raster an. „Patches of
  bare earth“ hat einmal einen Weg quer durchs Bild gemalt. Besser „mixed in
  irregularly“.
- **Großflächige Verläufe** (helle Mitte, dunkle Ränder) ergeben an der
  Kachelnaht Schlieren – Bild verwerfen oder `ausgleichen` erhöhen.
- **Immer mindestens 2 Seeds** erzeugen und vergleichen; ein Seed allein ist
  Glückssache.

## Wenn etwas schiefgeht

| Meldung / Beobachtung | Ursache und Abhilfe |
|---|---|
| `URLError` / `Connection refused` bei `qwen_image.py` | ComfyUI läuft nicht oder ist noch beim Laden – Schritt 2 |
| „CUDA out of memory“ im ComfyUI-Fenster | Ollama-Modell noch geladen: `ollama ps`, `ollama stop qwen3.8:27b` |
| `HTTP Error 400: Bad Request` oder `ComfyUI lehnt den Graphen ab: …` | meist eine fehlende oder anders benannte Modelldatei – Tabelle oben; die genaue Ursache steht im ComfyUI-Fenster |
| `Fatal Python error: Aborted` beim Freistellen | Qwen-Image und BiRefNet zugleich im Speicher; mit `--nur-freistellen` wiederholen |
| `fehlt: …_<seed>.png` bei `uebernehmen.ps1` | eingetragener Seed nicht erzeugt (oder `ausgabe/` leer) – Gruppe mit `qwen_image.py` erzeugen |
| Batchdatei findet `python_embeded` nicht | nicht aus ihrem eigenen Ordner gestartet – `-WorkingDirectory` setzen |
| Bild im Spiel unverändert | `dotnet build` vergessen oder falscher `ziel`-Pfad; Checkliste in [Bilder-einbinden.md](Bilder-einbinden.md) |
| Sichtbare Kachelgrenzen im Spiel | Motiv nicht gleichmäßig genug – anderen Seed wählen; `kachelbar` muss gesetzt sein |
