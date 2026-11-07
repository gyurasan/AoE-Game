namespace AoE.Core.Ai;

/// <summary>Abschnitt der Angriffswelle des <see cref="ArmyCommander"/>.</summary>
public enum ArmyPhase
{
    /// <summary>Keine Welle unterwegs: alle Soldaten stehen in Aufstellung am Sammelpunkt vor dem Dorf.</summary>
    Gather,
    /// <summary>Die Welle marschiert in Aufstellung auf ihr Ziel, Schritt für Schritt.</summary>
    March,
    /// <summary>Feinde in der Nähe der Welle: Kampf nach Rollen.</summary>
    Engage,
    /// <summary>Die Welle steht vor ihrem Zielgebäude und reißt es ein.</summary>
    Siege,
    /// <summary>Zu viele Verluste: die Welle zieht sich zum Sammelpunkt zurück.</summary>
    Retreat,
}

/// <summary>Abschnitt im Kreislauf der Reiter: angreifen, zurückziehen, sammeln.</summary>
public enum CavalryMode
{
    /// <summary>Bereit für den nächsten Schwall.</summary>
    Ready,
    /// <summary>Im Angriff („im Schwall").</summary>
    Charging,
    /// <summary>Zurückgezogen hinter die eigene Aufstellung, sammeln sich für den nächsten Schwall.</summary>
    Returning,
}

/// <summary>
/// Führt die Armee in Aufstellung (KI-Stufen Defense und This is Sparta, Nutzer 2026-11-07:
/// „Soldaten greifen in Formation an. Bogenschützen hinten, Soldaten vorne mit 1 Kachel Abstand
/// zueinander. Hier werden die Fähigkeiten der Soldaten berücksichtigt, weshalb die Bogenschützen
/// hinten sind und die Fußsoldaten von hinten verteidigen. Berittene Soldaten greifen im Schwall
/// an und ziehen sich wieder zurück. Und nicht vergessen, das ganze immer in Formation!").
///
/// Zwei Gruppen:
/// <list type="bullet">
///   <item><b>Heimgruppe</b> — alle Soldaten, die nicht in einer Welle sind. Sie steht in
///         <see cref="Formation"/> am Sammelpunkt vor dem Dorf (<see cref="RallyDistance"/>
///         Kacheln vom Stadtzentrum in Richtung des Gegners) und schlägt Eindringlinge im
///         <see cref="AiProfile.HomeDefenseRadius"/> nach Rollen zurück (<see cref="Fight"/>).
///         Bei Defense ist das alles: sie greift nie an.</item>
///   <item><b>Angriffswelle</b> (nur mit <see cref="AiProfile.Raids"/>) — sobald genug Soldaten
///         (<see cref="AiProfile.RaidMinSoldiers"/>) in Aufstellung stehen und ein feindliches
///         Gebäude bekannt ist, zieht die Heimgruppe los: Marsch in Schritten von
///         <see cref="MarchStep"/> Kacheln; der nächste Schritt erst, wenn
///         <see cref="InPlaceShare"/> der Welle auf ihren Plätzen steht (so läuft der Verband im
///         Tempo der Langsamsten, die Reiter warten). Feinde in der Nähe → Kampf nach Rollen,
///         danach neu aufstellen und weiter; am Ziel → Belagerung; nach schweren Verlusten →
///         Rückzug.</item>
/// </list>
/// Kampf nach Rollen (<see cref="Fight"/>): Nahkämpfer stellen sich den Feinden, die den
/// eigenen Schützen am nächsten sind; Schützen schießen aus der zweiten Reihe und weichen
/// zurück, wenn ein Nahkämpfer an sie herankommt; Reiter greifen gemeinsam Schützen (sonst
/// Dorfbewohner, sonst den nächsten Feind) an und ziehen sich nach <see cref="ChargeSeconds"/>
/// hinter die Aufstellung zurück (<see cref="CavalryMode"/>).
///
/// Wer schon kämpft (<see cref="UnitSnapshot.HasAttack"/>), bekommt keinen neuen Angriffsbefehl —
/// jeder Befehl setzt den Schlagtakt zurück (Lehre aus K3). Laufbefehle auf denselben Platz
/// werden nicht wiederholt, solange die Einheit läuft.
/// </summary>
public sealed class ArmyCommander
{
    /// <summary>Kacheln vom Stadtzentrum zum Sammelpunkt, in Richtung des Gegners.</summary>
    public const int RallyDistance = 6;

    /// <summary>So weit (Kacheln, in Zweierschritten) tiefer und seitlich vom idealen Sammelpunkt sucht
    /// <see cref="ChooseRally"/> nach freierem Feld.</summary>
    public const int RallySearch = 6;

    /// <summary>Der Sammelpunkt wird höchstens so oft neu gewählt (Sekunden), außer die Heimgruppe oder
    /// ihre Blickrichtung ändert sich.</summary>
    public const double RallyRefreshSeconds = 5;

    /// <summary>Kacheln je Marschschritt.</summary>
    public const int MarchStep = 3;

    /// <summary>Spätestens nach so vielen Sekunden geht der Marsch weiter, auch wenn Nachzügler fehlen.</summary>
    public const double StepTimeoutSeconds = 8;

    /// <summary>Spätestens nach so vielen Sekunden Sammeln zieht eine Welle los, auch wenn noch nicht
    /// alle auf ihren Plätzen stehen (verkeilte Einheiten).</summary>
    public const double GatherTimeoutSeconds = 30;

    /// <summary>Dieser Anteil der Gruppe muss auf seinen Plätzen stehen, bevor es weitergeht.</summary>
    public const double InPlaceShare = 0.8;

    /// <summary>„Auf dem Platz": höchstens so viele Kacheln (Chebyshev) daneben.</summary>
    public const int InPlaceTolerance = 1;

    /// <summary>Feinde bis zu so vielen Kacheln (Chebyshev) von einem Soldaten der Welle lösen den Kampf aus.</summary>
    public const int EngageRadius = 8;

    /// <summary>Steht der Anker so nah am Zielgebäude, beginnt die Belagerung.</summary>
    public const int SiegeDistance = 4;

    /// <summary>So lange ohne Feind in der Nähe, dann stellt sich die Gruppe neu auf.</summary>
    public const double CalmSeconds = 3;

    /// <summary>Kommt ein feindlicher Nahkämpfer einem Schützen so nah (Chebyshev), weicht der Schütze aus.</summary>
    public const int KiteDistance = 2;

    /// <summary>So viele Kacheln weicht ein Schütze zurück.</summary>
    public const int KiteStep = 3;

    /// <summary>Ein Schütze weicht höchstens alle so viele Sekunden zurück (sonst schießt er nie).</summary>
    public const double KiteCooldownSeconds = 2.5;

    /// <summary>Schützen nehmen Feinde bis zu so vielen Kacheln ins Visier (Bogenvorlage 4 plus Anlauf).</summary>
    public const int ArcherReach = 6;

