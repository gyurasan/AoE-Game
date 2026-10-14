using System;
using System.Collections.Generic;
using AgeOfEvolutions.Core;
using AgeOfEvolutions.Core.Inputs;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// Base class for screens that contain a menu of options. The user can
/// move up and down to select an entry, or cancel to back out of the screen.
/// </summary>
/// <remarks>
/// Titel und Einträge stehen auf einer Tafel in der Bildmitte, im Stil des
/// Hauptmenüs (<see cref="MenuStyle"/>), gezeichnet in echten Fensterpixeln.
/// Vorher lief das Menü im 800×480-Raster mit der 14-pt-HUD-Schrift, die die
/// GlobalTransformation auf Fenstergröße streckte - auf großen Fenstern
/// verschwommen, und der gewählte Eintrag pulsierte dauernd.
/// </remarks>
abstract class MenuScreen : GameScreen
{
    // Entwurfsmaße für 1080 px Fensterhöhe; MenuStyle.UiScale rechnet sie um
    private const float TITLE_HEIGHT = 56f, TITLE_GAP = 28f, PANEL_PAD = 36f;
    private const float ENTRY_TEXT_HEIGHT = 34f, ENTRY_HEIGHT = 48f, ENTRY_STEP = 56f;
    private const float MIN_ENTRY_WIDTH = 520f, ENTRY_TEXT_PAD = 24f;

    private List<MenuEntry> menuEntries = new List<MenuEntry>();
    private int selectedEntry = 0;
    private string menuTitle;

    // Lage von Tafel, Titel und Einträgen im Fenster - dieselbe zum Zeichnen
    // und Klicken, in UpdateLayout aus Fenstergröße und Texten berechnet
    private readonly List<Rectangle> entryRects = new List<Rectangle>();
    private Rectangle panelRect;
    private Vector2 titleCenter;
    private float ui = 1f;

    /// <summary>
    /// Gets or sets the title of the menu screen.
    /// </summary>
    public string Title { get => menuTitle; set => menuTitle = value; }

    /// <summary>
    /// Gets the list of menu entries, so derived classes can add
    /// or change the menu contents.
    /// </summary>
    protected IList<MenuEntry> MenuEntries
    {
        get { return menuEntries; }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MenuScreen"/> class.
    /// </summary>
    /// <param name="menuTitle">The title of the menu screen.</param>
    public MenuScreen(string menuTitle)
    {
        this.menuTitle = menuTitle;

        TransitionOnTime = TimeSpan.FromSeconds(0.25);
        TransitionOffTime = TimeSpan.FromSeconds(0.25);
    }

    /// <summary>
    /// Loads content for the menu screen. This method is called once per game
    /// and is the place to load all content specific to the menu screen.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();
    }

    /// <summary>
    /// Responds to user input, changing the selected entry and accepting
    /// or canceling the menu.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="inputState">Provides the current state of input devices.</param>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        base.HandleInput(gameTime, inputState);

        UpdateLayout();

        // Handle touch input for mobile platforms.
        if (AgeOfEvolutionsGame.IsMobile)
        {
            var touchState = inputState.CurrentTouchState;
            if (touchState.Count > 0)
            {
                foreach (var touch in touchState)
                {
                    if (touch.State == TouchLocationState.Pressed)
                    {
                        ActivateEntryAt(touch.Position.ToPoint());
                    }
                }
            }
        }
        // Handle mouse input for desktop platforms.
        else if (AgeOfEvolutionsGame.IsDesktop)
        {
            // Wie im Hauptmenü: Zeigen wählt, Loslassen über einem Eintrag
            // startet ihn; eine ruhende Maus überstimmt die Tastatur nicht.
            // Fensterpixel, nicht CurrentCursorLocation - die ist ins
            // 800×480-Raster zurückgerechnet.
            MouseState mouse = inputState.CurrentMouseState;
            int hovered = EntryAt(mouse.Position);
            if (hovered >= 0 && mouse.Position != inputState.LastMouseState.Position)
                selectedEntry = hovered;

            if (inputState.IsLeftMouseButtonClicked())
            {
                ActivateEntryAt(mouse.Position);
            }
            else if (inputState.IsMiddleMouseButtonClicked())
            {
                OnSelectEntry(selectedEntry, PlayerIndex.One);
            }
        }

