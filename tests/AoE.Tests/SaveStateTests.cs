using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Spielstand (SV1): jede Kernklasse mit eigenem Zustand gibt ihn als Stand heraus
/// (ToState) und übernimmt ihn wieder (FromState/Restore). Maßstab ist immer: nach dem
/// Laden läuft alles genau so weiter wie ohne Speichern.
/// </summary>
public class SaveStateTests
{
    /// <summary>Kleine Testwelt: Quellen im Wörterbuch, eine Abgabestelle für Spieler 0.</summary>
    private sealed class Welt : IGatherWorld
    {
        public readonly Dictionary<Position, (Resource Art, int Menge)> Quellen = new();

        public Welt Kopie()
        {
            var kopie = new Welt();
            foreach (var (k, v) in Quellen)
                kopie.Quellen[k] = v;
            return kopie;
        }

        public int AmountAt(Position cell, Resource resource)
            => Quellen.TryGetValue(cell, out var q) && q.Art == resource ? q.Menge : 0;

        public int Harvest(Position cell, Resource resource, int amount)
        {
            int vorhanden = AmountAt(cell, resource);
            int entnommen = Math.Min(vorhanden, amount);
            if (entnommen > 0)
                Quellen[cell] = (resource, vorhanden - entnommen);
            return entnommen;
        }

        public Position? FindNearestSource(Position from, Resource resource, int maxDistance)
            => Quellen.Where(q => q.Value.Art == resource && q.Value.Menge > 0 && q.Key.Distance(from) <= maxDistance)
                      .OrderBy(q => q.Key.Distance(from))
                      .Select(q => (Position?)q.Key)
                      .FirstOrDefault();

        public Position? FindNearestDropOff(Position from, int ownerId, Resource resource)
            => ownerId == 0 ? new Position(0, 0) : null;
    }

    private static readonly Position Wald = new(10, 10);

    private static Dictionary<Resource, int> Nahrung(int menge) => new() { [Resource.Food] = menge };

    // --- GatherJob -----------------------------------------------------------

    /// <summary>Ein Holzfäller, der schon eine Weile sammelt: Traglast und Bruchteil sind krumm.</summary>
    private static (GatherJob Job, Welt Welt) MittenImSammeln()
    {
        var welt = new Welt();
        welt.Quellen[Wald] = (Resource.Wood, 100);
        var job = new GatherJob(0, Resource.Wood, Wald);
        job.Arrive(welt);
        for (int i = 0; i < 13; i++)
            job.Update(0.5f, welt);   // 13 · 0,5 s · 0,39 = 2,535 → Traglast 2, Bruchteil 0,535
        return (job, welt);
    }

    [Fact]
    public void GatherJob_ToState_HaeltAllesFest()
    {
        var (job, _) = MittenImSammeln();

        var stand = job.ToState();

        Assert.Equal(0, stand.OwnerId);
        Assert.Equal(Resource.Wood, stand.Resource);
        Assert.Equal(Wald, stand.Source);
        Assert.Null(stand.DropOff);
        Assert.Equal(GatherPhase.Gathering, stand.Phase);
        Assert.Equal(2, stand.Carrying);
        Assert.InRange(stand.Progress, 0.5f, 0.57f);
    }

    [Fact]
    public void GatherJob_FromState_SammeltGenauWeiter()
    {
        var (original, welt) = MittenImSammeln();
        var zweiteWelt = welt.Kopie();
        var geladen = GatherJob.FromState(original.ToState());

        // Bild für Bild dasselbe - auch der angesammelte Bruchteil ist mitgekommen
        for (int i = 0; i < 40; i++)
        {
            original.Update(0.5f, welt);
            geladen.Update(0.5f, zweiteWelt);
            Assert.Equal(original.Carrying, geladen.Carrying);
            Assert.Equal(original.Phase, geladen.Phase);
            Assert.Equal(original.Destination, geladen.Destination);
        }
    }

