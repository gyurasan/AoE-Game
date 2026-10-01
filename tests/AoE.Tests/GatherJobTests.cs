using System.Collections.Generic;
using System.Linq;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Der Dorfbewohner-Kreislauf aus der Spezifikation, Kapitel „Der
/// Dorfbewohner-Loop": sammeln bis Traglast 10, zur nächsten Abgabestelle,
/// abliefern, selbstständig zurück — und bei erschöpfter Quelle selbst die
/// nächste gleiche suchen. Der Spieler klickt nur einmal.
/// </summary>
public class GatherJobTests
{
    /// <summary>Kleine Testwelt: Quellen im Wörterbuch, eine Abgabestelle für Spieler 0.</summary>
    private sealed class Welt : IGatherWorld
    {
        public readonly Dictionary<Position, (Resource Art, int Menge)> Quellen = new();
        public Position? Abgabe = new Position(0, 0);

        public int AmountAt(Position cell, Resource resource)
            => Quellen.TryGetValue(cell, out var q) && q.Art == resource ? q.Menge : 0;

        public int Harvest(Position cell, Resource resource, int amount)
        {
            int vorhanden = AmountAt(cell, resource);
            int entnommen = System.Math.Min(vorhanden, amount);
            if (entnommen > 0)
                Quellen[cell] = (resource, vorhanden - entnommen);
            return entnommen;
        }

        public Position? FindNearestSource(Position from, Resource resource, int maxDistance)
        {
            var treffer = Quellen
                .Where(q => q.Value.Art == resource && q.Value.Menge > 0
                            && q.Key.Distance(from) <= maxDistance)
                .OrderBy(q => q.Key.Distance(from))
                .ToList();
            return treffer.Count > 0 ? treffer[0].Key : null;
        }

        public Position? FindNearestDropOff(Position from, int ownerId, Resource resource)
            => ownerId == 0 ? Abgabe : null;
    }

    private static readonly Position Wald = new(10, 10);

    /// <summary>Ruft Update in 0,5-s-Schritten auf, wie ein Spiel mit niedriger Bildrate.</summary>
    private static void Sammle(GatherJob job, Welt welt, float sekunden)
    {
        for (float t = 0; t < sekunden - 0.001f; t += 0.5f)
            job.Update(0.5f, welt);
    }

    private static (GatherJob Job, Welt Welt) AmWald(int holz = 100)
    {
        var welt = new Welt();
        welt.Quellen[Wald] = (Resource.Wood, holz);
        var job = new GatherJob(0, Resource.Wood, Wald);
        job.Arrive(welt);
        return (job, welt);
    }

    [Fact]
    public void NeuerAuftrag_LaeuftZurQuelle()
    {
        var job = new GatherJob(0, Resource.Wood, Wald);

        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(Wald, job.Destination);
        Assert.Equal(0, job.Carrying);
    }

    [Fact]
    public void AnkunftAnDerQuelle_StartetDasSammeln()
    {
        var (job, _) = AmWald();

        Assert.Equal(GatherPhase.Gathering, job.Phase);
        Assert.Null(job.Destination);
    }

    [Fact]
    public void Sammelraten_FolgenDerSpezifikation()
    {
        Assert.Equal(0.33f, GatherJob.RateOf(Resource.Food), 3);
        Assert.Equal(0.39f, GatherJob.RateOf(Resource.Wood), 3);
        Assert.Equal(0.38f, GatherJob.RateOf(Resource.Gold), 3);
        Assert.Equal(0.36f, GatherJob.RateOf(Resource.Stone), 3);
        Assert.Equal(0f, GatherJob.RateOf(Resource.Population));
    }

    [Fact]
    public void Holz_NachZwanzigSekunden_SiebenEinheiten()
    {
        var (job, welt) = AmWald();

        Sammle(job, welt, 20f); // 20 s × 0,39 = 7,8 → 7 ganze Einheiten

        Assert.Equal(7, job.Carrying);
        Assert.Equal(GatherPhase.Gathering, job.Phase);
        Assert.Equal(93, welt.AmountAt(Wald, Resource.Wood));
    }

    [Fact]
    public void VolleTraglast_GehtZurAbgabestelle()
    {
        var (job, welt) = AmWald();

        Sammle(job, welt, 25f);
        Assert.Equal(9, job.Carrying); // 9,75
        Assert.Equal(GatherPhase.Gathering, job.Phase);

        Sammle(job, welt, 1f);
        Assert.Equal(GatherJob.CARRY_CAPACITY, job.Carrying);
        Assert.Equal(GatherPhase.ToDropOff, job.Phase);
        Assert.Equal(welt.Abgabe, job.Destination);
        Assert.Equal(90, welt.AmountAt(Wald, Resource.Wood));
    }

    [Fact]
    public void GrosserZeitschritt_SammeltNieUeberDieTraglast()
    {
        var (job, welt) = AmWald();

        job.Update(100f, welt);

        Assert.Equal(GatherJob.CARRY_CAPACITY, job.Carrying);
        Assert.Equal(GatherPhase.ToDropOff, job.Phase);
        Assert.Equal(90, welt.AmountAt(Wald, Resource.Wood));
    }

    [Fact]
    public void Abliefern_GibtDieTraglastZurueck_UndKehrtZurSelbenQuelleZurueck()
    {
        var (job, welt) = AmWald();
        job.Update(100f, welt);

        int abgeliefert = job.Arrive(welt);

        Assert.Equal(GatherJob.CARRY_CAPACITY, abgeliefert);
        Assert.Equal(0, job.Carrying);
        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(Wald, job.Destination);
    }

