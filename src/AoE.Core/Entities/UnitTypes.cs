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
        // Ein neuer Dorfbewohner ist untätig. Zum Sammeln schickt ihn erst
        // ein Auftrag (GatherJob) — vorher begann jeder im Zustand Gathering.
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
            Speed = 2.7f,
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
        // Bonus gegen Kavallerie (Nutzer 2026-10-18: Reiter sollen die
        // stärkere Nahkämpfter sein — der Miliz-Bonus bleibt als leichter
        // Konter, dreht das Duell aber nicht).
        stats.AddAttackBonus(UnitClass.Cavalry, 6);
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
        // Spezial: Bonus gegen Kavallerie (Nutzer 2026-10-18: Reiter sollen
        // die stärkere Nahkämpfter sein — der Speer-Bonus bleibt als
        // Klassikonter, dreht den 1-zu-1 aber nicht: Ritter 4 Schläge bis
        // der Speer stirbt, Ritter fallen erst im 8.).
        stats.AddAttackBonus(UnitClass.Cavalry, 10);
        // (Frühere Rüstung gegen Kavallerie: +1 — ohne Wirkung, denn der
        // Schaden-Berechner liest Rüstung nur pro Verteidiger-Klasse;
        // entfernt 2026-10-18.)
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
    
    // Nutzer 2026-10-18: „die berittenen Soldaten nicht stark genug —
    // normalerweise sollten sie stärker als die Soldaten zu Fuß sein".
    // Der Ritter ist damit der stärkste Nahkämpfer: gegen Miliz,
    // Speerkämpfer, Bogenschützen und Kamelreiter in weniger Schlägen
    // durch (45/12 vs. 120/14, 45/12 vs. 120/16, 30/29 vs. 120/8, 100/24 vs. 120/19).
    private const int BASE_HP = 120;
    private const int BASE_ATTACK = 12;
    
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
            Speed = 2.25f,
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
            Speed = 2.25f,
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
