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
        Assert.Equal(1.2f, scout.Stats.Speed);
    }
    
    [Fact]
    public void Spearman_HasCavalryBonus()
    {
        var spearman = new SpearMan(0, new Position(0, 0));
        var attack = spearman.Stats.GetTotalAttack(UnitClass.Cavalry);
        // Basisangriff 3 + Bonus 15 = 18
        Assert.Equal(18, attack);
    }
    
    [Fact]
    public void Knight_HasArcherBonus()
    {
        var knight = new Knight(0, new Position(0, 0));
        var attack = knight.Stats.GetTotalAttack(UnitClass.Archer);
        // Basisangriff 10 + Bonus 5 = 15
        Assert.Equal(15, attack);
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
        
        // Speer: 3 Nahkampf + 15 gegen Kavallerie = 18 Gesamt
        // Ritter: 2 Nahkampf Rüstung
        // 18 - 2 = 16, mindestens 1
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
