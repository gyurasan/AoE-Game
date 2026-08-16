using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Represents the player's state in the game
/// </summary>
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Civilization { get; set; }
    
    // Resources
    public Dictionary<Resource.Type, int> Resources { get; set; }
        = new Dictionary<Resource.Type, int>();
    
    // Population
    public int Population { get; set; }
    public int PopulationLimit { get; set; } = 10; // Start with 10 slots (5 houses)
    public int PopulationCount { get; set; }
    
    // Civilization bonus
    public string CivilizationBonus { get; set; }
    
    // Town center location
    public Vector2 TownCenterPosition { get; set; }
    
    // Age
    public string CurrentAge { get; set; } = "Dunkle Zeit";
    
    // Units owned
    public List<Unit> Units { get; set; } = new List<Unit>();
    
    // Buildings owned
    public List<Building> Buildings { get; set; } = new List<Building>();
    
    // Constructor
    public Player(int id, string name, string civilization)
    {
        Id = id;
        Name = name;
        Civilization = civilization;
        
        // Initialize resources
        Resources.Add(Resource.Type.Food, 200);
        Resources.Add(Resource.Type.Wood, 200);
        Resources.Add(Resource.Type.Gold, 100);
        Resources.Add(Resource.Type.Stone, 100);
        
        // Setup civilization
        SetupCivilization();
    }
    
    private void SetupCivilization()
    {
        switch (Civilization.ToLower())
        {
            case "briten":
                CivilizationBonus = "Langbogen 25% schneller";
                Resources[Resource.Type.Wood] += 50;
                break;
            case "franken":
                CivilizationBonus = "Kavallerie 15% schneller";
                break;
            case "mongolen":
                CivilizationBonus = "Ritter 20% günstiger";
                break;
            case "azteken":
                CivilizationBonus = "Dorfbewohner sammeln +15% schneller";
                break;
            default:
                CivilizationBonus = "Kein spezieller Bonus";
                break;
        }
    }
    
    // Resource management
    public bool HasResources(Dictionary<Resource.Type, int> costs)
    {
        foreach (var kvp in costs)
        {
            if (!Resources.ContainsKey(kvp.Key) || Resources[kvp.Key] < kvp.Value)
                return false;
        }
        return true;
    }
    
    public void PayResources(Dictionary<Resource.Type, int> costs)
    {
        foreach (var kvp in costs)
        {
            Resources[kvp.Key] -= kvp.Value;
        }
    }
    
    // Population management
    public bool CanSpawnUnit()
    {
        return PopulationCount < PopulationLimit;
    }
    
    public void AddUnit(Unit unit)
    {
        PopulationCount++;
        Units.Add(unit);
    }
    
    public void RemoveUnit(Unit unit)
    {
        PopulationCount--;
        Units.Remove(unit);
    }
    
    // Age progression
    public Dictionary<Resource.Type, int> GetAgeUpCost(string fromAge, string toAge)
    {
        return toAge.ToLower() switch
        {
            "feudalzeit" => new Dictionary<Resource.Type, int>
            {
                { Resource.Type.Food, 500 },
                { Resource.Type.Wood, 200 }
            },
            "ritterzeit" => new Dictionary<Resource.Type, int>
            {
                { Resource.Type.Food, 800 },
                { Resource.Type.Wood, 600 },
                { Resource.Type.Stone, 200 }
            },
            "imperialzeit" => new Dictionary<Resource.Type, int>
            {
                { Resource.Type.Food, 1000 },
                { Resource.Type.Wood, 800 },
                { Resource.Type.Gold, 400 }
            },
            _ => new Dictionary<Resource.Type, int>()
        };
    }
    
    public bool IsAgeUpAvailable(string toAge)
    {
        return GetAgeUpCost(CurrentAge, toAge).Count > 0;
    }
    
    public void AdvanceAge(string newAge)
    {
        CurrentAge = newAge;
    }
}
