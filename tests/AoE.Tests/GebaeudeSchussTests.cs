using AoE.Core.Combat;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>F1 — Wachturm, Stadtzentrum und Burg schießen: eigene Werte,
/// Schuss-Regel, Schadensformel und der Einfluss von Befiederten Pfeilen.</summary>
public class GebaeudeSchussTests
{
    static readonly Position Ort = new(5, 5);

    [Fact]
    public void Stadtzentrum_wehrt_sich_jetzt_selbst()
    {
        var tc = BuildingEntity.CreateTownCenter(0, Ort);
        Assert.Equal(5, tc.Stats.BaseAttack);
        Assert.Equal(6, tc.Stats.Range);
    }

    [Theory]
    [InlineData(BuildingType.Tower)]
    [InlineData(BuildingType.TownCenter)]
    [InlineData(BuildingType.Castle)]
    public void Schiessende_Gebaeude(BuildingType typ)
        => Assert.True(BuildingCombat.Shoots(BuildingEntity.Create(typ, 0, Ort)));

    [Theory]
    [InlineData(BuildingType.House)]
    [InlineData(BuildingType.Mill)]
    [InlineData(BuildingType.Barracks)]
    [InlineData(BuildingType.Market)]
    [InlineData(BuildingType.University)]
    [InlineData(BuildingType.Wonder)]
    [InlineData(BuildingType.PalisadeWall)]
    [InlineData(BuildingType.StoneWall)]
    public void Der_Rest_schiesst_nicht(BuildingType typ)
        => Assert.False(BuildingCombat.Shoots(BuildingEntity.Create(typ, 0, Ort)));

    [Fact]
    public void Gebaeudepfeil_zaehlt_Fernkampf_gegen_Fernkampf_Ruestung()
    {
        var turm = BuildingEntity.Create(BuildingType.Tower, 0, Ort);       // Angriff 5
        var miliz = new Militia(1, Ort);                                    // Rüstung 0
        Assert.Equal(5, BuildingCombat.DamageToUnit(turm, miliz));

        // Ein Bogenschützepanzer (Skirmisher hat Fernkampf-Rüstung) schluckt einen Teil.
        var schutzen = new Skirmisher(1, Ort);
        int ruestung = schutzen.Stats.GetTotalArmor(UnitClass.Infantry, isRanged: true);
        Assert.Equal(Math.Max(1, 5 - ruestung), BuildingCombat.DamageToUnit(turm, schutzen));
    }

    [Fact]
    public void Nie_unter_einen_Schaden()
    {
        var burg = BuildingEntity.Create(BuildingType.Castle, 0, Ort);      // 11 Angriff
        var gepanzert = new Knight(1, Ort);
        Assert.True(BuildingCombat.DamageToUnit(burg, gepanzert) >= 1);
    }

    [Fact]
    public void Befiederte_Pfeile_heben_die_schiessenden_Gebaeude()
    {
        var turm = BuildingEntity.Create(BuildingType.Tower, 0, Ort);
        Assert.True(TechEffects.Apply(Tech.Fletching, turm));
        Assert.Equal(6, turm.Stats.BaseAttack);
        Assert.Equal(9, turm.Stats.Range);
    }

    [Fact]
    public void Befiederte_Pfeile_wirken_nicht_auf_haeuser_und_mauern()
    {
        var haus = BuildingEntity.CreateHouse(0, Ort);
        Assert.False(TechEffects.Apply(Tech.Fletching, haus));
        Assert.Equal(0, haus.Stats.BaseAttack);
        Assert.Equal(0, haus.Stats.Range);
    }

    [Fact]
    public void Befiederte_Pfeile_lassen_die_Bogenschuetzen_unveraendert()
    {
        var bogen = new Archer(0, Ort);
        (int angriff, int reichweite) = (bogen.Stats.BaseAttack, bogen.Stats.Range);
        Assert.True(TechEffects.Apply(Tech.Fletching, bogen));
        Assert.Equal((angriff + 1, reichweite + 1), (bogen.Stats.BaseAttack, bogen.Stats.Range));
    }

    [Fact]
    public void Mauerwerk_und_Pfeile_kombinieren_sich_ueber_Alle_Techs()
    {
        var techs = new TechProgress();
        techs.Begin(Tech.Fletching); techs.Complete(Tech.Fletching);
        techs.Begin(Tech.Masonry);   techs.Complete(Tech.Masonry);
        var turm = BuildingEntity.Create(BuildingType.Tower, 0, Ort);
        Assert.Equal(2, TechEffects.ApplyAll(techs, turm));
        Assert.Equal(6, turm.Stats.BaseAttack);                    // Fletching +1
        Assert.Equal(9, turm.Stats.Range);                         // Fletching +1
        Assert.Equal(2, turm.Stats.BaseArmor);                     // Maurerkunst +1
        Assert.Equal(1020 + 102, turm.Stats.HitPoints);            // Maurerkunst +10 %
    }

    [Fact]
    public void Ein_Pfeiltakt_ist_zwei_Sekunden()
        => Assert.Equal(2f, BuildingCombat.BOMBARD_SECONDS);
}
