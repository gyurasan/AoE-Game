using System.Collections.Generic;
using AgeOfEvolutions.Core.Data;
using AoE.Core.Economy;
using Microsoft.Xna.Framework;
using Xunit;
using CorePosition = AoE.Core.Entities.Position;
using Resource = AoE.Core.Entities.Resource;
using UnitType = AgeOfEvolutions.Core.Data.UnitType;

namespace AoE.Tests;

/// <summary>
/// Jagd (F11, Nutzer 2026-11-04): „Reh flieht vor Dorfbewohnern und muss erst erlegt
/// werden", „Wildschwein greift den Jäger an und lässt sich zum Stadtzentrum locken" und
/// „Erlegtes Wild verliert langsam Nahrung". Was der Spielbildschirm daraus macht (Jagd
/// per Klick, Aasstufen, Sprungbilder), prüft tools/spielablauf.
/// </summary>
public class JagdTests
{
    private const int TS = 32;

    private static TileMap Leer() => TileMap.CreateBlank(30, 30, TS, new MapSettings());

    private static Vector2 Mitte(int x, int y) => new(x * TS + TS / 2, y * TS + TS / 2);

    private static Unit Einheit(UnitType typ, Vector2 lage, int besitzer = 0)
        => new(typ, besitzer) { Position = lage };

    /// <summary>
    /// Stellt Reh oder Wildschwein manuell auf (10, 10) und meldet es über
    /// RebuildWildAnimals im Wander-Bestand an. WanderTimer groß - ohne Bedrohung
    /// soll das Tier stehen, mit Bedrohung im Flucht-/Verfolgungstakt treten.
    /// </summary>
    private static WildAnimal Stelle(TileMap karte, FoodSource art, int x = 10, int y = 10)
    {
        var t = karte.GetTile(x, y);
        t.Food = art;
        t.ResourceType = Resource.Food;
        t.ResourceAmount = art == FoodSource.Deer ? karte.Settings.DeerFood : karte.Settings.BoarFood;
        t.Animal = new WildAnimal { Health = karte.PredatorMaxHealth(art), WanderTimer = 1000f };
        karte.RebuildWildAnimals();
        return t.Animal;
    }

    private static int AbstandZu(TileMap karte, WildAnimal tier, Vector2 punkt)
    {
        var lage = karte.LocatePredator(tier).Value;
        var g = karte.WorldToGrid(punkt);
        return System.Math.Max(System.Math.Abs(lage.X - (int)g.X), System.Math.Abs(lage.Y - (int)g.Y));
    }

    // --- Erst erlegen, dann ernten -------------------------------------------------

    [Fact]
    public void LebendesWildIstKeineErntequelle()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        var welt = new TileMapGatherWorld(karte);
        Assert.Equal(0, welt.AmountAt(new CorePosition(10, 10), Resource.Food));   // live: nichts zu holen
        Assert.True(karte.IsLiveHuntable(10, 10));
        Assert.False(karte.IsLivePredator(10, 10));                                // kein Raubtier

        // erst Angriffe erlegen es - ein Treffer allein nicht
        Assert.False(karte.DamageAnimal(10, 10, 10));
        Assert.True(karte.DamageAnimal(10, 10, 999));
        Assert.True(karte.GetTile(10, 10).Animal.Slaughtered);
        Assert.Equal(karte.Settings.DeerFood, karte.GetTile(10, 10).ResourceAmount);

