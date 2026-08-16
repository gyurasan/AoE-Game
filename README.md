# Age of Empires II - C#/.NET 10 Implementation

Cross-platform Implementation des AoE2-Spiels in C# (.NET 10), lauffähig auf Windows 11, Ubuntu Linux und macOS.

## Projektstruktur

```
AgeOfEmpire/
├── src/
│   └── AoE.Core/
│       ├──AoE.Core.csproj
│       ├── Entities/          # Einheiten, Gebäude, Ressourcen
│       ├── Combat/            # Schadensberechnung, Konter-System
│       ├── Economy/           # Dorfbewohner-Logik, Ressourcen
│       ├── Map/               # Karte, Sichtbarkeit
│       └── Pathfinding/       # Wegfindung (A*)
├── tests/
│   └── AoE.Tests/           # Unit-Tests (15 Tests, alle erfolgreich)
└── README.md
```

## Features (Core-Library)

- **Einheiten**: Villager, Scout, Militia, Spearman, Archer, Knight, Ram, Trebuchet
- **Konter-System**: Speer vs Kavallerie, Ritter vs Bogenschützen, Belagerung vs Gebäude
- **Ressourcen**: Food, Wood, Gold, Stone mit ResourcePool
- **Gebäude**: Town Center, Barracks, Stable, Castles mit Technologie-Forschung
- **Kampf**: Schadensberechnung mit Rüstung und Bonus-System
- **Sichtbarkeit**: Nebel des Krieges mit TileVisibility
- **Wegfindung**: A*-Algorithmus für Einheitenbewegung

## Build

### Voraussetzungen
- .NET 10 SDK installiert

### Build-Befehle

```bash
# Core-Library bauen
cd src/AoE.Core
dotnet build

# Tests ausführen (15 Tests)
cd tests/AoE.Tests
dotnet test

# Cross-Platform Publish
dotnet publish -c Release -r win10-x64 --self-contained false -o bin/publish/win
dotnet publish -c Release -r linux-x64 --self-contained false -o bin/publish/linux
dotnet publish -c Release -r osx-x64 --self-contained false -o bin/publish/osx
```

## Entwurf

### Plattformunabhängigkeit
- Keine platform-spezifischen APIs (DirectX, etc.)
- Verwendung von Standard-.NET 10 Klassen
- XAmit für UI (Avalonia UI, MAUI oder andere Cross-Platform UI-Frameworks können integriert werden)

### Erweiterung
Für eine vollständige Game-Engine empfehle ich die Integration einer UI-Engine:
- **Avalonia UI** für Desktop-UI
- **MAUI** für mobile Plattformen
- **MonoGame** für Native-Game-Development (falls gewünscht)

## TODO (Future)
- UI-Integration (Avalonia/MAUI/MonoGame)
- Multiplayer-System
- .xws Scripting-System für KI
- .slp/.tcx Dateiformate implementieren
- KI-Hierarchie (Rekrutierung, Strategie)