using System;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// End-of-game screen: zeigt, wer gewonnen und wer verloren hat, und führt
/// per Enter/Klick zurück zum Hauptmenü. <see cref="RTSGameplayScreen"/>
/// zeigt ihn, sobald ein Spieler weder Dorfbewohner noch Gebäude mehr hat.
/// </summary>
public class GameOverScreen : GameScreen
{
    private readonly string title;
    private readonly string detail;

    public GameOverScreen(string title, string detail)
    {
        this.title = string.IsNullOrEmpty(title) ? "Spiel beendet" : title;
        this.detail = detail ?? string.Empty;
        TransitionOnTime = TimeSpan.FromSeconds(0.6);
        TransitionOffTime = TimeSpan.FromSeconds(0.4);
    }

    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        if (inputState == null) return;

        PlayerIndex playerIndex;
        // „Auswahl" oder „Abbrechen" (Enter/Esc) beendet das Spiel und
        // geht zurück ins Hauptmenü.
        if (inputState.IsMenuSelect(ControllingPlayer, out playerIndex)
            || inputState.IsMenuCancel(ControllingPlayer, out playerIndex))
        {
            ReturnToMainMenu();
        }
    }

    private void ReturnToMainMenu()
    {
        // Wie der Quit-Pfad in PauseScreen: das aktuelle Spiel wird komplett
        // beendet und das Hauptmenü geladen.
        LoadingScreen.Load(ScreenManager, false, null, new BackgroundScreen(), new MainMenuScreen());
    }

    public override void Draw(GameTime gameTime)
    {
        if (ScreenManager.Font == null) return;

        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        SpriteFont font = ScreenManager.Font;

        // Andere Screens darunter abdunkeln.
        ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2f / 3f);

        // Titel groß in der Bildmitte, darunter die Erklärung, unten
        // der Hinweis auf die Steuerung.
        Vector2 baseSize = ScreenManager.BaseScreenSize;
        Vector2 titleSize = font.MeasureString(title) * 2f;
        Vector2 titlePos = (baseSize - titleSize) / 2f;
        titlePos.Y -= 60f;

        Vector2 detailSize = font.MeasureString(detail);
        Vector2 detailPos = (baseSize - detailSize) / 2f;
        detailPos.Y = titlePos.Y + titleSize.Y + 20f;

        string hint = "Enter oder Klick — zurück zum Menü";
        Vector2 hintSize = font.MeasureString(hint) * 1.1f;
        Vector2 hintPos = (baseSize - hintSize) / 2f;
        hintPos.Y = baseSize.Y * 0.84f;

        var titleCol = new Color(240, 240, 240) * TransitionAlpha;
        var detailCol = new Color(200, 200, 200) * TransitionAlpha;
        var hintCol = new Color(160, 160, 160) * TransitionAlpha;

        spriteBatch.Begin(SpriteSortMode.Deferred, null, null, null, null, null, ScreenManager.GlobalTransformation);
        spriteBatch.DrawString(font, title, titlePos, titleCol, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
        if (detail.Length > 0)
            spriteBatch.DrawString(font, detail, detailPos, detailCol, 0f, Vector2.Zero, 1f, SpriteEffects.None, 0f);
        spriteBatch.DrawString(font, hint, hintPos, hintCol, 0f, Vector2.Zero, 1.1f, SpriteEffects.None, 0f);
        spriteBatch.End();
    }
}