using System.Linq;
using AoE.Core.Entities;
using AoE.Core.Map;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Prüft die A*-Wegfindung aus <c>AoE.Core.Pathfinding</c> — seit B4 der Weg,
/// den auch <c>TileMap.FindPath</c> im Spiel nimmt.
///
/// Die Spezifikation setzt hier die Messlatte bewusst auf „bleibt nicht stecken"
/// und nicht auf „findet den kürzesten Weg". Genau das prüfen diese Tests.
/// </summary>
public class PathfindingTests
{
    /// <summary>Karte mit freiem Gelände.</summary>
    private static MapGrid FreieKarte(int width = 10, int height = 10)
    {
        var map = new MapGrid(width, height);
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                var tile = map.GetTile(x, y);
                if (tile != null)
                    tile.IsPassable = true;
            }
        }
        return map;
    }

    [Fact]
    public void FindetWegAufFreierKarte()
    {
        var map = FreieKarte();

        var path = AoE.Core.Pathfinding.Pathfinding.FindPath(
            map, new Position(0, 0), new Position(5, 5));

        Assert.NotNull(path);
        Assert.NotEmpty(path!);
        Assert.Equal(new Position(5, 5), path!.Last());
    }

    [Fact]
    public void LaeuftUmEineMauerHerum()
    {
        var map = FreieKarte();

        // Senkrechte Mauer bei x = 3, mit einer Lücke ganz unten bei y = 9.
        for (int y = 0; y < 9; y++)
        {
            var tile = map.GetTile(3, y);
            if (tile != null)
                tile.IsPassable = false;
        }

        var path = AoE.Core.Pathfinding.Pathfinding.FindPath(
            map, new Position(0, 0), new Position(6, 0));

        Assert.NotNull(path);
        Assert.Equal(new Position(6, 0), path!.Last());

        // Kein Wegpunkt darf in der Mauer liegen — sonst liefe die Einheit hindurch.
        Assert.DoesNotContain(path!, step => step.X == 3 && step.Y < 9);

        // Und der Umweg muss durch die Lücke führen.
        Assert.Contains(path!, step => step.X == 3 && step.Y == 9);
    }

    [Fact]
    public void GibtNullWennZielEingemauertIst()
    {
        var map = FreieKarte();

        // Ziel (5,5) vollständig einmauern.
        foreach (var (x, y) in new[] { (4, 5), (6, 5), (5, 4), (5, 6) })
        {
            var tile = map.GetTile(x, y);
            if (tile != null)
                tile.IsPassable = false;
        }

        var path = AoE.Core.Pathfinding.Pathfinding.FindPath(
            map, new Position(0, 0), new Position(5, 5));

        // Wichtig: kein Weg ist ein sauberes Ergebnis, kein Absturz.
        // Das Spiel lässt die Einheit dann stehen.
        Assert.True(path == null || path.Count == 0,
            "Ein eingemauertes Ziel darf keinen Weg liefern.");
    }
}