    /// <summary>Ist nichts in <see cref="ArcherReach"/>, rücken Schützen auf Feinde bis zu so vielen Kacheln
    /// nach: das Spiel hält sie dabei auf Bogenvorlage (AttackStandPoint), sie laufen also nicht in den
    /// Nahkampf, sondern bleiben hinter ihren Nahkämpfern.</summary>
    public const int ArcherSupport = 12;

    /// <summary>Dauer eines Reiterangriffs, danach Rückzug.</summary>
    public const double ChargeSeconds = 6;

    /// <summary>So lange sammeln sich die Reiter hinter der Aufstellung bis zum nächsten Schwall.</summary>
    public const double RestSeconds = 5;

    /// <summary>Reiter greifen Ziele bis zu so vielen Kacheln (Chebyshev) von ihrer Mitte an.</summary>
    public const int ChargeRange = 12;

    /// <summary>So weit hinter dem Anker sammeln sich die Reiter nach einem Angriff — außerhalb der
    /// Sichtweite der Späher (7), sonst zöge sie die automatische Gegenwehr gleich wieder in den Kampf.</summary>
    public const int CavalryFallback = 9;

    /// <summary>Unter diesem Anteil der Startstärke zieht sich die Welle zurück.</summary>
    public const double RetreatShare = 0.35;

    /// <summary>Denselben Laufbefehl frühestens nach so vielen Sekunden wiederholen (Einheit stand still).</summary>
    public const double ReorderSeconds = 3;

    /// <summary>Verteidiger der Heimgruppe: Wellensoldaten näher als so viele Kacheln am Dorf helfen mit.</summary>
    public const int HomeHelpRadius = 20;

    /// <summary>So viele bewaffnete Feinde im Dorf (<see cref="AiProfile.HomeDefenseRadius"/>) rufen die
    /// Angriffswelle zurück.</summary>
    public const int HomeRecall = 3;

    /// <summary>Leine der Heimgruppe: weiter als 2 × <see cref="AiProfile.HomeDefenseRadius"/> plus so viele
    /// Kacheln vom Stadtzentrum kehrt ein Soldat der Heimgruppe zum Sammelpunkt um, auch mitten im Kampf.</summary>
    public const int LeashSlack = 4;

    /// <summary>Schussweite bewehrter Gebäude ab ihrer Mitte (Wachturm 8, Stadtzentrum 6, plus eine Kachel):
    /// Ziele darin meidet die Welle, solange es andere gibt - am Stadtzentrum rieben Pfeile und die
    /// zurückschlagenden Dorfbewohner jede Welle auf (gemessen 2026-11-07).</summary>
    public const int ArmedCover = 9;

    /// <summary>Höchstens so viele bewaffnete Feinde bei der marschierenden Welle sind ein Geplänkel: der
    /// Verband marschiert weiter, nur einzelne kümmern sich darum.</summary>
    public const int SkirmishMax = 2;

    /// <summary>Ein Geplänkel gibt es erst ab dieser Wellengröße - eine kleine Welle kämpft geschlossen.</summary>
    public const int SkirmishMinWave = 6;

    /// <summary>Leine der Welle: ein Wellensoldat, der weiter als so viele Kacheln von seinem Platz kämpft,
    /// wird zurückgerufen.</summary>
    public const int WaveLeash = 10;

    /// <summary>Steht eine eigene Einheit so nah (Chebyshev) an der Mitte eines gemerkten Gebäudes und
    /// sieht es nicht, ist es zerstört (ein Stadtzentrum 4×4: wer daneben angreift, steht 3 weit weg).</summary>
    public const int ForgetRadius = 3;

    /// <summary>So lange (Sekunden) darf der Späher nach einem Laufbefehl stehen, bevor sein Ziel als
    /// unerreichbar gilt.</summary>
    public const double ScoutIdleSeconds = 3;

    /// <summary>So viele Soldaten mehr wartet die nächste Welle ab, je abgeschlagener Welle.</summary>
    public const int BeatenExtra = 4;

    /// <summary>Nach so vielen Sekunden ohne neue Niederlage fällt ein Zuschlag wieder weg - sonst
    /// wartete eine KI, deren Bevölkerung das Soll nie erreicht, für immer.</summary>
    public const double BeatenForgetSeconds = 300;

    private readonly AiProfile _profile;

    /// <summary>Der Abschnitt der Angriffswelle.</summary>
    public ArmyPhase Phase { get; private set; } = ArmyPhase.Gather;

    /// <summary>Der Punkt, an dem die Welle steht (Mitte ihres ersten Glieds).</summary>
    public (int X, int Y) Anchor { get; private set; }

    /// <summary>Blickrichtung der Welle.</summary>
    public (float X, float Y) Facing { get; private set; } = (1f, 0f);

    /// <summary>Das Zielgebäude der Welle (Mittelkachel), oder null.</summary>
    public (int X, int Y)? Target { get; private set; }

    /// <summary>Der Sammelpunkt der Heimgruppe aus dem letzten Takt.</summary>
    public (int X, int Y) Rally { get; private set; }

    /// <summary>Blickrichtung der Heimgruppe aus dem letzten Takt.</summary>
    public (float X, float Y) HomeFacing { get; private set; } = (1f, 0f);

    /// <summary>Die Ids der Soldaten in der Angriffswelle.</summary>
    public IReadOnlyCollection<int> Wave => _wave;

    /// <summary>Der Reiterkreislauf der Welle.</summary>
    public CavalryMode WaveCavalry => _waveCav.Mode;

    /// <summary>Der Reiterkreislauf der Heimgruppe.</summary>
    public CavalryMode HomeCavalry => _homeCav.Mode;

    /// <summary>Die zuletzt berechneten Plätze (Heimgruppe und Welle), Id → Kachel — für Prüfungen.</summary>
    public IReadOnlyDictionary<int, (int X, int Y)> Slots => _slots;

    private readonly HashSet<int> _wave = new();
    private int _waveStart;
    private double _stepAt;
    private double _gatherSince = -1;
    private double _lastWaveEnemy = -1e9;
    private double _lastHomeEnemy = -1e9;
    private double _lastRaidEnd = -1e9;
    private int _beaten;
    private double _beatenAt;
    private readonly Dictionary<(int X, int Y), EnemyInfo> _knownBuildings = new();
    private readonly Dictionary<int, ((int X, int Y) Slot, double At)> _ordered = new();
    private readonly Dictionary<int, double> _kitedAt = new();
    private readonly Dictionary<int, (int X, int Y)> _slots = new();
    private readonly CavalryCycle _waveCav = new();
    private readonly CavalryCycle _homeCav = new();
    private double _rallyAt = -1e9;
    private int _rallyCount = -1;
    private (float X, float Y) _rallyFacing;
    private bool _rallySet;

