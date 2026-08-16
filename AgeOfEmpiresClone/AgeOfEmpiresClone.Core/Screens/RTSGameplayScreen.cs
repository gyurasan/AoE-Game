using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEmpiresClone.Core.Data;
using AgeOfEmpiresClone.Core.Inputs;
using AgeOfEmpiresClone.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AgeOfEmpiresClone.Core.Screens;

/// <summary>
/// Main RTS gameplay screen with tilemap, units, and resource management
/// </summary>
public class RTSGameplayScreen : GameScreen
{
    private GraphicsDevice graphicsDevice;
    private SpriteBatch spriteBatch;
    private Texture2D tileTexture;

    // Camera
    private Vector2 cameraPosition = Vector2.Zero;
    private float cameraZoom = 1.0f;
    private Rectangle screenBounds;
    
    // Game state
    private TileMap tileMap;
    private Data.Player player1;
    private Data.Player player2;
    private List<Unit> units;
    
    // Selection
    private List<Unit> selectedUnits = new List<Unit>();
    private Vector2? selectionStart = null;
    private Rectangle selectionRectangle = Rectangle.Empty;
    
    // Resource gathering
    private float gatherTimer = 0f;
    private const float GATHER_INTERVAL = 2f; // seconds
    
    public RTSGameplayScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(1.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    /// <summary>
    /// The world is built here rather than in the constructor, because the
    /// GraphicsDevice is only reachable once the ScreenManager has adopted us.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();

        graphicsDevice = ScreenManager.GraphicsDevice;
        screenBounds = new Rectangle(0, 0, graphicsDevice.PresentationParameters.BackBufferWidth,
                                          graphicsDevice.PresentationParameters.BackBufferHeight);

        // Initialize game
        tileMap = new TileMap(64, 64, 32);
        player1 = new Data.Player(0, "Player 1", "Briten");
        player2 = new Data.Player(1, "Player 2", "Azteken");

        // Add units from tilemap
        units = tileMap.Units;

        // Assign units to players
        foreach (var unit in units)
        {
            if (unit.OwnerId == 0) player1.AddUnit(unit);
            else player2.AddUnit(unit);
        }

        // Create placeholder textures
        tileTexture = CreateTexture(graphicsDevice, 32, 32, Color.Green);

        // Start the camera on player 1's town center instead of the map corner.
        var start = tileMap.GridToWorld(new Vector2(3, 3));
        cameraPosition = new Vector2(-start.X + screenBounds.Width / 2f,
                                     -start.Y + screenBounds.Height / 2f);
    }

    public override void UnloadContent()
    {
        tileTexture?.Dispose();
        tileTexture = null;
    }

    private Texture2D CreateTexture(GraphicsDevice device, int width, int height, Color color)
    {
        var texture = new Texture2D(device, width, height);
        var colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = color;
        texture.SetData(colors);
        return texture;
    }
    
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (!IsActive)
            return;

        // Keep the HUD and selection maths in sync with window resizes.
        screenBounds = new Rectangle(0, 0,
            graphicsDevice.PresentationParameters.BackBufferWidth,
            graphicsDevice.PresentationParameters.BackBufferHeight);

