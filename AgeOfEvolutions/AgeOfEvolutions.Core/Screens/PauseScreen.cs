using System;
using AgeOfEvolutions.Core.Screens;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// The pause menu comes up over the top of the game (Esc), giving the player
/// options to resume, save or quit. Deutsch wie das Hauptmenü. „Spiel speichern"
/// öffnet die Platzliste <see cref="SaveGameScreen"/> - es gibt mehrere Plätze.
/// </summary>
class PauseScreen : MenuScreen
{
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
    /// Öffnet die Platzliste zum Speichern (neuer Spielstand oder überschreiben).
    /// VERTRAG: ScreenManager.AddScreen(new SaveGameScreen(), ControllingPlayer).
    /// </summary>
    /// <param name="sender">The source of the event.</param>
    /// <param name="e">The <see cref="PlayerIndexEventArgs"/> instance containing the event data.</param>
    private void SaveGameMenuEntrySelected(object sender, PlayerIndexEventArgs e)
    {
        ScreenManager.AddScreen(new SaveGameScreen(), ControllingPlayer);
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
