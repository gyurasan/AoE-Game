using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Entities;
using AoE.Core.Map;

namespace AoE.Core.Pathfinding;

/// <summary>
/// Pfadsuch-Algorithmus (A*)
/// </summary>
public static class Pathfinding
{
    /// <summary>
    /// Findet einen Pfad mit A*
    /// </summary>
    public static List<Position>? FindPath(
        MapGrid map,
        Position start,
        Position target)
    {
        // Heuristik: Manhattan-Distanz
        int Heuristic(Position a, Position b) =>
            Math.Abs(a.X - b.X) + Math.Abs(a.Y - b.Y);
        
        var openSet = new PriorityQueue<Position, int>();
        openSet.Enqueue(start, Heuristic(start, target));
        
        var cameFrom = new Dictionary<Position, Position>();
        var gScore = new Dictionary<Position, int>();
        gScore[start] = 0;
        
        var closedSet = new HashSet<Position>();
        
        while (openSet.Count > 0)
        {
            var current = openSet.Dequeue();
            
            if (current.X == target.X && current.Y == target.Y)
            {
                return ReconstructPath(cameFrom, current);
            }
            
            closedSet.Add(current);
            
            // Nachbarn prüfen
            foreach (var neighbor in GetNeighbors(map, current))
            {
                if (closedSet.Contains(neighbor))
                    continue;
                
                int tentativeGScore = gScore.GetValueOrDefault(current, int.MaxValue) + 1;
                
                if (tentativeGScore < gScore.GetValueOrDefault(neighbor, int.MaxValue))
                {
                    cameFrom[neighbor] = current;
                    gScore[neighbor] = tentativeGScore;
                    int fScore = tentativeGScore + Heuristic(neighbor, target);
                    openSet.Enqueue(neighbor, fScore);
                }
            }
        }
        
        // Kein Pfad gefunden
        return null;
    }
    
    /// <summary>
    /// Holt alle begehbbaren Nachbarn
    /// </summary>
    private static List<Position> GetNeighbors(MapGrid map, Position position)
    {
        var neighbors = new List<Position>();
        
        // 4-Richtungen (N, O, S, W)
        var directions = new[] {
            new Position(0, -1), new Position(1, 0),
            new Position(0, 1), new Position(-1, 0)
        };
        
        foreach (var dir in directions)
        {
            var pos = new Position(position.X + dir.X, position.Y + dir.Y);
            
            if (map.IsPassable(pos))
                neighbors.Add(pos);
        }
        
        return neighbors;
    }
    
    /// <summary>
    /// Rekonstruiert den Pfad
    /// </summary>
    private static List<Position> ReconstructPath(
        Dictionary<Position, Position> cameFrom,
        Position current)
    {
        var path = new List<Position> { current };
        
        while (cameFrom.TryGetValue(current, out Position prev))
        {
            path.Add(prev);
            current = prev;
        }
        
        path.Reverse();
        return path;
    }
    
    /// <summary>
    /// Findet nahe liegende freie Position (für Gebäude)
    /// </summary>
    public static Position? FindNearbyFreePosition(
        MapGrid map,
        Position center,
        int maxRadius = 5)
    {
        for (int radius = 1; radius <= maxRadius; radius++)
        {
            // Raster durchlaufen
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var pos = new Position(center.X + x, center.Y + y);
                    
                    if (map.IsBuildable(pos))
                        return pos;
                }
            }
        }
        
        return null;
    }
}

/// <summary>
/// Bewegungssystem für Einheiten
/// </summary>
public sealed class MovementSystem
{
    private readonly MapGrid _map;
    
    public MovementSystem(MapGrid map)
    {
        _map = map;
    }
    
    /// <summary>
    /// Bewegt eine Einheit zu einer Position
    /// </summary>
    public bool MoveTo(UnitEntity unit, Position target)
    {
        // Pfad finden
        var path = Pathfinding.FindPath(_map, unit.Position, target);
        
        if (path == null || path.Count == 0)
            return false;
        
        // Bewege zur ersten Position im Pfad
        var nextPos = path[0];
        
        if (_map.IsPassable(nextPos))
        {
            // Alte Kachel aktualisieren
            var oldTile = _map.GetTile(unit.Position);
            
            // Neue Position setzen
            unit.Position = nextPos;
            
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Bewegt eine Einheit entlang eines Pfads
    /// </summary>
    public bool MoveAlongPath(UnitEntity unit, List<Position> path)
    {
        if (path.Count == 0)
            return false;
        
        // Bewege zur ersten Position
        return MoveTo(unit, path[0]);
    }
    
    /// <summary>
    /// Findet einen freien Platz in der Nähe
    /// </summary>
    public Position? FindFreeSpot(UnitEntity unit, int radius = 3)
    {
        for (int r = 1; r <= radius; r++)
        {
            for (int x = -r; x <= r; x++)
            {
                for (int y = -r; y <= r; y++)
                {
                    var pos = new Position(unit.Position.X + x, unit.Position.Y + y);
                    
                    if (_map.IsPassable(pos))
                        return pos;
                }
            }
        }
        
        return unit.Position;
    }
}

/// <summary>
/// Gruppen-Bewegung
/// </summary>
public static class FormationMovement
{
    /// <summary>
    /// Bewegt eine Gruppe von Einheiten
    /// </summary>
    public static void MoveFormation(
        List<UnitEntity> units,
        Position target,
        Formation formation)
    {
        // Berechne Positionsrelativ zum Ziel
        var offsets = CalculateFormationOffsets(units.Count, formation);
        
        for (int i = 0; i < units.Count; i++)
        {
            if (i < offsets.Length)
            {
                var unit = units[i];
                var pos = new Position(
                    target.X + offsets[i].X,
                    target.Y + offsets[i].Y);
                
                unit.TargetPosition = pos;
            }
        }
    }
    
    /// <summary>
    /// Berechnet Positions-Offsets für eine Formation
    /// </summary>
    private static Position[] CalculateFormationOffsets(int count, Formation formation)
    {
        var offsets = new Position[count];
        
        switch (formation)
        {
            case Formation.Line:
                // Linie horizontal
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = new Position(i - count / 2, 0);
                }
                break;
            
            case Formation.Box:
                // Quadrat
                int side = (int)Math.Ceiling(Math.Sqrt(count));
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = new Position(i % side - side / 2, i / side - side / 2);
                }
                break;
            
            case Formation.Staggered:
                // Gestaffelt
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = new Position(
                        i % 2 * 2 - 1,
                        i / 2);
                }
                break;
            
            default:
                // Default: alle auf demselben Punkt (ungünstig!)
                for (int i = 0; i < count; i++)
                {
                    offsets[i] = new Position(0, 0);
                }
                break;
        }
        
        return offsets;
    }
}

/// <summary>
/// Formationen für Gruppen
/// </summary>
public enum Formation
{
    /// <summary>
    /// Linie
    /// </summary>
    Line,
    
    /// <summary>
    /// Kasten
    /// </summary>
    Box,
    
    /// <summary>
    /// Gestaffelt
    /// </summary>
    Staggered,
    
    /// <summary>
    /// Flanke
    /// </summary>
    Flank
}
