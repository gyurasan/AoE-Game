using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using Player = AgeOfEvolutions.Core.Data.Player;
using UnitType = AgeOfEvolutions.Core.Data.UnitType;

namespace AoE.Tests;

/// <summary>
/// Spielstand (SV2): die ganze Welt geht in ein <see cref="SaveGame"/>, über JSON und
/// zurück - und danach ist sie dieselbe. Maßstab ist der Ringschluss: speichern, laden,
/// noch einmal speichern ergibt Zeichen für Zeichen denselben Spielstand.
/// </summary>
public class SaveGameTests
{
    /// <summary>
    /// Eine kleine Welt mitten im Spiel: Rohstoffe, Webstuhl erforscht, Aufstieg und
    /// Ausbildung laufen, eine Forschung beim Gegner, ein Haus im Bau, Sammelauftrag,
    /// Bauarbeiter, Angriff, ein Weg, ein Feld, ein geschlachtetes Schaf, Erkundung.
    /// </summary>
    private sealed class Welt
    {
        public readonly TileMap Map = new(40, 30, 32, MapSettings.ForSize(MapSize.Test));
        public readonly Player P1 = new(0, "Spieler", "Briten");
        public readonly Player P2 = new(1, "Gegner", "Azteken");
        public readonly Building Haus;
        public readonly Unit Sammler, Bauer, Laeufer, Kaempfer;

        public Welt()
        {
            P1.Resources.Add(Resource.Food, 1000);
            P1.Resources.Add(Resource.Gold, 333);
            P1.Techs.Begin(Tech.Loom);
            P1.Techs.Complete(Tech.Loom);
            Assert.True(P1.Ages.TryStart(P1.Resources));
            P1.Ages.Update(42.5f);

            // Wie im Spiel: erforschte Wirkungen gelten für alles, was der Spieler hat
            foreach (var u in Map.Units.Where(u => u.OwnerId == 0))
                TechEffects.ApplyAll(P1.Techs, u.Core);

            var tc1 = Map.Buildings.First(b => b.OwnerId == 0);
            var tc2 = Map.Buildings.First(b => b.OwnerId == 1);
            Assert.True(tc1.Training.Enqueue(UnitType.Villager, new Dictionary<Resource, int> { [Resource.Food] = 50 }, 25f, P1.Resources));
            Assert.True(tc1.Training.Enqueue(UnitType.Villager, new Dictionary<Resource, int> { [Resource.Food] = 50 }, 25f, P1.Resources));
            tc1.Training.Update(7.25f, 0, 10, out _);
            Assert.True(tc2.Research.TryStart(Tech.Loom, Age.Dark, P2.Techs, P2.Resources));
            tc2.Research.Update(3.5f, P2.Techs, out _);
            tc2.Health -= 123;

            var (hx, hy) = FreierPlatz(2, 8, 8);
            Haus = Map.AddBuilding(hx, hy, "Haus", 0, 2);
            Haus.Construction = new Construction(BuildingRules.BuildSecondsOf(BuildingType.House));
            Haus.Construction.Update(5f, 1);
            Haus.Health = 77;

            var (fx, fy) = FreierPlatz(3, 14, 4);
            Map.PlantCrop(fx, fy, 3, 222);
            var feld = Map.GetTile(fx + 1, fy + 1);
            Map.ClearResource(feld);
            feld.FarmRegrow = 12.5f;
            Map.GetTile(5, 9).Wear = 0.6f;

            Assert.True(Map.SheepCount + Map.DeerCount > 0, "Die Testkarte braucht Tiere");
            var tier = Alle().First(t => t.Food.IsWild());
            Map.Slaughter(tier.X, tier.Y);
            tier.ResourceAmount -= 13;

            var eigene = Map.Units.Where(u => u.OwnerId == 0).ToList();
            var gegner = Map.Units.First(u => u.OwnerId == 1);
            Sammler = eigene[0];
            Bauer = eigene[1];
            Laeufer = eigene[2];
            Kaempfer = eigene[3];

            var wald = Alle().First(t => t.ResourceType == Resource.Wood && t.ResourceAmount > 0);
            Sammler.Job = new GatherJob(0, Resource.Wood, new Position(wald.X, wald.Y), 4);
            Sammler.State = UnitState.Gathering;

            Bauer.BuildSite = Haus;
            Bauer.State = UnitState.Building;

            Laeufer.Path = new List<Vector2> { new(100.5f, 200.25f), new(132f, 232f), new(164.75f, 240f) };
            Laeufer.TargetPosition = new Vector2(164.75f, 240f);
            Laeufer.Pace = 0.75f;
            Laeufer.State = UnitState.Moving;
            Laeufer.Position += new Vector2(3.5f, -1.25f);

            Kaempfer.UnitTarget = gegner;
            Kaempfer.AttackTarget = tc2;
            Kaempfer.AttackTimer = 0.4f;
            Kaempfer.Health -= 7;
            Kaempfer.State = UnitState.Attacking;

            Map.UpdateFogOfWarForPlayer(0, Map.Units);
            Map.UpdateFogOfWarForPlayer(1, Map.Units);
        }

