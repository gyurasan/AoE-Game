using AoE.Core.Economy;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Settings;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Die zehn neuen Menü-Einstellungen (Hauptmenü → Einstellungen) und ihre
/// Auswirkung: Startkapital, Bevölkerungsgrenze, Wetschritte, Sound-Master.
/// Die restlichen (KI-Profil, Tempos, Abstände, Kamera) leben im
/// Spielbildschirm — defaults dort sind die bisherigen Hartwerte, und
/// tools/spielablauf prüft die betroffenen Abläufe mit ihnen.
/// </summary>
public class GameSettingsTests
{
    [Fact]
    public void StartingCapital_Felder_Sind_Steigend()
    {
        var (fArm, wArm, gArm, sArm) = StartingCapitalRules.Values(StartingCapital.Arm);
        var (fNorm, wNorm, gNorm, sNorm) = StartingCapitalRules.Values(StartingCapital.Normal);
        var (fReich, wReich, gReich, sReich) = StartingCapitalRules.Values(StartingCapital.Reich);

        Assert.True(fArm < fNorm && fNorm < fReich, $"Nahrung {fArm} < {fNorm} < {fReich}");
        Assert.True(wArm < wNorm && wNorm < wReich, $"Holz {wArm} < {wNorm} < {wReich}");
        Assert.True(gArm < gNorm && gNorm < gReich, $"Gold {gArm} < {gNorm} < {gReich}");
        Assert.True(sArm < sNorm && sNorm < sReich, $"Stein {sArm} < {sNorm} < {sReich}");
    }

    [Fact]
    public void StartingCapital_Normal_Ist_Die_Bisherige_Standardausstattung()
    {
        var (f, w, g, s) = StartingCapitalRules.Values(StartingCapital.Normal);
        Assert.Equal(ResourcePool.START_FOOD, f);
        Assert.Equal(ResourcePool.START_WOOD, w);
        Assert.Equal(ResourcePool.START_GOLD, g);
        Assert.Equal(ResourcePool.START_STONE, s);
    }

    [Fact]
    public void ResourcePool_Eigenes_Kapital_Haelt_Mengen()
    {
        var pool = new ResourcePool(111, 222, 333, 444);
        Assert.Equal(111, pool[Resource.Food]);
        Assert.Equal(222, pool[Resource.Wood]);
        Assert.Equal(333, pool[Resource.Gold]);
        Assert.Equal(444, pool[Resource.Stone]);
    }

    [Fact]
    public void Player_Startkapital_Nach_Stufe()
    {
        // Azteken: kein Ressourcen-Bonus, also exakt die Stufenwerte
        var arm = new Player(0, "P", "Azteken", StartingCapital.Arm);
        var normal = new Player(1, "P", "Azteken", StartingCapital.Normal);
        var reich = new Player(2, "P", "Azteken", StartingCapital.Reich);

        var (fa, wa, ga, sa) = StartingCapitalRules.Values(StartingCapital.Arm);
        var (fn, wn, gn, sn) = StartingCapitalRules.Values(StartingCapital.Normal);
        var (fr, wr, gr, sr) = StartingCapitalRules.Values(StartingCapital.Reich);

        Assert.Equal((fa, wa, ga, sa), (arm.Resources[Resource.Food], arm.Resources[Resource.Wood],
                                       arm.Resources[Resource.Gold], arm.Resources[Resource.Stone]));
        Assert.Equal((fn, wn, gn, sn), (normal.Resources[Resource.Food], normal.Resources[Resource.Wood],
                                       normal.Resources[Resource.Gold], normal.Resources[Resource.Stone]));
        Assert.Equal((fr, wr, gr, sr), (reich.Resources[Resource.Food], reich.Resources[Resource.Wood],
                                       reich.Resources[Resource.Gold], reich.Resources[Resource.Stone]));

        // Briten bekommen +50 Holz dazu (Zivilisations-Bonus) — auf jeder Stufe
        var briten = new Player(3, "P", "Briten", StartingCapital.Normal);
        Assert.Equal(wn + 50, briten.Resources[Resource.Wood]);
    }

    [Fact]
    public void Population_Limit_Begrenzt_Capacity()
    {
        // 1 TC + 10 Häuser = 11 × 5 = 55 Plätze; mit Limit 30 nur 30
        var buildings = new[] { BuildingType.TownCenter }.Concat(
            Enumerable.Repeat(BuildingType.House, 10));

        Assert.Equal(55, Population.Capacity(buildings, 1000));
        Assert.Equal(30, Population.Capacity(buildings, 30));
        Assert.Equal(55, Population.Capacity(buildings, 100));
    }

    [Fact]
    public void GameplayOptions_Next_Blaettet_Durch_Die_Stufen()
    {
        int[] steps = { 50, 100, 150, 200 };
        Assert.Equal(100, GameplayOptions.Next(50, steps));
        Assert.Equal(150, GameplayOptions.Next(100, steps));
        Assert.Equal(200, GameplayOptions.Next(150, steps));
        // Über der höchsten Stufe zurück zur ersten
        Assert.Equal(50, GameplayOptions.Next(200, steps));
        // Werte, die nicht in der Liste stehen (z. B. 60 % Lautstärke),
        // springen zum ersten höheren Wert
        int[] volume = { 0, 25, 50, 75, 100 };
        Assert.Equal(75, GameplayOptions.Next(60, volume));
    }

    [Fact]
    public void GameplayOptions_Defaults_In_Settings_Liegen_In_Den_Stufen()
    {
        // Default muss nicht SELBST eine Stufe sein (60 % Lautstärke ist die
        // bisherige, aber nicht in der Stufenliste) — nur im Bereich der
        // Stufen, und Next() muss vom Default aus weiterzählen können.
        (int value, int[] steps)[] pairs =
        {
            (SettingsDefault().SoundVolume, GameplayOptions.SoundVolume),
            (SettingsDefault().AiDifficulty, GameplayOptions.AiDifficulty),
            (SettingsDefault().StartingCapital, GameplayOptions.StartingCapital),
            (SettingsDefault().PopulationCap, GameplayOptions.PopulationCap),
            (SettingsDefault().UnitSpeed, GameplayOptions.UnitSpeed),
            (SettingsDefault().GatherSpeed, GameplayOptions.GatherSpeed),
            (SettingsDefault().ProductionSpeed, GameplayOptions.ProductionSpeed),
            (SettingsDefault().MeleeStopTiles, GameplayOptions.MeleeStopTiles),
            (SettingsDefault().FormationSpacingTiles, GameplayOptions.FormationSpacingTiles),
            (SettingsDefault().CameraPanSpeed, GameplayOptions.CameraPanSpeed),
        };
        foreach (var (value, steps) in pairs)
        {
            Assert.True(value >= steps.Min() && value <= steps.Max(),
                $"{value} außerhalb von {string.Join(",", steps)}");
            int next = GameplayOptions.Next(value, steps);
            Assert.Contains(next, steps);
        }
    }

    private static AgeOfEvolutionsSettings SettingsDefault() => new();

    [Fact]
    public void SoundBoard_MasterVolume_Default_Ist_Bisheriges_Volumen()
    {
        var board = new AgeOfEvolutions.Core.Audio.SoundBoard();
        Assert.Equal(AgeOfEvolutions.Core.Audio.SoundBoard.VOLUME, board.MasterVolume);
    }
}
