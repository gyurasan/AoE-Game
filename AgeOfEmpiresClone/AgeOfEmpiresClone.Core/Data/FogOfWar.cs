using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Fog of War system for visibility management
/// </summary>
public class FogOfWar
{
    private int width;
    private int height;
    private bool[,] visibilityGrid;
    private bool[,] exploredGrid;
    
    public FogOfWar(int width, int height)
    {
        this.width = width;
        this.height = height;
        
        // Initialize grids
        visibilityGrid = new bool[width, height];
        exploredGrid = new bool[width, height];
        
        // Mark entire map as initially unknown
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                visibilityGrid[x, y] = false;
                exploredGrid[x, y] = false;
            }
        }
    }
    
    /// <summary>
    /// Update visibility for all players
    /// </summary>
    public void UpdateVisibility(Func<int, int, bool> isVisibleFunc)
    {
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (isVisibleFunc(x, y))
                {
                    visibilityGrid[x, y] = true;
                    exploredGrid[x, y] = true;
                }
                else
                {
                    visibilityGrid[x, y] = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Update visibility for a specific player
    /// </summary>
    public void UpdatePlayerVisibility(int playerId, List<Unit> units, TileMap tileMap)
    {
        // Clear previous visibility
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                visibilityGrid[x, y] = false;
            }
        }
        
        // Update visibility from units
        foreach (var unit in units)
        {
            if (unit.OwnerId == playerId && unit.State != UnitState.Dead)
            {
                SetUnitVisibility(unit, tileMap);
            }
        }
        
        // Mark explored areas that were visible before
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (visibilityGrid[x, y])
                {
                    exploredGrid[x, y] = true;
                }
            }
        }
    }
    
    /// <summary>
    /// Set visibility for a unit's sight range
    /// </summary>
    private void SetUnitVisibility(Unit unit, TileMap tileMap)
    {
        var sightRange = 6; // Unit sight radius in tiles
        var centerGrid = tileMap.WorldToGrid(unit.Position);
        
        for (int dx = -sightRange; dx <= sightRange; dx++)
        {
            for (int dy = -sightRange; dy <= sightRange; dy++)
            {
                var x = (int)centerGrid.X + dx;
                var y = (int)centerGrid.Y + dy;
                
                if (x >= 0 && x < width && y >= 0 && y < height)
                {
                    // Simple circular visibility
                    var distance = Math.Sqrt(dx * dx + dy * dy);
                    if (distance <= sightRange)
                    {
                        visibilityGrid[x, y] = true;
                    }
                }
            }
        }
    }
    
    /// <summary>
    /// Check if a tile is visible to a player
    /// </summary>
    public bool IsVisible(int x, int y)
    {
        return visibilityGrid[x, y];
    }
    
    /// <summary>
    /// Check if a tile has been explored
    /// </summary>
    public bool IsExplored(int x, int y)
    {
        return exploredGrid[x, y];
    }
    
    /// <summary>
    /// Set a tile as visible
    /// </summary>
    public void SetVisible(int x, int y, bool value = true)
    {
        visibilityGrid[x, y] = value;
        if (value)
            exploredGrid[x, y] = true;
    }
    
    /// <summary>
    /// Set a tile as explored
    /// </summary>
    public void SetExplored(int x, int y, bool value = true)
    {
        exploredGrid[x, y] = value;
    }
    
    /// <summary>
    /// Get the full visibility grid
    /// </summary>
    public bool[,] GetVisibilityGrid()
    {
        return visibilityGrid;
    }
    
    /// <summary>
    /// Get the full explored grid
    /// </summary>
    public bool[,] GetExploredGrid()
    {
        return exploredGrid;
    }
}