    public ArmyCommander(AiProfile profile)
        => _profile = profile ?? throw new ArgumentNullException(nameof(profile));

    /// <summary>Die bekannten feindlichen Gebäude (zuletzt gesehen), Mittelkachel → Info.</summary>
    public IReadOnlyDictionary<(int X, int Y), EnemyInfo> KnownBuildings => _knownBuildings;

    /// <summary>Abgeschlagene Wellen (Rückzug nach schweren Verlusten), die noch nachwirken.</summary>
    public int Beaten => _beaten;

    /// <summary>So viele Soldaten braucht die Heimgruppe für die nächste Welle:
    /// <see cref="AiProfile.RaidMinSoldiers"/> und je abgeschlagener Welle <see cref="BeatenExtra"/> mehr,
    /// höchstens <see cref="AiProfile.SoldierTarget"/>.</summary>
    public int RequiredWave => Math.Min(Math.Max(_profile.RaidMinSoldiers, _profile.SoldierTarget),
                                        _profile.RaidMinSoldiers + BeatenExtra * _beaten);

    /// <summary>Der Soldat, der gerade nach dem Gegner späht, oder null.</summary>
    public int? Scout => _scoutId;

    private int? _scoutId;
    private (int X, int Y)? _scoutTarget;
    private double _scoutAt;
    private readonly HashSet<(int X, int Y)> _scoutUnreachable = new();

    /// <summary>
    /// Ein Takt der Armeeführung. <paramref name="homeThreat"/>: der Feind, den
    /// <c>EconomyAi.SettlementUnderThreat</c> als Bedrohung meldet (oder null) — ein Angreifer
    /// fern vom Dorf zählt bis zum doppelten <see cref="AiProfile.HomeDefenseRadius"/> als Eindringling.
    /// </summary>
    public void Tick(IWorldState state, IWorldActions actions, AiContext ctx, EnemyInfo? homeThreat = null)
    {
        double now = ctx.Time;
        var soldiers = state.Units
            .Where(u => u.Kind != UnitKind.Villager && u.State != UnitStateKind.Dead)
            .OrderBy(u => u.Id)
            .ToList();
        var alive = soldiers.Select(u => u.Id).ToHashSet();
        _wave.IntersectWith(alive);
        foreach (var id in _ordered.Keys.Where(id => !alive.Contains(id)).ToList()) _ordered.Remove(id);
        foreach (var id in _kitedAt.Keys.Where(id => !alive.Contains(id)).ToList()) _kitedAt.Remove(id);
        _slots.Clear();

        var visible = state.VisibleEnemies();
        RememberBuildings(state, visible);

        var home = Home(state);
        var objective = Objective(home);
        HomeFacing = Direction(home, objective ?? (state.Width / 2, state.Height / 2), HomeFacing);

        var waveUnits = soldiers.Where(u => _wave.Contains(u.Id)).ToList();
        if (waveUnits.Count == 0 && Phase != ArmyPhase.Gather)
        {
            _beaten++;
            _beatenAt = now;
            EndWave(ctx, "Welle aufgerieben");
        }

        // Heimgruppe: alle außerhalb der Welle; bei einem Angriff aufs Dorf helfen nahe Wellensoldaten
        var reserve = soldiers.Where(u => !_wave.Contains(u.Id)).ToList();
        reserve = Explore(state, actions, ctx, reserve, objective, now);
        Rally = ChooseRally(state, home, reserve, now);

        // Leine: wer aus der Heimgruppe Feinden zu weit nachjagt, kehrt zum Sammelpunkt um -
        // die Heimgruppe verteidigt, sie verfolgt nicht über die Karte (gemessen 2026-11-07:
        // Defense-Milizen 71 Kacheln vom eigenen Stadtzentrum).
        int leash = 2 * _profile.HomeDefenseRadius + LeashSlack;
        var strays = reserve.Where(u => Cheb(u.X, u.Y, home.X, home.Y) > leash).ToList();
        foreach (var u in strays)
            MoveTo(actions, u, Rally, now, force: true);
        if (strays.Count > 0)
            reserve = reserve.Except(strays).ToList();

        var intruders = Intruders(state, visible, home, homeThreat);

        // Rückruf: ein feindlicher Trupp im Dorf geht vor - die Welle kehrt heim und hilft auf dem Weg
        // (gemessen 2026-11-07: Normal zerstörte drei Sparta-Gebäude, während die Welle draußen stand)
        int armed = intruders.Count(e => !e.IsBuilding && e.Kind is not UnitKind.Villager);
        if (armed >= HomeRecall && Phase is ArmyPhase.March or ArmyPhase.Engage or ArmyPhase.Siege)
        {
            Phase = ArmyPhase.Retreat;
            ctx.Log?.Invoke($"RÜCKZUG: {armed} Feinde im Dorf, die Welle kehrt heim");
        }

        // wer aus der Welle daheim hilft, ist kein Verlust - der Rückzug zählt alle lebenden der Welle
        int waveAlive = waveUnits.Count;
        if (intruders.Count > 0)
        {
            _lastHomeEnemy = now;
            var defenders = reserve.Concat(waveUnits.Where(u => Cheb(u.X, u.Y, home.X, home.Y) <= HomeHelpRadius)).ToList();
            Fight(state, actions, ctx, defenders, intruders, HomeFacing, Rally, _homeCav);
            waveUnits = waveUnits.Where(u => !defenders.Contains(u)).ToList();
        }
        else if (now - _lastHomeEnemy >= CalmSeconds)
        {
            HoldFormation(state, actions, now, reserve, Rally, HomeFacing);
            _homeCav.Reset();
            TryLaunch(state, ctx, reserve, objective);
        }

        if (Phase != ArmyPhase.Gather && waveUnits.Count > 0)
            RunWave(state, actions, ctx, waveUnits, waveAlive, visible, home);
    }

    // ------------------------------------------------------------------ Welle

