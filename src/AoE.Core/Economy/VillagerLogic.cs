using System;
using System.Collections.Generic;
using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Ressourcen-Kontounter für Spieler
/// </summary>
public sealed class ResourcePool
{
    private readonly Dictionary<Resource, int> _resources;
    
    /// <summary>
    /// Startkapital für jede Ressource
    /// </summary>
    public const int START_GOLD = 100;
    public const int START_STONE = 200;
    public const int START_WOOD = 200;
    public const int START_FOOD = 200;
    
    public ResourcePool()
    {
        _resources = new Dictionary<Resource, int>
        {
            [Resource.Food] = START_FOOD,
            [Resource.Wood] = START_WOOD,
            [Resource.Gold] = START_GOLD,
            [Resource.Stone] = START_STONE,
            [Resource.Population] = 0  // Wird durch Häuser bestimmt
        };
    }
    
    public int this[Resource resource] => _resources.ContainsKey(resource) ? _resources[resource] : 0;
    
    /// <summary>
    /// Addiert Ressourcen
    /// </summary>
    public void Add(Resource resource, int amount)
    {
        if (_resources.ContainsKey(resource))
            _resources[resource] += amount;
    }
    
    /// <summary>
    /// Subtrahiert Ressourcen, gibt false zurück wenn nicht genügend vorhanden
    /// </summary>
    public bool Remove(Resource resource, int amount)
    {
        if (!_resources.ContainsKey(resource))
            return false;
        
        if (_resources[resource] >= amount)
        {
            _resources[resource] -= amount;
            return true;
        }
        
        return false;
    }
    
    /// <summary>
    /// Prüft, ob Ressourcen ausreichen
    /// </summary>
    public bool HasEnough(Dictionary<Resource, int> costs)
    {
        foreach (var (resource, amount) in costs)
        {
            if (!_resources.TryGetValue(resource, out int current) || current < amount)
                return false;
        }
        return true;
    }
    
    /// <summary>
    /// Kostet eine Einheit oder ein Gebäude
    /// </summary>
    public bool PayCost(Dictionary<Resource, int> costs)
    {
        if (!HasEnough(costs))
            return false;
        
        foreach (var (resource, amount) in costs)
        {
            _resources[resource] -= amount;
        }
        
        return true;
    }
}

/// <summary>
/// Ressourcen-Quelle im Spiel
/// </summary>
public sealed class ResourceSource
{
    /// <summary>
    /// Typ der Ressource
    /// </summary>
    public Resource Resource { get; }
    
    /// <summary>
    /// Verbleibende Menge
    /// </summary>
    public int Amount { get; private set; }
    
    /// <summary>
    /// Position der Ressource
    /// </summary>
    public Position Position { get; }
    
    /// <summary>
    /// Anzahl der Sammler
    /// </summary>
    public int CollectorCount { get; set; }
    
    /// <summary>
    /// Ist die Ressource erschöpft?
    /// </summary>
    public bool IsDepleted => Amount <= 0;
    
    public ResourceSource(Resource resource, int amount, Position position)
    {
        Resource = resource;
        Amount = amount;
        Position = position;
    }
    
    /// <summary>
    /// Entnimmt Ressourcen
    /// </summary>
    public int Harvest(int amount)
    {
        int harvested = Math.Min(Amount, amount);
        Amount -= harvested;
        return harvested;
    }
}

/// <summary>
/// Abgabestelle für Ressourcen (Gebäude)
/// </summary>
public sealed class ResourceDropOffPoint
{
    /// <summary>
    /// Das Gebäude, das die Abgabe empfängt
    /// </summary>
    public BuildingEntity Building { get; }
    
    /// <summary>
    /// Typ der Abgabe
    /// </summary>
    public ResourceDropOffType Type { get; }
    
    public ResourceDropOffPoint(BuildingEntity building, ResourceDropOffType type)
    {
        Building = building;
        Type = type;
    }
}

/// <summary>
/// Typ der Abgabestelle
/// </summary>
public enum ResourceDropOffType
{
    None,
    TownCenter,
    Mill,
    LumberCamp,
    MiningCamp
}

/// <summary>
/// Dorfbewohner-Logik und Sammel-Loop
/// Implementiert die Prozess-Schleife aus der Spezifikation
/// </summary>
public sealed class VillagerLogic
{
    private const int LOAD_CAPACITY = 10;  // Traglast eines Dorfbewohners
    
