using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Ai;
using AoE.Core.Entities;
using Microsoft.Xna.Framework;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Age = AoE.Core.Economy.Age;

namespace AgeOfEvolutions.Core.AI;

/// <summary>
/// Brücke von der AI-Schnittstelle (<see cref="IWorldState"/>,
/// <see cref="IWorldActions"/> aus AoE.Core) auf den <see cref="RTSGameplayScreen"/>.
///
/// Sie übersetzt AI-Befehle in die owner-generic Screen-Methoden und stellt
/// den Weltzustand als Snapshots bereit. Die Brücke hält keine eigene Logik —
/// nur Übersetzung.
///
/// WICHTIG: Die Bridge wird IM SPIEL-FADEN aufgerufen (die eingebaute KI tickt
/// aus <c>Update()</c>). Sie ruft die Screen-Methoden direkt, so dass die
/// Wirkung sofort sichtbar ist. Für externe Steuerungen aus anderen Fäden
/// (REST-API) steht <see cref="RTSGameplayScreen.EnqueueOrder"/> bereit; das
/// ruft dann dieselben Methoden nur im nächsten Frame.
/// </summary>
public sealed class AiBridge : IWorldState, IWorldActions
{
    private readonly RTSGameplayScreen _screen;

    public AiBridge(RTSGameplayScreen screen)
        => _screen = screen ?? throw new ArgumentNullException(nameof(screen));

    /// <summary>Der Spieler, den die Brücke repräsentiert. Standard: 1 (KI/Gegner).</summary>
    public int Owner { get; set; } = 1;

    /// <summary>Optionales Debug-Log (vom Agenten gesetzt); Standard: null.</summary>
    public Action<string>? DebugLog { get; set; }

    // ------------------- IWorldState -------------------

    public int Width => _screen.tileMap.Width;
    public int Height => _screen.tileMap.Height;

    private Player OwnerPlayer => Owner == 0 ? _screen.player1 : _screen.player2;
    private IEnumerable<Unit> OwnUnits => _screen.units.Where(u => u.OwnerId == Owner);
    private IEnumerable<Building> OwnBuildings => _screen.tileMap.Buildings.Where(b => b.OwnerId == Owner);

    public ResourceVector Resources
    {
        get
        {
            var pool = OwnerPlayer.Resources;
            return new ResourceVector(
                pool[Resource.Food],
                pool[Resource.Wood],
                pool[Resource.Gold],
                pool[Resource.Stone]);
        }
    }

    public Age Age => OwnerPlayer.Ages.Current;
    public Age? AgeTarget => OwnerPlayer.Ages.Target;
    public float AgeProgress => OwnerPlayer.Ages.Progress;
    public int PopulationCount => OwnerPlayer.PopulationCount;
    public int PopulationCapacity => OwnerPlayer.PopulationLimit;

    public int VillagerTrainingCount
    {
        get
        {
            var b = OwnBuildings.FirstOrDefault(x => x.Core.BuildingType == BuildingType.TownCenter);
            return b is null ? 0 : b.Training.Count;
        }
    }

    public BuildingSnapshot? TownCenter
        => OwnBuildings.FirstOrDefault(b => b.Core.BuildingType == BuildingType.TownCenter)
            is { } b ? ToBuilding(b) : null;

    public IReadOnlyList<UnitSnapshot> Units
        => OwnUnits.Select(ToUnit).ToList();

    public IReadOnlyList<BuildingSnapshot> Buildings
        => OwnBuildings.Select(ToBuilding).ToList();

    public bool IsExplored(int x, int y) => _screen.tileMap.IsTileExplored(x, y, Owner);

    public bool CanPlace(BuildingType type, int x, int y, int size)
        => _screen.CanPlace(Owner, type, new Vector2(x, y));

    public (int X, int Y)? FindSource(Resource resource, int fromX, int fromY, int maxDistance)
    {
        var pos = _screen.gatherWorld.FindNearestSource(
            new Position(fromX, fromY), resource, maxDistance);
        return pos is null ? null : (pos.Value.X, pos.Value.Y);
    }

    /// <summary>
    /// Die Erkundungs-Front des Spielers: eine Zielkachel je Hauptrichtung
    /// am Rand des Bereichs, den er schon einmal gesehen hat. Die KI schickt
    /// Einheiten dorthin — der Weg wird nur über bekannte Kacheln geplant,
    /// und jeder Schritt deckt neue auf.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> ExploreTargets()
    {
        var tc = TownCenter;
        if (tc is null) return Array.Empty<(int, int)>();
        var home = (tc.X + tc.Width / 2, tc.Y + tc.Height / 2);
        return _screen.tileMap.FrontierTargets(Owner, home);
    }

