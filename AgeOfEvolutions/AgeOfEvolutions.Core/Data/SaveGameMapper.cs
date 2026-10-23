using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using AoE.Core.Economy;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using CorePosition = AoE.Core.Entities.Position;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Übersetzt zwischen der laufenden Welt (Karte, zwei Spieler, Einheiten, Gebäude) und einem
/// <see cref="SaveGame"/>. Ohne Grafik und ohne Spielbildschirm - Kamera, Spielzeit und KI
/// trägt RTSGameplayScreen.CaptureSave selbst ein.
/// </summary>
public static class SaveGameMapper
{
    /// <summary>
    /// Hält die Welt in einem Spielstand fest. Ändert nichts an der Welt.
    /// VERTRAG:
    /// - Version = SaveGame.CURRENT_VERSION, MapSize = <paramref name="mapSize"/>, Width, Height
    ///   und TileSize von <paramref name="map"/>. SavedAt, GameSeconds, AnimationTime, Kamera
    ///   und KI bleiben auf ihren Standardwerten (die setzt der Spielbildschirm).
    /// - Players: genau zwei, erst <paramref name="player1"/>, dann <paramref name="player2"/>:
    ///   Id, Name, Civilization; Food, Wood, Gold, Stone aus player.Resources[...];
    ///   Ages = player.Ages.ToState(); Techs = player.Techs.ToState();
    ///   Explored = map.ExploredMask(player.Id).
    /// - Tiles: Width · Height Einträge, Index y · Width + x, je Kachel map.GetTile(x, y) mit
    ///   Type, GroundType (G15), Walkable, Buildable, ResourceType, ResourceAmount, Food, Wear,
    ///   Farm, FarmRegrow, FarmFood, FarmCol, FarmRow, FarmSize, RoadLevel und
    ///   Animal: null, wenn tile.Animal null ist,
    ///   sonst ein AnimalSave mit allen neun Werten des Tiers (Look, FacingLeft, WanderTimer,
    ///   FromX, FromY, Glide, Slaughtered, Health, AttackCooldown - nicht Strike).
    /// - Buildings: je Gebäude aus map.Buildings in dieser Reihenfolge: X, Y, Type,
    ///   Size = Width, OwnerId, Health, Construction = building.Construction?.ToState(),
    ///   Training = building.Training.ToState(), Research = building.Research.ToState().
    /// - Units: je Einheit aus map.Units in dieser Reihenfolge: Id, Type, OwnerId,
    ///   X/Y = Position, TargetX/TargetY = TargetPosition, Path als neue Liste von
    ///   PointSave(p.X, p.Y), Pace, State, Health, AttackTimer, Job = unit.Job?.ToState();
    ///   BuildSite und AttackTarget = Index des Gebäudes in map.Buildings (IndexOf), UnitTarget =
    ///   Index der Einheit in map.Units - jeweils null, wenn kein Ziel gesetzt ist oder es nicht
    ///   (mehr) in der Liste steht.
    /// </summary>
    /// <exception cref="ArgumentNullException">map, player1 oder player2 ist null.</exception>
    public static SaveGame Capture(TileMap map, Player player1, Player player2, MapSize mapSize)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(player1);
        ArgumentNullException.ThrowIfNull(player2);

        // Index eines Gebäudes bzw. einer Einheit in der Liste, null wenn nicht gefunden.
        int? IndexOf<T>(List<T> list, T item)
            => item is null ? null : (int?)(list.IndexOf(item) >= 0 ? list.IndexOf(item) : null);

        SaveGame save = new()
        {
            Version = SaveGame.CURRENT_VERSION,
            MapSize = mapSize,
            Width = map.Width,
            Height = map.Height,
            TileSize = map.TileSize,
        };

        // Spieler in fester Reihenfolge: erst player1, dann player2.
        save.Players.Add(new PlayerSave
        {
            Id = player1.Id,
            Name = player1.Name,
            Civilization = player1.Civilization,
            Food = player1.Resources[Resource.Food],
            Wood = player1.Resources[Resource.Wood],
            Gold = player1.Resources[Resource.Gold],
            Stone = player1.Resources[Resource.Stone],
            Ages = player1.Ages.ToState(),
            Techs = player1.Techs.ToState(),
            Explored = map.ExploredMask(player1.Id),
        });
        save.Players.Add(new PlayerSave
        {
            Id = player2.Id,
            Name = player2.Name,
            Civilization = player2.Civilization,
            Food = player2.Resources[Resource.Food],
            Wood = player2.Resources[Resource.Wood],
            Gold = player2.Resources[Resource.Gold],
            Stone = player2.Resources[Resource.Stone],
            Ages = player2.Ages.ToState(),
            Techs = player2.Techs.ToState(),
            Explored = map.ExploredMask(player2.Id),
        });

