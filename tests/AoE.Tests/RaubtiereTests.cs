using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AoE.Core.Entities;
using Microsoft.Xna.Framework;
using Xunit;
using Player = AgeOfEvolutions.Core.Data.Player;
using UnitType = AgeOfEvolutions.Core.Data.UnitType;

namespace AoE.Tests;

/// <summary>
/// Raubtiere (RT). Der Nutzer (2026-10-23): „Bären und Wölfe in hoher qualität. Kommen
/// Dorfbewohner oder Soldaten den Tieren zu nahe werden sie von ihnen angegriffen. Wie
/// Wildschweine (nur seltener) laufen sie herum aber von Gebäuden halten sie Abstand (mindestens
/// 4-5 Kacheln)" und „Sie verfolgen aber dann keine Menschen wenn man davon läuft nur wenn man zu
/// nahe zu Ihnen hinläuft". Was der Spielbildschirm daraus macht (Gegenwehr, Jagd per Klick),
/// prüft tools/spielablauf in der Gruppe raubtiere.
/// </summary>
public class RaubtiereTests
{
    private const int TS = 32;

    /// <summary>Leere Wiese; wer will, lässt die Raubtiere nicht wandern.</summary>
    private static TileMap Leer(bool wandern = true)
    {
        var s = new MapSettings();
        if (!wandern)
        {
            s.BearWanderSecondsMin = s.BearWanderSecondsMax = 10000f;
            s.WolfWanderSecondsMin = s.WolfWanderSecondsMax = 10000f;
        }
        return TileMap.CreateBlank(30, 30, TS, s);
    }

    private static Vector2 Mitte(int x, int y) => new(x * TS + TS / 2, y * TS + TS / 2);

    private static Unit Einheit(UnitType typ, Vector2 lage, int besitzer = 0)
        => new(typ, besitzer) { Position = lage };

    private static List<PredatorStrike> Laufe(TileMap karte, List<Unit> einheiten, float sekunden, float dt = 1f / 60f)
    {
        var alle = new List<PredatorStrike>();
        for (float t = 0; t < sekunden; t += dt)
            alle.AddRange(karte.UpdatePredators(dt, einheiten));
        return alle;
    }

    private static (int X, int Y) Lage(TileMap karte, WildAnimal tier) => karte.LocatePredator(tier).Value;

    // --- Aufstellen -----------------------------------------------------------

    [Fact]
    public void RaubtierStehtLebendOhneNahrungAufSeinerKachel()
    {
        var karte = Leer();
        Assert.True(karte.PlacePredator(FoodSource.Bear, 5, 5));
        Assert.True(karte.PlacePredator(FoodSource.Wolf, 9, 5));
        var baer = karte.GetTile(5, 5);
        Assert.Equal(FoodSource.Bear, baer.Food);
        Assert.Null(baer.ResourceType);
        Assert.Equal(0, baer.ResourceAmount);
        Assert.NotNull(baer.Animal);
        Assert.Equal(karte.Settings.BearHealth, baer.Animal.Health);
        Assert.Equal(karte.Settings.WolfHealth, karte.GetTile(9, 5).Animal.Health);
        Assert.Equal(1, karte.BearCount);
        Assert.Equal(1, karte.WolfCount);
        Assert.True(karte.IsLivePredator(5, 5));
        Assert.False(karte.IsLivePredator(6, 5));
        Assert.True(FoodSource.Bear.IsPredator() && FoodSource.Wolf.IsPredator());
        Assert.False(FoodSource.Boar.IsPredator());
    }

    [Fact]
    public void RaubtierNurAufFreieWiese()
    {
        var karte = Leer();
        karte.GetTile(3, 3).Type = TileType.Forest;
        karte.AddBuilding(10, 10, "Haus", 0, 2);
        karte.GetTile(6, 6).Food = FoodSource.Sheep;
        karte.GetTile(6, 6).ResourceType = AoE.Core.Entities.Resource.Food;
        Assert.False(karte.PlacePredator(FoodSource.Bear, 3, 3));
        Assert.False(karte.PlacePredator(FoodSource.Bear, 10, 10));
        Assert.False(karte.PlacePredator(FoodSource.Wolf, 6, 6));
        Assert.False(karte.PlacePredator(FoodSource.Boar, 7, 7));
        Assert.False(karte.PlacePredator(FoodSource.Bear, 30, 7));
        Assert.True(karte.PlacePredator(FoodSource.Wolf, 7, 7));
        Assert.False(karte.PlacePredator(FoodSource.Bear, 7, 7));
        Assert.Equal(0, karte.BearCount);
        Assert.Equal(1, karte.WolfCount);
    }