    /// <summary>
    /// Alle Feinde, die der Spieler gerade mit eigenen Augen sieht —
    /// feindliche Einheiten und Gebäude. Die KI darf nur gegen was
    /// anschlagen, was sie tatsächlich sieht.
    /// </summary>
    public IReadOnlyList<EnemyInfo> VisibleEnemies()
    {
        var visible = new List<EnemyInfo>();
        foreach (var u in _screen.units)
        {
            if (u.OwnerId == Owner || u.State == AoE.Core.Entities.UnitState.Dead) continue;
            var grid = _screen.tileMap.WorldToGrid(u.Position);
            int x = (int)grid.X, y = (int)grid.Y;
            if (!_screen.tileMap.IsTileVisible(x, y, Owner)) continue;
            visible.Add(new EnemyInfo(x, y, IsBuilding: false, Health: u.Health));
        }
        foreach (var b in _screen.tileMap.Buildings)
        {
            if (b.OwnerId == Owner || b.Core.CurrentHp <= 0) continue;
            bool seen = false;
            for (int x = b.X; x < b.X + b.Width && !seen; x++)
                for (int y = b.Y; y < b.Y + b.Height && !seen; y++)
                    if (_screen.tileMap.IsTileVisible(x, y, Owner)) seen = true;
            if (!seen) continue;
            visible.Add(new EnemyInfo(b.X + b.Width / 2, b.Y + b.Height / 2,
                                       IsBuilding: true, Health: b.Health,
                                       IsTownCenter: b.Core.BuildingType == BuildingType.TownCenter));
        }
        return visible;
    }

    /// <summary>
    /// Wann hat die KI zuletzt Schaden genommen — in der selben Zeiteinheit,
    /// mit der die KI tickt (Sekunden seit Spielstart). Das Spiel führt die
    /// Uhr für beide Spieler; wenn noch nie getroffen: 0 (also „vor dem
    /// Spielbeginn").
    /// </summary>
    public double LastDamageAt => _screen.LastDamageReceivedAt(Owner);

    /// <summary>Weltzeit (Sekunden) — gleiche Skala wie
    /// <see cref="LastDamageAt"/>, damit die KI die Frische eines
    /// Treffers berechnen kann.</summary>
    public double WorldTime => _screen.GameSeconds;

    // ------------------- IWorldActions -------------------

