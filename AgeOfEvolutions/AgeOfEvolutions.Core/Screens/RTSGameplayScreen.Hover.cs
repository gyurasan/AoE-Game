using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Resource = AoE.Core.Entities.Resource;
using CoreVillager = AoE.Core.Entities.Villager;

namespace AgeOfEvolutions.Core.Screens;

// Mauszeiger-Hervorhebung (C17, Nutzer 2026-10-23): „man sieht nicht immer genau, was man mit der
// Maus gerade auswählen würde ... wenn die Maus über einem Objekt ist, müsste das Objekt leicht
// hervorgehoben werden, zum Beispiel etwas heller gemacht werden." Der Zeiger prüft jeden Bild,
// was der Linksklick an dieser Stelle täte (dieselbe Reihenfolge wie LeftClick), und das getroffene
// Bild wird mit einem additiven Aufhellungspass über sich selbst gezeichnet - die Umrisse bleiben
// exakt, es ist nur heller. Gehört der Treffer einer Kachel (Baum, Busch, Haufen, Raubtier,
// Sammelgrund), hebt die Prüfung die Kachel an; gezeichnet wird an dem Objekt, das dort steht.
public partial class RTSGameplayScreen
{
    internal enum HoverKind { None, Unit, Building, Tile }

    /// <summary>Was der Linksklick an der Stelle des Zeigers treffen würde.</summary>
    internal readonly struct HoverTarget
    {
        public readonly HoverKind Kind;
        public readonly Unit Unit;
        public readonly Data.Building Building;
        public readonly int X, Y;

        HoverTarget(HoverKind kind, Unit unit, Data.Building building, int x, int y)
        {
            Kind = kind;
            Unit = unit;
            Building = building;
            X = x;
            Y = y;
        }

        public static HoverTarget None => default;
        public static HoverTarget Of(Unit unit) => new HoverTarget(HoverKind.Unit, unit, null, 0, 0);
        public static HoverTarget Of(Data.Building building) => new HoverTarget(HoverKind.Building, null, building, 0, 0);
        public static HoverTarget Tile(int x, int y) => new HoverTarget(HoverKind.Tile, null, null, x, y);
    }

    // Der Treffer des letzten Bilds; HandleRtsInput setzt ihn vor allen Frührents des
    // Eingabecods, Zeichnen liest ihn (DrawUnits, DrawTileMap).
    HoverTarget _hover;

    /// <summary>
    /// Aufhellungston: Alpha 0 - im vormultiplizierten AlphaBlend des Batches (wie beim Schmiede-
    /// feuer A5) addiert der zweite Durchgang etwa ein Sechstel der Eigenfarbe des Bilds darüber.
    /// </summary>
    static readonly Color HoverLift = new Color(40, 40, 40, 0);

    /// <summary>
    /// VERTRAG - derselbe Ablauf wie der Linksklick (LeftClick), nur ohne zu handeln:
    /// 1. Setzmodus (placing nicht null): keins.
    /// 2. Mit Auswahl und ein sichtbarer Gegner unter dem Punkt: dieser Gegner (Unit).
    /// 3. Eine eigene Einheit unter dem Punkt: sie (Unit).
    /// 4. Ohne Auswahl und ein Gebäude unter dem Punkt: es (Building).
    /// 5. Mit Auswahl und ein fremdes Gebäude darunter: es (Building - der Klick wäre ein Angriff).
    /// 6. Ohne Auswahl sonst: keins.
    /// 7. Mit Auswahl, eigenes fertiges beschädigtes Gebäude und Dorfbewohner in der Auswahl:
    ///    es (Building - der Klick wäre die Reparatur, C15).
    /// 8. Mit Auswahl und ein lebendes, sichtbares Raubtier auf der Kachel: die Kachel (Tile, RT).
    /// 9. Mit Dorfbewohner in der Auswahl und dieselbe Sammelbedingung wie IssueCommand
    ///    (Ressource, Vorrat größer 0, kein Gebäude, von Spieler 0 erforscht): die Kachel (Tile).
    /// Sonst keins.
    /// </summary>
    internal HoverTarget HoverUnder(Vector2 worldPos)
    {
        if (placing != null)
            return HoverTarget.None;
        var gridPos = WorldToGrid(worldPos);

        if (selectedUnits.Count > 0 && EnemyUnitUnder(worldPos) is { } enemy)
            return HoverTarget.Of(enemy);
        if (OwnUnitAt(worldPos) is { } own)
            return HoverTarget.Of(own);
        if (selectedUnits.Count == 0 && BuildingUnder(worldPos) is { } building)
            return HoverTarget.Of(building);
        if (selectedUnits.Count > 0 && BuildingUnder(worldPos) is { OwnerId: not 0 } foreign)
            return HoverTarget.Of(foreign);
        if (selectedUnits.Count == 0)
            return HoverTarget.None;

        bool hasVillager = selectedUnits.Any(u => u.OwnerId == 0 && u.Core is CoreVillager);

        // Eigenes beschädigtes Gebäude mit Dorfbewohnern in der Auswahl: der Klick repariert (C15)
        if (BuildingUnder(worldPos) is { IsComplete: true, OwnerId: 0 } dmg
            && dmg.Health < dmg.MaxHealth && hasVillager)
            return HoverTarget.Of(dmg);

        // Lebendes Raubtier: der Klick befiehlt den Angriff (RT)
        if (PredatorAt(gridPos, 0) is { } beast)
            return HoverTarget.Tile(beast.X, beast.Y);

        // Erforschte Rohstoffkachel mit Dorfbewohner in der Auswahl: der Klick setzt den Sammelauftrag
        if (hasVillager)
        {
            int gx = (int)gridPos.X, gy = (int)gridPos.Y;
            var tile = tileMap.GetTile(gx, gy);
            bool isSource = tile != null && tile.ResourceType.HasValue && tile.ResourceAmount > 0
                            && string.IsNullOrEmpty(tile.Building)
                            && tileMap.IsTileExplored(gx, gy, 0);
            if (isSource)
                return HoverTarget.Tile(gx, gy);
        }
        return HoverTarget.None;
    }

    /// <summary>Hefft die Hervorhebung die Kachel (x, y)? (Baum, Busch, Haufen, Tier.)</summary>
    bool HoverWantsTile(int x, int y)
        => _hover.Kind == HoverKind.Tile && _hover.X == x && _hover.Y == y;

    /// <summary>Hebt die Hervorhebung dieses Gebäude hervor?</summary>
    bool HoverWantsBuilding(Data.Building b)
        => _hover.Kind == HoverKind.Building && ReferenceEquals(_hover.Building, b);
}
