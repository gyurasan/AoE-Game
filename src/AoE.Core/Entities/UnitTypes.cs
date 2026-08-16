using System;
using System.Collections.Generic;

namespace AoE.Core.Entities;

/// <summary>
/// Spezifische Einheiten-Typen
/// </summary>
public sealed class Villager : UnitEntity
{
    public override string Type => "Villager";
    
    private const int BASE_HP = 25;
    private const int BASE_ATTACK = 3;
    
    public Villager(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Gathering;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 0.9f,
            VisionRange = 3
        };
        return stats;
    }
}

/// <summary>
/// Späher (Reiter)
/// </summary>
public sealed class Scout : UnitEntity
{
    public override string Type => "Scout";
    
    private const int BASE_HP = 45;
    private const int BASE_ATTACK = 3;
    
    public Scout(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 1.2f,
            VisionRange = 7
        };
        return stats;
    }
}

/// <summary>
/// Infanterie - Miliz
/// </summary>
public sealed class Militia : UnitEntity
{
    public override string Type => "Militia";
    
    private const int BASE_HP = 40;
    private const int BASE_ATTACK = 4;
    
    public Militia(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 0.8f,
            VisionRange = 4
        };
        // Bonus gegen Kavallerie
        stats.AddAttackBonus(UnitClass.Cavalry, 12);
        return stats;
    }
}

/// <summary>
/// Speerkämpfer
/// </summary>
public sealed class SpearMan : UnitEntity
{
    public override string Type => "Spearman";
    
    private const int BASE_HP = 45;
    private const int BASE_ATTACK = 3;
    
    public SpearMan(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 0.75f,
            VisionRange = 4
        };
        // Spezial: Bonus gegen Kavallerie
        stats.AddAttackBonus(UnitClass.Cavalry, 15);
        stats.AddArmorBonus(UnitClass.Cavalry, 1);
        return stats;
    }
}

/// <summary>
/// Bogenschütze
/// </summary>
public sealed class Archer : UnitEntity
{
    public override string Type => "Archer";
    
    private const int BASE_HP = 30;
    private const int BASE_ATTACK = 4;
    
    public Archer(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 4,
            Speed = 0.7f,
            VisionRange = 5
        };
        return stats;
    }
}

/// <summary>
/// Skirmisher
/// </summary>
public sealed class Skirmisher : UnitEntity
{
    public override string Type => "Skirmisher";
    
    private const int BASE_HP = 30;
    private const int BASE_ATTACK = 2;
    
    public Skirmisher(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 4,
            Speed = 0.75f,
            VisionRange = 5
        };
        // Spezial: Bonus gegen Bogenschützen
        stats.AddAttackBonus(UnitClass.Archer, 3);
        stats.AddArmorBonus(UnitClass.Archer, 3);
        return stats;
    }
}

/// <summary>
/// Ritter (Kavallerie)
/// </summary>
public sealed class Knight : UnitEntity
{
    public override string Type => "Knight";
    
    private const int BASE_HP = 100;
    private const int BASE_ATTACK = 10;
    
    public Knight(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 1.0f,
            VisionRange = 5
        };
        // Bonus gegen Bogenschützen
        stats.AddAttackBonus(UnitClass.Archer, 5);
        return stats;
    }
}

/// <summary>
/// Kamelreiter
/// </summary>
public sealed class CamelRider : UnitEntity
{
    public override string Type => "CamelRider";
    
    private const int BASE_HP = 100;
    private const int BASE_ATTACK = 5;
    
    public CamelRider(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 1.0f,
            VisionRange = 5
        };
        // Spezial: Bonus gegen Kavallerie
        stats.AddAttackBonus(UnitClass.Cavalry, 9);
        return stats;
    }
}

/// <summary>
/// Rammböcker
/// </summary>
public sealed class Ram : UnitEntity
{
    public override string Type => "Ram";
    
    private const int BASE_HP = 175;
    private const int BASE_ATTACK = 2;
    
    public Ram(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 0,
            Speed = 0.6f,
            VisionRange = 3
        };
        // Spezial: riesiger Bonus gegen Gebäude
        stats.AddAttackBonus(UnitClass.Building, 125);
        return stats;
    }
}

/// <summary>
/// Mangonel (Belagerung)
/// </summary>
public sealed class Mangonel : UnitEntity
{
    public override string Type => "Mangonel";
    
    private const int BASE_HP = 50;
    private const int BASE_ATTACK = 40;
    
    public Mangonel(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 7,
            Speed = 0.5f,
            VisionRange = 4
        };
        return stats;
    }
}

/// <summary>
/// Trebuchet (Großes Belagerungsgerät)
/// </summary>
public sealed class Trebuchet : UnitEntity
{
    public override string Type => "Trebuchet";
    
    private const int BASE_HP = 150;
    private const int BASE_ATTACK = 200;
    
    public Trebuchet(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Guarding;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            BaseAttack = BASE_ATTACK,
            Range = 16,
            Speed = 0.4f,
            VisionRange = 6
        };
        // Spezial: massiver Bonus gegen Gebäude
        stats.AddAttackBonus(UnitClass.Building, 400);
        return stats;
    }
}

/// <summary>
/// Mönch
/// </summary>
public sealed class Monk : UnitEntity
{
    public override string Type => "Monk";
    
    private const int BASE_HP = 30;
    
    public Monk(int ownerId, Position position) 
        : base(ownerId, position, CreateStats())
    {
        State = UnitState.Idle;
    }
    
    private static UnitStats CreateStats()
    {
        var stats = new UnitStats
        {
            HitPoints = BASE_HP,
            Range = 0,
            Speed = 0.85f,
            VisionRange = 4
        };
        return stats;
    }
}
