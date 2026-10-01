using System;
using AoE.Core.Economy;
using CorePosition = AoE.Core.Entities.Position;
using Resource = AoE.Core.Entities.Resource;
using ResourceDropOff = AoE.Core.Entities.ResourceDropOff;

namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Die Kachelkarte aus Sicht eines <see cref="GatherJob"/>. Übersetzt dessen
/// Fragen in Kachelzugriffe — die Logik des Kreislaufs steckt in AoE.Core.
/// </summary>
public sealed class TileMapGatherWorld : IGatherWorld
{
    private readonly TileMap _map;

    public TileMapGatherWorld(TileMap map) => _map = map;

    public int AmountAt(CorePosition cell, Resource resource)
    {
        var tile = _map.GetTile(cell.X, cell.Y);
        return tile != null && tile.ResourceType == resource ? tile.ResourceAmount : 0;
    }

    public int Harvest(CorePosition cell, Resource resource, int amount)
    {
        var tile = _map.GetTile(cell.X, cell.Y);
        if (tile == null || tile.ResourceType != resource || tile.ResourceAmount <= 0)
            return 0;

        int taken = Math.Min(amount, tile.ResourceAmount);
        tile.ResourceAmount -= taken;
        if (tile.ResourceAmount == 0)
            _map.ClearResource(tile);
        return taken;
    }

    /// <summary>
    /// Kann ein Dorfbewohner an dieser Kachel arbeiten? Ja, wenn er sie betreten
    /// kann oder wenn eine Nachbarkachel begehbar ist – Fisch wird vom Ufer aus
    /// gefangen. Kacheln unter Gebäuden zählen nie.
    /// </summary>
    private bool IsWorkable(int x, int y)
    {
        var tile = _map.GetTile(x, y);
        if (tile == null || !string.IsNullOrEmpty(tile.Building))
            return false;
        if (_map.IsWalkable(x, y))
            return true;

        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && _map.IsWalkable(x + dx, y + dy))
                    return true;
        return false;
    }

    /// <summary>
    /// Nächste Kachel mit dieser Ressource nach Luftlinie, an der ein
    /// Dorfbewohner arbeiten kann (<see cref="IsWorkable"/>).
    /// </summary>
    public CorePosition? FindNearestSource(CorePosition from, Resource resource, int maxDistance)
    {
        CorePosition? best = null;
        double bestDistance = (double)maxDistance * maxDistance;

        for (int x = from.X - maxDistance; x <= from.X + maxDistance; x++)
        {
            for (int y = from.Y - maxDistance; y <= from.Y + maxDistance; y++)
            {
                var cell = new CorePosition(x, y);
                double distance = cell.DistanceSquared(from);
                if (distance > bestDistance || (best != null && distance == bestDistance))
                    continue;
                if (AmountAt(cell, resource) <= 0 || !IsWorkable(x, y))
                    continue;

                best = cell;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>
    /// Die begehbare Kachel direkt am Rand einer passenden eigenen
    /// Abgabestelle, die <paramref name="from"/> am nächsten liegt. Gebäude
    /// sind nicht begehbar, abgeliefert wird von der Nachbarkachel aus.
    /// </summary>
    public CorePosition? FindNearestDropOff(CorePosition from, int ownerId, Resource resource)
    {
        CorePosition? best = null;
        double bestDistance = double.MaxValue;

        foreach (var building in _map.Buildings)
        {
            // Lager zählen erst, wenn sie fertig gebaut sind
            if (building.OwnerId != ownerId || !building.IsComplete
                || !Accepts(building.Core.DropOffType, resource))
                continue;

            for (int x = building.X - 1; x <= building.X + building.Width; x++)
            {
                for (int y = building.Y - 1; y <= building.Y + building.Height; y++)
                {
                    bool onRing = x == building.X - 1 || x == building.X + building.Width
                               || y == building.Y - 1 || y == building.Y + building.Height;
                    if (!onRing || !_map.IsWalkable(x, y))
                        continue;

                    var cell = new CorePosition(x, y);
                    double distance = cell.DistanceSquared(from);
                    if (distance < bestDistance)
                    {
                        best = cell;
                        bestDistance = distance;
                    }
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Abgabestellen laut Spezifikation: das Stadtzentrum nimmt alles, die
    /// Lager je ihre Ressource. Welche Abgabestelle ein Gebäude ist, weiß sein
    /// Core-Gebäude; Lager baut der Spieler seit C5 selbst.
    /// </summary>
    private static bool Accepts(ResourceDropOff dropOff, Resource resource) => dropOff switch
    {
        ResourceDropOff.TownCenter => true,
        ResourceDropOff.Mill => resource == Resource.Food,
        ResourceDropOff.LumberCamp => resource == Resource.Wood,
        ResourceDropOff.MiningCamp => resource is Resource.Gold or Resource.Stone,
        _ => false,
    };
}
