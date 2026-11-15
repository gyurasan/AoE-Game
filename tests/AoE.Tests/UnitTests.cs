using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Entities;
using AoE.Core.Combat;
using AoE.Core.Economy;
using AoE.Core.Map;
using Xunit;

namespace AoE.Tests;

public class UnitTests
{
    [Fact]
    public void Villager_CanBeCreated()
    {
        var pos = new Position(10, 10);
        var villager = new Villager(0, pos);
        
        Assert.Equal(0, villager.OwnerId);
        Assert.Equal(25, villager.CurrentHp);
        Assert.Equal(pos, villager.Position);
        Assert.Equal(25, villager.Stats.HitPoints);
    }
    
    [Fact]
    public void Scout_HasBetterVision()
    {
        var scout = new Scout(0, new Position(0, 0));
        Assert.Equal(7, scout.Stats.VisionRange);
        Assert.Equal(2.7f, scout.Stats.Speed);
    }

    /// <summary>
    /// Nutzer 2026-11-13: Dorfbewohner, Miliz und Bogenschützen laufen gleich schnell,
    /// nur Reiter sind schneller. Geprüft über die Spielbrücke, damit auch die
    /// Spieltypen, die auf eine Kernklasse abgebildet werden (Langbogen, Schwertkämpfer …),
    /// das gemeinsame Fußtempo tragen.
    /// </summary>
    [Fact]
    public void FootUnits_ShareOneSpeed_OnlyRidersAreFaster()
    {
        var foot = new[]
        {
            AgeOfEvolutions.Core.Data.UnitType.Villager,
            AgeOfEvolutions.Core.Data.UnitType.Builder,
            AgeOfEvolutions.Core.Data.UnitType.Monk,
            AgeOfEvolutions.Core.Data.UnitType.Militia,
            AgeOfEvolutions.Core.Data.UnitType.SpearMan,
            AgeOfEvolutions.Core.Data.UnitType.HeavyInfantry,
            AgeOfEvolutions.Core.Data.UnitType.ManAtArms,
            AgeOfEvolutions.Core.Data.UnitType.Archer,
            AgeOfEvolutions.Core.Data.UnitType.Longbowman,
            AgeOfEvolutions.Core.Data.UnitType.Skirmisher,
        };
        foreach (var type in foot)
            Assert.True(SpeedOf(type) == UnitSpeeds.Foot, $"{type} läuft {SpeedOf(type)} statt {UnitSpeeds.Foot}");

        var riders = new[]
        {
            AgeOfEvolutions.Core.Data.UnitType.Scout,
            AgeOfEvolutions.Core.Data.UnitType.Cavalry,
            AgeOfEvolutions.Core.Data.UnitType.Paladin,
            AgeOfEvolutions.Core.Data.UnitType.CamelRider,
            AgeOfEvolutions.Core.Data.UnitType.ImperialCamel,
        };
        foreach (var type in riders)
            Assert.True(SpeedOf(type) > UnitSpeeds.Foot, $"{type} ({SpeedOf(type)}) ist nicht schneller als Fußvolk");

        // Belagerungsgeräte bleiben langsamer als Fußvolk
        foreach (var type in new[] { AgeOfEvolutions.Core.Data.UnitType.Ram, AgeOfEvolutions.Core.Data.UnitType.Catapult, AgeOfEvolutions.Core.Data.UnitType.Trebuchet })
            Assert.True(SpeedOf(type) < UnitSpeeds.Foot, $"{type} ({SpeedOf(type)}) ist nicht langsamer als Fußvolk");

        static float SpeedOf(AgeOfEvolutions.Core.Data.UnitType type)
            => AgeOfEvolutions.Core.Data.CoreUnits.Create(type, 0).Stats.Speed;
    }

    [Fact]
    public void Spearman_HasCavalryBonus()
    {
        var spearman = new SpearMan(0, new Position(0, 0));
        var attack = spearman.Stats.GetTotalAttack(UnitClass.Cavalry);
        // Basisangriff 3 + Bonus 10 = 13
        Assert.Equal(13, attack);
    }
    
