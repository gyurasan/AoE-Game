using System;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// Main menu screen for the Age of Evolutions RTS game
/// </summary>
public class MainMenuScreen : GameScreen
{
    Texture2D menuBackground;
    Texture2D menuButton;
    SoundEffect soundButton;
    string[] menuOptions = { "Neues Spiel", "Spiel laden", "Einstellungen", "Beenden" };
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

        // Navigate menu
        if (WasPressed(keyboard, prevKeyboard, Keys.Down))
        {
            selectedIndex = (selectedIndex + 1) % menuOptions.Length;
            soundButton?.Play();
        }
        else if (WasPressed(keyboard, prevKeyboard, Keys.Up))
        {
            selectedIndex = (selectedIndex - 1 + menuOptions.Length) % menuOptions.Length;
            soundButton?.Play();
        }
        else if (WasPressed(keyboard, prevKeyboard, Keys.Enter) || WasPressed(keyboard, prevKeyboard, Keys.Space))
        {
            HandleMenuSelect();
        }
    }

    private static bool WasPressed(KeyboardState current, KeyboardState previous, Keys key)
    {
        return current.IsKeyDown(key) && previous.IsKeyUp(key);
    }

    private void HandleMenuSelect()
    {
        soundButton?.Play();

        switch (selectedIndex)
        {
            case 0: // New Game
                // Via LoadingScreen so the menu transitions off before the map is built.
                LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen());
                break;
            case 1: // Load Game
                // TODO: Load game functionality
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
        var font = ScreenManager.Font;
        var width = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight;

        spriteBatch.Begin();

        DrawBackground(spriteBatch, width, height);

        // Draw title with golden texture effect
        var title = "Age of Evolutions";
        var titlePosition = new Vector2(
            width / 2f - font.MeasureString(title).X / 2f,
            60f);
        spriteBatch.DrawString(font, title, titlePosition + new Vector2(3, 3), Color.Black);
        spriteBatch.DrawString(font, title, titlePosition, new Color(255, 215, 0));

        // Draw age indicator
        var ageText = "Zeitalter: Dunkle Zeit";
        var agePosition = new Vector2(
            width / 2f - font.MeasureString(ageText).X / 2f,
            110f);
        spriteBatch.DrawString(font, ageText, agePosition + new Vector2(2, 2), Color.Black);
        spriteBatch.DrawString(font, ageText, agePosition, Color.Gray);

        // Draw menu options
        var menuY = 220f;
        for (int i = 0; i < menuOptions.Length; i++)
        {
            var text = menuOptions[i];
            var textSize = font.MeasureString(text);
            var position = new Vector2(width / 2f - textSize.X / 2f, menuY + i * 45f);

            // Every entry gets a plate so the text stays readable over the artwork;
            // the selected one is lighter and gets a golden border.
            var plate = new Rectangle((int)position.X - 20, (int)position.Y - 10,
                                      (int)textSize.X + 40, 45);
            spriteBatch.Draw(menuButton, plate, i == selectedIndex
                ? new Color(100, 60, 30) * 0.9f
                : new Color(30, 20, 10) * 0.75f);

            if (i == selectedIndex)
                DrawBorder(spriteBatch, plate, 2, new Color(255, 215, 0));

            // Draw text
            var color = i == selectedIndex ? new Color(255, 215, 0) : Color.LightGray;
            spriteBatch.DrawString(font, text, position + new Vector2(1, 1), Color.Black);
            spriteBatch.DrawString(font, text, position, color);
        }

        // Draw footer with controls info
        var footer = "Pfeiltasten (Auswahl) | ENTER (ausfuehren) | ESC (zurueck)";
        var footerPosition = new Vector2(
            width / 2f - font.MeasureString(footer).X / 2f,
            height - 35f);
        spriteBatch.DrawString(font, footer, footerPosition, new Color(200, 200, 200));

        spriteBatch.End();

        // Fade the screen in/out while transitioning.
        if (TransitionPosition > 0)
            ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
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
