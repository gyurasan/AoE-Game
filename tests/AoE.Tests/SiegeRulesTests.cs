using System;
using AoE.Core.Combat;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>Belagerungsregeln (F15): Plätze, Mindestreichweite, Fläche, Zielwahl,
/// Rammbock-Deckel gegen Einheiten und die hohe Fernkampf-Rüstung des Rammbocks.</summary>
public class SiegeRulesTests
{
    static readonly Position Ort = new(5, 5);

    // --- Bevölkerungsplätze (F15) ---

    [Theory]
    [InlineData("Ram", 2)]
    [InlineData("Mangonel", 2)]
    [InlineData("Trebuchet", 3)]
    [InlineData("Villager", 1)]
    [InlineData("Archer", 1)]
    public void Bevoelkerungsplaetze_je_Geraet(string typ, int erwartet)
        => Assert.Equal(erwartet, SiegeRules.Slots(typ));

    // --- Mindestreichweite (F15) ---

    [Theory]
    [InlineData("Mangonel", 3f)]
    [InlineData("Ram", 0f)]
    [InlineData("Trebuchet", 0f)]
    [InlineData("Archer", 0f)]
    public void Mindestreichweite_nur_beim_Mangonel(string typ, float erwartet)
        => Assert.Equal(erwartet, SiegeRules.MinRangeTiles(typ));

    // --- Flächenschaden (F15) ---

    [Theory]
    [InlineData("Mangonel", 1f)]
    [InlineData("Trebuchet", 1f)]
    [InlineData("Ram", 0f)]
    [InlineData("Villager", 0f)]
    public void Flaechenradius_beim_Wurfgeraet(string typ, float erwartet)
        => Assert.Equal(erwartet, SiegeRules.SplashRadiusTiles(typ));

    [Fact]
    public void Flaechenschaden_ist_halber_Hauptschlag_mindestens_1()
    {
        Assert.Equal(50, SiegeRules.SplashDamage(100));
        Assert.Equal(1, SiegeRules.SplashDamage(1));      // bleibt bei 1
        Assert.Equal(0, SiegeRules.SplashDamage(0));      // ohne Hauptschlag keine Fläche
    }

    // --- Zielwahl (F15) ---

    [Theory]
    [InlineData("Trebuchet", true)]
    [InlineData("Mangonel", false)]
    [InlineData("Ram", false)]
    [InlineData("Knight", false)]
    public void Tribok_greift_nur_Gebaeude_an(string typ, bool erwartet)
        => Assert.Equal(erwartet, SiegeRules.OnlyBuildings(typ));

    [Fact]
    public void Nur_der_Tribok_wird_aufgebaut()
    {
        Assert.True(SiegeRules.IsDeployable("Trebuchet"));
        Assert.False(SiegeRules.IsDeployable("Mangonel"));
        Assert.True(SiegeRules.SETUP_SECONDS > 0f);
        Assert.True(SiegeRules.PACK_SECONDS > 0f);
    }

    // --- Rammbock gegen Einheiten (F15) ---

    [Fact]
    public void Rammbock_schlaegt_gegen_Einheiten_hoechstens_1()
    {
        Assert.Equal(1, SiegeRules.DamageToUnits("Ram", 12));
        Assert.Equal(12, SiegeRules.DamageToUnits("Mangonel", 12));
        Assert.Equal(0, SiegeRules.DamageToUnits("Ram", 0));
    }

    [Fact]
    public void Bogenschuetze_kommt_am_Rammbock_fast_nicht_durch()
    {
        var ram = new Ram(0, Ort);
        // Die hohe Fernkampf-Rüstung: der Archer-Rüstungswert zählt nur bei Fernangriffen.
        Assert.Equal(15, ram.Stats.GetTotalArmor(UnitClass.Archer, true)
                          - ram.Stats.GetTotalArmor(UnitClass.Archer, false));
        // Und imkampf bleibt schwach: ein Bogenschütze wirft nur 1 Schaden auf die Bockleiter.
        Assert.Equal(1, DamageCalculator.CalculateDamage(new Archer(0, Ort), ram));
    }

    [Fact]
    public void Gebaeudeboni_der_Geraete_unveraendert()
    {
        Assert.Equal(125 + 2 - 0, new Ram(0, Ort).Stats.GetTotalAttack(UnitClass.Building));
        Assert.Equal(400 + 200, new Trebuchet(0, Ort).Stats.GetTotalAttack(UnitClass.Building));
    }
}
