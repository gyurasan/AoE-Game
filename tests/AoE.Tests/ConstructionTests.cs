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
    }

    [Fact]
    public void Kosten_UnbekannterTyp_SindLeer()
    {
        Assert.Empty(BuildingRules.CostOf(BuildingType.Wonder));
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
    [InlineData(BuildingType.Wonder, 60f)]
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
    public void Groesse_InKacheln(BuildingType typ, int kanten)
    {
        Assert.Equal(kanten, BuildingRules.SizeOf(typ));
    }

    // --- Abnehmender Ertrag -------------------------------------------------

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
