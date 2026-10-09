using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>Forschungen (P2): Regeln nach AoE II, Ablauf je Gebäude und Wirkung.</summary>
public class ResearchTests
{
    static readonly Position Ort = new(5, 5);

    static TechProgress Erforscht(params Tech[] techs)
    {
        var stand = new TechProgress();
        foreach (var t in techs)
        {
            stand.Begin(t);
            stand.Complete(t);
        }
        return stand;
    }

    static ResourcePool Reich()
    {
        var pool = new ResourcePool();
        foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
            pool.Add(r, 1000);
        return pool;
    }

    // --- Regeln ---------------------------------------------------------------------

    [Theory]
    [InlineData(Tech.Loom, BuildingType.TownCenter, Age.Dark, 25f, "Webstuhl")]
    [InlineData(Tech.HorseCollar, BuildingType.Mill, Age.Feudal, 20f, "Pferdekummet")]
    [InlineData(Tech.DoubleBitAxe, BuildingType.LumberCamp, Age.Feudal, 25f, "Doppelaxt")]
    [InlineData(Tech.GoldMining, BuildingType.MiningCamp, Age.Feudal, 30f, "Goldbergbau")]
    [InlineData(Tech.Forging, BuildingType.Blacksmith, Age.Feudal, 50f, "Schmiedekunst")]
    [InlineData(Tech.Fletching, BuildingType.Blacksmith, Age.Feudal, 30f, "Befiederte Pfeile")]
    [InlineData(Tech.ScaleMailArmor, BuildingType.Blacksmith, Age.Feudal, 40f, "Schuppenpanzer")]
    [InlineData(Tech.Masonry, BuildingType.University, Age.Castle, 50f, "Maurerkunst")]
    public void Tabelle_Gebaeude_Zeitalter_Dauer_Name(Tech tech, BuildingType gebaeude, Age ab, float sekunden, string name)
    {
        Assert.Equal(gebaeude, TechRules.BuildingOf(tech));
        Assert.Equal(ab, TechRules.RequiredAgeOf(tech));
        Assert.Equal(sekunden, TechRules.SecondsOf(tech));
        Assert.Equal(name, TechRules.NameOf(tech));
    }

    [Theory]
    [InlineData(Tech.Loom, 0, 0, 50, 0)]
    [InlineData(Tech.HorseCollar, 75, 75, 0, 0)]
    [InlineData(Tech.DoubleBitAxe, 100, 50, 0, 0)]
    [InlineData(Tech.GoldMining, 100, 75, 0, 0)]
    [InlineData(Tech.Forging, 150, 0, 0, 0)]
    [InlineData(Tech.Fletching, 100, 0, 50, 0)]
    [InlineData(Tech.ScaleMailArmor, 100, 0, 0, 0)]
    [InlineData(Tech.Masonry, 0, 175, 0, 150)]
    public void Tabelle_Kosten_nur_die_genannten_Rohstoffe(Tech tech, int nahrung, int holz, int gold, int stein)
    {
        var soll = new Dictionary<Resource, int>();
        if (nahrung > 0) soll[Resource.Food] = nahrung;
        if (holz > 0) soll[Resource.Wood] = holz;
        if (gold > 0) soll[Resource.Gold] = gold;
        if (stein > 0) soll[Resource.Stone] = stein;
        Assert.Equal(soll.OrderBy(e => e.Key), TechRules.CostOf(tech).OrderBy(e => e.Key));
    }

    [Fact]
    public void Kosten_jedes_Mal_ein_neues_Woerterbuch()
    {
        var erste = TechRules.CostOf(Tech.Loom);
        erste[Resource.Gold] = 1;
        Assert.Equal(50, TechRules.CostOf(Tech.Loom)[Resource.Gold]);
    }

    [Fact]
    public void Wirkungstexte_genau_und_mit_der_HUD_Schrift_zeichenbar()
    {
        var soll = new Dictionary<Tech, string>
        {
            [Tech.Loom] = "Dorfbewohner +15 LP, Rüstung +1",
            [Tech.HorseCollar] = "neue Felder +75 Nahrung",
            [Tech.DoubleBitAxe] = "Holz 20 % schneller",
            [Tech.GoldMining] = "Gold 15 % schneller",
            [Tech.Forging] = "Infanterie und Reiter Angriff +1",
            [Tech.Fletching] = "Bogenschützen Angriff +1, Reichweite +1",
            [Tech.ScaleMailArmor] = "Infanterie Rüstung +1",
            [Tech.Masonry] = "Gebäude +10 % LP, Rüstung +1",
        };
        foreach (var tech in Enum.GetValues<Tech>())
        {
            Assert.Equal(soll[tech], TechRules.EffectOf(tech));
            Assert.All(TechRules.NameOf(tech) + TechRules.EffectOf(tech), c => Assert.InRange(c, (char)32, (char)254));
        }
    }

