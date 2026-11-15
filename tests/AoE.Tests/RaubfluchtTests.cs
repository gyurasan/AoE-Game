using AgeOfEvolutions.Core.Data;
using AoE.Core.Economy;
using Xunit;
using CorePosition = AoE.Core.Entities.Position;
using Resource = AoE.Core.Entities.Resource;

namespace AoE.Tests;

/// <summary>
/// Flucht vor Raubtieren (Nutzer 2026-11-13: „Raubtiere können übrigens auch Flucht
/// auslösen“). Hier der Kern: die Gefahrenzone eines Bären oder Wolfs
/// (TileMap.InPredatorDanger) und dass die Quellensuche sie überspringt - sonst liefe ein
/// geflohener Dorfbewohner immer wieder in den Biss. Die Flucht selbst prüft
/// tools/spielablauf in der Gruppe raubflucht.
/// </summary>
public class RaubfluchtTests
{
    private const int TS = 32;

    private static TileMap Leer()
    {
        var s = new MapSettings();
        s.BearWanderSecondsMin = s.BearWanderSecondsMax = 10000f;
        s.WolfWanderSecondsMin = s.WolfWanderSecondsMax = 10000f;
        return TileMap.CreateBlank(30, 30, TS, s);
    }

    /// <summary>Eine Goldkachel auf der Wiese - begehbar, also immer bearbeitbar.</summary>
    private static void Gold(TileMap karte, int x, int y)
    {
        var t = karte.GetTile(x, y);
        t.ResourceType = Resource.Gold;
        t.ResourceAmount = 100;
    }

    [Fact]
    public void GefahrenzoneReichtBissweitePlusEineKachel()
    {
        var karte = Leer();
        Assert.True(karte.PlacePredator(FoodSource.Wolf, 10, 10));
        float radius = karte.Settings.PredatorReachTiles + TileMap.PREDATOR_DANGER_EXTRA_TILES;
        Assert.Equal(2.25f, radius, 3);

        Assert.True(karte.InPredatorDanger(10, 10));    // auf dem Tier
        Assert.True(karte.InPredatorDanger(11, 11));    // diagonal daneben (1,41)
        Assert.True(karte.InPredatorDanger(12, 10));    // 2,0
        Assert.True(karte.InPredatorDanger(8, 11));     // 2,24 - knapp drin
        Assert.False(karte.InPredatorDanger(12, 12));   // 2,83
        Assert.False(karte.InPredatorDanger(13, 10));   // 3,0
        Assert.False(karte.InPredatorDanger(10, 7));    // 3,0 nach oben
        Assert.False(karte.InPredatorDanger(20, 20));
    }

    [Fact]
    public void BaerGiltEbenso_ErlegterBaerNicht()
    {
        var karte = Leer();
        Assert.True(karte.PlacePredator(FoodSource.Bear, 10, 10));
        Assert.True(karte.InPredatorDanger(11, 10));
        Assert.True(karte.InPredatorDanger(9, 12));

        karte.GetTile(10, 10).Animal.Slaughtered = true;   // erlegt: nur noch Fleisch
        Assert.False(karte.InPredatorDanger(11, 10));
        Assert.False(karte.InPredatorDanger(10, 10));
    }

    [Fact]
    public void TotesTierOderLeereKachelZaehltNicht()
    {
        var karte = Leer();
        Assert.True(karte.PlacePredator(FoodSource.Wolf, 10, 10));
        karte.GetTile(10, 10).Animal.Health = 0;
        Assert.False(karte.InPredatorDanger(11, 10));

        // Steht das Tier nicht mehr auf seiner Kachel (umgezogen, erlegt und verschwunden),
        // zählt die alte Lage nicht
        var karte2 = Leer();
        Assert.True(karte2.PlacePredator(FoodSource.Wolf, 10, 10));
        var t = karte2.GetTile(10, 10);
        t.Food = FoodSource.None;
        t.Animal = null;
        Assert.False(karte2.InPredatorDanger(11, 10));
    }

    [Fact]
    public void WildschweinIstKeineGefahrenzone()
    {
        var karte = Leer();
        var t = karte.GetTile(10, 10);
        t.Food = FoodSource.Boar;
        t.ResourceType = Resource.Food;
        t.ResourceAmount = karte.Settings.BoarFood;
        t.Animal = new WildAnimal { Health = karte.PredatorMaxHealth(FoodSource.Boar), WanderTimer = 1000f };
        karte.RebuildWildAnimals();
        Assert.False(karte.InPredatorDanger(10, 10));
        Assert.False(karte.InPredatorDanger(11, 10));
    }

    [Fact]
    public void GefahrenzoneAendertNichts()
    {
        var karte = Leer();
        Assert.True(karte.PlacePredator(FoodSource.Wolf, 10, 10));
        var tier = karte.GetTile(10, 10).Animal;
        int hp = tier.Health;
        karte.InPredatorDanger(11, 10);
        karte.InPredatorDanger(25, 25);
        Assert.Equal(hp, tier.Health);
        Assert.Equal((10, 10), karte.LocatePredator(tier));
    }

    [Fact]
    public void QuellensucheUeberspringtDieGefahrenzone()
    {
        var karte = Leer();
        Gold(karte, 12, 10);   // 2 Kacheln vom Wolf - gefährlich
        Gold(karte, 16, 10);   // 6 Kacheln vom Wolf - sicher
        var welt = new TileMapGatherWorld(karte);

        // Ohne Wolf ist die nahe Kachel die nächste
        Assert.Equal(new CorePosition(12, 10), welt.FindNearestSource(new CorePosition(12, 10), Resource.Gold, GatherJob.SEARCH_RADIUS));

        Assert.True(karte.PlacePredator(FoodSource.Wolf, 10, 10));
        Assert.Equal(new CorePosition(16, 10), welt.FindNearestSource(new CorePosition(12, 10), Resource.Gold, GatherJob.SEARCH_RADIUS));

        // Ist der Wolf erlegt und fort, gilt die nahe Kachel wieder
        var t = karte.GetTile(10, 10);
        t.Animal.Health = 0;
        Assert.Equal(new CorePosition(12, 10), welt.FindNearestSource(new CorePosition(12, 10), Resource.Gold, GatherJob.SEARCH_RADIUS));
    }

    [Fact]
    public void LiegtAllesInDerGefahrenzone_GibtEsKeineQuelle()
    {
        var karte = Leer();
        Gold(karte, 11, 10);
        Gold(karte, 9, 11);
        Assert.True(karte.PlacePredator(FoodSource.Bear, 10, 10));
        var welt = new TileMapGatherWorld(karte);
        Assert.Null(welt.FindNearestSource(new CorePosition(11, 10), Resource.Gold, GatherJob.SEARCH_RADIUS));
    }
}
