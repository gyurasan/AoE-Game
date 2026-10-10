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

    private List<(int X, int Y)> _explore = new();
    public void AddExploreTarget(int x, int y) => _explore.Add((x, y));
    public IReadOnlyList<(int X, int Y)> ExploreTargets() => _explore;

    private List<EnemyInfo> _enemies = new();
    public void AddEnemy(EnemyInfo e) => _enemies.Add(e);
    public IReadOnlyList<EnemyInfo> VisibleEnemies() => _enemies;

    /// <summary>Simuliere „gerade getroffen": der Zeitstempel, zu dem
    /// die Siedlung zuletzt Schaden kassiert hat. 0 = noch nie.</summary>
    public double LastDamageAt { get; set; }

    /// <summary>Weltzeit (Sekunden), mit der die KI die Frische eines
    /// Schadens bewertet. Im Fake frei wählbar.</summary>
    public double WorldTime { get; set; }

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
        // Wie im Spiel: die Baustelle existiert danach (unfertig) und die
        // genannten Arbeiter sind ihre Erbauer.
        var site = AddBuilding(t, x, y, complete: false);
        foreach (int builderId in builders)
            Log.Add($"assign({builderId}@{site.Id})");
        return site.Id;
    }
    public void TrainVillager() => Log.Add("train()");
    public void AdvanceAge()
    {
        if (AgeTarget is null)
            AgeTarget = Age == Age.Dark ? Age.Feudal : Age == Age.Feudal ? Age.Castle : Age.Imperial;
        Log.Add("ageup()");
    }
    public void AssignBuilder(int unitId, int buildingId) => Log.Add($"assign({unitId}@{buildingId})");
    public void Attack(int unitId, int x, int y) => Log.Add($"attack({unitId},{x},{y})");
    public void TrainSoldier(BuildingType b, UnitKind s) => Log.Add($"soldier({b},{s})");

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
    /// <summary>Milizsoldat im Feld — für die Zeitalter-/Angriffs-Gates.</summary>
    public int AddSoldier(int x = 0, int y = 0, UnitKind kind = UnitKind.Militia,
        UnitStateKind s = UnitStateKind.Idle)
    {
        int id = _next++;
        AllUnits.Add(new UnitSnapshot { Id = id, Kind = kind, X = x, Y = y,
                                        State = s, Health = 100, MaxHealth = 100 });
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
    // Instance-Feld (nicht static!), damit jede xUnit-Testmethode eine
    // frische KI bekommt — die KI hält Zustände (_scoutId, _lastRaidTime),
    // die sonst zwischen Methoden weiterlaufen würden.
    readonly EconomyAi ai = new();

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
    public void PrefersFoodOverWood()
    {
        var w = new FakeWorld { Resources = new ResourceVector(50, 50, 50, 50), PopulationCapacity = 20 };
        int wId = w.AddVillager(x: 4, y: 4);
        w.AddSource(Resource.Wood, 10, 10);
        w.AddSource(Resource.Food, 6, 6);

        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Contains($"gather({wId},6,6)", w.Log);
        Assert.DoesNotContain($"gather({wId},10,10)", w.Log);
    }

    [Fact]
    public void AgeUpCostCheckFeudal()
    {
        // Neue Doktrin: Aufstieg erst mit wehrbereiter Armee (Kaserne +
        // Soll-Soldaten), Kosten aufführbar UND 100 Nahrung Reserve danach.
        void ArmWorld(FakeWorld w)
        {
            w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
            w.AddBuilding(BuildingType.Barracks, 12, 12, complete: true);
            for (int i = 0; i < AiProfile.Standard.SoldierTarget; i++)
                w.AddSoldier();
        }

        // 1) Nahrung reicht für den Kauf, aber Reserve fehlt (< 500+100) → nein.
        var shortReserve = new FakeWorld { Resources = new ResourceVector(550, 500, 100, 200), PopulationCapacity = 30 };
        ArmWorld(shortReserve);
        ai.Tick(shortReserve, shortReserve, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", shortReserve.Log);

        // 2) Nahrung reicht gar nicht für die 500 Kosten → nein.
        var broke = new FakeWorld { Resources = new ResourceVector(300, 500, 100, 200), PopulationCapacity = 30 };
        ArmWorld(broke);
        ai.Tick(broke, broke, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", broke.Log);

        // 3) Kosten + 100 Reserve + wehrbereit → Aufstieg.
        var ok = new FakeWorld { Resources = new ResourceVector(650, 500, 100, 200), PopulationCapacity = 30 };
        ArmWorld(ok);
        ai.Tick(ok, ok, 0.1f, new AiContext(1));
        Assert.Contains("ageup()", ok.Log);
    }

    [Fact]
    public void AgeUpBlockedWithoutBarracksAndArmy()
    {
        // Reiche Wirtschaft, aber kein Militär → der Aufstieg bleibt WARTEN.
        var rich = new FakeWorld { Resources = new ResourceVector(5000, 5000, 500, 500), PopulationCapacity = 30 };
        rich.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        ai.Tick(rich, rich, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", rich.Log);

        // Kaserne da, aber die Soll-Armee fehlt → weiterhin WARTEN.
        var onlyBarracks = new FakeWorld { Resources = new ResourceVector(5000, 5000, 500, 500), PopulationCapacity = 30 };
        onlyBarracks.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        onlyBarracks.AddBuilding(BuildingType.Barracks, 12, 12, complete: true);
        ai.Tick(onlyBarracks, onlyBarracks, 0.1f, new AiContext(1));
        Assert.DoesNotContain("ageup()", onlyBarracks.Log);
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

    // -----------------------------------------------------------------
    // Gebäudeplanung
    // -----------------------------------------------------------------
    [Fact]
    public void BuildsHouseWhenPopulationAtCap()
    {
        // Pop 5/5: ohne Haus kann niemand mehr ausgebildet werden.
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(100, 300, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddVillager(18, 22, UnitStateKind.Gathering, gathering: (30, 30, Resource.Food));
        int free = w.AddVillager(18, 21);   // freier Dorfbewohner

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(w.Log.Any(s => s.StartsWith("build(House,")), string.Join(" | ", w.Log));
        Assert.True(w.Log.Any(s => s.StartsWith($"assign({free}@")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void HousePullsWorkersFromGatheringWhenNoneFree()
    {
        // Der reale Deadlock: alle sammeln, Pop am Limit, niemand frei.
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(80, 250, 50, 200) };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        for (int i = 0; i < 3; i++)
            w.AddVillager(18 + i, 22, UnitStateKind.Gathering, gathering: (30, 30 + i, Resource.Wood));

        ai.Tick(w, w, 0.1f, new AiContext(1));

        // Das Haus muss trotzdem stehen — aus dem Sammelpool abgezogen
        Assert.True(w.Log.Any(s => s.StartsWith("build(House,")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void DoesNotBuildSecondHouseWhileOneIsUnderConstruction()
    {
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(80, 400, 50, 200) };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        var house = w.AddBuilding(BuildingType.House, 16, 20, complete: false);
        house.ConstructionProgress = 0.2f;
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        // Kein Haus darf mehr gebaut werden — es steht eines (im Bau)
        Assert.Empty(w.Log.Where(s => s.StartsWith("build(House,")));
    }

    [Fact]
    public void BuildsFarmWhenFoodLow()
    {
        var w = new FakeWorld { Resources = new ResourceVector(100, 300, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        int free = w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(w.Log.Any(s => s.StartsWith("build(Farm,")), string.Join(" | ", w.Log));
        Assert.True(w.Log.Any(s => s.StartsWith($"assign({free}@")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void DoesNotBuildFarmWhenFoodComfortable()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 300, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(!w.Log.Any(s => s.StartsWith("build(Farm,")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void BuildsLumberCampOnceWoodAtTwoHundred()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 210, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddSource(Resource.Wood, 23, 20);   // nahe Quelle — das Lager verankert hier
        w.AddVillager(18, 22);
        w.AddVillager(18, 21);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        var build = w.Log.Where(s => s.StartsWith("build(LumberCamp,"))
                         .Select(s => { var parts = s.Split(','); return (int.Parse(parts[1]), int.Parse(parts[2])); })
                         .FirstOrDefault();
        Assert.True(w.Log.Any(s => s.StartsWith("build(LumberCamp,")), string.Join(" | ", w.Log));
        // Direkt an die Quelle, nicht am TC (20,20).
        Assert.True(Math.Max(Math.Abs(build.Item1 - 23), Math.Abs(build.Item2 - 20)) <= 2,
                    $"Lager soll an der Quelle (23,20) stehen, nicht am TC — ist bei {build}");
    }

    [Fact]
    public void DoesNotBuildLumberCampWithoutSourceNearby()
    {
        // Ohne nahe Quelle ist das Lager wertlos — es darf nicht am TC stehen.
        var w = new FakeWorld { Resources = new ResourceVector(500, 150, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddSource(Resource.Wood, 60, 60);   // sehr weit weg (> 20)
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(!w.Log.Any(s => s.StartsWith("build(LumberCamp,")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void BuildsMillNearFoodSource()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 210, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddSource(Resource.Food, 22, 23);   // Naher Schafherde — Mühle verankert hier
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        var build = w.Log.Where(s => s.StartsWith("build(Mill,"))
                         .Select(s => { var parts = s.Split(','); return (int.Parse(parts[1]), int.Parse(parts[2])); })
                         .FirstOrDefault();
        Assert.True(w.Log.Any(s => s.StartsWith("build(Mill,")), string.Join(" | ", w.Log));
        Assert.True(Math.Max(Math.Abs(build.Item1 - 22), Math.Abs(build.Item2 - 23)) <= 2,
                    $"Mühle soll an der Nahrung (22,23) stehen — ist bei {build}");
    }

    [Fact]
    public void DoesNotBuildMillWithoutFoodSource()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 210, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddSource(Resource.Wood, 23, 20);   // nur Holz, keine Nahrung
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(!w.Log.Any(s => s.StartsWith("build(Mill,")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void DoesNotBuildLumberCampBelowTwoHundredWood()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 150, 50, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(!w.Log.Any(s => s.StartsWith("build(LumberCamp,")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void HouseHasPriorityOverFarmAndCamp()
    {
        // Alle drei Bedingungen gleichzeitig erfüllt: es muss das Haus zuerst
        // gebaut werden (einziger Baubefehl).
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(100, 300, 50, 200) };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddVillager(18, 22);

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(w.Log.Any(s => s.StartsWith("build(House,")), string.Join(" | ", w.Log));
        Assert.Empty(w.Log.Where(s => s.StartsWith("build(Farm,")));
        Assert.Empty(w.Log.Where(s => s.StartsWith("build(LumberCamp,")));
    }

    // -----------------------------------------------------------------
    // Verteidigung: untätige Dorfbewohner greifen sichtbare Feinde an
    // -----------------------------------------------------------------
    [Fact]
    public void IdleVillagerAttacksVisibleEnemyUnit()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        int v = w.AddVillager(30, 30);
        w.AddEnemy(new EnemyInfo(32, 30, IsBuilding: false, Health: 30));   // feindlicher DB

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(w.Log.Any(s => s.StartsWith($"attack({v},")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void IdleVillagerAttacksVisibleEnemyBuilding()
    {
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        int v = w.AddVillager(30, 30);
        w.AddEnemy(new EnemyInfo(34, 33, IsBuilding: true, Health: 400));   // feindliches Lager, nah

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.True(w.Log.Any(s => s.StartsWith($"attack({v},")), string.Join(" | ", w.Log));
    }

    [Fact]
    public void VillagerDoesNotAttackFarAwayEnemy()
    {
        // Über der Verteidigungsreichweite (8) bleibt der Arbeiter im Dorf —
        // er wird nicht aus der eigenen Basis gerissen.
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        int v = w.AddVillager(20, 20);
        w.AddEnemy(new EnemyInfo(55, 55, IsBuilding: false, Health: 30));   // 35 Kacheln

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Empty(w.Log.Where(s => s.StartsWith($"attack({v},")));
    }

    [Fact]
    public void WorkingVillagerNotPulledIntoCombat()
    {
        // Ein Dorfbewohner, der gerade sammelt, bleibt an seiner Quelle —
        // Verteidigung trifft nur untätige Einheiten.
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        int working = w.AddVillager(20, 20, s: UnitStateKind.Gathering,
                                    gathering: (23, 20, Resource.Wood));
        w.AddEnemy(new EnemyInfo(25, 25, IsBuilding: false, Health: 30));

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Empty(w.Log.Where(s => s.StartsWith($"attack({working},")));
    }

    [Fact]
    public void VillagersCounterAttackWhenSettlementThreatened()
    {
        // Der Dorf-Angriffs-Alarm: die eigene Siedlung hat grade einen
        // Treffer kassiert (WorldTime - LastDamageAt < 6 s) — und die KI
        // hat noch keine Soldaten. Trotzdem muss sie sofort
        // zurückschlagen: alle freien Dorfbewohner angreifen, ohne
        // Mindestgröße und Cooldown.
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20, LastDamageAt = 100d, WorldTime = 101d };
        w.AddBuilding(BuildingType.TownCenter, 32, 32, complete: true);
        int v1 = w.AddVillager(30, 30);
        int v2 = w.AddVillager(33, 33);
        w.AddEnemy(new EnemyInfo(38, 32, IsBuilding: false, Health: 100));   // 6 Kacheln, sichtbar

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Contains($"attack({v1},38,32)", w.Log);
        Assert.Contains($"attack({v2},38,32)", w.Log);
    }

    [Fact]
    public void NoCounterAttackWhenNoDamageTaken()
    {
        // Ein sichtbarer Feind in Sichtweite ist noch kein Angriff auf die
        // Siedlung — ohne einen Treffer (LastDamageAt = 0) bleibt die
        // Ökonomie ungestört, und die KI zieht ihre Arbeiter nicht weg.
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20, LastDamageAt = 0d, WorldTime = 10d };
        w.AddBuilding(BuildingType.TownCenter, 32, 32, complete: true);
        int v = w.AddVillager(35, 35);
        w.AddEnemy(new EnemyInfo(55, 55, IsBuilding: false, Health: 100)); // 20 Kacheln: auch DefenseRadius verfehlt

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Empty(w.Log.Where(s => s.StartsWith($"attack({v},")));
    }

    [Fact]
    public void NoCounterAttackWhenDamageIsOld()
    {
        // Ein Treffer, der älter als das 6-s-Fenster ist, zählt als
        // abgeklungen — die KI kehrt zur Ökonomie zurück und zerrt
        // nicht jede Einheit aus der Arbeit.
        var w = new FakeWorld { Resources = new ResourceVector(500, 500, 100, 200),
                                 PopulationCapacity = 20, LastDamageAt = 100d, WorldTime = 120d };
        w.AddBuilding(BuildingType.TownCenter, 32, 32, complete: true);   // TC-Zentrum (34,34)
        int v = w.AddVillager(35, 35);
        // Feind 10 Kacheln vom TC, aber > 8 vom Arbeiter: außerhalb der
        // lokalen Defense, innnerhalb der ThreatRadius — fällt also nur
        // auf die „unter Angriff"-Klausel an, und die ist hier abgelaufen.
        w.AddEnemy(new EnemyInfo(44, 34, IsBuilding: false, Health: 100));

        ai.Tick(w, w, 0.1f, new AiContext(1));

        Assert.Empty(w.Log.Where(s => s.StartsWith($"attack({v},")));
    }
}