    [Fact]
    public void GatherJob_FromState_AufDemWegZurAbgabe()
    {
        var stand = new GatherJobState(0, Resource.Gold, Wald, new Position(3, 4), GatherPhase.ToDropOff, 10, 0f);

        var job = GatherJob.FromState(stand);

        Assert.Equal(GatherPhase.ToDropOff, job.Phase);
        Assert.Equal(new Position(3, 4), job.DropOff);
        Assert.Equal(new Position(3, 4), job.Destination);
        Assert.Equal(Resource.Gold, job.Resource);
        Assert.Equal(10, job.Carrying);
        Assert.Equal(stand, job.ToState());
    }

    [Fact]
    public void GatherJob_FromState_BegrenztDieTraglast()
    {
        var zuViel = GatherJob.FromState(new GatherJobState(0, Resource.Wood, Wald, null, GatherPhase.Gathering, 15, 0f));
        var negativ = GatherJob.FromState(new GatherJobState(0, Resource.Wood, Wald, null, GatherPhase.Gathering, -3, 0f));

        Assert.Equal(GatherJob.CARRY_CAPACITY, zuViel.Carrying);
        Assert.Equal(0, negativ.Carrying);
    }

    [Fact]
    public void GatherJob_FromState_BegrenztDenBruchteil()
    {
        var welt = new Welt();
        welt.Quellen[Wald] = (Resource.Wood, 100);

        // Ein Bruchteil ab 1 wäre schon eine ganze Einheit: Update(0) dürfte nichts ernten
        var zuGross = GatherJob.FromState(new GatherJobState(0, Resource.Wood, Wald, null, GatherPhase.Gathering, 0, 5f));
        zuGross.Update(0f, welt);
        Assert.Equal(0, zuGross.Carrying);
        Assert.InRange(zuGross.ToState().Progress, 0.99f, 0.9999f);

        var negativ = GatherJob.FromState(new GatherJobState(0, Resource.Wood, Wald, null, GatherPhase.Gathering, 0, -2f));
        Assert.Equal(0f, negativ.ToState().Progress);
    }

    [Fact]
    public void GatherJob_FromState_Null_Wirft()
    {
        Assert.Throws<ArgumentNullException>(() => GatherJob.FromState(null!));
    }

    // --- TrainingQueue -------------------------------------------------------

    /// <summary>Zwei Dorfbewohner eingereiht (je 25 Nahrung, 25 s), der erste 10 s ausgebildet.</summary>
    private static (TrainingQueue<string> Queue, ResourcePool Pool) ZweiNachZehnSekunden()
    {
        var pool = new ResourcePool();
        var queue = new TrainingQueue<string>();
        Assert.True(queue.Enqueue("a", Nahrung(25), 25f, pool));
        Assert.True(queue.Enqueue("b", Nahrung(30), 20f, pool));
        queue.Update(10f, 0, 10, out _);
        return (queue, pool);
    }

    [Fact]
    public void Training_ToState_KopiertEintraegeUndZeit()
    {
        var (queue, _) = ZweiNachZehnSekunden();

        var stand = queue.ToState();

        Assert.Equal(new[] { "a", "b" }, stand.Entries.Select(e => e.Unit));
        Assert.Equal(25, stand.Entries[0].Cost[Resource.Food]);
        Assert.Equal(30, stand.Entries[1].Cost[Resource.Food]);
        Assert.Equal(20f, stand.Entries[1].Seconds);
        Assert.Equal(10f, stand.Elapsed);

        // Eine Kopie: Änderungen am Stand ändern die Warteschlange nicht
        stand.Entries[1].Cost[Resource.Food] = 999;
        var pool = new ResourcePool(0, 0, 0, 0);
        queue.CancelLast(pool);
        Assert.Equal(30, pool[Resource.Food]);
    }

