using AoE.Core.Entities;
using Age = AoE.Core.Economy.Age;

namespace AoE.Core.Ai;

/// <summary>
/// Lese-Zugang auf die Spielwelt für eine KI — der Spieler mit <see cref="Owner"/>
/// sieht die Karte aus seiner eigenen Sicht (Nebel, eigene Einheiten, Gebäude).
/// Reine Logik ohne MonoGame: das Spiel implementiert die Schnittstelle über eine
/// Bridge, Tests über ein Fake.
///
/// Alle Kachelkoordinaten sind Ganzzahl-Kacheln (0..Width/Height-1).
/// </summary>
public interface IWorldState
{
    int Owner { get; }
    int Width { get; }
    int Height { get; }

    /// <summary>Aktuelle Ressourcen des Spielers.</summary>
    ResourceVector Resources { get; }

    /// <summary>Das erreichte Zeitalter des Spielers.</summary>
    Age Age { get; }

    /// <summary>Das Jahr, in das der Aufstieg gerade läuft, sonst null.</summary>
    Age? AgeTarget { get; }

    /// <summary>Fortschritt des laufenden Aufstiegs zwischen 0 und 1.</summary>
    float AgeProgress { get; }

    /// <summary>Zahl der Einheiten, die der Spieler führt.</summary>
    int PopulationCount { get; }

    /// <summary>Besetzungsgrenze aus den fertigen Gebäuden.</summary>
    int PopulationCapacity { get; }

    /// <summary>Einheiten, die gerade im Stadtzentrum ausgebildet werden.</summary>
    int VillagerTrainingCount { get; }

    /// <summary>Das Stadtzentrum des Spielers, oder null.</summary>
    BuildingSnapshot TownCenter { get; }

    /// <summary>Alle Einheiten des Spielers.</summary>
    IReadOnlyList<UnitSnapshot> Units { get; }

    /// <summary>Alle Gebäude des Spielers, Baustellen inklusive
    /// (erkennbar über <see cref="BuildingSnapshot.IsComplete"/>).</summary>
    IReadOnlyList<BuildingSnapshot> Buildings { get; }

    /// <summary>
    /// Ist die Kachel einmal gesehen („erforscht")? Nicht-erforschte Kacheln
    /// kann der Spieler nicht gezielt sammeln oder darauf bauen.
    /// </summary>
    bool IsExplored(int x, int y);

    /// <summary>
    /// Kann der Spieler hier ein Gebäude der Größe <paramref name="size"/>
    /// anlegen? (freie Fläche ohne eigene Einheiten, im Nebel „bekannt".)
    /// </summary>
    bool CanPlace(BuildingType type, int x, int y, int size);

    /// <summary>Die nächste Kachel mit einer offenen Menge von <paramref name="resource"/>,
    /// an der ein Arbeiter arbeiten kann, maximal <paramref name="maxDistance"/>
    /// Kacheln von (<paramref name="fromX"/>, <paramref name="fromY"/>) entfernt —
    /// null, wenn es im Umkreis keine gibt.</summary>
    (int X, int Y)? FindSource(Resource resource, int fromX, int fromY, int maxDistance);

    /// <summary>
    /// Die Front des Spielers: eine Kachel je Hauptrichtung (nord, nordost,
    /// ost, …), am Rand des bereits Erkundeten. Die KI schickt dort
    /// Erkunder hin — <c>Move</c> auf so eine Kachel legt die Karte auf,
    /// weil der Weg nur über bekannte Kacheln geplant wird. Leere Liste,
    /// wenn der Spieler noch nichts (oder in dieser Richtung nichts) gesehen hat.
    /// </summary>
    IReadOnlyList<(int X, int Y)> ExploreTargets();

    /// <summary>
    /// Alle Feinde, die der Spieler gerade mit eigenen Augen sieht (Kachel
    /// aus seiner Sicht sichtbar): feindliche Einheiten und Gebäude.
    /// Leere Liste, wenn nichts sichtbar ist.
    /// </summary>
    IReadOnlyList<EnemyInfo> VisibleEnemies();

    /// <summary>
    /// Aktuelle Weltzeit in Sekunden (gleiche Skala wie
    /// <see cref="LastDamageAt"/>). Die KI vergleicht beide Größen,
    /// um zu wissen, wie frisch die eigene Siedlung getroffen wurde.
    /// </summary>
    double WorldTime { get; }