    private void TryLaunch(IWorldState state, AiContext ctx, List<UnitSnapshot> reserve, (int X, int Y)? objective)
    {
        if (Phase != ArmyPhase.Gather || !_profile.Raids || objective is not { } target) { _gatherSince = -1; return; }
        // aus Niederlagen lernen: nach einer abgeschlagenen Welle wartet die nächste auf mehr Soldaten
        if (_beaten > 0 && ctx.Time - _beatenAt >= BeatenForgetSeconds) { _beaten--; _beatenAt = ctx.Time; }
        if (reserve.Count < RequiredWave) { _gatherSince = -1; return; }
        if (ctx.Time - _lastRaidEnd < _profile.RaidCooldownSeconds) return;
        if (_gatherSince < 0) _gatherSince = ctx.Time;
        bool ready = InPlace(reserve, _slots) >= InPlaceShare || ctx.Time - _gatherSince >= GatherTimeoutSeconds;
        if (!ready) return;

        // Heimwache: Nahkämpfer vor Schützen, die Reiter ziehen mit (ihr Schwall gehört zur Welle);
        // innerhalb einer Rolle die jüngsten (größte Ids)
        int guardCount = Math.Max(0, _profile.HomeGuard);
        if (reserve.Count - guardCount < 1) return;
        var guard = reserve.OrderBy(u => Formation.RoleOf(u.Kind) switch
                                    {
                                        FormationRole.Front => 0,
                                        FormationRole.Ranged => 1,
                                        _ => 2,
                                    })
                           .ThenByDescending(u => u.Id)
                           .Take(guardCount)
                           .Select(u => u.Id)
                           .ToHashSet();
        _wave.Clear();
        foreach (var u in reserve.Where(u => !guard.Contains(u.Id))) _wave.Add(u.Id);
        _waveStart = _wave.Count;
        Anchor = Rally;
        Target = target;
        Facing = Direction(Anchor, target, HomeFacing);
        Phase = ArmyPhase.March;
        _stepAt = ctx.Time;
        _gatherSince = -1;
        _waveCav.Reset();
        ctx.Log?.Invoke($"WELLE: {_waveStart} Soldaten ziehen in Aufstellung los, Ziel ({target.X},{target.Y})");
    }

    private void RunWave(IWorldState state, IWorldActions actions, AiContext ctx,
                         List<UnitSnapshot> wave, int alive, IReadOnlyList<EnemyInfo> visible, (int X, int Y) home)
    {
        double now = ctx.Time;

        // Rückzug nach schweren Verlusten - zurück an den Sammelpunkt, dort löst sich die Welle auf
        int minimum = Math.Max(2, (int)Math.Ceiling(_waveStart * RetreatShare));
        if (Phase != ArmyPhase.Retreat && alive < minimum)
        {
            Phase = ArmyPhase.Retreat;
            _beaten++;
            _beatenAt = now;
            ctx.Log?.Invoke($"RÜCKZUG: nur noch {alive} von {_waveStart} Soldaten - die nächste Welle wartet auf {RequiredWave}");
        }
        if (Phase == ArmyPhase.Retreat)
        {
            var back = Formation.Slots(Rally, HomeFacing, wave, state.IsWalkable);
            foreach (var u in wave)
            {
                _slots[u.Id] = back[u.Id];
                MoveTo(actions, u, back[u.Id], now, force: true);
            }
            if (wave.All(u => Cheb(u.X, u.Y, Rally.X, Rally.Y) <= 6))
                EndWave(ctx, "Welle zurück am Sammelpunkt");
            return;
        }

        // Bewaffnete Feinde (oder wer uns angreift) in der Nähe: Kampf nach Rollen. Ein einzelner
        // Dorfbewohner hält die Welle nicht auf - den jagen die Reiter im Vorbeiziehen (gemessen
        // 2026-11-07: der Späher des Gegners stoppte die Welle alle 12 s, sie kam kaum voran).
        var near = visible.Where(e => !e.IsBuilding
                                      && (e.Kind != UnitKind.Villager || e.IsAttackingUs)
                                      && wave.Any(u => Cheb(u.X, u.Y, e.X, e.Y) <= (e.IsAttackingUs ? EngageRadius + 4 : EngageRadius)))
                          .ToList();
        // Geplänkel: ein, zwei Feinde beim Marsch halten den Verband nicht auf - Reiter (sonst die
        // zwei nächsten Nahkämpfer je Feind) und Schützen in Reichweite erledigen sie, der Rest
        // marschiert weiter (gemessen 2026-11-07: eine einzelne Miliz zog die Welle 4 Minuten
        // lang quer über die Karte).
        bool skirmish = Phase == ArmyPhase.March && near.Count > 0 && near.Count <= SkirmishMax
                        && wave.Count >= SkirmishMinWave;
        if (near.Count > 0 && !skirmish)
        {
            if (Phase != ArmyPhase.Engage)
                ctx.Log?.Invoke($"KAMPF: {near.Count} Feinde bei der Welle");
            Phase = ArmyPhase.Engage;
            _lastWaveEnemy = now;
            Fight(state, actions, ctx, wave, near, Facing, Anchor, _waveCav);
            return;
        }
        if (Phase == ArmyPhase.Engage)
        {
            if (now - _lastWaveEnemy < CalmSeconds) return;   // Kämpfe ausklingen lassen
            // neu aufstellen, wo die Welle jetzt steht, dann weiter
            Anchor = SnapWalkable(state, Centroid(wave.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Front).DefaultIfEmpty(wave[0])));
            Phase = ArmyPhase.March;
            _stepAt = now;
            _waveCav.Reset();
            ctx.Log?.Invoke($"NEU AUFSTELLEN bei ({Anchor.X},{Anchor.Y})");
        }

        // Ziel prüfen: zerstört (nicht mehr bekannt) → nächstes bekanntes Gebäude, sonst heim
        if (Target is not { } target || !_knownBuildings.ContainsKey(target))
        {
            // ein Ziel fiel: ein Zuschlag weniger
            if (Target is not null && _beaten > 0) _beaten--;
            var next = NearestBuilding(Anchor);
            if (next is null)
            {
                Phase = ArmyPhase.Retreat;
                ctx.Log?.Invoke("WELLE: kein Ziel mehr bekannt, zurück");
                return;
            }
            Target = target = next.Value;
            if (Phase == ArmyPhase.Siege) Phase = ArmyPhase.March;
            _stepAt = now;
            ctx.Log?.Invoke($"WELLE: nächstes Ziel ({target.X},{target.Y})");
        }

        if (Phase == ArmyPhase.Siege)
        {
            Siege(actions, wave, visible, target);
            return;
        }

