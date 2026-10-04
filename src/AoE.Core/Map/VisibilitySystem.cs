using System;
using System.Collections.Generic;
using AoE.Core.Entities;

namespace AoE.Core.Map;

/// <summary>
/// Sichtbarkeits-Zustand für eine Kachel
/// </summary>
public enum TileVisibility
{
    /// <summary>
    /// Unerforscht - schwarz, nichts sichtbar
    /// </summary>
    Unexplored,
    
    /// <summary>
    /// Erforscht - abgedunkelt, letzter bekannter Zustand
    /// </summary>
    Explored,
    
    /// <summary>
    /// Sichtbar - aktueller Zustand, inklusive gegnerischer Einheiten
    /// </summary>
    Visible
}

/// <summary>
/// Kachel-Informationen
/// </summary>
public sealed class Tile
{
    /// <summary>
    /// X-Position
    /// </summary>
    public int X { get; }
    
    /// <summary>
    /// Y-Position
    /// </summary>
    public int Y { get; }
    
    /// <summary>
    /// Geländetyp
    /// </summary>
    public TerrainType Terrain { get; set; }
    
    /// <summary>
    /// Ist die Kachel bebaubar?
    /// </summary>
    public bool IsBuildable { get; set; } = true;
    
    /// <summary>
    /// Ist die Kachel begehbar?
    /// </summary>
    public bool IsPassable { get; set; } = true;
    
    /// <summary>
    /// Gebäude auf dieser Kachel (falls vorhanden)
    /// </summary>
    public BuildingEntity? Building { get; set; }
    
    /// <summary>
    /// Sichtbarkeitszustand pro Spieler
    /// </summary>
    private readonly Dictionary<int, TileVisibility> _visibility;
    
    public Tile(int x, int y)
    {
        X = x;
        Y = y;
        _visibility = new Dictionary<int, TileVisibility>();
    }
    
    /// <summary>
    /// Setzt die Sichtbarkeit für einen Spieler
    /// </summary>
    public void SetVisibility(int playerId, TileVisibility state)
    {
        _visibility[playerId] = state;
    }
    
    /// <summary>
    /// Gibt die Sichtbarkeit für einen Spieler zurück
    /// </summary>
    public TileVisibility GetVisibility(int playerId)
    {
        return _visibility.TryGetValue(playerId, out var state) ? state : TileVisibility.Unexplored;
    }
    
    /// <summary>
    /// Gibt alle Spieler zurück, die diese Kachel sehen können
    /// </summary>
    public List<int> GetVisibleToPlayers()
    {
        var visible = new List<int>();
        foreach (var kvp in _visibility)
        {
            if (kvp.Value == TileVisibility.Visible)
                visible.Add(kvp.Key);
        }
        return visible;
    }
}

/// <summary>
/// Geländetypen
/// </summary>
public enum TerrainType
{
    /// <summary>
    /// Grasland
    /// </summary>
    Grass,
    
    /// <summary>
    /// Sand
    /// </summary>
    Sand,
    
    /// <summary>
    /// Schnee
    /// </summary>
    Snow,
    
    /// <summary>
    /// Wasser
    /// </summary>
    Water,
    
    /// <summary>
    /// Untiefe (begehbar aber langsamer)
    /// </summary>
    Shallow,
    
    /// <summary>
    /// Straße
    /// </summary>
    Path
}

/// <summary>
/// Sichtbarkeits-System
/// </summary>
public sealed class VisibilitySystem
{
    private readonly MapGrid _map;
    private readonly Dictionary<int, HashSet<Position>> _visibleTiles;
    
    public VisibilitySystem(MapGrid map)
    {
        _map = map;
        _visibleTiles = new Dictionary<int, HashSet<Position>>();
    }
    
    /// <summary>
    /// Aktualisiert die Sichtbarkeit für einen Spieler
    /// </summary>
    public void UpdateVisibility(int playerId)
    {
        var visible = new HashSet<Position>();

        // Finde alle sichtbaren Einheiten des Spielers
        foreach (var unit in _map.GetUnitsByOwner(playerId))
        {
            if (unit.IsAlive)
                MarkVisible(visible, unit.Position, unit.Stats.VisionRange);
        }

        // Gebäude sehen ihre Umgebung wie Einheiten. Ohne das läge das eigene
        // Stadtzentrum im Nebel, sobald kein Dorfbewohner daneben steht.
        // Baustellen sehen noch nichts - erst das fertige Gebäude deckt auf.
        foreach (var building in _map.GetBuildings())
        {
            if (building.OwnerId == playerId && building.IsAlive && !building.IsUnderConstruction)
                MarkVisible(visible, building.Position, building.Stats.VisionRange);
        }
        
        // Die vorherige Sichtmenge festhalten, bevor sie ersetzt wird: daraus
        // ergibt sich, welche Kacheln gerade aus dem Blickfeld geraten sind.
        _visibleTiles.TryGetValue(playerId, out var zuvorSichtbar);
        _visibleTiles[playerId] = visible;
        
        // Sichtbarkeitszustände updaten
        UpdateTileVisibility(playerId, visible, zuvorSichtbar);
    }
    
    /// <summary>
    /// Markiert alle Kacheln im Kreis um <paramref name="center"/> als sichtbar.
    /// </summary>
    private void MarkVisible(HashSet<Position> visible, Position center, int range)
    {
        for (int x = -range; x <= range; x++)
        {
            for (int y = -range; y <= range; y++)
            {
                var pos = new Position(center.X + x, center.Y + y);
                if (IsWithinMap(pos) && pos.DistanceSquared(center) <= range * range)
                    visible.Add(pos);
            }
        }
    }

