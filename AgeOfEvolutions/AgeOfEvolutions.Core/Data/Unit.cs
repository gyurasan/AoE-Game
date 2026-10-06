using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using UnitEntity = AoE.Core.Entities.UnitEntity;
using DamageCalculator = AoE.Core.Combat.DamageCalculator;
using GatherJob = AoE.Core.Economy.GatherJob;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Data;

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
    
    /// <summary>
    /// Die zugehörige Einheit aus AoE.Core. Sie hält die Kampfwerte samt
    /// Angriffs- und Rüstungsklassen und ist die einzige Quelle dafür.
    /// </summary>
    public UnitEntity Core { get; private set; }

    // Stats — durchgereicht aus der Kernbibliothek, nicht hier gepflegt
    public int Health
    {
        get => Core.CurrentHp;
        set => Core.CurrentHp = value;
    }
    public int MaxHealth => Core.Stats.HitPoints;
    public int AttackPower => Core.Stats.BaseAttack;
    public int Armor => Core.Stats.BaseArmor;
    public int AttackRange => Core.Stats.Range;
    public float MovementSpeed => Core.Stats.Speed;
    
    // Combat properties
    public UnitCategory Category { get; set; }
    public DamageType DamageType { get; set; }
    public AttackType AttackType { get; set; }
    
    // State
    /// <summary>Zustand der Einheit — gehalten von der Core-Einheit.</summary>
    public UnitState State
    {
        get => Core.State;
        set => Core.State = value;
    }
    public bool IsSelected { get; set; }
    
    // Owner
    public int OwnerId { get; set; }
    
    // Pathfinding
    public List<Vector2> Path { get; set; } = new List<Vector2>();

    /// <summary>
    /// Wie schnell die Einheit gerade geht, als Anteil an ihrem vollen Tempo:
    /// sie fährt an und bremst vor dem Ziel, siehe Gait.Approach und Gait.ArrivalSpeed.
    /// </summary>
    public float Pace { get; set; }
    
    /// <summary>
    /// Laufender Sammelauftrag, oder null. Traglast, Quelle und Abgabestelle
    /// führt der Auftrag aus AoE.Core; die Einheit hält nur die Verknüpfung.
    /// </summary>
    public GatherJob Job { get; set; }

    // Traglast — aus dem Sammelauftrag abgeleitet, nicht doppelt gepflegt
    public Resource? CarryingResource => Job is { Carrying: > 0 } ? Job.Resource : null;
    public int CarryingAmount => Job?.Carrying ?? 0;
    
    /// <summary>
    /// Die Baustelle, an der dieser Dorfbewohner baut oder zu der er
    /// unterwegs ist, oder null. Wie schnell es vorangeht, hängt davon ab,
    /// wie viele dort gerade arbeiten (BuildingRules.SpeedFactor).
    /// </summary>
    public Building BuildSite { get; set; }

    /// <summary>Das fremde Gebäude, das die Einheit auf Befehl angreift, oder null (K2).</summary>
    public Building AttackTarget { get; set; }

    /// <summary>Sekunden seit dem letzten Schlag; ein Schlag alle BuildingCombat.RELOAD_SECONDS.</summary>
    public float AttackTimer { get; set; }
    
    // Constructor
    public Unit(UnitType type, int ownerId)
    {
        Id = Guid.NewGuid();
        Type = type;
        OwnerId = ownerId;
        SetupUnitStats();
    }
    
    /// <summary>
    /// Legt die Core-Einheit an. Die Kampfwerte kommen damit aus AoE.Core;
    /// hier bleiben nur Anzeigename und Rolle, die das Spiel selbst führt.
    /// </summary>
    private void SetupUnitStats()
    {
        Core = CoreUnits.Create(Type, OwnerId);
        Name = CoreUnits.GermanName(Type);
        Category = CoreUnits.CategoryOf(Type);

        AttackType = AttackRange > 0 ? AttackType.Ranged : AttackType.Melee;
        DamageType = AttackPower > 0 ? DamageType.Pierce : DamageType.None;

        Health = MaxHealth;
    }
    
    /// <summary>
    /// Schaden, den diese Einheit dem Ziel zufügt — gerechnet von
    /// <c>AoE.Core.Combat.DamageCalculator</c> nach der AoE-Formel:
    /// je Angriffsklasse <c>max(0, Angriff − Rüstung)</c>, mindestens 1.
    ///
    /// Das Konter-Dreieck steckt damit in den Klassenboni der Einheiten
    /// (Speerkämpfer +15 gegen Kavallerie und so weiter) statt in einem
    /// pauschalen Faktor 2, wie ihn die frühere Fassung hier verwendete.
    /// </summary>
    public int DamageAgainst(Unit defender)
        => DamageCalculator.CalculateDamage(Core, defender.Core);

    /// <summary>
    /// Greift das Ziel an und gibt den zugefügten Schaden zurück.
    /// </summary>
    public int Attack(Unit defender)
    {
        int damage = DamageAgainst(defender);
        defender.Health = System.Math.Max(0, defender.Health - damage);
        if (defender.Health == 0)
            defender.State = UnitState.Dead;
        return damage;
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