        // Aas ist erntbar, lebendes Wild blieb tabu für den Sammler
        Assert.Equal(karte.Settings.DeerFood, welt.AmountAt(new CorePosition(10, 10), Resource.Food));
    }

    [Fact]
    public void AasLaesstSichErnten()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        karte.DamageAnimal(10, 10, 999);
        var welt = new TileMapGatherWorld(karte);
        Assert.Equal(10, welt.Harvest(new CorePosition(10, 10), Resource.Food, 10));
        Assert.Equal(karte.Settings.DeerFood - 10, karte.GetTile(10, 10).ResourceAmount);
    }

    [Fact]
    public void AelteresRehOhneLebenspunkteWirdBeimNeuaufbauGesund()
    {
        var karte = Leer();
        var t = karte.GetTile(10, 10);
        t.Food = FoodSource.Deer;
        t.ResourceType = Resource.Food;
        t.ResourceAmount = karte.Settings.DeerFood;
        t.Animal = new WildAnimal();   // alter Spielstand: Health 0, nicht geschlachtet
        karte.RebuildWildAnimals();
        Assert.Equal(karte.Settings.DeerHealth, t.Animal.Health);

        var eber = karte.GetTile(12, 10);
        eber.Food = FoodSource.Boar;
        eber.ResourceType = Resource.Food;
        eber.ResourceAmount = karte.Settings.BoarFood;
        eber.Animal = new WildAnimal();
        karte.RebuildWildAnimals();
        Assert.Equal(karte.Settings.BoarHealth, eber.Animal.Health);
    }

    // --- Reh flieht ----------------------------------------------------------------

    [Fact]
    public void RehFliehtVorNaechsterEinheit()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        var dorfie = Einheit(UnitType.Villager, Mitte(12, 10));
        var einheiten = new List<Unit> { dorfie };

        // steht erst ruhig (WanderTimer groß, aber die Flucht bremst nicht)
        for (int i = 0; i < 120; i++)
            karte.UpdateDeer(1f / 60f, einheiten);

        Assert.True(reh.Fleeing, "Reh steht, obwohl ein Dorfbewohner zwei Kacheln entfernt steht");
        Assert.True(AbstandZu(karte, reh, dorfie.Position) > 2, "Reh ist nicht gewichen");
        // und es bleibt in Reichweite nicht stehen: nach weiteren Sekunden wächst der Abstand
        int davor = AbstandZu(karte, reh, dorfie.Position);
        for (int i = 0; i < 240; i++)
            karte.UpdateDeer(1f / 60f, einheiten);
        Assert.True(AbstandZu(karte, reh, dorfie.Position) >= davor);
    }

    [Fact]
    public void RehOhneBedrohungFliehtNicht()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        var weitWeg = Einheit(UnitType.Villager, Mitte(28, 28));
        for (int i = 0; i < 300; i++)
            karte.UpdateDeer(1f / 60f, new List<Unit> { weitWeg });
        Assert.False(reh.Fleeing);
    }

    [Fact]
    public void VerletztesRehFliehtNichtMehr()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        Assert.False(karte.DamageAnimal(10, 10, 1));   // nur verletzt, nicht tot
        var dorfie = Einheit(UnitType.Villager, Mitte(11, 10));
        var lage = karte.LocatePredator(reh);
        for (int i = 0; i < 300; i++)
            karte.UpdateDeer(1f / 60f, new List<Unit> { dorfie });
        Assert.False(reh.Fleeing);                     // die Flucht ist vorbei - es steht
        Assert.Equal(lage, karte.LocatePredator(reh));
    }

    [Fact]
    public void ErlegtesRehFliehtNichtMehr()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        karte.DamageAnimal(10, 10, 999);
        var dorfie = Einheit(UnitType.Villager, Mitte(11, 10));
        var lage = karte.LocatePredator(reh);
        for (int i = 0; i < 300; i++)
            karte.UpdateDeer(1f / 60f, new List<Unit> { dorfie });
        Assert.False(reh.Fleeing);
        Assert.Equal(lage, karte.LocatePredator(reh));   // Aas liegt an Ort und Stelle
    }

    // --- Wildschwein wehrt sich ------------------------------------------------------

    [Fact]
    public void WildschweinSchlaegtJedenInReichweite()
    {
        var karte = Leer();
        var eber = Stelle(karte, FoodSource.Boar);
        var dorfie = Einheit(UnitType.Villager, Mitte(11, 10));
        var strikes = new List<PredatorStrike>();
        for (int i = 0; i < 60; i++)
            strikes.AddRange(karte.UpdatePredators(1f / 60f, new List<Unit> { dorfie }));

        Assert.Contains(strikes, s => s.Kind == FoodSource.Boar);
        Assert.True(dorfie.Health < dorfie.MaxHealth, "Eber hat nicht zugebissen");
        Assert.Equal(FoodSource.Boar, karte.GetTile(10, 10).Food);   // er bleibt stehen
    }

    [Fact]
    public void WildschweinFolgtSeinemAngreifer()
    {
        var karte = Leer();
        var eber = Stelle(karte, FoodSource.Boar);
        var jaeger = Einheit(UnitType.Villager, Mitte(14, 10));   // 4 Kacheln weg, außer Reichweite
        karte.DamageAnimal(10, 10, 1, jaeger);                    // ein Schlag provoziert ihn
        var start = karte.LocatePredator(eber).Value;

        for (int i = 0; i < 60 * 4; i++)
            karte.UpdatePredators(1f / 60f, new List<Unit> { jaeger });

        var jetzt = karte.LocatePredator(eber).Value;
        Assert.True(System.Math.Abs(jetzt.X - 14) < System.Math.Abs(start.X - 14),
            $"Eber folgt nicht: {start} -> {jetzt}");
    }

    [Fact]
    public void VergessenerAngreiferBeendetDieVerfolgung()
    {
        var karte = Leer();
        var eber = Stelle(karte, FoodSource.Boar);
        var jaeger = Einheit(UnitType.Villager, Mitte(14, 10));
        karte.DamageAnimal(10, 10, 1, jaeger);
        // außerhalb der Rufweite: Eber vergisst ihn und rastet wieder ein
        jaeger.Position = Mitte(28, 28);
        for (int i = 0; i < 60 * 10; i++)
            karte.UpdatePredators(1f / 60f, new List<Unit> { jaeger });
        Assert.Equal(0f, eber.ProvokedSeconds);
        Assert.Null(eber.ProvokedBy);
    }

    // --- Aas verdirbt ----------------------------------------------------------------

    [Fact]
    public void ErlegtesWildVerliertNahrungUndSchrumpft()
    {
        var karte = Leer();
        var reh = Stelle(karte, FoodSource.Deer);
        karte.DamageAnimal(10, 10, 999);

        Assert.Equal(0, karte.MeatStage(10, 10));
        karte.UpdateSpoilage(60f);   // 0,4 Nahrung je Sekunde
        int erwartung = karte.Settings.DeerFood - 24;
        Assert.Equal(erwartung, karte.GetTile(10, 10).ResourceAmount);
        Assert.Equal(0, karte.MeatStage(10, 10));   // noch über drei Viertel

        karte.UpdateSpoilage(40f);   // weitere 16: unter drei Viertel, über die Hälfte
        Assert.True(karte.GetTile(10, 10).ResourceAmount < karte.Settings.DeerFood * 3 / 4);
        Assert.True(karte.GetTile(10, 10).ResourceAmount > karte.Settings.DeerFood / 2);
        Assert.Equal(1, karte.MeatStage(10, 10));
    }

    [Fact]
    public void LeeresAasVerschwindet()
    {
        var karte = Leer();
        var eber = Stelle(karte, FoodSource.Boar);
        karte.DamageAnimal(10, 10, 999);
        karte.UpdateSpoilage(100000f);
        var t = karte.GetTile(10, 10);
        Assert.Equal(FoodSource.None, t.Food);
        Assert.Null(t.Animal);
        Assert.Equal(0, karte.BoarCount);
    }

    [Fact]
    public void SchafeUndKaninchenVerderbenNicht()
    {
        var karte = Leer();
        var schaf = karte.GetTile(10, 10);
        schaf.Food = FoodSource.Sheep;
        schaf.ResourceType = Resource.Food;
        schaf.ResourceAmount = karte.Settings.SheepFood;
        schaf.Animal = new WildAnimal { Slaughtered = true };
        karte.RebuildWildAnimals();
        karte.UpdateSpoilage(1000f);
        Assert.Equal(karte.Settings.SheepFood, karte.GetTile(10, 10).ResourceAmount);
    }
}
