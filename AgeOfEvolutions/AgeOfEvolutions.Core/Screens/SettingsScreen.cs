using System;
using System.Collections.Generic;
using AgeOfEvolutions.Core;
using AgeOfEvolutions.Core.Localization;
using AgeOfEvolutions.Core.Settings;
using AgeOfEvolutions.ScreenManagers;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// The settings screen is brought up over the top of the main menu
/// screen, and gives the user a chance to configure the game
/// in various hopefully useful ways.
/// </summary>
class SettingsScreen : MenuScreen
{
    private MenuEntry fullscreenMenuEntry;
    private MenuEntry backMenuEntry;

    // Die zehn Gameplay-Optionen; jede blätter bei einem Klick auf die nächste
    // Stufe (siehe GameplayOptions) und nimmt die gewählte in settingsManager.
    private MenuEntry soundVolumeEntry;
    private MenuEntry aiDifficultyEntry;
    private MenuEntry startingCapitalEntry;
    private MenuEntry populationCapEntry;
    private MenuEntry unitSpeedEntry;
    private MenuEntry gatherSpeedEntry;
    private MenuEntry productionSpeedEntry;
    private MenuEntry meleeStopEntry;
    private MenuEntry formationSpacingEntry;
    private MenuEntry cameraPanEntry;

    private GraphicsDeviceManager gdm;

    private SettingsManager<AgeOfEvolutionsSettings> settingsManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="SettingsScreen"/> class.
    /// </summary>
    public SettingsScreen()
        : base(Resources.Settings)
    {
        // Create our menu entries.
        fullscreenMenuEntry = new MenuEntry(string.Empty);
        backMenuEntry = new MenuEntry(string.Empty);

        // Hook up menu event handlers.
        fullscreenMenuEntry.Selected += FullScreenMenuEntrySelected;
        backMenuEntry.Selected += OnCancel;

        // Die zehn Gameplay-Optionen, jede ein eigener Eintrag.
        soundVolumeEntry = AddGameOption("Lautstärke", GameplayOptions.SoundVolume,
            () => settingsManager.Settings.SoundVolume, v => settingsManager.Settings.SoundVolume = v,
            v => v.ToString() + " %");
        aiDifficultyEntry = AddGameOption("KI-Schwierigkeit", GameplayOptions.AiDifficulty,
            () => settingsManager.Settings.AiDifficulty, v => settingsManager.Settings.AiDifficulty = v,
            v => GameplayOptions.AiNames[v]);
        startingCapitalEntry = AddGameOption("Startkapital", GameplayOptions.StartingCapital,
            () => settingsManager.Settings.StartingCapital, v => settingsManager.Settings.StartingCapital = v,
            v => GameplayOptions.CapitalNames[v]);
        populationCapEntry = AddGameOption("Bevölkerungsgrenze", GameplayOptions.PopulationCap,
            () => settingsManager.Settings.PopulationCap, v => settingsManager.Settings.PopulationCap = v,
            v => v.ToString());
        unitSpeedEntry = AddGameOption("Einheiten-Tempo", GameplayOptions.UnitSpeed,
            () => settingsManager.Settings.UnitSpeed, v => settingsManager.Settings.UnitSpeed = v,
            v => GameplayOptions.Percent(v));
        gatherSpeedEntry = AddGameOption("Sammeln", GameplayOptions.GatherSpeed,
            () => settingsManager.Settings.GatherSpeed, v => settingsManager.Settings.GatherSpeed = v,
            v => GameplayOptions.Percent(v));
        productionSpeedEntry = AddGameOption("Produktion", GameplayOptions.ProductionSpeed,
            () => settingsManager.Settings.ProductionSpeed, v => settingsManager.Settings.ProductionSpeed = v,
            v => GameplayOptions.Percent(v));
        meleeStopEntry = AddGameOption("Kampf-Abstand", GameplayOptions.MeleeStopTiles,
            () => settingsManager.Settings.MeleeStopTiles, v => settingsManager.Settings.MeleeStopTiles = v,
            v => GameplayOptions.TileHunderts(v));
        formationSpacingEntry = AddGameOption("Verbands-Abstand", GameplayOptions.FormationSpacingTiles,
            () => settingsManager.Settings.FormationSpacingTiles, v => settingsManager.Settings.FormationSpacingTiles = v,
            v => GameplayOptions.TileHunderts(v));
        cameraPanEntry = AddGameOption("Kamera-Pan", GameplayOptions.CameraPanSpeed,
            () => settingsManager.Settings.CameraPanSpeed, v => settingsManager.Settings.CameraPanSpeed = v,
            v => v.ToString() + " px/s");

        // Add entries to the menu.
        MenuEntries.Add(soundVolumeEntry);
        MenuEntries.Add(aiDifficultyEntry);
        MenuEntries.Add(startingCapitalEntry);
        MenuEntries.Add(populationCapEntry);
        MenuEntries.Add(unitSpeedEntry);
        MenuEntries.Add(gatherSpeedEntry);
        MenuEntries.Add(productionSpeedEntry);
        MenuEntries.Add(meleeStopEntry);
        MenuEntries.Add(formationSpacingEntry);
        MenuEntries.Add(cameraPanEntry);
        MenuEntries.Add(fullscreenMenuEntry);
        MenuEntries.Add(backMenuEntry);
    }