    [Fact]
    public void Knight_HasArcherBonus()
    {
        var knight = new Knight(0, new Position(0, 0));
        var attack = knight.Stats.GetTotalAttack(UnitClass.Archer);
        // Basisangriff 12 + Bonus 5 = 17
        Assert.Equal(17, attack);
    }
    
    [Fact]
    public void Ram_HasBuildingBonus()
    {
        var ram = new Ram(0, new Position(0, 0));
        var attack = ram.Stats.GetTotalAttack(UnitClass.Building);
        // Basisangriff 2 + Bonus 125 = 127
        Assert.Equal(127, attack);
    }
    
    [Fact]
    public void Trebuchet_HasBuildingBonus()
    {
        var trebuchet = new Trebuchet(0, new Position(0, 0));
        var attack = trebuchet.Stats.GetTotalAttack(UnitClass.Building);
        // Basisangriff 200 + Bonus 400 = 600
        Assert.Equal(600, attack);
    }
}

public class DamageCalculatorTests
{
    [Fact]
    public void BaseDamage_CalculatesCorrectly()
    {
        var spearman = new SpearMan(0, new Position(0, 0));
        var knight = new Knight(1, new Position(1, 1));
        
        int damage = DamageCalculator.CalculateDamage(spearman, knight);
        
        // Speer: Schritt 1 Grundangriff 3 + Schritt 2 gegen Kavallerie
        // (Basis 3 + Bonus 10) = 16 Gesamt
        Assert.True(damage >= 16);
    }
    
    [Fact]
    public void SpearmanVsArcher_LowDamage()
    {
        var spearman = new SpearMan(0, new Position(0, 0));
        var archer = new Archer(1, new Position(1, 1));
        
        int damage = DamageCalculator.CalculateDamage(spearman, archer);
        
        // Spearman vs Archer: 6 Nahkampf (Basisschaden)
        // Archer hat 0 Nahkampf Rüstung
        Assert.Equal(6, damage);
    }
}

public class ResourcePoolTests
{
    [Fact]
    public void Resources_AreInitializedCorrectly()
    {
        var pool = new ResourcePool();
        
        Assert.Equal(200, pool[Resource.Food]);
        Assert.Equal(200, pool[Resource.Wood]);
        Assert.Equal(100, pool[Resource.Gold]);
        Assert.Equal(200, pool[Resource.Stone]);
    }
    
    [Fact]
    public void Resources_CanBeAddedAndRemoved()
    {
        var pool = new ResourcePool();
        
        pool.Add(Resource.Food, 100);
        Assert.Equal(300, pool[Resource.Food]);
        
        Assert.True(pool.Remove(Resource.Wood, 50));
        Assert.Equal(150, pool[Resource.Wood]);
    }
    
    [Fact]
    public void CannotRemoveMoreThanAvailable()
    {
        var pool = new ResourcePool();
        
        Assert.False(pool.Remove(Resource.Gold, 500));
        Assert.Equal(100, pool[Resource.Gold]);
    }
}

public class MapTests
{
    [Fact]
    public void MapGrid_CanBeCreated()
    {
        var map = new MapGrid(100, 100);
        
        Assert.Equal(100, map.Width);
        Assert.Equal(100, map.Height);
    }
    
    [Fact]
    public void Tile_CanbeRetrieved()
    {
        var map = new MapGrid(100, 100);
        
        var tile = map.GetTile(50, 50);
        Assert.NotNull(tile);
        Assert.Equal(50, tile!.X);
        Assert.Equal(50, tile.Y);
    }
}

public class VisibilityTests
{
    [Fact]
    public void TileVisibility_IsInitializedCorrectly()
    {
        var tile = new Tile(0, 0);
        
        Assert.Equal(TileVisibility.Unexplored, tile.GetVisibility(0));
        Assert.Equal(TileVisibility.Unexplored, tile.GetVisibility(1));
    }
    
    [Fact]
    public void TileVisibility_CanBeSet()
    {
        var tile = new Tile(0, 0);
        
        tile.SetVisibility(0, TileVisibility.Visible);
        Assert.Equal(TileVisibility.Visible, tile.GetVisibility(0));
    }
}
