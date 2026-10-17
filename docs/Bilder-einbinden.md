# Bilder im Spiel – Einbindung & Wechseln

Kurz erklärt: wo die Spielbilder liegen, wie sie bis zum Renderpfad kommen,
und wie man ein Bild austauscht (oder ein neues anlegt).

## So hängen Bilder zusammen (Kette)

```
PNG in Content/<Gruppe>/<name>.png
        |
        |  Kompilierung (MonoGame Content Builder)
        v
.xnb in bin/…/net10.0/[Rider]/Content/<Gruppe>/<name>.xnb
        |
        |  Ladezeit: Content.Load<Texture2D>("Gruppe/name")
        v
Texture2D -> Sparte/Zeichnung in RTSGameplayScreen o. Ä.
```

- **Quelle:** Alle PNGs liegen in `AgeOfEvolutions/AgeOfEvolutions.Core/Content/`
  in Unterordnern: `Boden/`, `Baeume/`, `Einheiten/`, `Gebaeude/`, `Felder/`,
  `Gras/`, `Icons/`, `Leiste/`, `Nahrung/`, `Rohstoffe/`, `Sounds/`, `Sprites/`,
  `Tiles/`, `Tiere/`, `Werkzeuge/`, `Backgrounds/`, `Fonts/`, `Overlays/`, `Effects/`.
- **Registrierung:** Jeder einzelnen PNG ist im Content-Bundle
  `AgeOfEvolutions/AgeOfEvolutions.Core/Content/AgeOfEvolutions.mgcb` als eigener
  Block registriert (aktuell 484 Assets). Beispiel `Boden/gras.png`:

  ```
  #begin Boden/gras.png
  /importer:TextureImporter
  /processor:TextureProcessor
  /processorParam:ColorKeyEnabled=False
  /processorParam:GenerateMipmaps=True
  /processorParam:PremultiplyAlpha=True
  /processorParam:ResizeToPowerOfTwo=False
  /processorParam:MakeSquare=False
  /processorParam:TextureFormat=Color
  ```

  Der Content-Build (läuft automatisch bei `dotnet build` über
  `MonoGame.Content.Builder.Task`) kompiliert daraus das `.xnb`, das das Spiel
  zur Laufzeit lädt.
- **Laden:** `ScreenManager.Game.Content.Load<Texture2D>("Boden/gras")`.
  Im Gameplay wird das überall über `LoadOptional(asset)` verpackt
  (`RTSGameplayScreen.cs` ~Zeile 1258):

  Fehlende Bilder werfen also **keinen Crash** – das Spiel fällt auf prozedurale
  Zeichen oder null zurück. **Aber**: Seit dem Fehler-Log meldet jede
  fehlende Asset eine `WARN: Asset fehlt: <name>`-Zeile in `logs/` neben der
  Exe (`Diagnostics/GameLog.cs`) – „Bild kommt nicht“ beginnt jetzt immer
  dort, nicht mehr im Blindtest.

### Namenskodex (was der C#-Code aufruft)

| Gruppe | Beispiele |
|---|---|
| `Boden/` | `gras`, `sand`, `wasser`, `gras_trocken`, `gras_dunkel`, `gras_blumen`, `gras_hoch`, `erde_trocken`, `weg_feudal`, `weg_ritter`, `weg_imperial` |
| `Felder/` | `acker`, `weizen` |
| `Einheiten/` | `dorfbewohner_blau`, `dorfbewohner_rot`, plus `_lauf1.._lauf8` (Gang) und `_schlag1.._schlag8` (Angriff) |
| `Gebaeude/` | je `_<farbe>` (z. B. `muehle_fluegel`), Fahnentuch-Positionen sind pro Asset im Code |
| `Tiere/` | `schaf_blau`, `reich_schaf`, `fleisch_schaf` … |
| `Leiste/` | `dunkel`, `feudal`, `ritter`, `imperial` (Panel pro Zeitalter) |
| `Icons/` | Befehlssymbole (128 px) |
| `Backgrounds/` | `menu.png`, Parallax-Ebenen `Layer0_0` … |

Animationen sind Serien: fehlt **ein** Frame, nullt die Reihe aus und die Figur
gleitet/schlägt ohne Animation (Vertrag in `LoadAttackFrames` / `LoadWalkFrames`).

## Ein bestehendes Bild wechseln

So einfach wie möglich: **Datei ersetzen, neu bauen.**

