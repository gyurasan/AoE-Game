using System.Diagnostics.CodeAnalysis;
using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Bevölkerungsregeln der Spezifikation (Kapitel "Bevölkerung und Häuser"):
/// jede Einheit belegt einen Platz, Stadtzentrum und Haus geben je fünf, und
/// Wirtschaft und Armee teilen sich dieselbe Obergrenze.
/// </summary>
public static class Population
{
    /// <summary>Standard-Obergrenze laut Spezifikation.</summary>
    public const int DEFAULT_LIMIT = 200;

    /// <summary>Plätze je Stadtzentrum und je Haus.</summary>
    public const int SLOTS_PER_BUILDING = 5;

    /// <summary>
    /// Plätze, die ein Gebäude stiftet: <see cref="SLOTS_PER_BUILDING"/> für
    /// <see cref="BuildingType.TownCenter"/> und <see cref="BuildingType.House"/>,
    /// 0 für alle anderen.
    /// </summary>
    public static int SlotsOf(BuildingType type) =>
        type is BuildingType.TownCenter or BuildingType.House ? SLOTS_PER_BUILDING : 0;

    /// <summary>
    /// Bevölkerungsgrenze eines Spielers: die Summe von <see cref="SlotsOf"/>
    /// über seine Gebäude, höchstens <paramref name="limit"/>. Ohne Gebäude 0.
    /// </summary>
    public static int Capacity(IEnumerable<BuildingType> buildings, int limit = DEFAULT_LIMIT)
        => Math.Min(buildings.Sum(SlotsOf), limit);
}

/// <summary>
/// Ausbildungs-Warteschlange eines Gebäudes, etwa Dorfbewohner im
/// Stadtzentrum — reine Logik ohne MonoGame. <typeparamref name="T"/>
/// bezeichnet die Einheit; das Spiel nimmt seinen eigenen Einheitentyp.
///
/// Bezahlt wird beim Einreihen. Ausgebildet wird immer nur die vorderste
/// Einheit. Ist die Bevölkerungsgrenze erreicht, steht die Ausbildung still,
/// bis wieder Platz ist — "stoppt die Produktion komplett", wie die
/// Spezifikation sagt. Der Fortschritt geht dabei nicht verloren.
/// </summary>
public sealed class TrainingQueue<T> where T : notnull
{
    /// <summary>So viele Einheiten fasst die Warteschlange höchstens.</summary>
    public const int MAX_LENGTH = 15;

    private readonly List<(T Unit, Dictionary<Resource, int> Cost, float Seconds)> _entries = new();
    private float _elapsed;   // bisherige Ausbildungszeit der vordersten Einheit

    /// <summary>Anzahl eingereihter Einheiten, die gerade ausgebildete eingeschlossen.</summary>
    public int Count => _entries.Count;

    /// <summary>Die eingereihten Einheiten, die gerade ausgebildete zuerst.</summary>
    public IReadOnlyList<T> Units => _entries.Select(e => e.Unit).ToList();

    /// <summary>
    /// Fortschritt der vordersten Einheit zwischen 0 und 1: bisherige
    /// Ausbildungszeit geteilt durch ihre volle Ausbildungszeit. 0, wenn die
    /// Warteschlange leer ist.
    /// </summary>
    public float Progress => _entries.Count == 0 ? 0f : _elapsed / _entries[0].Seconds;

    /// <summary>
    /// true, wenn der letzte Aufruf von <see cref="Update"/> wegen der
    /// Bevölkerungsgrenze nichts ausbilden konnte. Bei leerer Warteschlange
    /// false — dann wartet nichts.
    /// </summary>
    public bool IsBlocked { get; private set; }

    /// <summary>
    /// Reiht <paramref name="unit"/> hinten ein und bucht
    /// <paramref name="cost"/> sofort von <paramref name="pool"/> ab.
    ///
    /// Gibt false zurück und ändert nichts, wenn die Warteschlange schon
    /// <see cref="MAX_LENGTH"/> Einheiten hat oder das Guthaben nicht reicht.
    /// Die Kosten werden kopiert gespeichert — <see cref="CancelLast"/> erstattet
    /// genau sie, auch wenn der Aufrufer sein Wörterbuch später ändert.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="trainSeconds"/> ist nicht größer als 0.
    /// </exception>
    public bool Enqueue(T unit, Dictionary<Resource, int> cost, float trainSeconds, ResourcePool pool)
    {
        if (trainSeconds <= 0)
            throw new ArgumentOutOfRangeException(nameof(trainSeconds));
        if (_entries.Count >= MAX_LENGTH)
            return false;
        if (!pool.PayCost(cost))
            return false;
        _entries.Add((unit, new Dictionary<Resource, int>(cost), trainSeconds));
        return true;
    }

    /// <summary>
    /// Bildet <paramref name="dt"/> Sekunden lang aus.
    ///
    /// Leere Warteschlange: nichts geschieht, <see cref="IsBlocked"/> wird
    /// false, Rückgabe false.
    ///
    /// <paramref name="population"/> ist mindestens <paramref name="capacity"/>:
    /// kein Fortschritt, <see cref="IsBlocked"/> wird true, Rückgabe false.
    ///
    /// Sonst wird <see cref="IsBlocked"/> false und die vorderste Einheit
    /// erhält <paramref name="dt"/> Sekunden Ausbildungszeit. Erreicht sie
    /// ihre volle Ausbildungszeit, verlässt sie die Warteschlange: Rückgabe
    /// true, <paramref name="finished"/> ist diese Einheit, und die nächste
    /// beginnt bei 0 — überzählige Zeit verfällt, je Aufruf wird also
    /// höchstens eine Einheit fertig.
    /// </summary>
    public bool Update(float dt, int population, int capacity, [MaybeNullWhen(false)] out T finished)
    {
        finished = default;
        if (_entries.Count == 0)
        {
            IsBlocked = false;
            return false;
        }
        if (population >= capacity)
        {
            IsBlocked = true;
            return false;
        }
        IsBlocked = false;
        _elapsed += dt;
        if (_elapsed < _entries[0].Seconds)
            return false;
        finished = _entries[0].Unit;
        _entries.RemoveAt(0);
        _elapsed = 0;
        return true;
    }

    /// <summary>
    /// Nimmt die hinterste Einheit aus der Warteschlange und erstattet ihre
    /// Kosten voll an <paramref name="pool"/>. War sie die einzige, beginnt
    /// der Fortschritt wieder bei 0; sonst bleibt der Fortschritt der
    /// vordersten Einheit erhalten. Rückgabe false bei leerer Warteschlange.
    /// </summary>
    public bool CancelLast(ResourcePool pool)
    {
        if (_entries.Count == 0)
            return false;
        var (_, cost, _) = _entries[^1];
        _entries.RemoveAt(_entries.Count - 1);
        foreach (var (resource, amount) in cost)
            pool.Add(resource, amount);
        if (_entries.Count == 0)
            _elapsed = 0;
        return true;
    }
}
