using AoE.Core.Entities;
using AoE.Core.Map;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Prüft die drei Sichtbarkeitszustände aus <c>AoE.Core.Map</c> — seit B5 die
/// Grundlage des Nebels des Krieges im Spiel.
///
/// Der wichtigste davon ist der mittlere: eine Kachel, die einmal gesehen wurde
/// und jetzt nicht mehr einsehbar ist, darf nicht wieder schwarz werden. Sie
/// zeigt abgedunkelt den letzten bekannten Stand.
/// </summary>
public class FogOfWarTests
{
    private static MapGrid Karte(int size = 20)
    {
        var map = new MapGrid(size, size);
        for (int x = 0; x < size; x++)
        {
            for (int y = 0; y < size; y++)
            {
                var tile = map.GetTile(x, y);
                if (tile != null)
                    tile.IsPassable = true;
            }
        }
        return map;
    }

    [Fact]
    public void KachelBeiEigenerEinheit_IstSichtbar()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        map.AddUnit(new Scout(0, new Position(5, 5)));
        sicht.UpdateVisibility(playerId: 0);

        Assert.Equal(TileVisibility.Visible, map.GetTile(5, 5)!.GetVisibility(0));
        Assert.True(sicht.CanSeePosition(0, new Position(5, 5)));
    }

    [Fact]
    public void VerlasseneKachel_BleibtErforscht_UndWirdNichtWiederSchwarz()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        // Späher steht bei (5,5) und sieht die Umgebung.
        var scout = new Scout(0, new Position(5, 5));
        map.AddUnit(scout);
        sicht.UpdateVisibility(playerId: 0);
        Assert.Equal(TileVisibility.Visible, map.GetTile(5, 5)!.GetVisibility(0));

        // Er zieht weit genug weg, dass die alte Kachel außer Sicht gerät.
        scout.Position = new Position(18, 18);
        sicht.UpdateVisibility(playerId: 0);

        var zustand = map.GetTile(5, 5)!.GetVisibility(0);
        Assert.Equal(TileVisibility.Explored, zustand);
        Assert.NotEqual(TileVisibility.Unexplored, zustand);
        Assert.False(sicht.CanSeePosition(0, new Position(5, 5)));
    }

    [Fact]
    public void EigenesGebaeude_SpendetSicht()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        // Stadtzentrum: Sichtweite 5 Kacheln
        map.AddBuilding(BuildingEntity.CreateTownCenter(0, new Position(10, 10)));
        sicht.UpdateVisibility(playerId: 0);

        Assert.Equal(TileVisibility.Visible, map.GetTile(10, 10)!.GetVisibility(0));
        Assert.Equal(TileVisibility.Visible, map.GetTile(15, 10)!.GetVisibility(0));
        Assert.Equal(TileVisibility.Unexplored, map.GetTile(16, 10)!.GetVisibility(0));
    }

    [Fact]
    public void FremdesGebaeude_SpendetKeineSicht()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        map.AddBuilding(BuildingEntity.CreateTownCenter(1, new Position(10, 10)));
        sicht.UpdateVisibility(playerId: 0);

        Assert.Equal(TileVisibility.Unexplored, map.GetTile(10, 10)!.GetVisibility(0));
    }

    [Fact]
    public void ZerstoertesGebaeude_SpendetKeineSichtMehr()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);
        var zentrum = BuildingEntity.CreateTownCenter(0, new Position(10, 10));
        map.AddBuilding(zentrum);
        sicht.UpdateVisibility(playerId: 0);

        zentrum.CurrentHp = 0;
        sicht.UpdateVisibility(playerId: 0);

        // Einmal gesehen bleibt erforscht, aber nicht mehr sichtbar
        Assert.Equal(TileVisibility.Explored, map.GetTile(10, 10)!.GetVisibility(0));
    }

    [Fact]
    public void Baustelle_SpendetNochKeineSicht()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        // Die Baustelle steht schon auf der Karte und sperrt ihre Kacheln,
        // deckt aber nichts auf - auch nicht ihre eigene Kachel
        var zentrum = BuildingEntity.CreateTownCenter(0, new Position(10, 10));
        zentrum.IsUnderConstruction = true;
        map.AddBuilding(zentrum);
        sicht.UpdateVisibility(playerId: 0);

        Assert.Equal(TileVisibility.Unexplored, map.GetTile(10, 10)!.GetVisibility(0));
        Assert.Equal(TileVisibility.Unexplored, map.GetTile(15, 10)!.GetVisibility(0));
    }

    [Fact]
    public void FertigGebauteBaustelle_SpendetSicht()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);
        var zentrum = BuildingEntity.CreateTownCenter(0, new Position(10, 10));
        zentrum.IsUnderConstruction = true;
        map.AddBuilding(zentrum);
        sicht.UpdateVisibility(playerId: 0);

        zentrum.IsUnderConstruction = false;
        sicht.UpdateVisibility(playerId: 0);

        Assert.Equal(TileVisibility.Visible, map.GetTile(10, 10)!.GetVisibility(0));
        Assert.Equal(TileVisibility.Visible, map.GetTile(15, 10)!.GetVisibility(0));
    }

    [Fact]
    public void SichtIstProSpielerGetrennt()
    {
        var map = Karte();
        var sicht = new VisibilitySystem(map);

        map.AddUnit(new Scout(0, new Position(5, 5)));
        sicht.UpdateVisibility(playerId: 0);
        sicht.UpdateVisibility(playerId: 1);

        Assert.Equal(TileVisibility.Visible, map.GetTile(5, 5)!.GetVisibility(0));
        Assert.Equal(TileVisibility.Unexplored, map.GetTile(5, 5)!.GetVisibility(1));
    }
}
