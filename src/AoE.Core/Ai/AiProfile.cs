namespace AoE.Core.Ai;

/// <summary>
/// Schwierigkeitsgrad der Regel-KI: ein Satz von Zahlen, die das Tempo und die
/// Härte des Spiels steuern — <b>dasselbe Regelwerk, andere Werte</b>, genau wie
/// in Age of Empires II (gleiche Stellschrauben, je Stufe unterschiedlich).
/// Die Profile sind bewusst klein, deterministisch und vergleichbar; es ist
/// kein zweites Regelwerk.
///
/// Default ist <see cref="Standard"/> — <see cref="EconomyAi.EconomyAi"/>
/// erzeugt ohne Argument eine Standard-KI, der alte Stand war die leicht
/// schwächere Mischung aus Einzelangriffen und festem Reihenfolge-Sammeln.
/// </summary>
public sealed record AiProfile
{
    /// <summary>Bei weniger als dieser Pop-Restplatz (Kapazität minus
    /// Besetzung) baut die KI ein Haus — nicht erst am Limit.</summary>
    public int HouseHeadroom { get; init; }

    /// <summary>Wie viele Soldaten die KI im Bestand hält, bevor sie
    /// aufhört, neue auszubilden (im Feld plus in Produktion).</summary>
    public int SoldierTarget { get; init; }

    /// <summary>Mindestgröße einer Angriffsgruppe: unter dieser Zahl zieht
    /// die KI bei einem <b>fernen</b> Feind nicht los. Ein Angriff auf die
    /// eigene Basis (<see cref="HomeDefenseRadius"/>) ist davon ausgenommen
    /// (Verteidigung braucht keine Gruppe).</summary>
    public int RaidMinSoldiers { get; init; }

    /// <summary>So viele Soldaten bleiben bei einem Angriff im Dorf
    /// (Heimwache).</summary>
    public int HomeGuard { get; init; }

    /// <summary>Sekunden zwischen zwei Angriffswellen.</summary>
    public double RaidCooldownSeconds { get; init; }

    /// <summary>Maximal so viele Dorfbewohner springen als Verteidigung zu.</summary>
    public int DefenseMax { get; init; }

    /// <summary>Feinde innerhalb dieses Radius um das Stadtzentrum gelten als
    /// Angriff auf das Dorf: die Armee verteidigt sofort, auch unter
    /// <see cref="RaidMinSoldiers"/>.</summary>
    public int HomeDefenseRadius { get; init; }

    // -------------------------------------------------------------------
    // Die vier Stufen — eine Kurve der Zahlen, kein zweites Regelwerk.
    // -------------------------------------------------------------------

    /// <summary>Leicht: spätere, kleinere Wellen; Verteidigungsorientiert.</summary>
    public static AiProfile Easy { get; } = new()
    {
        HouseHeadroom     = 2,
        SoldierTarget     = 4,
        RaidMinSoldiers   = 3,
        HomeGuard         = 1,
        RaidCooldownSeconds = 14,
        DefenseMax        = 3,
        HomeDefenseRadius = 8,
    };

    /// <summary>Mittel: ausbalanciert — die Default-KI.</summary>
    public static AiProfile Standard { get; } = new()
    {
        HouseHeadroom     = 3,
        SoldierTarget     = 8,
        RaidMinSoldiers   = 5,
        HomeGuard         = 1,
        RaidCooldownSeconds = 8,
        DefenseMax        = 4,
        HomeDefenseRadius = 10,
    };

    /// <summary>Schwer: frühere, größere Wellen, dichterer Haushalt.</summary>
    public static AiProfile Hard { get; } = new()
    {
        HouseHeadroom     = 4,
        SoldierTarget     = 12,
        RaidMinSoldiers   = 6,
        HomeGuard         = 1,
        RaidCooldownSeconds = 6,
        DefenseMax        = 5,
        HomeDefenseRadius = 12,
    };

    /// <summary>Extrem: frühe Großwellen, hohe Heimwache, enges Haus.</summary>
    public static AiProfile Extreme { get; } = new()
    {
        HouseHeadroom     = 5,
        SoldierTarget     = 16,
        RaidMinSoldiers   = 8,
        HomeGuard         = 2,
        RaidCooldownSeconds = 5,
        DefenseMax        = 6,
        HomeDefenseRadius = 14,
    };

    /// <summary>
    /// Testprofil (tools/ai-pruefung): sehr aggressive, schnelle Wellen,
    /// damit auf der halben Testkarte innerhalb kurzer Echtzeit ein
    /// Dorfzentrum fällt und ein eindeutiger Sieg feststeht. Nurmehrer
    /// Zahlen, kein zweites Regelwerk.
    /// </summary>
    public static AiProfile Aggressive { get; } = new()
    {
        HouseHeadroom     = 2,
        SoldierTarget     = 14,
        RaidMinSoldiers   = 4,
        HomeGuard         = 1,
        RaidCooldownSeconds = 3,
        DefenseMax        = 4,
        HomeDefenseRadius = 12,
    };
}
