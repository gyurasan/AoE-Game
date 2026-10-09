using System;
using System.Collections.Generic;

namespace AoE.Core.Entities;

/// <summary>
/// Gebäude-Typen im Spiel
/// </summary>
public enum BuildingType
{
    /// <summary>
    /// Stadtzentrum - Startgebäude
    /// </summary>
    TownCenter,
    
    /// <summary>
    /// Haus - Bevölkerungslimit
    /// </summary>
    House,
    
    /// <summary>
    /// Mühle - Nahrung Abgabestelle
    /// </summary>
    Mill,
    
    /// <summary>
    /// Holzfällerlager - Holz Abgabestelle
    /// </summary>
    LumberCamp,
    
    /// <summary>
    /// Bergbaulager -Gold/Stein Abgabestelle
    /// </summary>
    MiningCamp,
    
    /// <summary>
    /// Farm - Nahrung Produktion
    /// </summary>
    Farm,
    
    /// <summary>
    /// Kaserne - Infanterie Produktion
    /// </summary>
    Barracks,
    
    /// <summary>
    /// Schießstand - Bogenschützen Produktion
    /// </summary>
    ArcheryRange,
    
    /// <summary>
    /// Stall - Kavallerie Produktion
    /// </summary>
    Stable,
    
    /// <summary>
    /// Belagerungswerkstatt
    /// </summary>
    SiegeWorkshop,
    
    /// <summary>
    /// Schmiede - Upgrades
    /// </summary>
    Blacksmith,
    
    /// <summary>
    /// Universität - Technologien
    /// </summary>
    University,
    
    /// <summary>
    /// Kloster - Mönche
    /// </summary>
    Monastery,
    
    /// <summary>
    /// Markt - Handel
    /// </summary>
    Market,
    
    /// <summary>
    /// Burg - Elite Einheiten
    /// </summary>
    Castle,
    
    /// <summary>
    /// Wachturm
    /// </summary>
    Tower,
    
    /// <summary>
    /// Palisadenmauer
    /// </summary>
    PalisadeWall,
    
    /// <summary>
    /// Steinmauer
    /// </summary>
    StoneWall,
    
    /// <summary>
    /// Wunder
    /// </summary>
    Wonder
}

/// <summary>
/// Abgabestelle für Ressourcen
/// </summary>
public enum ResourceDropOff
{
    None,
    TownCenter,
    Mill,
    LumberCamp,
    MiningCamp
}

/// <summary>
/// Gebäude im Spiel
/// </summary>
public sealed class BuildingEntity : UnitEntity
{
    public override string Type => "Building";
    
    /// <summary>
    /// Typ des Gebäudes
    /// </summary>
    public BuildingType BuildingType { get; }
    
    /// <summary>
    /// Abgabestelle für Ressourcen
    /// </summary>
    public ResourceDropOff DropOffType { get; }
    
    /// <summary>
    /// Ist das Gebäude beschäftigt?
    /// </summary>
    public bool IsBusy { get; set; }

    /// <summary>
    /// Noch Baustelle: das Gebäude steht schon auf der Karte und sperrt seine
    /// Kacheln, sieht aber nichts - Sicht spendet es erst fertig gebaut
    /// (<see cref="AoE.Core.Map.VisibilitySystem"/>).
    /// </summary>
    public bool IsUnderConstruction { get; set; }
    
    public BuildingEntity(
        BuildingType type, 
        int ownerId, 
        Position position, 
        UnitStats stats) 
        : base(ownerId, position, stats)
    {
        BuildingType = type;
        State = UnitState.Idle;
        
        // Abgabestellen zuordnen
        DropOffType = type switch
        {
            BuildingType.TownCenter => ResourceDropOff.TownCenter,
            BuildingType.Mill => ResourceDropOff.Mill,
            BuildingType.LumberCamp => ResourceDropOff.LumberCamp,
            BuildingType.MiningCamp => ResourceDropOff.MiningCamp,
            _ => ResourceDropOff.None
        };
    }
    
