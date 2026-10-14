using System;
using AgeOfEvolutions.ScreenManagers;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// Gemeinsames Aussehen der Menüs im Stil des Hauptmenüs: dunkle Tafel mit
/// goldenem Rand, Schrift mit Schatten. Gezeichnet wird in echten
/// Fensterpixeln (SpriteBatch ohne GlobalTransformation) mit der großen
/// Menüschrift; die Maße der Aufrufer sind für 1080 px Fensterhöhe entworfen
/// und wachsen über <see cref="UiScale"/> mit dem Fenster.
/// </summary>
static class MenuStyle
{
    public const float DESIGN_HEIGHT = 1080f;

    public static readonly Color PanelFill = new Color(30, 20, 10) * 0.88f;
    public static readonly Color PanelBorder = new Color(180, 140, 60);
    public static readonly Color Plate = new Color(55, 38, 20) * 0.9f;
    public static readonly Color PlateSelected = new Color(100, 60, 30) * 0.95f;
    public static readonly Color Gold = new Color(255, 215, 0);
    public static readonly Color Text = Color.LightGray;
    public static readonly Color TextDisabled = new Color(120, 110, 95);

    /// <summary>Faktor von Entwurfspixeln (1080 px hoch) auf das Fenster.</summary>
    public static float UiScale(int windowHeight) => windowHeight / DESIGN_HEIGHT;

    /// <summary>Breite von <paramref name="text"/> bei <paramref name="pixelHeight"/> Pixel Zeilenhöhe.</summary>
    public static float TextWidth(ScreenManager screenManager, string text, float pixelHeight)
    {
        SpriteFont font = screenManager.MenuFontFor(pixelHeight);
        return font.MeasureString(text).X * pixelHeight / font.LineSpacing;
    }

    /// <summary>
    /// Text mit Schatten, mittig auf <paramref name="center"/>, jede Zeile
    /// <paramref name="pixelHeight"/> Pixel hoch. Die Lage wird auf ganze Pixel
    /// gerundet - ein halber Pixel verschmiert die Schrift zusätzlich.
    /// </summary>
    public static void DrawText(ScreenManager screenManager, string text, Vector2 center, float pixelHeight,
                                Color color, float alpha, int shadow)
    {
        SpriteFont font = screenManager.MenuFontFor(pixelHeight);
        float scale = pixelHeight / font.LineSpacing;
        Vector2 size = font.MeasureString(text) * scale;
        Vector2 position = new Vector2(MathF.Round(center.X - size.X / 2f), MathF.Round(center.Y - size.Y / 2f));

        SpriteBatch spriteBatch = screenManager.SpriteBatch;
        spriteBatch.DrawString(font, text, position + new Vector2(shadow, shadow), Color.Black * alpha,
                               0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, text, position, color * alpha,
                               0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
    }

    /// <summary>Gefülltes Rechteck mit Rand; <paramref name="thickness"/> 0 lässt den Rand weg.</summary>
    public static void DrawPanel(ScreenManager screenManager, Rectangle rect, Color fill, Color border,
                                 int thickness, float alpha)
    {
        SpriteBatch spriteBatch = screenManager.SpriteBatch;
        Texture2D pixel = screenManager.WhitePixel;

        spriteBatch.Draw(pixel, rect, fill * alpha);
        if (thickness <= 0)
            return;

        Color edge = border * alpha;
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), edge);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), edge);
        spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), edge);
        spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), edge);
    }
}
