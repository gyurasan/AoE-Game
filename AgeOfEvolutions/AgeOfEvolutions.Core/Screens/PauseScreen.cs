using System;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// The pause menu comes up over the top of the game (Esc), giving the player
/// options to resume, save or quit. Deutsch wie das Hauptmenü.
/// </summary>
class PauseScreen : MenuScreen
{
    /// <summary>So lange steht eine Meldung wie „Spiel gespeichert" über dem Menü.</summary>
    private static readonly TimeSpan TOAST_DURATION = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// Initializes a new instance of the <see cref="PauseScreen"/> class.
    /// </summary>
    public PauseScreen()
        : base("Pause")
    {
        // Create our menu entries.
        MenuEntry resumeGameMenuEntry = new MenuEntry("Fortsetzen");
        MenuEntry saveGameMenuEntry = new MenuEntry("Spiel speichern");
        MenuEntry quitGameMenuEntry = new MenuEntry("Beenden");

        // Hook up menu event handlers.
        resumeGameMenuEntry.Selected += OnCancel;
        saveGameMenuEntry.Selected += SaveGameMenuEntrySelected;
        quitGameMenuEntry.Selected += QuitGameMenuEntrySelected;

        // Add entries to the menu.
        MenuEntries.Add(resumeGameMenuEntry);
        MenuEntries.Add(saveGameMenuEntry);
        MenuEntries.Add(quitGameMenuEntry);
    }

    /// <summary>
    /// Speichert das laufende Spiel; das Pausenmenü bleibt offen.
    /// VERTRAG: game = ScreenManager.GetScreens().OfType&lt;RTSGameplayScreen&gt;().LastOrDefault();
    /// ohne Spielbildschirm geschieht nichts. Sonst in einem try:
    /// new SaveGameStore().Save(game.CaptureSave()) und danach ShowToast("Spiel gespeichert").
    /// Jede Ausnahme (catch Exception ex): ShowToast("Speichern fehlgeschlagen: " + ex.Message).
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
    private void SaveGameMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        RTSGameplayScreen game = ScreenManager.GetScreens().OfType<RTSGameplayScreen>().LastOrDefault();
        if (game == null)
            return;

        try
        {
            new SaveGameStore().Save(game.CaptureSave());
            ShowToast("Spiel gespeichert");
        }
        catch (Exception ex)
        {
            ShowToast("Speichern fehlgeschlagen: " + ex.Message);
        }
    }

    /// <summary>Eine kurze Meldung über dem Menü, die nach <see cref="TOAST_DURATION"/> verschwindet.</summary>
    private void ShowToast(string text)
    {
        ScreenManager.AddScreen(new MessageBoxScreen(text, false, TOAST_DURATION, true), ControllingPlayer);
    }

    /// <summary>
    /// Event handler for when the Quit Game menu entry is selected.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
    private void QuitGameMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        string message = "Spiel wirklich beenden?" + Environment.NewLine
                       + "Nicht gespeicherter Fortschritt geht verloren." + Environment.NewLine
                       + Environment.NewLine
                       + "Ja: Enter oder Leertaste" + Environment.NewLine
                       + "Nein: Esc";

        MessageBoxScreen confirmQuitMessageBox = new MessageBoxScreen(message, false, TimeSpan.Zero);

        confirmQuitMessageBox.Accepted += ConfirmQuitMessageBoxAccepted;

        ScreenManager.AddScreen(confirmQuitMessageBox, ControllingPlayer);
    }

    /// <summary>
    /// Event handler for when the user selects ok on the "are you sure you want to quit" message box.
    /// This uses the loading screen to transition from the game back to the main menu screen.
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
    private void ConfirmQuitMessageBoxAccepted(object sender, PlayerIndexEventArgs e)
    {
        LoadingScreen.Load(ScreenManager, false, null, new BackgroundScreen(), new MainMenuScreen());
    }
}
