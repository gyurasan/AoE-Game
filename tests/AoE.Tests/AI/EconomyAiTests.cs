using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using System.Diagnostics.CodeAnalysis;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Fake-World für die KI-Tests — hält in-memory-Zustand und zählt die
/// aufgerufenen Aktionen.
/// </summary>
public sealed class FakeWorld : IWorldState, IWorldActions
{
    public List<UnitSnapshot> AllUnits { get; } = new();
    public List<BuildingSnapshot> AllBuildings { get; } = new();

    public int Owner { get; init; } = 1;
    public int Width { get; init; } = 64;
    public int Height { get; init; } = 64;

    public ResourceVector Resources { get; set; } = new(300, 500, 100, 200);
    public Age Age { get; set; } = Age.Dark;
    public Age? AgeTarget { get; set; }
    public float AgeProgress => AgeTarget is null ? 0f : 1f;
    public int PopulationCount { get; set; } = 4;
    public int PopulationCapacity { get; set; } = 5;
    public int VillagerTrainingCount { get; set; }

    [AllowNull] public BuildingSnapshot? TownCenter { get; set; }
    public IReadOnlyList<UnitSnapshot> Units => AllUnits;
    public IReadOnlyList<BuildingSnapshot> Buildings => AllBuildings;

    private readonly List<(int X, int Y)> _notExplored = new();
    public void MarkNotExplored(int x, int y) => _notExplored.Add((x, y));
    public bool IsExplored(int x, int y) => !_notExplored.Contains((x, y));

    private readonly List<(int X, int Y)> _blocked = new();
    public void BlockPlace(int x, int y) => _blocked.Add((x, y));
    public bool CanPlace(BuildingType t, int x, int y, int size) =>
        !_blocked.Contains((x, y)) && IsExplored(x, y);

    private Dictionary<Resource, (int X, int Y)> _sources = new();
    public void AddSource(Resource t, int x, int y) => _sources[t] = (x, y);
    public (int X, int Y)? FindSource(Resource r, int fx, int fy, int md)
    {
        if (!_sources.TryGetValue(r, out var src)) return null;
        int dist = Math.Max(Math.Abs(src.X - fx), Math.Abs(src.Y - fy));
        return dist <= md ? (src.X, src.Y) : null;
    }

    // --- Actions-Log
    public List<string> Log { get; } = new();
    public void Gather(int unitId, int x, int y) => Log.Add($"gather({unitId},{x},{y})");
    public void Move(int unitId, int x, int y) => Log.Add($"move({unitId},{x},{y})");
    public int? Build(BuildingType t, int x, int y, int[] builders)
    {
        Log.Add($"build({t},{x},{y},n={builders.Length})");
        return AllBuildings.Count + 1;
    }
    public void TrainVillager() => Log.Add("train()");
    public void AdvanceAge()
    {
        if (AgeTarget is null)
            AgeTarget = Age == Age.Dark ? Age.Feudal : Age == Age.Feudal ? Age.Castle : Age.Imperial;
        Log.Add("ageup()");
    }
    public void AssignBuilder(int unitId, int buildingId) => Log.Add($"assign({unitId}@{buildingId})");

    // --- Factory-Helfer
    private int _next = 1;
    public int AddVillager(int x = 0, int y = 0, UnitStateKind s = UnitStateKind.Idle,
        (int X, int Y, Resource R)? gathering = null, int? site = null)
    {
        int id = _next++;
        AllUnits.Add(new UnitSnapshot { Id = id, Kind = UnitKind.Villager, X = x, Y = y,
                                        State = s, Gathering = gathering, BuildingId = site,
                                        Health = 25, MaxHealth = 25 });
        return id;
    }
    public BuildingSnapshot AddBuilding(BuildingType t, int x = 0, int y = 0, bool complete = false)
    {
        var b = new BuildingSnapshot { Id = AllBuildings.Count + 1, Type = t, X = x, Y = y,
                                       Width = 4, Height = 4, IsComplete = complete,
                                       ConstructionProgress = complete ? 1 : 0 };
        AllBuildings.Add(b);
        if (t == BuildingType.TownCenter && TownCenter is null)
            TownCenter = b;
        return b;
    }
}

/// <summary>Unit-Tests für <see cref="EconomyAi"/>.</summary>
public class EconomyAiTests
{
    static readonly EconomyAi ai = new();

