using System.Collections.Generic;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Bevölkerungslimit und Ausbildung aus der Spezifikation, Kapitel
/// „Bevölkerung und Häuser": Stadtzentrum und Haus geben je fünf Plätze,
/// höchstens 200, und bei erreichter Grenze stoppt die Produktion.
/// </summary>
public class TrainingTests
{
    private const string Dorfbewohner = "Dorfbewohner";
    private const float Ausbildung = 25f;

    private static Dictionary<Resource, int> Nahrung(int menge) => new() { [Resource.Food] = menge };

    /// <summary>Warteschlange mit einem Dorfbewohner; das Startguthaben hat 200 Nahrung.</summary>
    private static (TrainingQueue<string> Queue, ResourcePool Pool) MitEinem()
    {
        var pool = new ResourcePool();
        var queue = new TrainingQueue<string>();
        Assert.True(queue.Enqueue(Dorfbewohner, Nahrung(25), Ausbildung, pool));
        return (queue, pool);
    }

    // --- Population ---------------------------------------------------------

    [Theory]
    [InlineData(BuildingType.TownCenter, 5)]
    [InlineData(BuildingType.House, 5)]
    [InlineData(BuildingType.Mill, 0)]
    [InlineData(BuildingType.Barracks, 0)]
    public void Gebaeude_StiftenPlaetze(BuildingType typ, int plaetze)
    {
        Assert.Equal(plaetze, Population.SlotsOf(typ));
    }

    [Fact]
    public void Grenze_IstDieSummeDerGebaeude()
    {
        var gebaeude = new[] { BuildingType.TownCenter, BuildingType.House, BuildingType.Mill, BuildingType.House };

        Assert.Equal(15, Population.Capacity(gebaeude));
    }

    [Fact]
    public void Grenze_OhneGebaeude_IstNull()
    {
        Assert.Equal(0, Population.Capacity(new BuildingType[0]));
    }

    [Fact]
    public void Grenze_HoechstensZweihundert()
    {
        var gebaeude = new List<BuildingType>();
        for (int i = 0; i < 50; i++)
            gebaeude.Add(BuildingType.House);

        Assert.Equal(Population.DEFAULT_LIMIT, Population.Capacity(gebaeude));
        Assert.Equal(25, Population.Capacity(gebaeude, limit: 25));
    }

    // --- Einreihen ----------------------------------------------------------

    [Fact]
    public void Einreihen_BezahltSofort()
    {
        var (queue, pool) = MitEinem();

        Assert.Equal(1, queue.Count);
        Assert.Equal(new[] { Dorfbewohner }, queue.Units);
        Assert.Equal(175, pool[Resource.Food]);
        Assert.Equal(0f, queue.Progress);
    }

    [Fact]
    public void Einreihen_OhneGuthaben_AendertNichts()
    {
        var pool = new ResourcePool();
        var queue = new TrainingQueue<string>();

        Assert.False(queue.Enqueue(Dorfbewohner, Nahrung(500), Ausbildung, pool));

        Assert.Equal(0, queue.Count);
        Assert.Equal(200, pool[Resource.Food]);
    }

    [Fact]
    public void Einreihen_VolleWarteschlange_AendertNichts()
    {
        var pool = new ResourcePool();
        pool.Add(Resource.Food, 1000);
        var queue = new TrainingQueue<string>();
        for (int i = 0; i < TrainingQueue<string>.MAX_LENGTH; i++)
            Assert.True(queue.Enqueue(Dorfbewohner, Nahrung(25), Ausbildung, pool));
        int vorher = pool[Resource.Food];

        Assert.False(queue.Enqueue(Dorfbewohner, Nahrung(25), Ausbildung, pool));

        Assert.Equal(TrainingQueue<string>.MAX_LENGTH, queue.Count);
        Assert.Equal(vorher, pool[Resource.Food]);
    }

    [Theory]
    [InlineData(0f)]
    [InlineData(-1f)]
    public void Einreihen_OhneAusbildungszeit_IstEinFehler(float sekunden)
    {
        var queue = new TrainingQueue<string>();

        Assert.Throws<ArgumentOutOfRangeException>(
            () => queue.Enqueue(Dorfbewohner, Nahrung(25), sekunden, new ResourcePool()));
    }

    // --- Ausbilden ----------------------------------------------------------

