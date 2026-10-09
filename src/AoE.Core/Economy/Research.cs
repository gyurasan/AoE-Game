using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Die Forschungen (P2): acht aus AoE II, jede in ihrem Gebäude. Die Reihenfolge ist die
/// der Tasten in der Leiste.
/// </summary>
public enum Tech
{
    /// <summary>Webstuhl im Stadtzentrum: Dorfbewohner halten mehr aus.</summary>
    Loom,

    /// <summary>Pferdekummet in der Mühle: neue Felder tragen mehr.</summary>
    HorseCollar,

    /// <summary>Doppelaxt im Holzfällerlager: Holz schneller.</summary>
    DoubleBitAxe,

    /// <summary>Goldbergbau im Bergbaulager: Gold schneller.</summary>
    GoldMining,

    /// <summary>Schmiedekunst in der Schmiede: Infanterie und Reiter schlagen härter.</summary>
    Forging,

    /// <summary>Befiederte Pfeile in der Schmiede: Bogenschützen schießen härter und weiter.</summary>
    Fletching,

    /// <summary>Schuppenpanzer in der Schmiede: Infanterie hält mehr aus.</summary>
    ScaleMailArmor,

    /// <summary>Maurerkunst in der Universität: Gebäude halten mehr aus.</summary>
    Masonry
}