        // Kacheln: äußere Schleife y, innere x, Index y · Width + x.
        for (int y = 0; y < map.Height; y++)
        {
            for (int x = 0; x < map.Width; x++)
            {
                Tile tile = map.GetTile(x, y);
                save.Tiles.Add(new TileSave
                {
                    Type = tile.Type,
                    GroundType = tile.GroundType,
                    Walkable = tile.Walkable,
                    Buildable = tile.Buildable,
                    ResourceType = tile.ResourceType,
                    ResourceAmount = tile.ResourceAmount,
                    Food = tile.Food,
                    Animal = tile.Animal is null ? null : new AnimalSave
                    {
                        Look = tile.Animal.Look,
                        FacingLeft = tile.Animal.FacingLeft,
                        WanderTimer = tile.Animal.WanderTimer,
                        FromX = tile.Animal.FromX,
                        FromY = tile.Animal.FromY,
                        Glide = tile.Animal.Glide,
                        Slaughtered = tile.Animal.Slaughtered,
                        Health = tile.Animal.Health,
                        AttackCooldown = tile.Animal.AttackCooldown,
                    },
                    Wear = tile.Wear,
                    Farm = tile.Farm,
                    FarmRegrow = tile.FarmRegrow,
                    FarmFood = tile.FarmFood,
                    FarmCol = tile.FarmCol,
                    FarmRow = tile.FarmRow,
                    FarmSize = tile.FarmSize,
                    RoadLevel = tile.RoadLevel,
                });
            }
        }

        // Gebäude in der Reihenfolge von map.Buildings.
        foreach (Building building in map.Buildings)
        {
            save.Buildings.Add(new BuildingSave
            {
                X = building.X,
                Y = building.Y,
                Type = building.Type,
                Size = building.Width,
                OwnerId = building.OwnerId,
                Health = building.Health,
                Construction = building.Construction?.ToState(),
                Repair = building.Repair?.ToState(),
                Training = building.Training.ToState(),
                Research = building.Research.ToState(),
            });
        }

        // Einheiten in der Reihenfolge von map.Units; Querverweise als Index.
        foreach (Unit unit in map.Units)
        {
            save.Units.Add(new UnitSave
            {
                Id = unit.Id,
                Type = unit.Type,
                OwnerId = unit.OwnerId,
                X = unit.Position.X,
                Y = unit.Position.Y,
                TargetX = unit.TargetPosition.X,
                TargetY = unit.TargetPosition.Y,
                Path = unit.Path.Select(p => new PointSave(p.X, p.Y)).ToList(),
                Pace = unit.Pace,
                State = unit.State,
                Health = unit.Health,
                AttackTimer = unit.AttackTimer,
                Job = unit.Job?.ToState(),
                BuildSite = IndexOf(map.Buildings, unit.BuildSite),
                AttackTarget = IndexOf(map.Buildings, unit.AttackTarget),
                UnitTarget = IndexOf(map.Units, unit.UnitTarget),
            });
        }

