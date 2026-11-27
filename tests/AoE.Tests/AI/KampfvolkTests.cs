using System.Linq;
using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// This is Sparta als Kampfvolk (<see cref="AiProfile.WarriorPeople"/>, Nutzer 2026-11-27: „sparta ist eine Kampf
/// Folk“, „Die Kaserne bleibt leer, weil der billigere Dorfbewohner (25 Nahrung) jedes Mal vor der Miliz (60) gebaut
/// wird ... das muss natürlich berücksichtigt werden. Sparta entwickelt sich langsamer .. dafür sind sie
/// "Agresiver"“): Kaserne zuerst, Milizen vor Dorfbewohnern, Sammler mehr auf Nahrung.
/// </summary>
public class KampfvolkTests
{
    private const string Miliz = "soldier(Barracks,Militia)";
    private const string Dorfbewohner = "train()";

    private static void Tick(AiProfile p, FakeWorld w) => new EconomyAi(p).Tick(w, w, 0.5f, new AiContext(1));

    /// <summary>Stadtzentrum, wahlweise eine fertige Kaserne, <paramref name="villagers"/> Dorfbewohner beim
    /// Sammeln (nicht frei, damit nichts gebaut wird), <paramref name="militia"/> Milizen; Platz für 30.</summary>
    private static FakeWorld Dorf(int villagers, int militia, int food, Age age = Age.Dark, bool barracks = true)
    {
        var w = new FakeWorld { Age = age, PopulationCount = villagers + militia, PopulationCapacity = 30,
                                Resources = new ResourceVector(food, 100, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        if (barracks) w.AddBuilding(BuildingType.Barracks, 20, 10, complete: true);
        for (int i = 0; i < villagers; i++)
            w.AddVillager(12 + i % 5, 16, UnitStateKind.Gathering, gathering: (30, 30, Resource.Food));
        for (int i = 0; i < militia; i++) w.AddSoldier(14 + i, 14, UnitKind.Militia);
        return w;
    }

    [Fact]
    public void Die_Kaserne_hat_Vorrang_vor_dem_Stadtzentrum()
    {
        var w = Dorf(12, 1, food: 70);   // 70 Nahrung: eine Miliz, aber nicht beides
        Tick(AiProfile.Sparta, w);
        Assert.Contains(Miliz, w.Log);
        Assert.DoesNotContain(Dorfbewohner, w.Log);
    }

    [Fact]
    public void Reicht_die_Nahrung_fuer_beide_bildet_auch_das_Stadtzentrum_aus()
    {
        var w = Dorf(12, 1, food: EconomyAi.VillagerFoodCost + EconomyAi.MilitiaFoodCost);
        Tick(AiProfile.Sparta, w);
        Assert.Contains(Miliz, w.Log);
        Assert.Contains(Dorfbewohner, w.Log);
    }

    [Fact]
    public void Unter_der_Mindestzahl_Dorfbewohner_waechst_erst_das_Dorf()
    {
        var w = Dorf(EconomyAi.WarriorVillagerFloor - 1, 1, food: 70);
        Tick(AiProfile.Sparta, w);
        Assert.Contains(Dorfbewohner, w.Log);
    }

    [Fact]
    public void Steht_die_erste_Welle_bildet_das_Stadtzentrum_wieder_aus()
    {
        int welle = AiProfile.Sparta.RaidMinSoldiers + EconomyAi.WarriorArmyExtra;
        var voll = Dorf(12, welle, food: 70);
        Tick(AiProfile.Sparta, voll);
        Assert.Contains(Dorfbewohner, voll.Log);

        // eingereihte Milizen zählen mit
        var eingereiht = Dorf(12, welle - 2, food: 70);
        eingereiht.Queued[UnitKind.Militia] = 2;
        Tick(AiProfile.Sparta, eingereiht);
        Assert.Contains(Dorfbewohner, eingereiht.Log);

        var knapp = Dorf(12, welle - 1, food: 70);
        Tick(AiProfile.Sparta, knapp);
        Assert.DoesNotContain(Dorfbewohner, knapp.Log);
    }

    [Fact]
    public void Nur_in_der_Dunklen_Zeit_und_nur_mit_fertiger_Kaserne()
    {
        var feudal = Dorf(12, 1, food: 70, age: Age.Feudal);
        Tick(AiProfile.Sparta, feudal);
        Assert.Contains(Dorfbewohner, feudal.Log);

        var ohne = Dorf(12, 1, food: 70, barracks: false);
        Tick(AiProfile.Sparta, ohne);
        Assert.Contains(Dorfbewohner, ohne.Log);
    }

    [Fact]
    public void Normal_und_Defense_bilden_wie_bisher_aus()
    {
        foreach (var p in new[] { AiProfile.Standard, AiProfile.Defense })
        {
            var w = Dorf(12, 1, food: 70);
            Tick(p, w);
            Assert.Contains(Dorfbewohner, w.Log);
        }
    }

    /// <summary>Stadtzentrum ohne Kaserne, freie Dorfbewohner (Standard vier), 100 Nahrung (unter der Farmschwelle).</summary>
    private static FakeWorld Bauplatz(int wood, int villagers = 4)
    {
        var w = new FakeWorld { PopulationCount = 4, PopulationCapacity = 30,
                                Resources = new ResourceVector(100, wood, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        for (int i = 0; i < villagers; i++) w.AddVillager(12 + i, 16);
        w.AddSource(Resource.Wood, 24, 16);
        return w;
    }

    [Fact]
    public void Das_Holz_wird_fuer_die_Kaserne_gespart()
    {
        var normal = Bauplatz(wood: 100);
        Tick(AiProfile.Standard, normal);
        Assert.Contains(normal.Log, l => l.StartsWith("build(Farm"));   // Normal baut die Farm

        var sparta = Bauplatz(wood: 100);
        Tick(AiProfile.Sparta, sparta);
        Assert.DoesNotContain(sparta.Log, l => l.StartsWith("build("));   // Sparta spart für die Kaserne
    }

    [Fact]
    public void Die_Kaserne_kommt_vor_Farm_und_Lager()
    {
        var w = Bauplatz(wood: EconomyAi.BarracksWood);
        Tick(AiProfile.Sparta, w);
        Assert.Contains(w.Log, l => l.StartsWith("build(Barracks"));
        Assert.DoesNotContain(w.Log, l => l.StartsWith("build(Farm") || l.StartsWith("build(LumberCamp"));

        // steht sie (auch im Bau), plant Sparta wie alle anderen (die Baustelle nimmt drei Arbeiter, einer späht)
        var steht = Bauplatz(wood: 100, villagers: 8);
        steht.AddBuilding(BuildingType.Barracks, 20, 10, complete: false);
        Tick(AiProfile.Sparta, steht);
        Assert.Contains(steht.Log, l => l.StartsWith("build(Farm"));
    }

    /// <summary>Zwanzig freie Dorfbewohner, je eine Quelle Nahrung (20, 20), Holz (22, 22), Gold (24, 24);
    /// kein Holz für Bauten.</summary>
    private static Dictionary<string, int> Verteilung(AiProfile p, Age age)
    {
        var w = new FakeWorld { Age = age, PopulationCount = 20, PopulationCapacity = 30,
                                Resources = new ResourceVector(0, 0, 0, 0) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        for (int i = 0; i < 20; i++) w.AddVillager(12 + i % 5, 16 + i / 5);
        w.AddSource(Resource.Food, 20, 20);
        w.AddSource(Resource.Wood, 22, 22);
        w.AddSource(Resource.Gold, 24, 24);
        Tick(p, w);
        return new Dictionary<string, int>
        {
            ["Nahrung"] = w.Log.Count(l => l.StartsWith("gather(") && l.EndsWith(",20,20)")),
            ["Holz"] = w.Log.Count(l => l.StartsWith("gather(") && l.EndsWith(",22,22)")),
            ["Gold"] = w.Log.Count(l => l.StartsWith("gather(") && l.EndsWith(",24,24)")),
        };
    }

    [Fact]
    public void In_der_Dunklen_Zeit_sammelt_Sparta_mehr_Nahrung_und_kaum_Gold()
    {
        var sparta = Verteilung(AiProfile.Sparta, Age.Dark);
        var normal = Verteilung(AiProfile.Standard, Age.Dark);
        Assert.True(sparta["Nahrung"] > normal["Nahrung"], $"Sparta {sparta["Nahrung"]}, Normal {normal["Nahrung"]}");
        Assert.True(sparta["Gold"] < normal["Gold"], $"Sparta {sparta["Gold"]}, Normal {normal["Gold"]}");
        Assert.True(sparta["Gold"] <= 1, $"Gold {sparta["Gold"]}");

        // ab der Feudalzeit wie alle (Bogenschützen brauchen Gold)
        Assert.Equal(Verteilung(AiProfile.Standard, Age.Feudal), Verteilung(AiProfile.Sparta, Age.Feudal));
    }
}
