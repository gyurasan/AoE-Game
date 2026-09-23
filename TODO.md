# AoE-Clone — TODO / Meilensteine

**Stand:** 2026-09-23 · verifiziert durch Build + Testlauf, nicht aus Doku übernommen.

## Wiederaufnahme — hier weitermachen

Alles baut und läuft: `dotnet build AgeOfEmpire.slnx` (0 Fehler, 0 Warnungen),
`dotnet test tests/AoE.Tests` → **26/26 grün**.

**Nichts ist committet.** Der gesamte Stand liegt im Arbeitsbaum; Commits macht
der Nutzer selbst.

Zuletzt fertiggestellt:

- **Block A** (Hygiene) bis auf den `git rm -r --cached`-Schritt, siehe H1
- **Block B** vollständig — das Spiel nutzt jetzt durchgehend `AoE.Core`
- **C3** endliche Ressourcen, **C10** Mausrad-Zoom und Kameraklemmung
- **Block E** — vier Fehler, die erst beim Ausprobieren sichtbar wurden

Die Screenshots in `docs/Screenshot 2026-09-23 *.png` zeigen den Stand davor
und haben drei der vier Fehler aufgedeckt. Sie sind als Referenz nützlich.

Nächste sinnvolle Schritte, in dieser Reihenfolge:

1. **C1 Dorfbewohner-Loop** — der Punkt mit der größten Wirkung aufs Spielgefühl.
   Vorarbeit liegt: C3 erschöpft Quellen bereits, aber der Dorfbewohner bleibt
   dann auf der leeren Kachel stehen, statt sich die nächste zu suchen.
2. **Kampfschleife** — `Unit.Attack()` existiert seit B2 und wird nirgends
   aufgerufen. Ohne Aufrufer bleibt die Schadensformel ungenutzt.
3. **Nebel zeichnen (C7)** — die Sichtlogik läuft seit B5, die Darstellung fehlt.
4. **B6 Gebäude** auf `AoE.Core.Entities.Buildings` umstellen.

Werkzeuge: `tools/ollama-agent/` verteilt `[L]`- und `[R]`-Punkte an ein lokales
Ollama-Modell und nimmt sie per Build + Test ab (`py run_tasks.py --list`).
`[X]`-Punkte gehören nicht dorthin.

## Verifizierter Ist-Zustand

| Projekt | Framework | Build | Bemerkung |
|---|---|---|---|
| `src/AoE.Core` | net10.0 | OK, 0 Fehler | Reine Logik, keine MonoGame-Abhängigkeit |
| `tests/AoE.Tests` | net10.0 | **15/15 grün** | Villager, Scout, Konter-Boni, Schadensformel, Ressourcen |
| `demo/DemoApp` | net10.0 | OK | referenziert AoE.Core |
| `AgeOfEmpiresClone.Core` | net10.0 | OK, 0 Fehler | Dateiname seit H2 kanonisch |
| `AgeOfEmpiresClone.DesktopGL` | net10.0 | OK, 0 Fehler / 0 Warnungen | startbar |
| `AgeOfEmpiresClone.WindowsDX` | net10.0-windows | — | nur Windows |

**Die .NET-10-Migration und der Build sind erledigt.** Der eigentliche Engpass ist ein
anderer, siehe Block B.

## Zielplattformen

**PC und Mac.** `DesktopGL` ist der plattformübergreifende Pfad (Windows, macOS, Linux) und
deklariert `win-x64;osx-x64;osx-arm64;linux-x64` — `osx-arm64` ist für Apple Silicon zwingend.
`WindowsDX` bleibt als zusätzliche reine Windows-Variante.

