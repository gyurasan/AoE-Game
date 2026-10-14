using System;
using AgeOfEvolutions.Core;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Core.Localization;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// A popup message box screen, used to display messages and prompt user input.
/// </summary>
/// <remarks>
/// Gezeichnet wie die Menüs (<see cref="MenuStyle"/>) in echten Fensterpixeln;
/// vorher streckte die GlobalTransformation die 14-pt-HUD-Schrift unscharf.
/// </remarks>
class MessageBoxScreen : GameScreen
{
    private string message;
    private readonly bool toastMessage;
    private readonly TimeSpan toastDuration;
    private TimeSpan toastTimer;
    private Rectangle yesButtonRect;
    private Rectangle noButtonRect;
    private Vector2 messageCenter;
    private Rectangle backgroundRectangle;
    private float ui = 1f;

    /// <summary>
    /// Event raised when the user accepts the message box.
    /// </summary>
    public event EventHandler<PlayerIndexEventArgs> Accepted;

    /// <summary>
    /// Event raised when the user cancels the message box.
    /// </summary>
    public event EventHandler<PlayerIndexEventArgs> Cancelled;

    // Entwurfsmaße für 1080 px Fensterhöhe; MenuStyle.UiScale rechnet sie um.
    // Der Rahmen ist etwas größer als der Text selbst.
    private const float TEXT_HEIGHT = 32f;
    private const float H_PAD = 40f;
    private const float V_PAD = 28f;
    private const float BUTTON_GAP = 32f;

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBoxScreen"/> class, automatically including usage text.
    /// </summary>
    /// <param name="message">The message to display.</param>
    public MessageBoxScreen(string message)
        : this(message, true, TimeSpan.Zero) { }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessageBoxScreen"/> class, allowing customization.
    /// </summary>
    /// <param name="message">The message to display.</param>
    /// <param name="includeUsageText">Indicates whether to include usage text.</param>
    /// <param name="toastDuration">The duration for toast messages.</param>
    /// <param name="toastMessage">Indicates whether this is a toast message.</param>
    public MessageBoxScreen(string message, bool includeUsageText, TimeSpan toastDuration, bool toastMessage = false)
    {
        string usageText = $"{Environment.NewLine}{Environment.NewLine}{Resources.YesButtonHelp}{Environment.NewLine}{Resources.NoButtonHelp}";

        if (includeUsageText && !toastMessage)
            this.message = message + usageText;
        else
            this.message = message;

        this.toastMessage = toastMessage;
        this.toastDuration = toastDuration;
        this.toastTimer = TimeSpan.Zero;

        IsPopup = true;

        TransitionOnTime = TimeSpan.FromSeconds(0.2);
        TransitionOffTime = TimeSpan.FromSeconds(0.2);
    }

    /// <summary>Ob die Ja/Nein-Knöpfe zum Antippen gezeigt werden (nur mobil).</summary>
    private bool ShowsButtons => AgeOfEvolutionsGame.IsMobile && !toastMessage;

    /// <summary>
    /// Responds to user input, accepting or cancelling the message box.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="inputState">The current input state.</param>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        base.HandleInput(gameTime, inputState);

        // Ignore input if this is a ToastMessage
        if (toastMessage)
        {
            return;
        }

        PlayerIndex playerIndex;

