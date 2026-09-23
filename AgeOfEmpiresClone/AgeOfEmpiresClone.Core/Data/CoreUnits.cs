using AoE.Core.Entities;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Brücke zwischen den Einheitentypen des Spiels und den getesteten
/// Einheitenklassen aus <c>AoE.Core</c>.
///
/// Bis hierher pflegte das Spiel eine eigene Werttabelle in <see cref="Unit"/>,
/// die dieselben Einheiten ein zweites Mal beschrieb — mit anderen Zahlen und
/// ohne Angriffs- und Rüstungsklassen. Kampfwerte kommen jetzt ausschließlich
/// aus der Kernbibliothek, die dafür Tests hat.
/// </summary>
public static class CoreUnits
{
    /// <summary>
    /// Erzeugt die passende Core-Einheit zu einem Spiel-Einheitentyp.
    /// Jeder Wert der Enum wird abgedeckt — vorher fielen neun der siebzehn
    /// Typen durch die Werttabelle und kamen mit 0 Lebenspunkten zur Welt.
    /// </summary>
    public static UnitEntity Create(UnitType type, int ownerId, int x = 0, int y = 0)
    {
        var at = new Position(x, y);

        return type switch
        {
            // Zivil — der Bauarbeiter ist in AoE ein Dorfbewohner
            UnitType.Villager => new Villager(ownerId, at),
            UnitType.Builder => new Villager(ownerId, at),
            UnitType.Monk => new Monk(ownerId, at),

            // Infanterie
            UnitType.SpearMan => new SpearMan(ownerId, at),
            UnitType.HeavyInfantry => new Militia(ownerId, at),
            UnitType.ManAtArms => new Militia(ownerId, at),

            // Fernkampf
            UnitType.Archer => new Archer(ownerId, at),
            UnitType.Longbowman => new Archer(ownerId, at),
            UnitType.CavalryArcher => new Archer(ownerId, at),
            UnitType.Skirmisher => new Skirmisher(ownerId, at),

            // Berittene
            UnitType.Cavalry => new Knight(ownerId, at),
            UnitType.Paladin => new Knight(ownerId, at),
            UnitType.CamelRider => new CamelRider(ownerId, at),
            UnitType.ImperialCamel => new CamelRider(ownerId, at),

            // Belagerung
            UnitType.Ram => new Ram(ownerId, at),
            UnitType.Catapult => new Mangonel(ownerId, at),
            UnitType.Trebuchet => new Trebuchet(ownerId, at),

            _ => new Villager(ownerId, at),
        };
    }

    /// <summary>
    /// Anzeigename auf Deutsch. Den führt das Spiel selbst, weil die
    /// Kernbibliothek bewusst keine Lokalisierung kennt.
    /// </summary>
    public static string GermanName(UnitType type) => type switch
    {
        UnitType.Villager => "Dorfbewohner",
        UnitType.Builder => "Bauarbeiter",
        UnitType.SpearMan => "Speerkämpfer",
        UnitType.Skirmisher => "Plänkler",
        UnitType.Cavalry => "Ritter",
        UnitType.Archer => "Bogenschütze",
        UnitType.HeavyInfantry => "Schwere Infanterie",
        UnitType.ManAtArms => "Gewappneter",
        UnitType.CamelRider => "Kamelreiter",
        UnitType.ImperialCamel => "Imperialer Kamelreiter",
        UnitType.CavalryArcher => "Berittener Schütze",
        UnitType.Longbowman => "Langbogenschütze",
        UnitType.Paladin => "Paladin",
        UnitType.Ram => "Rammbock",
        UnitType.Catapult => "Katapult",
        UnitType.Trebuchet => "Tribok",
        UnitType.Monk => "Mönch",
        _ => type.ToString(),
    };

    /// <summary>
    /// Grobe Rolle für Anzeige und Auswahl-Gruppierung im Spiel.
    /// Für die Schadensrechnung ist sie ohne Bedeutung — dort zählt die
    /// <see cref="UnitClass"/> der Kernbibliothek.
    /// </summary>
    public static UnitCategory CategoryOf(UnitType type) => type switch
    {
        UnitType.Villager or UnitType.Builder => UnitCategory.Worker,
        UnitType.Monk => UnitCategory.Civilian,
        UnitType.SpearMan or UnitType.HeavyInfantry or UnitType.ManAtArms => UnitCategory.Infantry,
        UnitType.Archer or UnitType.Longbowman or UnitType.Skirmisher
            or UnitType.CavalryArcher => UnitCategory.Archer,
        UnitType.Cavalry or UnitType.Paladin or UnitType.CamelRider
            or UnitType.ImperialCamel => UnitCategory.Cavalry,
        UnitType.Ram or UnitType.Catapult or UnitType.Trebuchet => UnitCategory.Siege,
        _ => UnitCategory.Worker,
    };
}
