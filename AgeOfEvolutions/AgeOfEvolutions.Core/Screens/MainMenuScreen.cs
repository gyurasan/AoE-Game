using System;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// Hauptmenü: Titel und Einträge auf einer Tafel in der Bildmitte, bedienbar
/// mit der Maus (Zeigen wählt, Klick startet) und mit der Tastatur
/// (Pfeiltasten, Enter). Alle Maße sind für 1080 px Fensterhöhe entworfen und
/// wachsen mit dem Fenster - vorher standen sie in festen Pixeln und
/// schrumpften bei hoher Auflösung zu einem Fleck oben in der Mitte, und die
/// Maus wurde gar nicht ausgewertet.
/// </summary>
public class MainMenuScreen : GameScreen
{
    // Entwurfshöhe der Maße unten; UiScale rechnet sie auf das Fenster um
    private const float DESIGN_HEIGHT = 1080f;
    private const int PANEL_WIDTH = 560, PANEL_TOP = 170;
    private const int ENTRY_WIDTH = 440, ENTRY_HEIGHT = 64, ENTRY_STEP = 84, ENTRY_TOP = 330;

    Texture2D menuBackground;
    Texture2D menuButton;
    SpriteFont menuFont;
    SoundEffect soundButton;

    // „Spiel laden" gibt es noch nicht: grau und nicht wählbar statt eines
    // Eintrags, der beim Klick nichts tut
    readonly (string Text, bool Enabled)[] menuOptions =
    {
        ("Neues Spiel", true),
        ("Spiel laden", false),
        ("Einstellungen", true),
        ("Beenden", true),
    };
    int selectedIndex = 0;

    public MainMenuScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(1.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    public override void LoadContent()
    {
        base.LoadContent();

        var content = ScreenManager.Game.Content;

        // Menu art, converted from menu.webp to Content/Backgrounds/menu.png.
        // Everything is loaded defensively: a missing asset degrades to the plain
        // parchment look rather than taking the whole game down on startup.
        menuBackground = TryLoad<Texture2D>(content, "Backgrounds/menu");
        soundButton = TryLoad<SoundEffect>(content, "Sounds/PlayerGemCollected");

        // Eigene, große Schrift: die HUD-Schrift (14 pt) würde auf Menügröße
        // gestreckt unscharf. Fehlt sie, tut es die HUD-Schrift.
        menuFont = TryLoad<SpriteFont>(content, "Fonts/Menu") ?? ScreenManager.Font;

        // 1x1 white texture, tinted per draw call for panels, plates and bars.
        menuButton = new Texture2D(ScreenManager.GraphicsDevice, 1, 1);
        menuButton.SetData(new[] { Color.White });
    }

    private static T TryLoad<T>(Microsoft.Xna.Framework.Content.ContentManager content, string asset)
        where T : class
    {
        try
        {
            return content.Load<T>(asset);
        }
        catch
        {
            return null;
        }
    }

    public override void UnloadContent()
    {
        menuButton?.Dispose();
        menuButton = null;
    }

    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        var keyboard = inputState.CurrentKeyboardStates[0];
        var prevKeyboard = inputState.LastKeyboardStates[0];
        var buffer = ScreenManager.GraphicsDevice.PresentationParameters;

        // Maus: Zeigen wählt, Loslassen über einem Eintrag startet ihn
        var (selected, activate) = EvaluateMouse(inputState.CurrentMouseState, inputState.LastMouseState,
                                                 buffer.BackBufferWidth, buffer.BackBufferHeight);
        if (selected != selectedIndex)
        {
            selectedIndex = selected;
            soundButton?.Play();
        }
        if (activate)
        {
            HandleMenuSelect();
            return;
        }

        // Tastatur
        if (WasPressed(keyboard, prevKeyboard, Keys.Down))
            MoveSelection(1);
        else if (WasPressed(keyboard, prevKeyboard, Keys.Up))
            MoveSelection(-1);
        else if (WasPressed(keyboard, prevKeyboard, Keys.Enter) || WasPressed(keyboard, prevKeyboard, Keys.Space))
            HandleMenuSelect();
    }

    private static bool WasPressed(KeyboardState current, KeyboardState previous, Keys key)
    {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }

    /// <summary>Auswahl um <paramref name="step"/> weiter, über gesperrte Einträge hinweg.</summary>
    private void MoveSelection(int step)
    {
        do
            selectedIndex = (selectedIndex + step + menuOptions.Length) % menuOptions.Length;
        while (!menuOptions[selectedIndex].Enabled);
        soundButton?.Play();
    }

    /// <summary>
    /// Auswertung der Maus für ein Bild, ohne Nebenwirkung - tools/spielablauf
    /// prüft sie direkt. Über einem aktiven Eintrag wählt eine Bewegung ihn aus,
    /// das Loslassen der linken Taste startet ihn; eine ruhende Maus überstimmt
    /// die Tastaturauswahl nicht. Rückgabe: die neue Auswahl und ob sie
    /// gestartet wird.
    /// </summary>
    private (int Selected, bool Activate) EvaluateMouse(MouseState current, MouseState last, int width, int height)
    {
        int hit = EntryAt(current.Position, width, height);
        if (hit < 0 || !menuOptions[hit].Enabled)
            return (selectedIndex, false);
        bool moved = current.Position != last.Position;
        bool released = current.LeftButton == ButtonState.Released && last.LeftButton == ButtonState.Pressed;
        return (moved || released ? hit : selectedIndex, released);
    }

