using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// Der gemeinsame Hintergrund aller Menüs außerhalb des Spiels: das Menübild
/// (Dorfplatz, Qwen-Image, bilder.json: menu/hintergrund), fensterfüllend und
/// leicht abgedunkelt, damit Tafeln und Schrift sich abheben. Er bleibt stehen,
/// während Hauptmenü, „Einstellungen“ und „Spiel laden“ darüber wechseln.
/// Vorher lag hier noch das blaue Gebirge aus der Platformer-Vorlage
/// (Layer0_2), ins 800×480-Raster gestreckt mit schwarzen Rändern - nur das
/// Hauptmenü verdeckte es mit seinem eigenen Bild.
/// </summary>
class BackgroundScreen : GameScreen
{
    // Abdunklung über dem Bild; vorher im Hauptmenü, jetzt für alle Menüs gleich
    private const float DIM = 0.3f;

    // Dunkles Pergament: Grund unter dem Bild und Ersatz, falls es fehlt
    private static readonly Color Parchment = new Color(69, 46, 24);

    ContentManager content;
    Texture2D backgroundTexture;

    /// <summary>
    /// Initializes a new instance of the BackgroundScreen class.
    /// Sets the transition times for screen appearance and disappearance.
    /// </summary>
    public BackgroundScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(0.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    /// <summary>
    /// Lädt das Menübild über einen eigenen ContentManager, damit es mit dem
    /// Hintergrund wieder entladen wird. Fehlt es, bleibt das Pergament -
    /// das Spiel startet trotzdem.
    /// </summary>
    public override void LoadContent()
    {
        if (content == null)
            content = new ContentManager(ScreenManager.Game.Services, "Content");

        try
        {
            backgroundTexture = content.Load<Texture2D>("Backgrounds/menu");
        }
        catch
        {
            backgroundTexture = null;
        }
    }

    /// <summary>
    /// Unloads the background texture by unloading the local ContentManager.
    /// </summary>
    public override void UnloadContent()
    {
        content.Unload();
    }

    /// <summary>
    /// Updates the background screen.
    /// Forces the screen to remain active even when covered by other screens.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="otherScreenHasFocus">Indicates whether another screen has focus.</param>
    /// <param name="coveredByOtherScreen">Indicates whether the screen is covered by another screen.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        // Force coveredByOtherScreen to false to prevent the screen from transitioning off.
        base.Update(gameTime, otherScreenHasFocus, false);
    }

    /// <summary>
    /// Zeichnet das Menübild (16:9) fensterfüllend im eigenen Seitenverhältnis,
    /// in echten Fensterpixeln: was über den Rand ragt, wird abgeschnitten - die
    /// Mitte mit dem Dorfplatz bleibt immer sichtbar, und es gibt keine leeren
    /// Balken. Beim Ein- und Ausblenden geht alles gemeinsam aus dem Schwarz.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        ScreenManager.GraphicsDevice.Clear(ClearOptions.Target, Color.Black, 0, 0);

        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        int width = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
        int height = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
        Color tint = new Color(TransitionAlpha, TransitionAlpha, TransitionAlpha);

        // Ohne GlobalTransformation: Fensterpixel statt 800×480-Raster
        spriteBatch.Begin();

        spriteBatch.Draw(ScreenManager.WhitePixel, new Rectangle(0, 0, width, height), Parchment * TransitionAlpha);

        if (backgroundTexture != null)
        {
            float scale = Math.Max((float)width / backgroundTexture.Width,
                                   (float)height / backgroundTexture.Height);
            int w = (int)(backgroundTexture.Width * scale);
            int h = (int)(backgroundTexture.Height * scale);
            spriteBatch.Draw(backgroundTexture, new Rectangle((width - w) / 2, (height - h) / 2, w, h), tint);
        }

        spriteBatch.Draw(ScreenManager.WhitePixel, new Rectangle(0, 0, width, height), Color.Black * DIM);

        spriteBatch.End();
    }
}
