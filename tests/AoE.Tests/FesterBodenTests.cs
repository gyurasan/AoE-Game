using System.Linq;
using AoE.Core.Entities;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using DataPlayer = AgeOfEvolutions.Core.Data.Player;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using SaveGameMapper = AgeOfEvolutions.Core.Data.SaveGameMapper;
using TileType = AgeOfEvolutions.Core.Data.TileType;

namespace AoE.Tests;

/// <summary>
/// Fester Boden (G15). Der Nutzer (2026-10-17): „gras und pfäde dürfen sich nicht mehr ändern
/// wenn sie mal erstelt sind. Über gras kann nur noch der Pfad oder Strassen gelegt werden.
/// Aber Gras in ein anderes Gras nicht“. Jede Kachel merkt sich den Boden der Kartenerzeugung
/// (Tile.GroundType); ein Trampelpfad entsteht ab sechs Tritten (Tile.IsPath) und bleibt für
/// immer, Tritte darunter vergehen wieder. Was der Spielbildschirm daraus zeichnet (hohes Gras,
/// Lebendbild), prüft tools/spielablauf in der Gruppe festerboden.
/// </summary>
public class FesterBodenTests
{
    private static DataMap Leer() => DataMap.CreateBlank(12, 12, 16, new MapSettings());

    private static void Treten(DataMap karte, int x, int y, int tritte)
    {
        for (int i = 0; i < tritte; i++)
            karte.Trample(x, y);
    }

    [Fact]
    public void NachDerErzeugungMerktSichJedeKachelIhrenBoden()
    {
        var karte = new DataMap(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        for (int x = 0; x < karte.Width; x++)
            for (int y = 0; y < karte.Height; y++)
                Assert.Equal(karte.GetTile(x, y).Type, karte.GetTile(x, y).GroundType);
        // Die Karte hat Wald, und Wald ist nicht Wiese - sonst prüfte der Test nichts
        Assert.Contains(Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 30).Select(y => karte.GetTile(x, y))),
                        t => t.GroundType == TileType.Forest);
    }

    [Fact]
    public void GefaellterWaldUndLeereMinenBehaltenIhrenBoden()
    {
        var karte = new DataMap(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        var alle = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 30).Select(y => karte.GetTile(x, y))).ToList();
        var wald = alle.First(t => t.Type == TileType.Forest);
        karte.ClearResource(wald);
        Assert.Equal(TileType.Grassland, wald.Type);
        Assert.Equal(TileType.Forest, wald.GroundType);
        if (alle.FirstOrDefault(t => t.Type is TileType.Mountain or TileType.GoldMine) is { } mine)
        {
            var vorher = mine.Type;
            karte.ClearResource(mine);
            Assert.Equal(TileType.Grassland, mine.Type);
            Assert.Equal(vorher, mine.GroundType);
        }
    }

    [Fact]
    public void LeereKarteIstWiese()
    {
        var karte = Leer();
        Assert.All(Enumerable.Range(0, 12).Select(x => karte.GetTile(x, 3)), t => Assert.Equal(TileType.Grassland, t.GroundType));
    }

    [Fact]
    public void PfadAbSechsTritten()
    {
        var karte = Leer();
        Treten(karte, 4, 4, 5);
        Assert.False(karte.GetTile(4, 4).IsPath);
        karte.Trample(4, 4);
        Assert.True(karte.GetTile(4, 4).IsPath);
    }

    [Fact]
    public void EinPfadWaechstNieWiederZu()
    {
        var karte = Leer();
        Treten(karte, 4, 4, 6);
        Treten(karte, 6, 6, 20);
        float sechs = karte.GetTile(4, 4).Wear, zwanzig = karte.GetTile(6, 6).Wear;
        karte.RegrowGrass(DataMap.WEAR_REGROW_SECONDS * 10f);
        Assert.Equal(sechs, karte.GetTile(4, 4).Wear);
        Assert.Equal(zwanzig, karte.GetTile(6, 6).Wear);
        Assert.True(karte.GetTile(4, 4).IsPath);
    }

    [Fact]
    public void TritteOhnePfadVergehen()
    {
        var karte = Leer();
        Treten(karte, 4, 4, 4);   // 0,2
        Treten(karte, 5, 4, 5);   // 0,25
        karte.RegrowGrass(DataMap.WEAR_REGROW_SECONDS / 10f);
        Assert.Equal(0.1f, karte.GetTile(4, 4).Wear, 3);
        Assert.Equal(0.15f, karte.GetTile(5, 4).Wear, 3);
        karte.RegrowGrass(DataMap.WEAR_REGROW_SECONDS);
        Assert.Equal(0f, karte.GetTile(4, 4).Wear);
        Assert.Equal(0f, karte.GetTile(5, 4).Wear);
        Assert.Equal(0f, karte.GetTile(7, 7).Wear);   // nie betreten, bleibt 0
    }

    [Fact]
    public void WerOftGenugGehtTrittTrotzNachwachsenEinenPfad()
    {
        // Ein Tritt alle 10 s: dazwischen vergehen je 10/900, der Pfad entsteht trotzdem, nur
        // später - nach sieben Tritten 0,35 - 7 · 0,011 = 0,27, beim achten 0,32
        var karte = Leer();
        for (int i = 0; i < 7; i++)
        {
            karte.Trample(4, 4);
            karte.RegrowGrass(10f);
        }
        Assert.False(karte.GetTile(4, 4).IsPath);
        karte.Trample(4, 4);
        Assert.True(karte.GetTile(4, 4).IsPath);
        karte.RegrowGrass(DataMap.WEAR_REGROW_SECONDS);
        Assert.True(karte.GetTile(4, 4).IsPath);
    }

    [Fact]
    public void SpielstandBehaeltDenBodenDerErzeugung()
    {
        var karte = new DataMap(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        var wald = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 30).Select(y => karte.GetTile(x, y)))
            .First(t => t.Type == TileType.Forest);
        karte.ClearResource(wald);
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        Assert.Equal(TileType.Forest, save.Tiles[wald.Y * save.Width + wald.X].GroundType);
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        Assert.Equal(TileType.Grassland, geladen.GetTile(wald.X, wald.Y).Type);
        Assert.Equal(TileType.Forest, geladen.GetTile(wald.X, wald.Y).GroundType);
        for (int x = 0; x < 40; x++)
            for (int y = 0; y < 30; y++)
                Assert.Equal(karte.GetTile(x, y).GroundType, geladen.GetTile(x, y).GroundType);
    }

    [Fact]
    public void AelterSpielstandOhneBodenNimmtDenTyp()
    {
        var karte = new DataMap(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        var save = SaveGameMapper.Capture(karte, new DataPlayer(0, "Spieler", "Briten"), new DataPlayer(1, "Gegner", "Azteken"), MapSize.Test);
        foreach (var t in save.Tiles)
            t.GroundType = null;
        var (geladen, _, _) = SaveGameMapper.Restore(save);
        for (int x = 0; x < 40; x++)
            for (int y = 0; y < 30; y++)
                Assert.Equal(geladen.GetTile(x, y).Type, geladen.GetTile(x, y).GroundType);
        Assert.Contains(save.Tiles, t => t.Type == TileType.Forest);
    }
}