    [Fact]
    public void Training_Restore_SetztDenFortschrittFort()
    {
        var (original, _) = ZweiNachZehnSekunden();
        var geladen = new TrainingQueue<string>();

        geladen.Restore(original.ToState());

        Assert.Equal(2, geladen.Count);
        Assert.Equal(original.Progress, geladen.Progress);
        Assert.False(geladen.Update(14.9f, 0, 10, out _));
        Assert.True(geladen.Update(0.2f, 0, 10, out var fertig));
        Assert.Equal("a", fertig);
        Assert.Equal(new[] { "b" }, geladen.Units);
    }

    [Fact]
    public void Training_Restore_KopiertDieKosten()
    {
        var (original, _) = ZweiNachZehnSekunden();
        var stand = original.ToState();
        var geladen = new TrainingQueue<string>();
        geladen.Restore(stand);

        stand.Entries[1].Cost[Resource.Food] = 999;
        var pool = new ResourcePool(0, 0, 0, 0);
        geladen.CancelLast(pool);

        Assert.Equal(30, pool[Resource.Food]);
    }

    [Fact]
    public void Training_Restore_VerwirftAltesOhneErstattung()
    {
        var (queue, pool) = ZweiNachZehnSekunden();
        int nahrungVorher = pool[Resource.Food];

        queue.Restore(new TrainingQueueState<string>(new List<TrainingEntry<string>>(), 7f));

        Assert.Equal(0, queue.Count);
        Assert.Equal(0f, queue.Progress);
        Assert.Equal(nahrungVorher, pool[Resource.Food]);

        // Die verworfene Zeit wirkt nicht nach: ein neuer Eintrag beginnt bei 0
        queue.Enqueue("c", Nahrung(25), 25f, pool);
        Assert.Equal(0f, queue.Progress);
    }

    [Fact]
    public void Training_Restore_SetztBlockiertZurueck()
    {
        var pool = new ResourcePool();
        var queue = new TrainingQueue<string>();
        queue.Enqueue("a", Nahrung(25), 25f, pool);
        queue.Update(1f, 5, 5, out _);
        Assert.True(queue.IsBlocked);

        queue.Restore(queue.ToState());

        Assert.False(queue.IsBlocked);
    }

    [Fact]
    public void Training_Restore_NegativeZeit_WirdNull()
    {
        var queue = new TrainingQueue<string>();
        var eintrag = new TrainingEntry<string>("a", Nahrung(25), 25f);

        queue.Restore(new TrainingQueueState<string>(new List<TrainingEntry<string>> { eintrag }, -4f));

        Assert.Equal(0f, queue.Progress);
    }

    [Fact]
    public void Training_Restore_ZuLang_WirftUndAendertNichts()
    {
        var (queue, _) = ZweiNachZehnSekunden();
        var zuViele = Enumerable.Range(0, TrainingQueue<string>.MAX_LENGTH + 1)
            .Select(i => new TrainingEntry<string>("x" + i, Nahrung(1), 5f)).ToList();

        Assert.Throws<ArgumentException>(() => queue.Restore(new TrainingQueueState<string>(zuViele, 0f)));

        Assert.Equal(new[] { "a", "b" }, queue.Units);
        Assert.Equal(10f / 25f, queue.Progress, 3);
    }

    [Fact]
    public void Training_Restore_AusbildungszeitNull_WirftUndAendertNichts()
    {
        var (queue, _) = ZweiNachZehnSekunden();
        var eintraege = new List<TrainingEntry<string>>
        {
            new("x", Nahrung(1), 5f),
            new("y", Nahrung(1), 0f),
        };

        Assert.Throws<ArgumentOutOfRangeException>(() => queue.Restore(new TrainingQueueState<string>(eintraege, 0f)));

        Assert.Equal(new[] { "a", "b" }, queue.Units);
    }

    [Fact]
    public void Training_Restore_Null_Wirft()
    {
        Assert.Throws<ArgumentNullException>(() => new TrainingQueue<string>().Restore(null!));
    }

    // --- ResearchSlot und TechProgress -------------------------------------------

