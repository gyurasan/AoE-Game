using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

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
    private FogOfWar fogOfWar;
    
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
        fogOfWar = new FogOfWar(width, height);
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
        // Simple random tile generation
        int rand = (x * 73856093 ^ y * 19349663) % 100;
        
        if (rand < 5) return TileType.Water;
        if (rand < 15) return TileType.Forest;
        if (rand < 20) return TileType.Mountain;
        if (rand < 23) return TileType.GoldMine;
        return TileType.Grassland;
    }
    
    private void AddResourcePatches()
    {
        // Add forest patches
        for (int i = 0; i < 10; i++)
        {
            int rx = RandomInt(5, Width - 5);
            int ry = RandomInt(5, Height - 5);
            for (int fx = rx; fx < rx + 5; fx++)
            {
                for (int fy = ry; fy < ry + 5; fy++)
                {
                    if (fx >= 0 && fx < Width && fy >= 0 && fy < Height)
                    {
                        tiles[fx, fy].Type = TileType.Forest;
                        tiles[fx, fy].ResourceType = Resource.Type.Wood;
                        tiles[fx, fy].ResourceAmount = 100;
                    }
                }
            }
        }
        
        // Add stone patches
        for (int i = 0; i < 5; i++)
        {
            int rx = RandomInt(5, Width - 5);
            int ry = RandomInt(5, Height - 5);
            for (int fx = rx; fx < rx + 3; fx++)
            {
                for (int fy = ry; fy < ry + 3; fy++)
                {
                    if (fx >= 0 && fx < Width && fy >= 0 && fy < Height)
                    {
                        tiles[fx, fy].Type = TileType.Mountain;
                        tiles[fx, fy].ResourceType = Resource.Type.Stone;
                        tiles[fx, fy].ResourceAmount = 80;
                    }
                }
            }
        }
        
        // Add gold patches
        for (int i = 0; i < 3; i++)
        {
            int rx = RandomInt(5, Width - 5);
            int ry = RandomInt(5, Height - 5);
            for (int fx = rx; fx < rx + 2; fx++)
            {
                for (int fy = ry; fy < ry + 2; fy++)
                {
                    if (fx >= 0 && fx < Width && fy >= 0 && fy < Height)
                    {
                        tiles[fx, fy].Type = TileType.GoldMine;
                        tiles[fx, fy].ResourceType = Resource.Type.Gold;
                        tiles[fx, fy].ResourceAmount = 60;
                    }
                }
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
    
    public List<Vector2> FindPath(Vector2 start, Vector2 end)
    {
        // Simple pathfinding - return direct path if walkable
        var gridStart = WorldToGrid(start);
        var gridEnd = WorldToGrid(end);
        
        var path = new List<Vector2>();
        
        // Simple BFS pathfinding
        if (IsWalkable((int)gridEnd.X, (int)gridEnd.Y))
        {
            // Direct path for now - could be improved with A*
            float x = start.X;
            float y = start.Y;
            float dx = end.X - start.X;
            float dy = end.Y - start.Y;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            
            int steps = (int)(distance / (TileSize / 2));
            for (int i = 0; i < steps; i++)
            {
                path.Add(new Vector2(
                    start.X + dx * i / steps,
                    start.Y + dy * i / steps
                ));
            }
            path.Add(end);
        }
        
        return path;
    }
    
    private int RandomInt(int min, int max)
    {
        if (max <= min) return min;
        return _random.Next(min, max);
    }
    
    // Fog of War methods
    public FogOfWar FogOfWar => fogOfWar;
    
    public void UpdateFogOfWarForPlayer(int playerId, List<Unit> units)
    {
        fogOfWar.UpdatePlayerVisibility(playerId, units, this);
    }
    
    public bool IsTileVisible(int x, int y)
    {
        return fogOfWar.IsVisible(x, y);
    }
    
    public bool IsTileExplored(int x, int y)
    {
        return fogOfWar.IsExplored(x, y);
    }
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
