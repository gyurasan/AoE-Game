using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Unit types for the game
/// </summary>
public enum UnitType
{
    // Worker units
    Villager,
    
    // Military units
    SpearMan,
    Skirmisher,
    Cavalry,
    Archer,
    HeavyInfantry,
    CamelRider,
    CavalryArcher,
    
    // Elite units
    Longbowman,
    ManAtArms,
    Paladin,
    ImperialCamel,
    
    // Siege units
    Ram,
    Catapult,
    Trebuchet,
    
    // Civilian
    Monk,
    
    // Building units (construction)
    Builder
}

/// <summary>
/// Represents a unit in the game
/// </summary>
public class Unit
{
    public Guid Id { get; set; }
    public string Name { get; set; }
    public UnitType Type { get; set; }
    
    // Position
    public Vector2 Position { get; set; }
    public Vector2 TargetPosition { get; set; }
    
    // Stats
    public int Health { get; set; }
    public int MaxHealth { get; set; }
    public int AttackPower { get; set; }
    public int Armor { get; set; }
    public int AttackRange { get; set; }
    public float MovementSpeed { get; set; }
    
    // Combat properties
    public UnitCategory Category { get; set; }
    public DamageType DamageType { get; set; }
    public AttackType AttackType { get; set; }
    
    // State
    public UnitState State { get; set; }
    public bool IsSelected { get; set; }
    
    // Owner
    public int OwnerId { get; set; }
    
    // Pathfinding
    public List<Vector2> Path { get; set; } = new List<Vector2>();
    
    // Resource carrying
    public Resource.Type? CarryingResource { get; set; }
    public int CarryingAmount { get; set; }
    
    // Building related
    public bool IsBuilding { get; set; }
    public string BuildingType { get; set; }
    public float ConstructionProgress { get; set; }
    
    // Constructor
    public Unit(UnitType type, int ownerId)
    {
        Id = Guid.NewGuid();
        Type = type;
        OwnerId = ownerId;
        SetupUnitStats();
    }
    
    private void SetupUnitStats()
    {
        // Default stats based on unit type
        switch (Type)
        {
            case UnitType.Villager:
                Name = "Dorfbewohner";
                MaxHealth = 20;
                AttackPower = 0;
                Armor = 0;
                AttackRange = 0;
                MovementSpeed = 1.0f;
                Category = UnitCategory.Worker;
                DamageType = DamageType.None;
                AttackType = AttackType.None;
                break;
                
            case UnitType.SpearMan:
                Name = "Speerkämpfer";
                MaxHealth = 25;
                AttackPower = 7;
                Armor = 0;
                AttackRange = 1;
                MovementSpeed = 0.9f;
                Category = UnitCategory.Infantry;
                DamageType = DamageType.Pierce;
                AttackType = AttackType.Melee;
                break;
                
            case UnitType.Skirmisher:
                Name = "Skirmisher";
                MaxHealth = 20;
                AttackPower = 5;
                Armor = 1;
                AttackRange = 4;
                MovementSpeed = 1.0f;
                Category = UnitCategory.Archer;
                DamageType = DamageType.Pierce;
                AttackType = AttackType.Ranged;
                break;
                
            case UnitType.Cavalry:
                Name = "Kavallerie";
                MaxHealth = 40;
                AttackPower = 12;
                Armor = 0;
                AttackRange = 1;
                MovementSpeed = 1.2f;
                Category = UnitCategory.Cavalry;
                DamageType = DamageType.Pierce;
                AttackType = AttackType.Melee;
                break;
                
            case UnitType.Archer:
                Name = "Bogenschütze";
                MaxHealth = 20;
                AttackPower = 6;
                Armor = 0;
                AttackRange = 5;
                MovementSpeed = 0.9f;
                Category = UnitCategory.Archer;
                DamageType = DamageType.Pierce;
                AttackType = AttackType.Ranged;
                break;
                
            case UnitType.Ram:
                Name = "Rammbock";
                MaxHealth = 60;
                AttackPower = 30;
                Armor = 2;
                AttackRange = 1;
                MovementSpeed = 0.4f;
                Category = UnitCategory.Siege;
                DamageType = DamageType.Pierce;
                AttackType = AttackType.Melee;
                break;
                
            case UnitType.Catapult:
                Name = "Katapult";
                MaxHealth = 25;
                AttackPower = 40;
                Armor = 0;
                AttackRange = 10;
                MovementSpeed = 0.4f;
                Category = UnitCategory.Siege;
                DamageType = DamageType.Siege;
                AttackType = AttackType.Ranged;
                break;
                
            case UnitType.Monk:
                Name = "Mönch";
                MaxHealth = 20;
                AttackPower = 0;
                Armor = 0;
                AttackRange = 3;
                MovementSpeed = 0.9f;
                Category = UnitCategory.Civilian;
                DamageType = DamageType.Magic;
                AttackType = AttackType.Heal;
                break;
        }
        
        Health = MaxHealth;
    }
    
    // Counter relationship check
    public bool Counters(Unit other)
    {
        // Spearman counters Cavalry
        if (Type == UnitType.SpearMan && other.Category == UnitCategory.Cavalry)
            return true;
        
        // Cavalry counters Archer/Skirmisher
        if (Type == UnitType.Cavalry && 
            (other.Category == UnitCategory.Archer || other.Category == UnitCategory.Worker))
            return true;
        
        // Archer counters Infantry
        if (Type == UnitType.Archer && other.Category == UnitCategory.Infantry)
            return true;
        
        // Skirmisher counters Archer
        if (Type == UnitType.Skirmisher && other.Category == UnitCategory.Archer)
            return true;
        
        return false;
    }
    
    // Check if this unit is countered by another
    public bool IsCounteredBy(Unit other)
    {
        return other.Counters(this);
    }
    
    // Get damage multiplier based on counter relationship
    public float GetDamageMultiplier(Unit defender)
    {
        if (Counters(defender))
            return 2.0f; // Double damage against counter target
        return 1.0f;
    }
}

/// <summary>
/// Unit combat categories
/// </summary>
public enum UnitCategory
{
    Worker,      // Villagers
    Infantry,    // Spearmen, Men-at-arms
    Archer,      // Archers, Skirmishers, CavArcher
    Cavalry,     // LightHorse, Cav, ImperialCamel
    Siege,       // Rams, Catapults
    Civilian     // Monks
}

/// <summary>
/// Types of damage
/// </summary>
public enum DamageType
{
    None,        // No damage (healing, workers)
    Pierce,      // Arrows, cavalry
    Magic,       // Monk healing
    Siege        // Ram, catapult
}

/// <summary>
/// Attack types
/// </summary>
public enum AttackType
{
    None,
    Melee,
    Ranged,
    Heal
}

/// <summary>
/// Unit states
/// </summary>
public enum UnitState
{
    Idle,
    Moving,
    Gathering,
    Building,
    Attacking,
    Returning,
    Guarding,
    Dead
}
