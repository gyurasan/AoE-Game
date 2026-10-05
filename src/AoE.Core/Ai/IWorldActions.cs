using AoE.Core.Entities;

namespace AoE.Core.Ai;

/// <summary>
/// Befehl-Schnittstelle für eine KI — die Aktionen, die eine KI aus der
/// Sicht des Spieler mit <see cref="IWorldState.Owner"/> an die Welt schicken
/// darf. Das Spiel setzt die Aktionen durch (IssueCommand, PlaceBuilding,
/// TrainVillager, AdvanceAge, AssignBuilder); Tests setzen sie über ein Fake.
///
/// Rückgabe-Konvention: null bedeutet „Aktion abgelehnt — kein Zustand
/// geändert". Der Aufrufer darf die Aktion einfach ignorieren oder später
/// erneut versuchen.
/// </summary>
public interface IWorldActions
{
    /// <summary>Weitergeben: die Einheit (nach <see cref="IWorldState" />-Id)
    /// sammelt an der Kachel <paramref name="x"/>, <paramref name="y"/>.
    /// Die Kachel muss für den Spieler sichtbar (erforscht) sein und eine
    /// offene Menge der passenden Ressource tragen — sonst wird die Aktion
    /// abgelehnt.</summary>
    void Gather(int unitId, int x, int y);

    /// <summary>Weitergeben: die Einheit läuft zur Kachel (Laufbefehl).
    /// Planiert nur mit dem Wissen des Spielers; ein Klick in den Nebel wird
    /// wie ein Laufbefehl verstanden.</summary>
    void Move(int unitId, int x, int y);

    /// <summary>
    /// Weitergeben: eine Baustelle anlegen, bezahlt sofort, und die
    /// ausgewählten Einheiten zur Baustelle schicken. null, wenn der Platz
    /// nicht passt oder die Rohstoffe nicht reichen.
    /// </summary>
    int? Build(BuildingType type, int x, int y, int[] builderIds);

    /// <summary>Weitergeben: im Stadtzentrum einen Dorfbewohner ausbilden (25 s,
    /// 25 Nahrung). Wird abgelehnt, wenn die Warteschlange voll ist, die
    /// Nahrung nicht reicht, der Aufstieg läuft oder die Bevölkerungsgrenze
    /// erreicht ist.</summary>
    void TrainVillager();

    /// <summary>Weitergeben: Stadtzentrum startet den Aufstieg ins nächste
    /// Zeitalter. Wird abgelehnt, wenn schon ein Aufstieg läuft, die
    /// Imperialzeit erreicht ist oder die Rohstoffe nicht reichen.</summary>
    void AdvanceAge();

    /// <summary>Weitergeben: die Einheit hilft an der Baustelle
    /// <paramref name="buildingId"/> zu bauen (wird zur Arbeitskraft).</summary>
    void AssignBuilder(int unitId, int buildingId);
}
