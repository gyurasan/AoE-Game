using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using MapGrid = AoE.Core.Map.MapGrid;
using CorePosition = AoE.Core.Entities.Position;
using CorePathfinding = AoE.Core.Pathfinding.Pathfinding;
using CoreVisibility = AoE.Core.Map.VisibilitySystem;
using TileVisibility = AoE.Core.Map.TileVisibility;
using UnitEntity = AoE.Core.Entities.UnitEntity;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Represents a complete game map
/// </summary>
public class TileMap
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileSize { get; set; }
    
    private Tile[,] tiles;
    
    // Resources - List of resource patches
    public List<Resource> Resources { get; set; } = new List<Resource>();
    
    // Units on the map
    public List<Unit> Units { get; set; } = new List<Unit>();
    
    // Buildings on the map
    public List<Building> Buildings { get; set; } = new List<Building>();
    
    // Constructor
    public TileMap(int width, int height, int tileSize = 32)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        tiles = new Tile[width, height];
        InitializeRandomMap();
    }
    
    // Initialize a random map
    // Zufallszahlengenerator für Kartengenerierung (vorher: DateTime.Now.Ticks % Range
    // → in einer eng takteten Schleife nahezu konstante Werte, alle Flecken übereinander)
    private readonly Random _random = new Random();
    
    private void InitializeRandomMap()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = new Tile(x, y, GetRandomTileType(x, y));
            }
        }
        
        // Add resource patches
        AddResourcePatches();
        
        // Place starting positions
        PlaceStartingPositions();
    }
    
    private TileType GetRandomTileType(int x, int y)
    {
        // Nahezu reine Graskarte: nur kleine natürliche Wald-/Felsvorkommen,
        // keine Rohstoff-Kacheln (die kommen als Klumpen in AddResourcePatches).
        // Ortsfester Hash, damit dieselbe Karte reproduzierbar bleibt.
        //
        // unchecked + Vorzeichenbit ausmaskieren ist hier zwingend: die
        // Multiplikation läuft ab x = 30 über int.MaxValue und wird negativ,
        // und % liefert in C# bei negativem Dividenden ein negatives Ergebnis.
        // Damit traf 'rand < 4' immer zu — die Spalten x = 30..58 waren zu
        // 100 % Wald und der Waldanteil lag bei 47,8 % statt bei 4 %.
        int hash = unchecked(x * 73856093) ^ unchecked(y * 19349663);
        int rand = (hash & 0x7FFFFFFF) % 100;
        
        if (rand < 4) return TileType.Forest;
        if (rand < 7) return TileType.Rock;
        return TileType.Grassland;
    }
    
    // --- Naturformen ------------------------------------------------------
    
    // Weicher Kreis (Radius^2-Dämpfung an den Rändern), wie natürliche Seen.
    private bool InSoftCircle(int x, int y, int cx, int cy, int radius, Random rng)
    {
        int dx = x - cx, dy = y - cy;
        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
        if (dist < radius - 1.5f) return true;
        if (dist > radius + 0.5f) return false;
        return rng.NextDouble() < 0.5;
    }
    
    private void AddLakes()
    {
        int count = _random.Next(2, 5);
        for (int i = 0; i < count; i++)
        {
            int r = _random.Next(3, 6);
            int cx = _random.Next(4, Width - 4 - r);
            int cy = _random.Next(4, Height - 4 - r);
            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (!InSoftCircle(x, y, cx, cy, r, _random)) continue;
                    var t = tiles[x, y];
                    t.Type = TileType.Water;
                    t.ResourceType = null;
                    t.ResourceAmount = 0;
                }
            }
        }
    }
    
    // Sandstrand um jede Wasserkante (wie in AoE1).
    private void AddBeaches()
    {
        bool[,] isWater = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                isWater[x, y] = tiles[x, y].Type == TileType.Water;
        
        var sand = new List<(int x, int y)>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (isWater[x, y]) continue;
                if (tiles[x, y].Type != TileType.Grassland) continue;
                if (IsAdjacentWater(x, y, isWater)) sand.Add((x, y));
            }
        }
        foreach (var (x, y) in sand)
            tiles[x, y].Type = TileType.Sand;
    }
    
    private bool IsAdjacentWater(int x, int y, bool[,] water)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && water[nx, ny])
                    return true;
            }
        return false;
    }
    
    // --- Ressourcenklumpen -------------------------------------------------
    
    // Klumpenzentren an zufälligen freien Stellen, die weit weg von beiden
    // Startpositionen liegen. Rückkehrwert: Liste der Zentren (für Tiere).
    private List<(int x, int y)> PlaceClusterCenters(int count, int minSize)
    {
        var centers = new List<(int, int)>();
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int cx = _random.Next(minSize, Width - minSize);
                int cy = _random.Next(minSize, Height - minSize);
                if (TooCloseToStart(cx, cy, minSize + 3)) continue;
                if (centers.Any(c => Math.Abs(c.Item1 - cx) + Math.Abs(c.Item2 - cy) < minSize * 3)) continue;
                centers.Add((cx, cy));
                break;
            }
        }
        return centers;
    }
    
    private bool TooCloseToStart(int x, int y, int dist)
    {
        if (Math.Abs(x - 3) + Math.Abs(y - 3) < dist * 2) return true;
        if (Math.Abs(x - (Width - 4)) + Math.Abs(y - (Height - 4)) < dist * 3) return true;
        return false;
    }
    
    private void StampResource(int cx, int cy, int radius, TileType type,
                               Resource resource, int baseAmount)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                if (!InSoftCircle(x, y, cx, cy, radius, _random)) continue;
                var t = tiles[x, y];
                if (t.Type == TileType.Water || t.Building != null) continue;
                t.Type = type;
                if (t.ResourceType != resource)
                    t.ResourceAmount = baseAmount + _random.Next(0, baseAmount / 2);
            }
        }
    }
    
    // Kleine Gras-Inseln im Wald (aufsammlbar ohne Bäume fällen zu müssen)
    private void StampClearing(int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                var t = tiles[x, y];
                if (t.Type == TileType.Forest && InSoftCircle(x, y, cx, cy, radius, _random))
                    t.Type = TileType.Grassland;
            }
        }
    }
    
    private void AddResourcePatches()
    {
        // Natürliche Gewässer zuerst, dann Strände.
        AddLakes();
        AddBeaches();
        
        // Wälder (Holz): 5–7 Klumpen, jeweils mit einer kleinen Lichtung
        // für Nahrungsklumpen.
        var woodCenters = PlaceClusterCenters(_random.Next(5, 8), 3);
        foreach (var (cx, cy) in woodCenters)
        {
            int r = _random.Next(2, 4);
            StampResource(cx, cy, r, TileType.Forest, Resource.Wood, 100);
            if (_random.NextDouble() < 0.7)
            {
                // Lichtung am Rand des Waldes
                int ox = cx + _random.Next(-1, 2) * (r + 1);
                int oy = cy + _random.Next(-1, 2) * (r + 1);
                StampClearing(ox, oy, 1);
            }
        }
        
        // Steinbrüche: 3–4 kleinere Klumpen
        var stoneCenters = PlaceClusterCenters(_random.Next(3, 5), 2);
        foreach (var (cx, cy) in stoneCenters)
            StampResource(cx, cy, _random.Next(1, 3), TileType.Mountain, Resource.Stone, 80);
        
        // Goldklumpen: 2–3 kleine, wertvollere Klumpen
        var goldCenters = PlaceClusterCenters(_random.Next(2, 4), 2);
        foreach (var (cx, cy) in goldCenters)
            StampResource(cx, cy, _random.Next(1, 2), TileType.GoldMine, Resource.Gold, 60);
        
        // Nahrung: Schafherden auf der Wiese + Beerenbüsche in Lichtungen
        PlaceSheep(woodCenters);
        PlaceBerryBushes(woodCenters);
    }
    
    // Schafherden: 4–5 Stellen mal 2–4 Schafe, nur auf Gras in der Nähe
    // bestehender Grasflächen (Wiese, nicht mitten im Wald).
    private void PlaceSheep(List<(int x, int y)> woodCenters)
    {
        var herds = PlaceClusterCenters(_random.Next(4, 6), 2);
        foreach (var (cx, cy) in herds)
        {
            int count = _random.Next(2, 5);
            for (int i = 0; i < count; i++)
            {
                int sx = cx + _random.Next(-1, 2);
                int sy = cy + _random.Next(-1, 2);
                var t = GetTile(sx, sy);
                if (t == null || t.Type != TileType.Grassland) continue;
                t.ResourceType = Resource.Food;
                t.ResourceAmount = 1; // ein Schaf
            }
        }
    }
    
    // Beerenbüsche: ein paar einzelne Büsche nah an Waldlichtungen.
    private void PlaceBerryBushes(List<(int x, int y)> woodCenters)
    {
        foreach (var (cx, cy) in woodCenters)
        {
            if (_random.NextDouble() > 0.6) continue;
            for (int i = 0; i < 3; i++)
            {
                int bx = cx + _random.Next(-2, 3);
                int by = cy + _random.Next(-2, 3);
                var t = GetTile(bx, by);
                if (t == null || t.Type != TileType.Grassland) continue;
                t.ResourceType = Resource.Food;
                t.ResourceAmount = 8;
            }
        }
    }
    
    private void PlaceStartingPositions()
    {
        // Place starting town centers
        // Player 1 (top left)
        int p1x = 3;
        int p1y = 3;
        AddBuilding(p1x, p1y, "Stadtzentrum", 0);
        
        // Player 2 (bottom right)
        int p2x = Width - 4;
        int p2y = Height - 4;
        AddBuilding(p2x, p2y, "Stadtzentrum", 1);
        
        // Initial villagers
        AddVillager(p1x + 1, p1y, 0);
        AddVillager(p1x + 2, p1y, 0);
        AddVillager(p1x, p1y + 1, 0);
        AddVillager(p1x + 1, p1y + 1, 0);
        
        AddVillager(p2x - 1, p2y, 1);
        AddVillager(p2x - 2, p2y, 1);
        AddVillager(p2x, p2y - 1, 1);
        AddVillager(p2x - 1, p2y - 1, 1);
    }
    
    private void AddVillager(int x, int y, int ownerId)
    {
        var villager = new Unit(UnitType.Villager, ownerId)
        {
            Position = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2)
        };
        Units.Add(villager);
    }
    
    public void AddBuilding(int x, int y, string buildingType, int ownerId)
    {
        var building = new Building
        {
            X = x,
            Y = y,
            Type = buildingType,
            OwnerId = ownerId,
            Width = 4,
            Height = 4
        };
        Buildings.Add(building);
        
        // Mark tiles as occupied
        for (int bx = x; bx < x + building.Width && bx < Width; bx++)
        {
            for (int by = y; by < y + building.Height && by < Height; by++)
            {
                tiles[bx, by].Building = buildingType;
                _navDirty = true;
                tiles[bx, by].Buildable = false;
            }
        }
    }
    
    public Tile GetTile(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            return tiles[x, y];
        return null;
    }
    
    public bool IsWalkable(int x, int y)
    {
        var tile = GetTile(x, y);
        return tile != null && tile.Walkable && string.IsNullOrEmpty(tile.Building);
    }
    
    public Vector2 WorldToGrid(Vector2 worldPosition)
    {
        return new Vector2(
            (int)(worldPosition.X / TileSize),
            (int)(worldPosition.Y / TileSize)
        );
    }
    
    public Vector2 GridToWorld(Vector2 gridPosition)
    {
        return new Vector2(
            gridPosition.X * TileSize + TileSize / 2,
            gridPosition.Y * TileSize + TileSize / 2
        );
    }
    
    // --- Navigation ------------------------------------------------------
    // Die Wegfindung liegt in AoE.Core (A* mit Manhattan-Heuristik). Hier
    // wird nur die Begehbarkeit der Spielkarte in ein MapGrid gespiegelt.

    private MapGrid _navGrid;
    private bool _navDirty = true;

    /// <summary>
    /// Navigationsgitter für die Kernbibliothek. Wird nur neu aufgebaut,
    /// wenn sich die Begehbarkeit geändert hat — bei 100×100 Kacheln lohnt
    /// es sich, das nicht bei jedem Rechtsklick zu tun.
    /// </summary>
    private MapGrid NavGrid
    {
        get
        {
            _navGrid ??= new MapGrid(Width, Height);
            if (_navDirty)
            {
                // Nur die Begehbarkeit auffrischen. Das Gitter selbst bleibt
                // bestehen, weil daran die Sichtbarkeitszustände und die
                // angemeldeten Einheiten hängen.
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        var navTile = _navGrid.GetTile(x, y);
                        if (navTile != null)
                            navTile.IsPassable = IsWalkable(x, y);
                    }
                }
                _navDirty = false;
            }
            return _navGrid;
        }
    }

    /// <summary>
    /// Meldet, dass sich die Begehbarkeit geändert hat (etwa durch ein
    /// neu gesetztes Gebäude). Der nächste Wegfindungsaufruf baut neu auf.
    /// </summary>
    public void InvalidateNavigation() => _navDirty = true;

    /// <summary>
    /// Sucht einen begehbaren Weg von <paramref name="start"/> nach
    /// <paramref name="end"/> und gibt ihn als Weltkoordinaten zurück.
    /// Ist das Ziel nicht erreichbar, kommt eine leere Liste zurück —
    /// die Einheit bleibt dann stehen, statt in ein Hindernis zu laufen.
    /// </summary>
    public List<Vector2> FindPath(Vector2 start, Vector2 end)
    {
        var path = new List<Vector2>();

        var gridStart = WorldToGrid(start);
        var gridEnd = WorldToGrid(end);

        if (!IsWalkable((int)gridEnd.X, (int)gridEnd.Y))
            return path;

        var steps = CorePathfinding.FindPath(
            NavGrid,
            new CorePosition((int)gridStart.X, (int)gridStart.Y),
            new CorePosition((int)gridEnd.X, (int)gridEnd.Y));

        if (steps == null || steps.Count == 0)
            return path;

        // Die erste Kachel ist die, auf der die Einheit bereits steht.
        for (int i = 1; i < steps.Count; i++)
            path.Add(GridToWorld(new Vector2(steps[i].X, steps[i].Y)));

        // Letzten Wegpunkt auf das genaue Ziel legen, damit die Einheit
        // nicht nur bis zur Kachelmitte läuft.
        if (path.Count > 0)
            path[path.Count - 1] = end;

        return path;
    }
    
    private int RandomInt(int min, int max)
    {
        if (max <= min) return min;
        return _random.Next(min, max);
    }
    
    // --- Sichtbarkeit ----------------------------------------------------
    // Der Nebel des Krieges kommt aus AoE.Core.Map.VisibilitySystem. Das
    // liefert die drei Zustände der Spezifikation: unerforscht, erforscht
    // (letzter bekannter Stand) und aktuell sichtbar.

    private CoreVisibility _visibility;
    private readonly List<UnitEntity> _registeredUnits = new List<UnitEntity>();

    /// <summary>
    /// Rechnet die Sicht eines Spielers neu. Die Einheiten werden dafür an
    /// der Karte angemeldet und ihre Kachelposition nachgezogen — die
    /// Sichtrechnung arbeitet auf Gitter-, nicht auf Weltkoordinaten.
    /// </summary>
    public void UpdateFogOfWarForPlayer(int playerId, List<Unit> units)
    {
        var grid = NavGrid;
        _visibility ??= new CoreVisibility(grid);

        foreach (var stale in _registeredUnits)
            grid.RemoveUnit(stale);
        _registeredUnits.Clear();

        foreach (var unit in units)
        {
            if (unit?.Core == null)
                continue;

            var cell = WorldToGrid(unit.Position);
            unit.Core.Position = new CorePosition((int)cell.X, (int)cell.Y);
            unit.Core.OwnerId = unit.OwnerId;

            grid.AddUnit(unit.Core);
            _registeredUnits.Add(unit.Core);
        }

        _visibility.UpdateVisibility(playerId);
    }

    /// <summary>Kachel ist gerade einsehbar.</summary>
    public bool IsTileVisible(int x, int y, int playerId = 0)
        => NavGrid.GetTile(x, y)?.GetVisibility(playerId) == TileVisibility.Visible;

    /// <summary>
    /// Kachel wurde schon einmal gesehen. Schließt den Zustand
    /// „erforscht, aber veraltet" mit ein — der wird abgedunkelt gezeichnet.
    /// </summary>
    public bool IsTileExplored(int x, int y, int playerId = 0)
        => (NavGrid.GetTile(x, y)?.GetVisibility(playerId)
            ?? TileVisibility.Unexplored) != TileVisibility.Unexplored;
}

/// <summary>
/// Represents a building in the game
/// </summary>
public class Building
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OwnerId { get; set; }
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    
    // Production queue
    public Queue<UnitType> ProductionQueue { get; set; } = new Queue<UnitType>();
    public float ProductionTimer { get; set; }
    
    // Constructor
    public Building()
    {
        MaxHealth = 1000;
        Health = MaxHealth;
    }
}

/// <summary>
/// Represents a construction site
/// </summary>
public class ConstructionSite
{
    public int X { get; set; }
    public int Y { get; set; }
    public string BuildingType { get; set; }
    public int OwnerId { get; set; }
    public float Progress { get; set; }
    public int WorkersAssigned { get; set; }
}
