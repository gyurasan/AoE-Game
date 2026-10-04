# Projektstruktur

Stand: 2026-10-03. Ziel: Retro-Echtzeitstrategie im Stil von Age of Empires I. C# auf MonoGame, .NET 10. Zielplattformen PC und Mac (Windows, macOS, Linux über DesktopGL); Android und iOS werden nicht unterstützt und wurden am 2026-09-23 entfernt.

## Ordnerstruktur

```
AgeOfEmpire.slnx - Root-Solution mit allen sechs Projekten
README.md, TODO.md, .gitignore
src/AoE.Core/ - net10.0, MonoGame-unabhängige Spiellogik:
    Combat/DamageCalculator.cs (233 Zeilen)
    Economy/VillagerLogic.cs (331) - enthält ResourcePool
    Economy/GatherJob.cs (264) - Sammelauftrag: Dorfbewohner-Kreislauf als Zustandsautomat
    Economy/Training.cs (154) - Bevölkerungsgrenze und Ausbildungs-Warteschlange
    Economy/Construction.cs (107) - Bauregeln und Baustelle mit abnehmendem Ertrag
    Economy/Ages.cs (176) - Zeitalter: Kosten, Dauer, Freischaltung und der Aufstieg
    Entities/Buildings.cs (466), UnitEntity.cs (238), UnitTypes.cs (379), UnitClass.cs (57), Resource.cs (32)
    Map/VisibilitySystem.cs (412) - enthält AUCH die Klasse MapGrid; es gibt keine eigene MapGrid.cs
    Pathfinding/Pathfinding.cs (316) - A-Stern und Formationsbewegung
AgeOfEvolutions/ - das MonoGame-Spiel:
    AgeOfEvolutions.Core/ (net10.0) mit Data/, Screens/, Effects/, Inputs/, ScreenManagers/, Localization/, Settings/, Content/
        Content/ - Backgrounds/menu.png, Icons/, Gebaeude/, Einheiten/, Werkzeuge/, Boden/, Felder/, Baeume/, Rohstoffe/, Tiere/ (aus tools/bilder), Fonts/Hud und Fonts/Menu
    AgeOfEvolutions.DesktopGL/ (net10.0) - Windows, macOS, Linux; RuntimeIdentifiers win-x64, osx-x64, osx-arm64, linux-x64
    AgeOfEvolutions.WindowsDX/ (net10.0-windows) - nur Windows
tests/AoE.Tests/ - net10.0, xUnit, 157 Tests, alle grün
    UnitTests.cs (15), CounterTriangleTests.cs (5), PathfindingTests.cs (3), FogOfWarTests.cs (6), GatherJobTests.cs (21), TrainingTests.cs (22), ConstructionTests.cs (39), AgeTests.cs (46)
demo/DemoApp.csproj - net10.0, kleine Konsolen-Testapp
tools/ollama-agent/ - Harness, der TODO-Punkte an ein lokales Ollama-Modell verteilt
tools/kartenpruefung/ - prüft Regeln des Kartengenerators über 50 erzeugte Karten
tools/bilder/ - erzeugt Spielgrafiken mit Qwen-Image über ComfyUI; Prompts und Seeds in bilder.json, uebernehmen.ps1 bereitet die gewählten Bilder für Content/ auf
tools/spielablauf/ - lässt die Spielschleife ohne Grafik laufen und prüft Abläufe (Bauen, Weiterbauen, Linksklick, Farm, Schafe, Bewegen, Minimap, Zoom, Leiste, Zeitalter, Wachturm, Hauptmenü, Animation, Fenster, Werkzeug, Feld)
```

## Wichtige Dateien

