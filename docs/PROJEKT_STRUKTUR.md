# Projektstruktur

Stand: 2026-10-09. Ziel: Retro-Echtzeitstrategie im Stil von Age of Empires I. C# auf MonoGame, .NET 10. Zielplattformen PC und Mac (Windows, macOS, Linux über DesktopGL); Android und iOS werden nicht unterstützt und wurden am 2026-09-23 entfernt.

## Ordnerstruktur

```
AgeOfEmpire.slnx - Root-Solution mit allen sechs Projekten
README.md, TODO.md, .gitignore
src/AoE.Core/ - net10.0, MonoGame-unabhängige Spiellogik:
    Combat/DamageCalculator.cs (233 Zeilen)
    Combat/BuildingCombat.cs (55) - Schaden an Gebäuden: ein Schlag alle 2 s, bei 0 zerstört
    Economy/VillagerLogic.cs (331) - enthält ResourcePool
    Economy/GatherJob.cs (264) - Sammelauftrag: Dorfbewohner-Kreislauf als Zustandsautomat
    Economy/Training.cs (154) - Bevölkerungsgrenze und Ausbildungs-Warteschlange
    Economy/Construction.cs (107) - Bauregeln und Baustelle mit abnehmendem Ertrag
    Economy/Ages.cs (176) - Zeitalter: Kosten, Dauer, Freischaltung und der Aufstieg
    Economy/Research.cs (443) - Forschungen: Regeln, Ablauf je Gebäude und Wirkung
    Entities/Buildings.cs (339), UnitEntity.cs (238), UnitTypes.cs (379), UnitClass.cs (57), Resource.cs (32)
    Map/VisibilitySystem.cs (412) - enthält AUCH die Klasse MapGrid; es gibt keine eigene MapGrid.cs
    Pathfinding/Pathfinding.cs (316) - A-Stern und Formationsbewegung
AgeOfEvolutions/ - das MonoGame-Spiel:
    AgeOfEvolutions.Core/ (net10.0) mit Data/, Screens/, Effects/, Inputs/, ScreenManagers/, Localization/, Settings/, Content/
        Content/ - Backgrounds/menu.png, Icons/, Gebaeude/ (je Zeitalter in dunkel/, feudal/, ritter/, imperial/), Einheiten/ (ab der Feudalzeit je Zeitalter in feudal/, ritter/, imperial/), Werkzeuge/, Boden/, Felder/, Baeume/, Rohstoffe/, Tiere/, Leiste/ (aus tools/bilder), Fonts/Hud und Fonts/Menu
    AgeOfEvolutions.DesktopGL/ (net10.0) - Windows, macOS, Linux; RuntimeIdentifiers win-x64, osx-x64, osx-arm64, linux-x64
    AgeOfEvolutions.WindowsDX/ (net10.0-windows) - nur Windows
tests/AoE.Tests/ - net10.0, xUnit, 267 Tests, alle grün
    UnitTests.cs (15), CounterTriangleTests.cs (5), PathfindingTests.cs (3), FogOfWarTests.cs (8), GatherJobTests.cs (21), TrainingTests.cs (22), ConstructionTests.cs (74), AgeTests.cs (46), ResearchTests.cs (43), BuildingCombatTests.cs (9), AI/EconomyAiTests.cs (20), DebugBuild2.cs (1)
demo/DemoApp.csproj - net10.0, kleine Konsolen-Testapp
tools/ollama-agent/ - Harness, der TODO-Punkte an ein lokales Ollama-Modell verteilt
tools/kartenpruefung/ - prüft Regeln des Kartengenerators über 50 erzeugte Karten
tools/bilder/ - erzeugt Spielgrafiken mit Qwen-Image über ComfyUI; Prompts und Seeds in bilder.json, uebernehmen.ps1 bereitet die gewählten Bilder für Content/ auf; gang.py stellt aus einem Standbild acht Gehphasen (Bein-Skelett, Gelenke in gang.json), schlag.py acht Schlagphasen (Waffenarm oder Speer über einem Rumpf ohne Waffe, schlag.json)
tools/spielablauf/ - lässt die Spielschleife ohne Grafik laufen und prüft Abläufe (Bauen, Weiterbauen, Linksklick, Farm, Schafe, Bewegen, Minimap, Zoom, Leiste, Zeitalter, Wachturm, Hauptmenü, Animation, Fenster, Werkzeug, Feld, Trampelpfad, Wind, Gang, Blick, Dunkel, Neubauten, Türen, Gebäudeauswahl, Angriff, Soldaten, Forschung, Schlag)
```