    [Fact]
    public void FillSiteAssignsFreeWorkers()
    {
        var w = new FakeWorld { PopulationCapacity = 20, Resources = new ResourceVector(50,50,50,50) };
        var b = w.AddBuilding(BuildingType.House, 10, 10, complete: false);
        b.ActiveBuilders = 1;
        int w1 = w.AddVillager(); int w2 = w.AddVillager(); int w3 = w.AddVillager();

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Contains($"assign({w1}@{b.Id})", w.Log);
        Assert.Contains($"assign({w2}@{b.Id})", w.Log);
        Assert.Contains($"assign({w3}@{b.Id})", w.Log);
    }

    [Fact]
    public void DoesNotTrainWhenPopulationCapReached()
    {
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(500, 500, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        w.AddVillager();

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain("train()", w.Log);
    }

    [Fact]
    public void TrainsVillagersWhenPossible()
    {
        var w = new FakeWorld { PopulationCount = 4, PopulationCapacity = 5,
                                 Resources = new ResourceVector(500, 500, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        w.AddVillager(0, 0, UnitStateKind.Gathering, gathering: (10, 10, Resource.Wood));

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Contains("train()", w.Log);
    }

    [Fact]
    public void DoesNotTrainWhileAgeUpRunning()
    {
        var w = new FakeWorld { PopulationCapacity = 20, AgeTarget = Age.Feudal,
                                 Resources = new ResourceVector(500, 500, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        w.AddVillager();

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain("train()", w.Log);
    }

    [Fact]
    public void SendsIdleWorkersToSources()
    {
        var w = new FakeWorld { Resources = new ResourceVector(50, 50, 50, 50), PopulationCapacity = 20 };
        int wId = w.AddVillager(x: 4, y: 4);
        w.AddSource(Resource.Wood, 10, 10);

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Contains($"gather({wId},10,10)", w.Log);
    }

    [Fact]
    public void DoesNotSendAlreadyWorkingWorkers()
    {
        var w = new FakeWorld { Resources = new ResourceVector(50, 50, 50, 50), PopulationCapacity = 20 };
        w.AddVillager(x: 4, y: 4, s: UnitStateKind.Gathering, gathering: (10, 10, Resource.Wood));
        w.AddSource(Resource.Wood, 10, 10);

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain("gather(", w.Log);
    }

    [Fact]
    public void PrefersWoodOverFood()
    {
        var w = new FakeWorld { Resources = new ResourceVector(50, 50, 50, 50), PopulationCapacity = 20 };
        int wId = w.AddVillager(x: 4, y: 4);
        w.AddSource(Resource.Wood, 10, 10);
        w.AddSource(Resource.Food, 6, 6);

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Contains($"gather({wId},10,10)", w.Log);
        Assert.DoesNotContain($"gather({wId},6,6)", w.Log);
    }

    [Fact]
    public void AgeUpCostCheckFeudal()
    {
        var poor = new FakeWorld { Resources = new ResourceVector(400, 500, 100, 200), PopulationCapacity = 20 };
        poor.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        ai.Tick(poor, poor, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", poor.Log);

        var rich = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200), PopulationCapacity = 20 };
        rich.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        ai.Tick(rich, rich, 0.1f, new AiContext(1));
        Assert.Contains("ageup()", rich.Log);
    }

    [Fact]
    public void AgeUpStopsAtImperial()
    {
        var w = new FakeWorld { Age = Age.Imperial, Resources = new ResourceVector(5000, 5000, 5000, 5000),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", w.Log);
    }

    [Fact]
    public void AgeUpWhileRunningDoesNotRepeat()
    {
        var w = new FakeWorld { AgeTarget = Age.Feudal, Resources = new ResourceVector(5000, 5000, 5000, 5000),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", w.Log);
    }

    [Fact]
    public void WorkerStaysIdleIfSourceOutOfRange()
    {
        var w = new FakeWorld { PopulationCapacity = 20 };
        int wId = w.AddVillager(x: 0, y: 0);
        w.AddSource(Resource.Wood, 30, 30);   // Chebyshev 30 > 16 (Radius)

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain($"gather({wId},", w.Log);
    }

    [Fact]
    public void WorkerStaysIdleIfSourceNotExplored()
    {
        var w = new FakeWorld { PopulationCapacity = 20 };
        int wId = w.AddVillager(x: 4, y: 4);
        w.AddSource(Resource.Wood, 10, 10);   // in Reichweite ...
        w.MarkNotExplored(10, 10);            // ...aber im Nebel

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.DoesNotContain($"gather({wId},10,10)", w.Log);
    }
}
