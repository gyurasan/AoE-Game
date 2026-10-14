using System;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Screens;

/// <summary>
/// Helper class represents a single entry in a MenuScreen: its text, whether
/// it can be chosen, and an event raised when it is selected. Lage und
/// Aussehen bestimmt der <see cref="MenuScreen"/>, der alle Einträge auf
/// seiner Tafel anordnet.
/// </summary>
class MenuEntry
{
    string text;
    /// <summary>
    /// Gets or sets the text of this menu entry.
    /// </summary>
    public string Text
    {
        get { return text; }
        set { text = value; }
    }

    private bool enabled;
    /// <summary>
    /// Whether this menu option is enabled or not.
    /// If not it will be displayed but skipped over during navigation
    /// </summary>
    public bool Enabled
    {
        get { return enabled; }
        set { enabled = value; }
    }

    /// <summary>
    /// Event raised when the menu entry is selected.
    /// </summary>
    public event EventHandler<PlayerIndexEventArgs> Selected;

    /// <summary>
    /// Method for raising the Selected event.
    /// </summary>
    /// <param name="playerIndex">The player index that selected the entry.</param>
    protected internal virtual void OnSelectEntry(PlayerIndex playerIndex)
    {
        Selected?.Invoke(this, new PlayerIndexEventArgs(playerIndex));
    }

    /// <summary>
    /// Constructs a new menu entry with the specified text.
    /// </summary>
    /// <param name="text">The text to display for the menu entry.</param>
    /// <param name="enabled">Indicates whether the menu entry is enabled.</param>
    public MenuEntry(string text, bool enabled = true)
    {
        this.text = text;
        this.enabled = enabled;
    }
}
