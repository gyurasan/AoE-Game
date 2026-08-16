# Age of Empires Inspired Game Plan

## Core Architecture

The game will be built using MonoGame for cross-platform support (Windows, macOS, Linux only - no mobile).

### Game Loop
- Update loop for game state changes
- Draw loop for rendering graphics
- Input handling for player actions (mouse, keyboard)

### Tilemap System
- Use a 2D texture-based tilemap for the terrain
- Each tile has properties (type, walkability, resource type)
- Implement a grid system to manage tiles and entities
- Camera system with pan/zoom capabilities

### Resource System (4 Separate Resources)
| Ressource | Quelle | Verwendung |
|---|---|---|
| **Nahrung** | Beeren, Wild, Schafe, Farmen, Fisch | Dorfbewohner, Militär, Zeitalter-Aufstieg |
| **Holz** | Bäume | Gebäude, Bogenschützen, Schiffe, Farmen |
| **Gold** | Minen, Handel, Reliquien | Elite-Einheiten, Technologien |
| **Stein** | Steinbrüche | Mauern, Türme, Burgen, Stadtzentren |

- Players gather resources by moving units to resource spots
- Implement resource counters in the UI
- Dorfbewohner loop: gather → carry → deliver → repeat

### Population Limit System
- Shared cap (typically 200) between economy and military
- Each villager and unit consumes a population slot
- Houses provide additional population slots

### Units
- Base unit class with health, movement speed, attack power
- Specific unit types (infantry, cavalry, archers, buildings)
- Group movement and selection via mouse
- **Counter Triangle Combat**:
  - Spearmen counter cavalry
  - Cavalry crush archers
  - Archers counter infantry
  - Skirmishers counter archers
  - Siege weapons target buildings

### Buildings
- Different building types with construction requirements
- Structures that produce units or provide resources
- Walls and towers for defense

### AI System
- Enemy AI that gathers resources, builds structures, and attacks
- Use a state machine for AI behaviors

### Pathfinding
- A* algorithm for unit movement and pathfinding on the tilemap
- Group movement with formation handling
- Collision avoidance

### Visibility / Fog of War
- Areas outside player sight are obscured
- Reveal areas when units explore
- Critical for strategic gameplay

### User Interface (UI)
- Heads-up display showing resources, population count, selected units
- Minimap for overview
- Build queue and unit controls

## Implementation Steps

### Phase 1: Core Mechanics Setup
- Update game_plan.md with critical AoE mechanics ✓
- Test build - remove mobile dependencies ✓
- Create data models (Resource, Tile, Unit types)

### Phase 2: Game World
- Implement Tilemap system with terrain types
- Add camera system with pan/zoom
- Implement Resource System with 4 resource types

### Phase 3: Units and Economy
- Implement Villager unit with gather/deliver loop
- Add Population Limit system
- Create Unit management (select, move, group)

### Phase 4: Combat System
- Implement Counter Triangle combat mechanics
- Add health, damage, and armor types
- Unit animations and combat states

### Phase 5: Fog of War & Exploration
- Implement visibility system
- Add exploration mechanics
- Update minimap with discovered areas

### Phase 6: Buildings & Buildings System
- Implement building placement and construction
- Add building production queues
- Create defensive structures

### Phase 7: AI Implementation
- Basic resource-gathering AI
- Building placement AI
- Combat AI with counter logic

### Phase 8: Polish & Testing
- Add UI elements
- Optimize performance
- Test cross-platform (Windows, macOS, Linux)

This plan is a starting point. Each phase builds on the previous one, implementing core AoE mechanics as described in the official documentation.