    /// <summary>
    /// Sammelt Ressourcen von einer Quelle
    /// </summary>
    public static (int collected, bool sourceDepleted) Collect(
        Villager villager,
        ResourceSource source,
        ResourceDropOffPoint dropOff)
    {
        // 1. Sammeln bis Traglast erreicht
        int toCollect = LOAD_CAPACITY;
        int collected = source.Harvest(toCollect);
        
        if (collected > 0)
        {
            // Ressource zum dropOff bringen
            // In der Realität würde man das an einen ResourceManager übergeben
            // dropOff.Building.OwnerId hier verwenden für Resource-Add
            
            // Hier würde man die Ressource zum Spieler-Pool hinzufügen
            // In einer echten Implementierung würde das über ein central Manager laufen
            
            // Automatische Fortsetzung: Dorfbewohner geht zurück zur Quelle
            // (Das passiert im Spiel durch die UI-Auswahl, hier simuliert)
        }
        
        return (collected, source.IsDepleted);
    }
    
    /// <summary>
    /// Baut ein Gebäude
    /// </summary>
    public static (bool success, int progress) Build(
        List<Villager> builders,
        BuildingEntity building,
        int progress)
    {
        // Mehrere Dorfbewohner bauen schneller (aber mit abnehmendem Ertrag)
        int totalBuildersWork = builders.Count * 10;  // Beispielwert
        
        return (true, progress + totalBuildersWork);
    }
    
    /// <summary>
    /// Wählt die nächste Ressource für einen Dorfbewohner aus
    /// (wenn die aktuelle erschöpft ist)
    /// </summary>
    public static ResourceSource? FindNextSource(
        Villager villager,
        List<ResourceSource> availableSources,
        Resource resourceType)
    {
        // Finde nächsten ähnlichen Quelle
        foreach (var source in availableSources)
        {
            if (source.Resource == resourceType && !source.IsDepleted)
            {
                // Wenn Quelle nahe genug ist oder nicht blockiert
                return source;
            }
        }
        
        return null;
    }
    
    /// <summary>
    /// Findet den nächsten freien Pla für ein Gebäude
    /// </summary>
    public static Position FindBuildLocation(
        BuildingType type,
        Position center,
        List<Position> occupiedPositions)
    {
        // Einfache Linear-Scan-Suche in einem Radius
        for (int radius = 1; radius <= 5; radius++)
        {
            for (int x = -radius; x <= radius; x++)
            {
                for (int y = -radius; y <= radius; y++)
                {
                    var pos = new Position(center.X + x, center.Y + y);
                    
                    // Prüfe auf Kollision
                    bool collision = false;
                    foreach (var occupied in occupiedPositions)
                    {
                        if (occupied.X == pos.X && occupied.Y == pos.Y)
                        {
                            collision = true;
                            break;
                        }
                    }
                    
                    if (!collision)
                        return pos;
                }
            }
        }
        
        return center;
    }
}

/// <summary>
/// Ressourcen-Manager für das Spiel
/// </summary>
public sealed class ResourceManager
{
    private readonly Dictionary<Resource, int> _globalResources;
    private readonly List<ResourceSource> _sources;
    
    public ResourceManager()
    {
        _globalResources = new Dictionary<Resource, int>
        {
            [Resource.Food] = 0,
            [Resource.Wood] = 0,
            [Resource.Gold] = 0,
            [Resource.Stone] = 0
        };
        
        _sources = new List<ResourceSource>();
    }
    
    /// <summary>
    /// Fügt eine Ressourcenquelle hinzu
    /// </summary>
    public void AddSource(ResourceSource source)
    {
        _sources.Add(source);
    }
    
    /// <summary>
    /// Gibt alle Quellen eines Typs zurück
    /// </summary>
    public List<ResourceSource> GetSources(Resource resource)
    {
        return _sources.Where(s => s.Resource == resource).ToList();
    }
    
    /// <summary>
    /// Gibt alle Quellen in der Nähe zurück
    /// </summary>
    public List<ResourceSource> GetSourcesInRadius(Resource resource, Position center, int radius)
    {
        return _sources.Where(s => 
            s.Resource == resource && 
            s.Position.DistanceSquared(center) <= radius * radius &&
            !s.IsDepleted).ToList();
    }
}