/// <summary>
/// Regeln der Forschungen nach AoE II. Alle Werte stehen in dieser Tabelle:
///
///   Tech            Gebäude      ab Zeitalter  Kosten                 Sekunden  Name
///   Loom            TownCenter   Dark          50 Gold                   25     Webstuhl
///   HorseCollar     Mill         Feudal        75 Nahrung, 75 Holz       20     Pferdekummet
///   DoubleBitAxe    LumberCamp   Feudal        100 Nahrung, 50 Holz      25     Doppelaxt
///   GoldMining      MiningCamp   Feudal        100 Nahrung, 75 Holz      30     Goldbergbau
///   Forging         Blacksmith   Feudal        150 Nahrung               50     Schmiedekunst
///   Fletching       Blacksmith   Feudal        100 Nahrung, 50 Gold      30     Befiederte Pfeile
///   ScaleMailArmor  Blacksmith   Feudal        100 Nahrung               40     Schuppenpanzer
///   Masonry         University   Castle        175 Holz, 150 Stein       50     Maurerkunst
///
/// Nahrung ist Resource.Food, Holz Resource.Wood, Gold Resource.Gold, Stein Resource.Stone.
/// Jede Methode wirft für einen Wert, der nicht in der Aufzählung steht,
/// ArgumentOutOfRangeException.
/// </summary>
public static class TechRules
{
    /// <summary>
    /// Kosten nach der Tabelle, bei jedem Aufruf ein neues Wörterbuch - nur die genannten
    /// Rohstoffe sind Schlüssel.
    /// </summary>
    public static Dictionary<Resource, int> CostOf(Tech tech) => tech switch
    {
        Tech.Loom => new Dictionary<Resource, int> { [Resource.Gold] = 50 },
        Tech.HorseCollar => new Dictionary<Resource, int> { [Resource.Food] = 75, [Resource.Wood] = 75 },
        Tech.DoubleBitAxe => new Dictionary<Resource, int> { [Resource.Food] = 100, [Resource.Wood] = 50 },
        Tech.GoldMining => new Dictionary<Resource, int> { [Resource.Food] = 100, [Resource.Wood] = 75 },
        Tech.Forging => new Dictionary<Resource, int> { [Resource.Food] = 150 },
        Tech.Fletching => new Dictionary<Resource, int> { [Resource.Food] = 100, [Resource.Gold] = 50 },
        Tech.ScaleMailArmor => new Dictionary<Resource, int> { [Resource.Food] = 100 },
        Tech.Masonry => new Dictionary<Resource, int> { [Resource.Wood] = 175, [Resource.Stone] = 150 },
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>Forschungsdauer in Sekunden nach der Tabelle.</summary>
    public static float SecondsOf(Tech tech) => tech switch
    {
        Tech.Loom => 25f,
        Tech.HorseCollar => 20f,
        Tech.DoubleBitAxe => 25f,
        Tech.GoldMining => 30f,
        Tech.Forging => 50f,
        Tech.Fletching => 30f,
        Tech.ScaleMailArmor => 40f,
        Tech.Masonry => 50f,
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>Das Gebäude, in dem geforscht wird, nach der Tabelle.</summary>
    public static BuildingType BuildingOf(Tech tech) => tech switch
    {
        Tech.Loom => BuildingType.TownCenter,
        Tech.HorseCollar => BuildingType.Mill,
        Tech.DoubleBitAxe => BuildingType.LumberCamp,
        Tech.GoldMining => BuildingType.MiningCamp,
        Tech.Forging or Tech.Fletching or Tech.ScaleMailArmor => BuildingType.Blacksmith,
        Tech.Masonry => BuildingType.University,
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>Das Zeitalter, ab dem geforscht werden darf, nach der Tabelle.</summary>
    public static Age RequiredAgeOf(Tech tech) => tech switch
    {
        Tech.Loom => Age.Dark,
        Tech.HorseCollar or Tech.DoubleBitAxe or Tech.GoldMining
            or Tech.Forging or Tech.Fletching or Tech.ScaleMailArmor => Age.Feudal,
        Tech.Masonry => Age.Castle,
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>Der Anzeigename nach der Tabelle.</summary>
    public static string NameOf(Tech tech) => tech switch
    {
        Tech.Loom => "Webstuhl",
        Tech.HorseCollar => "Pferdekummet",
        Tech.DoubleBitAxe => "Doppelaxt",
        Tech.GoldMining => "Goldbergbau",
        Tech.Forging => "Schmiedekunst",
        Tech.Fletching => "Befiederte Pfeile",
        Tech.ScaleMailArmor => "Schuppenpanzer",
        Tech.Masonry => "Maurerkunst",
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>
    /// Die Wirkung in einer Zeile für die Leiste, genau so:
    ///
    ///   Loom            "Dorfbewohner +15 LP, Rüstung +1"
    ///   HorseCollar     "neue Felder +75 Nahrung"
    ///   DoubleBitAxe    "Holz 20 % schneller"
    ///   GoldMining      "Gold 15 % schneller"
    ///   Forging         "Infanterie und Reiter Angriff +1"
    ///   Fletching       "Bogenschützen Angriff +1, Reichweite +1"
    ///   ScaleMailArmor  "Infanterie Rüstung +1"
    ///   Masonry         "Gebäude +10 % LP, Rüstung +1"
    /// </summary>
    public static string EffectOf(Tech tech) => tech switch
    {
        Tech.Loom => "Dorfbewohner +15 LP, Rüstung +1",
        Tech.HorseCollar => "neue Felder +75 Nahrung",
        Tech.DoubleBitAxe => "Holz 20 % schneller",
        Tech.GoldMining => "Gold 15 % schneller",
        Tech.Forging => "Infanterie und Reiter Angriff +1",
        Tech.Fletching => "Bogenschützen Angriff +1, Reichweite +1",
        Tech.ScaleMailArmor => "Infanterie Rüstung +1",
        Tech.Masonry => "Gebäude +10 % LP, Rüstung +1",
        _ => throw new ArgumentOutOfRangeException(nameof(tech), tech, "Unbekannte Forschung.")
    };

    /// <summary>
    /// Die Forschungen, die im Gebäudetyp <paramref name="type"/> erforscht werden
    /// (<see cref="BuildingOf"/> gleich type), in der Reihenfolge der Aufzählung
    /// <see cref="Tech"/>; für Gebäude ohne Forschung eine leere Liste. Wirft nicht.
    /// </summary>
    public static IReadOnlyList<Tech> In(BuildingType type)
        => Enum.GetValues<Tech>().Where(t => BuildingOf(t) == type).ToList();
}

/// <summary>
/// Forschungsstand eines Spielers - reine Logik ohne MonoGame. Jede Forschung gibt es je
/// Spieler einmal: sie ist offen, läuft gerade in einem Gebäude (<see cref="IsPending"/>)
/// oder ist erforscht (<see cref="IsResearched"/>). Den Ablauf in einem Gebäude regelt
/// <see cref="ResearchSlot"/>.
/// </summary>
public sealed class TechProgress
{
    private readonly HashSet<Tech> _researched = new();
    private readonly HashSet<Tech> _pending = new();

    /// <summary>
    /// Zählt die Änderungen des Stands: jeder Aufruf von <see cref="Begin"/>,
    /// <see cref="Complete"/> und <see cref="Release"/>, der true zurückgibt, erhöht ihn um 1;
    /// zu Beginn 0. Die Leiste baut ihre Tasten neu, wenn er sich ändert.
    /// </summary>
    public int Version { get; private set; }

    /// <summary>true, wenn <paramref name="tech"/> erforscht ist.</summary>
    public bool IsResearched(Tech tech) => _researched.Contains(tech);

    /// <summary>true, solange <paramref name="tech"/> in einem Gebäude läuft.</summary>
    public bool IsPending(Tech tech) => _pending.Contains(tech);

    /// <summary>
    /// true, wenn <paramref name="tech"/> weder erforscht ist noch läuft und
    /// <paramref name="current"/> mindestens <see cref="TechRules.RequiredAgeOf"/> ist.
    /// </summary>
    public bool CanStart(Tech tech, Age current)
        => !IsResearched(tech) && !IsPending(tech) && current >= TechRules.RequiredAgeOf(tech);

    /// <summary>
    /// <paramref name="tech"/> läuft ab jetzt. false und keine Änderung, wenn sie schon
    /// erforscht ist oder schon läuft. Das Zeitalter prüft hier niemand - das tut
    /// <see cref="CanStart"/>.
    /// </summary>
    public bool Begin(Tech tech)
    {
        if (IsResearched(tech) || IsPending(tech))
            return false;
        _pending.Add(tech);
        Version++;
        return true;
    }

    /// <summary>
    /// Die laufende Forschung <paramref name="tech"/> ist fertig: sie läuft nicht mehr und ist
    /// erforscht. false und keine Änderung, wenn sie nicht läuft.
    /// </summary>
    public bool Complete(Tech tech)
    {
        if (!_pending.Remove(tech))
            return false;
        _researched.Add(tech);
        Version++;
        return true;
    }

    /// <summary>
    /// Die laufende Forschung <paramref name="tech"/> fällt weg, etwa weil ihr Gebäude zerstört
    /// ist: sie läuft nicht mehr und ist wieder offen. false und keine Änderung, wenn sie
    /// nicht läuft.
    /// </summary>
    public bool Release(Tech tech)
    {
        if (!_pending.Remove(tech))
            return false;
        Version++;
        return true;
    }
}

/// <summary>
/// Die Forschung in einem Gebäude - reine Logik ohne MonoGame. Ein Gebäude forscht
/// höchstens eine Forschung zur Zeit. Bezahlt wird beim Start; für den Spieler gilt die
/// Forschung erst, wenn ihre volle Dauer erforscht ist (<see cref="TechProgress.Complete"/>).
/// </summary>
public sealed class ResearchSlot
{
    private float _elapsed;   // bisherige Forschungszeit der laufenden Forschung

    /// <summary>Die laufende Forschung, sonst null.</summary>
    public Tech? Current { get; private set; }

    /// <summary>true, solange eine Forschung läuft.</summary>
    public bool IsBusy => Current != null;

    /// <summary>
    /// Anteil der bisherigen Forschungszeit an <see cref="TechRules.SecondsOf"/> der laufenden
    /// Forschung, von 0 bis höchstens 1. Ohne laufende Forschung 0.
    /// </summary>
    public float Progress => Current is { } tech
        ? Math.Min(1f, _elapsed / TechRules.SecondsOf(tech))
        : 0f;

    /// <summary>
    /// Beginnt <paramref name="tech"/> in diesem Gebäude.
    ///
    /// Gibt false zurück und ändert nichts - auch nicht am Guthaben -, wenn das Gebäude schon
    /// forscht (<see cref="IsBusy"/>), <paramref name="techs"/>.CanStart(tech, current) false
    /// ist oder <paramref name="pool"/> die Kosten (<see cref="TechRules.CostOf"/>) nicht hat.
    ///
    /// Sonst: die Kosten von pool abbuchen (pool.PayCost), techs.Begin(tech),
    /// <see cref="Current"/> wird tech, <see cref="Progress"/> 0; Rückgabe true.
    /// </summary>
    public bool TryStart(Tech tech, Age current, TechProgress techs, ResourcePool pool)
    {
        // Schon beschäftigt oder die Forschung geht gerade nicht: nichts ändern.
        if (IsBusy || !techs.CanStart(tech, current))
            return false;

        // Kosten abbuchen; reicht das Guthaben nicht, bleibt alles wie es ist.
        if (!pool.PayCost(TechRules.CostOf(tech)))
            return false;

        techs.Begin(tech);
        Current = tech;
        _elapsed = 0f;
        return true;
    }

    /// <summary>
    /// Forscht <paramref name="dt"/> Sekunden lang. Ohne laufende Forschung geschieht nichts,
    /// Rückgabe false.
    ///
    /// Erreicht die bisherige Forschungszeit <see cref="TechRules.SecondsOf"/> der laufenden
    /// Forschung, ist sie fertig: techs.Complete(sie), <paramref name="finished"/> ist sie,
    /// <see cref="Current"/> wird null, <see cref="Progress"/> 0, und die Rückgabe ist true -
    /// genau in diesem einen Aufruf. Überzählige Zeit verfällt. Sonst Rückgabe false;
    /// finished hat dann keine Bedeutung.
    /// </summary>
    public bool Update(float dt, TechProgress techs, out Tech finished)
    {
        finished = default;
        if (Current is not { } tech)
            return false;

        _elapsed += dt;
        if (_elapsed < TechRules.SecondsOf(tech))
            return false;

        // Die volle Dauer ist erreicht: die Forschung ist fertig.
        techs.Complete(tech);
        finished = tech;
        Current = null;
        _elapsed = 0f;
        return true;
    }

    /// <summary>
    /// Bricht die laufende Forschung ab, etwa wenn das Gebäude zerstört ist:
    /// techs.Release(sie), ihre vollen Kosten (<see cref="TechRules.CostOf"/>) gehen an
    /// <paramref name="pool"/> zurück (pool.Add je Rohstoff), <see cref="Current"/> wird null,
    /// <see cref="Progress"/> 0; Rückgabe true. Ohne laufende Forschung: false, nichts geändert.
    /// </summary>
    public bool Cancel(TechProgress techs, ResourcePool pool)
    {
        if (Current is not { } tech)
            return false;

        techs.Release(tech);
        foreach (var (resource, amount) in TechRules.CostOf(tech))
            pool.Add(resource, amount);
        Current = null;
        _elapsed = 0f;
        return true;
    }
}

/// <summary>
/// Was eine Forschung bewirkt - reine Logik ohne MonoGame. Werte nach AoE II, vereinfacht
/// auf das, was das Spiel kennt: eine Rüstung (Stats.BaseArmor) statt Nah- und
/// Fernkampfrüstung. Das Spiel wendet eine fertige Forschung einmal auf alles an, was der
/// Spieler schon hat (<see cref="Apply"/>), und alle erforschten auf alles, was danach
/// entsteht (<see cref="ApplyAll"/>).
/// </summary>
public static class TechEffects
{
    /// <summary>Mehr Lebenspunkte je Dorfbewohner durch den Webstuhl.</summary>
    public const int LOOM_HP = 15;

    /// <summary>Mehr Nahrung je neuer Feldkachel durch das Pferdekummet.</summary>
    public const int HORSE_COLLAR_FOOD = 75;

    /// <summary>
    /// Wendet <paramref name="tech"/> einmal auf <paramref name="entity"/> an. Rückgabe true,
    /// wenn entity betroffen ist und sich geändert hat; sonst false, nichts geändert:
    ///
    /// - Loom, Dorfbewohner (Villager): Stats.HitPoints und CurrentHp je +LOOM_HP,
    ///   Stats.BaseArmor +1.
    /// - Forging, Infanterie und Reiter (Militia, SpearMan, Scout, Knight, CamelRider):
    ///   Stats.BaseAttack +1.
    /// - Fletching, Bogenschützen (Archer, Skirmisher): Stats.BaseAttack +1, Stats.Range +1.
    /// - ScaleMailArmor, Infanterie (Militia, SpearMan): Stats.BaseArmor +1.
    /// - Masonry, Gebäude (BuildingEntity): Stats.HitPoints um ein Zehntel mehr, ganzzahlig
    ///   (HitPoints / 10 dazu, also 400 auf 440), CurrentHp um denselben Zuwachs,
    ///   Stats.BaseArmor +1.
    /// - HorseCollar, DoubleBitAxe und GoldMining wirken auf keine Einheit und kein Gebäude
    ///   (siehe <see cref="FarmFood"/> und <see cref="GatherFactor"/>): false.
    /// </summary>
    public static bool Apply(Tech tech, UnitEntity entity)
    {
        switch (tech)
        {
            case Tech.Loom:
                // Webstuhl: nur Dorfbewohner werden stärker.
                if (entity is not Villager)
                    return false;
                entity.Stats.HitPoints += LOOM_HP;
                entity.CurrentHp += LOOM_HP;
                entity.Stats.BaseArmor += 1;
                return true;

            case Tech.Forging:
                // Schmiedekunst: Infanterie und Reiter schlagen härter.
                if (entity is not (Militia or SpearMan or Scout or Knight or CamelRider))
                    return false;
                entity.Stats.BaseAttack += 1;
                return true;

            case Tech.Fletching:
                // Befiederte Pfeile: nur Bogenschützen schießen härter und weiter.
                if (entity is not (Archer or Skirmisher))
                    return false;
                entity.Stats.BaseAttack += 1;
                entity.Stats.Range += 1;
                return true;

            case Tech.ScaleMailArmor:
                // Schuppenpanzer: nur Infanterie hält mehr aus.
                if (entity is not (Militia or SpearMan))
                    return false;
                entity.Stats.BaseArmor += 1;
                return true;

            case Tech.Masonry:
                // Maurerkunst: nur Gebäude werden stärker.
                if (entity is not BuildingEntity)
                    return false;
                int hpBonus = entity.Stats.HitPoints / 10;
                entity.Stats.HitPoints += hpBonus;
                entity.CurrentHp += hpBonus;
                entity.Stats.BaseArmor += 1;
                return true;

            default:
                // Pferdekummet, Doppelaxt und Goldbergbau wirken auf keine Einheit.
                return false;
        }
    }

    /// <summary>
    /// Wendet jede erforschte Forschung von <paramref name="techs"/> (IsResearched) einmal mit
    /// <see cref="Apply"/> auf <paramref name="entity"/> an, in der Reihenfolge der Aufzählung
    /// <see cref="Tech"/> - für Einheiten und Gebäude, die nach der Forschung entstehen.
    /// Laufende Forschungen zählen nicht. Rückgabe: wie oft Apply true war.
    /// </summary>
    public static int ApplyAll(TechProgress techs, UnitEntity entity)
    {
        int anzahl = 0;
        foreach (var tech in Enum.GetValues<Tech>())
        {
            if (techs.IsResearched(tech) && Apply(tech, entity))
                anzahl++;
        }
        return anzahl;
    }

    /// <summary>
    /// Faktor auf die Sammelrate (<see cref="GatherJob.RateOf"/>) eines Spielers: Holz 1,2
    /// mit erforschter DoubleBitAxe, Gold 1,15 mit erforschtem GoldMining, sonst 1.
    /// </summary>
    public static float GatherFactor(TechProgress techs, Resource resource)
    {
        if (resource == Resource.Wood && techs.IsResearched(Tech.DoubleBitAxe))
            return 1.2f;
        if (resource == Resource.Gold && techs.IsResearched(Tech.GoldMining))
            return 1.15f;
        return 1f;
    }

    /// <summary>
    /// Vorrat einer neu angelegten Feldkachel: <paramref name="baseFood"/>, mit erforschtem
    /// HorseCollar baseFood + <see cref="HORSE_COLLAR_FOOD"/>.
    /// </summary>
    public static int FarmFood(TechProgress techs, int baseFood)
        => techs.IsResearched(Tech.HorseCollar) ? baseFood + HORSE_COLLAR_FOOD : baseFood;
}