    public static BuildingEntity CreateTownCenter(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 500,
            BaseAttack = 0,
            BaseArmor = 2,
            Range = 0,
            Speed = 0,
            VisionRange = 5
        };
        return new BuildingEntity(BuildingType.TownCenter, ownerId, position, stats);
    }
    
    public static BuildingEntity CreateHouse(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 400,
            BaseAttack = 0,
            BaseArmor = 1,
            Range = 0,
            Speed = 0,
            VisionRange = 3
        };
        return new BuildingEntity(BuildingType.House, ownerId, position, stats);
    }
    
    public static BuildingEntity CreateMill(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 400,
            BaseAttack = 0,
            BaseArmor = 1,
            Range = 0,
            Speed = 0,
            VisionRange = 3
        };
        return new BuildingEntity(BuildingType.Mill, ownerId, position, stats);
    }
    
    public static BuildingEntity CreateLumberCamp(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 400,
            BaseAttack = 0,
            BaseArmor = 1,
            Range = 0,
            Speed = 0,
            VisionRange = 3
        };
        return new BuildingEntity(BuildingType.LumberCamp, ownerId, position, stats);
    }

    public static BuildingEntity CreateMiningCamp(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 400,
            BaseAttack = 0,
            BaseArmor = 1,
            Range = 0,
            Speed = 0,
            VisionRange = 3
        };
        return new BuildingEntity(BuildingType.MiningCamp, ownerId, position, stats);
    }

    public static BuildingEntity CreateBarracks(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 400,
            BaseAttack = 0,
            BaseArmor = 2,
            Range = 0,
            Speed = 0,
            VisionRange = 4
        };
        return new BuildingEntity(BuildingType.Barracks, ownerId, position, stats);
    }

    /// <summary>
    /// Wachturm, das erste Gebäude der Feudalzeit. Er sieht 10 Kacheln weit -
    /// doppelt so weit wie das Stadtzentrum -, Angriff und Reichweite sind für
    /// die spätere Kampfschleife hinterlegt. Werte aus AoE II.
    /// </summary>
    public static BuildingEntity CreateTower(int ownerId, Position position)
    {
        var stats = new UnitStats
        {
            HitPoints = 1020,
            BaseAttack = 5,
            BaseArmor = 1,
            Range = 8,
            Speed = 0,
            VisionRange = 10
        };
        return new BuildingEntity(BuildingType.Tower, ownerId, position, stats);
    }

    /// <summary>
    /// Ein Gebäude beliebigen Typs - das Spiel setzt darüber jedes Gebäude des
    /// Baumenüs. Für Stadtzentrum, Haus, Mühle, Holzfällerlager, Bergbaulager,
    /// Kaserne und Wachturm genau die Werte der Create-Methode des Typs (sie
    /// aufrufen). Für die übrigen Typen gelten Werte nach AoE II; BaseAttack,
    /// Range und Speed sind 0, wo nichts anderes steht:
    ///
    ///   Typ             HitPoints  BaseArmor  VisionRange
    ///   ArcheryRange       1500        1           5
    ///   Stable             1500        1           5
    ///   Blacksmith         1800        1           5
    ///   Market             2100        1           6
    ///   SiegeWorkshop      2100        1           5
    ///   University         2100        1           6
    ///   Monastery          2100        1           6
    ///   Castle             4800        8          11     BaseAttack 11, Range 8
    ///   PalisadeWall        250        2           2
    ///   StoneWall          1800        8           2
    ///   Wonder             4800        3           8
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="type"/> ist <see cref="BuildingType.Farm"/> - eine Farm ist
    /// ein Feld, kein Gebäude - oder kein Wert der Aufzählung.
    /// </exception>
    public static BuildingEntity Create(BuildingType type, int ownerId, Position position)
    {
        // Gebäude mit eigener Create-Methode: deren Werte gelten unverändert.
        switch (type)
        {
            case BuildingType.TownCenter:
                return CreateTownCenter(ownerId, position);
            case BuildingType.House:
                return CreateHouse(ownerId, position);
            case BuildingType.Mill:
                return CreateMill(ownerId, position);
            case BuildingType.LumberCamp:
                return CreateLumberCamp(ownerId, position);
            case BuildingType.MiningCamp:
                return CreateMiningCamp(ownerId, position);
            case BuildingType.Barracks:
                return CreateBarracks(ownerId, position);
            case BuildingType.Tower:
                return CreateTower(ownerId, position);
        }

        // Eine Farm ist ein Feld, kein Gebäude.
        if (type == BuildingType.Farm)
            throw new ArgumentOutOfRangeException(nameof(type), type, "Eine Farm ist ein Feld, kein Gebäude.");

        // Übrige Gebäude: Werte nach AoE II, BaseAttack und Range 0, außer bei der Burg.
        var stats = type switch
        {
            BuildingType.ArcheryRange => new UnitStats { HitPoints = 1500, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 5 },
            BuildingType.Stable => new UnitStats { HitPoints = 1500, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 5 },
            BuildingType.Blacksmith => new UnitStats { HitPoints = 1800, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 5 },
            BuildingType.Market => new UnitStats { HitPoints = 2100, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 6 },
            BuildingType.SiegeWorkshop => new UnitStats { HitPoints = 2100, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 5 },
            BuildingType.University => new UnitStats { HitPoints = 2100, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 6 },
            BuildingType.Monastery => new UnitStats { HitPoints = 2100, BaseAttack = 0, BaseArmor = 1, Range = 0, Speed = 0, VisionRange = 6 },
            BuildingType.Castle => new UnitStats { HitPoints = 4800, BaseAttack = 11, BaseArmor = 8, Range = 8, Speed = 0, VisionRange = 11 },
            BuildingType.PalisadeWall => new UnitStats { HitPoints = 250, BaseAttack = 0, BaseArmor = 2, Range = 0, Speed = 0, VisionRange = 2 },
            BuildingType.StoneWall => new UnitStats { HitPoints = 1800, BaseAttack = 0, BaseArmor = 8, Range = 0, Speed = 0, VisionRange = 2 },
            BuildingType.Wonder => new UnitStats { HitPoints = 4800, BaseAttack = 0, BaseArmor = 3, Range = 0, Speed = 0, VisionRange = 8 },
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "Unbekannter Gebäudetyp.")
        };
        return new BuildingEntity(type, ownerId, position, stats);
    }
}