| Datei | Pfad | Beschreibung |
|---|---|---|
| DamageCalculator.cs | src/AoE.Core/Combat/DamageCalculator.cs | 233 Zeilen |
| VillagerLogic.cs | src/AoE.Core/Economy/VillagerLogic.cs | 331 Zeilen, enthält ResourcePool |
| Training.cs | src/AoE.Core/Economy/Training.cs | 154 Zeilen, `Population` (Grenze aus den Gebäuden, höchstens 200) und `TrainingQueue<T>` (Ausbildung, steht bei voller Bevölkerung still) |
| Construction.cs | src/AoE.Core/Economy/Construction.cs | 107 Zeilen, `BuildingRules` (Kosten, Bauzeit, Größe, abnehmender Ertrag) und `Construction` (Baustelle) |
| Ages.cs | src/AoE.Core/Economy/Ages.cs | 176 Zeilen, `Age`, `AgeRules` (Kosten, Dauer, ab welchem Zeitalter ein Gebäude baubar ist) und `AgeProgress` (der Aufstieg eines Spielers) |
| GatherJob.cs | src/AoE.Core/Economy/GatherJob.cs | 264 Zeilen, Sammelauftrag: hinlaufen, sammeln bis Traglast 10, abliefern, zurück; Schnittstelle IGatherWorld |
| Buildings.cs | src/AoE.Core/Entities/Buildings.cs | 466 Zeilen, Gebäudetypen und Fabriken; Holzfäller- und Bergbaulager seit C5, Wachturm seit C4c |
| UnitEntity.cs | src/AoE.Core/Entities/UnitEntity.cs | 238 Zeilen |
| UnitTypes.cs | src/AoE.Core/Entities/UnitTypes.cs | 379 Zeilen |
| UnitClass.cs | src/AoE.Core/Entities/UnitClass.cs | 57 Zeilen |
| Resource.cs | src/AoE.Core/Entities/Resource.cs | 32 Zeilen |
| VisibilitySystem.cs | src/AoE.Core/Map/VisibilitySystem.cs | 412 Zeilen, enthält AUCH MapGrid; Gebäude spenden Sicht wie Einheiten |
| Pathfinding.cs | src/AoE.Core/Pathfinding/Pathfinding.cs | 316 Zeilen, A-Stern und Formationsbewegung |
| TileMap.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMap.cs | 1147 Zeilen, Kartengenerierung mit Seen, Ressourcenklumpen, PvP-Startpositionen; Gebäude beliebiger Kantenlänge und `CanPlaceBuilding` |
| TileMapGatherWorld.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/TileMapGatherWorld.cs | 144 Zeilen, setzt IGatherWorld auf die Kachelkarte um: Quellen, Abgabestellen am Gebäuderand, nur fertig gebaute |
| Unit.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/Unit.cs | 199 Zeilen, Spieleinheit; hält über `Unit.Core` eine `UnitEntity` aus AoE.Core und reicht Kampfwerte, Lebenspunkte und Zustand durch; hält den Sammelauftrag (`Job`) und die Baustelle (`BuildSite`) |
| CoreUnits.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreUnits.cs | 101 Zeilen, bildet alle 17 Einheitentypen des Spiels auf Klassen aus AoE.Core ab |
| CoreBuildings.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs | 33 Zeilen, bildet die Gebäudetypen des Spiels auf BuildingEntity aus AoE.Core ab |
| RTSGameplayScreen.cs | AgeOfEvolutions/AgeOfEvolutions.Core/Screens/RTSGameplayScreen.cs | 3912 Zeilen, prozedurale Texturen und Spielschleife; Ausbildung (Taste Q), Zeitalter (A), Baumenü (H, M, F, B, G, T), Baustellen, Minimap und die Klick-Entscheidung (`LeftClick`) |

## Grafik

Menübild, Tastensymbole, Gebäude, Dorfbewohner, Boden (Gras, Sand, Wasser), Bäume, Stein-
und Goldhaufen, die Felder sowie Schafe und Rehe kommen aus Qwen-Image
(Qwen-Image-2512, fp8) über ComfyUI unter `D:\Apps\ComfyUI`. `tools/bilder/bilder.json` hält
je Bild Prompt, Seed und Ziel; `qwen_image.py` erzeugt die Bilder und stellt sie mit
BiRefNet frei, `uebernehmen.ps1` schneidet zu, färbt Fahnen, Banner und Kittel für
Spieler 2 rot und macht Bodenbilder kachelbar. Die Ergebnisse liegen in
`AgeOfEvolutions.Core/Content/` und sind in `AgeOfEvolutions.mgcb` eingetragen. Die
Bewegung der Dorfbewohner (Gehen, Stehen), den Schwung ihrer Werkzeuge und den Schritt
der Tiere rechnet das Spiel selbst (`RTSGameplayScreen.DrawVillager`, `DrawTool`, `DrawAnimal`). Fehlt eine Grafik, zeichnet das Spiel wie früher
prozedural. Bilder des Spiels liegen in `docs/bilder/`.

## Build-Status

| Projekt | Ziel | Status |
|---|---|---|
| AoE.Core | net10.0 | baut fehlerfrei |
| AoE.Tests | net10.0 | 157/157 grün |
| DemoApp | net10.0 | in Ordnung |
| AgeOfEvolutions.Core | net10.0 | fehlerfrei |
| DesktopGL | net10.0 | fehlerfrei, keine Warnungen |
| WindowsDX | net10.0-windows | fehlerfrei |

## Anbindung an AoE.Core

Seit Block B (2026-09-23) arbeitet das Spiel mit der getesteten Logik aus src/AoE.Core; die früheren Parallelimplementierungen sind gelöscht. Zur Laufzeit aktiv sind `ResourcePool` (Ressourcen der Spieler), `UnitEntity` (jede `Unit` hält über `Unit.Core` eine, erzeugt von `CoreUnits.Create`), `UnitState`, die A-Stern-Wegfindung (`TileMap.FindPath` ruft `AoE.Core.Pathfinding`), seit C1 der Sammelauftrag `GatherJob` (im Spiel über `TileMapGatherWorld`), seit C7n der Nebel des Krieges (`VisibilitySystem` über `TileMap.UpdateFogOfWarForPlayer`) seit B6 die Gebäude (`BuildingEntity` über `Building.Core`, erzeugt von `CoreBuildings`) und seit C2 die Bevölkerungsgrenze (`Population`) und die Ausbildung (`TrainingQueue` über `Building.Training`), seit C5 das Bauen (`BuildingRules` und `Construction` über `Building.Construction`), seit C4 die Zeitalter (`AgeProgress` über `Player.Ages`, Regeln in `AgeRules`).

Angebunden, aber nie aufgerufen:

- **Kampf:** `Unit.Attack()` rechnet über `DamageCalculator`, wird aber nirgends aufgerufen – es gibt keine Kampfschleife, der `DamageCalculator` läuft nur in den Tests.

---

Offene Punkte: [TODO.md](../TODO.md)
Spielspezifikation: [AgeOfEmpires.md](AgeOfEmpires.md)
