# Projektstruktur

Stand: 2026-09-23. Ziel: Retro-Echtzeitstrategie im Stil von Age of Empires I. C# auf MonoGame, .NET 10. Zielplattformen PC und Mac (Windows, macOS, Linux über DesktopGL); Android und iOS werden nicht unterstützt und wurden am 2026-09-23 entfernt.

## Ordnerstruktur

```
AgeOfEmpire.slnx - Root-Solution mit allen sechs Projekten
README.md, TODO.md, .gitignore
src/AoE.Core/ - net10.0, MonoGame-unabhängige Spiellogik:
    Combat/DamageCalculator.cs (233 Zeilen)
    Economy/VillagerLogic.cs (331) - enthält ResourcePool
    Entities/Buildings.cs (419), UnitEntity.cs (227), UnitTypes.cs (378), UnitClass.cs (57), Resource.cs (32)
    Map/VisibilitySystem.cs (376) - enthält AUCH die Klasse MapGrid ab Zeile 241; es gibt keine eigene MapGrid.cs
    Pathfinding/Pathfinding.cs (316) - A-Stern und Formationsbewegung
AgeOfEmpiresClone/ - das MonoGame-Spiel:
    AgeOfEmpiresClone.Core/ (net10.0) mit Data/, Screens/, Effects/, Inputs/, ScreenManagers/, Localization/, Settings/, Content/
    AgeOfEmpiresClone.DesktopGL/ (net10.0) - Windows, macOS, Linux; RuntimeIdentifiers win-x64, osx-x64, osx-arm64, linux-x64
    AgeOfEmpiresClone.WindowsDX/ (net10.0-windows) - nur Windows
tests/AoE.Tests/ - net10.0, xUnit, 15 Tests, alle grün
demo/DemoApp.csproj - net10.0, kleine Konsolen-Testapp
tools/ollama-agent/ - Harness, der TODO-Punkte an ein lokales Ollama-Modell verteilt
```

## Wichtige Dateien

| Datei | Pfad | Beschreibung |
|---|---|---|
| DamageCalculator.cs | src/AoE.Core/Combat/DamageCalculator.cs | 233 Zeilen |
| VillagerLogic.cs | src/AoE.Core/Economy/VillagerLogic.cs | 331 Zeilen, enthält ResourcePool |
| Buildings.cs | src/AoE.Core/Entities/Buildings.cs | 419 Zeilen |
| UnitEntity.cs | src/AoE.Core/Entities/UnitEntity.cs | 227 Zeilen |
| UnitTypes.cs | src/AoE.Core/Entities/UnitTypes.cs | 378 Zeilen |
| UnitClass.cs | src/AoE.Core/Entities/UnitClass.cs | 57 Zeilen |
| Resource.cs | src/AoE.Core/Entities/Resource.cs | 32 Zeilen |
| VisibilitySystem.cs | src/AoE.Core/Map/VisibilitySystem.cs | 376 Zeilen, enthält AUCH MapGrid ab Zeile 241 |
| Pathfinding.cs | src/AoE.Core/Pathfinding/Pathfinding.cs | 316 Zeilen, A-Stern und Formationsbewegung |
| TileMap.cs | AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Data/TileMap.cs | 461 Zeilen, Kartengenerierung mit Seen, Ressourcenklumpen, PvP-Startpositionen |
| RTSGameplayScreen.cs | AgeOfEmpiresClone/AgeOfEmpiresClone.Core/Screens/RTSGameplayScreen.cs | 1233 Zeilen, prozedurale Texturen und Spielschleife |

## Build-Status

| Projekt | Ziel | Status |
|---|---|---|
| AoE.Core | net10.0 | baut fehlerfrei |
| AoE.Tests | net10.0 | 15/15 grün |
| DemoApp | net10.0 | in Ordnung |
| AgeOfEmpiresClone.Core | net10.0 | fehlerfrei |
| DesktopGL | net10.0 | fehlerfrei, keine Warnungen |
| WindowsDX | net10.0-windows | fehlerfrei |

## Bekannter Engpass

Das Spielprojekt enthält kein einziges `using AoE.Core`. Die getestete Logik in src/AoE.Core wird im Spiel nicht verwendet; es existieren zwei parallele Implementierungen (UnitEntity gegen Unit, ResourcePool gegen Resource-struct, MapGrid gegen TileMap, VisibilitySystem gegen FogOfWar, A-Stern gegen Luftlinie). Das aufzulösen ist Block B in TODO.md.

---

Offene Punkte: [TODO.md](../TODO.md)
Spielspezifikation: [AgeOfEmpires.md](AgeOfEmpires.md)
