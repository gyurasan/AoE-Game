using System;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// „Spiel speichern“ als Liste: erst „Neuer Spielstand“ (nummeriert auf den nächsten
/// freien Platz, <see cref="SaveGameStore.NextSavePath"/>), dann ein Eintrag je
/// vorhandenem Spielstand - ihn zu wählen überschreibt ihn, die Löschtaste
/// (Entf / Y) löst sein Löschen aus - , darunter „Zurück“.
/// Entstand für die Nutzeridee „ich will viele Spiele speichern“ (2026-10-22);
/// das Löschen der Plätze kam mit „SV Spielstand-Plätze: Plätze löschen“ (2026-10-23).
/// </summary>
internal class SaveGameScreen : MenuScreen
{
    /// <summary>So lange steht die Meldung wie „Spiel gespeichert" über dem Menü.</summary>
    private static readonly TimeSpan TOAST_DURATION = TimeSpan.FromSeconds(1.5);

    /// <summary>
    /// VERTRAG: basis „Spiel speichern“. Zuerst der Eintrag „Neuer Spielstand“,
    /// dessen Selected-Ereignis SaveTo(null, e.PlayerIndex) aufruft; für jeden Platz
    /// aus SaveGameStore.ListSaves() einen MenuEntry(slot.Label), dessen Selected
    /// SaveTo(slot.FilePath, e.PlayerIndex) aufruft (die Auswahl überschreibt diesen
    /// Platz) und dessen Deletable gesetzt ist, und dessen DeleteSelected
    /// FragLoeschen(slot.Label, slot.FilePath, e.PlayerIndex) aufruft; zuletzt
    /// „Zurück“ mit Selected an OnCancel. Reihenfolge in MenuEntries genau so.
    /// </summary>
    public SaveGameScreen()
        : base("Spiel speichern")
    {
        BaueEintraege();
    }

    /// <summary>
    /// VERTRAG: die Listeninhalte in der Reihenfolge des
    /// <see cref="SaveGameScreen"/>-Konstruktorvertrags. Läuft im Konstruktor und
    /// nach jedem <see cref="NeuAufbauen"/>.
    /// </summary>
    private void BaueEintraege()
    {
        // Zuerst der neue Platz, dann die vorhandenen zum Überschreiben (neuester zuerst).
        MenuEntry neuer = new MenuEntry("Neuer Spielstand");
        neuer.Selected += (sender, e) => SaveTo(null, e.PlayerIndex);
        MenuEntries.Add(neuer);

        foreach (var slot in SaveGameStore.ListSaves())
        {
            var eintrag = new MenuEntry(slot.Label) { Deletable = true };
            eintrag.Selected += (sender, e) => SaveTo(slot.FilePath, e.PlayerIndex);
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
    /// ShowToast("Spielstand gelöscht").
    /// Jede Ausnahme (catch Exception ex): die Liste bleibt, wie sie war, und
    /// ShowToast("Löschen fehlgeschlagen: " + ex.Message).
    /// </summary>
    private void LoeschePlatz(string filePath, PlayerIndex playerIndex)
    {
        try
        {
            SaveGameStore.DeleteSlot(filePath);
            NeuAufbauen();
            ShowToast("Spielstand gelöscht");
        }
        catch (Exception ex)
        {
            ShowToast("Löschen fehlgeschlagen: " + ex.Message);
        }
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