    public void Gather(int unitId, int x, int y)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        // IssueCommand ist owner-generic: es prüft Nebel und Quelle aus Sicht
        // des Owners. Für ein Dorf (Quelle bekannt, sichtbar) wird es zu einem
        // Sammelauftrag; für einen Lauf ist es ein Bewegungsbefehl.
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
    }

    public void Move(int unitId, int x, int y)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        // Lauf = IssueCommand auf eine Kachel, die keine Quelle ist — der
        // Screen erkennt das selbst (tile.ResourceType ist null).
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
    }

    public int? Build(BuildingType type, int x, int y, int[] builderIds)
    {
        var builders = new List<Unit>();
        foreach (var id in builderIds ?? Array.Empty<int>())
        {
            if (UnitOf(id) is { } u) builders.Add(u);
        }

        if (!_screen.PlaceBuildingFor(Owner, type, new Vector2(x, y), builders))
        {
            DebugLog?.Invoke($"Build ABGELEHNT: {type} bei ({x},{y}), {builders.Count} Arbeiter");
            return null;
        }

        // Das neue Gebäude ist das letzte der Liste des Owners (AddBuilding
        // hängt hinten an).
        var newest = _screen.tileMap.Buildings.LastOrDefault(b => b.OwnerId == Owner);
        DebugLog?.Invoke($"Build OK: {type} bei ({x},{y}), {builders.Count} Arbeiter");
        return newest?.Id.GetHashCode();
    }

    public void TrainVillager() => _screen.TrainVillager(Owner);
    public void AdvanceAge() => _screen.AdvanceAge(Owner);

    public void AssignBuilder(int unitId, int buildingId)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        var b = OwnBuildings.FirstOrDefault(b => b.Id.GetHashCode() == buildingId);
        if (b is null) return;
        _screen.AssignBuilder(u, b);
    }

    /// <summary>
    /// Greift den Feind bei (<paramref name="x"/>, <paramref name="y"/>) an —
    /// im Idealfall ein sichtbares feindliches Gebäude an genau der Kachel.
    /// Findet sich dort kein Gebäude, das der Owner greifen kann, fällt der
    /// Befehl auf eine Bewegung zurück (IssueCommand-Verhalten).
    /// </summary>
    public void Attack(int unitId, int x, int y)
    {
        var u = UnitOf(unitId);
        if (u is null) return;
        _screen.IssueCommand(Owner, new List<Unit> { u }, new Vector2(x, y));
        DebugLog?.Invoke($"Attack: Einheit {unitId} auf ({x},{y})");
    }

    /// <summary>
    /// Bildet einen Soldaten aus. Die Bridge übersetzt <paramref name="soldier"/>
    /// auf den Spiel-<see cref="AoE.Core.Entities.UnitType"/> und ruft die
    /// owner-generic Screen-Methode auf. Scheitert still, wenn das Gebäude
    /// nicht ausbildet, nicht fertig ist, forscht oder kein Zeitalter erreicht
    /// ist — der Aufrufer liest dann die Warteschlange weiter.
    /// </summary>
    public void TrainSoldier(BuildingType building, UnitKind soldier)
    {
        Data.UnitType? unitType = soldier switch
        {
            UnitKind.Militia  => Data.UnitType.Militia,
            UnitKind.Spearman => Data.UnitType.SpearMan,
            UnitKind.Archer   => Data.UnitType.Archer,
            UnitKind.Cavalry  => Data.UnitType.Scout,
            _                 => null,
        };
        if (unitType is null) return;
        // Das Gebäude finden: eigenes, fertig, und es bildet genau diese
        // Einheit aus. Fehlt das, bleibt die KI untätig — die Screen-Prüfung
        // verwarf die Aktion sonst mit einer Meldung, die niemand liest.
        var b = OwnBuildings.FirstOrDefault(x => x.Core.BuildingType == building
                                                 && x.IsComplete);
        if (b is null) return;
        _screen.TrainSoldierForOwner(Owner, b, unitType.Value);
    }

    // ------------------- Snapshots -------------------

    private Unit UnitOf(int unitId)
        => _screen.units.FirstOrDefault(u => u.OwnerId == Owner && u.Id.GetHashCode() == unitId);

    private UnitSnapshot ToUnit(Unit u)
    {
        var grid = _screen.tileMap.WorldToGrid(u.Position);

        (int X, int Y, Resource)? gathering = (u.Job is { } job)
            && (u.State == AoE.Core.Entities.UnitState.Gathering
                || u.State == AoE.Core.Entities.UnitState.Returning
                || u.State == AoE.Core.Entities.UnitState.Moving)
            ? (job.Source.X, job.Source.Y, job.Resource)
            : null;

        return new UnitSnapshot
        {
            Id = u.Id.GetHashCode(),
            Owner = u.OwnerId,
            Kind = u.Core is Villager ? UnitKind.Villager : UnitKind.Militia,
            X = (int)grid.X,
            Y = (int)grid.Y,
            State = FromState(u.State),
            Gathering = gathering,
            BuildingId = u.BuildSite is { } s ? s.Id.GetHashCode() : null,
            Health = u.Health,
            MaxHealth = u.MaxHealth,
            HasAttack = u.AttackTarget is not null || u.UnitTarget is not null
        };
    }

    private BuildingSnapshot ToBuilding(Building b)
    {
        int active = 0;
        foreach (var u in OwnUnits)
            if (u.BuildSite == b
                && (u.State == AoE.Core.Entities.UnitState.Building
                    || u.State == AoE.Core.Entities.UnitState.Moving))
                active++;

        return new BuildingSnapshot
        {
            Id = b.Id.GetHashCode(),
            Owner = b.OwnerId,
            Type = b.Core.BuildingType,
            X = b.X,
            Y = b.Y,
            Width = b.Width,
            Height = b.Height,
            IsComplete = b.IsComplete,
            ConstructionProgress = b.Construction?.Progress ?? 0f,
            ActiveBuilders = active,
            Health = b.Health,
            MaxHealth = b.MaxHealth
        };
    }

    private static UnitStateKind FromState(AoE.Core.Entities.UnitState s) => s switch
    {
        AoE.Core.Entities.UnitState.Idle      => UnitStateKind.Idle,
        AoE.Core.Entities.UnitState.Gathering => UnitStateKind.Gathering,
        AoE.Core.Entities.UnitState.Building  => UnitStateKind.Building,
        AoE.Core.Entities.UnitState.Moving    => UnitStateKind.Moving,
        AoE.Core.Entities.UnitState.Returning => UnitStateKind.Returning,
        AoE.Core.Entities.UnitState.Dead      => UnitStateKind.Dead,
        _                                     => UnitStateKind.Idle
    };
}
