using System.Text.RegularExpressions;
using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Armeeführung in Aufstellung (<see cref="ArmyCommander"/>, KI-Stufen Defense und This is
/// Sparta, Nutzer 2026-11-07). Die Spielwelt ist die <see cref="FakeWorld"/>; Laufbefehle
/// versetzt <see cref="Apply"/> sofort an ihr Ziel, so lässt sich eine Welle über viele Takte
/// verfolgen.
/// </summary>
public class ArmyCommanderTests
{
    private static readonly Regex MoveRe = new(@"^move\((\d+),(-?\d+),(-?\d+)\)$");
    private static readonly Regex AttackRe = new(@"^attack\((\d+),(-?\d+),(-?\d+)\)$");

    /// <summary>Alle Laufbefehle ab <paramref name="from"/> ausführen: die Einheit steht danach untätig am Ziel.</summary>
    private static void Apply(FakeWorld w, int from)
    {
        for (int i = from; i < w.Log.Count; i++)
        {
            var m = MoveRe.Match(w.Log[i]);
            if (!m.Success)
            {
                // wie im Spiel: ein Angriff auf eine Kachel ohne Feind ist ein Laufbefehl
                m = AttackRe.Match(w.Log[i]);
                if (!m.Success) continue;
                int ax = int.Parse(m.Groups[2].Value), ay = int.Parse(m.Groups[3].Value);
                if (w.VisibleEnemies().Any(e => e.X == ax && e.Y == ay)) continue;
            }
            int id = int.Parse(m.Groups[1].Value);
            int k = w.AllUnits.FindIndex(u => u.Id == id);
            if (k < 0) continue;
            var u = w.AllUnits[k];
            w.AllUnits[k] = new UnitSnapshot { Id = u.Id, Owner = u.Owner, Kind = u.Kind, State = u.State == UnitStateKind.Dead ? u.State : UnitStateKind.Idle,
                                               X = int.Parse(m.Groups[2].Value), Y = int.Parse(m.Groups[3].Value),
                                               Health = u.Health, MaxHealth = u.MaxHealth };
        }
    }

    private static void Kill(FakeWorld w, int id)
    {
        int k = w.AllUnits.FindIndex(u => u.Id == id);
        var u = w.AllUnits[k];
        w.AllUnits[k] = new UnitSnapshot { Id = u.Id, Kind = u.Kind, X = u.X, Y = u.Y, State = UnitStateKind.Dead };
    }

    /// <summary>Takte zu je 0,5 s, Laufbefehle sofort ausgeführt; bricht ab, sobald <paramref name="until"/> gilt.</summary>
    private static void Run(ArmyCommander army, FakeWorld w, AiContext ctx, double seconds, Func<bool>? until = null)
    {
        for (double t = 0; t < seconds; t += 0.5)
        {
            int from = w.Log.Count;
            ctx.Time += 0.5;
            army.Tick(w, w, ctx);
            Apply(w, from);
            if (until?.Invoke() == true) return;
        }
    }

    /// <summary>Stadtzentrum bei (4, 4) (Mitte (6, 6)), Feind-Gebäude bei (50, 50).</summary>
    private static FakeWorld Dorf(bool enemyBase = true)
    {
        var w = new FakeWorld();
        w.AddBuilding(BuildingType.TownCenter, 4, 4, complete: true);
        if (enemyBase) w.AddEnemy(new EnemyInfo(50, 50, IsBuilding: true, Health: 500));
        return w;
    }

    private static List<int> Armee(FakeWorld w, int melee, int archers, int riders, int x = 8, int y = 8)
    {
        var ids = new List<int>();
        for (int i = 0; i < melee; i++) ids.Add(w.AddSoldier(x + i % 3, y, UnitKind.Militia));
        for (int i = 0; i < archers; i++) ids.Add(w.AddSoldier(x + i % 3, y + 1, UnitKind.Archer));
        for (int i = 0; i < riders; i++) ids.Add(w.AddSoldier(x + i % 3, y + 2, UnitKind.Cavalry));
        return ids;
    }

    private static float Depth((int X, int Y) p, (int X, int Y) anchor, (float X, float Y) f)
        => (p.X - anchor.X) * f.X + (p.Y - anchor.Y) * f.Y;

    private static UnitSnapshot U(FakeWorld w, int id) => w.AllUnits.First(u => u.Id == id);

    /// <summary>Die Plätze der Angriffswelle ohne die Heimwache.</summary>
    private static Dictionary<int, (int X, int Y)> WaveSlots(ArmyCommander army)
        => army.Slots.Where(kv => army.Wave.Contains(kv.Key)).ToDictionary(kv => kv.Key, kv => kv.Value);

    /// <summary>Die Nahkämpfer der Welle (ohne die Heimwache, die daheim bleibt).</summary>
    private static List<int> WaveMelee(ArmyCommander army, FakeWorld w, IEnumerable<int> ids)
        => ids.Where(id => army.Wave.Contains(id) && U(w, id).Kind == UnitKind.Militia).ToList();

    /// <summary>Ein Feindtrupp aus drei Milizen um (x, y) - mehr als ein Geplänkel.</summary>
    private static List<(int X, int Y)> Trupp(FakeWorld w, int x, int y)
    {
        var at = new List<(int X, int Y)> { (x, y), (x + 1, y - 1), (x - 1, y + 1) };
        foreach (var p in at) w.AddEnemy(new EnemyInfo(p.X, p.Y, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        return at;
    }

    /// <summary>Prüft die Aufstellung: alle Plätze verschieden, je zwei mindestens 2 Kacheln
    /// (Chebyshev) auseinander, jeder Schütze hinter jedem Nahkämpfer.</summary>
    private static void AssertFormation(FakeWorld w, IReadOnlyDictionary<int, (int X, int Y)> slots,
                                         (int X, int Y) anchor, (float X, float Y) f)
    {
        var list = slots.ToList();
        for (int i = 0; i < list.Count; i++)
            for (int j = i + 1; j < list.Count; j++)
            {
                var a = list[i].Value; var b = list[j].Value;
                Assert.True(Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) >= 2,
                    $"Plätze {a} und {b} zu dicht");
            }
        var front = list.Where(kv => U(w, kv.Key).Kind == UnitKind.Militia).Select(kv => Depth(kv.Value, anchor, f)).ToList();
        var back = list.Where(kv => U(w, kv.Key).Kind == UnitKind.Archer).Select(kv => Depth(kv.Value, anchor, f)).ToList();
        if (front.Count > 0 && back.Count > 0)
            Assert.True(back.Max() < front.Min(), $"Schützen nicht hinter den Nahkämpfern: {back.Max()} ≥ {front.Min()}");
    }

    // ------------------------------------------------------------------ Stufen

    [Fact]
    public void Nur_Defense_und_Sparta_fuehren_in_Aufstellung()
    {
        Assert.Null(new EconomyAi(AiProfile.Standard).Army);
        Assert.NotNull(new EconomyAi(AiProfile.Defense).Army);
        Assert.NotNull(new EconomyAi(AiProfile.Sparta).Army);
    }

    // ------------------------------------------------------------------ Heimgruppe

    [Fact]
    public void Heimgruppe_steht_in_Aufstellung_vor_dem_Dorf_Richtung_Gegner()
    {
        var w = Dorf();
        Armee(w, 3, 2, 0);
        var army = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 1);

        // Mitte (6, 6), Gegner diagonal → Sammelpunkt 6 Kacheln diagonal davor
        Assert.Equal((10, 10), army.Rally);
        Assert.Equal(5, army.Slots.Count);
        AssertFormation(w, army.Slots, army.Rally, army.HomeFacing);
        foreach (var kv in army.Slots)
            Assert.Equal(kv.Value, (U(w, kv.Key).X, U(w, kv.Key).Y));   // alle auf ihren Plätzen
    }

    [Fact]
    public void Sammelpunkt_weicht_einem_Waeldchen_aus_und_haelt_Abstand()
    {
        var w = Dorf();
        // Wäldchen 7×7 um den idealen Sammelpunkt (10, 10), nur der Punkt selbst ist frei
        for (int x = 7; x <= 13; x++)
            for (int y = 7; y <= 13; y++)
                if ((x, y) != (10, 10)) w.BlockWalk(x, y);
        Armee(w, 6, 4, 2, x: 20, y: 4);
        var army = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 1);
        Assert.NotEqual((10, 10), army.Rally);
        var slots = army.Slots.Values.ToList();
        Assert.Equal(12, slots.Count);
        foreach (var p in slots)
            Assert.True(w.IsWalkable(p.X, p.Y), $"{p} liegt im Wäldchen");
        for (int i = 0; i < slots.Count; i++)
            for (int j = i + 1; j < slots.Count; j++)
                Assert.True(Math.Max(Math.Abs(slots[i].X - slots[j].X), Math.Abs(slots[i].Y - slots[j].Y)) >= 2,
                    $"{slots[i]} und {slots[j]} zu dicht");

        // ein Soldat mehr: der Sammelpunkt springt nicht ohne Not
        var rally = army.Rally;
        w.AddSoldier(20, 4, UnitKind.Militia);
        Run(army, w, ctx, 1);
        Assert.True(Math.Max(Math.Abs(army.Rally.X - rally.X), Math.Abs(army.Rally.Y - rally.Y)) <= 2,
            $"Sammelpunkt sprang von {rally} nach {army.Rally}");
    }

    [Fact]
    public void Ohne_bekannten_Gegner_steht_die_Heimgruppe_Richtung_Kartenmitte()
    {
        var w = Dorf(enemyBase: false);
        Armee(w, 2, 0, 0);
        var army = new ArmyCommander(AiProfile.Sparta);
        Run(army, w, new AiContext(1), 1);
        Assert.Equal((10, 10), army.Rally);   // Kartenmitte (32, 32) liegt ebenfalls diagonal
        Assert.Equal(ArmyPhase.Gather, army.Phase);
    }

    [Fact]
    public void Ohne_bekanntes_Ziel_spaeht_ein_Reiter_in_den_Nebel()
    {
        var w = Dorf(enemyBase: false);
        // alles erkundet außer einem Streifen am rechten Rand
        for (int y = 0; y < w.Height; y++) w.MarkNotExplored(w.Width - 1, y);
        var ids = Armee(w, 6, 4, 2);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 1);
        Assert.NotNull(army.Scout);
        Assert.Equal(UnitKind.Cavalry, U(w, army.Scout!.Value).Kind);
        Assert.Contains(w.Log, l => l.StartsWith($"move({army.Scout},{w.Width - 1},"));
        Assert.Equal(ArmyPhase.Gather, army.Phase);

        // er findet ein Gebäude: der Späher kehrt heim, die Welle zieht
        w.AddEnemy(new EnemyInfo(w.Width - 2, 8, IsBuilding: true, Health: 500));
        Run(army, w, ctx, 40, () => army.Phase == ArmyPhase.March);
        Assert.Null(army.Scout);
        Assert.Equal(ArmyPhase.March, army.Phase);
    }

    [Fact]
    public void Defense_und_kleine_Heimgruppen_spaehen_nicht()
    {
        var w = Dorf(enemyBase: false);
        Armee(w, 6, 4, 2);
        var defense = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        Run(defense, w, ctx, 2);
        Assert.Null(defense.Scout);

        var klein = Dorf(enemyBase: false);
        Armee(klein, 5, 4, 2);   // 11 < 12
        var sparta = new ArmyCommander(AiProfile.Sparta);
        Run(sparta, klein, new AiContext(1), 2);
        Assert.Null(sparta.Scout);
    }

    [Fact]
    public void Defense_greift_nie_an_auch_mit_grosser_Armee()
    {
        var w = Dorf();
        Armee(w, 8, 8, 0);
        var army = new ArmyCommander(AiProfile.Defense);
        Run(army, w, new AiContext(1), 120);
        Assert.Equal(ArmyPhase.Gather, army.Phase);
        Assert.Empty(army.Wave);
        Assert.DoesNotContain(w.Log, l => l.StartsWith("attack("));
        Assert.All(w.AllUnits.Where(u => u.Kind != UnitKind.Villager),
                   u => Assert.True(Math.Max(Math.Abs(u.X - 10), Math.Abs(u.Y - 10)) <= 8, $"Soldat bei ({u.X},{u.Y}) zu weit vom Sammelpunkt"));
    }

    [Fact]
    public void Defense_schlaegt_Eindringlinge_im_Heimradius_zurueck_aber_nicht_draussen()
    {
        var w = Dorf();
        var ids = Armee(w, 3, 2, 0);
        var army = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 1);

        // Feind 30 Kacheln draußen: keine Reaktion
        w.AddEnemy(new EnemyInfo(36, 36, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        int from = w.Log.Count;
        Run(army, w, ctx, 2);
        Assert.DoesNotContain(w.Log.Skip(from), l => l.StartsWith("attack("));

        // Feind im Dorf: die Nahkämpfer greifen ihn an
        w.AddEnemy(new EnemyInfo(14, 12, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        from = w.Log.Count;
        Run(army, w, ctx, 0.5);
        foreach (int id in ids.Take(3))
            Assert.Contains($"attack({id},14,12)", w.Log.Skip(from));
    }

    [Fact]
    public void Gemeldete_Bedrohung_bis_doppelter_Heimradius_wird_abgewehrt()
    {
        var w = Dorf();
        Armee(w, 2, 0, 0);
        var army = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        var raider = new EnemyInfo(30, 10, IsBuilding: false, Health: 40, IsAttackingUs: true, Kind: UnitKind.Militia);
        w.AddEnemy(raider);
        ctx.Time = 1;
        army.Tick(w, w, ctx, raider);   // 24 Kacheln weg, Heimradius 14 → bis 28 zählt sie
        Assert.Contains(w.Log, l => l.StartsWith("attack(") && l.EndsWith(",30,10)"));
    }

    // ------------------------------------------------------------------ Welle

    private static (ArmyCommander Army, FakeWorld W, AiContext Ctx, List<int> Ids) Losgezogen(int melee = 5, int archers = 5, int riders = 2)
    {
        var w = Dorf();
        var ids = Armee(w, melee, archers, riders);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 10, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
        // ein Stück marschieren: aus dem Heimradius (14) heraus, sonst übernimmt die Heimabwehr
        Run(army, w, ctx, 60, () => Math.Max(Math.Abs(army.Anchor.X - 6), Math.Abs(army.Anchor.Y - 6)) >= 20);
        Assert.Equal(ArmyPhase.March, army.Phase);
        return (army, w, ctx, ids);
    }

    [Fact]
    public void Sparta_zieht_erst_mit_Mindeststaerke_los()
    {
        var w = Dorf();
        Armee(w, 5, 4, 2);   // 11 < RaidMinSoldiers 12
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 40);
        Assert.Equal(ArmyPhase.Gather, army.Phase);

        w.AddSoldier(10, 10, UnitKind.Militia);
        Run(army, w, ctx, 10, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
        Assert.Equal(12 - AiProfile.Sparta.HomeGuard, army.Wave.Count);   // die Heimwache bleibt
        Assert.Equal((50, 50), army.Target);
    }

    [Fact]
    public void Sparta_marschiert_in_Aufstellung_und_belagert_das_Ziel()
    {
        var (army, w, ctx, ids) = Losgezogen();
        int checks = 0;
        for (int i = 0; i < 200 && army.Phase == ArmyPhase.March; i++)
        {
            int from = w.Log.Count;
            ctx.Time += 0.5;
            army.Tick(w, w, ctx);
            if (army.Phase == ArmyPhase.March && army.Slots.Count == ids.Count)
            {
                AssertFormation(w, WaveSlots(army), army.Anchor, army.Facing);
                checks++;
            }
            Apply(w, from);
        }
        Assert.True(checks > 10, $"nur {checks} Marschtakte");
        Assert.Equal(ArmyPhase.Siege, army.Phase);

        int start = w.Log.Count;
        Run(army, w, ctx, 0.5);
        foreach (int id in army.Wave)
            Assert.Contains($"attack({id},50,50)", w.Log.Skip(start));
        // die Heimwache (Nahkämpfer) blieb daheim
        Assert.Equal(AiProfile.Sparta.HomeGuard, ids.Count(id => !army.Wave.Contains(id)));
        Assert.All(ids.Where(id => !army.Wave.Contains(id)), id => Assert.Equal(UnitKind.Militia, U(w, id).Kind));
    }

    [Fact]
    public void Marsch_wartet_auf_Nachzuegler()
    {
        var (army, w, ctx, _) = Losgezogen();
        var anchor = army.Anchor;
        // Laufbefehle nicht ausführen: niemand kommt an → kein Schritt vor der Frist
        for (int i = 0; i < 10; i++) { ctx.Time += 0.5; army.Tick(w, w, ctx); }
        Assert.Equal(ArmyPhase.March, army.Phase);
        int moved = Math.Max(Math.Abs(army.Anchor.X - anchor.X), Math.Abs(army.Anchor.Y - anchor.Y));
        Assert.True(moved <= ArmyCommander.MarchStep, $"Anker {moved} Kacheln weiter ohne die Truppe");
    }

    [Fact]
    public void Marsch_wartet_aufs_Katapult_und_geht_nach_der_Frist_weiter()
    {
        // F15: Geräte marschieren langsamer als Fußvolk. Der Verband darf nicht im
        // Fußvolk-Tempo von Platz zu Platz eilen, während das Katapult zurückfällt.
        var w = Dorf();
        Armee(w, 5, 5, 2);
        var kat = new[] { w.AddSoldier(9, 9, UnitKind.Catapult), w.AddSoldier(10, 9, UnitKind.Catapult) };
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 10, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
        Assert.Contains(army.Wave, id => kat.Contains(id));   // die Geräte gehören zur Welle
        Run(army, w, ctx, 60, () => Math.Max(Math.Abs(army.Anchor.X - 6), Math.Abs(army.Anchor.Y - 6)) >= 20);
        Assert.Equal(ArmyPhase.March, army.Phase);

        // Das Katapult bleibt zurück: jeden Takt zurück nach (9, 9) gesetzt, die anderen
        // marschieren wie gehabt auf ihre Plätze.
        void Takt()
        {
            int from = w.Log.Count;
            ctx.Time += 0.5;
            army.Tick(w, w, ctx);
            Apply(w, from);
            foreach (int id in kat)
            {
                int k = w.AllUnits.FindIndex(u => u.Id == id);
                var u = w.AllUnits[k];
                w.AllUnits[k] = new UnitSnapshot { Id = u.Id, Owner = u.Owner, Kind = u.Kind, X = 9, Y = 9,
                                                   State = UnitStateKind.Idle, Health = u.Health, MaxHealth = u.MaxHealth };
            }
        }
        int cheb((int X, int Y) a, (int X, int Y) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));
        var a0 = army.Anchor;
        for (int i = 0; i < 16; i++) Takt();   // 8 s: unter der Frist (10 s)
        Assert.True(cheb(army.Anchor, a0) <= ArmyCommander.MarchStep,
            $"Verband {cheb(army.Anchor, a0)} Kacheln weitergerückt, obwohl das Katapult fehlt");
        for (int i = 0; i < 24; i++) Takt();   // über die Frist: der Marsch bleibt nicht stehen (zwei Schritte)
        Assert.True(army.Phase == ArmyPhase.March && cheb(army.Anchor, a0) > ArmyCommander.MarchStep,
            $"auch nach der Frist kein Schritt (Phase {army.Phase}, Anker {army.Anchor})");
    }

    [Fact]
    public void Kampf_Nahkaempfer_vorn_Schuetzen_schiessen_aus_der_zweiten_Reihe()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 6, archers: 6, riders: 0);
        var a = army.Anchor; var f = army.Facing;
        // Feindtrupp 4 Kacheln vor dem Anker
        int ex = a.X + (int)MathF.Round(f.X * 4), ey = a.Y + (int)MathF.Round(f.Y * 4);
        var trupp = Trupp(w, ex, ey).Select(p => $",{p.X},{p.Y})").ToList();
        int from = w.Log.Count;
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.Engage, army.Phase);
        var melee = WaveMelee(army, w, ids);
        Assert.Equal(6 - AiProfile.Sparta.HomeGuard, melee.Count);
        foreach (int id in melee)
            Assert.Contains(w.Log.Skip(from), l => l.StartsWith($"attack({id},") && trupp.Any(l.EndsWith));
        // die Nahkämpfer verteilen sich auf den Trupp statt alle auf einen
        Assert.True(trupp.Count(t => w.Log.Skip(from).Any(l => melee.Any(id => l == $"attack({id}" + t))) >= 2);
        // Schützen schießen oder rücken auf Bogenweite nach, keiner läuft per Laufbefehl nach vorn
        Assert.Contains(w.Log.Skip(from), l => ids.Skip(6).Any(id => l.StartsWith($"attack({id},")));
        Assert.DoesNotContain(w.Log.Skip(from), l => ids.Skip(6).Any(id => l.StartsWith($"move({id},")));
    }

    [Fact]
    public void Schuetze_weicht_einem_nahen_Nahkaempfer_aus()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 6, archers: 6, riders: 0);
        var archer = U(w, ids[7]);
        w.AddEnemy(new EnemyInfo(archer.X + 1, archer.Y, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Contains($"move({archer.Id},{archer.X - ArmyCommander.KiteStep},{archer.Y})", w.Log.Skip(from));
    }

    [Fact]
    public void Reiter_greifen_im_Schwall_an_ziehen_sich_zurueck_und_kommen_wieder()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 5, archers: 5, riders: 2);
        var riders = ids.Skip(10).ToList();
        var a = army.Anchor; var f = army.Facing;
        int ex = a.X + (int)MathF.Round(f.X * 5), ey = a.Y + (int)MathF.Round(f.Y * 5);
        w.AddEnemy(new EnemyInfo(ex + 1, ey - 1, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        w.AddEnemy(new EnemyInfo(ex + 3, ey - 2, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));   // 3 vom Schützen: keine Wache (KK2)
        w.AddEnemy(new EnemyInfo(ex, ey, IsBuilding: false, Health: 30, Kind: UnitKind.Archer));

        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(CavalryMode.Charging, army.WaveCavalry);
        foreach (int id in riders)   // Schützen zuerst
            Assert.Contains($"attack({id},{ex},{ey})", w.Log.Skip(from));

        // nach dem Angriff: Rückzug hinter die Aufstellung
        from = w.Log.Count;
        for (double t = 0; t < ArmyCommander.ChargeSeconds; t += 0.5) { ctx.Time += 0.5; army.Tick(w, w, ctx); }
        Assert.Equal(CavalryMode.Returning, army.WaveCavalry);
        foreach (int id in riders)
        {
            var mv = w.Log.Skip(from).Last(l => l.StartsWith($"move({id},"));
            var m = MoveRe.Match(mv);
            var p = (int.Parse(m.Groups[2].Value), int.Parse(m.Groups[3].Value));
            Assert.True(Depth(p, a, f) <= -ArmyCommander.CavalryFallback + 2, $"Reiter {id} zieht nur nach {p}");
        }

        // nach der Pause: der nächste Schwall
        from = w.Log.Count;
        for (double t = 0; t <= ArmyCommander.RestSeconds + 0.5; t += 0.5) { ctx.Time += 0.5; army.Tick(w, w, ctx); }
        Assert.Equal(CavalryMode.Charging, army.WaveCavalry);
        foreach (int id in riders)
            Assert.Contains(w.Log.Skip(from), l => l.StartsWith($"attack({id},"));
    }

    [Fact]
    public void Geplaenkel_zwei_Nahkaempfer_kuemmern_sich_der_Rest_marschiert_weiter()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 6, archers: 6, riders: 0);
        var a = army.Anchor; var f = army.Facing;
        // eine einzelne Miliz seitlich der Welle
        int ex = a.X + (int)MathF.Round(-f.Y * 7), ey = a.Y + (int)MathF.Round(f.X * 7);
        w.AddEnemy(new EnemyInfo(ex, ey, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        int from = w.Log.Count;
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.March, army.Phase);
        var melee = ids.Take(6).Where(id => w.Log.Skip(from).Contains($"attack({id},{ex},{ey})")).ToList();
        Assert.Equal(2, melee.Count);

        // die Welle zieht weiter (bis vors Ziel, wenn sie schnell genug ist)
        var start = army.Anchor;
        Run(army, w, ctx, 10);
        Assert.Contains(army.Phase, new[] { ArmyPhase.March, ArmyPhase.Siege });
        Assert.True(Math.Max(Math.Abs(army.Anchor.X - start.X), Math.Abs(army.Anchor.Y - start.Y)) >= ArmyCommander.MarchStep,
            "die Welle steht wegen eines einzelnen Feindes");
    }

    [Fact]
    public void Geplaenkel_mit_Reitern_die_Reiter_uebernehmen()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 5, archers: 5, riders: 2);
        var a = army.Anchor; var f = army.Facing;
        int ex = a.X + (int)MathF.Round(f.X * 6), ey = a.Y + (int)MathF.Round(f.Y * 6);
        w.AddEnemy(new EnemyInfo(ex, ey, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        int from = w.Log.Count;
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.March, army.Phase);
        Assert.Equal(CavalryMode.Charging, army.WaveCavalry);
        foreach (int id in ids.Skip(10))
            Assert.Contains($"attack({id},{ex},{ey})", w.Log.Skip(from));
        Assert.DoesNotContain(w.Log.Skip(from), l => ids.Take(5).Any(id => l.StartsWith($"attack({id},")));
    }

    [Fact]
    public void Leine_der_Welle_ruft_Verfolger_zurueck()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 6, archers: 6, riders: 0);
        // Einheit 1 jagt 15 Kacheln hinter dem Verband einem Feind nach, den die Welle nicht sieht
        int k = w.AllUnits.FindIndex(u => u.Id == ids[0]);
        var u0 = w.AllUnits[k];
        w.AllUnits[k] = new UnitSnapshot { Id = u0.Id, Kind = u0.Kind, X = u0.X - 15, Y = u0.Y - 15, State = UnitStateKind.Moving,
                                           Health = 40, MaxHealth = 40, HasAttack = true };
        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Contains(w.Log.Skip(from), l => l.StartsWith($"move({ids[0]},"));
        // kämpft er weiter (die Gegenwehr des Spiels), wird er nach der Frist erneut gerufen
        from = w.Log.Count;
        for (int i = 0; i < 8; i++) { ctx.Time += 0.5; army.Tick(w, w, ctx); }
        Assert.Contains(w.Log.Skip(from), l => l.StartsWith($"move({ids[0]},"));
    }

    [Fact]
    public void Nach_dem_Kampf_neu_aufstellen_und_weiter()
    {
        var (army, w, ctx, _) = Losgezogen();
        var a = army.Anchor; var f = army.Facing;
        Trupp(w, a.X + (int)MathF.Round(f.X * 4), a.Y + (int)MathF.Round(f.Y * 4));
        Run(army, w, ctx, 1);
        Assert.Equal(ArmyPhase.Engage, army.Phase);

        w.ClearEnemies();
        w.AddEnemy(new EnemyInfo(50, 50, IsBuilding: true, Health: 500));
        Run(army, w, ctx, 2);
        Assert.Equal(ArmyPhase.Engage, army.Phase);              // erst ausklingen lassen
        Run(army, w, ctx, ArmyCommander.CalmSeconds);
        Assert.Equal(ArmyPhase.March, army.Phase);
    }

    [Fact]
    public void Rueckzug_nach_schweren_Verlusten_und_Welle_endet_am_Sammelpunkt()
    {
        var (army, w, ctx, ids) = Losgezogen();
        Run(army, w, ctx, 15);   // ein Stück weg vom Dorf
        // 9 in der Welle (3 Heimwache), Rückzug unter ceil(9 · 0,35) = 4
        Assert.Equal(12 - AiProfile.Sparta.HomeGuard, army.Wave.Count);
        foreach (int id in army.Wave.Take(6).ToList()) Kill(w, id);
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(ArmyPhase.Retreat, army.Phase);

        Run(army, w, ctx, 5, () => army.Phase == ArmyPhase.Gather);
        Assert.Equal(ArmyPhase.Gather, army.Phase);
        Assert.Empty(army.Wave);
    }

    [Fact]
    public void Nach_einer_abgeschlagenen_Welle_wartet_die_naechste_auf_mehr_Soldaten()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 6, archers: 6, riders: 4);   // 16, Welle 13
        Run(army, w, ctx, 5);
        foreach (int id in army.Wave.Take(10).ToList()) Kill(w, id);
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.Retreat, army.Phase);
        Assert.Equal(1, army.Beaten);
        Assert.Equal(AiProfile.Sparta.RaidMinSoldiers + ArmyCommander.BeatenExtra, army.RequiredWave);
        Run(army, w, ctx, 60, () => army.Phase == ArmyPhase.Gather);
        Assert.Equal(ArmyPhase.Gather, army.Phase);

        // 6 übrig + 8 neue = 14 < 16: keine Welle
        for (int i = 0; i < 8; i++) w.AddSoldier(8 + i % 3, 9, UnitKind.Militia);
        Run(army, w, ctx, 40);
        Assert.Equal(ArmyPhase.Gather, army.Phase);
        // zwei mehr: los
        w.AddSoldier(9, 9, UnitKind.Militia);
        w.AddSoldier(10, 9, UnitKind.Militia);
        Run(army, w, ctx, 40, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
    }

    [Fact]
    public void Ohne_neue_Niederlage_verfaellt_der_Zuschlag()
    {
        var (army, w, ctx, _) = Losgezogen(melee: 6, archers: 6, riders: 4);
        Run(army, w, ctx, 5);
        foreach (int id in army.Wave.Take(10).ToList()) Kill(w, id);
        Run(army, w, ctx, 60, () => army.Phase == ArmyPhase.Gather);
        Assert.Equal(1, army.Beaten);
        Run(army, w, ctx, ArmyCommander.BeatenForgetSeconds + 5);
        Assert.Equal(0, army.Beaten);
        Assert.Equal(AiProfile.Sparta.RaidMinSoldiers, army.RequiredWave);
    }

    [Fact]
    public void Der_Zuschlag_ist_hoechstens_das_Soll()
    {
        var p = AiProfile.Sparta;
        Assert.True(p.RaidMinSoldiers + 3 * ArmyCommander.BeatenExtra > p.SoldierTarget);
        var (army, w, ctx, _) = Losgezogen(melee: 6, archers: 6, riders: 4);
        for (int k = 0; k < 3; k++)
        {
            Run(army, w, ctx, 2);
            foreach (int id in army.Wave.ToList()) Kill(w, id);
            Run(army, w, ctx, 1);
            // neue Truppe, sofort wieder los (für den Test: RequiredWave erreicht)
            for (int i = 0; i < 20; i++) w.AddSoldier(8 + i % 3, 9 + i / 3 % 2, UnitKind.Militia);
            Run(army, w, ctx, 60, () => army.Phase == ArmyPhase.March);
            Run(army, w, ctx, 60, () => Math.Max(Math.Abs(army.Anchor.X - 6), Math.Abs(army.Anchor.Y - 6)) >= 20);
        }
        Assert.True(army.RequiredWave <= p.SoldierTarget, $"{army.RequiredWave} > {p.SoldierTarget}");
    }

    [Fact]
    public void Feindtrupp_im_Dorf_ruft_die_Welle_zurueck()
    {
        var (army, w, ctx, _) = Losgezogen();
        // zwei Feinde im Dorf: die Heimwache genügt, die Welle zieht weiter
        w.AddEnemy(new EnemyInfo(8, 8, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        w.AddEnemy(new EnemyInfo(9, 8, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.March, army.Phase);
        // ein dritter: zurück
        w.AddEnemy(new EnemyInfo(8, 9, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.Retreat, army.Phase);
    }

    [Fact]
    public void Zerstoertes_Ziel_wird_vergessen_naechstes_Ziel_oder_heim()
    {
        var (army, w, ctx, ids) = Losgezogen();
        Run(army, w, ctx, 200, () => army.Phase == ArmyPhase.Siege);
        Assert.Equal(ArmyPhase.Siege, army.Phase);

        // Gebäude fällt, ein zweites steht weiter hinten; wer es angriff, läuft auf die leere Kachel und sieht es fehlen
        w.ClearEnemies();
        w.AddEnemy(new EnemyInfo(58, 50, IsBuilding: true, Health: 300));
        Run(army, w, ctx, 3, () => army.Target == (58, 50));
        Assert.Equal((58, 50), army.Target);
        Assert.False(army.KnownBuildings.ContainsKey((50, 50)));

        // auch das fällt: nichts mehr bekannt → zurück
        Run(army, w, ctx, 120, () => army.Phase == ArmyPhase.Siege);
        Assert.Equal(ArmyPhase.Siege, army.Phase);
        w.ClearEnemies();
        Run(army, w, ctx, 3, () => army.Phase == ArmyPhase.Retreat);
        Assert.Equal(ArmyPhase.Retreat, army.Phase);
    }

    [Fact]
    public void Welle_meidet_Ziele_in_Schussweite_bewehrter_Gebaeude()
    {
        var w = Dorf(enemyBase: false);
        w.AddEnemy(new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true));
        w.AddEnemy(new EnemyInfo(36, 38, IsBuilding: true, Health: 150));   // Haus neben dem Stadtzentrum
        w.AddEnemy(new EnemyInfo(52, 30, IsBuilding: true, Health: 150));   // Holzfällerlager draußen
        Armee(w, 6, 4, 2);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 30, () => army.Phase == ArmyPhase.March);
        Assert.Equal((52, 30), army.Target);
    }

    [Fact]
    public void Gemerktes_Gebaeude_bleibt_Ziel_ausser_Sicht()
    {
        var w = Dorf();
        Armee(w, 7, 5, 0);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 0.5);
        w.ClearEnemies();   // Späher weitergezogen, das Gebäude liegt wieder im Nebel
        Run(army, w, ctx, 30, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
        Assert.Equal((50, 50), army.Target);
    }

    [Fact]
    public void Kaempfende_bekommen_keinen_neuen_Befehl()
    {
        var w = Dorf();
        int fighter = w.AddSoldier(9, 9, UnitKind.Militia, hasAttack: true);
        Armee(w, 2, 0, 0);
        var army = new ArmyCommander(AiProfile.Defense);
        Run(army, w, new AiContext(1), 2);
        Assert.DoesNotContain(w.Log, l => l.StartsWith($"move({fighter},") || l.StartsWith($"attack({fighter},"));
    }

    [Fact]
    public void Laufbefehl_wird_nicht_jeden_Takt_wiederholt()
    {
        var w = Dorf();
        int id = w.AddSoldier(30, 30, UnitKind.Militia, UnitStateKind.Moving);
        var army = new ArmyCommander(AiProfile.Defense);
        var ctx = new AiContext(1);
        for (int i = 0; i < 10; i++) { ctx.Time += 0.5; army.Tick(w, w, ctx); }   // läuft, kommt nicht an
        Assert.Single(w.Log, l => l.StartsWith($"move({id},"));
    }

    // ------------------------------------------------------------------ Kampf-KI 2026-11-17 (KK1-KK3)

    private static int Cheb((int X, int Y) a, (int X, int Y) b) => Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y));

    /// <summary>Die Einheit schlägt (laut Spiel) gerade auf ein Gebäude ein bzw. kämpft gegen eine Einheit.</summary>
    private static void SetAttack(FakeWorld w, int id, bool building)
    {
        int k = w.AllUnits.FindIndex(u => u.Id == id);
        var u = w.AllUnits[k];
        w.AllUnits[k] = new UnitSnapshot { Id = u.Id, Owner = u.Owner, Kind = u.Kind, X = u.X, Y = u.Y, State = UnitStateKind.Idle,
                                           Health = u.Health, MaxHealth = u.MaxHealth, HasAttack = true, AttacksBuilding = building };
    }

    private static void Versetze(FakeWorld w, int id, int x, int y)
    {
        int k = w.AllUnits.FindIndex(u => u.Id == id);
        var u = w.AllUnits[k];
        w.AllUnits[k] = new UnitSnapshot { Id = u.Id, Owner = u.Owner, Kind = u.Kind, X = x, Y = y, State = UnitStateKind.Idle,
                                           Health = u.Health, MaxHealth = u.MaxHealth };
    }

    /// <summary>Eine Welle (6 Nahkämpfer, 4 Schützen, ein Katapult) belagert das Gebäude bei (50, 50);
    /// danach schlägt jeder der Welle laut Spiel auf das Gebäude ein.</summary>
    private static (ArmyCommander Army, FakeWorld W, AiContext Ctx) Belagernd()
    {
        var w = Dorf();
        Armee(w, 6, 4, 0);
        int kat = w.AddSoldier(10, 10, UnitKind.Catapult);
        w.AddSoldier(11, 10, UnitKind.Militia);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 400, () => army.Phase == ArmyPhase.Siege);
        Assert.Equal(ArmyPhase.Siege, army.Phase);
        Run(army, w, ctx, 0.5);
        Assert.Contains(kat, army.Wave);
        foreach (int id in army.Wave) SetAttack(w, id, building: true);
        return (army, w, ctx);
    }

    [Fact]
    public void Gebaeudeschlaeger_wehren_sich_nur_gegen_nahe_Feinde_Vertrag()
    {
        UnitSnapshot S(UnitKind k, bool building = true, bool attack = true)
            => new() { Id = 1, Kind = k, X = 10, Y = 10, State = UnitStateKind.Idle, HasAttack = attack, AttacksBuilding = building };
        var miliz2 = new EnemyInfo(12, 9, IsBuilding: false, Health: 40, Kind: UnitKind.Militia);     // Abstand 2
        var miliz3 = new EnemyInfo(13, 10, IsBuilding: false, Health: 40, Kind: UnitKind.Militia);    // Abstand 3
        var reiter1 = new EnemyInfo(9, 9, IsBuilding: false, Health: 45, Kind: UnitKind.Cavalry);     // Abstand 1
        var dorf1 = new EnemyInfo(11, 10, IsBuilding: false, Health: 25, Kind: UnitKind.Villager);    // Abstand 1
        var unbekannt2 = new EnemyInfo(10, 12, IsBuilding: false, Health: 40);                        // Art null, Abstand 2
        var haus1 = new EnemyInfo(11, 11, IsBuilding: true, Health: 300);                             // Abstand 1
        var bogen6 = new EnemyInfo(16, 4, IsBuilding: false, Health: 30, Kind: UnitKind.Archer);      // Abstand 6
        var dorf6 = new EnemyInfo(4, 13, IsBuilding: false, Health: 25, Kind: UnitKind.Villager);     // Abstand 6
        var bogen7 = new EnemyInfo(17, 10, IsBuilding: false, Health: 30, Kind: UnitKind.Archer);     // Abstand 7

        // Nahkämpfer: nur Soldaten (auch unbekannter Art) höchstens DefendRadius weit
        Assert.Equal(2, ArmyCommander.DefendRadius);
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { miliz2 }));
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { reiter1 }));
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { unbekannt2 }));
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Spearman), new[] { miliz3, miliz2 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { miliz3 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { dorf1 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { haus1 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia), new[] { dorf1, haus1, miliz3, bogen6 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia), Array.Empty<EnemyInfo>()));
        // wer kein Gebäude angreift: nie (unverändert)
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia, building: false), new[] { miliz2 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Militia, building: false, attack: false), new[] { miliz2 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Archer, building: false), new[] { dorf1 }));
        // Schützen: jede Einheit in ArcherReach, auch Dorfbewohner, kein Gebäude
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Archer), new[] { dorf1 }));
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Archer), new[] { bogen6 }));
        Assert.True(ArmyCommander.DefendsItself(S(UnitKind.Archer), new[] { bogen7, dorf6 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Archer), new[] { bogen7 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Archer), new[] { haus1 }));
        // Reiter und Katapulte: nie
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Cavalry), new[] { miliz2, dorf1, reiter1 }));
        Assert.False(ArmyCommander.DefendsItself(S(UnitKind.Catapult), new[] { miliz2, dorf1, reiter1 }));
    }

    [Fact]
    public void Belagerung_Nahkaempfer_wehrt_sich_gegen_Soldaten_neben_ihm_die_anderen_schlagen_weiter()
    {
        var (army, w, ctx) = Belagernd();
        var wave = army.Wave.Select(id => U(w, id)).ToList();
        var front = wave.Where(u => u.Kind == UnitKind.Militia).ToList();
        var archers = wave.Where(u => u.Kind == UnitKind.Archer).ToList();
        int kat = wave.Single(u => u.Kind == UnitKind.Catapult).Id;
        // eine freie Kachel höchstens 2 neben genau einem Nahkämpfer, mindestens 3 von allen anderen
        // und mehr als KiteDistance von jedem Schützen
        UnitSnapshot? victim = null; (int X, int Y) spot = default;
        foreach (var m in front)
        {
            for (int dy = -2; dy <= 2 && victim is null; dy++)
                for (int dx = -2; dx <= 2 && victim is null; dx++)
                {
                    var p = (m.X + dx, m.Y + dy);
                    if (wave.Any(u => (u.X, u.Y) == p) || p == (50, 50)) continue;
                    if (front.Any(o => o.Id != m.Id && Cheb((o.X, o.Y), p) <= 2)) continue;
                    if (archers.Any(a => Cheb((a.X, a.Y), p) <= ArmyCommander.KiteDistance)) continue;
                    victim = m; spot = p;
                }
            if (victim is not null) break;
        }
        Assert.NotNull(victim);
        w.AddEnemy(new EnemyInfo(spot.X, spot.Y, IsBuilding: false, Health: 40, IsAttackingUs: true, Kind: UnitKind.Militia));

        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        var log = w.Log.Skip(from).ToList();
        Assert.Equal(ArmyPhase.Engage, army.Phase);
        Assert.Contains($"attack({victim!.Id},{spot.X},{spot.Y})", log);
        // die anderen Nahkämpfer schlagen weiter aufs Gebäude ein: kein neuer Befehl
        foreach (var o in front.Where(o => o.Id != victim.Id))
            Assert.DoesNotContain(log, l => l.StartsWith($"attack({o.Id},") || l.StartsWith($"move({o.Id},"));
        // Schützen in Bogenweite schießen auf den Soldaten, die übrigen bleiben am Gebäude
        foreach (var a in archers)
        {
            if (Cheb((a.X, a.Y), spot) <= ArmyCommander.ArcherReach)
                Assert.Contains($"attack({a.Id},{spot.X},{spot.Y})", log);
            else
                Assert.DoesNotContain(log, l => l.StartsWith($"attack({a.Id},") || l.StartsWith($"move({a.Id},"));
        }
        Assert.Contains(archers, a => Cheb((a.X, a.Y), spot) <= ArmyCommander.ArcherReach);
        // das Katapult bleibt am Gebäude
        Assert.DoesNotContain(log, l => l.StartsWith($"attack({kat},") || l.StartsWith($"move({kat},"));
    }

    [Fact]
    public void Belagerung_ein_Dorfbewohner_holt_die_Nahkaempfer_nicht_vom_Gebaeude()
    {
        var (army, w, ctx) = Belagernd();
        var wave = army.Wave.Select(id => U(w, id)).ToList();
        var front = wave.Where(u => u.Kind == UnitKind.Militia).ToList();
        var m = front[0];
        var p = new[] { (m.X + 1, m.Y), (m.X - 1, m.Y), (m.X, m.Y + 1), (m.X, m.Y - 1) }
            .First(q => !wave.Any(u => (u.X, u.Y) == q) && q != (50, 50));
        w.AddEnemy(new EnemyInfo(p.Item1, p.Item2, IsBuilding: false, Health: 25, IsAttackingUs: true, Kind: UnitKind.Villager));

        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        var log = w.Log.Skip(from).ToList();
        foreach (var o in front)
            Assert.DoesNotContain(log, l => l.StartsWith($"attack({o.Id},") || l.StartsWith($"move({o.Id},"));
    }

    [Fact]
    public void Bewachtes_Reiterziel_Vertrag()
    {
        EnemyInfo M(int x, int y, UnitKind? k = UnitKind.Militia) => new(x, y, IsBuilding: false, Health: 40, Kind: k);
        var bogen = new EnemyInfo(20, 20, IsBuilding: false, Health: 30, Kind: UnitKind.Archer);
        Assert.Equal(2, ArmyCommander.GuardRadius);
        Assert.Equal(2, ArmyCommander.GuardLimit);
        Assert.False(ArmyCommander.Guarded(bogen, new[] { bogen }));
        Assert.False(ArmyCommander.Guarded(bogen, Array.Empty<EnemyInfo>()));
        Assert.False(ArmyCommander.Guarded(bogen, new[] { bogen, M(21, 20) }));                       // eine Wache
        Assert.True(ArmyCommander.Guarded(bogen, new[] { bogen, M(21, 20), M(18, 22) }));             // Abstand 1 und 2
        Assert.False(ArmyCommander.Guarded(bogen, new[] { bogen, M(21, 20), M(23, 20) }));            // zweite 3 weit
        Assert.True(ArmyCommander.Guarded(bogen, new[] { bogen, M(21, 20), M(22, 22, UnitKind.Spearman) }));
        Assert.True(ArmyCommander.Guarded(bogen, new[] { M(19, 19, UnitKind.Cavalry), bogen, M(21, 21) }));
        // keine Wachen: Dorfbewohner, Schützen, Katapulte, unbekannte Art, Gebäude
        Assert.False(ArmyCommander.Guarded(bogen, new[]
        {
            bogen, M(21, 20, UnitKind.Villager), M(19, 20, UnitKind.Villager), M(20, 21, UnitKind.Archer),
            M(20, 19, UnitKind.Catapult), M(21, 21, null), new EnemyInfo(19, 19, IsBuilding: true, Health: 500),
            new EnemyInfo(21, 19, IsBuilding: true, Health: 500),
        }));
        // ein Nahkämpfer zählt sich nicht selbst als Wache
        var miliz = M(30, 30);
        Assert.False(ArmyCommander.Guarded(miliz, new[] { miliz, M(31, 30) }));
        Assert.True(ArmyCommander.Guarded(miliz, new[] { miliz, M(31, 30), M(29, 29) }));
        var reiter = M(40, 10, UnitKind.Cavalry);
        Assert.False(ArmyCommander.Guarded(reiter, new[] { reiter, M(41, 11, UnitKind.Spearman) }));
        // ein Gebäude ist kein Nahkämpfer: nichts abziehen
        var haus = new EnemyInfo(50, 40, IsBuilding: true, Health: 300);
        Assert.True(ArmyCommander.Guarded(haus, new[] { haus, M(51, 40), M(49, 40) }));
        Assert.False(ArmyCommander.Guarded(haus, new[] { haus, M(51, 40) }));
    }

    [Fact]
    public void Reiter_stuermen_kein_bewachtes_Ziel_sondern_ein_freies()
    {
        var (army, w, ctx, ids) = Losgezogen(melee: 5, archers: 5, riders: 3);
        var riders = ids.Skip(10).ToList();
        var a = army.Anchor; var f = army.Facing;
        // Feindtrupp 5 vor dem Anker, ein Schütze direkt dahinter (bewacht)
        int ex = a.X + (int)MathF.Round(f.X * 5), ey = a.Y + (int)MathF.Round(f.Y * 5);
        Trupp(w, ex, ey);
        int bx = ex + (int)MathF.Round(f.X * 1), by = ey + (int)MathF.Round(f.Y * 1);
        w.AddEnemy(new EnemyInfo(bx, by, IsBuilding: false, Health: 30, Kind: UnitKind.Archer));

        // nur Bewachtes in Reichweite: die Reiter bleiben in Bereitschaft
        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(ArmyPhase.Engage, army.Phase);
        Assert.Equal(CavalryMode.Ready, army.WaveCavalry);
        foreach (int id in riders)
            Assert.DoesNotContain(w.Log.Skip(from), l => l.StartsWith($"attack({id},"));

        // ein freier Schütze seitlich, 6 weit: auf den geht der Schwall
        int sx = a.X + (int)MathF.Round(-f.Y * 6), sy = a.Y + (int)MathF.Round(f.X * 6);
        w.AddEnemy(new EnemyInfo(sx, sy, IsBuilding: false, Health: 30, Kind: UnitKind.Archer));
        from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(CavalryMode.Charging, army.WaveCavalry);
        foreach (int id in riders)
        {
            Assert.Contains($"attack({id},{sx},{sy})", w.Log.Skip(from));
            Assert.DoesNotContain($"attack({id},{bx},{by})", w.Log.Skip(from));
        }
    }

    [Fact]
    public void Turmleine_Vertrag()
    {
        var known = new Dictionary<(int X, int Y), EnemyInfo>
        {
            [(40, 40)] = new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true),
            [(60, 40)] = new EnemyInfo(60, 40, IsBuilding: true, Health: 150),   // Lager, 20 vom Stadtzentrum
            [(46, 44)] = new EnemyInfo(46, 44, IsBuilding: true, Health: 150),   // Haus, 6 vom Stadtzentrum
        };
        Assert.Null(ArmyCommander.UnderFire(null, known));
        Assert.Null(ArmyCommander.UnderFire((70, 70), known));   // unbekanntes Ziel
        Assert.Null(ArmyCommander.UnderFire((40, 40), known));   // Ziel ist selbst bewehrt
        Assert.Null(ArmyCommander.UnderFire((46, 44), known));   // Ziel liegt im Feuer
        var fire = ArmyCommander.UnderFire((60, 40), known);
        Assert.NotNull(fire);
        Assert.True(fire!(40, 40));
        Assert.True(fire(31, 49));    // genau ArmedCover (9) weit
        Assert.True(fire(49, 31));
        Assert.False(fire(50, 40));   // 10 weit
        Assert.False(fire(60, 40));
        Assert.False(fire(40, 50));

        // ein zweiter Turm zählt mit; spätere Änderungen an known ändern die Leine nicht
        known[(80, 40)] = new EnemyInfo(80, 40, IsBuilding: true, Health: 500, IsArmed: true);
        var fire2 = ArmyCommander.UnderFire((60, 40), known);
        Assert.NotNull(fire2);
        Assert.True(fire2!(75, 45));
        Assert.True(fire2(40, 40));
        known.Remove((80, 40));
        known.Remove((40, 40));
        Assert.True(fire2(75, 45));
        Assert.True(fire2(40, 40));

        // ohne bewehrte Gebäude: Leine, aber nirgends Feuer
        var still = ArmyCommander.UnderFire((60, 40), new Dictionary<(int X, int Y), EnemyInfo>
        {
            [(60, 40)] = new EnemyInfo(60, 40, IsBuilding: true, Health: 150),
        });
        Assert.NotNull(still);
        Assert.False(still!(60, 40));
        Assert.False(still(0, 0));
    }

    /// <summary>Stadtzentrum (bewehrt) bei (40, 40), Holzfällerlager draußen bei (52, 30): die
    /// Welle (6 Nahkämpfer, 4 Schützen, 2 Reiter) belagert das Lager.</summary>
    private static (ArmyCommander Army, FakeWorld W, AiContext Ctx) LagerNebenStadtzentrum()
    {
        var w = Dorf(enemyBase: false);
        w.AddEnemy(new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true));
        w.AddEnemy(new EnemyInfo(52, 30, IsBuilding: true, Health: 150));
        Armee(w, 7, 4, 2);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 400, () => army.Phase == ArmyPhase.Siege);
        Assert.Equal(ArmyPhase.Siege, army.Phase);
        Assert.Equal((52, 30), army.Target);
        Run(army, w, ctx, 0.5);
        return (army, w, ctx);
    }

    /// <summary>Freie Kachel im Feuer des Stadtzentrums (40, 40), die allen <paramref name="units"/> am nächsten liegt.</summary>
    private static (int X, int Y) NaechsteImFeuer(FakeWorld w, IReadOnlyList<UnitSnapshot> units)
        => Enumerable.Range(31, 19).SelectMany(x => Enumerable.Range(31, 19).Select(y => (X: x, Y: y)))
            .Where(q => Cheb(q, (40, 40)) <= ArmyCommander.ArmedCover && q != (40, 40) && !w.AllUnits.Any(u => (u.X, u.Y) == q))
            .OrderBy(q => units.Max(u => Cheb((u.X, u.Y), q))).ThenBy(q => q.X).ThenBy(q => q.Y).First();

    [Fact]
    public void Turmleine_Reiter_jagen_bei_der_Belagerung_keine_Dorfbewohner_im_Feuer()
    {
        var (army, w, ctx) = LagerNebenStadtzentrum();
        var riders = army.Wave.Select(id => U(w, id)).Where(u => u.Kind == UnitKind.Cavalry).ToList();
        Assert.Equal(2, riders.Count);
        // der Dorfbewohner im Feuer steht jedem Reiter NÄHER als der draußen - ohne Leine nähme jeder ihn
        var inFire = NaechsteImFeuer(w, riders);
        var free = Enumerable.Range(0, 64).SelectMany(x => Enumerable.Range(0, 64).Select(y => (X: x, Y: y)))
            .Where(q => Cheb(q, (40, 40)) > ArmyCommander.ArmedCover && q != (52, 30) && !w.AllUnits.Any(u => (u.X, u.Y) == q)
                        && riders.All(r => Cheb((r.X, r.Y), q) > Cheb((r.X, r.Y), inFire) && Cheb((r.X, r.Y), q) <= ArmyCommander.ChargeRange))
            .OrderBy(q => riders.Max(r => Cheb((r.X, r.Y), q))).ThenBy(q => q.X).ThenBy(q => q.Y).First();
        Assert.All(riders, r => Assert.True(Cheb((r.X, r.Y), inFire) <= ArmyCommander.ChargeRange));
        w.AddEnemy(new EnemyInfo(inFire.X, inFire.Y, IsBuilding: false, Health: 25, Kind: UnitKind.Villager));
        w.AddEnemy(new EnemyInfo(free.X, free.Y, IsBuilding: false, Health: 25, Kind: UnitKind.Villager));
        int from = w.Log.Count;
        Run(army, w, ctx, 2);
        var log = w.Log.Skip(from).ToList();
        foreach (var r in riders)
            Assert.DoesNotContain($"attack({r.Id},{inFire.X},{inFire.Y})", log);
        Assert.Contains(log, l => riders.Any(r => l == $"attack({r.Id},{free.X},{free.Y})"));
    }

    [Fact]
    public void Turmleine_Reiter_jagen_beim_Marsch_keine_Dorfbewohner_im_Feuer()
    {
        var w = Dorf(enemyBase: false);
        w.AddEnemy(new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true));
        w.AddEnemy(new EnemyInfo(52, 30, IsBuilding: true, Health: 150));
        Armee(w, 7, 4, 2);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 30, () => army.Phase == ArmyPhase.March);
        Assert.Equal(ArmyPhase.March, army.Phase);
        // weitermarschieren, bis die Mitte der Reiter höchstens ChargeRange - 2 vom Feuer steht
        List<UnitSnapshot> Riders() => army.Wave.Select(id => U(w, id)).Where(u => u.Kind == UnitKind.Cavalry).ToList();
        UnitSnapshot Mid() { var rs = Riders(); return new UnitSnapshot { X = (int)Math.Round(rs.Average(r => r.X)), Y = (int)Math.Round(rs.Average(r => r.Y)) }; }
        Run(army, w, ctx, 400, () => army.Phase != ArmyPhase.March
            || Cheb((Mid().X, Mid().Y), NaechsteImFeuer(w, new[] { Mid() })) <= ArmyCommander.ChargeRange - 2);
        Assert.Equal(ArmyPhase.March, army.Phase);
        Assert.Equal(CavalryMode.Ready, army.WaveCavalry);
        var riders = Riders();
        var mid = Mid();
        var inFire = NaechsteImFeuer(w, new[] { mid });
        w.AddEnemy(new EnemyInfo(inFire.X, inFire.Y, IsBuilding: false, Health: 25, Kind: UnitKind.Villager));
        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(CavalryMode.Ready, army.WaveCavalry);
        foreach (var r in riders)
            Assert.DoesNotContain(w.Log.Skip(from), l => l.StartsWith($"attack({r.Id},"));

        // ein Dorfbewohner draußen, ebenso weit von der Mitte der Reiter: Schwall
        int d = Cheb((mid.X, mid.Y), inFire);
        var free = Enumerable.Range(0, 64).SelectMany(x => Enumerable.Range(0, 64).Select(y => (X: x, Y: y)))
            .Where(q => Cheb(q, (40, 40)) > ArmyCommander.ArmedCover && !w.AllUnits.Any(u => (u.X, u.Y) == q)
                        && Cheb((mid.X, mid.Y), q) == d && riders.Any(r => Cheb((r.X, r.Y), q) <= ArmyCommander.ChargeRange))
            .OrderBy(q => q.X).ThenBy(q => q.Y).First();
        w.AddEnemy(new EnemyInfo(free.X, free.Y, IsBuilding: false, Health: 25, Kind: UnitKind.Villager));
        from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        Assert.Equal(CavalryMode.Charging, army.WaveCavalry);
        Assert.Contains(w.Log.Skip(from), l => riders.Any(r => l == $"attack({r.Id},{free.X},{free.Y})"));
    }

    [Fact]
    public void Turmleine_wer_im_Feuer_steht_kehrt_zurueck()
    {
        var (army, w, ctx) = LagerNebenStadtzentrum();
        int m = army.Wave.First(id => U(w, id).Kind == UnitKind.Militia);
        Versetze(w, m, 45, 37);   // 5 vom Stadtzentrum
        int from = w.Log.Count;
        ctx.Time += 0.5;
        army.Tick(w, w, ctx);
        var moves = w.Log.Skip(from).Select(l => MoveRe.Match(l)).Where(x => x.Success && int.Parse(x.Groups[1].Value) == m)
                     .Select(x => (int.Parse(x.Groups[2].Value), int.Parse(x.Groups[3].Value))).ToList();
        Assert.NotEmpty(moves);
        Assert.All(moves, p => Assert.True(Cheb(p, (40, 40)) > ArmyCommander.ArmedCover, $"Rückweg nach {p} liegt im Feuer"));
    }

    [Fact]
    public void Turmleine_Feinde_im_Feuer_loesen_keinen_Kampf_aus_draussen_schon()
    {
        var (army, w, ctx) = LagerNebenStadtzentrum();
        var wave = army.Wave.Select(id => U(w, id)).ToList();
        // eine Kachel im Feuer, höchstens EngageRadius von der Welle, aber keiner der Welle steht im Feuer
        Assert.All(wave, u => Assert.True(Cheb((u.X, u.Y), (40, 40)) > ArmyCommander.ArmedCover, $"{u.Kind} bei ({u.X},{u.Y}) im Feuer"));
        var p = Enumerable.Range(31, 19).SelectMany(x => Enumerable.Range(31, 19).Select(y => (X: x, Y: y)))
            .Where(q => Cheb(q, (40, 40)) <= ArmyCommander.ArmedCover)
            .OrderBy(q => wave.Min(u => Cheb((u.X, u.Y), q))).ThenBy(q => q.X).ThenBy(q => q.Y).First();
        Assert.True(wave.Min(u => Cheb((u.X, u.Y), p)) <= ArmyCommander.EngageRadius);
        w.AddEnemy(new EnemyInfo(p.X, p.Y, IsBuilding: false, Health: 40, IsAttackingUs: true, Kind: UnitKind.Militia));
        w.AddEnemy(new EnemyInfo(p.X, p.Y, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        w.AddEnemy(new EnemyInfo(p.X, p.Y, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
        Run(army, w, ctx, 1);
        Assert.Equal(ArmyPhase.Siege, army.Phase);

        // derselbe Trupp draußen neben der Welle: Kampf
        w.ClearEnemies();
        w.AddEnemy(new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true));
        w.AddEnemy(new EnemyInfo(52, 30, IsBuilding: true, Health: 150));
        Trupp(w, 56, 26);
        Run(army, w, ctx, 0.5);
        Assert.Equal(ArmyPhase.Engage, army.Phase);
    }

    [Fact]
    public void Ohne_Turmleine_wenn_das_Ziel_selbst_im_Feuer_liegt()
    {
        // einziges unbewehrtes Gebäude steht 6 neben dem Stadtzentrum: die Welle muss hinein,
        // die Reiter jagen auch Dorfbewohner im Feuer
        var w = Dorf(enemyBase: false);
        w.AddEnemy(new EnemyInfo(40, 40, IsBuilding: true, Health: 600, IsTownCenter: true, IsArmed: true));
        w.AddEnemy(new EnemyInfo(46, 44, IsBuilding: true, Health: 150));
        Armee(w, 7, 4, 2);
        var army = new ArmyCommander(AiProfile.Sparta);
        var ctx = new AiContext(1);
        Run(army, w, ctx, 400, () => army.Phase == ArmyPhase.Siege);
        Assert.Equal(ArmyPhase.Siege, army.Phase);
        Assert.Equal((46, 44), army.Target);
        var riders = army.Wave.Where(id => U(w, id).Kind == UnitKind.Cavalry).ToList();
        w.AddEnemy(new EnemyInfo(43, 47, IsBuilding: false, Health: 25, Kind: UnitKind.Villager));   // 7 vom Stadtzentrum
        int from = w.Log.Count;
        Run(army, w, ctx, 2);
        Assert.Contains(w.Log.Skip(from), l => riders.Any(id => l == $"attack({id},43,47)"));
    }

    // ------------------------------------------------------------------ Einbindung

    [Fact]
    public void EconomyAi_Sparta_schickt_bei_Bedrohung_keine_Soldaten_ueber_die_Gegenwehr()
    {
        var w = new FakeWorld { PopulationCount = 10, PopulationCapacity = 10, Resources = new ResourceVector(0, 0, 0, 0) };
        w.AddBuilding(BuildingType.TownCenter, 4, 4, complete: true);
        int v = w.AddVillager(7, 7);
        int archer = w.AddSoldier(7, 9, UnitKind.Archer);
        // Feind 2 Kacheln neben dem Schützen, 3 vom Stadtzentrum: Bedrohung
        w.AddEnemy(new EnemyInfo(9, 9, IsBuilding: false, Health: 40, IsAttackingUs: true, Kind: UnitKind.Militia));
        new EconomyAi(AiProfile.Sparta).Tick(w, w, 0.5f, new AiContext(1));
        Assert.Contains($"attack({v},9,9)", w.Log);                // Dorfbewohner wehrt sich
        Assert.Contains($"move({archer},", string.Join(" ", w.Log)); // Schütze weicht aus, statt hinzulaufen
        Assert.DoesNotContain($"attack({archer},9,9)", w.Log);
    }
}
