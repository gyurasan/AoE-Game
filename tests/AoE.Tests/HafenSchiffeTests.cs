using System.Collections.Generic;
using System.Linq;
using AoE.Core.Combat;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using MapSizes = AgeOfEvolutions.Core.Data.MapSizes;
using MapStyle = AgeOfEvolutions.Core.Data.MapStyle;
using TileType = AgeOfEvolutions.Core.Data.TileType;

namespace AoE.Tests;

/// <summary>
/// Hafen, Schiffe und Inseln der Wiking-Karte. Der Nutzer (2026-11-07): „In der Wiking Version
/// müssen wir dann noch Hafen erstellen können. Die Inseln müssen da komplett von Wasser umgeben
/// sein und man wird nur mit booten von einer zur nächsten insel kommen“, „es muss am strand
/// möglich sein, also ein Teil im Wasser ein Teil auf land“, „den hafen können wir auch direkt ins
/// Wasser stellen solange er direkt am Stran ist, also 1 kachel maximal von land“, „die Anzahl
/// inseln würde ich so zwischen 4-12 halten“ (12 nur auf der größten Karte, sonst 10).
/// </summary>
public class HafenSchiffeTests
{
    // Leere Karte 30 × 20: links Wiese (x 0..14), rechts Meer (x 15..29)
    private static DataMap Kueste()
    {
        var karte = DataMap.CreateBlank(30, 20, 32, new MapSettings());
        for (int x = 15; x < 30; x++)
            for (int y = 0; y < 20; y++)
                Wasser(karte, x, y);
        karte.InvalidateNavigation();
        return karte;
    }

    private static void Wasser(DataMap karte, int x, int y)
    {
        var t = karte.GetTile(x, y);
        t.Type = TileType.Water;
        t.Walkable = false;
        t.Buildable = false;
    }

    [Fact]
    public void Hafen_halb_im_Wasser_am_Strand_passt()
    {
        // Grundfläche x 14..16: eine Spalte Land, zwei Spalten Wasser
        Assert.True(Kueste().CanPlaceDock(14, 5, 3));
    }

    [Fact]
    public void Hafen_ganz_im_Wasser_direkt_am_Strand_passt()
    {
        // Grundfläche x 15..17 ganz im Wasser, links daneben (x 14) der Strand
        Assert.True(Kueste().CanPlaceDock(15, 5, 3));
    }

    [Fact]
    public void Hafen_eine_Kachel_vor_dem_Strand_passt_nicht()
    {
        // Grundfläche x 16..18: zwischen ihr und dem Strand liegt eine Wasserkachel
        Assert.False(Kueste().CanPlaceDock(16, 5, 3));
    }

    [Fact]
    public void Hafen_ganz_auf_Land_passt_nicht()
    {
        Assert.False(Kueste().CanPlaceDock(5, 5, 3));
    }

    [Fact]
    public void Hafen_mit_nur_zwei_Wasserkacheln_passt_nicht()
    {
        // x 13..15 bei y 5..7, aber nur (15,5) und (15,6) sind Wasser - (15,7) wird Land
        var karte = Kueste();
        var t = karte.GetTile(15, 7);
        t.Type = TileType.Grassland;
        t.Walkable = true;
        t.Buildable = true;
        karte.InvalidateNavigation();
        Assert.False(karte.CanPlaceDock(13, 5, 3));
    }

    [Fact]
    public void Hafen_auf_Wald_am_Strand_passt_nicht()
    {
        var karte = Kueste();
        var t = karte.GetTile(14, 6);
        t.Type = TileType.Forest;
        t.ResourceType = Resource.Wood;
        t.ResourceAmount = 100;
        Assert.False(karte.CanPlaceDock(14, 5, 3));
    }

    [Fact]
    public void Hafen_an_einem_Tuempel_ohne_offenes_Wasser_passt_nicht()
    {
        // Ein Teich von 4 × 4 mitten im Land: nach dem Hafen bleiben 7 befahrbare Kacheln
        var karte = DataMap.CreateBlank(20, 20, 32, new MapSettings());
        for (int x = 5; x < 9; x++)
            for (int y = 5; y < 9; y++)
                Wasser(karte, x, y);
        karte.InvalidateNavigation();
        Assert.False(karte.CanPlaceDock(5, 5, 3));
    }

    [Fact]
    public void Wasserkachel_am_Strand_gehoert_zur_Landmasse_daneben()
    {
        var karte = Kueste();
        int land = karte.LandRegion(14, 5);
        Assert.NotEqual(0, land);
        Assert.Equal(0, karte.LandRegion(15, 5));
        Assert.Equal(land, karte.LandRegionAround(15, 5));
        Assert.Equal(0, karte.LandRegionAround(25, 10));
    }

    [Fact]
    public void Anlegestelle_liegt_am_Ufer_der_Zielinsel()
    {
        var karte = Kueste();
        var spot = karte.Landing(25, 10, 3, 10, karte.LandRegion(3, 10));
        Assert.NotNull(spot);
        var (wasser, land) = spot.Value;
        Assert.True(karte.IsNavigable(wasser.X, wasser.Y));
        Assert.True(karte.IsWalkable(land.X, land.Y));
        Assert.True(System.Math.Abs(wasser.X - land.X) <= 1 && System.Math.Abs(wasser.Y - land.Y) <= 1);
        Assert.Equal(14, land.X);   // die Küste liegt bei x 14
    }

