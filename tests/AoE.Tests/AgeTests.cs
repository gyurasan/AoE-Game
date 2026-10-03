using System.Collections.Generic;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Zeitalter aus der Spezifikation, Kapitel „Die Zeitalter": der Aufstieg
/// kostet Rohstoffe und Zeit, erst danach gilt das neue Zeitalter, und jedes
/// Zeitalter schaltet Gebäude frei.
/// </summary>
public class AgeTests
{
    /// <summary>Guthaben, das für jeden Aufstieg bis zur Imperialzeit reicht.</summary>
    private static ResourcePool Reich()
    {
        var pool = new ResourcePool();
        pool.Add(Resource.Food, 5000);
        pool.Add(Resource.Gold, 5000);
        return pool;
    }

    // --- AgeRules -----------------------------------------------------------

    [Theory]
    [InlineData(Age.Feudal, 500, 0)]
    [InlineData(Age.Castle, 800, 200)]
    [InlineData(Age.Imperial, 1000, 800)]
    public void Kosten_LautSpezifikation(Age ziel, int nahrung, int gold)
    {
        var kosten = AgeRules.CostOf(ziel);
        Assert.Equal(nahrung, kosten.GetValueOrDefault(Resource.Food));
        Assert.Equal(gold, kosten.GetValueOrDefault(Resource.Gold));
        Assert.Equal(0, kosten.GetValueOrDefault(Resource.Wood));
        Assert.Equal(0, kosten.GetValueOrDefault(Resource.Stone));
    }

    [Fact]
    public void Kosten_DunkleZeit_Leer()
    {
        Assert.Empty(AgeRules.CostOf(Age.Dark));
    }

    [Fact]
    public void Kosten_JedesMalNeuesWoerterbuch()
    {
        AgeRules.CostOf(Age.Feudal)[Resource.Food] = 1;
        Assert.Equal(500, AgeRules.CostOf(Age.Feudal)[Resource.Food]);
    }

    [Theory]
    [InlineData(Age.Dark, 0f)]
    [InlineData(Age.Feudal, 130f)]
    [InlineData(Age.Castle, 160f)]
    [InlineData(Age.Imperial, 190f)]
    public void Forschungsdauer_LautSpezifikation(Age ziel, float sekunden)
    {
        Assert.Equal(sekunden, AgeRules.ResearchSecondsOf(ziel));
    }

    [Theory]
    [InlineData(Age.Dark, Age.Feudal)]
    [InlineData(Age.Feudal, Age.Castle)]
    [InlineData(Age.Castle, Age.Imperial)]
    public void Naechstes_Zeitalter(Age von, Age nach)
    {
        Assert.Equal(nach, AgeRules.Next(von));
    }

    [Fact]
    public void Naechstes_NachImperialzeit_Keins()
    {
        Assert.Null(AgeRules.Next(Age.Imperial));
    }

    [Theory]
    [InlineData(BuildingType.House, Age.Dark)]
    [InlineData(BuildingType.Mill, Age.Dark)]
    [InlineData(BuildingType.LumberCamp, Age.Dark)]
    [InlineData(BuildingType.MiningCamp, Age.Dark)]
    [InlineData(BuildingType.Farm, Age.Dark)]
    [InlineData(BuildingType.Barracks, Age.Dark)]
    [InlineData(BuildingType.PalisadeWall, Age.Dark)]
    [InlineData(BuildingType.ArcheryRange, Age.Feudal)]
    [InlineData(BuildingType.Stable, Age.Feudal)]
    [InlineData(BuildingType.Market, Age.Feudal)]
    [InlineData(BuildingType.Blacksmith, Age.Feudal)]
    [InlineData(BuildingType.Tower, Age.Feudal)]
    [InlineData(BuildingType.StoneWall, Age.Feudal)]
    [InlineData(BuildingType.TownCenter, Age.Castle)]
    [InlineData(BuildingType.Castle, Age.Castle)]
    [InlineData(BuildingType.University, Age.Castle)]
    [InlineData(BuildingType.Monastery, Age.Castle)]
    [InlineData(BuildingType.SiegeWorkshop, Age.Castle)]
    [InlineData(BuildingType.Wonder, Age.Imperial)]
    public void Gebaeude_BrauchenZeitalter(BuildingType typ, Age ab)
    {
        Assert.Equal(ab, AgeRules.RequiredAgeOf(typ));
    }

    [Fact]
    public void Freigeschaltet_AbDemZeitalter()
    {
        Assert.False(AgeRules.IsUnlocked(BuildingType.Tower, Age.Dark));
        Assert.True(AgeRules.IsUnlocked(BuildingType.Tower, Age.Feudal));
        Assert.True(AgeRules.IsUnlocked(BuildingType.Tower, Age.Imperial));
        Assert.True(AgeRules.IsUnlocked(BuildingType.House, Age.Dark));
        Assert.False(AgeRules.IsUnlocked(BuildingType.Wonder, Age.Castle));
    }

