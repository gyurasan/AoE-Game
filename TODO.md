# AoE-Clone — TODO / Meilensteine

**Stand:** 2026-10-04 · verifiziert durch Build + Testlauf, nicht aus Doku übernommen.

## Wiederaufnahme — hier weitermachen

Zuletzt angefasst: **Farm und Tiere im neuen Stil** (2026-10-04) — die Farm ist ein Bild
über das ganze Feld (C7h), Schafe und Rehe sind Sprites mit eigenem Wandertakt und
sichtbarem Schritt (C7t), das Bildwerkzeug malt mit Rechteckmaske und Stärke aus (C7m).
Uncommittet im Arbeitsbaum; Commit macht der Nutzer.

Alles baut und läuft: `dotnet build AgeOfEvolutions.slnx` (0 Fehler, 0 Warnungen),
`dotnet test tests/AoE.Tests` → **157/157 grün**.

Der Stand vom 2026-09-23 ist mit `4cb57d4` committet, alles bis zu den Gebäude-Sprites
mit `4f85314` (2026-10-03). Commits macht der Nutzer selbst.

Zuletzt fertiggestellt:

- **Farm und Tiere (2026-10-04)** — `Felder/weizen.png` zeigt das ganze 3×3-Feld mit
  Weizenreihen und Holzzaun, `Felder/acker.png` dasselbe Feld nach der Ernte: Qwen-Image
  hat dafür nur die Weizenreihen neu gemalt (Rechteckmaske), Furchen und Zaun blieben
  Pixel für Pixel. Jede Kachel zeigt ihren Teil (`FieldPart`), erntet und wächst für sich.
  Schafe und Rehe kommen als freigestellte Sprites (`Tiere/`), je zwei Varianten. Jedes
  Tier trägt ein `WildAnimal` mit eigenem Wandertakt — vorher zog je Art immer nur das
  letzte Tier der Liste —, läuft einen Schritt sichtbar in einer Sekunde
  (`WILD_STEP_SECONDS`) und blickt in Laufrichtung. Neue Gruppe `herde` in
  `tools/spielablauf`
- **Grafik aus dem Bildgenerator (2026-10-03)** — ComfyUI mit Qwen-Image-2512 läuft lokal
  unter `D:\Apps\ComfyUI`; `tools/bilder` erzeugt daraus die Spielgrafik (Prompts und Seeds
  in `bilder.json`, wiederholbar) und bereitet sie für `Content/` auf. Neu: Menübild,
  Symbole der Befehlstasten, Gebäude als Sprites in Spielerfarbe (C6i, C6g), Dorfbewohner
  als Sprite mit Bewegung aus dem Code (C6v), kachelbares Gras und fünf Baumarten (C7g),
  Sand, treibendes Wasser sowie Stein- und Goldhaufen (C7r).
  Das Hauptmenü ist mit der Maus bedienbar und wächst mit dem Fenster (E12), der Zoom
  folgt der Fensterhöhe (C10s). Bilder des Spiels in `docs/bilder/` und im README
- **2026-10-03** — die DX-Fassung startet wieder (E11, Grafikprofil HiDef); die Minimap
  sitzt rechts unten in derselben Draufsicht wie die Spielkarte, ein Klick rückt die
  Kamera (C6m, C6d); die Zoomgrenzen wachsen mit der Fensterhöhe (C10z); die
  Befehlstasten reagieren auf Klicks, Bautasten gibt es nur mit ausgewähltem
  Dorfbewohner, und jede Taste ist so breit wie ihr Name (C8c, C8d)
- **C4 Zeitalter** — Taste A steigt im Stadtzentrum auf: Feudalzeit 500 Nahrung, 130 s;
  Ritterzeit 800 Nahrung + 200 Gold, 160 s; Imperialzeit 1000 Nahrung + 800 Gold, 190 s.
  Solange der Aufstieg läuft, bildet das Stadtzentrum nicht aus. Erstes Gebäude der
  Feudalzeit ist der **Wachturm** (Taste T, 50 Holz + 125 Stein, sieht 10 Kacheln weit).
  Logik als `AgeRules` und `AgeProgress` in AoE.Core (`Economy/Ages.cs`, 46 Tests);
  neue Gruppen `minimap`, `zoom`, `leiste`, `zeitalter` und `turm` in `tools/spielablauf`,
  dazu ein Bildschirmfoto-Test (`checks/c6m_minimap.py` mit `checks/_fenster.ps1`)
- **C1 Dorfbewohner-Loop** — Linksklick auf eine Ressource schickt Dorfbewohner
  sammeln: Traglast 10, Abgabe am Stadtzentrum, selbstständig zurück, bei
  erschöpfter Quelle zur nächsten gleichen. Logik als `GatherJob` in AoE.Core
  (15 Tests), im Spiel über `TileMapGatherWorld`
- **Kartengenerator reichlicher** — jede Karte trägt jetzt mehr: 9–13 Waldklumpen
  (vorher 7–10), 3–6 Steinbrüche und 2–5 Goldminen (je um 25 % gesenkt, weil
  Gold und Stein zu viel generiert wurden), mehr Fische, mehr
  Beerenbüsche. Alle Werte stecken im neuen `MapSettings` — das Menü kann sie
  ändern und daraus direkt eine neue Karte bauen (`TileMap(w,h,t,s)`), ohne die
  Generatoren anzugreifen. `MapSettings.Default` = die reichere Grundausstattung.
- **Schaf-Wanderung** — Schafe laufen frei über die Wiese (ein Schaf pro Kachel,
  100 Nahrung). Reserviert sind sie, während ein Dörfler sie erntet: das Schaf
  bleibt am Platz, die Wanderung überspringt es, und `FindNearestSource` lässt
  es für andere Dorfbewohner wegfallen, damit zwei nicht auf dasselbe Schaf
  laufen. Neue Gruppe `schafe` in `tools/spielablauf` prüft Reservierung und
  Wanderung ohne Grafik.
- **C5f Farm** — Getreidefeld (Taste G, 3×3, 175 Nahrung/Kachel, Regrow 100 s),
  neue `farm`-Gruppe in `tools/spielablauf` als Abnahme
- **C5d, C8b** nach dem Screenshot vom 2026-10-01: Wer ein Gebäude fertig hat und nichts
  zu sammeln bekommt, baut an der nächsten unfertigen eigenen Baustelle im Umkreis von
  8 Kacheln weiter; ein Linksklick auf eine eigene Einheit wählt sie aus, statt sie
  wegzuschicken. Neues Werkzeug `tools/spielablauf` prüft solche Abläufe ohne Grafik
- **C5 Bauen** — Dorfbewohner wählen, Taste H (Haus), M (Mühle), F (Holzfällerlager)
  oder B (Bergbaulager), dann den Bauplatz anklicken: die Baustelle wird bezahlt, die
  gewählten Dorfbewohner gehen hin und bauen. Bauzeiten aus AoE II (Haus 25 s, Mühle und
  Lager 35 s); mehrere Arbeiter mit abnehmendem Ertrag — jeder weitere baut ein Drittel
  so schnell wie der erste, vier brauchen die halbe Zeit. Baustellen zählen weder als
  Wohnraum noch als Abgabestelle; wer ein Lager gebaut hat, sammelt gleich daneben.
  Logik als `BuildingRules` und `Construction` in AoE.Core (`Economy/Construction.cs`,
  36 Tests)
- **C2 Bevölkerungslimit** — die Grenze ergibt sich aus den Gebäuden (Stadtzentrum und
  Haus je 5, höchstens 200). Taste Q bildet im Stadtzentrum einen Dorfbewohner aus
  (25 Nahrung, 25 s); bei erreichter Grenze steht die Ausbildung still, „Bev." wird rot
  und die obere Leiste sagt es. Logik als `Population` und `TrainingQueue` in AoE.Core
  (`Economy/Training.cs`, 22 Tests)
