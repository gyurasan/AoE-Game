using System;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// „Spiel laden“ als Liste: ein Eintrag je Spielstand (neuester zuerst, Text ist
/// <see cref="SaveSlot.Label"/> - Speicherzeit, Zeitalter, Spieldauer), darunter
/// „Zurück“. Die Löschtaste (Entf / Y) auf einem Platz löst sein Löschen aus -
/// nach Ja/Nein-Bestätigung (<see cref="SaveGameStore.DeleteSlot"/>), und die Liste
/// baut sich frisch auf.
/// Wie das Pausenmenü eine Tafel im Stil des Hauptmenüs (<see cref="MenuScreen"/>).
/// Entstand für die Nutzeridee „ich will viele Spiele speichern und im Menü alle
/// Ladeplätze sehen“ (2026-10-22); das Löschen der Plätze kam mit
/// „SV Spielstand-Plätze: Plätze löschen“ (2026-10-23).
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
        BaueEintraege();
    }

    /// <summary>
    /// VERTRAG: die Listeninhalte in der Reihenfolge des
    /// <see cref="LoadGameScreen"/>-Konstruktorvertrags. Läuft im Konstruktor und
    /// nach jedem <see cref="NeuAufbauen"/>.
    /// </summary>
    private void BaueEintraege()
    {
        // Ein Eintrag je Spielstand; ListSaves liefert schon den neuesten zuerst.
        foreach (var slot in SaveGameStore.ListSaves())
        {
            var eintrag = new MenuEntry(slot.Label) { Deletable = true };
            eintrag.Selected += (sender, e) => LoadSlot(slot.FilePath, e.PlayerIndex);
            eintrag.DeleteSelected += (sender, e) => FragLoeschen(slot.Label, slot.FilePath, e.PlayerIndex);
            MenuEntries.Add(eintrag);
        }

        MenuEntry zurueck = new MenuEntry("Zurück");
        zurueck.Selected += OnCancel;
        MenuEntries.Add(zurueck);
    }

    /// <summary>
    /// VERTRAG: nach gelöschtem Platz die Liste ersetzen - MenuEntries leeren,
    /// mit BaueEintraege() füllen, die Auswahl in der neuen Liste
    /// in Reichweite halten (ClampSelectedEntry).
    /// </summary>
    private void NeuAufbauen()
    {
        MenuEntries.Clear();
        BaueEintraege();
        ClampSelectedEntry();
    }

    /// <summary>
    /// Fragt nach, ob der Spielstand gelöscht werden soll (Ja/Nein).
    /// VERTRAG: ein MessageBoxScreen mit „Spielstand löschen?“, der Platz-Bezeichnung
    /// und dem Hinweis, dass sich das nicht rückgängig machen lässt; sein
    /// Accepted-Ereignis ruft LoeschePlatz(filePath, e.PlayerIndex) auf.
    /// </summary>
    private void FragLoeschen(string label, string filePath, PlayerIndex playerIndex)
    {
        MessageBoxScreen frag = new MessageBoxScreen(
            "Spielstand löschen?\n" + label + "\nDies lässt sich nicht rückgängig machen.");
        frag.Accepted += (sender, e) => LoeschePlatz(filePath, e.PlayerIndex);
        ScreenManager.AddScreen(frag, playerIndex);
    }

    /// <summary>
    /// Löscht die Platzei und aktualisiert die Liste.
    /// VERTRAG: in einem try: SaveGameStore.DeleteSlot(filePath), NeuAufbauen() und
    /// die Toast-Meldung „Spielstand gelöscht“ (MessageBoxScreen als Toast, 1,5 s).
    /// Jede Ausnahme (catch Exception ex): die Liste bleibt, wie sie war, und der
    /// Toast lautet „Löschen fehlgeschlagen: “ + ex.Message.
    /// </summary>
    private void LoeschePlatz(string filePath, PlayerIndex playerIndex)
    {
        try
        {
            SaveGameStore.DeleteSlot(filePath);
            NeuAufbauen();
            ScreenManager.AddScreen(new MessageBoxScreen("Spielstand gelöscht", false, TimeSpan.FromSeconds(1.5), true),
                                    playerIndex);
        }
        catch (Exception ex)
        {
            ScreenManager.AddScreen(new MessageBoxScreen("Löschen fehlgeschlagen: " + ex.Message, false,
                                                         TimeSpan.FromSeconds(3), true), playerIndex);
        }
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
