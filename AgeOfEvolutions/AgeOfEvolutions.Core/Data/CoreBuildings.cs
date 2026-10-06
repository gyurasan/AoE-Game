using System;
using AoE.Core.Entities;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Brücke zwischen den Gebäudetypen des Spiels und den Gebäuden aus
/// <c>AoE.Core</c> — das Gegenstück zu <see cref="CoreUnits"/>. Lebenspunkte,
/// Sichtweite und Abgabestelle kommen damit aus der Kernbibliothek.
/// </summary>
public static class CoreBuildings
{
    /// <summary>
    /// Erzeugt das Core-Gebäude zu einem Spiel-Gebäudetyp. Die Position ist
    /// die Kachel, von der aus die Sicht gerechnet wird — die Mitte der
    /// Grundfläche.
    /// Bewusst *nicht* gemappt: <c>Farm</c> — eine Farm ist kein Gebäude, das
    /// man abliefert oder abgibt, sondern ein 3×3-Feld; jede Kachel ist eine
    /// 175-Nahrungsquelle (siehe <c>TileMap.PlantFarm</c>) und wächst nach.
    /// Die Gebäude der zweiten Tastenreihe entstehen über
    /// <see cref="BuildingEntity.Create"/>: „Schießstand" ArcheryRange, „Stall"
    /// Stable, „Schmiede" Blacksmith, „Markt" Market, „Palisadenmauer"
    /// PalisadeWall, „Steinmauer" StoneWall, „Belagerungswerkstatt"
    /// SiegeWorkshop, „Universität" University, „Kloster" Monastery, „Burg"
    /// Castle, „Wunder" Wonder.
    /// </summary>
    public static BuildingEntity Create(string buildingType, int ownerId, Position center) => buildingType switch
    {
        "Stadtzentrum" => BuildingEntity.CreateTownCenter(ownerId, center),
        "Haus" => BuildingEntity.CreateHouse(ownerId, center),
        "Mühle" => BuildingEntity.CreateMill(ownerId, center),
        "Holzfällerlager" => BuildingEntity.CreateLumberCamp(ownerId, center),
        "Bergbaulager" => BuildingEntity.CreateMiningCamp(ownerId, center),
        "Kaserne" => BuildingEntity.CreateBarracks(ownerId, center),
        "Wachturm" => BuildingEntity.CreateTower(ownerId, center),
        "Schießstand" => BuildingEntity.Create(BuildingType.ArcheryRange, ownerId, center),
        "Stall" => BuildingEntity.Create(BuildingType.Stable, ownerId, center),
        "Schmiede" => BuildingEntity.Create(BuildingType.Blacksmith, ownerId, center),
        "Markt" => BuildingEntity.Create(BuildingType.Market, ownerId, center),
        "Palisadenmauer" => BuildingEntity.Create(BuildingType.PalisadeWall, ownerId, center),
        "Steinmauer" => BuildingEntity.Create(BuildingType.StoneWall, ownerId, center),
        "Belagerungswerkstatt" => BuildingEntity.Create(BuildingType.SiegeWorkshop, ownerId, center),
        "Universität" => BuildingEntity.Create(BuildingType.University, ownerId, center),
        "Kloster" => BuildingEntity.Create(BuildingType.Monastery, ownerId, center),
        "Burg" => BuildingEntity.Create(BuildingType.Castle, ownerId, center),
        "Wunder" => BuildingEntity.Create(BuildingType.Wonder, ownerId, center),
        _ => throw new ArgumentOutOfRangeException(nameof(buildingType), buildingType,
                 "Für diesen Gebäudetyp gibt es noch kein Core-Gebäude."),
    };
}