        HandleRtsInput(gameTime);
        UpdateUnits(gameTime);
        UpdateResources(gameTime);
    }

    /// <summary>
    /// Screen-level input: back out to the main menu.
    /// </summary>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        if (inputState.IsPauseGame(ControllingPlayer))
        {
            ExitScreen();
        }
    }

    private void HandleRtsInput(GameTime gameTime)
    {
        var keyboard = Keyboard.GetState();
        var mouse = Mouse.GetState();
        
        // Camera controls
        if (keyboard.IsKeyDown(Keys.Left))
            cameraPosition.X -= 10;
        if (keyboard.IsKeyDown(Keys.Right))
            cameraPosition.X += 10;
        if (keyboard.IsKeyDown(Keys.Up))
            cameraPosition.Y -= 10;
        if (keyboard.IsKeyDown(Keys.Down))
            cameraPosition.Y += 10;
        
        // Zoom
        if (keyboard.IsKeyDown(Keys.OemPlus))
            cameraZoom = Math.Min(cameraZoom + 0.1f, 2.0f);
        if (keyboard.IsKeyDown(Keys.OemMinus))
            cameraZoom = Math.Max(cameraZoom - 0.1f, 0.5f);
        
        // Mouse selection
        // BUGFIX: vorher rohe Bildschirmkoordinaten an WorldToGrid → Auswahl/Movement
        // haben die Kamera ignoriert und trafen von Anfang an die falsche Kachel.
        // ScreenToWorld (Inverse von WorldToScreen: pos/zoom - cameraPos) korrekt anwenden.
        var mouseGridPos = WorldToGrid(ScreenToWorld(new Vector2(mouse.X, mouse.Y)));
        
        if (mouse.LeftButton == ButtonState.Pressed)
        {
            if (!selectionStart.HasValue)
            {
                selectionStart = new Vector2(mouse.X, mouse.Y);
            }
            
            var currentPos = new Vector2(mouse.X, mouse.Y);
            var minX = Math.Min(selectionStart.Value.X, currentPos.X);
            var minY = Math.Min(selectionStart.Value.Y, currentPos.Y);
            var width = Math.Abs(currentPos.X - selectionStart.Value.X);
            var height = Math.Abs(currentPos.Y - selectionStart.Value.Y);
            
            selectionRectangle = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
        }
        else if (mouse.LeftButton == ButtonState.Released && selectionStart.HasValue)
        {
            // Finish selection
            if (selectionRectangle.Width > 5 && selectionRectangle.Height > 5)
            {
                // Select units in rectangle
                var worldSelection = new Rectangle(
                    (int)(selectionRectangle.X / cameraZoom - cameraPosition.X),
                    (int)(selectionRectangle.Y / cameraZoom - cameraPosition.Y),
                    (int)(selectionRectangle.Width / cameraZoom),
                    (int)(selectionRectangle.Height / cameraZoom));
                
                SelectUnitsInRectangle(worldSelection);
            }
            else
            {
                // Single unit click
                SelectSingleUnit(mouseGridPos);
            }
            
            selectionStart = null;
            selectionRectangle = Rectangle.Empty;
        }
        
        // Right click to move
        if (mouse.RightButton == ButtonState.Pressed)
        {
            MoveSelectedUnitsTo(mouseGridPos);
        }
    }
    
    private void SelectUnitsInRectangle(Rectangle selectionRect)
    {
        foreach (var u in units.Where(u => u.OwnerId == 0))
        {
            var unitRect = new Rectangle(
                (int)(u.Position.X - 8), (int)(u.Position.Y - 8), 16, 16);
            
            if (selectionRect.Intersects(unitRect))
            {
                if (!selectedUnits.Contains(u))
                {
                    selectedUnits.Add(u);
                    u.IsSelected = true;
                }
            }
            else
            {
                u.IsSelected = false;
            }
        }
        
        // Remove units no longer in list
        selectedUnits.RemoveAll(u => !u.IsSelected);
    }
    
    private void SelectSingleUnit(Vector2 gridPos)
    {
        // Clear previous selection
        foreach (var unit in selectedUnits)
            unit.IsSelected = false;
        selectedUnits.Clear();
        
        // Find unit under cursor
        var worldPos = GridToWorld(gridPos);
        var selectRect = new Rectangle(
            (int)(worldPos.X - 16), (int)(worldPos.Y - 16), 32, 32);
        
        var u = units.FirstOrDefault(u => u.OwnerId == 0 && 
            selectRect.Intersects(new Rectangle((int)(u.Position.X - 8), (int)(u.Position.Y - 8), 16, 16)));
        
        if (u != null)
        {
            selectedUnits.Add(u);
            u.IsSelected = true;
        }
    }
    
    private void MoveSelectedUnitsTo(Vector2 gridPos)
    {
        var targetPos = GridToWorld(gridPos);
        
        foreach (var unit in selectedUnits)
        {
            unit.TargetPosition = targetPos;
            unit.Path = tileMap.FindPath(unit.Position, targetPos);
            unit.State = UnitState.Moving;
        }
    }
    
    private void UpdateUnits(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        foreach (var unit in units.Where(u => u.OwnerId == 0))
        {
            switch (unit.State)
            {
                case UnitState.Moving:
                    UpdateMovingUnit(unit, dt);
                    break;
                case UnitState.Gathering:
                    UpdateGatheringUnit(unit, dt);
                    break;
            }
        }
    }
    
    private void UpdateMovingUnit(Unit unit, float dt)
    {
        if (unit.Path.Count == 0)
        {
            unit.State = UnitState.Idle;
            return;
        }
        
        var nextPosition = unit.Path[0];
        var direction = nextPosition - unit.Position;
        var distance = direction.Length();
        
        if (distance < 2)
        {
            unit.Path.RemoveAt(0);
            return;
        }
        
        direction.Normalize();
        unit.Position += direction * unit.MovementSpeed * 40 * dt;
    }
    
    private void UpdateGatheringUnit(Unit unit, float dt)
    {
        gatherTimer += dt;
        
        if (gatherTimer >= GATHER_INTERVAL)
        {
            gatherTimer = 0f;
            
            if (unit.CarryingResource.HasValue && unit.CarryingAmount < 10)
            {
                // Still gathering
                unit.CarryingAmount++;
            }
            else if (unit.CarryingAmount > 0)
            {
                // Deliver to town center
                DeliverResource(unit);
                unit.CarryingAmount = 0;
                unit.CarryingResource = null;
            }
            else
            {
                // Find a resource to gather
                var gridPos = tileMap.WorldToGrid(unit.Position);
                var tile = tileMap.GetTile((int)gridPos.X, (int)gridPos.Y);
                
                if (tile != null && tile.ResourceType.HasValue)
                {
                    unit.CarryingResource = tile.ResourceType.Value;
                    unit.CarryingAmount = 1;
                    unit.State = UnitState.Returning;
                }
            }
        }
    }
    
    private void DeliverResource(Unit unit)
    {
        if (!unit.CarryingResource.HasValue)
            return;
        
        // Add to player resources (70% efficiency like AoE)
        var amount = unit.CarryingAmount;
        var resourceType = unit.CarryingResource.Value;
        player1.Resources[resourceType] += (int)(amount * 0.7f);
    }
    
    private void UpdateResources(GameTime gameTime)
    {
        // Natural resource regeneration (slow)
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile != null && tile.ResourceAmount < 100)
                {
                    // Natural regeneration
                    tile.ResourceAmount += (int)(dt * 0.1f);
                }
            }
        }
    }
    
    public override void Draw(GameTime gameTime)
    {
        spriteBatch = ScreenManager.SpriteBatch;

        // Clear background — must happen outside of Begin/End.
        graphicsDevice.Clear(new Color(22, 79, 45)); // Grass color

        // Deliberately drawn without ScreenManager.GlobalTransformation: this screen
        // renders in raw backbuffer space so that mouse coordinates and drawn pixels
        // line up for unit selection.
        spriteBatch.Begin();

        // Draw tilemap
        DrawTileMap(spriteBatch);

        // Draw units
        DrawUnits(spriteBatch);

        // Draw selection rectangle
        if (selectionRectangle.Width > 0 && selectionRectangle.Height > 0)
        {
            spriteBatch.Draw(tileTexture, selectionRectangle, new Color(255, 255, 255, 100));
        }

        // Draw UI
        DrawUI(spriteBatch);

        spriteBatch.End();

        // Fade the screen in/out while transitioning.
        if (TransitionPosition > 0)
            ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
    }
    
    private void DrawTileMap(SpriteBatch spriteBatch)
    {
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                
                // Only draw visible tiles
                if (tile != null)
                {
                    var worldPos = tileMap.GridToWorld(new Vector2(x, y));
                    var screenPos = WorldToScreen(worldPos);
                    
                    if (screenPos.X >= -64 && screenPos.X <= screenBounds.Width + 64 &&
                        screenPos.Y >= -64 && screenPos.Y <= screenBounds.Height + 64)
                    {
                        var color = GetTileColor(tile);
                        spriteBatch.Draw(tileTexture, new Rectangle((int)screenPos.X, (int)screenPos.Y, 32, 32), color);
                    }
                }
            }
        }
    }
    
    private void DrawUnits(SpriteBatch spriteBatch)
    {
        foreach (var unit in units.Where(u => u.OwnerId == 0))
        {
            var screenPos = WorldToScreen(unit.Position);
            
            // Draw unit
            var color = unit.IsSelected ? Color.White : new Color(200, 200, 200);
            spriteBatch.Draw(tileTexture, new Rectangle((int)screenPos.X - 8, (int)screenPos.Y - 8, 16, 16), color);
            
            // Draw selection ring
            if (unit.IsSelected)
            {
                spriteBatch.Draw(tileTexture, new Rectangle((int)screenPos.X - 12, (int)screenPos.Y - 12, 24, 24), 
                    new Color(255, 255, 0, 150));
            }
            
            // Draw health bar
            var healthPercent = (float)unit.Health / unit.MaxHealth;
            spriteBatch.Draw(tileTexture, new Rectangle((int)screenPos.X - 10, (int)screenPos.Y - 20, 20, 4), 
                new Color(255, 0, 0));
            spriteBatch.Draw(tileTexture, new Rectangle((int)screenPos.X - 10, (int)screenPos.Y - 20, (int)(20 * healthPercent), 4), 
                new Color(0, 255, 0));
        }
    }
    
    private void DrawUI(SpriteBatch spriteBatch)
    {
        var font = ScreenManager.Font;
        if (font == null)
            return;

        // Resource bar across the top.
        spriteBatch.Draw(tileTexture, new Rectangle(0, 0, screenBounds.Width, 40), new Color(0, 0, 0, 160));

        var hud = string.Format("Nahrung {0}   Holz {1}   Gold {2}   Stein {3}   Bev. {4}/{5}   {6}",
            player1.Resources[Resource.Type.Food],
            player1.Resources[Resource.Type.Wood],
            player1.Resources[Resource.Type.Gold],
            player1.Resources[Resource.Type.Stone],
            player1.PopulationCount,
            player1.PopulationLimit,
            player1.CurrentAge);

        spriteBatch.DrawString(font, hud, new Vector2(12, 6), Color.White);

        var hint = string.Format("Links: auswählen   Rechts: bewegen   Pfeiltasten: Kamera   +/-: Zoom   ESC: Menü   ({0} ausgewählt)",
            selectedUnits.Count);
        spriteBatch.DrawString(font, hint, new Vector2(12, screenBounds.Height - 34), new Color(230, 230, 230));
    }
    
    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        return new Vector2(
            (worldPos.X + cameraPosition.X) * cameraZoom,
            (worldPos.Y + cameraPosition.Y) * cameraZoom);
    }
    
    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return new Vector2(
            screenPos.X / cameraZoom - cameraPosition.X,
            screenPos.Y / cameraZoom - cameraPosition.Y);
    }
    
    private Vector2 WorldToGrid(Vector2 worldPos)
    {
        return tileMap.WorldToGrid(worldPos);
    }
    
    private Vector2 GridToWorld(Vector2 gridPos)
    {
        return tileMap.GridToWorld(gridPos);
    }
    
    private Color GetTileColor(AgeOfEmpiresClone.Core.Data.Tile tile)
    {
        return tile.Type switch
        {
            TileType.Water => Color.Blue,
            TileType.Forest => new Color(34, 139, 34),
            TileType.Mountain => new Color(105, 105, 105),
            TileType.GoldMine => Color.Gold,
            TileType.Base => Color.Peru,
            _ => new Color(22, 79, 45) // Grass
        };
    }
    
    public void HandleMouseClick(Vector2 position)
    {
        // Handle mouse clicks for building placement, unit commands, etc.
        var gridPos = WorldToGrid(position);
        
        // Check if clicking on a resource
        var tile = tileMap.GetTile((int)gridPos.X, (int)gridPos.Y);
        if (tile != null && tile.ResourceType.HasValue)
        {
            // Assign villager to gather this resource
            var villager = units.FirstOrDefault(v => v.Type == UnitType.Villager && v.State == UnitState.Idle && v.OwnerId == 0);
            if (villager != null)
            {
                villager.TargetPosition = GridToWorld(gridPos);
                villager.CarryingResource = tile.ResourceType.Value;
                villager.State = UnitState.Gathering;
            }
        }
    }
}
