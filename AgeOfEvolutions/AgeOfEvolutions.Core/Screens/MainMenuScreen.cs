using System;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Core.Settings;
using AgeOfEvolutions.Core.Update;
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
    SoundEffect soundButton;

    // „Spiel laden" ist grau und nicht wählbar, solange es keinen Spielstand gibt;
    // LoadContent schaltet den Eintrag frei, wenn die Datei da ist.
    // „Update" erscheint nur, wenn ein neueres Release vorliegt (RefreshUpdateEntry)
    readonly (string Text, bool Enabled)[] menuOptions =
    {
        ("Neues Spiel", true),
        ("Karte", true),
        ("Spiel laden", false),
        ("Einstellungen", true),
        ("Beenden", true),
        ("Update", false),
    };
    int selectedIndex = 0;

    // Der Karteneintrag wechselt bei jeder Wahl die Größe (Standard, Groß, Maximal);
    // die Wahl bleibt in den Einstellungen gespeichert
    private const int MAP_ENTRY = 1;
    MapSize mapSize = MapSize.Standard;
    SettingsManager<AgeOfEvolutionsSettings> settingsManager;

    // „Spiel laden": der Spielstand am Standardort (neben den Einstellungen)
    private const int LOAD_ENTRY = 2;
    readonly SaveGameStore saves = new SaveGameStore();

    // Update (Nutzer 2026-10-18, Aussehen 2026-10-20: „nicht wie die anderen
    // menüs"): ein gewöhnlicher Menüeintrag unter „Beenden", nur sichtbar, wenn
    // GitHubs neueste Version neuer ist; darunter eine kleine Zeile mit der
    // Version dieser Installation (UpdateService.VersionLine)
    private const int UPDATE_ENTRY = 5;
    readonly UpdateService updater = new UpdateService();
    bool updateEntryVisible;
    // Nach dem Update (Phase Done) beendet sich diese Version genau einmal
    bool exitRequested;

    public MainMenuScreen()
    {
        TransitionOnTime = TimeSpan.FromSeconds(1.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    public override void LoadContent()
    {
        base.LoadContent();

        var content = ScreenManager.Game.Content;

        // Menübild aus tools/bilder (Qwen-Image, bilder.json: menu/hintergrund).
        // Everything is loaded defensively: a missing asset degrades to the plain
        // parchment look rather than taking the whole game down on startup.
        menuBackground = TryLoad<Texture2D>(content, "Backgrounds/menu");
        soundButton = TryLoad<SoundEffect>(content, "Sounds/PlayerGemCollected");

        // Die zuletzt gewählte Kartengröße
        settingsManager = ScreenManager.Game.Services.GetService<SettingsManager<AgeOfEvolutionsSettings>>();
        if (settingsManager != null)
            mapSize = (MapSize)Math.Clamp(settingsManager.Settings.MapSize, 0, 2);

        // „Spiel laden" nur, wenn es einen Spielstand gibt
        RefreshLoadEntry();

        // Update-Prüfung im Hintergrund: blockiert den Menüstart nicht,
        // ohne Netz bleibt die Zeile bei "nicht möglich" — CheckLatest()
        // ist selbst exception-safe (UpdateService-VERTRAG). Daneben räumt
        // CleanupAfterUpdate die Sicherungen eines vorigen Updates weg.
        System.Threading.Tasks.Task.Run(() => updater.CheckLatest());
        System.Threading.Tasks.Task.Run(() => UpdateService.CleanupAfterUpdate(UpdateService.InstallDir));

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

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);
        ExitAfterUpdate();
    }

    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        var keyboard = inputState.CurrentKeyboardStates[0];
        var prevKeyboard = inputState.LastKeyboardStates[0];
        var buffer = ScreenManager.GraphicsDevice.PresentationParameters;

        RefreshUpdateEntry();

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
        while (!EntryEnabled(selectedIndex));
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
        if (hit < 0 || !EntryEnabled(hit))
            return (selectedIndex, false);
        bool moved = current.Position != last.Position;
        bool released = current.LeftButton == ButtonState.Released && last.LeftButton == ButtonState.Pressed;
        return (moved || released ? hit : selectedIndex, released);
    }

    /// <summary>Der sichtbare Eintrag unter dem Bildschirmpunkt, oder -1.</summary>
    private int EntryAt(Point p, int width, int height)
    {
        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (EntryVisible(i) && EntryRect(i, width, height).Contains(p))
                return i;
        }
        return -1;
    }

    /// <summary>Wird der Eintrag gezeichnet? Alle außer „Update" ohne Angebot.</summary>
    private bool EntryVisible(int index)
        => index != UPDATE_ENTRY || updateEntryVisible;

    /// <summary>
    /// Ist der Eintrag wählbar? Sein eigener Zustand aus menuOptions; solange ein
    /// Update läuft oder die neue Version schon startet, sind dazu „Neues Spiel"
    /// und „Spiel laden" gesperrt - sonst liefe die alte Version weiter, während
    /// die neue aufgeht.
    /// </summary>
    private bool EntryEnabled(int index)
    {
        if (!menuOptions[index].Enabled || !EntryVisible(index))
            return false;
        if (index == 0 || index == LOAD_ENTRY)
            return !updater.UpdateActive && updater.UpdatePhase != UpdateService.Phase.Done;
        return true;
    }

    private static float UiScale(int height) => height / DESIGN_HEIGHT;

    /// <summary>Text eines Eintrags; der Karteneintrag nennt die gewählte Größe.</summary>
    private string EntryText(int index)
        => index == MAP_ENTRY ? $"Karte: {MapSizes.Name(mapSize)}" : menuOptions[index].Text;

    /// <summary>Die nächste Kartengröße im Wechsel Standard, Groß, Maximal.</summary>
    private static MapSize NextMapSize(MapSize size) => (MapSize)(((int)size + 1) % 3);

    /// <summary>Platte eines Eintrags im Fenster - dieselbe zum Zeichnen und Klicken.</summary>
    private static Rectangle EntryRect(int index, int width, int height)
    {
        float ui = UiScale(height);
        int w = (int)(ENTRY_WIDTH * ui), h = (int)(ENTRY_HEIGHT * ui);
        return new Rectangle((width - w) / 2, (int)((ENTRY_TOP + index * ENTRY_STEP) * ui), w, h);
    }

    /// <summary>
    /// Die kleine Versionszeile unter dem letzten sichtbaren Eintrag (Index
    /// <paramref name="lastEntry"/>): mittig, so breit wie eine Platte, ohne
    /// Überlappung mit ihr.
    /// </summary>
    private static Rectangle VersionRect(int lastEntry, int width, int height)
    {
        float ui = UiScale(height);
        int w = (int)(ENTRY_WIDTH * ui), h = (int)(32 * ui);
        int top = EntryRect(lastEntry, width, height).Bottom + (int)(12 * ui);
        return new Rectangle((width - w) / 2, top, w, h);
    }

    /// <summary>
    /// Gleicht den Eintrag „Update" mit dem UpdateService ab: Text aus
    /// updater.EntryText(), sichtbar nur mit Text (Angebot liegt vor), wählbar
    /// nur, wenn updater.CanStartUpdate. Verschwindet er, während er gewählt
    /// ist, springt die Auswahl auf „Neues Spiel".
    /// </summary>
    private void RefreshUpdateEntry()
    {
        string text = updater.EntryText();
        updateEntryVisible = text != null;
        menuOptions[UPDATE_ENTRY] = (text ?? "Update", text != null && updater.CanStartUpdate);
        if (!updateEntryVisible && selectedIndex == UPDATE_ENTRY)
            selectedIndex = 0;
    }

    /// <summary>
    /// Läuft die neue Version (Phase Done), beendet sich diese alte genau einmal:
    /// über UpdateService.ExitApp, wenn gesetzt (Tests), sonst regulär per
    /// Game.Exit(). Meldet true beim auslösenden Aufruf.
    /// </summary>
    private bool ExitAfterUpdate()
    {
        if (exitRequested || updater.UpdatePhase != UpdateService.Phase.Done)
            return false;
        exitRequested = true;
        if (UpdateService.ExitApp != null)
            UpdateService.ExitApp();
        else
            ScreenManager.Game.Exit();
        return true;
    }

    private void HandleMenuSelect()
    {
        if (!EntryEnabled(selectedIndex))
            return;
        soundButton?.Play();

        switch (selectedIndex)
        {
            case 0: // New Game
                // Via LoadingScreen so the menu transitions off before the map is built.
                LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen(mapSize));
                break;
            case MAP_ENTRY: // Kartengröße wechseln und merken
                mapSize = NextMapSize(mapSize);
                if (settingsManager != null)
                {
                    settingsManager.Settings.MapSize = (int)mapSize;
                    settingsManager.Save();
                }
                break;
            case LOAD_ENTRY: // Spiel laden
                LoadSavedGame();
                break;
            case 3: // Settings
                ScreenManager.AddScreen(new SettingsScreen(), ControllingPlayer);
                break;
            case 4: // Exit
                ScreenManager.Game.Exit();
                break;
            case UPDATE_ENTRY: // Update laden, installieren, neu starten
                updater.BeginUpdate();
                break;
        }
    }

    /// <summary>
    /// Schaltet „Spiel laden" frei, wenn es einen Spielstand gibt, sonst sperrt es den Eintrag.
    /// LoadContent ruft sie auf; tools/spielablauf (Gruppe menue) prüft sie ohne Grafik.
    /// VERTRAG: menuOptions[LOAD_ENTRY] = ("Spiel laden", saves.Exists); steht die Auswahl
    /// (selectedIndex) auf dem nun gesperrten Eintrag, springt sie auf 0 („Neues Spiel").
    /// </summary>
    private void RefreshLoadEntry()
    {
        menuOptions[LOAD_ENTRY] = ("Spiel laden", saves.Exists);
        if (selectedIndex == LOAD_ENTRY && !menuOptions[LOAD_ENTRY].Enabled)
            selectedIndex = 0;
    }

    /// <summary>
    /// Setzt das gespeicherte Spiel fort (Eintrag „Spiel laden").
    /// VERTRAG: in einem try: save = saves.Load(); zur Probe
    /// SaveGameMapper.Restore(save) (Ergebnis verwerfen - ein kaputter Spielstand fällt so
    /// hier auf und nicht erst mitten im Ladebildschirm); dann wie „Neues Spiel"
    /// LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen(save)).
    /// Jede Ausnahme (catch Exception ex): das Hauptmenü bleibt, der Eintrag wird gesperrt
    /// (menuOptions[LOAD_ENTRY] = ("Spiel laden", false)), die Auswahl springt auf „Neues Spiel"
    /// (selectedIndex = 0), und ScreenManager.AddScreen(new MessageBoxScreen("Spielstand nicht
    /// lesbar: " + ex.Message, false, TimeSpan.FromSeconds(3), true), ControllingPlayer) zeigt es.
    /// </summary>
    private void LoadSavedGame()
    {
        try
        {
            var save = saves.Load();
            SaveGameMapper.Restore(save);
            LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen(save));
        }
        catch (Exception ex)
        {
            menuOptions[LOAD_ENTRY] = ("Spiel laden", false);
            selectedIndex = 0;
            ScreenManager.AddScreen(new MessageBoxScreen("Spielstand nicht lesbar: " + ex.Message, false,
                                                         TimeSpan.FromSeconds(3), true), ControllingPlayer);
        }
    }

    public override void Draw(GameTime gameTime)
    {
        var spriteBatch = ScreenManager.SpriteBatch;
        var width = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferWidth;
        var height = ScreenManager.GraphicsDevice.PresentationParameters.BackBufferHeight;
        float ui = UiScale(height);
        int shadow = Math.Max(1, (int)(2 * ui));

        RefreshUpdateEntry();

        spriteBatch.Begin();

        DrawBackground(spriteBatch, width, height);

        // Leicht abgedunkelt, damit Tafel und Schrift sich vom Bild abheben
        spriteBatch.Draw(menuButton, new Rectangle(0, 0, width, height), Color.Black * 0.3f);

        // Tafel hinter Titel und Einträgen; reicht bis unter die Versionszeile
        int lastEntry = updateEntryVisible ? UPDATE_ENTRY : UPDATE_ENTRY - 1;
        var versionRect = VersionRect(lastEntry, width, height);
        int panelWidth = (int)(PANEL_WIDTH * ui);
        int panelTop = (int)(PANEL_TOP * ui);
        int panelBottom = versionRect.Bottom + (int)(20 * ui);
        var panel = new Rectangle((width - panelWidth) / 2, panelTop, panelWidth, panelBottom - panelTop);
        spriteBatch.Draw(menuButton, panel, new Color(30, 20, 10) * 0.88f);
        DrawBorder(spriteBatch, panel, Math.Max(2, (int)(3 * ui)), new Color(180, 140, 60));

        DrawCentered(spriteBatch, "Age of Evolutions", new Vector2(width / 2f, panelTop + 78 * ui),
                     72 * ui, new Color(255, 215, 0), Math.Max(2, (int)(3 * ui)));

        for (int i = 0; i < menuOptions.Length; i++)
        {
            if (!EntryVisible(i))
                continue;
            var text = EntryText(i);
            bool enabled = EntryEnabled(i);
            var plate = EntryRect(i, width, height);
            bool selected = i == selectedIndex;

            spriteBatch.Draw(menuButton, plate, selected
                ? new Color(100, 60, 30) * 0.95f
                : new Color(55, 38, 20) * 0.9f);
            if (selected)
                DrawBorder(spriteBatch, plate, Math.Max(2, (int)(2 * ui)), new Color(255, 215, 0));

            // Ein laufendes Update ist nicht wählbar, bleibt aber hell lesbar:
            // sein Text ist die Fortschrittsanzeige
            bool updateRunning = i == UPDATE_ENTRY && !enabled;
            var color = !enabled && !updateRunning ? new Color(120, 110, 95)
                      : selected ? new Color(255, 215, 0)
                                 : Color.LightGray;
            DrawCentered(spriteBatch, text, plate.Center.ToVector2(), 40 * ui, color, shadow);

            // Download: dünner Balken am unteren Rand der Platte
            if (i == UPDATE_ENTRY && updater.UpdatePhase == UpdateService.Phase.Downloading
                && updater.ProgressTotal > 0)
            {
                double f = Math.Min(1.0, (double)updater.ProgressDone / updater.ProgressTotal);
                int inset = Math.Max(2, (int)(4 * ui));
                var bar = new Rectangle(plate.X + inset, plate.Bottom - inset - Math.Max(2, (int)(4 * ui)),
                                        (int)((plate.Width - 2 * inset) * f), Math.Max(2, (int)(4 * ui)));
                spriteBatch.Draw(menuButton, bar, new Color(255, 215, 0) * 0.85f);
            }
        }

        // Versionszeile: klein, gedämpft; lange Meldungen (Fehlergrund) werden
        // kleiner gezeichnet, statt über die Tafel zu ragen
        string line = updater.VersionLine();
        float lineHeight = 24 * ui;
        float lineWidth = MenuStyle.TextWidth(ScreenManager, line, lineHeight);
        if (lineWidth > versionRect.Width)
            lineHeight *= versionRect.Width / lineWidth;
        DrawCentered(spriteBatch, line, versionRect.Center.ToVector2(), lineHeight,
                     new Color(196, 184, 160), shadow);

        DrawCentered(spriteBatch, "Maus oder Pfeiltasten: wählen   Klick oder Enter: starten",
                     new Vector2(width / 2f, height - 36 * ui), 24 * ui, new Color(220, 210, 190), shadow);

        spriteBatch.End();

        // Fade the screen in/out while transitioning.
        if (TransitionPosition > 0)
            ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
    }

    /// <summary>
    /// Text mit Schatten, mittig auf <paramref name="center"/> und
    /// <paramref name="pixelHeight"/> Pixel hoch. Die Schrift wählt
    /// ScreenManager.MenuFontFor passend zur Höhe (40 oder 80 pt), damit
    /// sie auf großen Fenstern nicht gestreckt und unscharf wird.
    /// </summary>
    private void DrawCentered(SpriteBatch spriteBatch, string text, Vector2 center, float pixelHeight,
                              Color color, int shadow)
    {
        MenuStyle.DrawText(ScreenManager, text, center, pixelHeight, color, 1f, shadow);
    }

    /// <summary>
    /// Zeichnet das Menübild (16:9) fensterfüllend im eigenen Seitenverhältnis:
    /// was über den Rand ragt, wird abgeschnitten - die Mitte mit dem Dorfplatz
    /// bleibt immer sichtbar, und es gibt keine leeren Balken.
    /// </summary>
    private void DrawBackground(SpriteBatch spriteBatch, int width, int height)
    {
        // Dark parchment base, also the fallback if the artwork is missing.
        spriteBatch.Draw(menuButton, new Rectangle(0, 0, width, height), new Color(69, 46, 24));

        if (menuBackground == null)
            return;

        float scale = Math.Max((float)width / menuBackground.Width,
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
