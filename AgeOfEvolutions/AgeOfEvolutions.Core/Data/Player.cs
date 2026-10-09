using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using AoE.Core.Economy;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Represents the player's state in the game
/// </summary>
public class Player
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Civilization { get; set; }
    
    // Ressourcen: eine ResourcePool aus AoE.Core statt eines eigenen Dictionary.
    // Startkapital und Verrechnung kommen damit aus der getesteten Kernbibliothek.
    public ResourcePool Resources { get; } = new ResourcePool();
    
    // Population
    public int Population { get; set; }
    // Bevölkerungsgrenze: rechnet RTSGameplayScreen.UpdatePopulationLimits aus
    // den Gebäuden des Spielers (Stadtzentrum und Haus je 5, höchstens 200)
    public int PopulationLimit { get; set; }
    public int PopulationCount { get; set; }
    
    // Civilization bonus
    public string CivilizationBonus { get; set; }
    
    // Town center location
    public Vector2 TownCenterPosition { get; set; }
    
    // Zeitalter und Aufstieg - Regeln und Ablauf aus AoE.Core (AgeRules, AgeProgress)
    public AgeProgress Ages { get; } = new AgeProgress();

    // Forschungen (P2): was erforscht ist und was gerade läuft - Regeln und Wirkung aus
    // AoE.Core (TechRules, TechProgress, TechEffects)
    public TechProgress Techs { get; } = new TechProgress();
    
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
        
        // Startkapital setzt die ResourcePool selbst: 200 Nahrung, 200 Holz,
        // 100 Gold, 200 Stein. Hier nichts nachlegen - ResourcePool.Add addiert
        // und ueberschreibt nicht, sonst stuende das Startkapital doppelt.
        
        // Setup civilization
        SetupCivilization();
    }
    
    private void SetupCivilization()
    {
        switch (Civilization.ToLower())
        {
            case "briten":
                CivilizationBonus = "Langbogen 25% schneller";
                Resources.Add(Resource.Wood, 50);
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
    public bool HasResources(Dictionary<Resource, int> costs)
        => Resources.HasEnough(costs);
    
    /// <summary>
    /// Bucht die Kosten ab. Gibt false zurueck, wenn das Guthaben nicht reicht -
    /// anders als zuvor kann der Kontostand damit nicht mehr negativ werden.
    /// </summary>
    public bool PayResources(Dictionary<Resource, int> costs)
        => Resources.PayCost(costs);
    
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
}
