using System;

namespace AoE.Core.Combat;

/// <summary>
/// Belagerungsregeln (F15) — reine Logik ohne MonoGame, analog <see cref="BuildingCombat"/>.
/// Alle Methoden tragen den Kern-Typnamen als Zeichenkette, genauso wie
/// <see cref="AoE.Core.Entities.UnitEntity.Type"/> ihn liefert: "Ram", "Mangonel",
/// "Trebuchet". Das Spielbild kennt dafür "Catapult" für den Mangonel; die Umrechnung
/// liegt im Spiel (CoreUnits.PopulationSlots).
/// </summary>
public static class SiegeRules
{
    /// <summary>Sekunden, die ein Tribok zum Aufbauen braucht.</summary>
    public const float SETUP_SECONDS = 4f;

    /// <summary>Sekunden, die ein Tribok zum Abbauen braucht, bevor er wieder fährt.</summary>
    public const float PACK_SECONDS = 2f;

    /// <summary>
    /// Bevölkerungsplätze, die ein Gerät belegt (F15): Rammbock 2, Mangonel 2,
    /// Tribok 3 — alles andere 1, wie bisher jede Einheit.
    /// </summary>
    public static int Slots(string type) => type switch
    {
        "Ram" => 2,
        "Mangonel" => 2,
        "Trebuchet" => 3,
        _ => 1,
    };

    /// <summary>
    /// Mindestreichweite in Kacheln (F15): der Mangonel wirft erst ab 3 Kacheln —
    /// steht ein Ziel näher, muss das Gerät zurückweichen. Alle andern 0.
    /// </summary>
    public static float MinRangeTiles(string type) => type == "Mangonel" ? 3f : 0f;

    /// <summary>
    /// Flächenradius in Kacheln um den Einschlag (F15): Mangonel und Tribok treffen
    /// auch Gegner binnen 1 Kachel Luftlinie vom Getroffenen. Alle andern 0.
    /// </summary>
    public static float SplashRadiusTiles(string type) =>
        type is "Mangonel" or "Trebuchet" ? 1f : 0f;

    /// <summary>
    /// Schaden für Nebenleute im Flächenradius: die Hälfte des Hauptschlags,
    /// gerundet abwärts, mindestens 1 — bei 0 Hauptschlag kein Flächenschaden.
    /// </summary>
    public static int SplashDamage(int primary) => primary <= 0 ? 0 : Math.Max(1, primary / 2);

    /// <summary>
    /// Greift dieses Gerät nur Gebäude an (F15)? Nur der Tribok: gegen Einheiten
    /// zielt er nicht (auch nicht per Auto-Antwort); seinen Flächenschaden an der
    /// Wand bekommen gegnerische Einheiten im Radius trotzdem ab.
    /// </summary>
    public static bool OnlyBuildings(string type) => type == "Trebuchet";

    /// <summary>
    /// Muss dieses Gerät erst aufgebaut werden, um zu schießen (F15)? Nur der Tribok;
    /// Fahr- und Aufbaubilder trägt das Spiel (RTSGameplayScreen.Belagerung.cs).
    /// </summary>
    public static bool IsDeployable(string type) => type == "Trebuchet";

    /// <summary>
    /// Schaden des Geräts gegen EINHEITEN (F15): der Rammbock schlägt gegen Einheiten
    /// fast nichts — sein Schlag ist auf 1 gedeckelt (die 125 gehen nur gegen Gebäude).
    /// Andere Geräte behalten den berechneten Schaden; 0 und weniger bleiben unangetastet.
    /// </summary>
    public static int DamageToUnits(string type, int calculated) =>
        type == "Ram" ? Math.Min(1, calculated) : calculated;
}
