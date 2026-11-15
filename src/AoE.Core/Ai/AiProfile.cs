namespace AoE.Core.Ai;

/// <summary>
/// Schwierigkeitsgrad der Regel-KI: ein Satz von Zahlen und Schaltern, die Tempo,
/// Härte und Spielweise steuern. Seit 2026-11-07 drei Stufen (Nutzer: „Defense,
/// Normal und This is Sparta"), die sich nicht nur in Zahlen, sondern im Verhalten
/// unterscheiden:
/// <list type="bullet">
///   <item><see cref="Defense"/>: greift nie von selbst an, bereitet sich aber auf
///         Angriffe vor (Wachtürme, Bogenschützen, Armee in Aufstellung vor dem Dorf)
///         und verteidigt sich.</item>
///   <item><see cref="Standard"/> („Normal"): greift selbst an, in Wellen wie bisher.</item>
///   <item><see cref="Sparta"/> („This is Sparta"): die schwerste Stufe — gemischte
///         Armee, Angriff in Aufstellung (<see cref="ArmyCommander"/>): Nahkämpfer vorn,
///         Bogenschützen dahinter, Reiter an den Flanken, die im Schwall angreifen und
///         sich wieder zurückziehen.</item>
/// </list>
/// Die Profile sind feste Instanzen (Vergleich per <see cref="object.ReferenceEquals"/>).
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

    /// <summary>Greift die KI von selbst an (Angriffswellen)? <see cref="Defense"/>:
    /// nein — ihre Soldaten verlassen das Dorf nur, um Eindringlinge im
    /// <see cref="HomeDefenseRadius"/> zu schlagen.</summary>
    public bool Raids { get; init; } = true;

    /// <summary>Die Armee kämpft und marschiert in Aufstellung, geführt vom
    /// <see cref="ArmyCommander"/> (Defense und Sparta). Aus: die alten Wellen
    /// (jeder Soldat läuft einzeln aufs Ziel).</summary>
    public bool FormationTactics { get; init; }

    /// <summary>Anteil der Bogenschützen an der Armee in Prozent (Schießstand,
    /// ab der Feudalzeit). 0: wie bisher nur Milizen, solange eine Kaserne steht.</summary>
    public int ArcherPercent { get; init; }

    /// <summary>Anteil der Reiter (Späher aus dem Stall, ab der Feudalzeit) an der
    /// Armee in Prozent. Größer 0: die KI baut einen Stall.</summary>
    public int CavalryPercent { get; init; }

    /// <summary>Anteil der Belagerungsgeräte (Katapult aus der Belagerungswerkstatt,
    /// ab der Ritterzeit) an der Armee in Prozent (F15). Größer 0: die KI baut eine
    /// Werkstatt — ohne Geräte kommt eine Wellenarmee gegen Türme und Mauern kaum
    /// durch (gemessen 2026-11-07: Sparta bricht Defense in 30 min nicht).</summary>
    public int SiegePercent { get; init; }

    /// <summary>So viele Wachtürme baut die KI ab der Feudalzeit am Dorf.</summary>
    public int TowerCount { get; init; }

    /// <summary>So viele Soldaten müssen im Feld stehen, bevor die KI ins nächste Zeitalter
    /// aufsteigt (dazu eine fertige Kaserne). Bis 2026-11-07 war das
    /// <see cref="RaidMinSoldiers"/> - Defense greift nie an und käme so nie aus der Dunklen Zeit.</summary>
    public int AgeUpArmy { get; init; } = 5;

    // -------------------------------------------------------------------
    // Die drei Stufen des Menüs, dazu das Testprofil.
    // -------------------------------------------------------------------

    /// <summary>Defense: greift nie an; viele Bogenschützen, zwei Wachtürme, die
    /// Armee wartet in Aufstellung vor dem Dorf und schlägt Eindringlinge zurück.</summary>
    public static AiProfile Defense { get; } = new()
    {
        HouseHeadroom     = 3,
        SoldierTarget     = 12,
        RaidMinSoldiers   = int.MaxValue,   // greift nie an (Raids = false)
        AgeUpArmy         = 6,
        HomeGuard         = 0,
        RaidCooldownSeconds = 8,
        DefenseMax        = 5,
        HomeDefenseRadius = 14,
        Raids             = false,
        FormationTactics  = true,
        ArcherPercent     = 40,
        CavalryPercent    = 0,
        TowerCount        = 2,
    };

    /// <summary>Normal: ausbalanciert — Angriffswellen wie bisher (die Default-KI).</summary>
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

    /// <summary>This is Sparta: große gemischte Armee (25 % Nahkampf, 35 % Bogen,
    /// 25 % Reiter, 15 % Belagerung — F15), Angriff in Aufstellung, Reiter im
    /// Schwall. Die Geräte gehören zur Welle: erst mit ihnen bricht die Welle
    /// bewachte Dörfer.</summary>
    public static AiProfile Sparta { get; } = new()
    {
        HouseHeadroom     = 5,
        SoldierTarget     = 20,
        RaidMinSoldiers   = 12,
        AgeUpArmy         = 6,
        HomeGuard         = 3,   // das Dorf bleibt nie leer, wenn die Welle draußen ist
        RaidCooldownSeconds = 10,
        DefenseMax        = 6,
        HomeDefenseRadius = 14,
        FormationTactics  = true,
        ArcherPercent     = 35,
        CavalryPercent    = 25,
        SiegePercent      = 15,
    };

    /// <summary>
    /// Testprofil (tools/ai-pruefung): sehr aggressive, schnelle Wellen,
    /// damit auf der halben Testkarte innerhalb kurzer Echtzeit ein
    /// Dorfzentrum fällt und ein eindeutiger Sieg feststeht. Nur andere
    /// Zahlen, kein zweites Regelwerk.
    /// </summary>
    public static AiProfile Aggressive { get; } = new()
    {
        HouseHeadroom     = 2,
        SoldierTarget     = 14,
        RaidMinSoldiers   = 4,
        AgeUpArmy         = 4,
        HomeGuard         = 1,
        RaidCooldownSeconds = 3,
        DefenseMax        = 4,
        HomeDefenseRadius = 12,
    };

    /// <summary>Zahl der Stufen im Menü (0 Defense, 1 Normal, 2 This is Sparta).</summary>
    public const int LevelCount = 3;

    /// <summary>
    /// Das Profil der Menüstufe <paramref name="level"/>: 0 <see cref="Defense"/>,
    /// 1 <see cref="Standard"/>, ab 2 <see cref="Sparta"/> — ältere Einstellungen und
    /// Spielstände kannten noch 3 (Extrem), das wird ebenfalls Sparta. Alles darunter
    /// (ungültig) wird <see cref="Standard"/>.
    /// </summary>
    public static AiProfile ForLevel(int level) => level switch
    {
        0 => Defense,
        1 => Standard,
        >= 2 => Sparta,
        _ => Standard,
    };

    /// <summary>Die Menüstufe eines Profils, rückwärts zu <see cref="ForLevel"/>:
    /// Defense 0, Sparta 2, jedes andere 1.</summary>
    public static int LevelOf(AiProfile profile)
        => ReferenceEquals(profile, Defense) ? 0
         : ReferenceEquals(profile, Sparta) ? 2
         : 1;
}
