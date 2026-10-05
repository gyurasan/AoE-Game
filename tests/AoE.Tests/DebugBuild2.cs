using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;
using Xunit.Abstractions;
namespace AoE.Tests;
public class DebugBuild2
{
    ITestOutputHelper Output { get; }
    public DebugBuild2(ITestOutputHelper o) => Output = o;
    [Fact]
    public void Trace2()
    {
        // EXAKTE Kopie des EconomyAiTests.BuildsHouseWhenPopulationAtCap-Setsups
        var w = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                 Resources = new ResourceVector(100, 300, 100, 200) };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddVillager(18, 22, UnitStateKind.Gathering, gathering: (30, 30, Resource.Food));
        int free = w.AddVillager(18, 21);

        var ai = new EconomyAi();   // frisches Objekt, wie der Test über die statische instanz
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Output.WriteLine("L1: " + string.Join(" | ", w.Log));

        // und mit der statischen Instanz des EconomyAiTests-Blocks:
        var w2 = new FakeWorld { PopulationCount = 5, PopulationCapacity = 5,
                                  Resources = new ResourceVector(100, 300, 100, 200) };
        w2.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w2.AddVillager(18, 22, UnitStateKind.Gathering, gathering: (30, 30, Resource.Food));
        w2.AddVillager(18, 21);
        var ai2 = new EconomyAi();
        ai2.Tick(w2, w2, 0.1f, new AiContext(2));
        Output.WriteLine("L2: " + string.Join(" | ", w2.Log));
    }
}