    [Fact]
    public void Forschungen_je_Gebaeude_in_Reihenfolge()
    {
        Assert.Equal(new[] { Tech.Forging, Tech.Fletching, Tech.ScaleMailArmor }, TechRules.In(BuildingType.Blacksmith));
        Assert.Equal(new[] { Tech.Loom }, TechRules.In(BuildingType.TownCenter));
        Assert.Equal(new[] { Tech.Masonry }, TechRules.In(BuildingType.University));
        Assert.Empty(TechRules.In(BuildingType.House));
        Assert.Empty(TechRules.In(BuildingType.Barracks));
    }

    [Fact]
    public void Unbekannte_Forschung_wirft()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.CostOf((Tech)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.SecondsOf((Tech)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.BuildingOf((Tech)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.RequiredAgeOf((Tech)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.NameOf((Tech)99));
        Assert.Throws<ArgumentOutOfRangeException>(() => TechRules.EffectOf((Tech)99));
    }

    // --- Forschungsstand eines Spielers -----------------------------------------------

    [Fact]
    public void Zu_Beginn_ist_alles_offen()
    {
        var stand = new TechProgress();
        Assert.Equal(0, stand.Version);
        foreach (var tech in Enum.GetValues<Tech>())
        {
            Assert.False(stand.IsResearched(tech));
            Assert.False(stand.IsPending(tech));
        }
    }

    [Fact]
    public void Begin_Complete_Release_mit_Version()
    {
        var stand = new TechProgress();
        Assert.True(stand.Begin(Tech.Forging));
        Assert.True(stand.IsPending(Tech.Forging));
        Assert.Equal(1, stand.Version);

        Assert.False(stand.Begin(Tech.Forging));      // läuft schon
        Assert.Equal(1, stand.Version);

        Assert.True(stand.Release(Tech.Forging));
        Assert.False(stand.IsPending(Tech.Forging));
        Assert.False(stand.IsResearched(Tech.Forging));
        Assert.Equal(2, stand.Version);

        Assert.True(stand.Begin(Tech.Forging));
        Assert.True(stand.Complete(Tech.Forging));
        Assert.True(stand.IsResearched(Tech.Forging));
        Assert.False(stand.IsPending(Tech.Forging));
        Assert.Equal(4, stand.Version);

        Assert.False(stand.Begin(Tech.Forging));      // schon erforscht
        Assert.False(stand.Complete(Tech.Forging));   // läuft nicht
        Assert.False(stand.Release(Tech.Forging));
        Assert.Equal(4, stand.Version);
    }

    [Fact]
    public void Complete_und_Release_nur_fuer_laufende()
    {
        var stand = new TechProgress();
        Assert.False(stand.Complete(Tech.Loom));
        Assert.False(stand.Release(Tech.Loom));
        Assert.False(stand.IsResearched(Tech.Loom));
        Assert.Equal(0, stand.Version);
    }

    [Fact]
    public void CanStart_prueft_Zeitalter_und_Stand()
    {
        var stand = new TechProgress();
        Assert.True(stand.CanStart(Tech.Loom, Age.Dark));
        Assert.False(stand.CanStart(Tech.Forging, Age.Dark));
        Assert.True(stand.CanStart(Tech.Forging, Age.Feudal));
        Assert.False(stand.CanStart(Tech.Masonry, Age.Feudal));
        Assert.True(stand.CanStart(Tech.Masonry, Age.Imperial));

        stand.Begin(Tech.Loom);
        Assert.False(stand.CanStart(Tech.Loom, Age.Dark));     // läuft
        stand.Complete(Tech.Loom);
        Assert.False(stand.CanStart(Tech.Loom, Age.Imperial)); // erforscht
    }

    // --- Forschung in einem Gebäude ---------------------------------------------------

    [Fact]
    public void Start_bezahlt_und_laeuft()
    {
        var stand = new TechProgress();
        var pool = new ResourcePool();
        int gold = pool[Resource.Gold];
        var slot = new ResearchSlot();
        Assert.False(slot.IsBusy);
        Assert.Null(slot.Current);
        Assert.Equal(0f, slot.Progress);

        Assert.True(slot.TryStart(Tech.Loom, Age.Dark, stand, pool));
        Assert.Equal(gold - 50, pool[Resource.Gold]);
        Assert.True(slot.IsBusy);
        Assert.Equal(Tech.Loom, slot.Current);
        Assert.Equal(0f, slot.Progress);
        Assert.True(stand.IsPending(Tech.Loom));
    }

    [Fact]
    public void Kein_Start_ohne_Rohstoffe_aendert_nichts()
    {
        var stand = new TechProgress();
        var pool = new ResourcePool();
        pool.Remove(Resource.Food, pool[Resource.Food] - 100);   // 100 Nahrung
        pool.Remove(Resource.Wood, pool[Resource.Wood] - 50);    // 50 Holz
        var slot = new ResearchSlot();

        // Goldbergbau: 100 Nahrung reichen, 75 Holz nicht - auch die Nahrung bleibt
        Assert.False(slot.TryStart(Tech.GoldMining, Age.Feudal, stand, pool));
        Assert.Equal(100, pool[Resource.Food]);
        Assert.Equal(50, pool[Resource.Wood]);
        Assert.False(slot.IsBusy);
        Assert.Null(slot.Current);
        Assert.False(stand.IsPending(Tech.GoldMining));
        Assert.Equal(0, stand.Version);

        // Doppelaxt: 100 Nahrung und 50 Holz - genau genug
        Assert.True(slot.TryStart(Tech.DoubleBitAxe, Age.Feudal, stand, pool));
        Assert.Equal(0, pool[Resource.Food]);
        Assert.Equal(0, pool[Resource.Wood]);
    }

    [Fact]
    public void Kein_Start_zu_frueh_bezahlt_nichts()
    {
        var stand = new TechProgress();
        var pool = Reich();
        int holz = pool[Resource.Wood], stein = pool[Resource.Stone];
        var slot = new ResearchSlot();
        Assert.False(slot.TryStart(Tech.Masonry, Age.Feudal, stand, pool));
        Assert.Equal(holz, pool[Resource.Wood]);
        Assert.Equal(stein, pool[Resource.Stone]);
        Assert.False(slot.IsBusy);
        Assert.False(stand.IsPending(Tech.Masonry));
        Assert.Equal(0, stand.Version);
    }

    [Fact]
    public void Ein_Gebaeude_forscht_nur_eine_zur_Zeit()
    {
        var stand = new TechProgress();
        var pool = Reich();
        var slot = new ResearchSlot();
        Assert.True(slot.TryStart(Tech.Forging, Age.Feudal, stand, pool));
        int nahrung = pool[Resource.Food], gold = pool[Resource.Gold];
        Assert.False(slot.TryStart(Tech.Fletching, Age.Feudal, stand, pool));
        Assert.Equal(nahrung, pool[Resource.Food]);
        Assert.Equal(gold, pool[Resource.Gold]);
        Assert.Equal(Tech.Forging, slot.Current);
        Assert.False(stand.IsPending(Tech.Fletching));
    }

    [Fact]
    public void Dieselbe_Forschung_nicht_in_zwei_Gebaeuden()
    {
        var stand = new TechProgress();
        var pool = Reich();
        Assert.True(new ResearchSlot().TryStart(Tech.Forging, Age.Feudal, stand, pool));
        int nahrung = pool[Resource.Food];
        var zweite = new ResearchSlot();
        Assert.False(zweite.TryStart(Tech.Forging, Age.Feudal, stand, pool));
        Assert.Equal(nahrung, pool[Resource.Food]);
        Assert.False(zweite.IsBusy);
        // Andere Forschung im zweiten Gebäude derselben Art geht
        Assert.True(zweite.TryStart(Tech.Fletching, Age.Feudal, stand, pool));
    }

    [Fact]
    public void Fortschritt_und_Abschluss_genau_nach_der_Dauer()
    {
        var stand = new TechProgress();
        var slot = new ResearchSlot();
        slot.TryStart(Tech.Fletching, Age.Feudal, stand, Reich());   // 30 s

        Assert.False(slot.Update(15f, stand, out _));
        Assert.Equal(0.5f, slot.Progress, 3);
        Assert.False(stand.IsResearched(Tech.Fletching));

        Assert.False(slot.Update(14.9f, stand, out _));
        Assert.True(slot.Update(0.2f, stand, out var fertig));
        Assert.Equal(Tech.Fletching, fertig);
        Assert.True(stand.IsResearched(Tech.Fletching));
        Assert.False(stand.IsPending(Tech.Fletching));
        Assert.False(slot.IsBusy);
        Assert.Null(slot.Current);
        Assert.Equal(0f, slot.Progress);

        Assert.False(slot.Update(100f, stand, out _));               // nichts mehr zu tun
    }

    [Fact]
    public void Ueberzaehlige_Zeit_verfaellt()
    {
        var stand = new TechProgress();
        var pool = Reich();
        var slot = new ResearchSlot();
        slot.TryStart(Tech.Forging, Age.Feudal, stand, pool);
        Assert.True(slot.Update(500f, stand, out _));
        Assert.True(slot.TryStart(Tech.ScaleMailArmor, Age.Feudal, stand, pool));
        Assert.Equal(0f, slot.Progress);
        Assert.False(slot.Update(39f, stand, out _));
    }

    [Fact]
    public void Abbruch_erstattet_und_gibt_frei()
    {
        var stand = new TechProgress();
        var pool = Reich();
        int holz = pool[Resource.Wood], stein = pool[Resource.Stone];
        var slot = new ResearchSlot();
        slot.TryStart(Tech.Masonry, Age.Castle, stand, pool);
        slot.Update(10f, stand, out _);

        Assert.True(slot.Cancel(stand, pool));
        Assert.Equal(holz, pool[Resource.Wood]);
        Assert.Equal(stein, pool[Resource.Stone]);
        Assert.False(slot.IsBusy);
        Assert.Equal(0f, slot.Progress);
        Assert.False(stand.IsPending(Tech.Masonry));
        Assert.False(stand.IsResearched(Tech.Masonry));
        Assert.True(stand.CanStart(Tech.Masonry, Age.Castle));

        Assert.False(slot.Cancel(stand, pool));                     // nichts mehr zu tun
        Assert.Equal(holz, pool[Resource.Wood]);
    }

    // --- Wirkung -----------------------------------------------------------------------

    [Fact]
    public void Webstuhl_staerkt_Dorfbewohner()
    {
        var dorf = new Villager(0, Ort);
        dorf.CurrentHp = 20;
        Assert.True(TechEffects.Apply(Tech.Loom, dorf));
        Assert.Equal(40, dorf.Stats.HitPoints);
        Assert.Equal(35, dorf.CurrentHp);
        Assert.Equal(1, dorf.Stats.BaseArmor);
        Assert.False(TechEffects.Apply(Tech.Loom, new Militia(0, Ort)));
    }

    [Fact]
    public void Schmiedekunst_fuer_Infanterie_und_Reiter()
    {
        var miliz = new Militia(0, Ort);
        var spaeher = new Scout(0, Ort);
        var bogen = new Archer(0, Ort);
        Assert.True(TechEffects.Apply(Tech.Forging, miliz));
        Assert.True(TechEffects.Apply(Tech.Forging, spaeher));
        Assert.True(TechEffects.Apply(Tech.Forging, new Knight(0, Ort)));
        Assert.True(TechEffects.Apply(Tech.Forging, new SpearMan(0, Ort)));
        Assert.True(TechEffects.Apply(Tech.Forging, new CamelRider(0, Ort)));
        Assert.Equal(5, miliz.Stats.BaseAttack);
        Assert.Equal(4, spaeher.Stats.BaseAttack);
        Assert.False(TechEffects.Apply(Tech.Forging, bogen));
        Assert.False(TechEffects.Apply(Tech.Forging, new Villager(0, Ort)));
        Assert.Equal(4, bogen.Stats.BaseAttack);
    }

    [Fact]
    public void Befiederte_Pfeile_fuer_Bogenschuetzen()
    {
        var bogen = new Archer(0, Ort);
        Assert.True(TechEffects.Apply(Tech.Fletching, bogen));
        Assert.Equal(5, bogen.Stats.BaseAttack);
        Assert.Equal(5, bogen.Stats.Range);
        Assert.True(TechEffects.Apply(Tech.Fletching, new Skirmisher(0, Ort)));
        var miliz = new Militia(0, Ort);
        Assert.False(TechEffects.Apply(Tech.Fletching, miliz));
        Assert.Equal(0, miliz.Stats.Range);
    }

    [Fact]
    public void Schuppenpanzer_fuer_Infanterie()
    {
        var miliz = new Militia(0, Ort);
        Assert.True(TechEffects.Apply(Tech.ScaleMailArmor, miliz));
        Assert.Equal(1, miliz.Stats.BaseArmor);
        Assert.True(TechEffects.Apply(Tech.ScaleMailArmor, new SpearMan(0, Ort)));
        Assert.False(TechEffects.Apply(Tech.ScaleMailArmor, new Scout(0, Ort)));
        Assert.False(TechEffects.Apply(Tech.ScaleMailArmor, new Villager(0, Ort)));
    }

    [Fact]
    public void Maurerkunst_staerkt_Gebaeude()
    {
        var haus = BuildingEntity.CreateHouse(0, Ort);               // 400 LP, Rüstung 1
        haus.CurrentHp = 300;
        Assert.True(TechEffects.Apply(Tech.Masonry, haus));
        Assert.Equal(440, haus.Stats.HitPoints);
        Assert.Equal(340, haus.CurrentHp);
        Assert.Equal(2, haus.Stats.BaseArmor);

        var mauer = BuildingEntity.Create(BuildingType.PalisadeWall, 0, Ort);   // 250 LP
        TechEffects.Apply(Tech.Masonry, mauer);
        Assert.Equal(275, mauer.Stats.HitPoints);
        Assert.Equal(275, mauer.CurrentHp);

        Assert.False(TechEffects.Apply(Tech.Masonry, new Villager(0, Ort)));
    }

    [Theory]
    [InlineData(Tech.HorseCollar)]
    [InlineData(Tech.DoubleBitAxe)]
    [InlineData(Tech.GoldMining)]
    public void Wirtschaftsforschungen_aendern_keine_Werte(Tech tech)
    {
        var dorf = new Villager(0, Ort);
        var haus = BuildingEntity.CreateHouse(0, Ort);
        Assert.False(TechEffects.Apply(tech, dorf));
        Assert.False(TechEffects.Apply(tech, haus));
        Assert.Equal(25, dorf.Stats.HitPoints);
        Assert.Equal(400, haus.Stats.HitPoints);
    }

    [Fact]
    public void ApplyAll_nur_erforschte()
    {
        var stand = Erforscht(Tech.Forging, Tech.ScaleMailArmor, Tech.Loom);
        stand.Begin(Tech.Fletching);                                   // läuft nur
        var miliz = new Militia(0, Ort);
        Assert.Equal(2, TechEffects.ApplyAll(stand, miliz));
        Assert.Equal(5, miliz.Stats.BaseAttack);
        Assert.Equal(1, miliz.Stats.BaseArmor);

        var bogen = new Archer(0, Ort);
        Assert.Equal(0, TechEffects.ApplyAll(stand, bogen));
        Assert.Equal(4, bogen.Stats.Range);

        var dorf = new Villager(0, Ort);
        Assert.Equal(1, TechEffects.ApplyAll(stand, dorf));
        Assert.Equal(40, dorf.CurrentHp);

        Assert.Equal(0, TechEffects.ApplyAll(new TechProgress(), new Militia(0, Ort)));
    }

    [Fact]
    public void Sammelfaktor_Holz_und_Gold()
    {
        var ohne = new TechProgress();
        foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
            Assert.Equal(1f, TechEffects.GatherFactor(ohne, r));

        var axt = Erforscht(Tech.DoubleBitAxe);
        Assert.Equal(1.2f, TechEffects.GatherFactor(axt, Resource.Wood), 4);
        Assert.Equal(1f, TechEffects.GatherFactor(axt, Resource.Gold));

        var gold = Erforscht(Tech.GoldMining);
        Assert.Equal(1.15f, TechEffects.GatherFactor(gold, Resource.Gold), 4);
        Assert.Equal(1f, TechEffects.GatherFactor(gold, Resource.Stone));
        Assert.Equal(1f, TechEffects.GatherFactor(gold, Resource.Wood));

        var laeuft = new TechProgress();
        laeuft.Begin(Tech.DoubleBitAxe);
        Assert.Equal(1f, TechEffects.GatherFactor(laeuft, Resource.Wood));
    }

    [Fact]
    public void Pferdekummet_fuer_neue_Felder()
    {
        Assert.Equal(175, TechEffects.FarmFood(new TechProgress(), 175));
        Assert.Equal(250, TechEffects.FarmFood(Erforscht(Tech.HorseCollar), 175));
        Assert.Equal(175, TechEffects.FarmFood(Erforscht(Tech.Loom, Tech.DoubleBitAxe), 175));
    }
}
