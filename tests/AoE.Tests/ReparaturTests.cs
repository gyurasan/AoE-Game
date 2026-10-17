using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Reparatur (C15): ein beschädigtes Gebäude wird wieder hergestellt, Kosten
/// und Dauer anteilig zum fehlenden Schadensvolumen — zu 50 % angeschlagen
/// kostet und dauert es die Hälfte. Arbeiten wie an einer Baustelle: der erste
/// Arbeiter voll, jeder weitere ein Drittel so schnell (AoE-II-Formel).
/// </summary>
public class ReparaturTests
{
    // Haus: 400 LP, 25 Holz, Bauzeit 25 s
    [Fact]
    public void HausZuHaelfteAngeschlagenKostetUndDauertDieHaelfte()
    {
        var cost = BuildingRules.RepairCostOf(BuildingType.House, 200, 400);
        Assert.Equal(13, cost[Resource.Wood]);   // 25 * 0.5 = 12.5, aufgerundet
        Assert.Single(cost);

        Assert.Equal(12.5f, BuildingRules.RepairSecondsOf(BuildingType.House, 200, 400), 3);
    }

    [Fact]
    public void MehrereRohstoffeKommenBeideAnteiligZurueck()
    {
        // Wachturm: 50 Holz + 125 Stein, 1020 LP — zu 50 %: 25 + 63
        var cost = BuildingRules.RepairCostOf(BuildingType.Tower, 510, 1020);
        Assert.Equal(25, cost[Resource.Wood]);
        Assert.Equal(63, cost[Resource.Stone]);
        Assert.Equal(2, cost.Count);
    }

    [Fact]
    public void UnverletztKostetUndDauertNichts()
    {
        Assert.Empty(BuildingRules.RepairCostOf(BuildingType.House, 400, 400));
        Assert.Equal(0f, BuildingRules.RepairSecondsOf(BuildingType.House, 400, 400), 3);
        // Über das Maximum hinweg (Forschung kann LP über das Basis-Maximum heben)
        Assert.Empty(BuildingRules.RepairCostOf(BuildingType.House, 440, 400));
    }

    [Fact]
    public void KleinstschadenKostetJeRohstoffMindestensEins()
    {
        // Mauer: 2 Holz, 250 LP — 1 LP fehlend ist kein halbes Holz
        var cost = BuildingRules.RepairCostOf(BuildingType.PalisadeWall, 249, 250);
        Assert.Equal(1, cost[Resource.Wood]);
    }

    [Fact]
    public void GanzeStandfestigkeitFehlendKostetDenVollenPreis()
    {
        // HP 0 (zerrissen, aber noch stehend) = der volle Baupreis
        var cost = BuildingRules.RepairCostOf(BuildingType.House, 0, 400);
        Assert.Equal(25, cost[Resource.Wood]);
        Assert.Equal(25f, BuildingRules.RepairSecondsOf(BuildingType.House, 0, 400), 3);
    }

    [Fact]
    public void EinArbeiterBrauchtGenausoLangWieDieAuftragDauer()
    {
        var repair = new Repair(12.5f, 200);
        Assert.Equal(0f, repair.Progress);
        Assert.False(repair.Update(12.4f, 1));
        Assert.Equal(0.992f, repair.Progress, 3);
        Assert.True(repair.Update(0.2f, 1));
        Assert.True(repair.IsComplete);
        // Danach bleibt sie abgeschlossen
        Assert.False(repair.Update(1f, 1));
    }

    [Fact]
    public void ZweiArbeiterSindDrittelSchneller()
    {
        // (2 + 2) / 3 = 4/3 schnell → 3/4 der Zeit
        var eins = new Repair(12f, 200);
        var zwei = new Repair(12f, 200);
        eins.Update(9f, 1);
        zwei.Update(9f, 2);
        Assert.True(zwei.Progress > eins.Progress);
        Assert.Equal(eins.Progress * 4f / 3f, zwei.Progress, 3);
    }

    [Fact]
    public void OhneArbeiterStehtDerAuftragStill()
    {
        var repair = new Repair(12f, 200);
        Assert.False(repair.Update(10f, 0));
        Assert.Equal(0f, repair.Progress);
    }

    [Fact]
    public void HpAtFuehrtVonStartZumMaximum()
    {
        // Start 200, Auftrag 200 LP: bei Fortschritt p = 200 + 200 * p
        var repair = new Repair(10f, 200);
        Assert.Equal(200, repair.HpAt(0f, 400));
        Assert.Equal(300, repair.HpAt(0.5f, 400));
        Assert.Equal(400, repair.HpAt(1f, 400));
    }

    [Fact]
    public void SpielstandGehtHinUndZurueck()
    {
        var repair = new Repair(12.5f, 200);
        repair.Update(3f, 1);
        var state = repair.ToState();
        var kopie = Repair.FromState(state);
        Assert.Equal(repair.Seconds, kopie.Seconds);
        Assert.Equal(repair.Missing, kopie.Missing);
        Assert.Equal(repair.Progress, kopie.Progress);
    }

    [Fact]
    public void AuftragOhneDauerOderOhneSchadenWirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new Repair(0f, 100));
        Assert.Throws<ArgumentOutOfRangeException>(() => new Repair(10f, 0));
    }
}
