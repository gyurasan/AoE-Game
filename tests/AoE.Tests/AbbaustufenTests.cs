using System.Collections.Generic;
using System.Linq;
using AoE.Core.Entities;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using DataPlayer = AgeOfEvolutions.Core.Data.Player;
using DataTile = AgeOfEvolutions.Core.Data.Tile;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using SaveGameMapper = AgeOfEvolutions.Core.Data.SaveGameMapper;
using TileType = AgeOfEvolutions.Core.Data.TileType;

namespace AoE.Tests;

/// <summary>
/// Abbaustufen der Stein- und Goldhaufen (R2). Der Nutzer (2026-11-01): „Resourcen wie gold oder
/// steine müssten sichtbar abnehmen wenn abgebaut wird … ein bild für 75% ein Bild für 50% und
/// eins für 25%“. Jede Kachel merkt sich ihren Startvorrat (Tile.ResourceStart); Tile.PileStage
/// sagt, welches Bild der Spielbildschirm zeigt: 0 voll, 1 drei Viertel, 2 halb, 3 ein Viertel.
/// </summary>
public class AbbaustufenTests
{
    private static DataMap Karte() => new(40, 30, 32, MapSettings.ForSize(MapSize.Test));

    private static List<DataTile> Alle(DataMap karte) =>
        Enumerable.Range(0, karte.Width).SelectMany(x => Enumerable.Range(0, karte.Height).Select(y => karte.GetTile(x, y))).ToList();

    private static DataTile Haufen(int start, int rest) =>
        new(0, 0, TileType.Mountain) { ResourceStart = start, ResourceAmount = rest };

    [Theory]
    [InlineData(100, 0)]
    [InlineData(76, 0)]
    [InlineData(75, 1)]
    [InlineData(51, 1)]
    [InlineData(50, 2)]
    [InlineData(26, 2)]
    [InlineData(25, 3)]
    [InlineData(1, 3)]
    [InlineData(0, 3)]
    public void DieStufeIstDasAngebrocheneViertel(int rest, int stufe)
    {
        Assert.Equal(stufe, Haufen(100, rest).PileStage);
    }

    [Fact]
    public void OhneStartvorratUndDarueberBleibtDerHaufenVoll()
    {
        Assert.Equal(0, Haufen(0, 40).PileStage);
        Assert.Equal(0, Haufen(80, 95).PileStage);
    }

    [Fact]
    public void AbbauenGehtDurchAlleVierStufenUndNieZurueck()
    {
        // Ungerader Startvorrat: die Viertelgrenzen liegen zwischen zwei Mengen
        var haufen = Haufen(87, 87);
        var gesehen = new List<int> { haufen.PileStage };
        while (haufen.ResourceAmount > 1)
        {
            haufen.ResourceAmount--;
            Assert.True(haufen.PileStage >= gesehen[^1], $"bei {haufen.ResourceAmount} zurück auf Stufe {haufen.PileStage}");
            gesehen.Add(haufen.PileStage);
        }
        Assert.Equal(new[] { 0, 1, 2, 3 }, gesehen.Distinct());
        // Jede Stufe hält ungefähr ein Viertel des Vorrats
        foreach (int stufe in new[] { 0, 1, 2, 3 })
            Assert.InRange(gesehen.Count(s => s == stufe), 87 / 4 - 1, 87 / 4 + 2);
    }

    [Fact]
    public void JederErzeugteHaufenMerktSichSeinenStartvorratUndIstVoll()
    {
        var haufen = Alle(Karte()).Where(t => t.Type is TileType.Mountain or TileType.GoldMine).ToList();
        Assert.Contains(haufen, t => t.Type == TileType.Mountain);
        Assert.Contains(haufen, t => t.Type == TileType.GoldMine);
        foreach (var t in haufen)
        {
            Assert.Equal(t.ResourceAmount, t.ResourceStart);
            Assert.Equal(0, t.PileStage);
        }
        // Der Vorrat schwankt je Kachel - sonst bräuchte es den Startvorrat nicht
        Assert.True(haufen.Where(t => t.Type == TileType.Mountain).Select(t => t.ResourceStart).Distinct().Count() > 1);
        Assert.All(haufen.Where(t => t.Type == TileType.Mountain),
                   t => Assert.InRange(t.ResourceStart, DataMap.STONE_AMOUNT, DataMap.STONE_AMOUNT * 3 / 2));
        Assert.All(haufen.Where(t => t.Type == TileType.GoldMine),
                   t => Assert.InRange(t.ResourceStart, DataMap.GOLD_AMOUNT, DataMap.GOLD_AMOUNT * 3 / 2));
    }

    [Fact]
    public void SpielstandBehaeltDenStartvorrat()
    {
        var karte = Karte();
        var stein = Alle(karte).First(t => t.Type == TileType.Mountain);
        stein.ResourceAmount = stein.ResourceStart / 3;
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        foreach (var t in Alle(karte))
            Assert.Equal(t.ResourceStart, geladen.GetTile(t.X, t.Y).ResourceStart);
        Assert.Equal(stein.PileStage, geladen.GetTile(stein.X, stein.Y).PileStage);
        Assert.Equal(2, geladen.GetTile(stein.X, stein.Y).PileStage);
    }

    [Fact]
    public void AelterSpielstandNimmtDenMittlerenStartvorrat()
    {
        var karte = Karte();
        var gold = Alle(karte).First(t => t.Type == TileType.GoldMine);
        gold.ResourceAmount = 30;
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        foreach (var t in save.Tiles)
            t.ResourceStart = null;
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        foreach (var t in Alle(geladen))
        {
            int erwartet = t.ResourceType switch
            {
                Resource.Stone => DataMap.STONE_AMOUNT * 5 / 4,
                Resource.Gold => DataMap.GOLD_AMOUNT * 5 / 4,
                _ => 0,
            };
            Assert.Equal(erwartet, t.ResourceStart);
            // Unberührte Haufen zeigen sich voll - auch der kleinstmögliche
            if (t.Type is TileType.Mountain or TileType.GoldMine && (t.X != gold.X || t.Y != gold.Y))
                Assert.Equal(0, t.PileStage);
        }
        // 30 von 75: angebrochene Hälfte
        Assert.Equal(2, geladen.GetTile(gold.X, gold.Y).PileStage);
    }
}