    [Theory]
    [InlineData(Age.Dark, "Dunkle Zeit")]
    [InlineData(Age.Feudal, "Feudalzeit")]
    [InlineData(Age.Castle, "Ritterzeit")]
    [InlineData(Age.Imperial, "Imperialzeit")]
    public void Namen(Age age, string name)
    {
        Assert.Equal(name, AgeRules.NameOf(age));
    }

    // --- AgeProgress --------------------------------------------------------

    [Fact]
    public void Start_InDerDunklenZeit()
    {
        var ages = new AgeProgress();
        Assert.Equal(Age.Dark, ages.Current);
        Assert.Null(ages.Target);
        Assert.False(ages.IsResearching);
        Assert.Equal(0f, ages.Progress);
    }

    [Fact]
    public void Aufstieg_ZuWenigNahrung_AendertNichts()
    {
        var pool = new ResourcePool();   // Startguthaben: 200 Nahrung
        var ages = new AgeProgress();
        Assert.False(ages.TryStart(pool));
        Assert.False(ages.IsResearching);
        Assert.Equal(200, pool[Resource.Food]);
    }

    [Fact]
    public void Aufstieg_ZieltKostenAbUndLaeuft()
    {
        var pool = new ResourcePool();
        pool.Add(Resource.Food, 300);    // 500 Nahrung
        var ages = new AgeProgress();
        Assert.True(ages.TryStart(pool));
        Assert.Equal(0, pool[Resource.Food]);
        Assert.Equal(Age.Feudal, ages.Target);
        Assert.True(ages.IsResearching);
        Assert.Equal(Age.Dark, ages.Current);
        Assert.Equal(0f, ages.Progress);
    }

    [Fact]
    public void Aufstieg_Ritterzeit_BrauchtGold()
    {
        var ages = new AgeProgress();
        var pool = Reich();
        Assert.True(ages.TryStart(pool));
        ages.Update(130f);

        var arm = new ResourcePool();     // 100 Gold reichen nicht für 200
        arm.Add(Resource.Food, 1000);
        Assert.False(ages.TryStart(arm));
        Assert.Equal(1200, arm[Resource.Food]);
        Assert.Equal(100, arm[Resource.Gold]);
        Assert.False(ages.IsResearching);
    }

    [Fact]
    public void Aufstieg_ZweimalStarten_ZahltNurEinmal()
    {
        var pool = Reich();
        var ages = new AgeProgress();
        Assert.True(ages.TryStart(pool));
        int nahrung = pool[Resource.Food];
        Assert.False(ages.TryStart(pool));
        Assert.Equal(nahrung, pool[Resource.Food]);
        Assert.Equal(Age.Feudal, ages.Target);
    }

    [Fact]
    public void Forschung_Halbzeit_NochAltesZeitalter()
    {
        var ages = new AgeProgress();
        ages.TryStart(Reich());
        Assert.False(ages.Update(65f));
        Assert.Equal(0.5f, ages.Progress, 3);
        Assert.Equal(Age.Dark, ages.Current);
        Assert.True(ages.IsResearching);
    }

    [Fact]
    public void Forschung_Fertig_GenauEinmalTrue()
    {
        var ages = new AgeProgress();
        ages.TryStart(Reich());
        Assert.False(ages.Update(100f));
        Assert.True(ages.Update(40f));    // 140 s >= 130 s, Überschuss verfällt
        Assert.Equal(Age.Feudal, ages.Current);
        Assert.Null(ages.Target);
        Assert.False(ages.IsResearching);
        Assert.Equal(0f, ages.Progress);
        Assert.False(ages.Update(1000f));
        Assert.Equal(Age.Feudal, ages.Current);
    }

    [Fact]
    public void Forschung_OhneAufstieg_Nichts()
    {
        var ages = new AgeProgress();
        Assert.False(ages.Update(500f));
        Assert.Equal(Age.Dark, ages.Current);
    }

    [Fact]
    public void Aufstieg_BisZurImperialzeit_DannSchluss()
    {
        var pool = Reich();
        var ages = new AgeProgress();
        foreach (var (ziel, dauer) in new[] { (Age.Feudal, 130f), (Age.Castle, 160f), (Age.Imperial, 190f) })
        {
            Assert.True(ages.TryStart(pool));
            Assert.Equal(ziel, ages.Target);
            Assert.True(ages.Update(dauer));
            Assert.Equal(ziel, ages.Current);
        }
        int nahrung = pool[Resource.Food], gold = pool[Resource.Gold];
        Assert.Equal(5200 - 500 - 800 - 1000, nahrung);
        Assert.Equal(5100 - 200 - 800, gold);
        Assert.False(ages.TryStart(pool));
        Assert.Equal(nahrung, pool[Resource.Food]);
        Assert.Equal(gold, pool[Resource.Gold]);
    }
}