    /// <summary>
    /// Prüft, ob ein Spieler eine Einheit sehen kann
    /// </summary>
    public bool CanSeeUnit(int observerId, UnitEntity target)
    {
        if (!_visibleTiles.TryGetValue(observerId, out var visible))
            return false;
        
        return visible.Contains(target.Position);
    }
    
    /// <summary>
    /// Prüft, ob ein Spieler eine Position sehen kann
    /// </summary>
    public bool CanSeePosition(int observerId, Position position)
    {
        if (!_visibleTiles.TryGetValue(observerId, out var visible))
            return false;
        
        return visible.Contains(position);
    }
    
    /// <summary>
    /// Aktualisiert die Tile-Sichtbarkeiten
    /// </summary>
    private void UpdateTileVisibility(
        int playerId,
        HashSet<Position> visible,
        HashSet<Position>? zuvorSichtbar)
    {
        // Kacheln, die zuletzt sichtbar waren und es jetzt nicht mehr sind,
        // fallen auf "erforscht" zurück. Sie zeigen weiterhin den letzten
        // bekannten Stand, werden aber abgedunkelt dargestellt. Fehlt dieser
        // Schritt, bleibt einmal Gesehenes für immer hell und der Nebel des
        // Krieges kennt effektiv nur zwei statt drei Zustände.
        if (zuvorSichtbar != null)
        {
            foreach (var position in zuvorSichtbar)
            {
                if (visible.Contains(position))
                    continue;

                var verlassen = _map.GetTile(position);
                if (verlassen != null
                    && verlassen.GetVisibility(playerId) == TileVisibility.Visible)
                {
                    verlassen.SetVisibility(playerId, TileVisibility.Explored);
                }
            }
        }

        foreach (var position in visible)
        {
            var tile = _map.GetTile(position);
            if (tile != null)
            {
                tile.SetVisibility(playerId, TileVisibility.Visible);
            }
        }
    }
    
    /// <summary>
    /// Prüft, ob eine Position innerhalb der Karte ist
    /// </summary>
    private bool IsWithinMap(Position position)
    {
        return position.X >= 0 && position.X < _map.Width &&
               position.Y >= 0 && position.Y < _map.Height;
    }
}

/// <summary>
/// Karten-Gitter
/// </summary>
public sealed class MapGrid
{
    private readonly Tile[,] _tiles;
    private readonly List<UnitEntity> _units;
    private readonly List<BuildingEntity> _buildings;
    
    public int Width { get; }
    public int Height { get; }
    
    public MapGrid(int width, int height)
    {
        Width = width;
        Height = height;
        _tiles = new Tile[width, height];
        _units = new List<UnitEntity>();
        _buildings = new List<BuildingEntity>();
        
        // Tiles initialisieren
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                _tiles[x, y] = new Tile(x, y)
                {
                    Terrain = TerrainType.Grass
                };
            }
        }
    }
    
    /// <summary>
    /// Gibt eine Kachel zurück
    /// </summary>
    public Tile? GetTile(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            return _tiles[x, y];
        return null;
    }
    
    /// <summary>
    /// Gibt eine Kachel an einer Position zurück
    /// </summary>
    public Tile? GetTile(Position position)
    {
        return GetTile(position.X, position.Y);
    }
    
    /// <summary>
    /// Prüft, ob eine Position begehbar ist
    /// </summary>
    public bool IsPassable(Position position)
    {
        var tile = GetTile(position);
        return tile != null && tile.IsPassable;
    }
    
    /// <summary>
    /// Prüft, ob eine Position bebaubar ist
    /// </summary>
    public bool IsBuildable(Position position)
    {
        var tile = GetTile(position);
        return tile != null && tile.IsBuildable && tile.Building == null;
    }
    
    /// <summary>
    /// Fügt eine Einheit hinzu
    /// </summary>
    public void AddUnit(UnitEntity unit)
    {
        _units.Add(unit);
        
        // Kachel aktualisieren
        var tile = GetTile(unit.Position);
        if (tile != null)
        {
            // Für Einheiten aktualisieren wir nichts direkt
            // (Einheiten können sich bewegen)
        }
    }
    
    /// <summary>
    /// Entfernt eine Einheit
    /// </summary>
    public void RemoveUnit(UnitEntity unit)
    {
        _units.Remove(unit);
    }
    
    /// <summary>
    /// Fügt ein Gebäude hinzu
    /// </summary>
    public void AddBuilding(BuildingEntity building)
    {
        _buildings.Add(building);
        
        // Kachel aktualisieren
        var tile = GetTile(building.Position);
        if (tile != null)
        {
            tile.Building = building;
            tile.IsBuildable = false;
            tile.IsPassable = false;
        }
    }
    
    /// <summary>
    /// Entfernt ein Gebäude
    /// </summary>
    public void RemoveBuilding(BuildingEntity building)
    {
        _buildings.Remove(building);
        
        var tile = GetTile(building.Position);
        if (tile != null)
        {
            tile.Building = null;
            tile.IsBuildable = true;
            tile.IsPassable = true;
        }
    }
    
    /// <summary>
    /// Gibt alle Einheiten eines Spielers zurück
    /// </summary>
    public List<UnitEntity> GetUnitsByOwner(int ownerId)
    {
        return _units.FindAll(u => u.OwnerId == ownerId);
    }
    
    /// <summary>
    /// Gibt alle Gebäude zurück
    /// </summary>
    public List<BuildingEntity> GetBuildings() => new List<BuildingEntity>(_buildings);
}