    /// <summary>
    /// Beschreibung einer Gameplay-Option: Beschriftung, Stufen und die
    /// Zugriffspfade auf settingsManager. Die Einträge werden im Konstruktor
    /// angelegt und in LoadContent beschriftet — davor ist settingsManager
    /// noch null (lazy), und der erste Text darf deshalb nicht da kommen.
    /// </summary>
    private class GameOption
    {
        public MenuEntry Entry;
        public string Label;
        public int[] Steps;
        public Func<int> Getter;
        public Action<int> Setter;
        public Func<int, string> Format;
    }

    private readonly List<GameOption> gameOptions = new();

    /// <summary>
    /// Ein Optionseintrag: bei jeder Auswahl die nächste Stufe setzen
    /// (über der höchsten Stufe zurück zur ersten) und die gewählte zeigen.
    /// Die Werte leben in settingsManager; PropertyChanged dort speichert.
    /// </summary>
    private MenuEntry AddGameOption(string label, int[] steps, Func<int> getter, Action<int> setter, Func<int, string> format)
    {
        MenuEntry entry = new MenuEntry(string.Empty);
        GameOption option = new GameOption
        {
            Entry = entry,
            Label = label,
            Steps = steps,
            Getter = getter,
            Setter = setter,
            Format = format,
        };
        entry.Selected += (sender, e) =>
        {
            if (settingsManager?.Settings is null)
                return;   // LoadContent noch nicht gelaufen: nichts zu wechseln
            int next = GameplayOptions.Next(option.Getter(), option.Steps);
            option.Setter(next);
            entry.Text = option.Label + ": " + option.Format(next);
        };
        gameOptions.Add(option);
        return entry;
    }

    /// <summary>Die aktuellen Werte aller zehn Optionen in die Eintragstexte.</summary>
    private void SetGameOptionTexts()
    {
        if (settingsManager?.Settings is null)
            return;
        foreach (GameOption option in gameOptions)
            option.Entry.Text = option.Label + ": " + option.Format(option.Getter());
    }

    /// <summary>
    /// Loads content for the settings screen, including lazy loading services and setting initial values.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();

        // Lazy Load some things
        gdm ??= ScreenManager.Game.Services.GetService<GraphicsDeviceManager>();

        settingsManager ??= ScreenManager.Game.Services.GetService<SettingsManager<AgeOfEvolutionsSettings>>();

        settingsManager.Settings.PropertyChanged += (s, e) =>
        {
            SetMenuTexts();
            SetGameOptionTexts();

            settingsManager.Save();
        };

        // Die Anzeige zeigt, wie das Spiel gerade läuft (gdm.IsFullScreen); die
        // gespeicherte Einstellung hat der Start schon übernommen. Sie hier ohne
        // ApplyChanges zu setzen, ließ das Menü "Vollbild" zeigen, während das
        // Spiel im Fenster lief.

        SetMenuTexts();
        SetGameOptionTexts();
    }

    /// <summary>
    /// Fills in the latest values for the options screen menu text.
    /// </summary>
    private void SetMenuTexts()
    {
        fullscreenMenuEntry.Text = string.Format(Resources.DisplayMode, gdm.IsFullScreen ? Resources.FullScreen : Resources.Windowed);

        backMenuEntry.Text = Resources.Back;

        Title = Resources.Settings;
    }

    /// <summary>
    /// Event handler for when the Fullscreen menu entry is selected.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
    private void FullScreenMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        // Über das Spiel, nicht direkt am GraphicsDeviceManager: das Spiel stellt
        // beim Verlassen des Vollbilds die Fenstergröße wieder her.
        ((AgeOfEvolutionsGame)ScreenManager.Game).ToggleFullScreen();

        settingsManager.Settings.FullScreen = gdm.IsFullScreen;
    }
}