        // Move to the previous menu entry.
        if (inputState.IsMenuUp(ControllingPlayer))
        {
            selectedEntry--;

            if (selectedEntry < 0)
                selectedEntry = menuEntries.Count - 1;

            while (!menuEntries[selectedEntry].Enabled)
            {
                selectedEntry--;

                if (selectedEntry < 0)
                    selectedEntry = menuEntries.Count - 1;
            }
        }

        // Move to the next menu entry.
        if (inputState.IsMenuDown(ControllingPlayer))
        {
            selectedEntry++;

            if (selectedEntry >= menuEntries.Count)
                selectedEntry = 0;

            SetNextEnabledMenu();
        }

        // Accept or cancel the menu.
        PlayerIndex playerIndex;

        if (inputState.IsMenuSelect(ControllingPlayer, out playerIndex))
        {
            OnSelectEntry(selectedEntry, playerIndex);
        }
        else if (inputState.IsMenuCancel(ControllingPlayer, out playerIndex))
        {
            OnCancel(playerIndex);
        }
    }

    /// <summary>Der wählbare Eintrag unter dem Fensterpunkt, oder -1.</summary>
    private int EntryAt(Point point)
    {
        for (int i = 0; i < entryRects.Count; i++)
        {
            if (menuEntries[i].Enabled && entryRects[i].Contains(point))
                return i;
        }
        return -1;
    }

    /// <summary>
    /// Startet den Eintrag unter einem Klick oder einer Berührung, falls dort einer ist.
    /// </summary>
    /// <param name="point">Der Klickpunkt in Fensterpixeln.</param>
    private void ActivateEntryAt(Point point)
    {
        int hit = EntryAt(point);
        if (hit < 0)
            return;

        selectedEntry = hit;
        OnSelectEntry(selectedEntry, ControllingPlayer ?? PlayerIndex.One);
    }

    /// <summary>
    /// Sets the next enabled menu entry as the selected entry.
    /// </summary>
    private void SetNextEnabledMenu()
    {
        while (!menuEntries[selectedEntry].Enabled)
        {
            selectedEntry++;

            if (selectedEntry >= menuEntries.Count)
                selectedEntry = 0;
        }
    }

    /// <summary>
    /// Handler for when the user has chosen a menu entry.
    /// </summary>
    /// <param name="entryIndex">The index of the selected menu entry.</param>
    /// <param name="playerIndex">The index of the player who triggered the selection.</param>
    protected virtual void OnSelectEntry(int entryIndex, PlayerIndex playerIndex)
    {
        menuEntries[entryIndex].OnSelectEntry(playerIndex);
    }

    /// <summary>
    /// Handler for when the user has canceled the menu.
    /// </summary>
    /// <param name="playerIndex">The index of the player who triggered the cancellation.</param>
    protected virtual void OnCancel(PlayerIndex playerIndex)
    {
        ExitScreen();
    }

    /// <summary>
    /// Helper overload makes it easy to use OnCancel as a MenuEntry event handler.
    /// </summary>
    /// <param name="sender">The object that triggered the event.</param>
    /// <param name="e">Event arguments containing the player index.</param>
    protected void OnCancel(object sender, PlayerIndexEventArgs e)
    {
        OnCancel(e.PlayerIndex);
    }

    /// <summary>
    /// Ordnet Tafel, Titel und Einträge im Fenster an: alle Einträge gleich
    /// breit (so breit wie der längste Text, mindestens MIN_ENTRY_WIDTH),
    /// untereinander, die ganze Tafel mittig im Fenster.
    /// </summary>
    private void UpdateLayout()
    {
        var parameters = ScreenManager.GraphicsDevice.PresentationParameters;
        int width = parameters.BackBufferWidth;
        int height = parameters.BackBufferHeight;
        ui = MenuStyle.UiScale(height);

        float entryWidth = MIN_ENTRY_WIDTH * ui;
        foreach (MenuEntry entry in menuEntries)
            entryWidth = Math.Max(entryWidth,
                MenuStyle.TextWidth(ScreenManager, entry.Text, ENTRY_TEXT_HEIGHT * ui) + 2 * ENTRY_TEXT_PAD * ui);
        if (!string.IsNullOrEmpty(menuTitle))
            entryWidth = Math.Max(entryWidth, MenuStyle.TextWidth(ScreenManager, menuTitle, TITLE_HEIGHT * ui));

        float contentHeight = TITLE_HEIGHT + TITLE_GAP
                            + Math.Max(0, menuEntries.Count - 1) * ENTRY_STEP + ENTRY_HEIGHT;
        int panelWidth = (int)(entryWidth + 2 * PANEL_PAD * ui);
        int panelHeight = (int)((contentHeight + 2 * PANEL_PAD) * ui);
        panelRect = new Rectangle((width - panelWidth) / 2, (height - panelHeight) / 2, panelWidth, panelHeight);

        titleCenter = new Vector2(width / 2f, panelRect.Y + (PANEL_PAD + TITLE_HEIGHT / 2f) * ui);

        float entriesTop = panelRect.Y + (PANEL_PAD + TITLE_HEIGHT + TITLE_GAP) * ui;
        int entryLeft = (int)((width - entryWidth) / 2);
        entryRects.Clear();
        for (int i = 0; i < menuEntries.Count; i++)
        {
            entryRects.Add(new Rectangle(entryLeft, (int)(entriesTop + i * ENTRY_STEP * ui),
                                         (int)entryWidth, (int)(ENTRY_HEIGHT * ui)));
        }
    }

    /// <summary>
    /// Updates the menu screen.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    /// <param name="otherScreenHasFocus">Whether another screen currently has focus.</param>
    /// <param name="coveredByOtherScreen">Whether this screen is covered by another screen.</param>
    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        SetNextEnabledMenu();
    }

    /// <summary>
    /// Draws the menu screen.
    /// </summary>
    /// <param name="gameTime">Provides a snapshot of timing values.</param>
    public override void Draw(GameTime gameTime)
    {
        // Texte können sich seit dem letzten Bild geändert haben (Einstellungen)
        UpdateLayout();

        SpriteBatch spriteBatch = ScreenManager.SpriteBatch;
        float alpha = TransitionAlpha;
        int shadow = Math.Max(1, (int)(2 * ui));

        // Ohne GlobalTransformation: Fensterpixel, die Schrift wird nicht gestreckt
        spriteBatch.Begin();

        MenuStyle.DrawPanel(ScreenManager, panelRect, MenuStyle.PanelFill, MenuStyle.PanelBorder,
                            Math.Max(2, (int)(3 * ui)), alpha);

        if (!string.IsNullOrEmpty(menuTitle))
            MenuStyle.DrawText(ScreenManager, menuTitle, titleCenter, TITLE_HEIGHT * ui, MenuStyle.Gold, alpha,
                               Math.Max(2, (int)(3 * ui)));

        for (int i = 0; i < menuEntries.Count; i++)
        {
            MenuEntry entry = menuEntries[i];
            Rectangle plate = entryRects[i];
            bool isSelected = IsActive && i == selectedEntry;

            MenuStyle.DrawPanel(ScreenManager, plate,
                                isSelected ? MenuStyle.PlateSelected : MenuStyle.Plate,
                                MenuStyle.Gold, isSelected ? Math.Max(2, (int)(2 * ui)) : 0, alpha);

            Color color = !entry.Enabled ? MenuStyle.TextDisabled
                        : isSelected ? MenuStyle.Gold
                                     : MenuStyle.Text;
            MenuStyle.DrawText(ScreenManager, entry.Text, plate.Center.ToVector2(), ENTRY_TEXT_HEIGHT * ui,
                               color, alpha, shadow);
        }

        spriteBatch.End();
    }
}
