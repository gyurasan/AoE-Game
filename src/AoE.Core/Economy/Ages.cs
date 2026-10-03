using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Die vier Zeitalter in Aufstiegsreihenfolge (Spezifikation, Kapitel
/// „Die Zeitalter"). Die Reihenfolge der Werte ist die Reihenfolge des
/// Aufstiegs, Vergleiche wie <c>current &gt;= Age.Feudal</c> sind also erlaubt.
/// </summary>
public enum Age
{
    /// <summary>Dunkle Zeit — der Start.</summary>
    Dark,

    /// <summary>Feudalzeit.</summary>
    Feudal,

    /// <summary>Ritterzeit.</summary>
    Castle,

    /// <summary>Imperialzeit — das letzte Zeitalter.</summary>
    Imperial
}

/// <summary>
/// Regeln der Zeitalter laut Spezifikation, Kapitel „Die Zeitalter": was der
/// Aufstieg kostet, wie lange er dauert und welche Gebäude er freischaltet.
/// </summary>
public static class AgeRules
{
    /// <summary>
    /// Kosten des Aufstiegs in <paramref name="target"/>, bei jedem Aufruf ein
    /// neues Wörterbuch: Feudalzeit 500 Nahrung, Ritterzeit 800 Nahrung und
    /// 200 Gold, Imperialzeit 1000 Nahrung und 800 Gold. Für die Dunkle Zeit —
    /// in sie steigt niemand auf — ein leeres Wörterbuch.
    /// </summary>
    public static Dictionary<Resource, int> CostOf(Age target) => target switch
    {
        Age.Feudal => new Dictionary<Resource, int> { [Resource.Food] = 500 },
        Age.Castle => new Dictionary<Resource, int> { [Resource.Food] = 800, [Resource.Gold] = 200 },
        Age.Imperial => new Dictionary<Resource, int> { [Resource.Food] = 1000, [Resource.Gold] = 800 },
        _ => new Dictionary<Resource, int>()
    };

    /// <summary>
    /// Forschungsdauer des Aufstiegs in <paramref name="target"/> in Sekunden:
    /// Feudalzeit 130, Ritterzeit 160, Imperialzeit 190. Dunkle Zeit 0.
    /// </summary>
    public static float ResearchSecondsOf(Age target) => target switch
    {
        Age.Feudal => 130f,
        Age.Castle => 160f,
        Age.Imperial => 190f,
        _ => 0f
    };

    /// <summary>Das folgende Zeitalter, oder null nach der Imperialzeit.</summary>
    public static Age? Next(Age current) => current >= Age.Imperial ? null : (Age)(current + 1);

    /// <summary>
    /// Das Zeitalter, ab dem ein Gebäude gebaut werden darf.
    /// Dunkle Zeit: Haus, Mühle, Holzfällerlager, Bergbaulager, Farm, Kaserne,
    /// Palisadenmauer.
    /// Feudalzeit: Schießstand, Stall, Markt, Schmiede, Wachturm, Steinmauer.
    /// Ritterzeit: Stadtzentrum — das erste steht schon, weitere erst jetzt —,
    /// Burg, Universität, Kloster, Belagerungswerkstatt.
    /// Imperialzeit: Wunder.
    /// </summary>
    public static Age RequiredAgeOf(BuildingType type) => type switch
    {
        BuildingType.ArcheryRange or BuildingType.Stable or BuildingType.Market
            or BuildingType.Blacksmith or BuildingType.Tower or BuildingType.StoneWall => Age.Feudal,
        BuildingType.TownCenter or BuildingType.Castle or BuildingType.University
            or BuildingType.Monastery or BuildingType.SiegeWorkshop => Age.Castle,
        BuildingType.Wonder => Age.Imperial,
        _ => Age.Dark
    };

    /// <summary>
    /// true, wenn <paramref name="type"/> im Zeitalter <paramref name="current"/>
    /// gebaut werden darf — also <paramref name="current"/> mindestens
    /// <see cref="RequiredAgeOf"/> ist.
    /// </summary>
    public static bool IsUnlocked(BuildingType type, Age current) => current >= RequiredAgeOf(type);

    /// <summary>
    /// Anzeigename: „Dunkle Zeit", „Feudalzeit", „Ritterzeit", „Imperialzeit".
    /// </summary>
    public static string NameOf(Age age) => age switch
    {
        Age.Dark => "Dunkle Zeit",
        Age.Feudal => "Feudalzeit",
        Age.Castle => "Ritterzeit",
        Age.Imperial => "Imperialzeit",
        _ => age.ToString()
    };
}

/// <summary>
/// Zeitalter eines Spielers und sein Aufstieg — reine Logik ohne MonoGame.
/// Der Aufstieg wird im Stadtzentrum erforscht: er kostet sofort beim Start
/// und dauert <see cref="AgeRules.ResearchSecondsOf"/> Sekunden; erst danach
/// gilt das neue Zeitalter.
/// </summary>
public sealed class AgeProgress
{
    private float _researchedSeconds;

    /// <summary>Das erreichte Zeitalter; zu Beginn <see cref="Age.Dark"/>.</summary>
    public Age Current { get; private set; } = Age.Dark;

    /// <summary>Das Zeitalter, in das gerade aufgestiegen wird, sonst null.</summary>
    public Age? Target { get; private set; }

    /// <summary>true, solange ein Aufstieg läuft.</summary>
    public bool IsResearching => Target != null;

    /// <summary>
    /// Anteil der erforschten Zeit am laufenden Aufstieg, von 0 bis unter 1.
    /// Ohne laufenden Aufstieg 0.
    /// </summary>
    public float Progress => Target is { } target
        ? Math.Min(1f, _researchedSeconds / AgeRules.ResearchSecondsOf(target))
        : 0f;

    /// <summary>
    /// Beginnt den Aufstieg in das nächste Zeitalter und zieht dessen Kosten
    /// (<see cref="AgeRules.CostOf"/>) aus <paramref name="pool"/> ab; danach
    /// ist <see cref="Target"/> dieses Zeitalter und <see cref="Progress"/> 0.
    ///
    /// Gibt false zurück und ändert nichts — auch nicht am Guthaben —, wenn
    /// schon ein Aufstieg läuft, die Imperialzeit erreicht ist oder das
    /// Guthaben nicht reicht.
    /// </summary>
    public bool TryStart(ResourcePool pool)
    {
        if (IsResearching)
            return false;

        Age? next = AgeRules.Next(Current);
        if (next is not { } target)
            return false;

        if (!pool.PayCost(AgeRules.CostOf(target)))
            return false;

        Target = target;
        _researchedSeconds = 0f;
        return true;
    }

    /// <summary>
    /// Erforscht <paramref name="dt"/> Sekunden lang.
    ///
    /// Ohne laufenden Aufstieg geschieht nichts, Rückgabe false.
    ///
    /// Erreicht die erforschte Zeit die volle Dauer, ist der Aufstieg fertig:
    /// <see cref="Current"/> wird das neue Zeitalter, <see cref="Target"/> null,
    /// <see cref="Progress"/> 0, und die Rückgabe ist true — genau in diesem
    /// einen Aufruf. Überzählige Zeit verfällt. Sonst Rückgabe false.
    /// </summary>
    public bool Update(float dt)
    {
        if (Target is not { } target)
            return false;

        _researchedSeconds += dt;
        if (_researchedSeconds < AgeRules.ResearchSecondsOf(target))
            return false;

        Current = target;
        Target = null;
        _researchedSeconds = 0f;
        return true;
    }
}