    [Fact]
    public void Ausbilden_DauertDieAusbildungszeit()
    {
        var (queue, _) = MitEinem();

        Assert.False(queue.Update(10f, 4, 5, out _));
        Assert.Equal(0.4f, queue.Progress, 3);

        Assert.True(queue.Update(15f, 4, 5, out var fertig));
        Assert.Equal(Dorfbewohner, fertig);
        Assert.Equal(0, queue.Count);
        Assert.Equal(0f, queue.Progress);
    }

    [Fact]
    public void Ausbilden_JeAufrufHoechstensEineEinheit()
    {
        var (queue, pool) = MitEinem();
        queue.Enqueue("Zweiter", Nahrung(25), Ausbildung, pool);

        Assert.True(queue.Update(100f, 4, 10, out var erster));
        Assert.Equal(Dorfbewohner, erster);
        Assert.Equal(1, queue.Count);
        Assert.Equal(0f, queue.Progress);   // überzählige Zeit verfällt

        Assert.True(queue.Update(Ausbildung, 5, 10, out var zweiter));
        Assert.Equal("Zweiter", zweiter);
    }

    [Fact]
    public void LeereWarteschlange_TutNichts()
    {
        var queue = new TrainingQueue<string>();

        Assert.False(queue.Update(10f, 5, 5, out _));
        Assert.False(queue.IsBlocked);
        Assert.Equal(0f, queue.Progress);
    }

    // --- Bevölkerungsgrenze -------------------------------------------------

    [Fact]
    public void VolleBevoelkerung_StopptDieAusbildung()
    {
        var (queue, _) = MitEinem();

        Assert.False(queue.Update(30f, 5, 5, out _));

        Assert.True(queue.IsBlocked);
        Assert.Equal(0f, queue.Progress);
        Assert.Equal(1, queue.Count);
    }

    [Fact]
    public void UeberDerGrenze_StopptEbenfalls()
    {
        // Etwa wenn ein Haus zerstört wurde: 7 Einheiten bei Grenze 5
        var (queue, _) = MitEinem();

        Assert.False(queue.Update(30f, 7, 5, out _));
        Assert.True(queue.IsBlocked);
    }

    [Fact]
    public void FreierPlatz_SetztDieAusbildungFort_OhneFortschrittZuVerlieren()
    {
        var (queue, _) = MitEinem();
        queue.Update(10f, 4, 5, out _);

        queue.Update(10f, 5, 5, out _);
        Assert.True(queue.IsBlocked);
        Assert.Equal(0.4f, queue.Progress, 3);

        Assert.True(queue.Update(15f, 4, 5, out _));
        Assert.False(queue.IsBlocked);
    }

    // --- Abbrechen ----------------------------------------------------------

    [Fact]
    public void Abbrechen_ErstattetDieHintersteUndLaesstDieVorderste()
    {
        var (queue, pool) = MitEinem();
        queue.Enqueue("Zweiter", Nahrung(30), Ausbildung, pool);
        queue.Update(10f, 4, 10, out _);
        Assert.Equal(145, pool[Resource.Food]);

        Assert.True(queue.CancelLast(pool));

        Assert.Equal(175, pool[Resource.Food]);
        Assert.Equal(new[] { Dorfbewohner }, queue.Units);
        Assert.Equal(0.4f, queue.Progress, 3);
    }

    [Fact]
    public void Abbrechen_DerEinzigen_SetztDenFortschrittZurueck()
    {
        var (queue, pool) = MitEinem();
        queue.Update(10f, 4, 5, out _);

        Assert.True(queue.CancelLast(pool));

        Assert.Equal(0, queue.Count);
        Assert.Equal(0f, queue.Progress);
        Assert.Equal(200, pool[Resource.Food]);
    }

    [Fact]
    public void Abbrechen_ErstattetDieGespeichertenKosten()
    {
        var pool = new ResourcePool();
        var queue = new TrainingQueue<string>();
        var kosten = Nahrung(25);
        queue.Enqueue(Dorfbewohner, kosten, Ausbildung, pool);
        kosten[Resource.Food] = 999;   // der Aufrufer ändert sein Wörterbuch

        queue.CancelLast(pool);

        Assert.Equal(200, pool[Resource.Food]);
    }

    [Fact]
    public void Abbrechen_LeereWarteschlange_GibtFalse()
    {
        Assert.False(new TrainingQueue<string>().CancelLast(new ResourcePool()));
    }
}
