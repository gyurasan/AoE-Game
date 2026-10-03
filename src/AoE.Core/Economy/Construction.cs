using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Bauregeln. Die Kosten stehen in der Spezifikation (Kapitel „Gebäude");
/// Bauzeit und Grundfläche nennt sie nicht, dafür gelten die Werte aus AoE II.
/// </summary>
public static class BuildingRules
{
    /// <summary>
    /// Kosten laut Spezifikation, bei jedem Aufruf ein neues Wörterbuch:
    /// Stadtzentrum 275 Holz und 100 Stein, Haus 25 Holz, Mühle, Holzfällerlager
    /// und Bergbaulager je 100 Holz, Farm 60 Holz, Kaserne 175 Holz, Wachturm
    /// 50 Holz und 125 Stein. Für alle anderen Typen ein leeres Wörterbuch.
    /// </summary>
    public static Dictionary<Resource, int> CostOf(BuildingType type) => type switch
    {
        BuildingType.TownCenter => new Dictionary<Resource, int> { [Resource.Wood] = 275, [Resource.Stone] = 100 },
        BuildingType.House => new Dictionary<Resource, int> { [Resource.Wood] = 25 },
        BuildingType.Mill or BuildingType.LumberCamp or BuildingType.MiningCamp => new Dictionary<Resource, int> { [Resource.Wood] = 100 },
        BuildingType.Farm => new Dictionary<Resource, int> { [Resource.Wood] = 60 },
        BuildingType.Barracks => new Dictionary<Resource, int> { [Resource.Wood] = 175 },
        BuildingType.Tower => new Dictionary<Resource, int> { [Resource.Wood] = 50, [Resource.Stone] = 125 },
        _ => new Dictionary<Resource, int>()
    };

    /// <summary>
    /// Bauzeit in Sekunden mit einem einzigen Bauarbeiter: Haus 25, Mühle,
    /// Holzfällerlager und Bergbaulager je 35, Farm 15, Kaserne 50,
    /// Wachturm 80, Stadtzentrum 150. Für alle anderen Typen 60.
    /// </summary>
    public static float BuildSecondsOf(BuildingType type) => type switch
    {
        BuildingType.House => 25f,
        BuildingType.Mill or BuildingType.LumberCamp or BuildingType.MiningCamp => 35f,
        BuildingType.Farm => 15f,
        BuildingType.Barracks => 50f,
        BuildingType.Tower => 80f,
        BuildingType.TownCenter => 150f,
        _ => 60f
    };

    /// <summary>
    /// Kantenlänge der quadratischen Grundfläche in Kacheln: Stadtzentrum 4,
    /// Farm und Kaserne 3, alle anderen 2.
    /// </summary>
    public static int SizeOf(BuildingType type) => type switch
    {
        BuildingType.TownCenter => 4,
        BuildingType.Farm or BuildingType.Barracks => 3,
        _ => 2
    };

    /// <summary>
    /// Baugeschwindigkeit von <paramref name="builders"/> Arbeitern im
    /// Verhältnis zu einem: der erste baut voll, jeder weitere ein Drittel so
    /// schnell — (n + 2) / 3, die Formel aus AoE II. Das ist der abnehmende
    /// Ertrag der Spezifikation: vier Arbeiter brauchen die halbe Zeit, nicht
    /// ein Viertel. 0 bei <paramref name="builders"/> kleiner oder gleich 0.
    /// </summary>
    public static float SpeedFactor(int builders) => builders <= 0 ? 0f : (builders + 2) / 3f;
}

/// <summary>
/// Baustelle eines Gebäudes — reine Logik ohne MonoGame. Ohne Bauarbeiter
/// steht sie still; mit n Arbeitern wächst der Fortschritt je Aufruf von
/// <see cref="Update"/> um dt · SpeedFactor(n) / <see cref="BuildSeconds"/>.
/// </summary>
public sealed class Construction
{
    /// <summary>Neue Baustelle mit Fortschritt 0.</summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="buildSeconds"/> ist nicht größer als 0.
    /// </exception>
    public Construction(float buildSeconds)
    {
        if (buildSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(buildSeconds));
        BuildSeconds = buildSeconds;
    }

    /// <summary>Bauzeit mit einem einzigen Bauarbeiter, in Sekunden.</summary>
    public float BuildSeconds { get; }

    /// <summary>Fortschritt zwischen 0 und 1; bei 1 ist das Gebäude fertig.</summary>
    public float Progress { get; private set; }

    /// <summary>true, sobald <see cref="Progress"/> 1 erreicht hat.</summary>
    public bool IsComplete => Progress >= 1f;

    /// <summary>
    /// Baut <paramref name="dt"/> Sekunden lang mit <paramref name="builders"/>
    /// Arbeitern: <see cref="Progress"/> wächst um
    /// dt · BuildingRules.SpeedFactor(builders) / BuildSeconds, höchstens bis 1.
    ///
    /// Rückgabe true genau in dem Aufruf, mit dem das Gebäude fertig wird. War
    /// es schon vorher fertig, ändert sich nichts und die Rückgabe ist false.
    /// </summary>
    public bool Update(float dt, int builders)
    {
        if (IsComplete)
            return false;
        Progress = Math.Min(1f, Progress + dt * BuildingRules.SpeedFactor(builders) / BuildSeconds);
        return IsComplete;
    }
}
