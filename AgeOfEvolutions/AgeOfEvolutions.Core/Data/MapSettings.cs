namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Wie viel von jedem abbaubaren Rohstoff eine zufällige Karte trägt. Die
/// Kartengenerator-Funktionen (AddResourcePatches &amp; Co.) lesen ausschließlich
/// diese Werte, ein Menü kann sie also ändern und daraus eine neue Karte bauen,
/// ohne die Generatoren anzufassen.
/// </summary>
/// <remarks>
/// Zahlenpaare sind <c>Min</c>/<c>Max</c> (jeweils inclusive): die Karte streut
/// pro Zufallswert zwischen Min und Max. <c>Default</c> hält die reichere
/// Grundausstattung gegenüber der früheren festeren Werte — rund 30 % mehr
/// von jedem abbaubaren Klumpen, rund 50 % mehr Schafe.
/// </remarks>
public class MapSettings
{
    // --- Holz ----------------------------------------------------------
    /// <summary>Anzahl der Waldklumpen (Min/Max, inclusive).</summary>
    public int WoodClustersMin { get; set; } = 9;
    public int WoodClustersMax { get; set; } = 13;
    /// <summary>Radius eines Waldklumpens (Min/Max, inclusive, Kacheln).</summary>
    public int WoodRadiusMin { get; set; } = 3;
    public int WoodRadiusMax { get; set; } = 5;

    // --- Stein ---------------------------------------------------------
    public int StoneClustersMin { get; set; } = 3;
    public int StoneClustersMax { get; set; } = 5;
    public int StoneRadiusMin { get; set; } = 2;
    public int StoneRadiusMax { get; set; } = 4;

    // --- Gold ----------------------------------------------------------
    public int GoldClustersMin { get; set; } = 2;
    public int GoldClustersMax { get; set; } = 4;
    public int GoldRadiusMin { get; set; } = 1;
    public int GoldRadiusMax { get; set; } = 3;

    // --- Fisch ---------------------------------------------------------
    /// <summary>Trefferquote je Küstenkachel für einen Fischschwarm (0..1).</summary>
    public float FishChance { get; set; } = 0.20f;

    // --- Beeren --------------------------------------------------------
    /// <summary>Wahrscheinlichkeit (0..1), dass an einer Waldlichtung Büsche wachsen.</summary>
    public float BerryChance { get; set; } = 0.85f;
    public int BerriesMin { get; set; } = 3;
    public int BerriesMax { get; set; } = 5;

    // --- Schafe --------------------------------------------------------
    /// <summary>Anzahl der Schafherden auf der Karte (Min/Max, inclusive).</summary>
    public int SheepHerdsMin { get; set; } = 6;
    public int SheepHerdsMax { get; set; } = 9;
    /// <summary>Schafe je Herde (Min/Max, inclusive).</summary>
    public int SheepPerHerdMin { get; set; } = 6;
    public int SheepPerHerdMax { get; set; } = 14;
    /// <summary>Nahrung, die ein einzelnes Schaf trägt (Äquivalent zu ~100).</summary>
    public int SheepFood { get; set; } = 100;

    // --- Schaf-Wanderung ----------------------------------------------
    /// <summary>Sekunden bis ein unreserviertes Schaf die Kachel wechselt (Min/Max).</summary>
    public float SheepWanderSecondsMin { get; set; } = 1.5f;
    public float SheepWanderSecondsMax { get; set; } = 4.0f;
    /// <summary>
    /// Kacheln, in denen ein Schaf maximal wandern darf. 1 = nur die acht
    /// direkt benachbarten Kacheln - ein Sprung über den Hügel wirkt wie
    /// Teleportieren.
    /// </summary>
    public int SheepWanderRadius { get; set; } = 1;

    /// <summary>Die reichere Standardausstattung.</summary>
    public static MapSettings Default => new();
}