    /// <summary>Der Eintrag unter dem Bildschirmpunkt, oder -1.</summary>
    private int EntryAt(Point p, int width, int height)
    {
        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (EntryRect(i, width, height).Contains(p))
                return i;
        }
        return -1;
    }

    private static float UiScale(int height) => height / DESIGN_HEIGHT;

    /// <summary>Platte eines Eintrags im Fenster - dieselbe zum Zeichnen und Klicken.</summary>
    private static Rectangle EntryRect(int index, int width, int height)
    {
        float ui = UiScale(height);
        int w = (int)(ENTRY_WIDTH * ui), h = (int)(ENTRY_HEIGHT * ui);
        return new Rectangle((width - w) / 2, (int)((ENTRY_TOP + index * ENTRY_STEP) * ui), w, h);
    }

    private void HandleMenuSelect()
    {
        if (!menuOptions[selectedIndex].Enabled)
            return;
        soundButton?.Play();

        switch (selectedIndex)
        {
            case 0: // New Game
                // Via LoadingScreen so the menu transitions off before the map is built.
                LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen());
                break;
            case 2: // Settings
                ScreenManager.AddScreen(new SettingsScreen(), ControllingPlayer);
                break;
            case 3: // Exit
                ScreenManager.Game.Exit();
                break;
        }
    }

    public override void Draw(GameTime gameTime)
    {
        var spriteBatch = ScreenManager.SpriteBatch;
        var width = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
        float ui = UiScale(height);
        int shadow = Math.Max(1, (int)(2 * ui));

        spriteBatch.Begin();

        DrawBackground(spriteBatch, width, height);

        // Die Hintergrundgrafik malt eigene Schaltflächen - abgedunkelt drängen
        // sie sich nicht mehr vor das echte Menü
        spriteBatch.Draw(menuButton, new Rectangle(0, 0, width, height), Color.Black * 0.45f);

        // Tafel hinter Titel und Einträgen
        int panelWidth = (int)(PANEL_WIDTH * ui);
        int panelTop = (int)(PANEL_TOP * ui);
        int panelBottom = EntryRect(menuOptions.Length - 1, width, height).Bottom + (int)(36 * ui);
        var panel = new Rectangle((width - panelWidth) / 2, panelTop, panelWidth, panelBottom - panelTop);
        spriteBatch.Draw(menuButton, panel, new Color(30, 20, 10) * 0.88f);
        DrawBorder(spriteBatch, panel, Math.Max(2, (int)(3 * ui)), new Color(180, 140, 60));

        DrawCentered(spriteBatch, "Age of Evolutions", new Vector2(width / 2f, panelTop + 78 * ui),
                     72 * ui, new Color(255, 215, 0), Math.Max(2, (int)(3 * ui)));

        for (int i = 0; i < menuOptions.Length; i++)
        {
            var (text, enabled) = menuOptions[i];
            var plate = EntryRect(i, width, height);
            bool selected = i == selectedIndex;

            spriteBatch.Draw(menuButton, plate, selected
                ? new Color(100, 60, 30) * 0.95f
                : new Color(55, 38, 20) * 0.9f);
            if (selected)
                DrawBorder(spriteBatch, plate, Math.Max(2, (int)(2 * ui)), new Color(255, 215, 0));

            var color = !enabled ? new Color(120, 110, 95)
                      : selected ? new Color(255, 215, 0)
                                 : Color.LightGray;
            DrawCentered(spriteBatch, text, plate.Center.ToVector2(), 40 * ui, color, shadow);
        }

        DrawCentered(spriteBatch, "Maus oder Pfeiltasten: wählen   Klick oder Enter: starten",
                     new Vector2(width / 2f, height - 36 * ui), 24 * ui, new Color(220, 210, 190), shadow);

        spriteBatch.End();

        // Fade the screen in/out while transitioning.
        if (TransitionPosition > 0)
            ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
    }

    /// <summary>
    /// Text mit Schatten, mittig auf <paramref name="center"/> und
    /// <paramref name="pixelHeight"/> Pixel hoch - die Schrift wird dafür
    /// skaliert, gleich ob die Menü- oder die HUD-Schrift geladen ist.
    /// </summary>
    private void DrawCentered(SpriteBatch spriteBatch, string text, Vector2 center, float pixelHeight,
                              Color color, int shadow)
    {
        float scale = pixelHeight / menuFont.LineSpacing;
        var size = menuFont.MeasureString(text) * scale;
        var position = center - size / 2f;
        spriteBatch.DrawString(menuFont, text, position + new Vector2(shadow, shadow), Color.Black,
                               0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(menuFont, text, position, color,
                               0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Draws the menu art letterboxed so the 4:3 original keeps its aspect ratio
    /// on the 16:10 window instead of being stretched.
    /// </summary>
    private void DrawBackground(SpriteBatch spriteBatch, int width, int height)
    {
        // Dark parchment base, also the fallback if the artwork is missing.
        spriteBatch.Draw(menuButton, new Rectangle(0, 0, width, height), new Color(69, 46, 24));

        if (menuBackground == null)
            return;

        float scale = Math.Min((float)width / menuBackground.Width,
                               (float)height / menuBackground.Height);
        int w = (int)(menuBackground.Width * scale);
        int h = (int)(menuBackground.Height * scale);

        spriteBatch.Draw(menuBackground,
            new Rectangle((width - w) / 2, (height - h) / 2, w, h),
            Color.White);
    }

    private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, int thickness, Color color)
    {
        spriteBatch.Draw(menuButton, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
        spriteBatch.Draw(menuButton, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
        spriteBatch.Draw(menuButton, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
        spriteBatch.Draw(menuButton, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
    }
}
