using System;

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

    // --- Seen ----------------------------------------------------------
    /// <summary>Seen auf der Karte (Min/Max, inclusive).</summary>
    public int LakesMin { get; set; } = 2;
    public int LakesMax { get; set; } = 4;

    // --- Beeren --------------------------------------------------------
    /// <summary>Wahrscheinlichkeit (0..1), dass an einer Waldlichtung Büsche wachsen.</summary>
    public float BerryChance { get; set; } = 0.95f;
    /// <summary>
    /// Büsche je Waldlichtung (Min/Max, inclusive) — seit 2026-10-04 doppelt so
    /// viele, seit 2026-10-09 noch mal spürbar mehr, damit Beeren gut auffindbar sind.
    /// </summary>
    public int BerriesMin { get; set; } = 9;
    public int BerriesMax { get; set; } = 16;

    // --- Schafe --------------------------------------------------------
    /// <summary>Anzahl der Schafherden auf der Karte (Min/Max, inclusive).</summary>
    public int SheepHerdsMin { get; set; } = 5;
    public int SheepHerdsMax { get; set; } = 9;
    /// <summary>
    /// Schafe je Herde (Min/Max, inclusive) — seit 2026-10-09 deutlich kleiner
    /// (vorher 6–14): die Karte war übersät von Schafen.
    /// </summary>
    public int SheepPerHerdMin { get; set; } = 6;
    public int SheepPerHerdMax { get; set; } = 8;
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

    // --- Wild (Rehe) ----------------------------------------------------
    /// <summary>
    /// Anzahl der Reherden auf der Karte (Min/Max, inclusive). Die ersten beiden
    /// stehen je eine in Reichweite eines Stadtzentrums, siehe DeerStartDistanceMin.
    /// </summary>
    public int DeerHerdsMin { get; set; } = 5;
    public int DeerHerdsMax { get; set; } = 8;
    /// <summary>Rehe je Herde (Min/Max, inclusive).</summary>
    /// <remarks>
    /// Seit 2026-10-09 an die kleineren Schafherden angepasst: die Dichte bleibt
    /// ~70 % der Schaf-Dichte (schafseitig wurde die Herdengröße verkleinert).
    /// </remarks>
    public int DeerPerHerdMin { get; set; } = 3;
    public int DeerPerHerdMax { get; set; } = 5;
    /// <summary>
    /// Abstand der Startherde jedes Spielers zu seinem Stadtzentrum (Kacheln,
    /// waagerecht plus senkrecht) - in Reichweite, wie die Startjagd in AoE.
    /// </summary>
    public int DeerStartDistanceMin { get; set; } = 12;
    public int DeerStartDistanceMax { get; set; } = 20;
    /// <summary>Nahrung, die ein einzelnes Reh trägt — wertvoller als ein Schaf.</summary>
    public int DeerFood { get; set; } = 150;
    /// <summary>Sekunden bis ein unreserviertes Reh die Kachel wechselt (Min/Max).</summary>
    /// <remarks>Rehe sind rastloser als Schafe — kürzerer Takt, häufigere Sprünge.</remarks>
    public float DeerWanderSecondsMin { get; set; } = 1.0f;
    public float DeerWanderSecondsMax { get; set; } = 2.5f;
    /// <summary>Kacheln, in denen ein Reh maximal wandern darf (Rehe: 2 Sprünge weit).</summary>
    public int DeerWanderRadius { get; set; } = 1;

    // --- Kaninchen und Wildschweine --------------------------------------
    /// <summary>Kaninchengruppen auf der Karte und Kaninchen je Gruppe (Min/Max, inclusive).</summary>
    public int RabbitGroupsMin { get; set; } = 4;
    public int RabbitGroupsMax { get; set; } = 6;
    public int RabbitsPerGroupMin { get; set; } = 3;
    public int RabbitsPerGroupMax { get; set; } = 6;
    /// <summary>Nahrung je Kaninchen — wenig, dafür viele und flink.</summary>
    public int RabbitFood { get; set; } = 50;
    /// <summary>Sekunden bis ein Kaninchen weiterhoppelt (Min/Max).</summary>
    public float RabbitWanderSecondsMin { get; set; } = 0.6f;
    public float RabbitWanderSecondsMax { get; set; } = 1.8f;
    public int RabbitWanderRadius { get; set; } = 1;
    /// <summary>Wildschwein-Rotten auf der Karte und Tiere je Rotte (Min/Max, inclusive).</summary>
    public int BoarGroupsMin { get; set; } = 3;
    public int BoarGroupsMax { get; set; } = 5;
    public int BoarsPerGroupMin { get; set; } = 1;
    public int BoarsPerGroupMax { get; set; } = 3;
    /// <summary>Nahrung je Wildschwein — das ergiebigste Wild.</summary>
    public int BoarFood { get; set; } = 300;
    /// <summary>Sekunden bis ein Wildschwein weiterzieht (Min/Max) — gemächlich.</summary>
    public float BoarWanderSecondsMin { get; set; } = 2.0f;
    public float BoarWanderSecondsMax { get; set; } = 5.0f;
    public int BoarWanderRadius { get; set; } = 1;

    /// <summary>Die reichere Standardausstattung.</summary>
    public static MapSettings Default => new();

    /// <summary>
    /// Die Standardausstattung für eine Karte der Größe size: Wälder, Steinbrüche,
    /// Goldadern, Seen, Herden und Gruppen werden mit der Fläche mehr, damit eine
    /// große Karte genauso dicht besetzt ist wie die Standardkarte. Radien, Tiere je
    /// Herde und die Startausstattung bleiben, wie sie sind.
    /// </summary>
    public static MapSettings ForSize(MapSize size)
    {
        int side = MapSizes.Side(size), standard = MapSizes.Side(MapSize.Standard);
        float flaeche = side * side / (float)(standard * standard);
        int Mehr(int n) => Math.Max(1, (int)Math.Round(n * flaeche));
        var s = new MapSettings();
        s.WoodClustersMin = Mehr(s.WoodClustersMin);
        s.WoodClustersMax = Mehr(s.WoodClustersMax);
        s.StoneClustersMin = Mehr(s.StoneClustersMin);
        s.StoneClustersMax = Mehr(s.StoneClustersMax);
        s.GoldClustersMin = Mehr(s.GoldClustersMin);
        s.GoldClustersMax = Mehr(s.GoldClustersMax);
        s.LakesMin = Mehr(s.LakesMin);
        s.LakesMax = Mehr(s.LakesMax);
        s.SheepHerdsMin = Mehr(s.SheepHerdsMin);
        s.SheepHerdsMax = Mehr(s.SheepHerdsMax);
        s.DeerHerdsMin = Mehr(s.DeerHerdsMin);
        s.DeerHerdsMax = Mehr(s.DeerHerdsMax);
        s.RabbitGroupsMin = Mehr(s.RabbitGroupsMin);
        s.RabbitGroupsMax = Mehr(s.RabbitGroupsMax);
        s.BoarGroupsMin = Mehr(s.BoarGroupsMin);
        s.BoarGroupsMax = Mehr(s.BoarGroupsMax);
        return s;
    }
}

