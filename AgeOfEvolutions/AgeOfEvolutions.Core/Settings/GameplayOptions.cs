namespace AgeOfEvolutions.Core.Settings;

/// <summary>
/// Die erlaubten Wertschritte der zehn Spielmöglichkeiten in den Einstellungen.
/// Das Menü blätter bei jedem Klick auf die nächste Stufe (zurück zum Anfang,
/// wenn die höchste erreicht ist) und zeigt die gewählte Stufe an; der
/// Spielbildschirm liest die rohe Zahl aus den Einstellungen und rechnet selbst
/// die Einheit. Die Defaults in <see cref="AgeOfEvolutionsSettings"/> sind
/// exakt die bisherigen Hartwerte des Spiels.
/// </summary>
public static class GameplayOptions
{
    // --- Lautstärke (Prozent) --------------------------------------------
    public static readonly int[] SoundVolume = { 0, 25, 50, 75, 100 };

    // --- KI-Schwierigkeit (Index) -----------------------------------------
    // Drei Stufen seit 2026-11-07: Defense greift nie an, Normal in Wellen,
    // This is Sparta in Aufstellung (AoE.Core.Ai.AiProfile.ForLevel)
    public static readonly int[] AiDifficulty = { 0, 1, 2 };
    public static readonly string[] AiNames = { "Defense", "Normal", "This is Sparta" };

    /// <summary>Name einer gespeicherten KI-Stufe; ältere Einstellungen kannten noch
    /// Stufe 3 (Extrem), die heute This is Sparta ist.</summary>
    public static string AiName(int level) => AiNames[System.Math.Clamp(level, 0, AiNames.Length - 1)];

    // --- Startkapital (Index) ----------------------------------------------
    public static readonly int[] StartingCapital = { 0, 1, 2 };
    public static readonly string[] CapitalNames = { "Arm", "Normal", "Reich" };

    // --- Bevölkerungsgrenze (Plätze) ----------------------------------------
    public static readonly int[] PopulationCap = { 100, 200, 400 };

    // --- Tempos (Prozent) ---------------------------------------------------
    public static readonly int[] UnitSpeed = { 50, 100, 150, 200 };
    public static readonly int[] GatherSpeed = { 50, 100, 150, 200 };
    public static readonly int[] ProductionSpeed = { 100, 150, 200, 400 };

    // --- Kämpfe (Hundertstel-Kacheln) ----------------------------------------
    public static readonly int[] MeleeStopTiles = { 25, 50, 100, 150 };
    public static readonly int[] FormationSpacingTiles = { 25, 50, 100 };

    // --- Kamera (Bildschirm-Pixel/Secunde) ------------------------------------
    public static readonly int[] CameraPanSpeed = { 400, 800, 1600 };

    /// <summary>
    /// Die nächste Stufe nach <paramref name="current"/> in der Liste
    /// <paramref name="steps"/>; über der höchsten Stufe geht es zurück zur
    /// ersten ("blätter durcheinander," wie die Kartenwahl im Hauptmenü).
    /// </summary>
    public static int Next(int current, int[] steps)
    {
        foreach (int value in steps)
            if (value > current)
                return value;
        return steps[0];
    }

    /// <summary>Prozentzahl als Mehrfach-Anzeige: 50 → "0.5×", 100 → "1×".</summary>
    public static string Percent(int percent)
        => (percent / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + "×";

    /// <summary>Hundertstel-Kachel als Anzeige: 50 → "0.5 Kachel".</summary>
    public static string TileHunderts(int hunderts)
        => (hunderts / 100f).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture) + " Kachel";
}
