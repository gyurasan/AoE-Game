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
    /// </summary>
    public static BuildingEntity Create(string buildingType, int ownerId, Position center) => buildingType switch
    {
        "Stadtzentrum" => BuildingEntity.CreateTownCenter(ownerId, center),
        "Haus" => BuildingEntity.CreateHouse(ownerId, center),
        "Mühle" => BuildingEntity.CreateMill(ownerId, center),
        "Holzfällerlager" => BuildingEntity.CreateLumberCamp(ownerId, center),
        "Bergbaulager" => BuildingEntity.CreateMiningCamp(ownerId, center),
        "Kaserne" => BuildingEntity.CreateBarracks(ownerId, center),
        _ => throw new ArgumentOutOfRangeException(nameof(buildingType), buildingType,
                 "Für diesen Gebäudetyp gibt es noch kein Core-Gebäude."),
    };
}