- **Häuser** — mit gewähltem Dorfbewohner Taste H, dann Linksklick: 2×2 Kacheln für
  25 Holz; eine grüne oder rote Fläche zeigt, ob das Haus passt. Seit C5 eine Baustelle
  mit 25 s Bauzeit
- **C1t** ein beladener Dorfbewohner behält seine Traglast, wenn man ihn an eine andere
  Quelle derselben Ressource schickt
- **E10** Stein und Gold waren nie abbaubar: `StampResource` setzte nie die Ressource.
  Ein Prüfprogramm über 20 Karten fand Stein 0 von 557 Kacheln abbaubar, Gold 0 von 235;
  die grauen Deko-Felsen ohne Ressource sind weg
- **C1r** Startrohstoffe: Steinbruch und Goldmine liegen 7–10 Kacheln vom Stadtzentrum
  statt im Schnitt 33
- **C1f** Fischen: Schwärme an der Küste mit 200 Nahrung, gefangen vom Ufer aus
- **G1–G4** Wald als Blätterdach mit überlappenden Kronen, ruhigeres Wasser mit Uferlinie
- **E8, E9** nach dem Screenshot vom 2026-09-30: Auswahl, Auswahlrahmen und
  Lebensbalken beziehen sich auf die ganze Figur; das Stadtzentrum wächst beim
  Zoomen im Ganzen mit
- **Nebel des Krieges (C7n)** — nie Gesehenes schwarz, einmal Gesehenes abgedunkelt,
  fremde Einheiten nur in Sicht; die Sicht wird viermal pro Sekunde gerechnet
- **Maussteuerung (C8a)** — rechts markieren (Klick oder Rahmen), links gedrückt
  halten und ziehen verschiebt die Karte, ein kurzer Linksklick ist der Befehl
- **B6 Gebäude** — `Building.Core` hält eine `BuildingEntity`; Gebäude spenden Sicht,
  das Stadtzentrum 5 Kacheln weit
- **Leerlauf** — Zähler „Untätig" in der unteren Leiste, Taste „." springt zum
  nächsten untätigen Dorfbewohner; eine einzeln gewählte Einheit zeigt LP und Traglast
- **C10** Kantenscrollen; die Pfeiltasten schwenken jetzt in Pfeilrichtung (E6)
- **E5** Startplatz: die Dorfbewohner von Spieler 1 standen im Stadtzentrum
- **E7** die Karte lag eine halbe Kachel neben Maus und Einheiten und wuchs beim Zoomen nicht mit
- Nahrung laut Spezifikation: Schaf 100, Beerenbusch 125

Die Screenshots in `docs/Screenshot 2026-09-23 *.png` zeigen den Stand davor
und haben drei der vier Fehler aufgedeckt. Sie sind als Referenz nützlich.

`docs/Screenshot 2026-09-30 224900.png` bestätigt Nebel, Sammelkreislauf (Nahrung 220,
Holz 300), Startplatz und Kachelraster — und hat E8 und E9 aufgedeckt.
`docs/Screenshot 2026-09-30 230012.png` zeigt den Auswahlrahmen um die ganze Figur (E8).
`docs/Screenshot 2026-10-01 225150.png` bestätigt im Spiel: Stein wird abgebaut
(Stein 210, E10), Steinbruch und Goldmine liegen nah am Stadtzentrum (C1r), Baumkronen
ragen über die Kachelgrenzen (G1, G3), „Bev. 4/5" kommt aus den Gebäuden (C2b), das
Baumenü steht in der unteren Leiste (C5b), und Auswahl und Sammelbefehl wirken
(Untätig: 0). Gebaut und ausgebildet wurde darauf noch nichts.
`docs/Screenshot 2026-10-01 225659.png` bestätigt Ausbildung und Limit („Bev. 5/5" rot,
Hinweis oben), ein fertiges Holzfällerlager mit Sammeln danach und die Baustellen-Grafik.
Zwei Baustellen standen bei rund 5 %: ihre Bauarbeiter waren abgezogen worden — per
Linksklick, der hier ein Befehl ist, oder per neuem Bauauftrag. Die Nachstellung in
`tools/spielablauf` hat das bestätigt; daraus wurden C5d und C8b.

Nächste sinnvolle Schritte, in dieser Reihenfolge:

1. **Sichtprüfung im Spiel** — offen: Aufstieg in die Feudalzeit und Bau eines Wachturms,
   Bauen von Mühle und Bergbaulager, Fischen, Kantenscrollen, die Hinweise beim Zeigen
   auf eine Befehlstaste und der Klick in die Minimap. C5d, C8b, die Farm, die Leiste und
   die Zeitalter prüft `tools/spielablauf` bereits ohne Grafik. Bestätigt sind Wald, Stein,
   Gold, die Maussteuerung, Ausbildung mit Q, die rote Bevölkerungsanzeige, das
   Holzfällerlager und die Baustellen-Grafik (Screenshots vom 2026-10-01), dazu per
   Bildschirmfoto Menü, Leiste mit Symbolen, Minimap, Gebäude, Dorfbewohner, Gras und
   Wald (2026-10-03).
2. **Restliche Grafik im neuen Stil** — Beerenbüsche, Fische und Baustelle zeichnet noch
   der Code; neben den Sprites wirken sie flach. Echte Laufbilder für die
   Dorfbewohner bräuchten Qwen-Image-Edit (dieselbe Figur in mehreren Posen).
3. **Bedienoberfläche mit der Auflösung skalieren** — Leisten, Schrift, Tasten und Minimap
   haben feste Pixelmaße und wirken in der DX-Fassung (2707 px hoch) halb so groß; dazu
   ein Infokästchen am Mauszeiger statt des Hinweises in der Leistenmitte.
4. **Weitere Gebäude der Feudalzeit** — die Zeitalter (C4) stehen, freigeschaltet ist
   bisher nur der Wachturm. Schießstand und Stall brauchen die Kampfschleife, der Markt
   den Handel.
5. **Kampfschleife** — `Unit.Attack()` existiert seit B2 und wird nirgends aufgerufen.
   Auf Wunsch des Nutzers zurückgestellt (2026-09-30).
6. **Veraltete Abnahmen** — `c1n`, `c1c` und `c8a` prüfen Schreibweisen von früher (die
   Schaf-Nahrung als Zahl, „Untätig" in `DrawUI`, „Links ziehen" im Hilfetext) und sind
   deshalb rot, obwohl das Verhalten stimmt; auf Verhalten umstellen.
7. **Wild (Rehe)** — fertig: 2–4 Herden je 2–4 Rehe, 150 Nahrung, reserviert wie die
   Schafe, solange ein Dorfbewohner jagt (Gruppe `wild` in `tools/spielablauf`); Sprites
   und Bewegung seit C7t. Offen nur die Sichtprüfung, dass zwei Dörfler nicht auf dasselbe
   Reh laufen.

Farm (C5f) ist fertig: Dorfbewohner wählen, Taste G (Getreide) drücken, Bauplatz
anklicken — 3×3-Feld von 175 Nahrung. Die ausgewählten Dorfbewohner ernten die
Getreidekacheln direkt wie jede andere Nahrungsquelle (Traglast bleibt erhalten) und
liefern an Mühle oder Stadtzentrum; nach der Ernte wächst jede Kachel in rund
100 s wieder nach. Die Grafik wechselt von hellem Getreide zu dunkler Erde, und
`tools/spielablauf` prüft das ohne Grafik. Siehe Block C, C5f.