    [Fact]
    public void Ufer_Taschen_ohne_Platz_fuer_alle_zaehlen_nicht()
    {
        // Ecke (0,0)-(1,1) von zwei Häusern abgeriegelt: 4 Kacheln
        var karte = Kueste();
        karte.AddBuilding(2, 0, "Haus", 0, 2);
        karte.AddBuilding(0, 2, "Haus", 0, 2);
        int region = karte.LandRegion(5, 5);
        Assert.Contains(new Point(0, 0), karte.RoomyLand(region, 4));
        Assert.DoesNotContain(new Point(0, 0), karte.RoomyLand(region, 5));
        Assert.Contains(new Point(5, 5), karte.RoomyLand(region, 5));
    }

    [Fact]
    public void Schiffe_haben_ihre_Werte()
    {
        var transport = new TransportShip(0, new Position(0, 0));
        Assert.Equal(5, TransportShip.CAPACITY);
        Assert.Equal(100, transport.Stats.HitPoints);
        Assert.Equal(0, transport.Stats.BaseAttack);

        var krieg = new WarShip(0, new Position(0, 0));
        Assert.Equal(120, krieg.Stats.HitPoints);
        Assert.Equal(5, krieg.Stats.Range);
        // Schiffe versenken Schiffe: gegen ein Schiff mehr Schaden als gegen eine Miliz
        var andereFlotte = new WarShip(1, new Position(1, 0));
        var miliz = new Militia(1, new Position(1, 0));
        Assert.True(DamageCalculator.CalculateDamage(krieg, andereFlotte) > DamageCalculator.CalculateDamage(krieg, miliz));
    }

    [Fact]
    public void Hafen_kostet_150_Holz_und_ist_3_mal_3()
    {
        var kosten = BuildingRules.CostOf(BuildingType.Dock);
        Assert.Equal(150, kosten[Resource.Wood]);
        Assert.Single(kosten);
        Assert.Equal(3, BuildingRules.SizeOf(BuildingType.Dock));
    }

    [Theory]
    [InlineData(MapSize.Standard, 10)]
    [InlineData(MapSize.Continent, 10)]
    [InlineData(MapSize.World, 12)]
    public void Wiking_hat_4_bis_10_Inseln_auf_der_groessten_Karte_12(MapSize groesse, int hoechstens)
    {
        var s = MapSettings.ForStyle(MapStyle.Wiking, groesse);
        // Die Einstellungen zählen die zusätzlichen Inseln, die zwei Startinseln kommen dazu
        Assert.Equal(4, s.IslandsMin + 2);
        Assert.Equal(hoechstens, s.IslandsMax + 2);
    }

    [Fact]
    public void Wiking_Karte_hat_so_viele_Landmassen_wie_erlaubt_und_keine_Splitter()
    {
        var (w, h) = MapSizes.Dimensions(MapSize.Standard, 16f / 9f);
        for (int n = 0; n < 3; n++)
        {
            var karte = new DataMap(w, h, 32, MapSettings.ForStyle(MapStyle.Wiking, MapSize.Standard), MapStyle.Wiking);
            var groessen = new Dictionary<int, int>();
            for (int x = 0; x < w; x++)
                for (int y = 0; y < h; y++)
                    if (karte.LandRegion(x, y) is var r && r != 0)
                        groessen[r] = groessen.GetValueOrDefault(r) + 1;
            Assert.InRange(groessen.Count, 4, 10);
            Assert.All(groessen.Values, g => Assert.True(g >= DataMap.ISLAND_MIN_TILES));
            // Ganz von Wasser umgeben: am Kartenrand liegt kein Land
            for (int x = 0; x < w; x++)
            {
                Assert.Equal(TileType.Water, karte.GetTile(x, 0).Type);
                Assert.Equal(TileType.Water, karte.GetTile(x, h - 1).Type);
            }
        }
    }

    [Fact]
    public void Kleine_Wiking_Startinseln_tragen_Holz_Stein_und_Gold()
    {
        // Die Testkarte (32 × 32): die Startinseln sind kaum größer als die Startzone
        for (int n = 0; n < 6; n++)
        {
            var karte = new DataMap(32, 32, 32, MapSettings.ForStyle(MapStyle.Wiking, MapSize.Test), MapStyle.Wiking);
            foreach (var tc in karte.Buildings.Where(b => b.Type == "Stadtzentrum"))
            {
                int insel = karte.LandRegion(tc.X + 1, tc.Y + 1);
                foreach (var art in new[] { TileType.Forest, TileType.Mountain, TileType.GoldMine })
                {
                    int anzahl = 0;
                    for (int x = 0; x < 32; x++)
                        for (int y = 0; y < 32; y++)
                            if (karte.GetTile(x, y).Type == art && karte.LandRegion(x, y) == insel)
                                anzahl++;
                    Assert.True(anzahl >= 1, $"Karte {n}: auf der Startinsel von Spieler {tc.OwnerId + 1} fehlt {art}");
                }
            }
        }
    }
}