    [Fact]
    public void Forschung_ToStateUndRestore_ForschtGenauWeiter()
    {
        var pool = new ResourcePool();
        var techs = new TechProgress();
        var slot = new ResearchSlot();
        Assert.True(slot.TryStart(Tech.Loom, Age.Dark, techs, pool));
        slot.Update(10f, techs, out _);

        var slotStand = slot.ToState();
        var techStand = techs.ToState();
        Assert.Equal(Tech.Loom, slotStand.Current);
        Assert.Equal(10f, slotStand.Elapsed);

        var geladenTechs = new TechProgress();
        geladenTechs.Restore(techStand);
        var geladen = new ResearchSlot();
        geladen.Restore(slotStand);

        Assert.True(geladen.IsBusy);
        Assert.Equal(slot.Progress, geladen.Progress);
        Assert.True(geladenTechs.IsPending(Tech.Loom));
        Assert.False(geladen.Update(14.9f, geladenTechs, out _));
        Assert.True(geladen.Update(0.2f, geladenTechs, out var fertig));
        Assert.Equal(Tech.Loom, fertig);
        Assert.True(geladenTechs.IsResearched(Tech.Loom));
    }

    [Fact]
    public void Forschung_Restore_BuchtNichtsUndMeldetNichts()
    {
        var techs = new TechProgress();
        var slot = new ResearchSlot();

        slot.Restore(new ResearchSlotState(Tech.Loom, 5f));

        Assert.False(techs.IsPending(Tech.Loom));
        Assert.Equal(0, techs.Version);
    }

    [Fact]
    public void Forschung_Restore_OhneForschung_IstLeer()
    {
        var slot = new ResearchSlot();

        slot.Restore(new ResearchSlotState(null, 12f));

        Assert.False(slot.IsBusy);
        Assert.Equal(0f, slot.Progress);
        Assert.Equal(new ResearchSlotState(null, 0f), slot.ToState());
    }

    [Fact]
    public void Forschung_Restore_NegativeZeit_WirdNull()
    {
        var slot = new ResearchSlot();

        slot.Restore(new ResearchSlotState(Tech.Loom, -3f));

        Assert.Equal(0f, slot.Progress);
    }

    [Fact]
    public void Forschung_Restore_Null_Wirft()
    {
        Assert.Throws<ArgumentNullException>(() => new ResearchSlot().Restore(null!));
        Assert.Throws<ArgumentNullException>(() => new TechProgress().Restore(null!));
    }

    [Fact]
    public void TechStand_ToState_SortiertNachAufzaehlung()
    {
        var techs = new TechProgress();
        techs.Begin(Tech.Fletching);
        techs.Complete(Tech.Fletching);
        techs.Begin(Tech.Loom);
        techs.Complete(Tech.Loom);
        techs.Begin(Tech.Masonry);
        techs.Begin(Tech.HorseCollar);

        var stand = techs.ToState();

        Assert.Equal(new[] { Tech.Loom, Tech.Fletching }, stand.Researched);
        Assert.Equal(new[] { Tech.HorseCollar, Tech.Masonry }, stand.Pending);
        Assert.Equal(techs.Version, stand.Version);
    }

    [Fact]
    public void TechStand_Restore_ErsetztAllesUndUebernimmtVersion()
    {
        var techs = new TechProgress();
        techs.Begin(Tech.Forging);

        techs.Restore(new TechProgressState(new List<Tech> { Tech.Loom, Tech.Loom }, new List<Tech> { Tech.Masonry }, 17));

        Assert.True(techs.IsResearched(Tech.Loom));
        Assert.True(techs.IsPending(Tech.Masonry));
        Assert.False(techs.IsPending(Tech.Forging));
        Assert.Equal(17, techs.Version);
        Assert.Equal(new[] { Tech.Loom }, techs.ToState().Researched);
    }

    [Fact]
    public void TechStand_Restore_BeidesZugleich_WirftUndAendertNichts()
    {
        var techs = new TechProgress();
        techs.Begin(Tech.Forging);
        int version = techs.Version;

        Assert.Throws<ArgumentException>(() =>
            techs.Restore(new TechProgressState(new List<Tech> { Tech.Loom }, new List<Tech> { Tech.Loom }, 9)));

        Assert.True(techs.IsPending(Tech.Forging));
        Assert.False(techs.IsResearched(Tech.Loom));
        Assert.Equal(version, techs.Version);
    }

