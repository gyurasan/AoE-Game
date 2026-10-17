using System.Collections.Generic;
using System.Linq;
using AoE.Core.Economy;
using AoE.Core.Entities;
using AoE.Tests;
using Xunit;
using DataBuilding = AgeOfEvolutions.Core.Data.Building;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using DataPlayer = AgeOfEvolutions.Core.Data.Player;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using SaveGameMapper = AgeOfEvolutions.Core.Data.SaveGameMapper;
using TileType = AgeOfEvolutions.Core.Data.TileType;

namespace AoE.Tests;

/// <summary>
/// Wege (C12): Trampelpfade (Wear ab ROAD_WEAR_MIN, sechs Tritte - C16) rücken bei jedem Zeitaufstieg zur neuen
/// Stufe auf — Feudal 1, Ritter 2, Imperial 3 — und verlieren die Stufe nie.
/// Auf Stufen 1 und höher laufen Einheiten 20 % schneller als auf der Wiese
/// (gemessen in der Abnahme-Gruppe strassen, nicht hier).
/// </summary>
public class FeldwegeTests
{
    private static AgeOfEvolutions.Core.Data.TileMap Karte(int groesse = 12)
        => AgeOfEvolutions.Core.Data.TileMap.CreateBlank(groesse, groesse, 16, new AgeOfEvolutions.Core.Data.MapSettings());

    /// <summary>Ein echter Trampelpfad: sechs Tritte (ROAD_WEAR_MIN, C16).</summary>
    private static void Austreten(AgeOfEvolutions.Core.Data.TileMap karte, int x, int y, int tritte = 6)
    {
        for (int i = 0; i < tritte; i++)
            karte.Trample(x, y);
    }