        public IEnumerable<Tile> Alle()
        {
            for (int x = 0; x < Map.Width; x++)
                for (int y = 0; y < Map.Height; y++)
                    yield return Map.GetTile(x, y);
        }

        private (int X, int Y) FreierPlatz(int groesse, int abX, int abY)
        {
            for (int x = abX; x < Map.Width - groesse; x++)
                for (int y = abY; y < Map.Height - groesse; y++)
                    if (Map.CanPlaceBuilding(x, y, groesse))
                        return (x, y);
            throw new InvalidOperationException("kein freier Platz");
        }

        public SaveGame Speichern() => SaveGameMapper.Capture(Map, P1, P2, MapSize.Test);
    }

    private static string Json(TileMap map, Player p1, Player p2)
        => SaveGameStore.Serialize(SaveGameMapper.Capture(map, p1, p2, MapSize.Test));

    // --- Ringschluss ---------------------------------------------------------

    [Fact]
    public void Ringschluss_ErgibtDenselbenSpielstand()
    {
        var welt = new Welt();
        string json = SaveGameStore.Serialize(welt.Speichern());

        var (map, p1, p2) = SaveGameMapper.Restore(SaveGameStore.Deserialize(json));

        Assert.Equal(json, Json(map, p1, p2));
    }

    [Fact]
    public void Capture_AendertDieWeltNicht()
    {
        var welt = new Welt();

        string erst = SaveGameStore.Serialize(welt.Speichern());
        string dann = SaveGameStore.Serialize(welt.Speichern());

        Assert.Equal(erst, dann);
    }

    [Fact]
    public void Capture_HaeltDieWeltFest()
    {
        var welt = new Welt();

        var stand = welt.Speichern();

        Assert.Equal(SaveGame.CURRENT_VERSION, stand.Version);
        Assert.Equal(MapSize.Test, stand.MapSize);
        Assert.Equal((40, 30, 32), (stand.Width, stand.Height, stand.TileSize));
        Assert.Equal(default, stand.SavedAt);
        Assert.Equal(40 * 30, stand.Tiles.Count);
        Assert.Equal(new[] { 0, 1 }, stand.Players.Select(p => p.Id));
        Assert.Equal("Gegner", stand.Players[1].Name);
        Assert.Equal(welt.P1.Resources[Resource.Gold], stand.Players[0].Gold);
        Assert.Equal(Age.Feudal, stand.Players[0].Ages.Target);
        Assert.Equal(new[] { Tech.Loom }, stand.Players[0].Techs.Researched);
        Assert.Equal(new[] { Tech.Loom }, stand.Players[1].Techs.Pending);
        Assert.Equal(40 * 30, stand.Players[0].Explored.Length);
        Assert.Contains('1', stand.Players[0].Explored);
        Assert.Equal(0.6f, stand.Tiles[9 * 40 + 5].Wear);

        int haus = welt.Map.Buildings.IndexOf(welt.Haus);
        Assert.Equal("Haus", stand.Buildings[haus].Type);
        Assert.Equal(2, stand.Buildings[haus].Size);
        Assert.Equal(77, stand.Buildings[haus].Health);
        Assert.NotNull(stand.Buildings[haus].Construction);
        Assert.Null(stand.Buildings[0].Construction);
        Assert.Equal(2, stand.Buildings[0].Training.Entries.Count);

        var bauer = stand.Units[welt.Map.Units.IndexOf(welt.Bauer)];
        Assert.Equal(haus, bauer.BuildSite);
        Assert.Null(bauer.UnitTarget);
        var kaempfer = stand.Units[welt.Map.Units.IndexOf(welt.Kaempfer)];
        Assert.Equal(welt.Map.Units.FindIndex(u => u.OwnerId == 1), kaempfer.UnitTarget);
        var laeufer = stand.Units[welt.Map.Units.IndexOf(welt.Laeufer)];
        Assert.Equal(3, laeufer.Path.Count);
        Assert.Equal(new PointSave(132f, 232f), laeufer.Path[1]);
        Assert.Equal(welt.Laeufer.Position.X, laeufer.X);
        var sammler = stand.Units[welt.Map.Units.IndexOf(welt.Sammler)];
        Assert.Equal(4, sammler.Job.Carrying);
    }