## Wichtige Dateien

| Datei | Pfad | Beschreibung |
|---|---|---|
| DamageCalculator.cs | src/AoE.Core/Combat/DamageCalculator.cs | 233 Zeilen |
| BuildingCombat.cs | src/AoE.Core/Combat/BuildingCombat.cs | 55 Zeilen, Schaden an Gebäuden (K1): ein Schlag alle 2 s, Nahkampf gegen die Rüstung, Pfeile prallen fast ganz ab |
| VillagerLogic.cs | src/AoE.Core/Economy/VillagerLogic.cs | 331 Zeilen, enthält ResourcePool |
| Training.cs | src/AoE.Core/Economy/Training.cs | 154 Zeilen, `Population` (Grenze aus den Gebäuden, höchstens 200) und `TrainingQueue<T>` (Ausbildung, steht bei voller Bevölkerung still) |
| Construction.cs | src/AoE.Core/Economy/Construction.cs | 131 Zeilen, `BuildingRules` (Kosten, Bauzeit und Größe jedes Gebäudetyps, abnehmender Ertrag) und `Construction` (Baustelle) |
| Ages.cs | src/AoE.Core/Economy/Ages.cs | 176 Zeilen, `Age`, `AgeRules` (Kosten, Dauer, ab welchem Zeitalter ein Gebäude baubar ist) und `AgeProgress` (der Aufstieg eines Spielers) |
| Research.cs | src/AoE.Core/Economy/Research.cs | 443 Zeilen, Forschungen (P2): `Tech`, `TechRules` (Kosten, Dauer, Gebäude, Zeitalter, Name, Wirkungstext nach AoE II), `TechProgress` (Forschungsstand eines Spielers), `ResearchSlot` (die Forschung in einem Gebäude, eine zur Zeit) und `TechEffects` (Wirkung auf Einheiten und Gebäude, Sammelfaktor, Feldvorrat) |
| GatherJob.cs | src/AoE.Core/Economy/GatherJob.cs | 264 Zeilen, Sammelauftrag: hinlaufen, sammeln bis Traglast 10, abliefern, zurück; Schnittstelle IGatherWorld |
| Buildings.cs | src/AoE.Core/Entities/Buildings.cs | 339 Zeilen, Gebäudetypen und Fabriken; Holzfäller- und Bergbaulager seit C5, Wachturm seit C4c; Baustellen ohne Sicht seit C7b; `Create` für jeden Typ mit Werten nach AoE II seit L1; der alte, nie benutzte `TechTree` ist seit P2 durch Research.cs ersetzt |
| UnitEntity.cs | src/AoE.Core/Entities/UnitEntity.cs | 238 Zeilen |
| UnitTypes.cs | src/AoE.Core/Entities/UnitTypes.cs | 379 Zeilen |
| UnitClass.cs | src/AoE.Core/Entities/UnitClass.cs | 57 Zeilen |
| Resource.cs | src/AoE.Core/Entities/Resource.cs | 32 Zeilen |
| VisibilitySystem.cs | src/AoE.Core/Map/VisibilitySystem.cs | 413 Zeilen, enthält AUCH MapGrid; Gebäude spenden Sicht wie Einheiten, Baustellen noch nicht |
| Pathfinding.cs | src/AoE.Core/Pathfinding/Pathfinding.cs | 316 Zeilen, A-Stern und Formationsbewegung |
| TileMap.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs | 1443 Zeilen, Kartengenerierung mit Seen (nicht in den Startzonen), Ressourcenklumpen, PvP-Startpositionen; Gebäude beliebiger Kantenlänge und `CanPlaceBuilding`; Trampelpfade (`Trample`, `RegrowGrass`); gerade Wege über freies Land (`IsSegmentWalkable`); Laufbefehle nach dem Wissen des Spielers (`FindPathKnown`); zerstörte Gebäude räumen (`RemoveBuilding`), Einheiten jedes Typs absetzen (`AddUnit`); Felder mit eigenem Vorrat (`PlantCrop`, mit Pferdekummet 250) |
| TileMapGatherWorld.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMapGatherWorld.cs | 144 Zeilen, setzt IGatherWorld auf die Kachelkarte um: Quellen, Abgabestellen am Gebäuderand, nur fertig gebaute; `Accepts` sagt, welches Gebäude was annimmt |
| Unit.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/Unit.cs | 213 Zeilen, Spieleinheit; hält über `Unit.Core` eine `UnitEntity` aus AoE.Core und reicht Kampfwerte, Lebenspunkte und Zustand durch; hält den Sammelauftrag (`Job`) und die Baustelle (`BuildSite`), seit K2 das angegriffene Gebäude (`AttackTarget`) |
| CoreUnits.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreUnits.cs | 105 Zeilen, bildet alle 19 Einheitentypen des Spiels auf Klassen aus AoE.Core ab |
| CoreBuildings.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs | 50 Zeilen, bildet alle Gebäude des Baumenüs auf BuildingEntity aus AoE.Core ab |
| RTSGameplayScreen.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs | 6059 Zeilen, prozedurale Texturen und Spielschleife; Gras, Sand und Wasser aus dem Bodenshader (`DrawGroundShaded`), Weizen (`DrawWheat`) und Bäume (`DrawTree`, Wind.cs) im Wind; Bäume, Tiere, Gebäude und Figuren nach ihrer Sohle sortiert in einer Tiefenschicht (`DrawUnits`); Leisten im Stoff des Zeitalters (`DrawPanel`); Gehen nach Strecke mit Anfahren und Abbremsen (Gait.cs); Gebäude- und Dorfbewohnerbilder je Zeitalter des Besitzers; Gebäude auswählen mit Rahmen, Lebensbalken und Status, Ausbildung im ausgewählten Gebäude nach `Products` (Taste Q: Dorfbewohner, Miliz, Bogenschütze, Späher), Forschungen im ausgewählten Gebäude nach `Researches` (`Research`, `UpdateResearch`), Angriff auf fremde Gebäude (`AttackBuilding`), Zeitalter (A), Baumenü in zwei Tastenreihen (H, M, F, B, G, T; darunter die zweite Reihe K, P, S, L, E, R, W, Z, X, U, O, C, N), Baustellen, Minimap und die Klick-Entscheidung (`LeftClick`) |

