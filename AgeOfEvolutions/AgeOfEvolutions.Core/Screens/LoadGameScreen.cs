using System;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// „Spiel laden" als Liste: ein Eintrag je Spielstand (neuester zuerst, Text ist
/// <see cref="SaveSlot.Label"/> - Speicherzeit, Zeitalter, Spieldauer), darunter
/// „Zurück". Wie das Pausenmenü eine Tafel im Stil des Hauptmenüs (<see cref="MenuScreen"/>).
/// Entstand für die Nutzeridee „ich will viele Spiele speichern und im Menü alle
/// Ladeplätze sehen" (2026-10-22).
/// </summary>
internal class LoadGameScreen : MenuScreen
{
    /// <summary>
    /// VERTRAG: basis „Spiel laden". Für jeden Platz aus SaveGameStore.ListSaves()
    /// einen MenuEntry(slot.Label), dessen Selected-Ereignis LoadSlot(slot.FilePath,
    /// e.PlayerIndex) aufruft; danach genau ein Eintrag „Zurück", dessen Selected
    /// an OnCancel hängt. Jeder Eintrag kommt in dieser Reihenfolge in MenuEntries.
    /// Gibt es keine Spielstände, bleibt nur „Zurück" (das Hauptmenü öffnet die Liste
    /// ohnehin nur, wenn es mindestens einen Stand gibt).
    /// </summary>
    public LoadGameScreen()
        : base("Spiel laden")
    {
        // Ein Eintrag je Spielstand; ListSaves liefert schon den neuesten zuerst.
        foreach (var slot in SaveGameStore.ListSaves())
        {
            var eintrag = new MenuEntry(slot.Label);
            eintrag.Selected += (sender, e) => LoadSlot(slot.FilePath, e.PlayerIndex);
            MenuEntries.Add(eintrag);
        }

        MenuEntry zurueck = new MenuEntry("Zurück");
        zurueck.Selected += OnCancel;
        MenuEntries.Add(zurueck);
    }

    /// <summary>
    /// Setzt den Spielstand aus <paramref name="filePath"/> fort.
    /// VERTRAG: in einem try: save = new SaveGameStore(filePath).Load();
    /// SaveGameMapper.Restore(save) (Ergebnis verwerfen - ein kaputter Stand fällt so
    /// hier auf und nicht erst im Ladebildschirm); dann
    /// LoadingScreen.Load(ScreenManager, true, ControllingPlayer, new RTSGameplayScreen(save)).
    /// Jede Ausnahme (catch Exception ex): diese Liste bleibt offen und
    /// ScreenManager.AddScreen(new MessageBoxScreen("Spielstand nicht lesbar: " + ex.Message,
    /// false, TimeSpan.FromSeconds(3), true), ControllingPlayer) zeigt den Grund.
    /// </summary>
    private void LoadSlot(string filePath, PlayerIndex playerIndex)
    {
        try
        {
            var save = new SaveGameStore(filePath).Load();
            SaveGameMapper.Restore(save);   // zur Probe: ein kaputter Stand fällt hier auf
            LoadingScreen.Load(ScreenManager, true, ControllingPlayer ?? playerIndex, new RTSGameplayScreen(save));
        }
        catch (Exception ex)
        {
            ScreenManager.AddScreen(new MessageBoxScreen("Spielstand nicht lesbar: " + ex.Message, false,
                                                         TimeSpan.FromSeconds(3), true), ControllingPlayer);
        }
    }
}