        // Marsch in Aufstellung; Dorfbewohner (oder die Feinde eines Geplänkels) in Reichweite der
        // Reiter: Schwall auf sie
        var riders = wave.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Flank).ToList();
        var prey = skirmish
            ? near
            : visible.Where(e => !e.IsBuilding && e.Kind == UnitKind.Villager
                                 && riders.Any(r => Cheb(r.X, r.Y, e.X, e.Y) <= ChargeRange)).ToList();
        if (prey.Count > 0 || _waveCav.Mode != CavalryMode.Ready)
            _waveCav.Step(state, actions, ctx, this, riders, prey, Facing, Anchor);
        bool hunting = _waveCav.Mode == CavalryMode.Charging;
        var responders = skirmish ? Skirmish(state, actions, wave, near, riders.Count > 0 && hunting, now) : new HashSet<int>();

        Facing = Direction(Anchor, target, Facing);
        var slots = Formation.Slots(Anchor, Facing, wave, state.IsWalkable);
        foreach (var u in wave)
        {
            _slots[u.Id] = slots[u.Id];
            if (responders.Contains(u.Id) || (hunting && Formation.RoleOf(u.Kind) == FormationRole.Flank)) continue;
            if (u.HasAttack)
            {
                // Leine der Welle: wer einem Feind zu weit vom Verband nachjagt, kehrt auf seinen Platz zurück
                if (!skirmish && Cheb(u.X, u.Y, slots[u.Id].X, slots[u.Id].Y) > WaveLeash)
                    MoveTo(actions, u, slots[u.Id], now, force: true);
                continue;
            }
            MoveTo(actions, u, slots[u.Id], now);
        }
        if (Cheb(Anchor.X, Anchor.Y, target.X, target.Y) <= SiegeDistance)
        {
            Phase = ArmyPhase.Siege;
            ctx.Log?.Invoke($"BELAGERUNG von ({target.X},{target.Y})");
            Siege(actions, wave, visible, target);
            return;
        }
        // wer im Geplänkel kämpft oder als Reiter jagt, zählt für den Schritt nicht mit
        double inPlace = InPlace(wave.Where(u => !responders.Contains(u.Id)
                                                 && (Formation.RoleOf(u.Kind) != FormationRole.Flank || !hunting)).ToList(), slots);
        if (inPlace >= InPlaceShare || now - _stepAt >= StepTimeoutSeconds)
        {
            if (inPlace < InPlaceShare)
                ctx.Log?.Invoke($"MARSCH: Frist abgelaufen, {inPlace:P0} auf dem Platz bei ({Anchor.X},{Anchor.Y})");
            Anchor = Advance(state, Anchor, target);
            Facing = Direction(Anchor, target, Facing);
            _stepAt = now;
        }
    }

    /// <summary>Geplänkel beim Marsch: ohne Reiter im Angriff greifen die zwei nächsten Nahkämpfer je Feind
    /// an, Schützen schießen auf Feinde in <see cref="ArcherReach"/> (ohne nachzurücken). Liefert die Ids,
    /// die in diesem Takt einen Angriffsbefehl bekamen oder schon kämpfen - sie marschieren nicht mit.</summary>
    private HashSet<int> Skirmish(IWorldState state, IWorldActions actions, List<UnitSnapshot> wave, List<EnemyInfo> enemies,
                                  bool cavalryOnIt, double now)
    {
        var busy = wave.Where(u => u.HasAttack && Formation.RoleOf(u.Kind) != FormationRole.Flank).Select(u => u.Id).ToHashSet();
        if (!cavalryOnIt)
            foreach (var e in enemies)
            {
                var fighters = wave.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Front)
                                   .OrderBy(u => Cheb(u.X, u.Y, e.X, e.Y)).ThenBy(u => u.Id).Take(2);
                foreach (var u in fighters)
                {
                    if (!u.HasAttack) actions.Attack(u.Id, e.X, e.Y);
                    busy.Add(u.Id);
                }
            }
        foreach (var u in wave.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Ranged))
        {
            if (Kite(state, actions, u, enemies, Facing, now)) { busy.Add(u.Id); continue; }
            if (u.HasAttack) continue;
            var shot = enemies.Where(e => Cheb(u.X, u.Y, e.X, e.Y) <= ArcherReach)
                              .OrderBy(e => Cheb(u.X, u.Y, e.X, e.Y)).Select(e => (EnemyInfo?)e).FirstOrDefault();
            if (shot is not { } s) continue;
            actions.Attack(u.Id, s.X, s.Y);
            busy.Add(u.Id);
        }
        return busy;
    }

    /// <summary>Belagerung: alle greifen das Zielgebäude an; Reiter jagen sichtbare Dorfbewohner in der Nähe.</summary>
    private void Siege(IWorldActions actions, List<UnitSnapshot> wave, IReadOnlyList<EnemyInfo> visible, (int X, int Y) target)
    {
        var villagers = visible.Where(e => !e.IsBuilding && e.Kind == UnitKind.Villager).ToList();
        foreach (var u in wave)
        {
            if (u.HasAttack) continue;
            if (Formation.RoleOf(u.Kind) == FormationRole.Flank
                && villagers.Where(v => Cheb(u.X, u.Y, v.X, v.Y) <= ChargeRange)
                            .OrderBy(v => Cheb(u.X, u.Y, v.X, v.Y)).FirstOrDefault() is { Kind: UnitKind.Villager } prey)
            {
                actions.Attack(u.Id, prey.X, prey.Y);
                continue;
            }
            actions.Attack(u.Id, target.X, target.Y);
        }
    }

    private void EndWave(AiContext ctx, string why)
    {
        _wave.Clear();
        Phase = ArmyPhase.Gather;
        Target = null;
        _lastRaidEnd = ctx.Time;
        _waveCav.Reset();
        ctx.Log?.Invoke("WELLE ENDE: " + why);
    }

    // ------------------------------------------------------------------ Kampf nach Rollen

    /// <summary>
    /// Kampf nach Rollen. Nahkämpfer: jeder Untätige greift den Feind an, der ihm am nächsten steht,
    /// wobei Feinde nahe den eigenen Schützen 3 Kacheln näher zählen (die Fußsoldaten decken die
    /// Schützen) und jeder schon in diesem Takt gewählte Feind 2 Kacheln weiter (verteilen statt
    /// alle auf einen). Schützen: weichen vor nahen Nahkämpfern zurück, sonst schießen sie auf den
    /// nächsten Feind in Reichweite - wer gerade Eigene angreift, zuerst -, ohne Feind in Reichweite
    /// rücken sie nach (<see cref="ArcherSupport"/>). Reiter: Schwall und Rückzug.
    /// </summary>
    private void Fight(IWorldState state, IWorldActions actions, AiContext ctx, List<UnitSnapshot> units,
                       List<EnemyInfo> enemies, (float X, float Y) facing, (int X, int Y) anchor, CavalryCycle cav)
    {
        double now = ctx.Time;
        var front = units.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Front).ToList();
        var ranged = units.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Ranged).ToList();
        var riders = units.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Flank).ToList();
        var targets = enemies.Where(e => !e.IsBuilding).ToList();
        if (targets.Count == 0) targets = enemies;   // nur Gebäude im Dorf: die reißen alle ein

        // Nahkämpfer
        var load = new Dictionary<(int X, int Y), int>();
        foreach (var u in front)
        {
            if (u.HasAttack) continue;
            EnemyInfo? best = null;
            int bestScore = int.MaxValue;
            foreach (var e in targets)
            {
                int score = Cheb(u.X, u.Y, e.X, e.Y);
                if (ranged.Any(r => Cheb(r.X, r.Y, e.X, e.Y) <= 3)) score -= 3;
                score += 2 * load.GetValueOrDefault((e.X, e.Y));
                if (score < bestScore) { bestScore = score; best = e; }
            }
            if (best is not { } t) continue;
            actions.Attack(u.Id, t.X, t.Y);
            load[(t.X, t.Y)] = load.GetValueOrDefault((t.X, t.Y)) + 1;
        }

        // Schützen
        foreach (var u in ranged)
        {
            if (Kite(state, actions, u, targets, facing, now)) continue;
            if (u.HasAttack) continue;
            var shot = targets.Where(e => Cheb(u.X, u.Y, e.X, e.Y) <= ArcherReach)
                              .OrderByDescending(e => e.IsAttackingUs)
                              .ThenBy(e => Cheb(u.X, u.Y, e.X, e.Y))
                              .Select(e => (EnemyInfo?)e).FirstOrDefault()
                    ?? targets.Where(e => Cheb(u.X, u.Y, e.X, e.Y) <= ArcherSupport)
                              .OrderBy(e => Cheb(u.X, u.Y, e.X, e.Y))
                              .Select(e => (EnemyInfo?)e).FirstOrDefault();
            if (shot is { } s)
                actions.Attack(u.Id, s.X, s.Y);
        }

        // Reiter im Schwall
        cav.Step(state, actions, ctx, this, riders, targets, facing, anchor);
    }

    /// <summary>Ausweichen: steht ein feindlicher Nahkämpfer höchstens <see cref="KiteDistance"/> neben dem
    /// Schützen, weicht er <see cref="KiteStep"/> Kacheln von ihm weg (höchstens alle
    /// <see cref="KiteCooldownSeconds"/>). true: der Schütze ist bedroht (ob er gerade ausweicht oder nicht).</summary>
    private bool Kite(IWorldState state, IWorldActions actions, UnitSnapshot u, List<EnemyInfo> enemies,
                      (float X, float Y) facing, double now)
    {
        var threat = enemies.Where(e => !e.IsBuilding && IsMelee(e) && Cheb(u.X, u.Y, e.X, e.Y) <= KiteDistance)
                            .OrderBy(e => Cheb(u.X, u.Y, e.X, e.Y)).Select(e => (EnemyInfo?)e).FirstOrDefault();
        if (threat is not { } th) return false;
        if (now - _kitedAt.GetValueOrDefault(u.Id, -1e9) >= KiteCooldownSeconds)
        {
            float dx = u.X - th.X, dy = u.Y - th.Y;
            if (dx == 0 && dy == 0) { dx = -facing.X; dy = -facing.Y; }
            float len = MathF.Sqrt(dx * dx + dy * dy);
            var dest = SnapWalkable(state, (u.X + (int)MathF.Round(dx / len * KiteStep),
                                            u.Y + (int)MathF.Round(dy / len * KiteStep)));
            actions.Move(u.Id, dest.X, dest.Y);
            _ordered[u.Id] = (dest, now);
            _kitedAt[u.Id] = now;
        }
        return true;
    }

    private static bool IsMelee(EnemyInfo e)
        => e.Kind is null or UnitKind.Militia or UnitKind.Spearman or UnitKind.Cavalry or UnitKind.Villager;

    /// <summary>Reiterkreislauf: alle Reiter greifen gemeinsam an, ziehen sich zurück, sammeln sich.</summary>
    private sealed class CavalryCycle
    {
        public CavalryMode Mode { get; private set; } = CavalryMode.Ready;
        private double _since;

        public void Reset() { Mode = CavalryMode.Ready; _since = 0; }

        public void Step(IWorldState state, IWorldActions actions, AiContext ctx, ArmyCommander army,
                         List<UnitSnapshot> riders, List<EnemyInfo> enemies, (float X, float Y) facing, (int X, int Y) anchor)
        {
            if (riders.Count == 0) { Reset(); return; }
            double now = ctx.Time;
            var mid = Centroid(riders);
            EnemyInfo? Pick() =>
                enemies.Where(e => Cheb(mid.X, mid.Y, e.X, e.Y) <= ChargeRange)
                       .OrderBy(e => e.Kind is UnitKind.Archer or UnitKind.Catapult ? 0
                                   : e.Kind == UnitKind.Villager ? 1 : 2)
                       .ThenBy(e => Cheb(mid.X, mid.Y, e.X, e.Y))
                       .Select(e => (EnemyInfo?)e).FirstOrDefault();

            switch (Mode)
            {
                case CavalryMode.Ready:
                    if (Pick() is not { } t) return;
                    foreach (var r in riders)
                        actions.Attack(r.Id, t.X, t.Y);
                    Mode = CavalryMode.Charging;
                    _since = now;
                    ctx.Log?.Invoke($"REITER: {riders.Count} im Schwall auf ({t.X},{t.Y})");
                    return;

                case CavalryMode.Charging:
                    if (now - _since >= ChargeSeconds)
                    {
                        // Rückzug hinter die Aufstellung, je Seite verteilt
                        var back = (anchor.X - (int)MathF.Round(facing.X * CavalryFallback),
                                    anchor.Y - (int)MathF.Round(facing.Y * CavalryFallback));
                        var slots = Formation.Slots(back, facing, riders, state.IsWalkable);
                        foreach (var r in riders)
                            army.MoveTo(actions, r, slots[r.Id], now, force: true);
                        Mode = CavalryMode.Returning;
                        _since = now;
                        ctx.Log?.Invoke("REITER: Rückzug hinter die Aufstellung");
                        return;
                    }
                    // wer sein Ziel verloren hat, schließt sich dem nächsten an
                    if (Pick() is { } next)
                        foreach (var r in riders.Where(r => !r.HasAttack))
                            actions.Attack(r.Id, next.X, next.Y);
                    return;

                case CavalryMode.Returning:
                    if (now - _since >= RestSeconds)
                        Mode = CavalryMode.Ready;
                    return;
            }
        }
    }

    // ------------------------------------------------------------------ Aufstellung halten

    private void HoldFormation(IWorldState state, IWorldActions actions, double now,
                               List<UnitSnapshot> units, (int X, int Y) anchor, (float X, float Y) facing)
    {
        if (units.Count == 0) return;
        var slots = Formation.Slots(anchor, facing, units, state.IsWalkable);
        foreach (var u in units)
        {
            _slots[u.Id] = slots[u.Id];
            if (!u.HasAttack) MoveTo(actions, u, slots[u.Id], now);
        }
    }

    /// <summary>Laufbefehl auf einen Platz - nicht, wenn die Einheit schon dort steht, und denselben
    /// Platz erst nach <see cref="ReorderSeconds"/> wieder, solange sie noch läuft.</summary>
    private void MoveTo(IWorldActions actions, UnitSnapshot u, (int X, int Y) slot, double now, bool force = false)
    {
        bool atSlot = u.X == slot.X && u.Y == slot.Y;
        if (atSlot && (!force || !u.HasAttack)) return;
        if (_ordered.TryGetValue(u.Id, out var o) && o.Slot == slot)
        {
            if (now - o.At < ReorderSeconds) return;
            // läuft schon dorthin - wer aber wieder kämpft (die Gegenwehr des Spiels zog ihn in
            // den Kampf zurück), wird erneut gerufen, sonst hielte keine Leine
            if (u.State == UnitStateKind.Moving && !u.HasAttack) return;
        }
        actions.Move(u.Id, slot.X, slot.Y);
        _ordered[u.Id] = (slot, now);
    }

    /// <summary>Anteil der Einheiten (ohne Kämpfende) auf ihren Plätzen; ohne Einheiten 1.</summary>
    private static double InPlace(List<UnitSnapshot> units, IReadOnlyDictionary<int, (int X, int Y)> slots)
    {
        var free = units.Where(u => !u.HasAttack && slots.ContainsKey(u.Id)).ToList();
        if (free.Count == 0) return 1;
        return free.Count(u => Cheb(u.X, u.Y, slots[u.Id].X, slots[u.Id].Y) <= InPlaceTolerance) / (double)free.Count;
    }

    // ------------------------------------------------------------------ Lage

    /// <summary>
    /// Der Sammelpunkt der Heimgruppe: <see cref="RallyDistance"/> Kacheln vom Stadtzentrum in
    /// Blickrichtung - oder, stehen dort Häuser und Bäume im Weg, ein Punkt bis
    /// <see cref="RallySearch"/> Kacheln tiefer oder seitlich, an dem die Aufstellung am wenigsten
    /// verschoben wird (<see cref="FormationCost"/>, dazu die Entfernung vom idealen Punkt). Der
    /// bisherige Punkt bleibt, solange ein neuer nicht um mehr als 2 besser ist - sonst zöge die
    /// Gruppe bei jedem neuen Soldaten um.
    /// </summary>
    private (int X, int Y) ChooseRally(IWorldState state, (int X, int Y) home, List<UnitSnapshot> reserve, double now)
    {
        var ideal = SnapWalkable(state, (home.X + (int)MathF.Round(HomeFacing.X * RallyDistance),
                                         home.Y + (int)MathF.Round(HomeFacing.Y * RallyDistance)));
        if (reserve.Count == 0) { _rallySet = false; return ideal; }
        if (_rallySet && now - _rallyAt < RallyRefreshSeconds && reserve.Count == _rallyCount && _rallyFacing == HomeFacing)
            return Rally;

        float fx = HomeFacing.X, fy = HomeFacing.Y, px = -fy, py = fx;
        (int X, int Y) best = ideal;
        int bestScore = int.MaxValue;
        for (int depth = 0; depth <= RallySearch; depth += 2)
            for (int side = -RallySearch; side <= RallySearch; side += 2)
            {
                var c = (X: Math.Clamp(ideal.X + (int)MathF.Round(fx * depth + px * side), 0, state.Width - 1),
                         Y: Math.Clamp(ideal.Y + (int)MathF.Round(fy * depth + py * side), 0, state.Height - 1));
                if (!state.IsWalkable(c.X, c.Y)) continue;
                int score = 4 * FormationCost(state, c, HomeFacing, reserve) + Math.Abs(side) + depth;
                if (score < bestScore) { bestScore = score; best = c; }
            }
        if (_rallySet && _rallyFacing == HomeFacing && state.IsWalkable(Rally.X, Rally.Y))
        {
            int keep = 4 * FormationCost(state, Rally, HomeFacing, reserve)
                       + Cheb(Rally.X, Rally.Y, ideal.X, ideal.Y);
            if (keep <= bestScore + 2) best = Rally;
        }
        _rallySet = true;
        _rallyAt = now;
        _rallyCount = reserve.Count;
        _rallyFacing = HomeFacing;
        return best;
    }

    /// <summary>
    /// Erkundung durch die Armee: ist die Heimgruppe stark genug für eine Welle
    /// (<see cref="AiProfile.RaidMinSoldiers"/>), kennt aber kein feindliches Gebäude, späht ein Soldat -
    /// ein Reiter, sonst der jüngste Nahkämpfer. Wie der Dorfbewohner-Späher (K8) läuft er zur nächsten
    /// Kachel im Nebel; die Lage des Gegners kennt er nicht. Steht er nach einem Laufbefehl
    /// <see cref="ScoutIdleSeconds"/> still, obwohl sein Ziel noch im Nebel liegt, ist es unerreichbar.
    /// Sobald ein Ziel bekannt ist (oder die Welle zieht), kehrt er in die Heimgruppe zurück.
    /// Liefert die Heimgruppe ohne den Späher.
    /// </summary>
    private List<UnitSnapshot> Explore(IWorldState state, IWorldActions actions, AiContext ctx,
                                       List<UnitSnapshot> reserve, (int X, int Y)? objective, double now)
    {
        bool need = _profile.Raids && objective is null && Phase == ArmyPhase.Gather
                    && reserve.Count >= _profile.RaidMinSoldiers;
        var scout = _scoutId is int id ? reserve.FirstOrDefault(u => u.Id == id) : null;
        if (!need || (_scoutId is not null && scout is null))
        {
            if (_scoutId is not null && need) ctx.Log?.Invoke("ERKUNDUNG: Späher verloren");
            _scoutId = null;
            _scoutTarget = null;
            if (!need) return reserve;
        }
        if (scout is null)
        {
            scout = reserve.Where(u => u.Kind == UnitKind.Cavalry).OrderByDescending(u => u.Id).FirstOrDefault()
                    ?? reserve.Where(u => Formation.RoleOf(u.Kind) == FormationRole.Front).OrderByDescending(u => u.Id).FirstOrDefault()
                    ?? reserve.OrderByDescending(u => u.Id).First();
            _scoutId = scout.Id;
            _scoutTarget = null;
            ctx.Log?.Invoke($"ERKUNDUNG: {scout.Kind} {scout.Id} sucht den Gegner");
        }
        var rest = reserve.Where(u => u.Id != scout.Id).ToList();
        if (_scoutTarget is { } t && !state.IsExplored(t.X, t.Y))
        {
            if (scout.State == UnitStateKind.Moving || now - _scoutAt < ScoutIdleSeconds) return rest;
            _scoutUnreachable.Add(t);
        }
        _scoutTarget = NearestFog(state, (scout.X, scout.Y), _scoutUnreachable);
        if (_scoutTarget is { } next)
        {
            actions.Move(scout.Id, next.X, next.Y);
            _scoutAt = now;
        }
        return rest;
    }

    /// <summary>Die nächste Kachel im Nebel (kleinster quadrierter Abstand, bei Gleichstand kleineres Y,
    /// dann X), ohne <paramref name="skip"/>; null, wenn alles erkundet ist.</summary>
    private static (int X, int Y)? NearestFog(IWorldState state, (int X, int Y) from, HashSet<(int X, int Y)> skip)
    {
        (int X, int Y)? best = null;
        long bestDist = long.MaxValue;
        for (int y = 0; y < state.Height; y++)
            for (int x = 0; x < state.Width; x++)
            {
                if (state.IsExplored(x, y) || skip.Contains((x, y))) continue;
                long dx = x - from.X, dy = y - from.Y, d = dx * dx + dy * dy;
                if (d < bestDist) { bestDist = d; best = (x, y); }
            }
        return best;
    }

    /// <summary>Wie stark Hindernisse die Aufstellung am Punkt <paramref name="anchor"/> verschieben: die
    /// Summe der Chebyshev-Abstände zwischen den Plätzen auf freiem Feld und den tatsächlichen Plätzen,
    /// dazu 3 je Platz mit einem Nachbarn auf der Nachbarkachel.</summary>
    private static int FormationCost(IWorldState state, (int X, int Y) anchor, (float X, float Y) facing, List<UnitSnapshot> units)
    {
        int w = state.Width, h = state.Height;
        var open = Formation.Slots(anchor, facing, units, (x, y) => x >= 0 && y >= 0 && x < w && y < h);
        var real = Formation.Slots(anchor, facing, units, state.IsWalkable);
        int cost = 0;
        var tiles = real.Values.ToHashSet();
        foreach (var (id, p) in real)
        {
            var o = open[id];
            cost += Cheb(p.X, p.Y, o.X, o.Y);
            for (int dy = -1; dy <= 1; dy++)
                for (int dx = -1; dx <= 1; dx++)
                    if ((dx != 0 || dy != 0) && tiles.Contains((p.X + dx, p.Y + dy))) { cost += 3; dy = 2; break; }
        }
        return cost;
    }

    private static (int X, int Y) Home(IWorldState state)
    {
        if (state.TownCenter is { } tc) return (tc.X + tc.Width / 2, tc.Y + tc.Height / 2);
        if (state.Buildings.FirstOrDefault() is { } b) return (b.X + b.Width / 2, b.Y + b.Height / 2);
        if (state.Units.FirstOrDefault() is { } u) return (u.X, u.Y);
        return (state.Width / 2, state.Height / 2);
    }

    /// <summary>Eindringlinge: sichtbare feindliche Einheiten im Heimradius (sonst Gebäude darin), dazu
    /// die gemeldete Bedrohung, wenn sie höchstens doppelt so weit weg ist.</summary>
    private List<EnemyInfo> Intruders(IWorldState state, IReadOnlyList<EnemyInfo> visible, (int X, int Y) home, EnemyInfo? threat)
    {
        int r = _profile.HomeDefenseRadius;
        var list = visible.Where(e => !e.IsBuilding && Cheb(e.X, e.Y, home.X, home.Y) <= r).ToList();
        if (threat is { IsBuilding: false } t && Cheb(t.X, t.Y, home.X, home.Y) <= 2 * r && !list.Contains(t))
            list.Add(t);
        if (list.Count == 0)
            list = visible.Where(e => e.IsBuilding && Cheb(e.X, e.Y, home.X, home.Y) <= r).ToList();
        return list;
    }

    /// <summary>Feindliche Gebäude merken; vergessen, wenn eine eigene Einheit höchstens
    /// <see cref="ForgetRadius"/> Kacheln von der Mitte steht und es nicht mehr sieht (zerstört).</summary>
    private void RememberBuildings(IWorldState state, IReadOnlyList<EnemyInfo> visible)
    {
        foreach (var e in visible.Where(e => e.IsBuilding))
            _knownBuildings[(e.X, e.Y)] = e;
        var seen = visible.Where(e => e.IsBuilding).Select(e => (e.X, e.Y)).ToHashSet();
        foreach (var pos in _knownBuildings.Keys.ToList())
            if (!seen.Contains(pos) && state.Units.Any(u => u.State != UnitStateKind.Dead && Cheb(u.X, u.Y, pos.X, pos.Y) <= ForgetRadius))
                _knownBuildings.Remove(pos);
    }

    /// <summary>Das Ziel einer Welle vom Dorf aus: das nächste bekannte unbewehrte feindliche Gebäude
    /// außerhalb der Schussweite bewehrter Gebäude (<see cref="ArmedCover"/>), sonst das nächste
    /// unbewehrte, sonst das nächste bewehrte (Wachturm, Stadtzentrum) - wie die Wellen seit F1.</summary>
    private (int X, int Y)? Objective((int X, int Y) from) => NearestBuilding(from);

    private (int X, int Y)? NearestBuilding((int X, int Y) from)
    {
        if (_knownBuildings.Count == 0) return null;
        var armed = _knownBuildings.Values.Where(e => e.IsArmed).ToList();
        bool Covered(EnemyInfo e) => armed.Any(a => (a.X != e.X || a.Y != e.Y) && Cheb(a.X, a.Y, e.X, e.Y) <= ArmedCover);
        var best = _knownBuildings.Values
            .OrderBy(e => e.IsArmed)
            .ThenBy(Covered)
            .ThenBy(e => Cheb(e.X, e.Y, from.X, from.Y))
            .ThenBy(e => e.X).ThenBy(e => e.Y)
            .First();
        return (best.X, best.Y);
    }

    private static (int X, int Y) Advance(IWorldState state, (int X, int Y) from, (int X, int Y) to)
    {
        float dx = to.X - from.X, dy = to.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        if (len < 0.5f) return from;
        float step = Math.Min(MarchStep, len);
        return SnapWalkable(state, (from.X + (int)MathF.Round(dx / len * step),
                                    from.Y + (int)MathF.Round(dy / len * step)));
    }

    private static (float X, float Y) Direction((int X, int Y) from, (int X, int Y) to, (float X, float Y) fallback)
    {
        float dx = to.X - from.X, dy = to.Y - from.Y;
        float len = MathF.Sqrt(dx * dx + dy * dy);
        return len < 0.5f ? fallback : (dx / len, dy / len);
    }

    /// <summary>Die nächste begehbare Kachel (Chebyshev-Ringe bis 4), sonst der Punkt selbst; in der Karte.</summary>
    private static (int X, int Y) SnapWalkable(IWorldState state, (int X, int Y) p)
    {
        p = (Math.Clamp(p.X, 0, Math.Max(0, state.Width - 1)), Math.Clamp(p.Y, 0, Math.Max(0, state.Height - 1)));
        if (state.IsWalkable(p.X, p.Y)) return p;
        for (int d = 1; d <= 4; d++)
            for (int dy = -d; dy <= d; dy++)
                for (int dx = -d; dx <= d; dx++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != d) continue;
                    if (state.IsWalkable(p.X + dx, p.Y + dy)) return (p.X + dx, p.Y + dy);
                }
        return p;
    }

    private static (int X, int Y) Centroid(IEnumerable<UnitSnapshot> units)
    {
        var list = units.ToList();
        if (list.Count == 0) return (0, 0);
        return ((int)MathF.Round((float)list.Average(u => u.X)), (int)MathF.Round((float)list.Average(u => u.Y)));
    }

    private static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));
}