## Grafik

Menübild, Tastensymbole, Leisten, Gebäude, Dorfbewohner, Boden (Gras, Sand, Wasser), Bäume, Stein-
und Goldhaufen, die Felder sowie Schafe und Rehe kommen aus Qwen-Image
(Qwen-Image-2512, fp8) über ComfyUI unter `D:\Apps\ComfyUI`. `tools/bilder/bilder.json` hält
je Bild Prompt, Seed und Ziel; `qwen_image.py` erzeugt die Bilder und stellt sie mit
BiRefNet frei, `uebernehmen.ps1` schneidet zu, färbt Fahnen, Banner und Kittel für
Spieler 2 rot, macht Bodenbilder kachelbar (großflächig ausgeglichen) und weiße Lücken in
Baumkronen durchsichtig. Die Ergebnisse liegen in
`AgeOfEvolutions.Core/Content/` und sind in `AgeOfEvolutions.mgcb` eingetragen. Gehen
Dorfbewohner und Soldaten, zeigt das Spiel acht Gehphasen je Doppelschritt (`lauf1` bis
`lauf8`, nach der gelaufenen Strecke, `Gait.WalkFrame`): `tools/bilder/gang.py` schneidet die
Beine des Standbilds in Oberschenkel, Unterschenkel und Fuß (beim Pferd des Spähers vier
Beine im Viertakt) und stellt sie nach Fußbahnen - der Standfuß ruht, rollt über Ferse und
Zehen ab, der Körper wippt mit; Rumpf, Kleidung und Waffe bleiben Pixel für Pixel. Die
Schrittlänge `Gait.VILLAGER_STRIDE` (5,06 Welteinheiten) ist die der Bilder, so rutschen die
Füße nicht; `docs/bilder/gang.gif` zeigt alle Figuren in Zeitlupe. Greifen Miliz und Späher ein
Gebäude an, zeigt das Spiel acht Schlagphasen aus `tools/bilder/schlag.py` (`AttackPhase` nach
`AttackTimer`, der Treffer im Bild fällt auf den Abzug der Stärke), auf größerer Leinwand im
Maßstab des Standbilds; Zeitlupe in `docs/bilder/schlag.gif`. Den Schwung der Werkzeuge
und den Schritt der Tiere rechnet das Spiel selbst (`RTSGameplayScreen.DrawVillager`,
`DrawTool`, `DrawAnimal`). Fehlt eine Grafik, zeichnet das Spiel wie früher
prozedural. Gras, Sand und Wasser setzt der Bodenshader `Content/Effects/Boden.fx` in einem
Durchgang aus Gras- und Sandbildern zusammen: weiche Ufer und Strände, Wassertiefe, Wellen,
Schaum, Fischschwärme und Trampelpfade. Das Gras ist eine Wiese aus vier Sorten
(`Boden/gras`, `gras_trocken`, `gras_dunkel`, `gras_blumen`, Gruppe `gras`): je Bildpunkt
gewinnt die Sorte mit dem größten Gewicht plus Halmhöhe (`Meadow`, `GrassHeight`), so
schieben sich an den Grenzen die Halme ineinander; Blumen wachsen nur in Flecken, am Wald
das dunkle Gras mit Klee. Die Grasbilder liegen feiner als Sand und Wasser (`GRASS_TEXELS`,
Shaderparameter `GrassScale`), damit die Halme neben Bäumen und Figuren klein bleiben. Im Wind wiegen sich der Weizen (`Content/Effects/Weizen.fx`)
und die Bäume (`Screens/Wind.cs`), in denselben Böen. Bilder des Spiels liegen in `docs/bilder/`.