Werkzeuge: `tools/ollama-agent/` verteilt `[L]`- und `[R]`-Punkte an ein lokales
Ollama-Modell (`qwen3.8:27b`, 256K Kontext) und nimmt sie per Build, Test und
Prüfskript aus `tools/ollama-agent/checks/` ab (`py run_tasks.py --list`).
`[X]`-Punkte gehören nicht dorthin — bei C1, C2 und C5 kamen Entwurf und Tests von
Claude, die Implementierung von `GatherJob`, `Population`, `TrainingQueue` und
`Construction` vom lokalen Modell.
Die 256K stehen seit 2026-10-01 als `num_ctx` im Modell selbst; das unveränderte
Original liegt als `qwen3.8:27b-orig` daneben.

Spielgrafik: `py tools/bilder/qwen_image.py <gruppe>` erzeugt Bilder über ComfyUI (Server
starten mit `D:\Apps\ComfyUI\ComfyUI_windows_portable\run_nvidia_gpu.bat`; Modelle
Qwen-Image-2512 fp8, Qwen2.5-VL-7B als Text-Encoder, BiRefNet zum Freistellen), und
`pwsh -File tools/bilder/uebernehmen.ps1` übernimmt die in `bilder.json` gewählten Seeds
nach `Content/` (zuschneiden, Spielerfarben Blau/Rot, kachelbar). ComfyUI und das
Ollama-Modell passen nicht zugleich in die 32 GB der Grafikkarte: vor einem Agent-Lauf
`POST /free` an ComfyUI, vor Bildern `ollama stop qwen3.8:27b`. Fehlt eine Grafik,
zeichnet das Spiel wie früher selbst.

Kartenfehler sehen die Unit-Tests nicht – sie kennen das Spielprojekt nicht. Dafür gibt
es `tools/kartenpruefung`: `dotnet run --project tools/kartenpruefung -- rohstoffe start fisch`
erzeugt 50 Karten und prüft Regeln des Kartengenerators; bei einem Verstoß endet es mit
Fehlercode und taugt so als Abnahme.

Ähnlich für den Spielbildschirm: `dotnet run --project tools/spielablauf -- bauen weiterbauen linksklick`
lässt die Spielschleife ohne Grafik auf echten Karten laufen und prüft, wer baut, wer
stehen bleibt und was ein Klick bewirkt. Den Bildschirm erreicht es per Reflection —
benennt jemand eine Methode um, meldet es das, statt still falsch zu prüfen.

## Verifizierter Ist-Zustand

| Projekt | Framework | Build | Bemerkung |
|---|---|---|---|
| `src/AoE.Core` | net10.0 | OK, 0 Fehler | Reine Logik, keine MonoGame-Abhängigkeit |
| `tests/AoE.Tests` | net10.0 | **157/157 grün** | Einheiten und Ressourcen (15), Sammelauftrag (21), Ausbildung und Bevölkerung (22), Bauen (39), Zeitalter (46), Konter-Dreieck (5), Wegfindung (3), Nebel (6) |
| `demo/DemoApp` | net10.0 | OK | referenziert AoE.Core |
| `AgeOfEvolutions.Core` | net10.0 | OK, 0 Fehler | Dateiname seit H2 kanonisch |
| `AgeOfEvolutions.DesktopGL` | net10.0 | OK, 0 Fehler / 0 Warnungen | startbar |
| `AgeOfEvolutions.WindowsDX` | net10.0-windows | OK, 0 Fehler | nur Windows |

**Die .NET-10-Migration und der Build sind erledigt.** Der eigentliche Engpass ist ein
anderer, siehe Block B.

## Zielplattformen

**PC und Mac.** `DesktopGL` ist der plattformübergreifende Pfad (Windows, macOS, Linux) und
deklariert `win-x64;osx-x64;osx-arm64;linux-x64` — `osx-arm64` ist für Apple Silicon zwingend.
`WindowsDX` bleibt als zusätzliche reine Windows-Variante.