        // We pass in our ControllingPlayer, which may either be null (to
        // accept input from any player) or a specific index. If we pass a null
        // controlling player, the InputState helper returns to us which player
        // actually provided the input. We pass that through to our Accepted and
        // Cancelled events, so they can tell which player triggered them.
        if (inputState.IsMenuSelect(ControllingPlayer, out playerIndex)
            || (ShowsButtons && IsClicked(inputState, yesButtonRect)))
        {
            // Raise the accepted event, then exit the message box.
            Accepted?.Invoke(this, new PlayerIndexEventArgs(playerIndex));

            ExitScreen();
        }
        else if (inputState.IsMenuCancel(ControllingPlayer, out playerIndex)
            || (ShowsButtons && IsClicked(inputState, noButtonRect)))
        {
            // Raise the cancelled event, then exit the message box.
            Cancelled?.Invoke(this, new PlayerIndexEventArgs(playerIndex));

            ExitScreen();
        }
    }

    /// <summary>
    /// Klick oder Berührung in <paramref name="rect"/>, beides in Fensterpixeln
    /// (InputState.IsUIClicked rechnet ins 800×480-Raster zurück).
    /// </summary>
    private static bool IsClicked(InputState inputState, Rectangle rect)
    {
        if (inputState.IsLeftMouseButtonClicked() && rect.Contains(inputState.CurrentMouseState.Position))
            return true;

        foreach (var touch in inputState.CurrentTouchState)
        {
            if (touch.State == TouchLocationState.Pressed && rect.Contains(touch.Position.ToPoint()))
                return true;
        }
        return false;
    }

    /// <summary>
    /// Updates the screen, particularly for toast messages and positioning.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="otherScreenHasFocus">Indicates whether another screen has focus.</param>
    /// <param name="coveredByOtherScreen">Indicates whether the screen is covered by another screen.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        // Handle toast duration countdown.
        if (toastMessage)
        {
            toastTimer += gameTime.ElapsedGameTime;
            if (toastTimer >= toastDuration)
            {
                // Raise the accepted event, then exit the message box.
                Accepted?.Invoke(this, new PlayerIndexEventArgs(PlayerIndex.One));

                // Exit the screen when the toast time has elapsed.
                ExitScreen();
            }
        }

        // Kasten mittig im Fenster, in Fensterpixeln. Jedes Bild neu, weil
        // sich Fenstergröße und Sprache ändern können.
        var parameters = ScreenManager.GraphicsDevice.PresentationParameters;
        int width = parameters.BackBufferWidth;
        int height = parameters.BackBufferHeight;
        ui = MenuStyle.UiScale(height);

        float lineHeight = TEXT_HEIGHT * ui;
        SpriteFont font = ScreenManager.MenuFontFor(lineHeight);
        Vector2 textSize = font.MeasureString(message) * (lineHeight / font.LineSpacing);

        float yesWidth = MenuStyle.TextWidth(ScreenManager, Resources.YesButtonText, lineHeight);
        float noWidth = MenuStyle.TextWidth(ScreenManager, Resources.NoButtonText, lineHeight);
        float contentWidth = textSize.X;
        float contentHeight = textSize.Y;
        if (ShowsButtons)
        {
            contentWidth = Math.Max(contentWidth, yesWidth + BUTTON_GAP * ui + noWidth);
            contentHeight += V_PAD * ui + lineHeight;
        }

        int boxWidth = (int)(contentWidth + 2 * H_PAD * ui);
        int boxHeight = (int)(contentHeight + 2 * V_PAD * ui);
        backgroundRectangle = new Rectangle((width - boxWidth) / 2, (height - boxHeight) / 2, boxWidth, boxHeight);
        messageCenter = new Vector2(width / 2f, backgroundRectangle.Y + V_PAD * ui + textSize.Y / 2f);

        if (ShowsButtons)
        {
            int buttonsTop = (int)(backgroundRectangle.Bottom - V_PAD * ui - lineHeight);
            noButtonRect = new Rectangle((int)(backgroundRectangle.Right - H_PAD * ui - noWidth), buttonsTop,
                                         (int)noWidth, (int)lineHeight);
            yesButtonRect = new Rectangle((int)(noButtonRect.X - BUTTON_GAP * ui - yesWidth), buttonsTop,
                                          (int)yesWidth, (int)lineHeight);
        }
    }

    /// <summary>
    /// Draws the message box.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;

        // Darken down any other screens that were drawn beneath the popup.
        ScreenManager.FadeBackBufferToBlack(TransitionAlpha * 2 / 3);

        // Fade the popup alpha during transitions.
        float alpha = TransitionAlpha;
        int shadow = Math.Max(1, (int)(2 * ui));
        float lineHeight = TEXT_HEIGHT * ui;

        // Ohne GlobalTransformation: Fensterpixel, die Schrift wird nicht gestreckt
        spriteBatch.Begin();

        MenuStyle.DrawPanel(ScreenManager, backgroundRectangle, MenuStyle.PanelFill, MenuStyle.PanelBorder,
                            Math.Max(2, (int)(3 * ui)), alpha);

        MenuStyle.DrawText(ScreenManager, message, messageCenter, lineHeight, Color.White, alpha, shadow);

        if (ShowsButtons)
        {
            MenuStyle.DrawText(ScreenManager, Resources.YesButtonText, yesButtonRect.Center.ToVector2(), lineHeight,
                               Color.LimeGreen, alpha, shadow);
            MenuStyle.DrawText(ScreenManager, Resources.NoButtonText, noButtonRect.Center.ToVector2(), lineHeight,
                               Color.OrangeRed, alpha, shadow);
        }

        spriteBatch.End();
    }
}