    [Fact]
    public void GebaeudeabstandIstChebyshevZurNaechstenGebaeudekachel()
    {
        var karte = Leer();
        Assert.Equal(int.MaxValue, karte.BuildingDistance(4, 4));
        karte.AddBuilding(5, 5, "Haus", 0, 2);            // Kacheln 5..6 x 5..6
        Assert.Equal(0, karte.BuildingDistance(5, 5));
        Assert.Equal(0, karte.BuildingDistance(6, 6));
        Assert.Equal(2, karte.BuildingDistance(8, 5));
        Assert.Equal(3, karte.BuildingDistance(3, 9));
        Assert.Equal(1, karte.BuildingDistance(4, 4));
        karte.AddBuilding(20, 20, "Haus", 1, 2);          // auch Gebäude des Gegners zählen
        Assert.Equal(2, karte.BuildingDistance(22, 18));
    }

    [Fact]
    public void ZufallskartenTragenBaerenUndWolfsrudelFernVonDenStartplaetzen()
    {
        for (int i = 0; i < 8; i++)
        {
            var karte = new TileMap(64, 64, TS, MapSettings.ForSize(MapSize.Standard));
            Assert.InRange(karte.BearCount, 1, karte.Settings.BearGroupsMax * karte.Settings.BearsPerGroupMax);
            Assert.InRange(karte.WolfCount, 2, karte.Settings.WolfPacksMax * karte.Settings.WolvesPerPackMax);
            int gefunden = 0;
            for (int x = 0; x < 64; x++)
                for (int y = 0; y < 64; y++)
                {
                    var t = karte.GetTile(x, y);
                    if (!t.Food.IsPredator()) continue;
                    gefunden++;
                    Assert.Equal(TileType.Grassland, t.Type);
                    Assert.Null(t.ResourceType);
                    Assert.True(karte.IsLivePredator(x, y));
                    Assert.Equal(karte.PredatorMaxHealth(t.Food), t.Animal.Health);
                    Assert.True(karte.BuildingDistance(x, y) >= karte.Settings.PredatorBuildingDistance,
                                $"Raubtier ({x}, {y}) nur {karte.BuildingDistance(x, y)} Kacheln von einem Gebäude");
                    foreach (var owner in new[] { 0, 1 })
                    {
                        var (sx, sy) = karte.StartPosition(owner);
                        Assert.True(System.Math.Abs(x - sx) + System.Math.Abs(y - sy) >= 2 * karte.Settings.PredatorStartDistance - 4,
                                    $"Raubtier ({x}, {y}) zu nah am Startplatz ({sx}, {sy})");
                    }
                }
            Assert.Equal(karte.BearCount + karte.WolfCount, gefunden);
        }
    }

    [Fact]
    public void RaubtiereWandernSeltenerAlsWildschweine()
    {
        var s = new MapSettings();
        Assert.True(s.BearWanderSecondsMin > s.BoarWanderSecondsMin && s.BearWanderSecondsMax > s.BoarWanderSecondsMax);
        Assert.True(s.WolfWanderSecondsMin > s.BoarWanderSecondsMin && s.WolfWanderSecondsMax > s.BoarWanderSecondsMax);
        Assert.InRange(s.PredatorBuildingDistance, 4, 5);
    }

    // --- Zuschlagen --------------------------------------------------------------