Das Programmsymbol (Dorfbewohner vor dem Wappenschild, Gruppe `spielicon`) schreibt
`uebernehmen.ps1` als `Content/Icon.ico` mit 16 bis 256 px, das beide Exe-Projekte als
`ApplicationIcon` einbinden, und als `Content/Icon.bmp` für das Fenster: MonoGame lädt
das Fenstersymbol aus der eingebetteten Ressource `Icon.bmp`, ohne sie zeigt es sein
eigenes Logo.

## Build-Status

| Projekt | Ziel | Status |
|---|---|---|
| AoE.Core | net10.0 | baut fehlerfrei |
| AoE.Tests | net10.0 | 267/267 grün |
| DemoApp | net10.0 | in Ordnung |
| AgeOfEvolutions.Core | net10.0 | fehlerfrei |
| DesktopGL | net10.0 | fehlerfrei, keine Warnungen |
| WindowsDX | net10.0-windows | fehlerfrei |

## Anbindung an AoE.Core

Seit Block B (2026-09-23) arbeitet das Spiel mit der getesteten Logik aus src/AoE.Core; die früheren Parallelimplementierungen sind gelöscht. Zur Laufzeit aktiv sind `ResourcePool` (Ressourcen der Spieler), `UnitEntity` (jede `Unit` hält über `Unit.Core` eine, erzeugt von `CoreUnits.Create`), `UnitState`, die A-Stern-Wegfindung (`TileMap.FindPath` ruft `AoE.Core.Pathfinding`), seit C1 der Sammelauftrag `GatherJob` (im Spiel über `TileMapGatherWorld`), seit C7n der Nebel des Krieges (`VisibilitySystem` über `TileMap.UpdateFogOfWarForPlayer`) seit B6 die Gebäude (`BuildingEntity` über `Building.Core`, erzeugt von `CoreBuildings`) und seit C2 die Bevölkerungsgrenze (`Population`) und die Ausbildung (`TrainingQueue` über `Building.Training`), seit C5 das Bauen (`BuildingRules` und `Construction` über `Building.Construction`), seit C4 die Zeitalter (`AgeProgress` über `Player.Ages`, Regeln in `AgeRules`), seit K1 der Schaden an Gebäuden (`BuildingCombat`), seit P2 die Forschungen (`TechProgress` über `Player.Techs`, `ResearchSlot` über `Building.Research`, Wirkung über `TechEffects`).

Angebunden, aber nie aufgerufen:

- **Kampf:** `Unit.Attack()` rechnet über `DamageCalculator`, wird aber nirgends aufgerufen – Einheiten kämpfen noch nicht gegeneinander (Gebäude greifen sie seit K2 über `BuildingCombat` an), der `DamageCalculator` läuft nur in den Tests.

---

Offene Punkte: [TODO.md](../TODO.md)
Spielspezifikation: [AgeOfEmpires.md](AgeOfEmpires.md)