**Mobile ist kein Ziel.** Die Android- und iOS-Projekte der MonoGame-Vorlage wurden am
2026-09-23 gelöscht; das deckt sich mit `game_plan.md` („Windows, macOS, Linux only — no
mobile"). Die 84 Dateien sind entfernt, die Löschung ist mit `4cb57d4` committet.

## Eignung für Delegation

- **[L] lokal delegierbar** — mechanisch, eng umrissen, durch Build/Test abnehmbar
- **[R] lokal mit Review** — eine Datei, klares Ziel, Ergebnis muss gesichtet werden
- **[X] nicht delegieren** — schneidet quer durch die Architektur, braucht Entwurfsentscheidungen

**Abnahmekriterium für jeden Punkt**, sofern nicht anders genannt:

```
dotnet build AgeOfEvolutions/AgeOfEvolutions.DesktopGL/AgeOfEvolutions.DesktopGL.csproj
dotnet test  tests/AoE.Tests/AoE.Tests.csproj
```

beide fehlerfrei.

---

# Block A — Hygiene & Build (blockiert alles andere)

### H1 · `.gitignore` anlegen, Build-Artefakte aus dem Index nehmen [L]

Die `.gitignore` existiert, wirkt aber nicht auf Dateien, die bereits getrackt sind:
485 Dateien unter `bin/` und `obj/` sowie 16 unter `.vs/` liegen weiterhin im Index.
Deshalb meldet `git status` nach jedem Build geänderte `.dll`-, `.pdb`- und
`.cache`-Dateien (am 2026-09-30: 32 Stück).

- [x] `.gitignore` für .NET + MonoGame + Visual Studio anlegen *(Agent H1)*
- [ ] `git rm -r --cached` für `bin/`, `obj/`, `.vs/`, `Content/obj/`, `Content/bin/`
      — **bewusst nicht automatisiert.** Das stellt 501 Löschungen in den Index;
      dieser Schritt gehört dir, nicht einem Agent. Per Trockenlauf geprüft, erfasst
      genau diese 501 Dateien und keine Quelldatei, in PowerShell wie in der Git-Bash:
      `git rm -r --cached -- ':(glob)**/bin/**' ':(glob)**/obj/**' ':(glob)**/.vs/**'`
      Der früher hier notierte Befehl mit `AgeOfEvolutions/*/bin` erfasste `src/`,
      `tests/`, `demo/` und `Content/` nicht und bricht in der Git-Bash mit
      „pathspec did not match" ganz ab.
- [ ] Abnahme: `git status --short` zeigt nur noch echte Quelldateien

### H2 · csproj-Dateinamen begradigen [L]

Die Datei hieß auf der Platte `AgeofempiresClone.Core.csproj`, während Git und alle
verweisenden Projekte die kanonische Schreibweise nutzten. Unter Windows fiel das nicht auf —
**auf Linux und macOS hätte es den Build gebrochen**, und genau die sind jetzt Zielplattform.

- [x] Auf die kanonische Schreibweise `AgeOfEvolutions.Core.csproj` umbenannt *(Agent H2)*

### H3 · Doppelten `ProjectReference` entfernen [L]

`AgeOfEvolutions.Core.csproj` verwies **zweimal** auf AoE.Core — zwei ItemGroups,
einmal mit `/`, einmal mit `\`.

- [x] Eine der beiden ItemGroups löschen *(Agent H3 — Build grün)*

### H4 · Solution vervollständigen [R]

Die alte `AgeOfEvolutions.sln` enthielt nur Core + DesktopGL; `src/`, `tests/` und `demo/`
lagen in gar keiner Solution.

- [x] Root-Solution `AgeOfEmpire.slnx` mit allen sechs Projekten *(Agent H4)* — XML-Format
      von .NET 10, keine GUIDs; `dotnet restore AgeOfEmpire.slnx` läuft durch
- [ ] Offen: die alte `AgeOfEvolutions/AgeOfEvolutions.sln` liegt noch neben der `AgeOfEmpire.slnx` und kennt nur Core und DesktopGL — löschen oder bewusst behalten

### H5 · Platformer-Reste entfernen [R]

Aus dem MonoGame-Beispiel übrig, vom Menü nicht mehr erreichbar
(`MainMenuScreen.cs:99` führt korrekt zu `RTSGameplayScreen`):
`Game/Level.cs`, `Game/Player.cs`, `Game/Gem.cs`, `Game/GemState.cs`, `Game/Enemy.cs`,
`Screens/GameplayScreen.cs`, `Content/Levels/`

- [x] 6 Quelldateien + 7 `Content/Levels/*.txt` gelöscht, `Levels/`-Einträge aus der
      `.mgcb` entfernt *(Agent H5)* — `Data/Player.cs` und `Data/Tile.cs` blieben unangetastet

### H6 · Android/iOS auf net10 ziehen [L] — erledigt, danach gegenstandslos

- [x] Auf `net10.0-android` / `net10.0-ios` gezogen *(Agent H6)*
- [x] Projekte anschließend gelöscht, weil Mobile kein Ziel ist — siehe Zielplattformen oben

### H7 · DesktopGL für PC und Mac veröffentlichbar machen [L]

- [x] `RuntimeIdentifiers` = `win-x64;osx-x64;osx-arm64;linux-x64` ergänzt *(Agent H7)*
- [x] Abnahme: `dotnet restore -r osx-arm64` läuft durch
- [x] `dotnet build AgeOfEvolutions.slnx` auf macOS: brach mit NETSDK1100 an `WindowsDX`
      (`net10.0-windows`) ab. `WindowsDX.csproj` setzt jetzt außerhalb von Windows
      `EnableWindowsTargeting`; unter Windows ändert sich nichts *(2026-10-04)*
- [ ] Offen: auf echter macOS-Hardware spielen — auf Apple Silicon (osx-arm64) startet
      `--rts` und läuft ohne Ausnahme, das Spielen selbst ist noch nicht geprüft *(2026-10-04)*

---

# Block E — Fehler aus dem Spielbetrieb

E1–E4 gefunden am 2026-09-23 beim Ausprobieren des laufenden Spiels, E5 bis E7 am
2026-09-30 bei der Code-Durchsicht, E8 und E9 am selben Tag im Screenshot, E10 durch
ein Kartenprüfprogramm — keiner davon durch Tests. Alle behoben.

### E1 · ESC führte in ein schwarzes Nichts [L] — behoben

`RTSGameplayScreen.HandleInput` rief bei ESC nur `ExitScreen()` auf. Das Spiel
wird aber über `LoadingScreen.Load(...)` gestartet, und das beendet **alle**
vorhandenen Screens — das Hauptmenü existierte danach nicht mehr. Der Stack war
nach dem Schließen leer: kein Bild, kein Rückweg.

- [x] Stattdessen `ScreenManager.AddScreen(new PauseScreen(), ...)`.
      `PauseScreen` gab es bereits und führt über „Beenden" sauber ins Hauptmenü.

### E2 · Fast die halbe Karte war Wald [L] — behoben

In `GetRandomTileType` lief `x * 73856093` ab **x ≥ 30** über `int.MaxValue` und
wurde negativ. In C# liefert `%` bei negativem Dividenden ein negatives Ergebnis,
womit `rand < 4` (Wald) immer zutraf.

Nachgerechnet über die ganze 64×64-Karte:

| | vorher | nachher | beabsichtigt |
|---|---|---|---|
| Wald | **47,8 %** | 4,8 % | ~4 % |
| Fels | 2,0 % | 3,5 % | ~3 % |
| Gras | 50,2 % | 91,7 % | ~93 % |

Die Spalten x = 30…58 waren zu **100 %** Wald — eine massive Waldwand über 45 %
der Kartenbreite. Gut im zweiten Screenshot zu sehen.

- [x] `unchecked` plus Ausmaskieren des Vorzeichenbits (`& 0x7FFFFFFF`);
      die Karte bleibt dabei reproduzierbar

### E3 · Kamera zeigte überwiegend Fläche außerhalb der Karte [L] — behoben

Siehe C10. Der erste Screenshot zeigt es deutlich: die Karte klebte im unteren
rechten Viertel, der Rest war leerer Hintergrund.

- [x] `ClampCamera()` in `RTSGameplayScreen`

### E4 · Fenster fest auf 1280×768 [L] — behoben

Auf einer hochauflösenden Anzeige winzig.

- [x] Fenstergröße auf 80 % der Bildschirmfläche, nach unten auf 1280×768 begrenzt
- [x] `Window.AllowUserResizing = true` plus `ClientSizeChanged`-Behandlung, die
      den Bildpuffer nachzieht (mit Wiedereintrittsschutz — `ApplyChanges()` löst
      das Ereignis selbst wieder aus)

### E5 · Dorfbewohner im Stadtzentrum, Startplatz im See [L] — behoben

Die vier Dorfbewohner von Spieler 1 entstanden *innerhalb* des Stadtzentrums
(4 × 4 Kacheln, nicht begehbar). Der auf der inneren Kachel hatte nur gesperrte
Nachbarn — die Wegsuche kennt vier Richtungen — und kam nie heraus. Außerdem
konnten Seen und verstreute Wald- oder Felskacheln auf dem Startplatz landen.

- [x] Dorfbewohner von Spieler 1 rechts unterhalb des Stadtzentrums, spiegelbildlich
      zu Spieler 2 *(Agent E5)*
- [x] `ClearStartArea` räumt Stadtzentrum plus zwei Kacheln Rand zu Wiese frei *(Agent E5)*

### E6 · Pfeiltasten verkehrt herum [L] — behoben

Links schob den Ausschnitt nach rechts, und die Geschwindigkeit hing an der Bildrate
(feste 10 Einheiten pro Frame).

- [x] Richtung korrigiert, 800 Bildschirmpixel pro Sekunde unabhängig von Bildrate
      und Zoom — zusammen mit dem Kantenscrollen *(Agent C10)*

### E7 · Karte um eine halbe Kachel versetzt, ohne Zoom gezeichnet [R] — behoben

`DrawTileMap` setzte die linke obere Ecke jeder Kachel auf ihren *Mittelpunkt*
(`GridToWorld`) und zeichnete sie fest 32 × 32 Pixel groß. Die Karte lag damit
eine halbe Kachel rechts unterhalb der Welt, in der Maus und Einheiten rechnen —
ein Klick auf die rechte untere Hälfte einer Goldmine traf die Nachbarkachel.
Bei Zoom 2 klafften Lücken zwischen den Kacheln, bei 0,5 überlappten sie.

- [x] `TileScreenRect` rechnet das Bildschirmrechteck aus den Weltecken; Kacheln,
      Nahrungsobjekte und die Grundfläche der Gebäude nutzen es *(Agent E7)*

### E8 · Auswahl traf nur die Füße [R] — behoben

Trefferfläche und Auswahlrahmen waren ein Kästchen um die *Füße* einer Einheit, die
Figur reicht aber 24 Einheiten nach oben. Ein Klick auf Kopf oder Oberkörper wählte
nichts aus, der Rahmen umfasste die untere Körperhälfte und das Schaf darunter, der
Lebensbalken lag quer über der Brust (Screenshot vom 2026-09-30).

- [x] `UnitWorldRect` beschreibt die Figur; Klick- und Rahmenauswahl prüfen dagegen,
      Rahmen und Lebensbalken sitzen an der Figur und wachsen mit dem Zoom *(Agent E8)*

### E9 · Stadtzentrum wuchs beim Zoomen nur als Grundplatte [R] — behoben

Haus, Dach, Tür, Fenster und Ecktürme hatten feste Pixelmaße. Bei Zoom 2 blieb das
Haus ein kleiner Fleck im großen Erdplatz. Der Pfad vor der Tür wurde nie gezeichnet —
seine Höhe kam negativ heraus.

- [x] `BuildingPart` rechnet den 128 × 128-Entwurf auf die tatsächliche Größe um *(Agent E9)*

### E10 · Stein und Gold waren nie abbaubar [L] — behoben

`StampResource` setzte bei den Rohstoffklumpen Kacheltyp und Menge, aber nie
`ResourceType`. Die Ressource einer Kachel stammte damit aus ihrem Konstruktor: Wiese
keine, zufälliger Einzelbaum Holz. Ein Prüfprogramm über 20 erzeugte Karten fand Stein
0 von 557 Kacheln abbaubar, Gold 0 von 235, Wald aus Klumpen zu einem Drittel nicht —
und 35 Stein- und Goldkacheln, die Holz lieferten. Dazu kamen rund 3 % graue
Deko-Felsen, die wie Stein aussahen, aber nie etwas hergaben.

- [x] `StampResource` setzt die Ressource des Klumpens *(Agent E10)*
- [x] Deko-Felsen entfernt *(Agent E10)*
- [x] Abnahme: `tools/kartenpruefung` — alle Stein-, Gold- und Waldkacheln abbaubar

### E11 · DX-Fassung stürzte beim Start ab [L] — behoben

`CreateSwapChain` meldete `E_INVALIDARG`: Mit dem Standardprofil Reach legt MonoGame unter
DirectX ein Gerät mit Feature Level 9_3 an, dessen Bildpuffer höchstens 4096 Pixel breit sein
darf. Mit der Windows-Einstellung „Hohe DPI-Skalierung überschreiben: Anwendung" rechnet die
DX-Fassung in physischen Pixeln, und 80 % des 6K-Bildschirms sind 4812.

- [x] Grafikprofil HiDef (Feature Level 11_0, bis 16384 Pixel) *(Agent E11)*; die Abnahme
      `checks/e11_dx_start.py` startet die DX-Fassung mit `__COMPAT_LAYER=HIGHDPIAWARE`

### E12 · Hauptmenü nur mit Enter bedienbar [R] — behoben

Screenshot `docs/Menu noch Fehlerhaft.png`: `MainMenuScreen` wertete nur die Tastatur aus,
und Titel und Einträge standen in festen Pixeln — bei hoher Auflösung ein kleiner Fleck oben
in der Mitte, über einer Hintergrundgrafik mit eigenen, nur aufgemalten Schaltflächen.

- [x] Maus: Zeigen wählt, Klick startet; Tafel in der Bildmitte, für 1080 px entworfen und
      mit der Fensterhöhe skaliert, Hintergrund abgedunkelt; eigene Menüschrift (40 pt);
      „Spiel laden" grau, bis es das gibt *(Agent E12)*
- [x] Einstellungen und Pause (`MenuScreen`): die Klickfläche lag eine halbe Zeile unter
      dem Text *(Agent E12)*
- [x] Neue Hintergrundgrafik, erzeugt mit Qwen-Image (`tools/bilder`) — die alte enthielt
      fremde Logos (Age of Empires IV, Xbox Game Studios, Relic, MSN Games) *(Agent C6i)*

### Kein Fehler, nur zur Beruhigung

Die grünen Textfragmente am linken Bildrand der ersten beiden Screenshots waren
der **Visual-Studio-Editor hinter dem Spielfenster** — die sichtbare Zeile war
ein XML-Kommentar aus `Unit.cs` („…mindestens 1."). Kein Renderfehler.

---

# Block B — AoE.Core tatsächlich verwenden (der eigentliche Engpass) [X]

**Erledigt am 2026-09-23.** Das Spiel verwendet jetzt durchgehend `AoE.Core`;
die parallelen Implementierungen sind entfernt. Vorher lief in beiden Projekten
dasselbe Konzept doppelt — und die getestete Variante war die, die im Spiel
*nicht* lief:

| Konzept | vorher im Spiel | jetzt |
|---|---|---|
| Ressourcen | `Resource`-struct + `Dictionary` | `AoE.Core.Economy.ResourcePool` |
| Einheitenwerte | eigene Werttabelle | `UnitEntity` über `Data/CoreUnits.cs` |
| Schaden | `AttackPower` + Faktor 2 | `DamageCalculator` mit Angriffs-/Rüstungsklassen |
| Zustand | eigene `UnitState`-Enum | `AoE.Core.Entities.UnitState` |
| Wegfindung | Luftlinie | `AoE.Core.Pathfinding` (A*) |
| Sicht | `FogOfWar` (ungenutzt) | `AoE.Core.Map.VisibilitySystem` |

Gelöscht: `Data/Resource.cs`, `Data/FogOfWar.cs` (172 Zeilen), die Werttabelle in
`Data/Unit.cs` und die doppelte `UnitState`-Enum.

**Der Entwurf:** das Spiel *hält eine Referenz* auf die Core-Einheit
(`Unit.Core`) und reicht Kampfwerte, Lebenspunkte und Zustand durch. Rendering-State
(`Vector2 Position`, `IsSelected`, `Path`) bleibt im Spiel — Core hat bewusst keine
MonoGame-Abhängigkeit.

### B1 · `ResourcePool` verdrahten [X]

Kleinster Einstieg, weil der Typ isoliert ist und Tests hat.

- [x] `Player.Resources` → `AoE.Core.Economy.ResourcePool`
- [x] `Data/Resource.cs` entfernt — der struct war toter Code, genutzt wurde nur
      die verschachtelte `Type`-Enum (42 Stellen), jetzt `AoE.Core.Entities.Resource`
- [x] Ressourcenanzeige liest aus dem Pool
- [ ] **Verhaltensänderung:** Startkapital kommt jetzt aus `ResourcePool`
      (200/200/100/**200**). Das Spiel begann vorher mit 100 Stein. Falls du die
      alten 100 willst, muss `ResourcePool.START_STONE` geändert werden.

### B2 · `DamageCalculator` verdrahten [X]

- [x] `Unit.DamageAgainst()` und `Unit.Attack()` rechnen über `DamageCalculator`
- [x] Konter-Dreieck steckt jetzt in den Klassenboni statt im pauschalen Faktor 2
- [x] Abnahme: `tests/AoE.Tests/CounterTriangleTests.cs`, 5 Tests
- [x] Nebenbefund behoben: die Werttabelle deckte nur 8 der 17 Einheitentypen ab —
      die übrigen 9 kamen mit **0 Lebenspunkten** zur Welt. `CoreUnits.Create`
      bildet jetzt alle 17 ab.
- [ ] Offen: das Spiel ruft `Attack()` noch nirgends auf — es gibt keine
      Kampfschleife. Das gehört zu Block C.

### B3 · Einheitentypen vereinen [X]

Die größte Einzeländerung — hier zuerst einen Entwurf, dann Code.

- [x] Entschieden: **Referenz statt Vererbung** — `Unit.Core` hält die Core-Einheit,
      `Health`, `MaxHealth`, `AttackPower`, `Armor`, `AttackRange`, `MovementSpeed`
      und `State` werden durchgereicht
- [x] Doppelte `UnitState`-Enum entfernt; `Returning` und `Dead` sind dafür nach
      `AoE.Core` gewandert, weil das Spiel sie braucht und sie dort hingehören

### B4 · A* statt Luftlinie [X]

- [x] Luftlinie durch A* aus `AoE.Core.Pathfinding` ersetzt
- [x] `TileMap` spiegelt die Begehbarkeit in ein `MapGrid`; es wird nur
      aufgefrischt, wenn ein Gebäude gesetzt wurde (`InvalidateNavigation()`)
- [x] Abnahme: `tests/AoE.Tests/PathfindingTests.cs` — läuft um eine Mauer herum,
      und ein eingemauertes Ziel liefert sauber *keinen* Weg statt eines Absturzes

### B5 · Fog of War vereinen [X]

- [x] `Data/FogOfWar.cs` gelöscht, `AoE.Core.Map.VisibilitySystem` verdrahtet
- [x] **Fehler in AoE.Core gefunden und behoben:** `UpdateTileVisibility` setzte
      Kacheln nur auf `Visible` und stufte nie zurück. Einmal Gesehenes wäre für
      immer hell geblieben — der Zustand „erforscht, aber veraltet" kam damit
      *nie* vor. Aufgefallen ist es erst durch den Test.
- [x] Abnahme: `tests/AoE.Tests/FogOfWarTests.cs`, 3 Tests
- [x] Seit C7n aufgerufen: `RTSGameplayScreen` rechnet die Sicht viermal pro Sekunde
      und zeichnet den Nebel *(Agent C7n)*

### B6 · Gebäude auf `AoE.Core.Entities.Buildings` umstellen [X] — erledigt

Beim Aufräumen von Block B übrig geblieben: die `Building`-Klasse in
`TileMap.cs` führt eigene Werte (`MaxHealth = 1000`), während `AoE.Core`
mit `Buildings.cs` (419 Zeilen) und `BuildingEntity` eine eigene Fassung hat.
Dasselbe Muster wie B1–B5, nur für Gebäude.

- [x] `Building` auf `BuildingEntity` umstellen, analog zu `Unit.Core` — `Building.Core`,
      erzeugt von `CoreBuildings.Create`; die Lebenspunkte kommen von dort
- [x] `MapGrid.AddBuilding` nutzen, damit Gebäude Sicht und Wegfindung beeinflussen —
      `TileMap.AddBuilding` meldet das Gebäude an, `VisibilitySystem` zählt Gebäude
      als Sichtquelle (3 neue Tests); Gebäudekacheln sperrte die Wegfindung schon
      über `TileMap.IsWalkable`

---

# Block C — Gameplay (Spec-Reihenfolge, `docs/AgeOfEmpires.md` Kap. „Relevanz")

Die Spec priorisiert nach Spielgefühl, nicht nach Aufwand. C1 ist der Punkt, ohne den sich
nichts nach AoE anfühlt.

### C1 · Dorfbewohner-Loop [X] — höchste Priorität

laufen → sammeln → Traglast 10 → Abgabestelle → zurück → **automatisch fortsetzen**

- [x] Automatische Ressourcensuche und -sammlung — Linksklick auf eine Ressource (seit C8a);
      ist die Quelle erschöpft, sucht der Dorfbewohner im Umkreis von 8 Kacheln
      die nächste gleiche
- [x] Abgabe an nächstgelegene Stelle — Stadtzentrum, seit C5 auch Mühle,
      Holzfällerlager und Bergbaulager, sobald sie fertig gebaut sind
- [x] Automatischer Neustart derselben Ressource nach Abgabe
- [x] Leerlauf-Erkennung: untätig heißt Zustand Idle ohne Sammelauftrag; Zähler in
      der unteren Leiste, Taste „." *(Agent C1c)*
- [x] Startrohstoffe: je Spieler ein Steinbruch und eine Goldmine 7–10 Kacheln vom
      Stadtzentrum, zur Kartenmitte hin *(Agent C1r)*
- [x] Fischen: Schwärme an der Küste, 200 Nahrung; der Dorfbewohner stellt sich auf
      eine begehbare Nachbarkachel und fängt vom Ufer aus *(Agent C1f)*
- [ ] Offen: Sichtprüfung im laufenden Spiel — Holz, Stein und Nahrung (Schafe) sind
      auf den Screenshots vom 2026-09-30 und 2026-10-01 bestätigt, Fischen fehlt noch
- [x] Schickt man einen beladenen Dorfbewohner an eine andere Quelle derselben
      Ressource, behält er seine Traglast — wie in AoE *(Agent C1t)*

Aufbau: die Logik ist ein Zustandsautomat `GatherJob` in AoE.Core
(`Economy/GatherJob.cs`, 15 Tests); das Spiel übersetzt ihn über
`Data/TileMapGatherWorld.cs` auf die Kachelkarte. Vorher war Sammeln im Spiel
unerreichbar: kein Befehl setzte den Zustand `Gathering`, `Returning` hatte keine
Behandlung, alle Einheiten teilten sich einen Sammel-Timer, und abgeliefert wurden
nur 70 %.

### C2 · Bevölkerungslimit [R] — erledigt

- [x] Gemeinsame Obergrenze (default 200) für Wirtschaft **und** Armee — `Population.Capacity`
      zählt die Gebäude, jede Einheit belegt einen Platz *(Agent C2a, C2b)*
- [x] Haus = +5 Plätze, Kosten 25 Holz — Taste H, Linksklick, 2×2 Kacheln; seit C5 eine
      Baustelle mit 25 s Bauzeit, erst das fertige Haus zählt *(Agent C2c, C5b)*
- [x] Bei vollem Limit Produktion stoppen — mit sichtbarem UI-Hinweis: „Bev." rot, oben
      „Bevölkerungslimit erreicht" *(Agent C2b)*
- [x] Ausbildung im Stadtzentrum: Taste Q reiht einen Dorfbewohner ein (25 Nahrung, 25 s,
      bis zu 15 in der Warteschlange) *(Agent C2b)*

Achtung: die Spezifikation nennt 25 Nahrung je Dorfbewohner, im Original kostet er 50.
Übernommen ist der Wert der Spezifikation (`VillagerCost` in `RTSGameplayScreen`).

### C3 · Endliche Vorkommen [L]

Laut Spec Punkt 6: ohne das hat eine Partie kein natürliches Ende.

- [x] Abbau reduziert den Vorrat (`tile.ResourceAmount--`) statt zu regenerieren *(Agent C3)*
- [x] Erschöpfte Quelle verschwindet bzw. wird zu Normalgelände *(Agent C3)*
- [x] Folge davon geschlossen durch C1: der Dorfbewohner sucht die nächste gleiche
      Quelle, die erschöpfte Kachel wird zu Wiese

### C4 · Zeitalter als Gate [X]

- [x] Stadtzentrum: Upgrade nach Feudal-/Ritter-/Imperialzeit — Taste A oder „Zeit." in der
      Leiste; solange der Aufstieg läuft, bildet das Stadtzentrum nicht aus *(Agent C4b)*
- [x] Kosten 500N · 800N+200G · 1000N+800G, Dauer 130 / 160 / 190 s — `AgeRules` und
      `AgeProgress` in AoE.Core (`Economy/Ages.cs`, 46 Tests) *(Agent C4a)*
- [ ] Jedes Zeitalter schaltet Gebäude und Einheiten frei — die Regel steht
      (`AgeRules.RequiredAgeOf`, Baumenü und Tasten richten sich danach). Erstes
      Gebäude der Feudalzeit: **Wachturm** (Taste T, 50 Holz + 125 Stein, 80 s, sieht
      10 Kacheln weit) *(Agent C4c)*. Offen: weitere Gebäude und die Einheiten — die
      brauchen die zurückgestellte Kampfschleife

### C5 · Bauen [X] — erledigt

- [x] Bauzeiten je Gebäude — Richtwerte aus AoE II, die Spezifikation nennt keine: Haus
      25 s, Mühle und Lager 35 s, Farm 15 s, Kaserne 50 s, Stadtzentrum 150 s *(Agent C5a)*
- [x] Bauplatz-Logik: Kollision, freie Fläche — `TileMap.CanPlaceBuilding`, dazu Nebel und
      Einheiten; seit C5b für jedes Gebäude des Baumenüs *(Agent C2c, C5b)*
- [x] Mehrere Arbeiter mit abnehmendem Ertrag — Formel aus AoE II: n Arbeiter bauen
      (n + 2) / 3-mal so schnell wie einer, vier also doppelt so schnell *(Agent C5a)*
- [x] Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager; ein Linksklick mit
      Dorfbewohnern auf eine eigene Baustelle schickt sie zum Mitbauen *(Agent C5b)*
- [x] Lager als Abgabestellen, sobald sie fertig sind; ihre Erbauer sammeln danach gleich
      die passende Ressource in der Nähe *(Agent C5b)*
- [x] Eigene Grafik für Baustelle (mit Fortschrittsbalken), Mühle, Holzfällerlager und
      Bergbaulager *(Agent C5c)*
- [x] Liegengebliebene Baustellen: wer ein Gebäude fertig hat und nichts zu sammeln
  bekommt, baut an der nächsten unfertigen eigenen Baustelle im Umkreis von 8 Kacheln
  weiter *(Agent C5d)*
- [ ] Offen: Ein beladener Dorfbewohner, den man bauen schickt, verliert seine Traglast —
  AoE behält sie
- [x] **C5f Farm** — Dorfbewohner wählen, Taste G (Getreide) drücken, einen freien
  3×3-Platz anklicken: 60 Holz, 9 Kacheln je 175 Nahrung, alle begehbar (wie ein
  Beerenbusch). Die Ernte geht an Mühle oder Stadtzentrum, nach der Ernte wächst jede
  Kachel in ~100 s wieder nach. Logik in `TileMap.PlantCrop` und `TileMap.RegrowCrop`;
  Abnahme als `farm`-Gruppe in `tools/spielablauf` *(2026-10-03)*

### C6 · HUD dreiteilig [R]

- [ ] Ressourcenleiste oben: `[Holz] [Nahrung] [Gold] [Stein] [Bev. 37/45] « Feudalzeit »`
- [x] Bevölkerung rot bei erreichtem Limit *(Agent C2b)*
- [ ] Kommandoleiste unten: Aktionssymbole – Einheiteninfo – Minimap
- [x] Befehlstasten mit Symbolen (Qwen-Image, `tools/bilder`); Name, Kürzel und Kosten
      nennt die Leiste, solange die Maus auf einer Taste steht *(Agent C6i)*
- [ ] Minimap: kleine Karte rechts unten in der Kommandoleiste, in derselben Ansicht wie
      die Spielkarte (zurzeit Draufsicht, Norden oben; Entscheidung 2026-10-03, statt
      der Raute aus dem AoE-II-HUD) — Farben nach dem Referenzbild `docs/overview.jpg`:
      Grün Land, Braun Wald, Blau Wasser, weiße Punkte eigene Einheiten, dunkler Rahmen.
      Feinde rot, Kameraausschnitt als Rechteck *(Agent C6m, C6d)*

### C7 · Terrain-Rendering vervollständigen [R]

- [x] Nebel des Krieges: unerforscht schwarz, erforscht abgedunkelt, fremde Einheiten
      nur in Sicht; ein Klick in den Nebel ist ein Laufbefehl *(Agent C7n)*
- [x] Bäume als prozedurale Pixel-Art — bestand schon (`BuildForestTexture`, `DrawTree`)
- [x] Beeren, Stein, Goldminen als Textur statt Farbfläche — bestand schon
      (`BuildBerryTexture`, `BuildMountainTexture`, `BuildGoldTexture`)
- [ ] Schatten und Beleuchtung
- [x] Stadtzentrum skaliert im Ganzen mit dem Zoom *(Agent E9)*
- [x] Gebäude als Sprites aus Qwen-Image (`tools/bilder`, freigestellt mit BiRefNet), Fahnen
      und Banner in Spielerfarbe; Baustellen und die Farm zeichnet weiter der Code *(Agent C6g)*
- [x] Dorfbewohner als Sprite (Qwen-Image) mit Bewegung aus dem Code: wippt beim Gehen, holt
      beim Sammeln und Bauen aus, atmet im Stehen, blickt in Laufrichtung, Schatten und
      Traglast-Bündel *(Agent C6v)*
- [x] Werkzeuge als eigene Sprites in der Faust: Axt, Spitzhacke, Hammer, Sichel, Hacke und
      Angel je nach Arbeit; beim Arbeiten holt das Werkzeug aus und schlägt zu, die Figur
      steht still. Die Hacke hat Qwen-Image per Inpainting aus dem Dorfbewohner-Bild
      entfernt *(Agent C6t)*
- [x] Gras aus einem großen, kachelbar gemachten Grasbild (Qwen-Image), vier Bildpixel je
      Welteinheit, wiederholt alle acht Kacheln; Schaf und Beerenbusch auf durchsichtigem
      Grund darüber; Wald mit fünf Baumarten als Sprites mit Stamm statt runder
      Kronen *(Agent C7g)*
- [x] Sand und Wasser aus kachelbaren Bodenbildern (Qwen-Image), das Wasser in zwei
      gegeneinander treibenden Lagen; Stein und Gold als freigestellte Haufen auf Gras,
      zwei Varianten je Rohstoff; Gras statt dunkler Kacheln unter den Bäumen; der Boden
      in einem eigenen Durchgang mit wiederholender Abtastung (`DrawGround`) *(Agent C7r)*
- [x] Felder aus Acker und Weizen statt der gezeichneten Farmtextur: beide kachelbar
      (Boden/acker, Boden/weizen), die Kante des Felds als dunkler Erdstreifen, und der
      Weizen wächst sichtbar beim Nachwachsen (WheatLook aus Vorrat und FarmRegrow);
      FARM_FOOD und FARM_REGROW_SECONDS als Konstanten in TileMap *(C7f)*
- [x] Farm als ein Bild über das ganze Feld: Weizenreihen mit Holzzaun (Felder/weizen) und
      dasselbe Feld abgeerntet (Felder/acker, per Rechteckmaske aus dem Weizenbild
      ausgebessert, damit Reihen und Zaun deckungsgleich bleiben); jede Kachel zeigt ihren
      Teil (FarmCol, FarmRow, FieldPart), der Zaun ersetzt die gezeichnete Feldkante *(Agent C7h)*
- [x] Schafe und Rehe als freigestellte Sprites (Tiere/, je zwei Varianten) in der
      Zeilenschicht; jedes Tier mit eigenem Wandertakt (WildAnimal), einem sichtbaren
      Schritt von Kachel zu Kachel, Blickrichtung und Schatten *(Agent C7t)*
- [x] Tiere gehen: je Tier zwei Laufbilder (Tiere/<name>_lauf1, _lauf2), für die Qwen-Image
      nur die Beine neu gemalt hat; uebernehmen.ps1 schneidet alle Bilder einer Figur auf
      dasselbe Rechteck (Feld "figur"), und solange ein Schritt läuft, wechseln Schritt,
      Stand, Gegenschritt, Stand (WalkPhase) *(Agent C7u, C7w)*
- [x] Wald als Blätterdach: dunkler Boden als Kachel, Baumkronen als eigene Figuren
      über Kachelgrenzen hinweg *(Agent G1, G3)*
- [x] Wasser: zwei überlagerte Wellen in drei Varianten, festes Rauschen ohne Flimmern,
      Uferlinie mit Schaumstrich *(Agent G2, G4)*

### C10 · Kamera [R]

- [x] Zoomen mit dem Mausrad, **auf den Cursor zu** statt auf die Bildmitte
      (RTSGameplayScreen, multiplikativer Schritt 1,12; Grenzen MIN_ZOOM 0,5 / MAX_ZOOM 2,0
      bis 1080 px Fensterhöhe, darüber wachsen sie und der Startzoom mit *(Agent C10z)*)
- [x] Der Zoom folgt der Fensterhöhe, der Ausschnitt bleibt gleich groß — vorher begann das
      Spiel mitunter mit Zoom 1 statt 1,25, wenn das Fenster seine Größe erst nach dem
      Laden bekam *(Agent C10s)*
- [x] Falle umgangen: ``ScrollWheelValue`` zählt seit Programmstart kumuliert —
      ohne Startwert springt der erste Frame auf Maximalzoom
- [x] Kamera an den Kartenrand geklemmt (`ClampCamera()`) — vorher zeigte der
      Startausschnitt überwiegend Fläche außerhalb der Karte, siehe Screenshot
      vom 2026-09-23 in `docs/`
- [ ] **Offene Entwurfsfrage:** die Karte ist 64×64 Kacheln à 32 px = 2048 px,
      ein typisches Fenster ist breiter. Bei Zoom 1 passt die ganze Karte ins
      Bild und wird jetzt zentriert — links und rechts bleibt Rand. Entweder
      größere Karte, oder MIN_ZOOM dynamisch so setzen, dass die Karte das
      Fenster immer füllt (schließt dann aber die Gesamtübersicht aus)
- [x] Kantenscrollen mit der Maus, 8 Pixel Randstreifen *(Agent C10)*
- [x] Karte mit gedrückter linker Maustaste ziehen, ab 6 Pixeln Weg *(Agent C8a)*

### C8 · Auswahl & Steuerung [R]

- [x] Selektion zeigt HP/Status — für eine einzeln gewählte Einheit: Name, LP und
      bei Dorfbewohnern die Traglast *(Agent C1c)*
- [x] Befehl → Bewegung oder Sammeln — seit C8a per kurzem Linksklick, einmal pro Klick
- [x] Lasso-Auswahl per Mauszug — seit C8a mit der rechten Taste (bestand schon als Rechteck)
- [x] Maussteuerung auf Wunsch des Nutzers umgestellt: rechts markieren, links ziehen
      verschiebt die Karte, kurzer Linksklick befiehlt *(Agent C8a)*
- [x] Ein Linksklick auf eine eigene Einheit wählt sie aus, statt sie wegzuschicken —
      vorher zog ein Klick zum Auswählen einen Bauarbeiter vom Bau ab *(Agent C8b)*
- [x] Befehlstasten und Minimap reagieren auf Klicks — vorher kam kein Klick in die
      untere Leiste an. Bautasten nur, solange ein Dorfbewohner ausgewählt ist; Q und
      „." immer *(Agent C8c)*
- [x] Befehlstasten so breit wie ihr Name, der Name sitzt in der Taste — „Bergbaulager" und
      „Untätig" liefen in die Nachbartaste, alle Namen unten aus der Taste *(Agent C8d)*

### C9 · Basis-KI [X]

- [ ] Einheit sucht nächste Ressourcenquelle und sammelt

---

# Block D — Doku

### D1 · `docs/PROJEKT_STRUKTUR.md` reparieren [R]

Die Datei ist **ab „Wichtige Dateien" textuell zerschossen** — sieht nach einem
fehlgeschlagenen Patch aus:

- Pfade, die nicht existieren: `src/AfempireCore/core.Pathfinding.cs`,
  `AgeofempiresClone.Core.Screens/TSGameplaScreen.ccsproj`, `src/AoE.Map/VisibilitySystem.Vsible.cs`
- Mermaid-Diagramm mit kaputter Syntax (`Combat[DamageCalc]<br/>Schaden:::src`)
- Build-Status-Tabelle mit zerbrochener Zeile
- Verweist auf `AoE1-Readme.md` und das `.webp` im Root — beide gelöscht
- `MapGrid.cs` als eigene Datei gelistet — liegt tatsächlich in `VisibilitySystem.cs:241`

- [x] Komplett neu geschrieben *(Agent D1)* — erfundene Pfade raus, Build-Tabelle gegen den
      echten Stand, kein Mermaid mehr, Stand 2026-09-23

### D4 · Zielplattformen in den MD-Dateien festhalten [L]

- [x] `README.md`: Abschnitt „Zielplattformen"; Android/iOS als nicht unterstützt vermerkt *(Agent D4)*
- [x] `AgeOfEvolutions/game_plan.md`: Hinweis auf die Entfernung der Mobile-Projekte *(Agent D4)*
- [x] `docs/PROJEKT_STRUKTUR.md`: im Kopf vermerkt *(Agent D1)*

### D2 · `README.md` im Root [L]

Existiert nicht.

- [x] Kurzbeschreibung, Build-Anleitung, wie man das Spiel startet *(Agent D2)*

### D3 · `.github/copilot-instructions.md` [L]

Enthält reines Azure-Boilerplate ohne jeden Bezug zum Projekt.

- [x] Durch projektbezogene Hinweise ersetzt *(Agent D3)*

---

# Erledigt

- [x] Projektstruktur konsolidiert, MD-Dateien zentral in `docs/`
- [x] .NET-10-Migration: AoE.Core, Tests, Demo, Spiel-Core, DesktopGL, WindowsDX
- [x] Build läuft fehlerfrei durch (AoE.Core + DesktopGL, 0 Warnungen)
- [x] AoE.Core als Projektverweis eingebunden *(seit Block B im Spiel genutzt)*
- [x] 157 Unit-Tests grün
- [x] Kartengenerierung `TileMap.cs`: Seen mit Falloff, Beeren, Schafe, Wildschweine,
      Stein/Gold, PvP-Startpositionen
- [x] Wasser-Animation
- [x] Menü führt zu `RTSGameplayScreen`
- [x] Platformer-Reste des MonoGame-Beispiels entfernt
- [x] Root-Solution `AgeOfEmpire.slnx`, `.gitignore`, `README.md`
- [x] Zielplattformen auf PC + Mac festgelegt, Mobile-Projekte entfernt
- [x] Endliche Ressourcenvorkommen (C3)
- [x] Dorfbewohner-Loop (C1), Kantenscrollen (C10), Startplatz (E5)
- [x] Nebel des Krieges (C7n), Maussteuerung (C8a), Gebäude aus AoE.Core (B6)
- [x] Auswahl an der ganzen Figur (E8), Stadtzentrum skaliert mit dem Zoom (E9)
- [x] Stein und Gold abbaubar (E10), Startrohstoffe (C1r), Fischen (C1f), Wald und Wasser (G1–G4)
- [x] Bevölkerungslimit, Ausbildung und Häuser (C2), Traglast bleibt erhalten (C1t)
- [x] Bauen mit Bauzeit, Bauarbeitern, Lagern und eigener Grafik (C5)
- [x] Weiterbauen an liegengebliebenen Baustellen (C5d), Linksklick-Auswahl (C8b),
  Werkzeug `tools/spielablauf`
- [x] **C5f Farm** — Getreidefeld (Taste G, 3×3, 175 Nahrung/Kachel, Regrow ~100 s),
  neue `farm`-Gruppe in `tools/spielablauf` als Abnahme *(2026-10-03)*

---

## Notizen

- **Lokale Agents:** `tools/ollama-agent/` verteilt Punkte von hier an ein Ollama-Modell
  (`qwen3.8:27b`, 256K Kontext) und nimmt sie per Build, Test und Prüfskript ab
  (`py run_tasks.py --list`). Die IDs sind dieselben. Sinnvoll für `[L]` und mit
  Sichtung für `[R]`; `[X]` gehört nicht dorthin.
- Priorisierung folgt `docs/AgeOfEmpires.md`, Kap. „Relevanz für dieses Projekt" (Z. 672–715)
- **Bekannter Engpass laut Spec:** es existiert *kein einziges AoE-taugliches Grafik-Asset*.
  `RTSGameplayScreen` zeichnet prozedural erzeugte 32×32-Pixelart statt echter Grafiken. Alle Punkte außer C6/C7 und der
  isometrischen Darstellung lassen sich damit trotzdem vollständig umsetzen und testen.
- **Bewusst nicht nötig** fürs Grundgerüst: Zivilisationsboni, Reliquien, Mönche, Handel,
  Seekampf, Formationen, Kampagnen.
