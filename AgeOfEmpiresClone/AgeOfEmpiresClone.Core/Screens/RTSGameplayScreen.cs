using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEmpiresClone.Core.Data;
using AgeOfEmpiresClone.Core.Inputs;
using AgeOfEmpiresClone.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;

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

    private const float MIN_ZOOM = 0.5f;
    private const float MAX_ZOOM = 2.0f;

    // Ein Schritt je Rastung des Mausrads. Multiplikativ, damit sich das
    // Zoomen über den ganzen Bereich gleich schnell anfühlt — additiv wäre
    // es nah an MIN_ZOOM viel grober als nah an MAX_ZOOM.
    private const float ZOOM_STEP = 1.12f;

    // ScrollWheelValue zählt seit Programmstart hoch. Ohne diesen Startwert
    // ergäbe der erste Frame eine riesige Differenz und würde sofort auf
    // Maximalzoom springen.
    private int previousScrollWheel;
    private bool scrollInitialised;
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
    
    // Rendering (prozedural generierte AoE1-artige Texturen)
    private Texture2D px;                     // 1x1-Pixel für FillRect/DrawLine
    private Dictionary<TileType, Texture2D> tileTex = new();
    private Dictionary<UnitType, Texture2D> unitTex = new();
    private Texture2D[] waterFrames = Array.Empty<Texture2D>();
    private int waterAnimationFrameCounter;
    private float waterAnimTimer;
    private List<Unit> drawList = new();
    
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
        BuildAoETextures();

        // Start the camera on player 1's town center instead of the map corner.
        var start = tileMap.GridToWorld(new Vector2(3, 3));
        cameraPosition = new Vector2(-start.X + screenBounds.Width / 2f,
                                     -start.Y + screenBounds.Height / 2f);
        ClampCamera();
    }

    public override void UnloadContent()
    {
        tileTexture?.Dispose();
        tileTexture = null;
        px?.Dispose(); px = null;
        foreach (var t in tileTex.Values) t?.Dispose();
        tileTex.Clear();
        foreach (var t in waterFrames) t?.Dispose();
        waterFrames = Array.Empty<Texture2D>();
        foreach (var t in unitTex.Values) t?.Dispose();
        unitTex.Clear();
    }

    // Fester Seed: die prozeduralen Texturen sollen bei jedem Start gleich aussehen.
    private readonly Random _texRng = new Random(1337);
    
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
        
        // Wasser-Animation: alle ~400 ms ein Frame weiter (2,5 fps-Loop)
        waterAnimTimer += (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        if (waterFrames.Length > 0 && waterAnimTimer > 400f)
        {
            waterAnimTimer = 0f;
            waterAnimationFrameCounter = (waterAnimationFrameCounter + 1) % waterFrames.Length;
        }
    }

    // ====================================================================
    // AoE1-artiges Rendering: prozedurale Pixel-Art-Texturen
    // ====================================================================
    
    // --- Kleine Pixel-Helfer -------------------------------------------
    
    private class TextureBuilder
    {
        public readonly int W, H;
        public Color[] P;
        public TextureBuilder(int w, int h) { W = w; H = h; P = new Color[w * h]; }
        public TextureBuilder Set(int x, int y, Color c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) P[y * W + x] = c;
            return this;
        }
        public void FillRect(int x, int y, int w, int h, Color c)
        {
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++) Set(i, j, c);
        }
        public void FillCircle(int cx, int cy, int r, Color c)
        {
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                    if (i * i + j * j <= r * r) Set(cx + i, cy + j, c);
        }
        public void OutlineRect(int x, int y, int w, int h, Color c)
        {
            FillRect(x, y, w, 1, c);
            FillRect(x, y + h - 1, w, 1, c);
            FillRect(x, y, 1, h, c);
            FillRect(x + w - 1, y, 1, h, c);
        }
        public void Noise(Color baseC, int amount, Random rng, float density = 1f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (rng.NextDouble() > density) continue;
                    int d = rng.Next(-amount, amount + 1);
                    var c = P[y * W + x];
                    Set(x, y, new Color(
                        (byte)Math.Clamp(c.R + d, 0, 255),
                        (byte)Math.Clamp(c.G + d, 0, 255),
                        (byte)Math.Clamp(c.B + d, 0, 255)));
                }
        }
        public Texture2D Build(GraphicsDevice gd)
        {
            var t = new Texture2D(gd, W, H);
            t.SetData(P);
            return t;
        }
    }
    
    /// <summary>
    /// Erzeugt prozedurale Pixel-Art-Texturen für alle Kacheltypen + Wasserframes.
    /// Ruft sich in LoadContent, nachdem graphicsDevice verfügbar ist.
    /// </summary>
    private void BuildAoETextures()
    {
        var gd = graphicsDevice;
        px = new Texture2D(gd, 1, 1);
        px.SetData(new[] { Color.White });
        
        tileTex[TileType.Grassland] = BuildGrassTexture(gd);
        tileTex[TileType.Sand] = BuildSandTexture(gd);
        tileTex[TileType.Rock] = BuildRockTexture(gd);
        tileTex[TileType.Forest] = BuildForestTexture(gd);
        tileTex[TileType.Mountain] = BuildMountainTexture(gd);
        tileTex[TileType.GoldMine] = BuildGoldTexture(gd);
        tileTex[TileType.Snow] = BuildSandTexture(gd); // Snow ≈ Sand-Optik
        
        // Wasser als 4 Frames (für die Animation)
        waterFrames = new[]
        {
            BuildWaterTexture(gd, 0),
            BuildWaterTexture(gd, 1),
            BuildWaterTexture(gd, 2),
            BuildWaterTexture(gd, 3)
        };
        tileTex[TileType.Water] = waterFrames[0];
        tileTex[TileType.Base] = BuildGrassTexture(gd); // Startzone = Gras
        tileTex[TileType.Wall] = BuildRockTexture(gd);
        
        // Einheiten-Sprites (24x24). Basisfarbe blau; Spieler 2 wird beim Zeichnen rot getönt.
        var unitBase = new Color(70, 110, 190);
        unitTex[UnitType.Villager] = BuildVillagerTexture(gd, unitBase);
        unitTex[UnitType.SpearMan] = BuildSpearManTexture(gd, unitBase);
        unitTex[UnitType.Archer] = BuildArcherTexture(gd, unitBase);
        unitTex[UnitType.Cavalry] = BuildCavalryTexture(gd, unitBase);
    }
    
    private Texture2D BuildGrassTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(76, 141, 48));
        b.Noise(new Color(76, 141, 48), 10, _texRng, 0.6f);
        // Einzelne helle Grashalme
        for (int i = 0; i < 14; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(100, 170, 60));
            if (_texRng.NextDouble() < 0.4) b.Set(x, y - 1, new Color(90, 155, 52));
        }
        // Einzelne dunklere Flecken (Erde)
        for (int i = 0; i < 5; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(60, 110, 40));
            b.Set(x + 1, y, new Color(65, 118, 44));
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildSandTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(210, 190, 130));
        b.Noise(new Color(210, 190, 130), 8, _texRng, 0.7f);
        // Kleine Kieselsteine
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(180, 160, 110));
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildRockTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(130, 130, 125));
        b.Noise(new Color(130, 130, 125), 15, _texRng, 0.5f);
        // Felsbrocken
        for (int i = 0; i < 4; i++)
        {
            int x = _texRng.Next(28) + 2, y = _texRng.Next(28) + 2;
            b.FillCircle(x, y, _texRng.Next(2, 4), new Color(100, 100, 95));
            b.FillCircle(x - 1, y - 1, 1, new Color(150, 150, 140));
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildWaterTexture(GraphicsDevice gd, int frame)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(64, 128, 190));
        b.Noise(new Color(64, 128, 190), 6, _texRng, 0.5f);
        
        // Wellen: helle Streifen, die je Frame verschoben sind
        Color light = new Color(120, 180, 230);
        Color darker = new Color(40, 100, 160);
        
        for (int row = 0; row < 4; row++)
        {
            int yBase = row * 8 + frame;
            for (int y = 0; y < 8; y++)
            {
                int yy = (yBase + y) % 32;
                // Sägezahn-Muster wie kleine Wellen
                for (int x = 0; x < 32; x++)
                {
                    int phase = (x + frame * 4 + row) % 8;
                    if (phase < 2) b.Set(x, yy, light);
                    else if (phase >= 6) b.Set(x, yy, darker);
                }
            }
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildForestTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Hintergrund = Gras
        b.FillRect(0, 0, 32, 32, new Color(76, 141, 48));
        b.Noise(new Color(76, 141, 48), 8, _texRng, 0.4f);
        
        // 1–3 Bäume (Pixel-Art: Stamm + konische Blattschichten), zufällige Position
        int trees = _texRng.Next(1, 4);
        var spots = new[] { 6, 16, 26 };
        // Einfacher Shuffle (Fisher-Yates)
        for (int i = spots.Length - 1; i > 0; i--)
        {
            int j = _texRng.Next(i + 1);
            (spots[i], spots[j]) = (spots[j], spots[i]);
        }
        
        for (int t = 0; t < Math.Min(trees, 3); t++)
        {
            int tx = spots[t];
            DrawTree(b, tx, 10, 14);
        }
        return b.Build(gd);
    }
    
    private void DrawTree(TextureBuilder b, int cx, int topY, int size)
    {
        // Stamm
        for (int y = topY + size / 2; y < topY + size; y++)
        {
            b.Set(cx - 1, y, new Color(90, 60, 30));
            b.Set(cx, y, new Color(100, 68, 35));
        }
        // Blätter: drei konische Schichten
        for (int y = 0; y < size / 2; y++)
        {
            int radius = Math.Max(1, (size / 2 - y) / 2 + 1);
            int yy = topY + y;
            Color c = new Color(30 + y * 4, 90 + y * 6, 30);
            for (int x = -radius; x <= radius; x++)
                b.Set(cx + x, yy, c);
        }
        // Spitze
        b.Set(cx, topY - 1, new Color(20, 80, 25));
    }
    
    private Texture2D BuildMountainTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(130, 125, 120));
        b.Noise(new Color(130, 125, 120), 12, _texRng, 0.6f);
        
        // Bergkamm: diagonale Felsen
        for (int i = 0; i < 6; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            int sz = _texRng.Next(2, 5);
            b.FillRect(x, y, sz, sz, new Color(110, 105, 100));
            b.Set(x + 1, y, new Color(140, 135, 128)); // Lichtkante
            b.Set(x, y + sz - 1, new Color(80, 78, 72)); // Schattenkante
        }
        // Steinbruch-Spuren (dunkle Punkte)
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(95, 92, 88));
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildGoldTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Brauner Untergrund
        b.FillRect(0, 0, 32, 32, new Color(140, 110, 70));
        b.Noise(new Color(140, 110, 70), 10, _texRng, 0.5f);
        
        // Goldklumpen
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(28) + 2, y = _texRng.Next(28) + 2;
            int sz = _texRng.Next(1, 3);
            b.FillRect(x, y, sz, sz, new Color(255, 200, 50));
            if (sz > 1) b.Set(x, y, new Color(255, 230, 120)); // Glanz
        }
        // Fels um die Adern
        for (int i = 0; i < 5; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.FillCircle(x, y, _texRng.Next(1, 3), new Color(100, 80, 55));
        }
        return b.Build(gd);
    }
    
    private Texture2D BuildBerryTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Gras-Hintergrund
        b.FillRect(0, 0, 32, 32, new Color(76, 141, 48));
        b.Noise(new Color(76, 141, 48), 8, _texRng, 0.4f);
        
        // Büsch: grüner Haufen mit roten Beeren
        int cx = 16, cy = 18;
        b.FillCircle(cx, cy, 8, new Color(50, 110, 40));
        b.FillCircle(cx - 2, cy - 2, 5, new Color(60, 125, 48));
        b.FillCircle(cx + 3, cy + 2, 4, new Color(45, 100, 36));
        
        // Beeren (kleine rote Punkte)
        for (int i = 0; i < 10; i++)
        {
            int angle = _texRng.Next(360);
            int dist = _texRng.Next(0, 6);
            int x = cx + (int)(Math.Cos(angle * Math.PI / 180) * dist);
            int y = cy + (int)(Math.Sin(angle * Math.PI / 180) * dist);
            b.Set(x, y, new Color(200, 40, 40));
            b.Set(x + 1, y, new Color(180, 30, 30));
        }
        // Glanzpunkt
        b.Set(cx - 1, cy - 3, new Color(255, 100, 100));
        return b.Build(gd);
    }
    
    private Texture2D BuildSheepTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Gras
        b.FillRect(0, 0, 32, 32, new Color(76, 141, 48));
        b.Noise(new Color(76, 141, 48), 6, _texRng, 0.3f);
        
        // Schaf (sehr klein, pixelartig): 8×6 Pixel
        int sx = 10, sy = 12;
        // Wollkörper
        b.FillRect(sx, sy + 2, 10, 5, new Color(230, 228, 220));
        b.FillRect(sx + 1, sy + 1, 8, 1, new Color(230, 228, 220)); // Oberkante
        // Kopf (dunkler)
        b.FillRect(sx + 9, sy + 1, 3, 4, new Color(70, 60, 55));
        b.Set(sx + 11, sy + 2, new Color(30, 30, 30)); // Auge
        // Beine
        b.Set(sx + 1, sy + 7, new Color(60, 50, 45));
        b.Set(sx + 3, sy + 7, new Color(60, 50, 45));
        b.Set(sx + 6, sy + 7, new Color(60, 50, 45));
        b.Set(sx + 8, sy + 7, new Color(60, 50, 45));
        // Schatten
        for (int x = sx; x < sx + 12; x++)
            b.Set(x, sy + 8, new Color(56, 106, 38, 120));
        return b.Build(gd);
    }
    
    /// <summary>
    /// Screen-Eingaben: ESC öffnet das Pausenmenü.
    /// </summary>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        if (inputState.IsPauseGame(ControllingPlayer))
        {
            // Nicht ExitScreen(): LoadingScreen.Load hat beim Spielstart alle
            // vorherigen Screens beendet, das Hauptmenü existiert also nicht
            // mehr. Ein Schließen hinterließe einen leeren Stack — schwarzes
            // Bild ohne Rückweg. Das Pausenmenü bietet Fortsetzen und einen
            // sauberen Rückweg ins Hauptmenü.
            ScreenManager.AddScreen(new PauseScreen(), ControllingPlayer);
        }
    }
    private Texture2D BuildVillagerTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
        // Figur (10x16 Pixel): horizontal zentriert (x=7..17)
        int x = 7, y = 4;

        // Hautfarbe
        Color skinColor = new Color(225, 190, 150);
        Color outlineColor = new Color(40, 30, 25);

        // Kopf (eine kleine Halbkugel)
        b.FillCircle(x + 4, y + 1, 3, skinColor);
        b.OutlineRect(x + 1, y, 8, 3, outlineColor);

        // Körper (Kappe und Tunika)
        // Kappe (braun)
        b.FillRect(x, y + 4, 10, 2, new Color(139, 69, 19));
        b.OutlineRect(x, y + 4, 10, 2, outlineColor);

        // Tunika in Spielerfarbe
        b.FillRect(x + 1, y + 6, 8, 10, playerColor);
        b.OutlineRect(x + 1, y + 6, 8, 10, outlineColor);

        // Arme (klein, ausgerichtet mit Körper)
        b.FillRect(x - 1, y + 8, 2, 4, playerColor);
        b.FillRect(x + 9, y + 8, 2, 4, playerColor);
        b.OutlineRect(x - 1, y + 8, 2, 4, outlineColor);
        b.OutlineRect(x + 9, y + 8, 2, 4, outlineColor);

        // eine kleine Axt in der rechten Hand
        b.FillRect(x + 10, y + 10, 1, 3, new Color(150, 150, 150)); // Stock
        b.FillRect(x + 11, y + 10, 1, 2, new Color(100, 100, 100)); // Axtkopf

        // Beine (in der Mitte)
        b.FillRect(x + 3, y + 16, 2, 2, skinColor);
        b.FillRect(x + 6, y + 16, 2, 2, skinColor);

        // Schatten unter dem Fuß
        b.FillCircle(x + 4, y + 18, 3, new Color(0, 0, 0, 70));
        b.FillCircle(x + 7, y + 18, 3, new Color(0, 0, 0, 70));

        return b.Build(gd);
    }

    private Texture2D BuildSpearManTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
        // Figur (10x16 Pixel): horizontal zentriert (x=7..17)
        int x = 7, y = 4;

        // Hautfarbe
        Color skinColor = new Color(225, 190, 150);
        Color outlineColor = new Color(40, 30, 25);

        // Helm (Grau)
        b.FillRect(x + 2, y, 6, 2, new Color(150, 150, 160));
        b.OutlineRect(x + 2, y, 6, 2, outlineColor);
        b.FillCircle(x + 4, y + 1, 3, new Color(150, 150, 160));

        // Körper
        b.FillRect(x + 1, y + 4, 8, 10, playerColor); // Tunika in Spielerfarbe
        b.OutlineRect(x + 1, y + 4, 8, 10, outlineColor);

        // Arme (ausgerichtet mit Körper)
        b.FillRect(x - 1, y + 6, 2, 4, playerColor);
        b.FillRect(x + 9, y + 6, 2, 4, playerColor);
        b.OutlineRect(x - 1, y + 6, 2, 4, outlineColor);
        b.OutlineRect(x + 9, y + 6, 2, 4, outlineColor);

        // Speer auf der rechten Seite (1px breit, von 2 bis 21)
        b.FillRect(x + 10, y + 2, 1, 19, new Color(150, 150, 150)); // Speer-Schaft
        b.FillRect(x + 10, y + 1, 1, 1, new Color(200, 200, 200)); // Spitze

        // Beine (in der Mitte)
        b.FillRect(x + 3, y + 14, 2, 2, skinColor);
        b.FillRect(x + 6, y + 14, 2, 2, skinColor);

        // Schatten unter dem Fuß
        b.FillCircle(x + 4, y + 16, 3, new Color(0, 0, 0, 70));
        b.FillCircle(x + 7, y + 16, 3, new Color(0, 0, 0, 70));

        return b.Build(gd);
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
            cameraZoom = Math.Min(cameraZoom + 0.1f, MAX_ZOOM);
        if (keyboard.IsKeyDown(Keys.OemMinus))
            cameraZoom = Math.Max(cameraZoom - 0.1f, MIN_ZOOM);

        // Zoom mit dem Mausrad — auf den Cursor zu, nicht auf die Bildmitte.
        // Der Weltpunkt unter dem Zeiger bleibt dabei an Ort und Stelle, so
        // wie man es von Karten kennt.
        if (!scrollInitialised)
        {
            previousScrollWheel = mouse.ScrollWheelValue;
            scrollInitialised = true;
        }

        int wheelDelta = mouse.ScrollWheelValue - previousScrollWheel;
        previousScrollWheel = mouse.ScrollWheelValue;

        if (wheelDelta != 0)
        {
            var cursor = new Vector2(mouse.X, mouse.Y);
            var worldUnderCursor = ScreenToWorld(cursor);

            float factor = wheelDelta > 0 ? ZOOM_STEP : 1f / ZOOM_STEP;
            float newZoom = MathHelper.Clamp(cameraZoom * factor, MIN_ZOOM, MAX_ZOOM);

            if (newZoom != cameraZoom)
            {
                cameraZoom = newZoom;

                // Kamera so nachführen, dass derselbe Weltpunkt wieder unter
                // dem Cursor liegt. Umkehrung von ScreenToWorld:
                //   welt = bildschirm / zoom - kamera
                cameraPosition = cursor / cameraZoom - worldUnderCursor;
            }
        }

        // Egal ob per Tastatur geschwenkt, per Tastatur oder Mausrad gezoomt:
        // danach darf der Ausschnitt nicht über den Kartenrand hinausragen.
        ClampCamera();
        
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
                // Check if the source tile still has resources
                var gridPos = tileMap.WorldToGrid(unit.Position);
                var tile = tileMap.GetTile((int)gridPos.X, (int)gridPos.Y);
                
                if (tile == null || !tile.ResourceType.HasValue || tile.ResourceAmount <= 0)
                {
                    // No more resources — deliver what the unit is carrying
                    DeliverResource(unit);
                    unit.CarryingAmount = 0;
                    unit.CarryingResource = null;
                    return;
                }
                
                // Still gathering — reduce tile resource amount
                unit.CarryingAmount++;
                tile.ResourceAmount--;
                
                // If the source is depleted, clear it
                if (tile.ResourceAmount <= 0)
                {
                    tile.ResourceAmount = 0;
                    tile.ResourceType = null;
                }
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
        player1.Resources.Add(resourceType, (int)(amount * 0.7f));
    }
    
    private void UpdateResources(GameTime gameTime)
    {
        // No natural resource regeneration — resources are finite.
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
        // Kacheln
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null) continue;
                
                var worldPos = tileMap.GridToWorld(new Vector2(x, y));
                var screenPos = WorldToScreen(worldPos);
                int sx = (int)screenPos.X, sy = (int)screenPos.Y;
                
                if (sx >= -32 && sx <= screenBounds.Width && sy >= -32 && sy <= screenBounds.Height)
                {
                    var tex = GetTileTexture(tile);
                    if (tex != null)
                        spriteBatch.Draw(tex, new Rectangle(sx, sy, 32, 32), Color.White);
                }
            }
        }
        
        // Nahrungskacheln (Schafe/Beeren) als Objekte auf Gras-Grund
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null || !tile.ResourceType.HasValue) continue;
                if (tile.ResourceType != Resource.Food) continue;
                
                var worldPos = tileMap.GridToWorld(new Vector2(x, y));
                var screenPos = WorldToScreen(worldPos);
                int sx = (int)screenPos.X + 16, sy = (int)screenPos.Y + 16;
                
                if (sx < -32 || sx > screenBounds.Width + 32 || sy < -32 || sy > screenBounds.Height + 32)
                    continue;
                
                // Gras-Grund unter dem Tier
                if (tileTex.TryGetValue(TileType.Grassland, out var grass))
                    spriteBatch.Draw(grass, new Rectangle(sx - 16, sy - 16, 32, 32), Color.White);
                
                bool isSheep = tile.ResourceAmount >= 1 && tile.ResourceAmount <= 2;
                var objTex = isSheep ? BuildSheepTextureCached() : BuildBerryTextureCached();
                if (objTex != null)
                    spriteBatch.Draw(objTex, new Rectangle(sx - 16, sy - 16, 32, 32), Color.White);
            }
        }
        
        // Gebäude (Stadtzentrum = 4×4 Kacheln)
        foreach (var b in tileMap.Buildings)
        {
            DrawBuilding(spriteBatch, b);
        }
    }
    
    // Gedächtnis-Texturen für wiederkehrende Nahrungsobjekte
    private Texture2D _sheepTex;
    private Texture2D _berryTex;
    
    private Texture2D BuildSheepTextureCached()
    {
        if (_sheepTex == null) _sheepTex = BuildSheepTexture(graphicsDevice);
        return _sheepTex;
    }
    private Texture2D BuildBerryTextureCached()
    {
        if (_berryTex == null) _berryTex = BuildBerryTexture(graphicsDevice);
        return _berryTex;
    }
    
    private Texture2D GetTileTexture(Data.Tile tile)
    {
        // Wasser nutzt den animierten Frame
        if (tile.Type == TileType.Water && waterFrames.Length > 0)
            return waterFrames[waterAnimationFrameCounter];
        
        if (tileTex.TryGetValue(tile.Type, out var tex))
            return tex;
        
        return tileTex.Values.FirstOrDefault();
    }
    
    private void DrawBuilding(SpriteBatch spriteBatch, Data.Building b)
    {
        // Nur Gebäude mit bekanntem Typ zeichnen (erstmal: Stadtzentrum)
        var topWorld = GridToWorld(new Vector2(b.X, b.Y));
        var bottomWorld = GridToWorld(new Vector2(b.X + b.Width, b.Y + b.Height));
        
        Rectangle rect = new Rectangle(
            (int)WorldToScreen(topWorld).X, (int)WorldToScreen(topWorld).Y,
            (int)WorldToScreen(bottomWorld).X - (int)WorldToScreen(topWorld).X,
            (int)WorldToScreen(bottomWorld).Y - (int)WorldToScreen(topWorld).Y);
        
        if (rect.Intersects(screenBounds) == false && 
            new Rectangle(rect.X - 32, rect.Y - 32, rect.Width + 64, rect.Height + 64).Intersects(screenBounds) == false)
            return;
        
        bool isPlayer1 = b.OwnerId == 0;
        
        // Fundament (grün/braun)
        Color foundation = isPlayer1 ? new Color(120, 100, 70) : new Color(140, 90, 70);
        Color roof = isPlayer1 ? new Color(170, 160, 140) : new Color(180, 140, 120);
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);
        
        // 4×4 Kacheln = 128×128 Pixel bei Zoom 1
        int w = rect.Width, h = rect.Height;
        
        // Grundplatte
        spriteDraw(spriteBatch, px, rect, foundation);
        // Innenfläche (hellere Erde)
        var inner = new Rectangle(rect.X + 6, rect.Y + 6, w - 12, h - 12);
        spriteDraw(spriteBatch, px, inner, new Color(140, 125, 90));
        
        // Hauptgebäude (Mittelslot): 80×80
        var house = new Rectangle(rect.X + (w - 80) / 2, rect.Y + (h - 80) / 2, 80, 80);
        spriteDraw(spriteBatch, px, house, wood);
        // Dach (heller, nach oben schmaler)
        var roofTop = new Rectangle(house.X + 6, house.Y + 6, 68, 40);
        spriteDraw(spriteBatch, px, roofTop, roof);
        // Dachschrägen
        for (int i = 0; i < 40; i++)
            spriteDraw(spriteBatch, px, new Rectangle(house.X + 6 + i / 2, house.Y + 46 + i, 68 - i, 2), 
                       i % 8 < 4 ? new Color(190, 180, 150) : new Color(160, 150, 130));
        // Tür
        spriteDraw(spriteBatch, px, new Rectangle(house.X + 34, house.Y + 56, 12, 24), new Color(60, 45, 30));
        // Fenster
        spriteDraw(spriteBatch, px, new Rectangle(house.X + 14, house.Y + 58, 10, 10), new Color(200, 200, 220));
        spriteDraw(spriteBatch, px, new Rectangle(house.X + 56, house.Y + 58, 10, 10), new Color(200, 200, 220));
        
        // Ecktürme (kleine Türme in den 4 Ecken)
        int size = 20;
        var corners = new[]
        {
            new Rectangle(rect.X, rect.Y, size, size),
            new Rectangle(rect.Right - size, rect.Y, size, size),
            new Rectangle(rect.X, rect.Bottom - size, size, size),
            new Rectangle(rect.Right - size, rect.Bottom - size, size, size)
        };
        foreach (var c in corners)
        {
            spriteDraw(spriteBatch, px, c, wood);
            // Kuppel (halbrund, durch Stapeln)
            for (int i = 0; i < 8; i++)
                spriteDraw(spriteBatch, px, new Rectangle(c.X + i, c.Y - i, size - i * 2, 2), roof);
        }
        
        // Aue/Hof vor dem Hauptgebäude (heller Pfad)
        var path = new Rectangle(house.Center.X - 16, house.Bottom, 32, h - house.Bottom + house.Y + 60 - h);
        // Vereinfacht: Pfad von der Tür nach unten
        if (path.Height > 0)
            spriteDraw(spriteBatch, px, new Rectangle(house.Center.X - 8, house.Y + 80, 16, 12), new Color(160, 145, 100));
    }
    
    private void spriteDraw(SpriteBatch sb, Texture2D tex, Rectangle r, Color c)
    {
        if (tex == null) return;
        // Clipping auf ScreenBounds, um GPU-Load zu sparen
        var clipped = Rectangle.Intersect(r, new Rectangle(0, 0, screenBounds.Width, screenBounds.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        // Zeichnen mit Tinting (px-Texture = weißes 1×1-Pixel)
        sb.Draw(tex, clipped, c);
    }
    
    private void DrawUnits(SpriteBatch spriteBatch)
    {
        var margin = new Rectangle(screenBounds.X - 32, screenBounds.Y - 32, screenBounds.Width + 64, screenBounds.Height + 64);

        foreach (var unit in units)
        {
            var screenPos = WorldToScreen(unit.Position);
            var size = (int)(24 * cameraZoom);

            // Culling
            if (!margin.Contains((int)screenPos.X, (int)screenPos.Y))
                continue;

            var tex = unitTex.GetValueOrDefault(unit.Type);
            var tint = unit.OwnerId == 0 ? Color.White : new Color(255, 180, 180);

            // Einheit zeichnen
            if (tex != null)
            {
                spriteBatch.Draw(tex, screenPos - new Vector2(size / 2, size), null, tint, 0f, Vector2.Zero, cameraZoom, SpriteEffects.None, 0f);
            }
            else
            {
                // Fallback: 12x12 Quadrat aus px
                spriteBatch.Draw(px, screenPos - new Vector2(6, 6), null, tint, 0f, Vector2.Zero, 12 * cameraZoom, SpriteEffects.None, 0f);
            }

            // Auswahlring
            if (unit.IsSelected)
            {
                var ringRect = new Rectangle((int)(screenPos.X - size / 2), (int)(screenPos.Y - size / 2), size, size);
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Y, size, 1), null, new Color(80, 255, 80)); // oben
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Y + size - 1, size, 1), null, new Color(80, 255, 80)); // unten
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Y, 1, size), null, new Color(80, 255, 80)); // links
                spriteBatch.Draw(px, new Rectangle(ringRect.X + size - 1, ringRect.Y, 1, size), null, new Color(80, 255, 80)); // rechts
            }

            // Lebensbalken
            if (unit.Health < unit.MaxHealth || unit.IsSelected)
            {
                var barX = screenPos.X - 10;
                var barY = screenPos.Y - size / 2 - 4;

                // Hintergrund
                spriteBatch.Draw(px, new Rectangle((int)barX, (int)barY, 20, 3), null, new Color(20, 20, 20));

                // Gesundheit
                var healthWidth = (int)(20 * ((float)unit.Health / unit.MaxHealth));
                spriteBatch.Draw(px, new Rectangle((int)barX, (int)barY, healthWidth, 3), null, Color.Green);
            }
        }
    }

    private void DrawUI(SpriteBatch spriteBatch)
    {
        if (ScreenManager.Font == null) return;

        // Obere Leiste
        var topBarRect = new Rectangle(0, 0, screenBounds.Width, 34);
        spriteBatch.Draw(px, topBarRect, new Color(94, 76, 48));
        spriteBatch.Draw(px, new Rectangle(0, 32, screenBounds.Width, 2), new Color(52, 40, 24));

        // Ressourcen
        int startX = 10;
        int iconSize = 12;
        int spacing = 10;
        int resourceWidth = 110;

        // Food
        var foodRect = new Rectangle(startX, 6, iconSize, iconSize);
        spriteBatch.Draw(px, foodRect, new Color(190, 70, 55));
        var foodText = player1.Resources[Resource.Food].ToString();
        spriteBatch.DrawString(ScreenManager.Font, foodText, new Vector2(startX + iconSize + spacing, 6), Color.White);

        // Wood
        var woodRect = new Rectangle(startX + resourceWidth, 6, iconSize, iconSize);
        spriteBatch.Draw(px, woodRect, new Color(130, 95, 55));
        var woodText = player1.Resources[Resource.Wood].ToString();
        spriteBatch.DrawString(ScreenManager.Font, woodText, new Vector2(startX + resourceWidth + iconSize + spacing, 6), Color.White);

        // Gold
        var goldRect = new Rectangle(startX + resourceWidth * 2, 6, iconSize, iconSize);
        spriteBatch.Draw(px, goldRect, new Color(215, 180, 60));
        var goldText = player1.Resources[Resource.Gold].ToString();
        spriteBatch.DrawString(ScreenManager.Font, goldText, new Vector2(startX + resourceWidth * 2 + iconSize + spacing, 6), Color.White);

        // Stone
        var stoneRect = new Rectangle(startX + resourceWidth * 3, 6, iconSize, iconSize);
        spriteBatch.Draw(px, stoneRect, new Color(160, 160, 165));
        var stoneText = player1.Resources[Resource.Stone].ToString();
        spriteBatch.DrawString(ScreenManager.Font, stoneText, new Vector2(startX + resourceWidth * 3 + iconSize + spacing, 6), Color.White);

        // Bevölkerung und Zeitalter
        var popText = $"Bev. {player1.PopulationCount}/{player1.PopulationLimit}";
        var ageText = player1.CurrentAge.ToString();
        var popSize = ScreenManager.Font.MeasureString(popText);
        var ageSize = ScreenManager.Font.MeasureString(ageText);
        var textX = screenBounds.Width - 10 - popSize.X - ageSize.X - 10;
        spriteBatch.DrawString(ScreenManager.Font, popText, new Vector2(textX, 6), Color.White);
        spriteBatch.DrawString(ScreenManager.Font, ageText, new Vector2(textX + popSize.X + 10, 6), Color.White);

        // Untere Leiste
        var bottomBarRect = new Rectangle(0, screenBounds.Height - 48, screenBounds.Width, 48);
        spriteBatch.Draw(px, bottomBarRect, new Color(94, 76, 48));
        spriteBatch.Draw(px, new Rectangle(0, screenBounds.Height - 50, screenBounds.Width, 2), new Color(140, 115, 75));

        // Text in unterer Leiste
        string commandText;
        if (selectedUnits.Count > 0)
        {
            commandText = selectedUnits.Count == 1 ? "1 Einheit ausgewählt" : $"{selectedUnits.Count} Einheiten ausgewählt";
        }
        else
        {
            commandText = "Links: auswählen   Rechts: bewegen   Pfeiltasten: Kamera   Mausrad/+-: Zoom   ESC: Menü";
        }
        var textPos = new Vector2(10, screenBounds.Height - 38);

        spriteBatch.DrawString(ScreenManager.Font, commandText, textPos, Color.White);
    }

    private Texture2D BuildArcherTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
    
        // Körper
        int bodyX = 7, bodyY = 6, bodyW = 10, bodyH = 14;
        b.FillRect(bodyX, bodyY, bodyW, bodyH, new Color(225, 190, 150)); // Haut
        b.OutlineRect(bodyX, bodyY, bodyW, bodyH, new Color(40, 30, 25)); // Umriss
    
        // Tunika über dem Unterkörper (Haut bleibt nur als Kopf/Arme sichtbar)
        b.FillRect(bodyX + 1, bodyY + 4, bodyW - 2, bodyH - 5, playerColor);
    
        // Kapuze
        b.FillRect(bodyX - 1, bodyY - 2, bodyW + 2, 3, playerColor);
    
        // Bogen (vertikaler Halbbogen links)
        int bowX = 2, bowY = 6;
        b.FillCircle(bowX, bowY + 5, 4, new Color(120, 80, 40)); // Bogen
        b.FillRect(bowX - 1, bowY + 3, 1, 5, new Color(220, 210, 190)); // Sehne
    
        // Schatten
        b.FillRect(6, 21, 12, 2, new Color(0, 0, 0, 70));
    
        return b.Build(gd);
    }

    private Texture2D BuildCavalryTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
    
        // Pferd
        int horseX = 3, horseY = 8, horseW = 16, horseH = 8;
        b.FillRect(horseX, horseY, horseW, horseH, new Color(110, 75, 50)); // Pferdekörper
        b.OutlineRect(horseX, horseY, horseW, horseH, new Color(40, 30, 25)); // Umriss
    
        // Beine
        b.FillRect(horseX + 1, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 5, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 9, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 13, horseY + 7, 2, 2, new Color(110, 75, 50));
    
        // Kopf
        b.FillRect(horseX + 16, horseY + 4, 3, 3, new Color(110, 75, 50));
    
        // Schweif
        b.FillRect(horseX - 1, horseY + 6, 2, 1, new Color(110, 75, 50));
    
        // Reiter
        int riderX = 9, riderY = 4;
        b.FillRect(riderX, riderY, 6, 4, playerColor); // Oberkörper
        b.FillRect(riderX + 2, riderY - 1, 2, 2, new Color(150, 150, 160)); // Helm
    
        // Schatten
        b.FillRect(4, 21, 16, 2, new Color(0, 0, 0, 70));
    
        return b.Build(gd);
    }
    
    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        return new Vector2(
            (worldPos.X + cameraPosition.X) * cameraZoom,
            (worldPos.Y + cameraPosition.Y) * cameraZoom);
    }
    
    /// <summary>
    /// Hält den sichtbaren Ausschnitt innerhalb der Karte.
    ///
    /// Aus <c>WorldToScreen(w) = (w + cameraPosition) * cameraZoom</c> folgt,
    /// dass der sichtbare Weltbereich bei <c>-cameraPosition</c> beginnt und
    /// <c>screenBounds / cameraZoom</c> breit ist. Daraus ergeben sich die
    /// Grenzen unten.
    ///
    /// Ist die Karte kleiner als das Fenster — bei 64×32 = 2048 Pixeln und
    /// Zoom 1 der Normalfall —, wird sie stattdessen zentriert. Ohne das
    /// klebte die Karte in einer Ecke und der Rest blieb leere Fläche.
    /// </summary>
    private void ClampCamera()
    {
        float mapWidth = tileMap.Width * tileMap.TileSize;
        float mapHeight = tileMap.Height * tileMap.TileSize;

        float viewWidth = screenBounds.Width / cameraZoom;
        float viewHeight = screenBounds.Height / cameraZoom;

        cameraPosition.X = viewWidth >= mapWidth
            ? (viewWidth - mapWidth) / 2f
            : MathHelper.Clamp(cameraPosition.X, viewWidth - mapWidth, 0f);

        cameraPosition.Y = viewHeight >= mapHeight
            ? (viewHeight - mapHeight) / 2f
            : MathHelper.Clamp(cameraPosition.Y, viewHeight - mapHeight, 0f);
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
