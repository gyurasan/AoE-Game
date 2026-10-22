using System;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// „Spiel speichern" als Liste: erst „Neuer Spielstand" (nummeriert auf den nächsten
/// freien Platz, <see cref="SaveGameStore.NextSavePath"/>), dann ein Eintrag je
/// vorhandenem Spielstand - ihn zu wählen überschreibt ihn -, darunter „Zurück".
/// Entstand für die Nutzeridee „ich will viele Spiele speichern" (2026-10-22).
/// </summary>
internal class SaveGameScreen : MenuScreen
{
    /// <summary>So lange steht die Meldung wie „Spiel gespeichert" über dem Menü.</summary>
    private static readonly TimeSpan TOAST_DURATION = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// VERTRAG: basis „Spiel speichern". Zuerst der Eintrag „Neuer Spielstand",
    /// dessen Selected-Ereignis SaveTo(null, e.PlayerIndex) aufruft; für jeden Platz
    /// aus SaveGameStore.ListSaves() einen MenuEntry(slot.Label), dessen Selected
    /// SaveTo(slot.FilePath, e.PlayerIndex) aufruft (die Auswahl überschreibt diesen
    /// Platz); zuletzt „Zurück" mit Selected an OnCancel. Reihenfolge in MenuEntries
    /// genau so.
    /// </summary>
    public SaveGameScreen()
        : base("Spiel speichern")
    {
        // Zuerst der neue Platz, dann die vorhandenen zum Überschreiben (neuester zuerst).
        MenuEntry neuer = new MenuEntry("Neuer Spielstand");
        neuer.Selected += (sender, e) => SaveTo(null, e.PlayerIndex);
        MenuEntries.Add(neuer);

        foreach (var slot in SaveGameStore.ListSaves())
        {
            var eintrag = new MenuEntry(slot.Label);
            eintrag.Selected += (sender, e) => SaveTo(slot.FilePath, e.PlayerIndex);
            MenuEntries.Add(eintrag);
        }

        MenuEntry zurueck = new MenuEntry("Zurück");
        zurueck.Selected += OnCancel;
        MenuEntries.Add(zurueck);
    }

    /// <summary>
    /// Speichert das laufende Spiel.
    /// VERTRAG: game = ScreenManager.GetScreens().OfType&lt;RTSGameplayScreen&gt;().LastOrDefault();
    /// ohne Spielbildschirm geschieht nichts. Sonst in einem try:
    /// new SaveGameStore(filePath ?? SaveGameStore.NextSavePath()).Save(game.CaptureSave())
    /// (<paramref name="filePath"/> null heißt: der nächste freie Platz), danach
    /// ShowToast("Spiel gespeichert") und ExitScreen() - zurück ins Pausenmenü.
    /// Jede Ausnahme (catch Exception ex): die Liste bleibt offen und
    /// ShowToast("Speichern fehlgeschlagen: " + ex.Message).
    /// </summary>
    private void SaveTo(string filePath, PlayerIndex playerIndex)
    {
        RTSGameplayScreen game = ScreenManager.GetScreens().OfType<RTSGameplayScreen>().LastOrDefault();
        if (game == null)
            return;

        try
        {
            // filePath null heißt: der nächste freie Platz.
            new SaveGameStore(filePath ?? SaveGameStore.NextSavePath()).Save(game.CaptureSave());
            ShowToast("Spiel gespeichert");
            ExitScreen();   // zurück ins Pausenmenü
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
}