    [Fact]
    public void LevelOfAgeMapptZeitalterAufStufen()
    {
        Assert.Equal(0, AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Dark));
        Assert.Equal(1, AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Feudal));
        Assert.Equal(2, AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Castle));
        Assert.Equal(3, AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Imperial));
    }

    [Fact]
    public void AufstiegRucktDieTrampelpfadeAuf()
    {
        var karte = Karte();
        // Trampelpfade aus der Dunklen Zeit: ausgetritten, ohne Weg-Stufe.
        Austreten(karte, 3, 3);
        Austreten(karte, 4, 3);
        Assert.Equal(0, karte.GetTile(3, 3).RoadLevel);

        karte.UpgradeRoads(AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Feudal));
        Assert.Equal(1, karte.GetTile(3, 3).RoadLevel);
        Assert.Equal(1, karte.GetTile(4, 3).RoadLevel);
    }

    [Fact]
    public void JederAufstuhlHebtDieStufeAn()
    {
        var karte = Karte();
        Austreten(karte, 2, 2);
        karte.UpgradeRoads(1);
        karte.UpgradeRoads(AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Castle));
        Assert.Equal(2, karte.GetTile(2, 2).RoadLevel);
        karte.UpgradeRoads(AgeOfEvolutions.Core.Data.TileMap.LevelOfAge(Age.Imperial));
        Assert.Equal(3, karte.GetTile(2, 2).RoadLevel);
    }

    [Fact]
    public void DieStufeFalltNiemalsAb()
    {
        var karte = Karte();
        Austreten(karte, 5, 5);
        karte.UpgradeRoads(3);
        Assert.Equal(3, karte.GetTile(5, 5).RoadLevel);
        // Ein „früheres" Zeitalter (z. B. der andere Spieler) hebt nicht ab.
        karte.UpgradeRoads(1);
        Assert.Equal(3, karte.GetTile(5, 5).RoadLevel);
    }

    [Fact]
    public void OhneTrampelpfadEntstehtKeinWeg()
    {
        var karte = Karte();
        karte.UpgradeRoads(2);
        // Nie betreten: bleibt Wiese, keine Weg-Stufe.
        Assert.Equal(0, karte.GetTile(7, 7).RoadLevel);
    }

    [Fact]
    public void NeueTritteAbFeudalWerdenNachSechsSchrittenWeg()
    {
        var karte = Karte();
        // Aufstieg auf die Feudalzeit bereits passiert
        karte.UpgradeRoads(1);
        Austreten(karte, 8, 8, 5);
        Assert.Equal(0, karte.GetTile(8, 8).RoadLevel);
        karte.Trample(8, 8);
        Assert.Equal(1, karte.GetTile(8, 8).RoadLevel);
        // Auch in der Imperialzeit: gleich auf Stufe 3
        karte.UpgradeRoads(3);
        Austreten(karte, 9, 9);
        Assert.Equal(3, karte.GetTile(9, 9).RoadLevel);
    }

    [Fact]
    public void EinzelneSchritteMachenKeinenWeg()
    {
        // Wer in der Dunklen Zeit nur ein paarmal über die Wiese geht, hinterlässt keinen Weg
        var karte = Karte();
        Austreten(karte, 4, 4, 5);
        karte.UpgradeRoads(1);
        Assert.Equal(0, karte.GetTile(4, 4).RoadLevel);
        Assert.Equal(0.3f, AgeOfEvolutions.Core.Data.TileMap.ROAD_WEAR_MIN, 3);
    }

    // --- Wege von Gebäuden (C16) --------------------------------------------
    // Ab der Feudalzeit führt von der Vorderseite jedes fertigen Gebäudes ein Weg
    // zum nächsten vorhandenen Weg oder zum Stadtzentrum seines Besitzers.

    /// <summary>Ein Dorf auf leerer Wiese: Stadtzentrum (10, 10) 4×4, Haus (2, 2) 2×2.</summary>
    private static (DataMap Karte, DataBuilding Haus, DataBuilding Zentrum) Dorf(int breite = 20, int hoehe = 20)
    {
        var karte = DataMap.CreateBlank(breite, hoehe, 16, new MapSettings());
        var zentrum = karte.AddBuilding(10, 10, "Stadtzentrum", 0, 4);
        var haus = karte.AddBuilding(2, 2, "Haus", 0, 2);
        return (karte, haus, zentrum);
    }

    private static IEnumerable<(int X, int Y)> Wegkacheln(DataMap karte)
    {
        for (int x = 0; x < karte.Width; x++)
            for (int y = 0; y < karte.Height; y++)
                if (karte.GetTile(x, y).RoadLevel >= 1)
                    yield return (x, y);
    }

    private static bool ImRing(DataBuilding g, int x, int y)
        => x >= g.X - 1 && x <= g.X + g.Width && y >= g.Y - 1 && y <= g.Y + g.Height
           && !(x >= g.X && x < g.X + g.Width && y >= g.Y && y < g.Y + g.Height);

    /// <summary>
    /// Ob Wegkacheln (vier Nachbarn) eine Kette von einer Kachel im Ring von
    /// <paramref name="von"/> zu einer Kachel im Ring von <paramref name="bis"/> bilden.
    /// </summary>
    private static bool Kette(DataMap karte, DataBuilding von, DataBuilding bis)
    {
        var offen = new Queue<(int X, int Y)>();
        var gesehen = new HashSet<(int, int)>();
        foreach (var k in Wegkacheln(karte).Where(k => ImRing(von, k.X, k.Y)))
        {
            gesehen.Add(k);
            offen.Enqueue(k);
        }
        while (offen.Count > 0)
        {
            var (x, y) = offen.Dequeue();
            if (ImRing(bis, x, y))
                return true;
            foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (karte.GetTile(n.Item1, n.Item2) is { RoadLevel: >= 1 } && gesehen.Add(n))
                    offen.Enqueue(n);
        }
        return false;
    }

    [Fact]
    public void WegKannNurAufFreierWieseOderSandLiegen()
    {
        var (karte, _, _) = Dorf();
        Assert.True(karte.CanCarryRoad(5, 5));
        karte.GetTile(6, 5).Type = TileType.Sand;
        Assert.True(karte.CanCarryRoad(6, 5));
        Assert.False(karte.CanCarryRoad(-1, 5));
        Assert.False(karte.CanCarryRoad(5, 20));
        Assert.False(karte.CanCarryRoad(2, 2));    // Haus
        Assert.False(karte.CanCarryRoad(11, 11));  // Stadtzentrum
        var wasser = karte.GetTile(7, 5);
        wasser.Type = TileType.Water;
        wasser.Walkable = false;
        Assert.False(karte.CanCarryRoad(7, 5));
        karte.GetTile(8, 5).Type = TileType.Forest;
        Assert.False(karte.CanCarryRoad(8, 5));
        karte.GetTile(5, 7).ResourceType = Resource.Gold;
        Assert.False(karte.CanCarryRoad(5, 7));
        karte.PlantCrop(15, 2, 2);
        Assert.False(karte.CanCarryRoad(15, 2));
        // Auch ein abgeerntetes Feld ohne Vorrat bleibt Feld
        var feld = karte.GetTile(16, 3);
        feld.ResourceType = null;
        feld.ResourceAmount = 0;
        Assert.False(karte.CanCarryRoad(16, 3));
    }

    [Fact]
    public void InDerDunklenZeitEntstehtKeinWeg()
    {
        var (karte, haus, _) = Dorf();
        Assert.Equal(0, karte.ConnectBuilding(haus, 0));
        Assert.Empty(Wegkacheln(karte));
    }

    [Fact]
    public void WegFuehrtVonDerVorderseiteZumStadtzentrum()
    {
        var (karte, haus, zentrum) = Dorf();
        // Kürzester Weg: von (3, 4) unter dem Haus zur Ecke (9, 9) des Rings
        // um das Stadtzentrum - 11 Schritte, 12 Kacheln
        Assert.Equal(12, karte.ConnectBuilding(haus, 1));
        Assert.Equal(12, Wegkacheln(karte).Count());
        Assert.Equal(1, karte.GetTile(3, 4).RoadLevel);
        Assert.All(Wegkacheln(karte), k => Assert.Equal(1, karte.GetTile(k.X, k.Y).RoadLevel));
        Assert.True(Kette(karte, haus, zentrum));
    }

    [Fact]
    public void WegEndetAmNaechstenVorhandenenWeg()
    {
        var (karte, haus, zentrum) = Dorf();
        karte.GetTile(3, 7).RoadLevel = 1;
        // (3, 4), (3, 5), (3, 6) werden Weg, (3, 7) war schon einer
        Assert.Equal(3, karte.ConnectBuilding(haus, 1));
        Assert.Equal(4, Wegkacheln(karte).Count());
        Assert.Equal(1, karte.GetTile(3, 5).RoadLevel);
        Assert.Equal(1, karte.GetTile(3, 6).RoadLevel);
        Assert.False(Kette(karte, haus, zentrum));
    }

    [Fact]
    public void DerAnschlussHebtDieStufeAnAberNieAb()
    {
        var (karte, haus, _) = Dorf();
        karte.GetTile(3, 7).RoadLevel = 3;
        Assert.Equal(3, karte.ConnectBuilding(haus, 1));
        Assert.Equal(3, karte.GetTile(3, 7).RoadLevel);
        Assert.Equal(1, karte.GetTile(3, 6).RoadLevel);

        var (karte2, haus2, _) = Dorf();
        karte2.GetTile(3, 7).RoadLevel = 1;
        // Alle vier Kacheln steigen auf Stufe 2, auch die schon vorhandene
        Assert.Equal(4, karte2.ConnectBuilding(haus2, 2));
        Assert.Equal(2, karte2.GetTile(3, 7).RoadLevel);
        Assert.Equal(2, karte2.GetTile(3, 4).RoadLevel);
    }

    [Fact]
    public void WegFuehrtUmWasserFelderUndGebaeudeHerum()
    {
        var (karte, haus, zentrum) = Dorf();
        // Wasser von x = 0 bis 12 in Reihe 6, ein Feld bei (13, 5), ein Haus bei (15, 6):
        // durch Reihe 6 geht es erst ab x = 17
        for (int x = 0; x <= 12; x++)
        {
            var t = karte.GetTile(x, 6);
            t.Type = TileType.Water;
            t.Walkable = false;
        }
        karte.PlantCrop(13, 5, 2);
        karte.AddBuilding(15, 6, "Haus", 0, 2);

        Assert.True(karte.ConnectBuilding(haus, 1) > 0);
        Assert.True(Kette(karte, haus, zentrum));
        Assert.All(Wegkacheln(karte), k =>
        {
            var t = karte.GetTile(k.X, k.Y);
            Assert.Equal(TileType.Grassland, t.Type);
            Assert.False(t.Farm);
            Assert.True(string.IsNullOrEmpty(t.Building));
        });
        Assert.True(Wegkacheln(karte).Count() <= DataMap.ROAD_CONNECT_MAX_TILES);
    }

    [Fact]
    public void IstDieVorderseiteZuGehtDerWegVonDerSeiteAus()
    {
        var (karte, haus, zentrum) = Dorf();
        foreach (int x in new[] { 2, 3 })
        {
            var t = karte.GetTile(x, 4);
            t.Type = TileType.Water;
            t.Walkable = false;
        }
        Assert.True(karte.ConnectBuilding(haus, 1) > 0);
        Assert.True(Kette(karte, haus, zentrum));
        Assert.Equal(0, karte.GetTile(2, 4).RoadLevel);
        Assert.Equal(0, karte.GetTile(3, 4).RoadLevel);
    }

    [Fact]
    public void ZuWeitWegBleibtOhneWeg()
    {
        var karte = DataMap.CreateBlank(60, 12, 16, new MapSettings());
        karte.AddBuilding(50, 4, "Stadtzentrum", 0, 4);
        var haus = karte.AddBuilding(1, 1, "Haus", 0, 2);
        Assert.Equal(0, karte.ConnectBuilding(haus, 1));
        Assert.Empty(Wegkacheln(karte));
    }

    [Fact]
    public void NurDasEigeneStadtzentrumIstZiel()
    {
        var karte = DataMap.CreateBlank(60, 12, 16, new MapSettings());
        karte.AddBuilding(50, 4, "Stadtzentrum", 0, 4);
        karte.AddBuilding(6, 1, "Stadtzentrum", 1, 4);   // das des Gegners, ganz nah
        var haus = karte.AddBuilding(1, 1, "Haus", 0, 2);
        Assert.Equal(0, karte.ConnectBuilding(haus, 1));
        Assert.Empty(Wegkacheln(karte));
    }

    [Fact]
    public void EinStadtzentrumIstNichtSeinEigenesZiel()
    {
        var karte = DataMap.CreateBlank(20, 20, 16, new MapSettings());
        var zentrum = karte.AddBuilding(10, 10, "Stadtzentrum", 0, 4);
        Assert.Equal(0, karte.ConnectBuilding(zentrum, 1));
        Assert.Empty(Wegkacheln(karte));
    }

    [Fact]
    public void EinZweiterAnschlussAendertNichts()
    {
        var (karte, haus, _) = Dorf();
        int erst = karte.ConnectBuilding(haus, 1);
        Assert.True(erst > 0);
        Assert.Equal(0, karte.ConnectBuilding(haus, 1));
        Assert.Equal(erst, Wegkacheln(karte).Count());
    }

    [Fact]
    public void WegDurchDieKartenecke()
    {
        // Haus (2, 1) 2×2: der Ring ist bis auf (1, 0) Wasser, also führt der Weg von (1, 0)
        // durch die Ecke (0, 0) und an der linken Kante hinunter zum Weg bei (0, 5)
        var karte = DataMap.CreateBlank(12, 12, 16, new MapSettings());
        var haus = karte.AddBuilding(2, 1, "Haus", 0, 2);
        foreach (var (x, y) in new[] { (2, 0), (3, 0), (4, 0), (1, 1), (1, 2), (1, 3), (2, 3), (3, 3), (4, 3), (4, 1), (4, 2) })
        {
            var t = karte.GetTile(x, y);
            t.Type = TileType.Water;
            t.Walkable = false;
        }
        karte.GetTile(0, 5).RoadLevel = 1;
        Assert.Equal(6, karte.ConnectBuilding(haus, 1));
        foreach (var (x, y) in new[] { (1, 0), (0, 0), (0, 1), (0, 2), (0, 3), (0, 4) })
            Assert.Equal(1, karte.GetTile(x, y).RoadLevel);
    }

    [Fact]
    public void SpielstandStelltDieErreichteWegStufeWiederHer()
    {
        var karte = new DataMap(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        var p1 = new DataPlayer(0, "Spieler", "Briten");
        var p2 = new DataPlayer(1, "Gegner", "Azteken");
        p2.Resources.Add(Resource.Food, 5000);
        p2.Resources.Add(Resource.Gold, 5000);
        Assert.True(p2.Ages.TryStart(p2.Resources));
        Assert.True(p2.Ages.Update(10000f));
        Assert.Equal(Age.Feudal, p2.Ages.Current);
        karte.UpgradeRoads(DataMap.LevelOfAge(p2.Ages.Current));

        var (geladen, _, _) = SaveGameMapper.Restore(SaveGameMapper.Capture(karte, p1, p2, MapSize.Test));
        Assert.Equal(1, geladen.CurrentRoadLevel);
        // Neue Tritte nach dem Laden sind sofort Wege
        var frei = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 30).Select(y => (x, y)))
            .First(k => geladen.GetTile(k.x, k.y).Type == TileType.Grassland && geladen.GetTile(k.x, k.y).Wear <= 0f);
        Austreten(geladen, frei.x, frei.y);
        Assert.Equal(1, geladen.GetTile(frei.x, frei.y).RoadLevel);
    }
}
