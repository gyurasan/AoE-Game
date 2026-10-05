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
            return null;

        // Das neue Gebäude ist das letzte der Liste des Owners (AddBuilding
        // hängt hinten an).
        var newest = _screen.tileMap.Buildings.LastOrDefault(b => b.OwnerId == Owner);
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
            MaxHealth = u.MaxHealth
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
