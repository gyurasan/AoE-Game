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
}

/// <summary>
/// Forschungs-Typen
/// </summary>
public enum TechType
{
    /// Wirtschaft
    LinenClothing,
    Cartwright,
    HandCart,
    HorseCollar,
    HeavyPlow,
    CropRotation,
    DoubleAx,
    BowSaw,
    GoldMining,
    GoldShaftMining,
    
    /// Militär
    IronCasting,
    BlastFurnace,
    Chainmail,
    Platemail,
    Arrows,
    Bullets,
    InfantryArmor,
    CavalryArmor,
    Fletching,
    BodkinArrow,
    LeatherArcherArmor,
    RingArcherArmor
}

/// <summary>
/// Technologie im Spiel
/// </summary>
public sealed class Technology
{
    /// <summary>
    /// Typ der Technologie
    /// </summary>
    public TechType Type { get; }
    
    /// <summary>
    /// Kosten für die Forschung
    /// </summary>
    public Dictionary<Resource, int> Cost { get; }
    
    /// <summary>
    /// Dauer in Sekunden
    /// </summary>
    public int DurationSeconds { get; }
    
    /// <summary>
    /// Gebäude, in dem erforscht wird
    /// </summary>
    public BuildingType ResearchBuilding { get; }
    
    /// <summary>
    /// Beschreibung der Technologie
    /// </summary>
    public string Description { get; }
    
    public Technology(
        TechType type,
        Dictionary<Resource, int> cost,
        int durationSeconds,
        BuildingType researchBuilding,
        string description)
    {
        Type = type;
        Cost = cost;
        DurationSeconds = durationSeconds;
        ResearchBuilding = researchBuilding;
        Description = description;
    }
}

/// <summary>
/// Forschungs-Manager für Spieler
/// </summary>
public sealed class TechTree
{
    private readonly HashSet<TechType> _researched = new();
    private readonly Dictionary<TechType, Technology> _technologies;
    
    public TechTree()
    {
        _technologies = InitializeTechnologies();
    }
    
    private Dictionary<TechType, Technology> InitializeTechnologies()
    {
        var techs = new Dictionary<TechType, Technology>
        {
            // Wirtschaft
            [TechType.LinenClothing] = new Technology(
                TechType.LinenClothing,
                new Dictionary<Resource, int> { [Resource.Gold] = 200 },
                35,
                BuildingType.TownCenter,
                "Dorfbewohner +15 HP"),
            
            [TechType.Cartwright] = new Technology(
                TechType.Cartwright,
                new Dictionary<Resource, int> { [Resource.Gold] = 150 },
                40,
                BuildingType.TownCenter,
                "Dorfbewohner trägt mehr, geht schneller"),
            
            [TechType.HorseCollar] = new Technology(
                TechType.HorseCollar,
                new Dictionary<Resource, int> { [Resource.Gold] = 100 },
                35,
                BuildingType.Mill,
                "Farm-Ertrag: 175 → 250"),
            
            [TechType.DoubleAx] = new Technology(
                TechType.DoubleAx,
                new Dictionary<Resource, int> { [Resource.Gold] = 100 },
                35,
                BuildingType.LumberCamp,
                "Holzfällen schneller"),
            
            [TechType.GoldMining] = new Technology(
                TechType.GoldMining,
                new Dictionary<Resource, int> { [Resource.Gold] = 100 },
                35,
                BuildingType.MiningCamp,
                "Gold schneller"),
            
            // Militär - Schmiede
            [TechType.IronCasting] = new Technology(
                TechType.IronCasting,
                new Dictionary<Resource, int> { [Resource.Gold] = 100 },
                30,
                BuildingType.Blacksmith,
                "Angriff +1 (Infanterie)"),
            
            [TechType.Chainmail] = new Technology(
                TechType.Chainmail,
                new Dictionary<Resource, int> { [Resource.Gold] = 100 },
                30,
                BuildingType.Blacksmith,
                "Rüstung +1 (Infanterie)")
        };
        
        return techs;
    }
    
    /// <summary>
    /// Prüft, ob eine Technologie erforscht wurde
    /// </summary>
    public bool IsResearched(TechType type) => _researched.Contains(type);
    
    /// <summary>
    /// Markiert eine Technologie als erforscht
    /// </summary>
    public void MarkResearched(TechType type)
    {
        _researched.Add(type);
    }
    
    /// <summary>
    /// Prüft, ob eine Technologie erforschbar ist
    /// </summary>
    public bool IsResearchable(TechType type, Dictionary<Resource, int> currentResources, int age)
    {
        if (IsResearched(type))
            return false;
        
        var tech = _technologies[type];
        
        // Zeitalter-Prüfung (Vereinfacht)
        if (age < 2 && TypeInAge(type, 1))
            return false;
        
        // Ressourcen-Prüfung
        foreach (var (res, cost) in tech.Cost)
        {
            if (currentResources.TryGetValue(res, out int available) && available < cost)
                return false;
        }
        
        return true;
    }
    
    private bool TypeInAge(TechType type, int age)
    {
        var economyTechs = new[] {
            TechType.LinenClothing, TechType.Cartwright, TechType.HorseCollar,
            TechType.DoubleAx, TechType.GoldMining
        };
        
        var militaryTechs = new[] {
            TechType.IronCasting, TechType.Chainmail
        };
        
        return (age == 1 && economyTechs.Contains(type)) ||
               (age >= 2 && militaryTechs.Contains(type));
    }
}
