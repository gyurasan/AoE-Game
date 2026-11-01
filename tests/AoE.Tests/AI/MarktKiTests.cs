using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Die KI am Markt (F8): ab der Feudalzeit baut sie einen, und sie handelt, wenn ihr eine
/// Ressource ausgeht - Überschuss gegen Gold, oder Nahrung und Holz mit übrigem Gold.
/// </summary>
public class MarktKiTests
{
    readonly EconomyAi ai = new();

    /// <summary>Ein Dorf in der Feudalzeit mit Kaserne, Schießstand und genug Platz im Haus.</summary>
    private static FakeWorld Dorf(ResourceVector vorrat, bool markt = true, bool marktFertig = true,
                                  Age zeitalter = Age.Feudal)
    {
        var w = new FakeWorld { Resources = vorrat, Age = zeitalter, PopulationCapacity = 40, PopulationCount = 10 };
        w.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        w.AddBuilding(BuildingType.Barracks, 26, 20, complete: true);
        w.AddBuilding(BuildingType.ArcheryRange, 26, 26, complete: true);
        if (markt)
            w.AddBuilding(BuildingType.Market, 14, 26, complete: marktFertig);
        for (int i = 0; i < 3; i++)
            w.AddVillager(18 + i, 24);
        return w;
    }

    private static List<string> Handel(FakeWorld w)
        => w.Log.Where(l => l.StartsWith("buy(") || l.StartsWith("sell(")).ToList();

    [Fact]
    public void BautAbDerFeudalzeitNachDemSchiessstandEinenMarkt()
    {
        var w = Dorf(new ResourceVector(2000, 500, 300, 0), markt: false);
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Contains(w.Log, l => l.StartsWith("build(Market,"));
    }

    [Fact]
    public void BautKeinenMarktInDerDunkelzeitOhneSchiessstandOderMitMarkt()
    {
        var dunkel = Dorf(new ResourceVector(2000, 500, 300, 0), markt: false, zeitalter: Age.Dark);
        ai.Tick(dunkel, dunkel, 0.1f, new AiContext(1));
        Assert.DoesNotContain(dunkel.Log, l => l.StartsWith("build(Market,"));

        var ohneSchiessstand = new FakeWorld { Resources = new ResourceVector(2000, 500, 300, 0), Age = Age.Feudal,
                                               PopulationCapacity = 40, PopulationCount = 10 };
        ohneSchiessstand.AddBuilding(BuildingType.TownCenter, 20, 20, complete: true);
        ohneSchiessstand.AddBuilding(BuildingType.Barracks, 26, 20, complete: true);
        ohneSchiessstand.AddVillager(18, 24);
        new EconomyAi().Tick(ohneSchiessstand, ohneSchiessstand, 0.1f, new AiContext(1));
        Assert.DoesNotContain(ohneSchiessstand.Log, l => l.StartsWith("build(Market,"));

        var imBau = Dorf(new ResourceVector(2000, 500, 300, 0), marktFertig: false);
        new EconomyAi().Tick(imBau, imBau, 0.1f, new AiContext(1));
        Assert.DoesNotContain(imBau.Log, l => l.StartsWith("build(Market,"));
    }

    [Fact]
    public void GehtDasGoldAusVerkauftSieDenGroesstenUeberschuss()
    {
        // Gold 50 unter 200 (Bedarf der Ritterzeit); Stein 800 ist der größte Überschuss -
        // Nahrung bleibt für die Ritterzeit (800 + 300), Holz hat nichts übrig
        var w = Dorf(new ResourceVector(1000, 350, 50, 800));
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Equal(new[] { "sell(Stone)" }, Handel(w));
    }

    [Fact]
    public void OhneUeberschussVerkauftSieNichts()
    {
        // Nahrung 1100 = Bedarf 800 + 300: nach dem Verkauf wären es zu wenig
        var w = Dorf(new ResourceVector(1100, 399, 0, 399));
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Empty(Handel(w));
    }

    [Fact]
    public void GehtHolzAusKauftSieMitUebrigemGold()
    {
        // Gold 600: nach dem Kauf (130) bleiben 470, mehr als 200 Bedarf + 200 Rückhalt
        var w = Dorf(new ResourceVector(2000, 40, 600, 0));
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Equal(new[] { "buy(Wood)" }, Handel(w));
    }

    [Fact]
    public void KauftNichtWennDasGoldFuerDenAufstiegFehlte()
    {
        // 520 - 130 = 390 < 400: das Gold fürs nächste Zeitalter geht vor
        var w = Dorf(new ResourceVector(2000, 40, 520, 0));
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Empty(Handel(w));

        // Teurer Kurs: auch mit 600 Gold nicht, wenn das Los 250 kostet
        var teuer = Dorf(new ResourceVector(2000, 40, 600, 0));
        teuer.BuyCosts[Resource.Wood] = 250;
        new EconomyAi().Tick(teuer, teuer, 0.1f, new AiContext(1));
        Assert.Empty(Handel(teuer));
    }

    [Fact]
    public void SteinKauftSieNie()
    {
        var w = Dorf(new ResourceVector(2000, 2000, 5000, 0));
        ai.Tick(w, w, 0.1f, new AiContext(1));
        Assert.Empty(Handel(w));
    }

    [Fact]
    public void OhneFertigenMarktHandeltSieNicht()
    {
        var ohne = Dorf(new ResourceVector(1000, 350, 50, 800), markt: false);
        ai.Tick(ohne, ohne, 0.1f, new AiContext(1));
        Assert.Empty(Handel(ohne));

        var imBau = Dorf(new ResourceVector(1000, 350, 50, 800), marktFertig: false);
        new EconomyAi().Tick(imBau, imBau, 0.1f, new AiContext(1));
        Assert.Empty(Handel(imBau));
    }

    [Fact]
    public void HoechstensEinHandelJeAbstand()
    {
        var w = Dorf(new ResourceVector(1000, 350, 50, 2000));
        var ctx = new AiContext(1);
        ai.Tick(w, w, 0.1f, ctx);
        Assert.Single(Handel(w));

        // Zwei Sekunden später: noch zu früh
        for (int i = 0; i < 20; i++)
            ai.Tick(w, w, 0.1f, ctx);
        Assert.Single(Handel(w));

        // Nach dem Abstand das nächste Los
        ai.Tick(w, w, (float)EconomyAi.TradeIntervalSeconds, ctx);
        Assert.Equal(2, Handel(w).Count);
    }
}