1. Das PNG unter demselben Namen überschreiben, z. B.
   `AgeOfEvolutions/AgeOfEvolutions.Core/Content/Boden/gras.png`.
   (Namenskonventionen und Farbtöne müssen stimmen: `_blau`/`_rot` für
   Spielerfarben, Animationen als vollständige `_lauf1..8`-/`_schlag1..8`-Reihe.)
2. Neu bauen (kompiliert das `.xnb`):

   ```powershell
   dotnet build
   ```

   Oder nur die Plattform, die du testest:
   ```powershell
   dotnet build AgeOfEvolutions/AgeOfEvolutions.DesktopGL  # bzw. ..WindowsDX
   ```
3. Spiel starten – fertig. Es muss **nichts im Code** geändert werden, nur in
   dem einen Fall, wo das Bild zu klein/groß wurde: `GROUND_TEXELS`, Frame-
   Größen oder die pro-Bild-Positionen (FlagCloth/MillHubs) passen.

Kein mgcb-Eintrag nötig, solange der **Dateipfad identisch** bleibt – der Block
ist schon drin.

## Ein neues Bild anlegen

1. Die PNG nach `AgeOfEvolutions/AgeOfEvolutions.Core/Content/<Gruppe>/<name>.png`
   legen.
2. Einen Block in `AgeOfEvolutions.mgcb` ergänzen – einfach einen
   Nachbarn (z. B. `Boden/gras.png`) kopieren und den Pfad ändern. Für
   Sprites mit Transparenz (Bäume, Gebäude, Einheiten)
   `ColorKeyEnabled=False` belassen; für Hintergrund-Tiles `True` ist
   egal, da `PremultiplyAlpha=True` gesetzt ist.
3. Im C#-Code über `LoadOptional("<Gruppe>/<name>")` laden (bzw. in die
   vorhandene Asset-Liste einfügen).
4. Neu bauen.

**Fehlerquelle #1:** mgcb-Block vergessen -> `LoadOptional` liefert still `null`,
Bild kommt nie ins Spiel. Im Zweifel: `Content.Load` in einem `try/catch`
debuggen oder die `.xnb` in `bin/…/Content/` nachschauen.

## Die offizielle Bild-Pipeline (KI-generiert)

Der „echte" Weg, um (neue) Bilder zu produzieren, lebt unter `tools/bilder/`:

- `bilder.json` – Manifest mit 35 Gruppen (`boden`, `baeume`, `einheiten`,
  `gebaeude_dunkel/…`, `soldaten`, `leiste`, `wege`, …). Jede Gruppe hat
  Stil-Prompt, Größe, `kachelbar`/`spielerfarben`/` zuschneiden`-Schalter und
  eine `bilder[]`-Liste mit `name`, `seed`, `prompt` und optional `ziel`
  (Ziel-Pfad im Content, z. B. `"Boden/sand.png"`).
- `qwen_image.py` – erzeugt die PNGs in `ausgabe/<gruppe>/<name>_<seed>.png`
  (+ `_roh`-Version).
- `uebernehmen.ps1` – kopiert die gewählten Bilder aus `ausgabe/` in
  `AgeOfEvolutions.Core/Content/` und macht die Nachbearbeitung (Beschnitt,
  Kachelbarkeit, Spielerfarben, Größen). Aufruf:

  ```powershell
  pwsh -File tools/bilder/uebernehmen.ps1             # alle Gruppen
  pwsh -File tools/bilder/uebernehmen.ps1 -Gruppe icons
  ```

- **Danach:** neue Ziele per Hand in `AgeOfEvolutions.mgcb` eintragen
  (ausdrücklich im ps1-Kopfkommentar vermerkt) und in C# laden.
- Spezialwerkzeuge: `gang.py`/`gang.json` (Lauf-Reihen),
  `schlag.py`/`schlag.json` (Angriffsphasen), `naht.py` (nahtloses Kacheln).

## Checkliste „Bild ist nicht zu sehen"

1. PNG unter dem exakten Pfad/Namen im Content-Ordner? (Groß-/Kleinschreibung!)
2. In `AgeOfEvolutions.mgcb` ein `#begin <Pfad>.png` drin?
3. Neu gebaut (`.xnb` in `bin/…/Content/` vorhanden)?
4. Für Animationen: alle Frames da (`_lauf1..8` oder `_schlag1..8`), sonst
   nullt die ganze Reihe.
5. Im Code: `LoadOptional` liefert `null` bei `ContentLoadException` – die
   `Asset fehlt: <name>`-Zeile im Log (`logs/` neben der Exe) zeigt, welches.