**Mobile ist kein Ziel.** Die Android- und iOS-Projekte der MonoGame-Vorlage wurden am
2026-09-23 gelöscht; das deckt sich mit `game_plan.md` („Windows, macOS, Linux only — no
mobile"). Die 84 Dateien sind im Arbeitsbaum entfernt, aber noch nicht aus dem Git-Index.

## Eignung für Delegation

- **[L] lokal delegierbar** — mechanisch, eng umrissen, durch Build/Test abnehmbar
- **[R] lokal mit Review** — eine Datei, klares Ziel, Ergebnis muss gesichtet werden
- **[X] nicht delegieren** — schneidet quer durch die Architektur, braucht Entwurfsentscheidungen

**Abnahmekriterium für jeden Punkt**, sofern nicht anders genannt:

```
dotnet build AgeOfEmpiresClone/AgeOfEmpiresClone.DesktopGL/AgeOfEmpiresClone.DesktopGL.csproj
dotnet test  tests/AoE.Tests/AoE.Tests.csproj
```

beide fehlerfrei.

---

# Block A — Hygiene & Build (blockiert alles andere)

### H1 · `.gitignore` anlegen, Build-Artefakte aus dem Index nehmen [L]

Es gibt **keine `.gitignore`**. `bin/`, `obj/`, `.vs/` und `Content/obj/` sind eingecheckt —
daher rund 200 Zeilen Rauschen in `git status` (`.suo`, `.vsidx`, `.dll`, `.pdb`, `.dtbcache`).

- [x] `.gitignore` für .NET + MonoGame + Visual Studio anlegen *(Agent H1)*
- [ ] `git rm -r --cached` für `bin/`, `obj/`, `.vs/`, `Content/obj/`, `Content/bin/`
      — **bewusst nicht automatisiert.** Das stellt mehrere tausend Löschungen in den
      Index; dieser Schritt gehört dir, nicht einem Agent:
      `git rm -r --cached AgeOfEmpiresClone/*/bin AgeOfEmpiresClone/*/obj AgeOfEmpiresClone/.vs`
- [ ] Abnahme: `git status --short` zeigt nur noch echte Quelldateien

### H2 · csproj-Dateinamen begradigen [L]

Die Datei hieß auf der Platte `AgeofempiresClone.Core.csproj`, während Git und alle
verweisenden Projekte die kanonische Schreibweise nutzten. Unter Windows fiel das nicht auf —
**auf Linux und macOS hätte es den Build gebrochen**, und genau die sind jetzt Zielplattform.

- [x] Auf die kanonische Schreibweise `AgeOfEmpiresClone.Core.csproj` umbenannt *(Agent H2)*

### H3 · Doppelten `ProjectReference` entfernen [L]

`AgeOfEmpiresClone.Core.csproj` verwies **zweimal** auf AoE.Core — zwei ItemGroups,
einmal mit `/`, einmal mit `\`.

- [x] Eine der beiden ItemGroups löschen *(Agent H3 — Build grün)*

### H4 · Solution vervollständigen [R]

Die alte `AgeOfEmpiresClone.sln` enthielt nur Core + DesktopGL; `src/`, `tests/` und `demo/`
lagen in gar keiner Solution.

- [x] Root-Solution `AgeOfEmpire.slnx` mit allen sechs Projekten *(Agent H4)* — XML-Format
      von .NET 10, keine GUIDs; `dotnet restore AgeOfEmpire.slnx` läuft durch

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
- [ ] Offen: auf echter macOS-Hardware starten — bisher nur Restore geprüft, nicht ausgeführt

---

# Block E — Fehler aus dem Spielbetrieb

Gefunden am 2026-09-23 beim Ausprobieren des laufenden Spiels, nicht durch Tests.
Alle vier behoben.

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
- [ ] Offen: `RTSGameplayScreen` zeichnet den Nebel noch nicht — die Logik läuft,
      die Darstellung fehlt. Gehört zu C7.

---

# Block C — Gameplay (Spec-Reihenfolge, `docs/AgeOfEmpires.md` Kap. „Relevanz")

Die Spec priorisiert nach Spielgefühl, nicht nach Aufwand. C1 ist der Punkt, ohne den sich
nichts nach AoE anfühlt.

### B6 · Gebäude auf `AoE.Core.Entities.Buildings` umstellen [X] — neu

Beim Aufräumen von Block B übrig geblieben: die `Building`-Klasse in
`TileMap.cs:427` führt eigene Werte (`MaxHealth = 1000`), während `AoE.Core`
mit `Buildings.cs` (419 Zeilen) und `BuildingEntity` eine eigene Fassung hat.
Dasselbe Muster wie B1–B5, nur für Gebäude.

- [ ] `Building` auf `BuildingEntity` umstellen, analog zu `Unit.Core`
- [ ] `MapGrid.AddBuilding` nutzen, damit Gebäude Sicht und Wegfindung beeinflussen

---

# Block C — Gameplay

### C1 · Dorfbewohner-Loop [X] — höchste Priorität

laufen → sammeln → Traglast 10 → Abgabestelle → zurück → **automatisch fortsetzen**

- [ ] Automatische Ressourcensuche und -sammlung
- [ ] Abgabe an nächstgelegene Stelle (Stadtzentrum, Mühle, Lager)
- [ ] Automatischer Neustart derselben Ressource nach Abgabe
- [ ] Leerlauf-Erkennung: wann ist ein Dorfbewohner idle?

### C2 · Bevölkerungslimit [R]

- [ ] Gemeinsame Obergrenze (default 200) für Wirtschaft **und** Armee
- [ ] Haus = +5 Plätze, Kosten 25 Holz
- [ ] Bei vollem Limit Produktion stoppen — mit sichtbarem UI-Hinweis

### C3 · Endliche Vorkommen [L]

Laut Spec Punkt 6: ohne das hat eine Partie kein natürliches Ende.

- [x] Abbau reduziert den Vorrat (`tile.ResourceAmount--`) statt zu regenerieren *(Agent C3)*
- [x] Erschöpfte Quelle verschwindet bzw. wird zu Normalgelände *(Agent C3)*
- [ ] Folge davon: ein Dorfbewohner bleibt auf der erschöpften Kachel stehen, statt
      sich die nächste Quelle zu suchen — das schließt erst C1.

### C4 · Zeitalter als Gate [X]

- [ ] Stadtzentrum: Upgrade nach Feudal-/Ritter-/Imperialzeit
- [ ] Kosten 500N · 800N+200G · 1000N+800G, je rund 2 Minuten
- [ ] Jedes Zeitalter schaltet Gebäude und Einheiten frei

### C5 · Bauen [X]

- [ ] Bauzeiten je Gebäude
- [ ] Bauplatz-Logik: Kollision, freie Fläche
- [ ] Mehrere Arbeiter mit abnehmendem Ertrag

### C6 · HUD dreiteilig [R]

- [ ] Ressourcenleiste oben: `[Holz] [Nahrung] [Gold] [Stein] [Bev. 37/45] « Feudalzeit »`
- [ ] Bevölkerung rot bei erreichtem Limit
- [ ] Kommandoleiste unten: Aktionssymbole – Einheiteninfo – Minimap
- [ ] Minimap: Geländefarben, eigene grün / Feinde rot, Kameraausschnitt als Rechteck

### C7 · Terrain-Rendering vervollständigen [R]

- [ ] Bäume als prozedurale Pixel-Art
- [ ] Beeren, Stein, Goldminen als Textur statt Farbfläche
- [ ] Schatten und Beleuchtung

### C10 · Kamera [R]

- [x] Zoomen mit dem Mausrad, **auf den Cursor zu** statt auf die Bildmitte
      (RTSGameplayScreen, multiplikativer Schritt 1,12; Grenzen MIN_ZOOM 0,5 / MAX_ZOOM 2,0)
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
- [ ] Kantenscrollen mit der Maus

### C8 · Auswahl & Steuerung [R]

- [ ] Selektion zeigt HP/Status
- [ ] Rechtsklick → Bewegung oder Sammeln
- [ ] Lasso-Auswahl per Mauszug

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
- [x] `AgeOfEmpiresClone/game_plan.md`: Hinweis auf die Entfernung der Mobile-Projekte *(Agent D4)*
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
- [x] AoE.Core als Projektverweis eingebunden *(noch ungenutzt → Block B)*
- [x] 15 Unit-Tests grün
- [x] Kartengenerierung `TileMap.cs`: Seen mit Falloff, Beeren, Schafe, Wildschweine,
      Stein/Gold, PvP-Startpositionen
- [x] Wasser-Animation
- [x] Menü führt zu `RTSGameplayScreen`
- [x] Platformer-Reste des MonoGame-Beispiels entfernt
- [x] Root-Solution `AgeOfEmpire.slnx`, `.gitignore`, `README.md`
- [x] Zielplattformen auf PC + Mac festgelegt, Mobile-Projekte entfernt
- [x] Endliche Ressourcenvorkommen (C3)

---

## Notizen

- **Lokale Agents:** `tools/ollama-agent/` verteilt Punkte von hier an ein Ollama-Modell
  und nimmt sie per Build + Test ab (`py run_tasks.py --list`). Die IDs sind dieselben.
  Sinnvoll für `[L]` und mit Sichtung für `[R]`; `[X]` gehört nicht dorthin.
- Priorisierung folgt `docs/AgeOfEmpires.md`, Kap. „Relevanz für dieses Projekt" (Z. 672–715)
- **Bekannter Engpass laut Spec:** es existiert *kein einziges AoE-taugliches Grafik-Asset*.
  `RTSGameplayScreen` zeichnet farbige 32×32-Quadrate. Alle Punkte außer C6/C7 und der
  isometrischen Darstellung lassen sich damit trotzdem vollständig umsetzen und testen.
- **Bewusst nicht nötig** fürs Grundgerüst: Zivilisationsboni, Reliquien, Mönche, Handel,
  Seekampf, Formationen, Kampagnen.