/// <summary>Die Kartengrößen, die das Hauptmenü anbietet. <c>Test</c> ist
/// die halbe Standard-Karte — für schnelle KI-vs-KI-Tests (tools/ai-pruefung).</summary>
public enum MapSize { Standard, Large, Max, Test }

/// <summary>Seitenlänge und Name der Kartengrößen.</summary>
public static class MapSizes
{
    /// <summary>
    /// Seitenlänge der quadratischen Referenzkarte: Standard 64, Groß 90 (fast
    /// die doppelte Fläche), Maximal 128 (die vierfache), Test 32 (die halbe
    /// Kantenlänge — ein Viertel der Fläche, für kurze Testläufe).
    /// </summary>
    public static int Side(MapSize size) => size switch
    {
        MapSize.Large => 90,
        MapSize.Max => 128,
        MapSize.Test => 32,
        _ => 64,
    };

    /// <summary>
    /// Kartenabmessungen (Kacheln, Breite × Höhe) in einem gegebenen Seitenverhältnis
    /// <paramref name="aspect"/> (Breite/Höhe, z. B. das Bildschirmformat 16/9):
    /// Standard 64, Groß 90, Maximal 128 Kacheln je Seite.
    ///
    /// Die Fläche bleibt exakt Side(size)² Kacheln, nur das Seitenverhältnis
    /// passt sich dem Bild an. So bleibt die Rohstoffdichte je Stufe identisch
    /// (MapSettings.ForSize rechnet über die Fläche), während die Karte wie der
    /// Bildschirm breit statt quadratisch ist.
    /// </summary>
    public static (int Width, int Height) Dimensions(MapSize size, float aspect)
    {
        float area = Side(size);
        area *= Side(size);
        if (aspect <= 0f)
            aspect = 1f;
        int w = Math.Max(1, (int)Math.Round(Math.Sqrt(area * aspect)));
        int h = Math.Max(1, (int)Math.Round(area / w));
        return (w, h);
    }

    /// <summary>Name der Größe im Hauptmenü.</summary>
    public static string Name(MapSize size) => size switch
    {
        MapSize.Large => "Groß",
        MapSize.Max => "Maximal",
        _ => "Standard",
    };
}