    [Fact]
    public void ErschoepfteQuelle_NaechsteGleicheWirdAngesteuert_TraglastBleibt()
    {
        var (job, welt) = AmWald(holz: 3);
        var gold = new Position(11, 10);   // näher, aber falsche Ressource
        var nah = new Position(12, 10);    // nächste Holzquelle
        var fern = new Position(15, 10);
        welt.Quellen[gold] = (Resource.Gold, 50);
        welt.Quellen[nah] = (Resource.Wood, 50);
        welt.Quellen[fern] = (Resource.Wood, 50);

        Sammle(job, welt, 15f);

        Assert.Equal(3, job.Carrying);
        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(nah, job.Source);
        Assert.Equal(nah, job.Destination);
    }

    [Fact]
    public void QuelleErschoepft_KeineWeitere_ErstAbliefernDannUntaetig()
    {
        var (job, welt) = AmWald(holz: 4);

        Sammle(job, welt, 15f);
        Assert.Equal(4, job.Carrying);
        Assert.Equal(GatherPhase.ToDropOff, job.Phase);

        Assert.Equal(4, job.Arrive(welt));
        Assert.Equal(GatherPhase.Done, job.Phase);
        Assert.Null(job.Destination);
    }

    [Fact]
    public void QuelleBeimAbliefernLeer_SuchtDieNaechsteGleiche()
    {
        var (job, welt) = AmWald(holz: 10);
        var nah = new Position(12, 10);
        welt.Quellen[nah] = (Resource.Wood, 50);

        job.Update(100f, welt);   // die zehn Einheiten leeren die Quelle
        Assert.Equal(GatherPhase.ToDropOff, job.Phase);

        Assert.Equal(10, job.Arrive(welt));
        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(nah, job.Source);
    }

    [Fact]
    public void QuelleBeiAnkunftSchonLeer_SuchtDieNaechsteGleiche()
    {
        var welt = new Welt();
        var nah = new Position(12, 10);
        welt.Quellen[Wald] = (Resource.Wood, 0);   // ein anderer war schneller
        welt.Quellen[nah] = (Resource.Wood, 50);
        var job = new GatherJob(0, Resource.Wood, Wald);

        Assert.Equal(0, job.Arrive(welt));

        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(nah, job.Destination);
    }

    [Fact]
    public void QuelleAusserhalbDesSuchradius_WirdNichtAngesteuert()
    {
        var (job, welt) = AmWald(holz: 2);
        welt.Quellen[new Position(10 + GatherJob.SEARCH_RADIUS + 1, 10)] = (Resource.Wood, 50);

        Sammle(job, welt, 10f);

        Assert.Equal(GatherPhase.ToDropOff, job.Phase);
        Assert.Equal(2, job.Carrying);
    }

    [Fact]
    public void OhneAbgabestelle_EndetDerAuftrag_TraglastBleibt()
    {
        var (job, welt) = AmWald();
        welt.Abgabe = null;

        job.Update(100f, welt);

        Assert.Equal(GatherPhase.Done, job.Phase);
        Assert.Equal(GatherJob.CARRY_CAPACITY, job.Carrying);
        Assert.Null(job.Destination);
    }

    [Fact]
    public void UnerreichbaresZiel_BeendetDenAuftrag()
    {
        var job = new GatherJob(0, Resource.Wood, Wald);

        job.Unreachable();

        Assert.Equal(GatherPhase.Done, job.Phase);
        Assert.Null(job.Destination);
    }

    [Fact]
    public void UpdateAusserhalbDesSammelns_TutNichts()
    {
        var welt = new Welt();
        welt.Quellen[Wald] = (Resource.Wood, 100);
        var job = new GatherJob(0, Resource.Wood, Wald);   // noch unterwegs

        job.Update(100f, welt);

        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(0, job.Carrying);
        Assert.Equal(100, welt.AmountAt(Wald, Resource.Wood));
    }

    // C1t: Schickt man einen beladenen Dorfbewohner an eine andere Quelle
    // derselben Ressource, behält er seine Traglast — wie in AoE.

    [Fact]
    public void NeuerAuftragMitTraglast_BehaeltSie()
    {
        var job = new GatherJob(0, Resource.Wood, Wald, carrying: 7);

        Assert.Equal(GatherPhase.ToSource, job.Phase);
        Assert.Equal(7, job.Carrying);
    }

    [Theory]
    [InlineData(-3, 0)]
    [InlineData(0, 0)]
    [InlineData(10, 10)]
    [InlineData(15, 10)]
    public void MitgebrachteTraglast_LiegtZwischenNullUndTraglastgrenze(int mitgebracht, int erwartet)
    {
        var job = new GatherJob(0, Resource.Wood, Wald, carrying: mitgebracht);

        Assert.Equal(erwartet, job.Carrying);
    }

    [Fact]
    public void MitgebrachteTraglast_WirdAufgefuelltUndGanzAbgeliefert()
    {
        var welt = new Welt();
        welt.Quellen[Wald] = (Resource.Wood, 100);
        var job = new GatherJob(0, Resource.Wood, Wald, carrying: 7);
        job.Arrive(welt);

        // 3 Holz fehlen bis zur Traglast 10: bei 0,39/s reichen 8 s
        Sammle(job, welt, 8f);

        Assert.Equal(GatherPhase.ToDropOff, job.Phase);
        Assert.Equal(97, welt.AmountAt(Wald, Resource.Wood));
        Assert.Equal(10, job.Arrive(welt));
    }
}
