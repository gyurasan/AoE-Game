using System.Collections.Generic;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Bauen aus der Spezifikation: Kosten laut Kapitel „Gebäude", und „mehrere
/// Dorfbewohner an einem Gebäude bauen schneller, aber mit abnehmendem Ertrag,
/// nicht linear". Bauzeiten und Formel stammen aus AoE II.
/// </summary>
public class ConstructionTests
{
    // --- Kosten, Bauzeit, Größe ---------------------------------------------

    [Fact]
    public void Kosten_LautSpezifikation()
    {
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 25 }, BuildingRules.CostOf(BuildingType.House));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 100 }, BuildingRules.CostOf(BuildingType.Mill));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 100 }, BuildingRules.CostOf(BuildingType.LumberCamp));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 100 }, BuildingRules.CostOf(BuildingType.MiningCamp));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 60 }, BuildingRules.CostOf(BuildingType.Farm));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Barracks));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 275, [Resource.Stone] = 100 },
                     BuildingRules.CostOf(BuildingType.TownCenter));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 50, [Resource.Stone] = 125 },
                     BuildingRules.CostOf(BuildingType.Tower));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.ArcheryRange));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Stable));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Market));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Monastery));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 150 }, BuildingRules.CostOf(BuildingType.Blacksmith));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 200 }, BuildingRules.CostOf(BuildingType.SiegeWorkshop));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 200 }, BuildingRules.CostOf(BuildingType.University));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Stone] = 650 }, BuildingRules.CostOf(BuildingType.Castle));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 2 }, BuildingRules.CostOf(BuildingType.PalisadeWall));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Stone] = 5 }, BuildingRules.CostOf(BuildingType.StoneWall));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 1000, [Resource.Stone] = 1000, [Resource.Gold] = 1000 },
                     BuildingRules.CostOf(BuildingType.Wonder));
    }

    [Fact]
    public void Kosten_JederTypKostetEtwas()
    {
        foreach (var typ in System.Enum.GetValues<BuildingType>())
            Assert.NotEmpty(BuildingRules.CostOf(typ));
    }

    [Fact]
    public void Kosten_JederAufrufEinNeuesWoerterbuch()
    {
        var erste = BuildingRules.CostOf(BuildingType.House);
        erste[Resource.Wood] = 999;

        Assert.Equal(25, BuildingRules.CostOf(BuildingType.House)[Resource.Wood]);
    }

    [Theory]
    [InlineData(BuildingType.House, 25f)]
    [InlineData(BuildingType.Mill, 35f)]
    [InlineData(BuildingType.LumberCamp, 35f)]
    [InlineData(BuildingType.MiningCamp, 35f)]
    [InlineData(BuildingType.Farm, 15f)]
    [InlineData(BuildingType.Barracks, 50f)]
    [InlineData(BuildingType.TownCenter, 150f)]
    [InlineData(BuildingType.Tower, 80f)]
    [InlineData(BuildingType.PalisadeWall, 5f)]
    [InlineData(BuildingType.StoneWall, 8f)]
    [InlineData(BuildingType.Blacksmith, 40f)]
    [InlineData(BuildingType.Monastery, 40f)]
    [InlineData(BuildingType.SiegeWorkshop, 40f)]
    [InlineData(BuildingType.ArcheryRange, 50f)]
    [InlineData(BuildingType.Stable, 50f)]
    [InlineData(BuildingType.Market, 60f)]
    [InlineData(BuildingType.University, 60f)]
    [InlineData(BuildingType.Castle, 200f)]
    [InlineData(BuildingType.Wonder, 3500f)]
    public void Bauzeit_MitEinemArbeiter(BuildingType typ, float sekunden)
    {
        Assert.Equal(sekunden, BuildingRules.BuildSecondsOf(typ));
    }

    [Theory]
    [InlineData(BuildingType.TownCenter, 4)]
    [InlineData(BuildingType.Farm, 3)]
    [InlineData(BuildingType.Barracks, 3)]
    [InlineData(BuildingType.House, 2)]
    [InlineData(BuildingType.Mill, 2)]
    [InlineData(BuildingType.LumberCamp, 2)]
    [InlineData(BuildingType.MiningCamp, 2)]
    [InlineData(BuildingType.Tower, 2)]
    [InlineData(BuildingType.PalisadeWall, 1)]
    [InlineData(BuildingType.StoneWall, 1)]
    [InlineData(BuildingType.ArcheryRange, 3)]
    [InlineData(BuildingType.Stable, 3)]
    [InlineData(BuildingType.Blacksmith, 3)]
    [InlineData(BuildingType.Monastery, 3)]
    [InlineData(BuildingType.Market, 4)]
    [InlineData(BuildingType.SiegeWorkshop, 4)]
    [InlineData(BuildingType.University, 4)]
    [InlineData(BuildingType.Castle, 4)]
    [InlineData(BuildingType.Wonder, 5)]
    public void Groesse_InKacheln(BuildingType typ, int kanten)
    {
        Assert.Equal(kanten, BuildingRules.SizeOf(typ));
    }

    // --- Abnehmender Ertrag -------------------------------------------------

    [Theory]
    [InlineData(BuildingType.ArcheryRange, 1500, 1, 5)]
    [InlineData(BuildingType.Stable, 1500, 1, 5)]
    [InlineData(BuildingType.Blacksmith, 1800, 1, 5)]
    [InlineData(BuildingType.Market, 2100, 1, 6)]
    [InlineData(BuildingType.SiegeWorkshop, 2100, 1, 5)]
    [InlineData(BuildingType.University, 2100, 1, 6)]
    [InlineData(BuildingType.Monastery, 2100, 1, 6)]
    [InlineData(BuildingType.Castle, 4800, 8, 11)]
    [InlineData(BuildingType.PalisadeWall, 250, 2, 2)]
    [InlineData(BuildingType.StoneWall, 1800, 8, 2)]
    [InlineData(BuildingType.Wonder, 4800, 3, 8)]
    public void Erzeugen_NeueTypen_WerteNachAoE2(BuildingType typ, int lebenspunkte, int ruestung, int sicht)
    {
        var gebaeude = BuildingEntity.Create(typ, 1, new Position(7, 8));

        Assert.Equal(typ, gebaeude.BuildingType);
        Assert.Equal(1, gebaeude.OwnerId);
        Assert.Equal(new Position(7, 8), gebaeude.Position);
        Assert.Equal(lebenspunkte, gebaeude.Stats.HitPoints);
        Assert.Equal(ruestung, gebaeude.Stats.BaseArmor);
        Assert.Equal(sicht, gebaeude.Stats.VisionRange);
    }

    [Fact]
    public void Erzeugen_Burg_SchiesstWieImOriginal()
    {
        var burg = BuildingEntity.Create(BuildingType.Castle, 0, new Position(3, 3));
        Assert.Equal(11, burg.Stats.BaseAttack);
        Assert.Equal(8, burg.Stats.Range);
    }

    [Fact]
    public void Erzeugen_BisherigeTypen_WieIhreCreateMethode()
    {
        var ort = new Position(4, 5);
        var paare = new (BuildingType Typ, BuildingEntity Bisher)[]
        {
            (BuildingType.TownCenter, BuildingEntity.CreateTownCenter(0, ort)),
            (BuildingType.House, BuildingEntity.CreateHouse(0, ort)),
            (BuildingType.Mill, BuildingEntity.CreateMill(0, ort)),
            (BuildingType.LumberCamp, BuildingEntity.CreateLumberCamp(0, ort)),
            (BuildingType.MiningCamp, BuildingEntity.CreateMiningCamp(0, ort)),
            (BuildingType.Barracks, BuildingEntity.CreateBarracks(0, ort)),
            (BuildingType.Tower, BuildingEntity.CreateTower(0, ort)),
        };
        foreach (var (typ, bisher) in paare)
        {
            var neu = BuildingEntity.Create(typ, 0, ort);
            Assert.Equal(typ, neu.BuildingType);
            Assert.Equal(bisher.Stats.HitPoints, neu.Stats.HitPoints);
            Assert.Equal(bisher.Stats.BaseArmor, neu.Stats.BaseArmor);
            Assert.Equal(bisher.Stats.VisionRange, neu.Stats.VisionRange);
            Assert.Equal(bisher.DropOffType, neu.DropOffType);
        }
    }

    [Fact]
    public void Erzeugen_Farm_IstKeinGebaeude()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => BuildingEntity.Create(BuildingType.Farm, 0, new Position(1, 1)));
    }

    [Fact]
    public void Wachturm_SiehtZehnKachelnWeit()
    {
        // Das erste Gebäude der Feudalzeit; seine Stärke vor dem Kampf ist die
        // Sicht - doppelt so weit wie das Stadtzentrum (5)
        var turm = BuildingEntity.CreateTower(0, new Position(10, 10));
        Assert.Equal(BuildingType.Tower, turm.BuildingType);
        Assert.Equal(10, turm.Stats.VisionRange);
        Assert.Equal(new Position(10, 10), turm.Position);
    }

    [Theory]
    [InlineData(-1, 0f)]
    [InlineData(0, 0f)]
    [InlineData(1, 1f)]
    [InlineData(2, 4f / 3f)]
    [InlineData(3, 5f / 3f)]
    [InlineData(4, 2f)]
    public void Geschwindigkeit_JederWeitereEinDrittel(int arbeiter, float faktor)
    {
        Assert.Equal(faktor, BuildingRules.SpeedFactor(arbeiter), 4);
    }

    // --- Baustelle ----------------------------------------------------------

    [Fact]
    public void NeueBaustelle_BeginntBeiNull()
    {
        var bau = new Construction(25f);

        Assert.Equal(25f, bau.BuildSeconds);
        Assert.Equal(0f, bau.Progress);
        Assert.False(bau.IsComplete);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-5f)]
    public void Baustelle_OhneBauzeit_IstEinFehler(float sekunden)
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Construction(sekunden));
    }

    [Fact]
    public void OhneArbeiter_StehtDieBaustelleStill()
    {
        var bau = new Construction(25f);

        Assert.False(bau.Update(100f, 0));
        Assert.Equal(0f, bau.Progress);
    }

    [Fact]
    public void EinArbeiter_BrauchtDieVolleBauzeit()
    {
        var bau = new Construction(25f);

        Assert.False(bau.Update(10f, 1));
        Assert.Equal(0.4f, bau.Progress, 3);

        Assert.True(bau.Update(16f, 1));   // 26 s > 25 s
        Assert.True(bau.IsComplete);
        Assert.Equal(1f, bau.Progress);
    }

    [Fact]
    public void VierArbeiter_BrauchenDieHalbeZeit()
    {
        var bau = new Construction(25f);

        // 25 s · 3 / 6 = 12,5 s
        Assert.False(bau.Update(12f, 4));
        Assert.True(bau.Update(1f, 4));
    }

    [Fact]
    public void ZweiArbeiter_SindNichtDoppeltSoSchnell()
    {
        // 25 s · 3 / 4 = 18,75 s statt 12,5 s
        var bau = new Construction(25f);

        Assert.False(bau.Update(18.5f, 2));
        Assert.True(bau.Update(0.5f, 2));
    }

    [Fact]
    public void ArbeiterWechseln_DerFortschrittBleibt()
    {
        var bau = new Construction(25f);
        bau.Update(10f, 1);   // 0,4

        Assert.True(bau.Update(8f, 4));   // + 8 · 2 / 25 = 0,64
    }

    [Fact]
    public void Fortschritt_HoechstensEins()
    {
        var bau = new Construction(25f);

        Assert.True(bau.Update(1000f, 3));
        Assert.Equal(1f, bau.Progress);
    }

    [Fact]
    public void FertigeBaustelle_MeldetFertigNurEinmal()
    {
        var bau = new Construction(25f);
        bau.Update(25f, 1);

        Assert.False(bau.Update(10f, 1));
        Assert.True(bau.IsComplete);
        Assert.Equal(1f, bau.Progress);
    }

    // --- Lager als Core-Gebäude ---------------------------------------------

    [Fact]
    public void Holzfaellerlager_NimmtHolz()
    {
        var lager = BuildingEntity.CreateLumberCamp(0, new Position(5, 5));

        Assert.Equal(BuildingType.LumberCamp, lager.BuildingType);
        Assert.Equal(ResourceDropOff.LumberCamp, lager.DropOffType);
        Assert.Equal(0, lager.OwnerId);
    }

    [Fact]
    public void Bergbaulager_NimmtGoldUndStein()
    {
        var lager = BuildingEntity.CreateMiningCamp(1, new Position(5, 5));

        Assert.Equal(BuildingType.MiningCamp, lager.BuildingType);
        Assert.Equal(ResourceDropOff.MiningCamp, lager.DropOffType);
        Assert.Equal(1, lager.OwnerId);
    }
}