    [Fact]
    public void WerZuNaheKommtWirdSofortGetroffen()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        var dorfi = Einheit(UnitType.Villager, Mitte(11, 10));
        var schlaege = karte.UpdatePredators(1f / 60f, new List<Unit> { dorfi });
        var s = Assert.Single(schlaege);
        Assert.Equal(FoodSource.Bear, s.Kind);
        Assert.Equal((10, 10), (s.X, s.Y));
        Assert.Same(dorfi, s.Victim);
        int schaden = System.Math.Max(1, karte.Settings.BearDamage - dorfi.Armor);
        Assert.Equal(schaden, s.Damage);
        Assert.False(s.Killed);
        Assert.Equal(dorfi.MaxHealth - schaden, dorfi.Health);
        Assert.Equal(TileMap.PREDATOR_STRIKE_SECONDS, tier.Strike, 3);
        Assert.Equal(karte.Settings.BearReloadSeconds, tier.AttackCooldown, 3);
        Assert.False(tier.FacingLeft);                     // das Opfer steht rechts
    }

    [Fact]
    public void ZuschlagenImTaktDerArt()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        var soldat = Einheit(UnitType.Militia, Mitte(9, 10));
        var einheiten = new List<Unit> { soldat };
        // In 4 Sekunden: sofort, nach 1,5 und nach 3 Sekunden - dreimal
        var schlaege = Laufe(karte, einheiten, 4f);
        Assert.Equal(3, schlaege.Count);
        int schaden = System.Math.Max(1, karte.Settings.WolfDamage - soldat.Armor);
        Assert.All(schlaege, s => Assert.Equal(schaden, s.Damage));
        Assert.Equal(soldat.MaxHealth - 3 * schaden, soldat.Health);
        Assert.True(karte.GetTile(10, 10).Animal.FacingLeft);   // das Opfer steht links
    }

    [Fact]
    public void MitOpferInReichweiteHoltEsAus()
    {
        // Engaged steuert das Ausholen vor dem nächsten Schlag (Angriffsphasen 0 bis 2)
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        var dorfi = Einheit(UnitType.Villager, Mitte(11, 10));
        var einheiten = new List<Unit> { dorfi };
        Assert.False(tier.Engaged);
        karte.UpdatePredators(1f / 60f, einheiten);
        Assert.True(tier.Engaged);
        karte.UpdatePredators(1f / 60f, einheiten);
        Assert.True(tier.Engaged);                         // auch zwischen den Schlägen
        dorfi.Position = Mitte(14, 10);
        karte.UpdatePredators(1f / 60f, einheiten);
        Assert.False(tier.Engaged);
    }

    [Fact]
    public void WerAbstandHaeltBleibtUnbehelligt()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        // 1,5 Kacheln vom Bären - knapp außer Reichweite (PredatorReachTiles 1,25)
        var dorfi = Einheit(UnitType.Villager, Mitte(10, 10) + new Vector2(1.5f * TS, 0));
        Assert.Empty(Laufe(karte, new List<Unit> { dorfi }, 20f));
        Assert.Equal(dorfi.MaxHealth, dorfi.Health);
        Assert.True(karte.IsLivePredator(10, 10));
    }

    [Fact]
    public void RaubtierVerfolgtNiemandenDerWeglaeuft()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        tier.WanderTimer = 10000f;                        // dieser Bär wandert nicht von selbst
        var dorfi = Einheit(UnitType.Villager, Mitte(11, 10));
        var einheiten = new List<Unit> { dorfi };
        Assert.Single(Laufe(karte, einheiten, 0.5f));
        // Weglaufen: drei Kacheln weiter - der Bär bleibt, wo er ist, und schlägt nicht mehr zu
        dorfi.Position = Mitte(14, 10);
        Assert.Empty(Laufe(karte, einheiten, 20f));
        Assert.Equal((10, 10), Lage(karte, tier));
        Assert.Equal(0f, tier.Glide);
    }

    [Fact]
    public void DerNaechsteImUmkreisWirdGetroffen_ToteNicht()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var fern = Einheit(UnitType.Villager, Mitte(10, 10) + new Vector2(0, 1.1f * TS));
        var tot = Einheit(UnitType.Villager, Mitte(10, 10) + new Vector2(0.2f * TS, 0));
        tot.Health = 0;
        tot.State = UnitState.Dead;
        var nah = Einheit(UnitType.Militia, Mitte(10, 10) + new Vector2(-0.6f * TS, 0));
        var s = Assert.Single(karte.UpdatePredators(1f / 60f, new List<Unit> { fern, tot, nah }));
        Assert.Same(nah, s.Victim);
        Assert.Equal(fern.MaxHealth, fern.Health);
    }

    [Fact]
    public void DerLetzteSchlagToetet()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var dorfi = Einheit(UnitType.Villager, Mitte(10, 11));
        dorfi.Health = 2;
        var s = Assert.Single(karte.UpdatePredators(1f / 60f, new List<Unit> { dorfi }));
        Assert.True(s.Killed);
        Assert.Equal(0, dorfi.Health);
        Assert.Equal(UnitState.Dead, dorfi.State);
    }

    [Fact]
    public void MittenImSchrittSchlaegtEsNicht()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        tier.Glide = 0.5f;
        var dorfi = Einheit(UnitType.Villager, Mitte(11, 10));
        Assert.Empty(karte.UpdatePredators(0.1f, new List<Unit> { dorfi }));
        Assert.Equal(0.4f, tier.Glide, 3);
        // Nach dem Schritt sofort
        Assert.Single(Laufe(karte, new List<Unit> { dorfi }, 0.5f));
    }

    // --- Abstand zu Gebäuden --------------------------------------------------------

    [Fact]
    public void ZuNahAnEinemGebaeudeZiehtEsSichZurueck()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        tier.WanderTimer = 10000f;                        // es zieht nur, weil das Haus zu nah ist
        karte.AddBuilding(11, 12, "Haus", 0, 2);          // Abstand 2
        Assert.Equal(2, karte.BuildingDistance(10, 10));
        Laufe(karte, new List<Unit>(), 15f);
        var (x, y) = Lage(karte, tier);
        Assert.True(karte.BuildingDistance(x, y) >= karte.Settings.PredatorBuildingDistance,
                    $"der Bär steht nach 15 s auf ({x}, {y}), {karte.BuildingDistance(x, y)} Kacheln vom Haus");
    }

    [Fact]
    public void DerRueckzugFuehrtUmHindernisseHerum()
    {
        var karte = Leer();
        // Wald als Winkel: links (x = 9) und oben (y = 9) ist der direkte Weg vom Haus weg versperrt
        for (int y = 6; y <= 11; y++)
            karte.GetTile(9, y).Type = TileType.Forest;
        for (int x = 9; x <= 14; x++)
            karte.GetTile(x, 9).Type = TileType.Forest;
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        tier.WanderTimer = 10000f;
        karte.AddBuilding(11, 12, "Haus", 0, 2);          // Abstand 2
        Laufe(karte, new List<Unit>(), 20f);
        var (x2, y2) = Lage(karte, tier);
        Assert.True(karte.BuildingDistance(x2, y2) >= karte.Settings.PredatorBuildingDistance,
                    $"der Bär steckt nach 20 s auf ({x2}, {y2}) fest, {karte.BuildingDistance(x2, y2)} Kacheln vom Haus");
    }

    [Fact]
    public void BeimWandernKommtEsKeinemGebaeudeZuNahe()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Wolf, 12, 12);
        karte.PlacePredator(FoodSource.Bear, 5, 6);
        karte.AddBuilding(12, 18, "Haus", 0, 2);
        karte.AddBuilding(20, 10, "Haus", 1, 2);
        var tiere = new[] { karte.GetTile(12, 12).Animal, karte.GetTile(5, 6).Animal };
        var start = tiere.Select(t => Lage(karte, t)).ToArray();
        int grenze = karte.Settings.PredatorBuildingDistance;
        bool gewandert = false;
        for (int i = 0; i < 4000; i++)                    // 200 Sekunden
        {
            karte.UpdatePredators(0.05f, new List<Unit>());
            for (int k = 0; k < tiere.Length; k++)
            {
                var (x, y) = Lage(karte, tiere[k]);
                Assert.True(karte.BuildingDistance(x, y) >= grenze, $"Raubtier auf ({x}, {y}) nach {i * 0.05f:0.0} s");
                gewandert |= (x, y) != start[k];
            }
        }
        Assert.True(gewandert, "in 200 Sekunden ist kein Raubtier gewandert");
    }

    // --- Erlegen --------------------------------------------------------------------

    [Fact]
    public void ErlegterBaerLiegtAlsFleischDa()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var t = karte.GetTile(10, 10);
        var tier = t.Animal;
        Assert.False(karte.DamagePredator(10, 10, 30));
        Assert.Equal(karte.Settings.BearHealth - 30, tier.Health);
        Assert.False(karte.DamagePredator(10, 11, 999));   // dort steht keins
        Assert.True(karte.DamagePredator(10, 10, 999));
        Assert.Equal(0, tier.Health);
        Assert.True(tier.Slaughtered);
        Assert.Equal(FoodSource.Bear, t.Food);
        Assert.Equal(AoE.Core.Entities.Resource.Food, t.ResourceType);
        Assert.Equal(karte.Settings.BearFood, t.ResourceAmount);
        Assert.False(karte.IsLivePredator(10, 10));
        Assert.False(karte.DamagePredator(10, 10, 5));      // schon erlegt
        Assert.Equal(1, karte.BearCount);
    }

    [Fact]
    public void FleischSchlaegtNichtZuUndWandertNicht()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Bear, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        Assert.True(karte.DamagePredator(10, 10, 999));
        var dorfi = Einheit(UnitType.Villager, Mitte(11, 10));
        Assert.Empty(Laufe(karte, new List<Unit> { dorfi }, 30f));
        Assert.Same(tier, karte.GetTile(10, 10).Animal);
    }

    [Fact]
    public void ErlegterWolfVerschwindet()
    {
        var karte = Leer(wandern: false);
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        Assert.False(karte.DamagePredator(10, 10, 0));    // auch 0 Schaden zählt als 1
        Assert.Equal(karte.Settings.WolfHealth - 1, karte.GetTile(10, 10).Animal.Health);
        Assert.True(karte.DamagePredator(10, 10, karte.Settings.WolfHealth));
        var t = karte.GetTile(10, 10);
        Assert.Equal(FoodSource.None, t.Food);
        Assert.Null(t.Animal);
        Assert.Null(t.ResourceType);
        Assert.Equal(0, karte.WolfCount);
    }

    [Fact]
    public void LocatePredatorFindetDasTier()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        karte.PlacePredator(FoodSource.Bear, 4, 4);
        var tier = karte.GetTile(10, 10).Animal;
        Assert.Equal((10, 10), karte.LocatePredator(tier));
        Assert.Equal((4, 4), karte.LocatePredator(karte.GetTile(4, 4).Animal));
        Assert.Null(karte.LocatePredator(null));
        Assert.Null(karte.LocatePredator(new WildAnimal()));
        // der erlegte Bär liegt als Fleisch da und ist noch zu finden, der erlegte Wolf nicht mehr
        var baer = karte.GetTile(4, 4).Animal;
        karte.DamagePredator(4, 4, 999);
        karte.DamagePredator(10, 10, 999);
        Assert.Equal((4, 4), karte.LocatePredator(baer));
        Assert.Null(karte.LocatePredator(tier));
    }

    [Fact]
    public void LocatePredatorFolgtDemTier()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Wolf, 10, 10);
        var tier = karte.GetTile(10, 10).Animal;
        karte.AddBuilding(10, 12, "Haus", 0, 2);          // schiebt den Wolf weg
        Laufe(karte, new List<Unit>(), 3f);
        var lage = karte.LocatePredator(tier);
        Assert.NotNull(lage);
        Assert.NotEqual((10, 10), lage.Value);
        Assert.Same(tier, karte.GetTile(lage.Value.X, lage.Value.Y).Animal);
    }

    // --- Spielstand -------------------------------------------------------------------

    [Fact]
    public void SpielstandBehaeltLebenspunkteUndTakt()
    {
        var karte = new TileMap(40, 30, TS, MapSettings.ForSize(MapSize.Test));
        var p1 = new Player(0, "Spieler", "Briten");
        var p2 = new Player(1, "Gegner", "Azteken");
        // ein frischer Bär und ein angeschlagener Wolf auf freier Wiese fern der Gebäude
        var frei = new List<(int X, int Y)>();
        for (int x = 0; x < 40 && frei.Count < 2; x++)
            for (int y = 0; y < 30 && frei.Count < 2; y++)
                if (karte.BuildingDistance(x, y) >= 6 && karte.GetTile(x, y) is { Type: TileType.Grassland, Food: FoodSource.None, ResourceType: null, Farm: false }
                    && (frei.Count == 0 || System.Math.Abs(frei[0].X - x) > 2))
                    frei.Add((x, y));
        Assert.True(karte.PlacePredator(FoodSource.Bear, frei[0].X, frei[0].Y));
        Assert.True(karte.PlacePredator(FoodSource.Wolf, frei[1].X, frei[1].Y));
        var wolf = karte.GetTile(frei[1].X, frei[1].Y).Animal;
        wolf.Health = 7;
        wolf.AttackCooldown = 0.75f;

        var (geladen, _, _) = SaveGameMapper.Restore(SaveGameStore.Deserialize(SaveGameStore.Serialize(
            SaveGameMapper.Capture(karte, p1, p2, MapSize.Test))));
        // (dass die geladenen Raubtiere wieder in ihren Listen stehen, prüft RebuildWildAnimalsZaehltDieRaubtiere)
        var baer2 = geladen.GetTile(frei[0].X, frei[0].Y);
        Assert.Equal(FoodSource.Bear, baer2.Food);
        Assert.Equal(karte.Settings.BearHealth, baer2.Animal.Health);
        var wolf2 = geladen.GetTile(frei[1].X, frei[1].Y).Animal;
        Assert.Equal(7, wolf2.Health);
        Assert.Equal(0.75f, wolf2.AttackCooldown, 3);
        Assert.True(geladen.IsLivePredator(frei[1].X, frei[1].Y));
    }

    [Fact]
    public void RebuildWildAnimalsZaehltDieRaubtiere()
    {
        var karte = Leer();
        karte.PlacePredator(FoodSource.Bear, 4, 4);
        karte.PlacePredator(FoodSource.Wolf, 8, 8);
        karte.PlacePredator(FoodSource.Wolf, 9, 8);
        karte.RebuildWildAnimals();
        Assert.Equal(1, karte.BearCount);
        Assert.Equal(2, karte.WolfCount);
    }
}