    /// <summary>
    /// Wann (KI-Zeit, Sekunden) hat die eigene Siedlung das letzte
    /// Mal Schaden genommen? „Schaden" = eine eigene Einheit oder
    /// ein Gebäude hat einen Treffer kassiert. 0 = noch nie getroffen,
    /// -1 (oder null-ähnlich) = Information nicht verfügbar.
    ///
    /// Die KI nutzt diese Uhr, um „unter Angriff" zu erkennen: ein
    /// sichtbarer Feind ist noch keine Attacke, ein Treffer schon.
    /// Der Abstand (Sekunden) dieser Uhr und <c>ctx.Time</c> sagt der
    /// KI, wie frisch sie selbst getroffen wurde — jünger als
    /// eine kleine Schwelle = Alarm, alle freien Einheiten schlagen
    /// sofort zurück.
    /// </summary>
    double LastDamageAt { get; }
}

/// <summary>Ein sichtbarer Feind aus Sicht der KI: Kachelkoordinaten,
/// ob es ein Gebäude ist, sein aktueller Zustand und — bei Gebäuden —
/// ob es ein Dorfzentrum ist (das primäre Raubziel, siehe
/// <see cref="EconomyAi"/>).</summary>
public readonly record struct EnemyInfo(int X, int Y, bool IsBuilding, int Health, bool IsTownCenter = false);

/// <summary>Der vierstellige Ressourcenvektor (Nahrung, Holz, Gold, Stein).</summary>
public readonly record struct ResourceVector(int Food, int Wood, int Gold, int Stone)
{
    public static readonly ResourceVector Empty = new(0, 0, 0, 0);

    public bool CanAfford(ResourceVector cost) =>
        Food >= cost.Food && Wood >= cost.Wood && Gold >= cost.Gold && Stone >= cost.Stone;

    public ResourceVector Subtract(ResourceVector o) =>
        new(Food - o.Food, Wood - o.Wood, Gold - o.Gold, Stone - o.Stone);
}

/// <summary>Einheitentyp der KI — bewusst unabhängig vom spiel-specific
/// <c>UnitType</c>. Die Bridge übersetzt auf den Spieltyp.</summary>
public enum UnitKind
{
    Villager,
    Militia,
    Spearman,
    Archer,
    Cavalry,
    Catapult,
}

/// <summary>Zustand einer Einheit aus Sicht der KI — nur die für die Wirtschaft
/// relevanten Zustände.</summary>
public enum UnitStateKind
{
    Idle,
    Gathering,
    Building,
    Moving,
    Returning,
    Dead,
}

/// <summary>Snapshot einer Einheit; die Identität ist die <see cref="Id"/>, die
/// das Spiel an die echte Unit zurückbindet.</summary>
public sealed class UnitSnapshot
{
    public int Id { get; init; }
    public int Owner { get; init; }
    public UnitKind Kind { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public UnitStateKind State { get; init; }

    /// <summary>Gültig: der Arbeiter ist an (X, Y) beim Sammeln — sonst null.</summary>
    public (int X, int Y, Resource Resource)? Gathering { get; init; }

    /// <summary>Die Einheit ist gerade an Baustelle <see cref="BuildingId"/></summary>
    public int? BuildingId { get; init; }

    public int Health { get; init; }
    public int MaxHealth { get; init; }

    /// <summary>Die Einheit führt gerade einen Angriff (Einheiten- oder
    /// Gebäude-Ziel) — das Spiel bestätigt das, damit die KI nicht bei
    /// jedem Takt "Angegriffen auf (x,y)" für dieselbe Einheit ausgeben und
    /// damit den Schlag-Timer zurücksetzen muss.</summary>
    public bool HasAttack { get; init; }
}

/// <summary>Snapshot eines Gebäudes des Spielers.</summary>
public sealed class BuildingSnapshot
{
    public int Id { get; init; }
    public int Owner { get; init; }
    public BuildingType Type { get; init; }
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    /// <summary>Fertig gebaut — keine offene Baustelle.</summary>
    public bool IsComplete { get; init; }

    /// <summary>Fortschritt der Baustelle zwischen 0 und 1 (0,5 = 50 %).</summary>
    public float ConstructionProgress { get; set; }

    /// <summary>Anzahl der Arbeiter, die daran gerade bauen.</summary>
    public int ActiveBuilders { get; set; }

    public int Health { get; init; }
    public int MaxHealth { get; init; }
}
