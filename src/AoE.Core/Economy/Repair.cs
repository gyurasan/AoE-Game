namespace AoE.Core.Economy;

/// <summary>
/// Reparatur-Auftrag für ein bestehendes Gebäude — reine Logik ohne MonoGame.
/// Kosten und Dauer fallen proportional zum fehlenden Anteil der Standfestigkeit
/// (siehe <see cref="BuildingRules.RepairCostOf"/> und
/// <see cref="BuildingRules.RepairSecondsOf"/>): ein Gebäude zu 50 % angeschlagen
/// zu reparieren kostet und dauert die Hälfte. Mit n Reparateuren wächst der
/// Fortschritt wie an einer Baustelle — dt · SpeedFactor(n) / Seconds (der erste
/// arbeitet voll, jeder weitere ein Drittel so schnell, die AoE-II-Formel).
/// </summary>
public sealed class Repair
{
    /// <summary>
    /// Neuer Reparatur-Auftrag mit Fortschritt 0.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="seconds"/> ist nicht größer als 0 oder
    /// <paramref name="missingHp"/> ist nicht größer als 0.
    /// </exception>
    public Repair(float seconds, int missingHp)
    {
        if (seconds <= 0f)
            throw new ArgumentOutOfRangeException(nameof(seconds));
        if (missingHp <= 0)
            throw new ArgumentOutOfRangeException(nameof(missingHp));
        Seconds = seconds;
        Missing = missingHp;
    }

    /// <summary>Vollreparatur-Dauer mit einem einzigen Reparateur, in Sekunden.</summary>
    public float Seconds { get; }

    /// <summary>Standfestigkeit, die der Auftrag herstellt (fehlende LP zum Auftrag).</summary>
    public int Missing { get; }

    /// <summary>Fortschritt zwischen 0 und 1; bei 1 ist das Gebäude wieder hergestelt.</summary>
    public float Progress { get; private set; }

    /// <summary>true, sobald <see cref="Progress"/> 1 erreicht hat.</summary>
    public bool IsComplete => Progress >= 1f;

    /// <summary>
    /// Repariert <paramref name="dt"/> Sekunden lang mit
    /// <paramref name="repairers"/> Reparateuren: <see cref="Progress"/> wächst um
    /// dt · BuildingRules.SpeedFactor(repairers) / Seconds, höchstens bis 1.
    /// Rückgabe true genau in dem Aufruf, mit dem die Reparatur fertig wird;
    /// war sie schon vorher fertig, ändert sich nichts und die Rückgabe ist false.
    /// </summary>
    public bool Update(float dt, int repairers)
    {
        if (IsComplete)
            return false;
        Progress = Math.Min(1f, Progress + dt * BuildingRules.SpeedFactor(repairers) / Seconds);
        return IsComplete;
    }

    /// <summary>
    /// Die Reparatur für einen Spielstand.
    /// VERTRAG: ein neuer <see cref="RepairState"/> mit <see cref="Seconds"/>,
    /// <see cref="Missing"/> und <see cref="Progress"/>. Ändert nichts.
    /// </summary>
    public RepairState ToState() => new(Seconds, Missing, Progress);

    /// <summary>
    /// Stellt einen Reparatur-Auftrag aus einem Spielstand wieder her.
    /// VERTRAG: eine neue <see cref="Repair"/> mit state.Seconds und state.Missing
    /// (gleiche <see cref="ArgumentOutOfRangeException"/>s wie der Konstruktor) und
    /// Progress state.Progress, begrenzt auf 0 bis 1.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="state"/> ist null.</exception>
    public static Repair FromState(RepairState state)
    {
        ArgumentNullException.ThrowIfNull(state);
        var repair = new Repair(state.Seconds, state.Missing);
        repair.Progress = Math.Clamp(state.Progress, 0f, 1f);
        return repair;
    }

    /// <summary>
    /// Die Standfestigkeit, die bei Fortschritt <paramref name="progress"/>
    /// erreicht ist: Startpunkt = maxHp - Missing, davon fehlt noch
    /// (1 - progress) · Missing.
    /// </summary>
    public int HpAt(float progress, int maxHp)
        => Math.Clamp(maxHp - (int)Math.Round(Missing * (1f - progress)), 0, maxHp);
}

/// <summary>Gespeicherter Stand einer <see cref="Repair"/> (Spielstand).</summary>
public sealed record RepairState(float Seconds, int Missing, float Progress);