    [Fact]
    public void Restore_StelltDieWeltWiederHer()
    {
        var welt = new Welt();

        var (map, p1, p2) = SaveGameMapper.Restore(SaveGameStore.Deserialize(SaveGameStore.Serialize(welt.Speichern())));

        // Spieler: Rohstoffe genau, trotz Startkapital und Volksbonus im Konstruktor
        foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
        {
            Assert.Equal(welt.P1.Resources[r], p1.Resources[r]);
            Assert.Equal(welt.P2.Resources[r], p2.Resources[r]);
        }
        Assert.Equal(welt.P1.Ages.Progress, p1.Ages.Progress);
        Assert.True(p1.Techs.IsResearched(Tech.Loom));
        Assert.True(p2.Techs.IsPending(Tech.Loom));
        Assert.Empty(p1.Units);

        // Gebäude: Grundfläche gesperrt, Baustelle, Ausbildung, Forschung
        Assert.Equal(welt.Map.Buildings.Count, map.Buildings.Count);
        var haus = map.Buildings[welt.Map.Buildings.IndexOf(welt.Haus)];
        Assert.Equal(welt.Haus.Construction.Progress, haus.Construction.Progress);
        Assert.Equal(77, haus.Health);
        Assert.Equal("Haus", map.GetTile(haus.X, haus.Y).Building);
        Assert.False(map.GetTile(haus.X + 1, haus.Y + 1).Walkable);
        Assert.Equal(welt.Map.Buildings[0].Training.Progress, map.Buildings[0].Training.Progress);
        Assert.Equal(2, map.Buildings[0].Training.Count);
        var tc2 = map.Buildings.First(b => b.OwnerId == 1);
        Assert.Equal(Tech.Loom, tc2.Research.Current);
        Assert.Equal(welt.Map.Buildings.First(b => b.OwnerId == 1).Health, tc2.Health);

        // Einheiten: Webstuhl wirkt wieder (mehr Lebenspunkte), Lebenspunkte wie gespeichert
        Assert.Equal(welt.Map.Units.Count, map.Units.Count);
        var bauer = map.Units[welt.Map.Units.IndexOf(welt.Bauer)];
        Assert.Equal(welt.Bauer.MaxHealth, bauer.MaxHealth);
        Assert.Equal(welt.Bauer.Health, bauer.Health);
        Assert.Same(haus, bauer.BuildSite);
        var kaempfer = map.Units[welt.Map.Units.IndexOf(welt.Kaempfer)];
        Assert.Equal(welt.Kaempfer.Health, kaempfer.Health);
        Assert.Same(map.Units[welt.Map.Units.IndexOf(welt.Kaempfer.UnitTarget)], kaempfer.UnitTarget);
        Assert.Same(tc2, kaempfer.AttackTarget);
        Assert.Equal(welt.Kaempfer.Id, kaempfer.Id);
        var laeufer = map.Units[welt.Map.Units.IndexOf(welt.Laeufer)];
        Assert.Equal(welt.Laeufer.Position, laeufer.Position);
        Assert.Equal(welt.Laeufer.Path, laeufer.Path);
        Assert.Equal(UnitState.Moving, laeufer.State);
        var sammler = map.Units[welt.Map.Units.IndexOf(welt.Sammler)];
        Assert.Equal(welt.Sammler.Job.ToState(), sammler.Job.ToState());

        // Karte: jedes Tier wandert wieder. Maßstab sind die Tierkacheln, nicht die alten
        // Listen - der Kartengenerator trägt manche Tiere doppelt ein (zwei Würfe auf dieselbe
        // Kachel), nach dem Laden zählt jede Tierkachel genau einmal.
        int Tiere(FoodSource art) => welt.Alle().Count(t => t.Food == art);
        Assert.Equal(Tiere(FoodSource.Sheep), map.SheepCount);
        Assert.Equal(Tiere(FoodSource.Deer), map.DeerCount);
        Assert.Equal(Tiere(FoodSource.Rabbit), map.RabbitCount);
        Assert.Equal(Tiere(FoodSource.Boar), map.BoarCount);
        Assert.True(map.SheepCount + map.DeerCount > 0);
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                Assert.Equal(welt.Map.IsTileExplored(x, y, 0), map.IsTileExplored(x, y, 0));
                Assert.Equal(welt.Map.IsTileExplored(x, y, 1), map.IsTileExplored(x, y, 1));
                Assert.Equal(welt.Map.GetTile(x, y).Food, map.GetTile(x, y).Food);
            }
    }

    [Fact]
    public void Restore_VerweisAusserhalbDerListe_WirdNull()
    {
        var welt = new Welt();
        var stand = welt.Speichern();
        int bauer = welt.Map.Units.IndexOf(welt.Bauer);
        int kaempfer = welt.Map.Units.IndexOf(welt.Kaempfer);
        stand.Units[bauer].BuildSite = 999;
        stand.Units[kaempfer].UnitTarget = -1;
        stand.Units[kaempfer].AttackTarget = stand.Buildings.Count;

        var (map, _, _) = SaveGameMapper.Restore(stand);

        Assert.Null(map.Units[bauer].BuildSite);
        Assert.Null(map.Units[kaempfer].UnitTarget);
        Assert.Null(map.Units[kaempfer].AttackTarget);
    }

    [Fact]
    public void Restore_PrueftDenSpielstand()
    {
        Assert.Throws<ArgumentNullException>(() => SaveGameMapper.Restore(null));

        var falscheFassung = new Welt().Speichern();
        falscheFassung.Version = SaveGame.CURRENT_VERSION + 1;
        Assert.Throws<InvalidDataException>(() => SaveGameMapper.Restore(falscheFassung));

        var zuWenigKacheln = new Welt().Speichern();
        zuWenigKacheln.Tiles.RemoveAt(0);
        Assert.Throws<InvalidDataException>(() => SaveGameMapper.Restore(zuWenigKacheln));

        var einSpieler = new Welt().Speichern();
        einSpieler.Players.RemoveAt(1);
        Assert.Throws<InvalidDataException>(() => SaveGameMapper.Restore(einSpieler));

        var ohneFlaeche = new Welt().Speichern();
        ohneFlaeche.Width = 0;
        ohneFlaeche.Tiles.Clear();
        Assert.Throws<InvalidDataException>(() => SaveGameMapper.Restore(ohneFlaeche));
    }

    // --- TileMap -------------------------------------------------------------

    [Fact]
    public void LeereKarte_HatNurWiese()
    {
        var map = TileMap.CreateBlank(12, 7, 32, MapSettings.Default);

        Assert.Equal((12, 7, 32), (map.Width, map.Height, map.TileSize));
        Assert.Empty(map.Units);
        Assert.Empty(map.Buildings);
        Assert.Equal(0, map.SheepCount + map.DeerCount + map.RabbitCount + map.BoarCount);
        for (int x = 0; x < 12; x++)
            for (int y = 0; y < 7; y++)
            {
                var t = map.GetTile(x, y);
                Assert.Equal((x, y), (t.X, t.Y));
                Assert.Equal(TileType.Grassland, t.Type);
                Assert.True(t.Walkable);
                Assert.Null(t.ResourceType);
            }
    }

    [Fact]
    public void Tierbestand_KommtAusDenKacheln()
    {
        var map = TileMap.CreateBlank(10, 10, 32, MapSettings.Default);
        map.GetTile(2, 3).Food = FoodSource.Sheep;
        map.GetTile(4, 4).Food = FoodSource.Deer;
        map.GetTile(5, 1).Food = FoodSource.Rabbit;
        map.GetTile(7, 8).Food = FoodSource.Boar;
        map.GetTile(8, 8).Food = FoodSource.Boar;
        map.GetTile(6, 6).Food = FoodSource.Berries;

        map.RebuildWildAnimals();
        map.RebuildWildAnimals();   // ein zweites Mal zählt nichts doppelt

        Assert.Equal((1, 1, 1, 2), (map.SheepCount, map.DeerCount, map.RabbitCount, map.BoarCount));
    }

    [Fact]
    public void Erkundung_Maske_HinUndZurueck()
    {
        var map = TileMap.CreateBlank(6, 4, 32, MapSettings.Default);
        Assert.Equal(new string('0', 24), map.ExploredMask(0));

        string maske = "100000" + "010000" + "001000" + "000001";
        map.RestoreExplored(1, maske);

        Assert.Equal(maske, map.ExploredMask(1));
        Assert.Equal(new string('0', 24), map.ExploredMask(0));
        Assert.True(map.IsTileExplored(2, 2, 1));
        Assert.False(map.IsTileExplored(3, 2, 1));
        Assert.True(map.IsTileExplored(5, 3, 1));

        // Zu kurz oder null: der Rest gilt als unerforscht
        map.RestoreExplored(1, "11");
        Assert.Equal("11" + new string('0', 22), map.ExploredMask(1));
        map.RestoreExplored(1, null);
        Assert.Equal(new string('0', 24), map.ExploredMask(1));
    }

    // --- Datei ---------------------------------------------------------------

    [Fact]
    public void Datei_SpeichernUndLaden()
    {
        string ordner = Path.Combine(Path.GetTempPath(), "aoe-spielstand-" + Guid.NewGuid().ToString("N"));
        string pfad = Path.Combine(ordner, "unter", SaveGameStore.FILE_NAME);
        try
        {
            var store = new SaveGameStore(pfad);
            Assert.Equal(pfad, store.FilePath);
            Assert.False(store.Exists);
            Assert.Throws<FileNotFoundException>(() => store.Load());

            var welt = new Welt();
            var stand = welt.Speichern();
            store.Save(stand);
            Assert.True(store.Exists);
            Assert.False(File.Exists(pfad + ".tmp"));
            Assert.Equal(SaveGameStore.Serialize(stand), SaveGameStore.Serialize(store.Load()));

            // Ein neuer Spielstand ersetzt den alten
            stand.GameSeconds = 4711.5;
            store.Save(stand);
            Assert.Equal(4711.5, store.Load().GameSeconds);
            Assert.False(File.Exists(pfad + ".tmp"));
        }
        finally
        {
            if (Directory.Exists(ordner))
                Directory.Delete(ordner, true);
        }
    }

    [Fact]
    public void Datei_StandardOrt_NebenDenEinstellungen()
    {
        string erwartet = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                                       "AgeOfEvolutions", SaveGameStore.FILE_NAME);

        Assert.Equal(erwartet, new SaveGameStore().FilePath);
        Assert.Equal(erwartet, new SaveGameStore("").FilePath);
    }

    [Fact]
    public void Json_AufzaehlungenAlsNamen_StandardwerteWeggelassen()
    {
        string json = SaveGameStore.Serialize(new Welt().Speichern());

        Assert.Contains("\"Feudal\"", json);
        Assert.DoesNotContain("\"Wear\":0,", json);
        Assert.Throws<ArgumentNullException>(() => SaveGameStore.Serialize(null));
    }

    [Theory]
    [InlineData("kein json")]
    [InlineData("null")]
    [InlineData("")]
    [InlineData(null)]
    [InlineData("{\"Tiles\": 5}")]
    public void Json_Unlesbar_WirftInvalidData(string json)
    {
        Assert.Throws<InvalidDataException>(() => SaveGameStore.Deserialize(json));
    }
}