    // --- AgeProgress ---------------------------------------------------------

    [Fact]
    public void Zeitalter_ToStateUndRestore_SteigtGenauWeiterAuf()
    {
        var pool = new ResourcePool(1000, 0, 0, 0);
        var ages = new AgeProgress();
        Assert.True(ages.TryStart(pool));
        ages.Update(30f);

        var stand = ages.ToState();
        Assert.Equal(new AgeProgressState(Age.Dark, Age.Feudal, 30f), stand);

        var geladen = new AgeProgress();
        geladen.Restore(stand);

        Assert.True(geladen.IsResearching);
        Assert.Equal(ages.Progress, geladen.Progress);
        Assert.False(geladen.Update(99.9f));
        Assert.True(geladen.Update(0.2f));
        Assert.Equal(Age.Feudal, geladen.Current);
    }

    [Fact]
    public void Zeitalter_Restore_OhneAufstieg()
    {
        var ages = new AgeProgress();

        ages.Restore(new AgeProgressState(Age.Castle, null, 50f));

        Assert.Equal(Age.Castle, ages.Current);
        Assert.False(ages.IsResearching);
        Assert.Equal(new AgeProgressState(Age.Castle, null, 0f), ages.ToState());
    }

    [Fact]
    public void Zeitalter_Restore_NegativeZeit_WirdNull()
    {
        var ages = new AgeProgress();

        ages.Restore(new AgeProgressState(Age.Dark, Age.Feudal, -5f));

        Assert.Equal(0f, ages.Progress);
    }

    [Theory]
    [InlineData(Age.Dark, Age.Castle)]
    [InlineData(Age.Feudal, Age.Feudal)]
    [InlineData(Age.Castle, Age.Dark)]
    [InlineData(Age.Imperial, Age.Imperial)]
    public void Zeitalter_Restore_FalschesZiel_WirftUndAendertNichts(Age current, Age target)
    {
        var pool = new ResourcePool(1000, 0, 0, 0);
        var ages = new AgeProgress();
        ages.TryStart(pool);
        ages.Update(30f);
        var vorher = ages.ToState();

        Assert.Throws<ArgumentException>(() => ages.Restore(new AgeProgressState(current, target, 10f)));

        Assert.Equal(vorher, ages.ToState());
    }

    [Fact]
    public void Zeitalter_Restore_Null_Wirft()
    {
        Assert.Throws<ArgumentNullException>(() => new AgeProgress().Restore(null!));
    }

    // --- Construction --------------------------------------------------------

    [Fact]
    public void Baustelle_ToStateUndFromState_BautGenauWeiter()
    {
        var original = new Construction(30f);
        original.Update(12f, 1);

        var geladen = Construction.FromState(original.ToState());

        Assert.Equal(new ConstructionState(30f, original.Progress), geladen.ToState());
        Assert.Equal(original.Progress, geladen.Progress);
        Assert.False(geladen.Update(17.9f, 1));
        Assert.True(geladen.Update(0.2f, 1));
    }

    [Theory]
    [InlineData(1.5f, 1f)]
    [InlineData(-0.2f, 0f)]
    [InlineData(0.25f, 0.25f)]
    public void Baustelle_FromState_BegrenztDenFortschritt(float gespeichert, float erwartet)
    {
        var baustelle = Construction.FromState(new ConstructionState(30f, gespeichert));

        Assert.Equal(erwartet, baustelle.Progress);
        Assert.Equal(30f, baustelle.BuildSeconds);
    }

    [Fact]
    public void Baustelle_FromState_OhneBauzeit_Wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => Construction.FromState(new ConstructionState(0f, 0.5f)));
        Assert.Throws<ArgumentNullException>(() => Construction.FromState(null!));
    }
}
