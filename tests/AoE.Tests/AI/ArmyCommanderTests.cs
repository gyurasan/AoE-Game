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
        w.AddEnemy(new EnemyInfo(ex + 2, ey, IsBuilding: false, Health: 40, Kind: UnitKind.Militia));
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
