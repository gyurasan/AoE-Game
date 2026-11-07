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
/// Fällstufen der Bäume (Holz abbauen). Der Nutzer (2026-11-07): „was noch fehlt ist beim Abbau
/// der Bäume die Zwischenschritte, also gefällter Baum und 1-2 Stände bis zum kompletten Abbau
/// eines Baumes“. Eine Waldkachel trägt zwei Bäume, die nacheinander fallen; Tile.TreeStage sagt
/// je Baum: 0 steht, 1 gefällt mit Krone, 2 zwei Stammstücke, 3 eines, 4 nur der Stumpf.
/// </summary>
public class FaellstufenTests
{
    private static DataMap Karte() => new(40, 30, 32, MapSettings.ForSize(MapSize.Test));

    private static List<DataTile> Alle(DataMap karte) =>
        Enumerable.Range(0, karte.Width).SelectMany(x => Enumerable.Range(0, karte.Height).Select(y => karte.GetTile(x, y))).ToList();

    private static DataTile Wald(int start, int rest) =>
        new(0, 0, TileType.Forest) { ResourceStart = start, ResourceAmount = rest };

    [Theory]
    // Startvorrat 120, zwei Bäume zu je 60: der erste fällt beim ersten Holz
    [InlineData(120, 0, 0)]
    [InlineData(119, 1, 0)]
    [InlineData(101, 1, 0)]
    [InlineData(100, 2, 0)]   // ein Drittel des ersten Baums (20 von 60) geschlagen
    [InlineData(80, 3, 0)]
    [InlineData(61, 3, 0)]
    [InlineData(60, 4, 0)]    // der erste ist weg, der zweite steht noch
    [InlineData(59, 4, 1)]
    [InlineData(40, 4, 2)]
    [InlineData(20, 4, 3)]
    [InlineData(1, 4, 3)]
    [InlineData(0, 4, 4)]
    public void ErstFaelltDerEineBaumDannDerAndere(int rest, int erster, int zweiter)
    {
        var wald = Wald(120, rest);
        Assert.Equal(erster, wald.TreeStage(0, 2));
        Assert.Equal(zweiter, wald.TreeStage(1, 2));
    }

    [Fact]
    public void OhneHolzOhneStartvorratOderUnberuehrtStehtDerBaum()
    {
        Assert.Equal(DataTile.TREE_STANDING, Wald(0, 40).TreeStage(0, 2));
        Assert.Equal(DataTile.TREE_STANDING, Wald(100, 130).TreeStage(0, 2));
        Assert.Equal(DataTile.TREE_STANDING, Wald(100, 100).TreeStage(0, 2));
        Assert.Equal(DataTile.TREE_STANDING, Wald(100, 50).TreeStage(0, 0));
        var stein = new DataTile(0, 0, TileType.Mountain) { ResourceStart = 100, ResourceAmount = 10 };
        Assert.Equal(DataTile.TREE_STANDING, stein.TreeStage(0, 2));
    }

    [Theory]
    [InlineData(100)]
    [InlineData(127)]   // ungerader Teil je Baum: die Grenzen liegen zwischen zwei Mengen
    [InlineData(149)]
    public void AbholzenGehtJeBaumDurchAlleStufenUndNieZurueck(int start)
    {
        var wald = Wald(start, start);
        var folge = new List<(int, int)> { (wald.TreeStage(0, 2), wald.TreeStage(1, 2)) };
        while (wald.ResourceAmount > 0)
        {
            wald.ResourceAmount--;
            var jetzt = (wald.TreeStage(0, 2), wald.TreeStage(1, 2));
            Assert.True(jetzt.Item1 >= folge[^1].Item1 && jetzt.Item2 >= folge[^1].Item2,
                        $"bei {wald.ResourceAmount}: {jetzt} nach {folge[^1]}");
            // Der zweite Baum fällt erst, wenn vom ersten nur der Stumpf steht
            if (jetzt.Item2 != DataTile.TREE_STANDING)
                Assert.Equal(DataTile.TREE_STUMP, jetzt.Item1);
            folge.Add(jetzt);
        }
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, folge.Select(f => f.Item1).Distinct());
        Assert.Equal(new[] { 0, 1, 2, 3, 4 }, folge.Select(f => f.Item2).Distinct());
        // Gefällt, zwei Stücke, ein Stück: je etwa ein Drittel des Baums
        foreach (int stufe in new[] { 1, 2, 3 })
            Assert.InRange(folge.Count(f => f.Item1 == stufe), start / 6 - 1, start / 6 + 2);
    }

    [Fact]
    public void JederErzeugteWaldMerktSichSeinenStartvorratUndSteht()
    {
        var wald = Alle(Karte()).Where(t => t.Type == TileType.Forest).ToList();
        Assert.NotEmpty(wald);
        foreach (var t in wald)
        {
            Assert.Equal(t.ResourceAmount, t.ResourceStart);
            Assert.InRange(t.ResourceStart, DataMap.WOOD_AMOUNT, DataMap.WOOD_AMOUNT * 3 / 2);
            Assert.Equal(DataTile.TREE_STANDING, t.TreeStage(0, 2));
            Assert.Equal(DataTile.TREE_STANDING, t.TreeStage(1, 2));
        }
    }

    [Fact]
    public void SpielstandBehaeltDieFaellstufe()
    {
        var karte = Karte();
        var baum = Alle(karte).First(t => t.Type == TileType.Forest);
        baum.ResourceAmount = baum.ResourceStart * 2 / 5;
        int erster = baum.TreeStage(0, 2), zweiter = baum.TreeStage(1, 2);
        Assert.Equal(DataTile.TREE_STUMP, erster);
        Assert.NotEqual(DataTile.TREE_STANDING, zweiter);
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        var neu = geladen.GetTile(baum.X, baum.Y);
        Assert.Equal(erster, neu.TreeStage(0, 2));
        Assert.Equal(zweiter, neu.TreeStage(1, 2));
    }

    [Fact]
    public void AelterSpielstandLaesstUnberuehrtenWaldStehen()
    {
        // Ohne gespeicherten Startvorrat gilt der kleinste (100): kein unberührter Baum liegt
        var karte = Karte();
        var wald = Alle(karte).Where(t => t.Type == TileType.Forest).ToList();
        var angebrochen = wald[0];
        angebrochen.ResourceAmount = 30;
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        foreach (var t in save.Tiles)
            t.ResourceStart = null;
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        foreach (var t in wald.Skip(1))
        {
            var g = geladen.GetTile(t.X, t.Y);
            Assert.Equal(DataMap.WOOD_AMOUNT, g.ResourceStart);
            Assert.Equal(DataTile.TREE_STANDING, g.TreeStage(0, 2));
        }
        // 30 von 100: der erste ist weg, vom zweiten sind 20 von 50 geschlagen - zwei Stücke
        var a = geladen.GetTile(angebrochen.X, angebrochen.Y);
        Assert.Equal(DataTile.TREE_STUMP, a.TreeStage(0, 2));
        Assert.Equal(DataTile.TREE_TWO_LOGS, a.TreeStage(1, 2));
    }
}