        return save;
    }

    /// <summary>
    /// Baut die Welt aus einem Spielstand wieder auf.
    /// VERTRAG, in dieser Reihenfolge:
    /// 1. Prüfen: <paramref name="save"/> null → ArgumentNullException. InvalidDataException
    ///    (Meldung deutsch), wenn save.Version nicht SaveGame.CURRENT_VERSION ist, Width oder
    ///    Height nicht größer als 0, save.Players nicht genau zwei Einträge hat oder
    ///    save.Tiles nicht genau Width · Height.
    /// 2. map = TileMap.CreateBlank(Width, Height, TileSize, MapSettings.ForSize(save.MapSize)).
    /// 3. Kacheln: jede Kachel map.GetTile(x, y) bekommt alle Werte ihres TileSave (Index
    ///    y · Width + x); Animal null bleibt null, sonst ein neues WildAnimal mit den neun
    ///    Werten (Strike bleibt 0). GroundType = ts.GroundType ?? ts.Type (ältere Spielstände ohne den Boden
    ///    der Kartenerzeugung, G15). Building bleibt null - das setzt Schritt 5.
    /// 4. Spieler: je PlayerSave new Player(Id, Name, Civilization); danach steht jeder der vier
    ///    Rohstoffe GENAU auf dem gespeicherten Wert (player.Resources.Add(r, gespeichert -
    ///    player.Resources[r]) - der Konstruktor hat schon Startkapital und Volksbonus gebucht);
    ///    Ages.Restore(Ages) und Techs.Restore(Techs), soweit nicht null. Danach (C16)
    ///    map.CurrentRoadLevel = das größere von TileMap.LevelOfAge(Ages.Current) der beiden
    ///    Spieler - sonst entstünden nach dem Laden in der Feudalzeit keine neuen Wege.
    /// 5. Gebäude in Listenreihenfolge: map.AddBuilding(X, Y, Type, OwnerId, Size); dann
    ///    TechEffects.ApplyAll(Besitzer.Techs, building.Core) (Besitzer = Spieler mit dieser Id);
    ///    erst danach Health; Construction = Construction.FromState(...) oder null;
    ///    Training.Restore und Research.Restore, soweit nicht null.
    /// 6. Einheiten in Listenreihenfolge: map.AddUnit(Type, 0, 0, OwnerId), dann Id, Position
    ///    (X, Y), TargetPosition, Path (neue Liste von Vector2), Pace, State; dann
    ///    TechEffects.ApplyAll(Besitzer.Techs, unit.Core) und erst danach Health; AttackTimer;
    ///    Job = GatherJob.FromState(...) oder null. Die Einheiten kommen NICHT in
    ///    player.Units (das macht der Spielbildschirm wie beim neuen Spiel).
    /// 7. Querverweise, erst wenn alle Gebäude und Einheiten stehen: BuildSite und
    ///    AttackTarget = map.Buildings[Index], UnitTarget = map.Units[Index]; null, wenn der
    ///    Index null ist oder außerhalb der Liste liegt.
    /// 8. map.RebuildWildAnimals(); map.RestoreExplored(Id, Explored) je Spieler;
    ///    map.InvalidateNavigation().
    /// Rückgabe: die Karte und die beiden Spieler in der gespeicherten Reihenfolge.
    /// </summary>
    public static (TileMap Map, Player Player1, Player Player2) Restore(SaveGame save)
    {
        // 1. Prüfen.
        ArgumentNullException.ThrowIfNull(save);
        if (save.Version != SaveGame.CURRENT_VERSION)
            throw new InvalidDataException("Der Spielstand hat eine andere Fassung und kann nicht geladen werden.");
        if (save.Width <= 0 || save.Height <= 0)
            throw new InvalidDataException("Der Spielstand hat eine ungültige Kartenbreite oder -höhe.");
        if (save.Players.Count != 2)
            throw new InvalidDataException("Der Spielstand muss genau zwei Spieler enthalten.");
        if (save.Tiles.Count != save.Width * save.Height)
            throw new InvalidDataException("Die Anzahl der Kacheln im Spielstand passt nicht zur Kartenbreite und -höhe.");

        // 2. Leere Karte.
        TileMap map = TileMap.CreateBlank(save.Width, save.Height, save.TileSize, MapSettings.ForSize(save.MapSize));

        // 3. Kacheln auffüllen; das Gebäude bleibt null (Schritt 5).
        for (int y = 0; y < save.Height; y++)
        {
            for (int x = 0; x < save.Width; x++)
            {
                TileSave ts = save.Tiles[y * save.Width + x];
                Tile tile = map.GetTile(x, y);
                tile.Type = ts.Type;
                // Ältere Spielstände ohne den Boden der Erzeugung: der Typ (G15).
                tile.GroundType = ts.GroundType ?? ts.Type;
                tile.Walkable = ts.Walkable;
                tile.Buildable = ts.Buildable;
                tile.ResourceType = ts.ResourceType;
                tile.ResourceAmount = ts.ResourceAmount;
                tile.Food = ts.Food;
                tile.Animal = ts.Animal is null ? null : new WildAnimal
                {
                    Look = ts.Animal.Look,
                    FacingLeft = ts.Animal.FacingLeft,
                    WanderTimer = ts.Animal.WanderTimer,
                    FromX = ts.Animal.FromX,
                    FromY = ts.Animal.FromY,
                    Glide = ts.Animal.Glide,
                    Slaughtered = ts.Animal.Slaughtered,
                    Health = ts.Animal.Health,
                    AttackCooldown = ts.Animal.AttackCooldown,
                };
                tile.Wear = ts.Wear;
                tile.Farm = ts.Farm;
                tile.FarmRegrow = ts.FarmRegrow;
                tile.FarmFood = ts.FarmFood;
                tile.FarmCol = ts.FarmCol;
                tile.FarmRow = ts.FarmRow;
                tile.FarmSize = ts.FarmSize;
                tile.RoadLevel = ts.RoadLevel;
            }
        }

        // 4. Spieler; Rohstoffe exakt auf den gespeicherten Wert setzen.
        Player player1 = new(save.Players[0].Id, save.Players[0].Name, save.Players[0].Civilization);
        Player player2 = new(save.Players[1].Id, save.Players[1].Name, save.Players[1].Civilization);
        Player[] players = { player1, player2 };
        for (int i = 0; i < 2; i++)
        {
            PlayerSave ps = save.Players[i];
            Player player = players[i];
            player.Resources.Add(Resource.Food, ps.Food - player.Resources[Resource.Food]);
            player.Resources.Add(Resource.Wood, ps.Wood - player.Resources[Resource.Wood]);
            player.Resources.Add(Resource.Gold, ps.Gold - player.Resources[Resource.Gold]);
            player.Resources.Add(Resource.Stone, ps.Stone - player.Resources[Resource.Stone]);
            if (ps.Ages is not null)
                player.Ages.Restore(ps.Ages);
            if (ps.Techs is not null)
                player.Techs.Restore(ps.Techs);
        }
        // Die erreichte Weg-Stufe (C16): das größere der beiden Zeitalter.
        map.CurrentRoadLevel = Math.Max(TileMap.LevelOfAge(player1.Ages.Current), TileMap.LevelOfAge(player2.Ages.Current));

        // 5. Gebäude in Listenreihenfolge.
        foreach (BuildingSave bs in save.Buildings)
        {
            Player owner = players.FirstOrDefault(p => p.Id == bs.OwnerId)
                ?? throw new InvalidDataException("Der Spielstand enthält ein Gebäude mit unbekanntem Besitzer.");
            Building building = map.AddBuilding(bs.X, bs.Y, bs.Type, bs.OwnerId, bs.Size);
            TechEffects.ApplyAll(owner.Techs, building.Core);
            building.Health = bs.Health;
            building.Construction = bs.Construction is null ? null : Construction.FromState(bs.Construction);
            building.Repair = bs.Repair is null ? null : Repair.FromState(bs.Repair);
            if (bs.Training is not null)
                building.Training.Restore(bs.Training);
            if (bs.Research is not null)
                building.Research.Restore(bs.Research);
        }

        // 6. Einheiten in Listenreihenfolge; nicht in player.Units.
        foreach (UnitSave us in save.Units)
        {
            Player owner = players.FirstOrDefault(p => p.Id == us.OwnerId)
                ?? throw new InvalidDataException("Der Spielstand enthält eine Einheit mit unbekanntem Besitzer.");
            Unit unit = map.AddUnit(us.Type, 0, 0, us.OwnerId);
            unit.Id = us.Id;
            unit.Position = new Vector2(us.X, us.Y);
            unit.TargetPosition = new Vector2(us.TargetX, us.TargetY);
            unit.Path = us.Path is null ? new List<Vector2>() : us.Path.Select(p => new Vector2(p.X, p.Y)).ToList();
            unit.Pace = us.Pace;
            unit.State = us.State;
            TechEffects.ApplyAll(owner.Techs, unit.Core);
            unit.Health = us.Health;
            unit.AttackTimer = us.AttackTimer;
            unit.Job = us.Job is null ? null : GatherJob.FromState(us.Job);
        }

        // 7. Querverweise, erst wenn alle Gebäude und Einheiten stehen.
        for (int i = 0; i < save.Units.Count; i++)
        {
            UnitSave us = save.Units[i];
            Unit unit = map.Units[i];
            unit.BuildSite = us.BuildSite is null || us.BuildSite < 0 || us.BuildSite >= map.Buildings.Count
                ? null : map.Buildings[us.BuildSite.Value];
            unit.AttackTarget = us.AttackTarget is null || us.AttackTarget < 0 || us.AttackTarget >= map.Buildings.Count
                ? null : map.Buildings[us.AttackTarget.Value];
            unit.UnitTarget = us.UnitTarget is null || us.UnitTarget < 0 || us.UnitTarget >= map.Units.Count
                ? null : map.Units[us.UnitTarget.Value];
        }

        // 8. Tiere, Erkundung und Navigation.
        map.RebuildWildAnimals();
        for (int i = 0; i < 2; i++)
        {
            PlayerSave ps = save.Players[i];
            if (ps.Explored is not null)
                map.RestoreExplored(players[i].Id, ps.Explored);
        }
        map.InvalidateNavigation();

        return (map, player1, player2);
    }
}
