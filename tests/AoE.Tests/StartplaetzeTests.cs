using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;

namespace AoE.Tests;

/// <summary>
/// Startplätze (SZ1): beide Stadtzentren stehen pro Karte auf zufälligen
/// Kacheln, nicht mehr fest in der linken oberen und rechten unteren Ecke,
/// und ihre euklidische Entfernung zueinander beträgt mindestens die halbe
/// Kartenbreite.
/// </summary>
public class StartplaetzeTests
{
    private static double Entfernung((int x, int y) a, (int x, int y) b)
        => Math.Sqrt((a.x - b.x) * (double)(a.x - b.x) + (a.y - b.y) * (double)(a.y - b.y));

    [Theory]
    [InlineData(64, 64)]
    [InlineData(96, 54)]
    [InlineData(128, 72)]
    [InlineData(40, 30)]
    [InlineData(24, 24)]
    public void StartabstandMindestensHalbeKartenbreite(int breite, int hoehe)
    {
        for (int k = 0; k < 20; k++)
        {
            var karte = new DataMap(breite, hoehe, 32);
            double d = Entfernung(karte.StartPosition(0), karte.StartPosition(1));
            Assert.True(d >= breite / 2.0 - 1e-9,
                $"Karte {breite}x{hoehe} Versuch {k}: Abstand {d:0.0}, gefordert mindestens {breite / 2.0}");
        }
    }

    [Theory]
    [InlineData(64, 64)]
    [InlineData(96, 54)]
    [InlineData(24, 24)]
    public void StartInnenhalbDerRandzone(int breite, int hoehe)
    {
        for (int k = 0; k < 20; k++)
        {
            var karte = new DataMap(breite, hoehe, 32);
            foreach (var spieler in new[] { 0, 1 })
            {
                var (x, y) = karte.StartPosition(spieler);
                // ClearStartArea reicht von x - 2 bis x + 5, dazu die
                // Dorfbewohner-Versätze bis x + 5 - beides muss auf die Karte passen.
                Assert.True(x >= 3 && x <= breite - 7 && y >= 3 && y <= hoehe - 7,
                    $"Spieler {spieler} startet bei ({x}, {y}) auf {breite}x{hoehe}");
            }
        }
    }

    [Fact]
    public void StadtzentrumUndDorfbewohnerAnDenStartplaetzen()
    {
        var karte = new DataMap(64, 64, 32);
        foreach (var spieler in new[] { 0, 1 })
        {
            var start = karte.StartPosition(spieler);
            var zentrum = Assert.Single(karte.Buildings,
                b => b.Type == "Stadtzentrum" && b.OwnerId == spieler);
            Assert.Equal(start.x, zentrum.X);
            Assert.Equal(start.y, zentrum.Y);
            // vier Dorfbewohner dicht am Stadtzentrum (Versätze bis fünf Kacheln)
            var dorfbewohner = karte.Units.Where(u => u.OwnerId == spieler
                                   && u.Type == AgeOfEvolutions.Core.Data.UnitType.Villager).ToList();
            Assert.Equal(4, dorfbewohner.Count);
            foreach (var u in dorfbewohner)
            {
                int ux = (int)u.Position.X / karte.TileSize, uy = (int)u.Position.Y / karte.TileSize;
                Assert.True(Math.Abs(ux - start.x) <= 6 && Math.Abs(uy - start.y) <= 6,
                    $"Dorfbewohner bei ({ux}, {uy}), Start bei ({start.x}, {start.y})");
            }
        }
    }

    [Fact]
    public void StartNichtImmerInDenEcken()
    {
        var p1Starts = new HashSet<(int, int)>();
        for (int k = 0; k < 40; k++)
        {
            var karte = new DataMap(64, 64, 32);
            p1Starts.Add(karte.StartPosition(0));
        }
        // über 40 Karten: mindestens zwei verschiedene Plätze, und nicht alle
        // sitzen auf der alten Ecke (3, 3)
        Assert.True(p1Starts.Count >= 2, "alle 40 Karten starten auf demselben Platz");
        Assert.True(p1Starts.Count(s => s != (3, 3)) >= 1, "Spieler 1 startet immer noch fest bei (3, 3)");
    }
}
