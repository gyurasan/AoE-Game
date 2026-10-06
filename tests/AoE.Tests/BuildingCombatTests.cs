using AoE.Core.Combat;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>Schaden an Gebäuden (K1): Schlag um Schlag sinkt die Gebäudestärke.</summary>
public class BuildingCombatTests
{
    static readonly Position Ort = new(5, 5);

    [Fact]
    public void Dorfbewohner_gegen_Haus_Angriff_minus_Ruestung()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        Assert.Equal(3 - haus.Stats.BaseArmor, BuildingCombat.DamagePerHit(new Villager(0, Ort), haus));
    }

    [Fact]
    public void Dorfbewohner_gegen_Stadtzentrum()
    {
        var tc = BuildingEntity.CreateTownCenter(1, Ort);
        Assert.Equal(1, BuildingCombat.DamagePerHit(new Villager(0, Ort), tc));   // 3 Angriff, 2 Rüstung
    }

    [Fact]
    public void Pfeile_prallen_ab()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        Assert.Equal(1, BuildingCombat.DamagePerHit(new Archer(0, Ort), haus));
    }

    [Fact]
    public void Bonus_gegen_Gebaeude_zaehlt()
    {
        var angreifer = new Villager(0, Ort);
        angreifer.Stats.AddAttackBonus(UnitClass.Building, 4);
        var haus = BuildingEntity.CreateHouse(1, Ort);
        Assert.Equal(3 + 4 - haus.Stats.BaseArmor, BuildingCombat.DamagePerHit(angreifer, haus));
    }

    [Fact]
    public void Mindestens_ein_Schaden()
    {
        var turm = BuildingEntity.Create(BuildingType.Castle, 1, Ort);   // Rüstung 8
        Assert.Equal(1, BuildingCombat.DamagePerHit(new Villager(0, Ort), turm));
    }

    [Fact]
    public void Ohne_Angriff_kein_Schaden()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        Assert.Equal(0, BuildingCombat.DamagePerHit(BuildingEntity.CreateHouse(0, Ort), haus));
    }

    [Fact]
    public void Schlag_senkt_die_Staerke()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        int vorher = haus.CurrentHp;
        bool zerstoert = BuildingCombat.Hit(new Villager(0, Ort), haus);
        Assert.False(zerstoert);
        Assert.Equal(vorher - (3 - haus.Stats.BaseArmor), haus.CurrentHp);
    }

    [Fact]
    public void Letzter_Schlag_zerstoert_und_nie_unter_null()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        haus.CurrentHp = 1;
        Assert.True(BuildingCombat.Hit(new Villager(0, Ort), haus));
        Assert.Equal(0, haus.CurrentHp);
        Assert.Equal(0, BuildingCombat.DamagePerHit(new Villager(0, Ort), haus));
        Assert.True(BuildingCombat.Hit(new Villager(0, Ort), haus));
        Assert.Equal(0, haus.CurrentHp);
    }

    [Fact]
    public void Drei_Dorfbewohner_reissen_ein_Haus_in_gut_zwei_Minuten_ein()
    {
        var haus = BuildingEntity.CreateHouse(1, Ort);
        var dorf = new[] { new Villager(0, Ort), new Villager(0, Ort), new Villager(0, Ort) };
        int schlaege = 0;
        while (!dorf.Select(v => BuildingCombat.Hit(v, haus)).ToList().Any(z => z))
            schlaege++;
        float sekunden = (schlaege + 1) * BuildingCombat.RELOAD_SECONDS;
        Assert.InRange(sekunden, 120f, 140f);
    }
}
