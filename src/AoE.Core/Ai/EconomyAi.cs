using AoE.Core.Economy;
using AoE.Core.Entities;

namespace AoE.Core.Ai;

/// <summary>
/// Die eingebaute Wirtschaft-KI: ein Regel-Modell, das eine funktionierende
/// AoE-Wirtschaft spielt — Dorfbewohner sammeln, bauen, ausbilden und steigen
/// auf. Dazu: Kasernen, Soldaten, Erkundung und Angriff in Wellen.
/// <see cref="IAi"/> ist die Schnittstelle, an die sich auch eine externe AI
/// (LLM, Skript, Netzwerk) hängt.
///
/// Die Härte steuert ein <see cref="AiProfile"/> (Schwierigkeitsgrad):
/// <b>dasselbe Regelwerk, andere Zahlen</b> — exakt das Prinzip des
/// Originals (gleiche Stellschrauben, je Stufe anders eingestellt). Standard:
/// <see cref="AiProfile.Standard"/>.
///
/// Entscheidungsstrategie (deterministisch, <see cref="ctx"/>.Rng bleibt
/// ungenutzt):
/// <ol>
///   <li>Offene Baustellen füllen — jeder freie Platz (max. 4/Baustelle)
///      bekommt einen freien Dorfbewohner (AssignBuilder).</li>
///   <li>Gebäude planen: Haus mit Polster (vor dem Pop-Lock, beliebig viele),
///      Farm, Lagerräume an der Quelle, Kaserne, Schießstand (Build).</li>
///   <li>Dorfbewohner und Soldaten ausbilden — Soldaten haben Vorrang,
///      solange Pop unter der Grenze und Ressourcen da (TrainVillager,
///      TrainSoldier).</li>
///   <li>Freie Dorfbewohner an Quellen (Gather) — nach der
///      Zeitalter-Soll-Verteilung (Nahrung/Holz/Gold/Stein), nicht in fester
///      Reihenfolge: wer am weitesten unter Soll ist, bekommt den nächsten
///      Arbeiter.</li>
///   <li>Erkundung: Einheiten auf die Front schicken (Move), wenn es eine
///      gibt. Jedes Mal, wenn ein Erkundungsziel wieder frei wird.</li>
///   <li>Angriff in Wellen: ab <see cref="AiProfile.RaidMinSoldiers"/>
///      Soldaten und nach dem Cooldown zieht die Truppe los; ein Rest bleibt
///      als Heimwache. Steht ein Feind im Umkreis des eigenen Dorfes, wird
///      sofort zurückgeschlagen — ohne Mindestgröße und ohne Cooldown.</li>
///   <li>Verteidigung: untätige Dorfbewohner im Radius springen mit ein,
///      große Scharen decken die Soldaten.</li>
///   <li>Zeitalter aufsteigen, sobald Rohstoffe ausreichen (AdvanceAge).</li>
/// </ol>
/// </summary>
public sealed class EconomyAi : IAi
{
    /// <summary>So viel Nahrung braucht die KI pro Ausbildung — muss mit
    /// <c>BuildingRules</c> / <c>Player.VillagerCost</c> im Spiel übereinstimmen.</summary>
    public const int VillagerFoodCost = 25;

    /// <summary>So viel Nahrung muss mindestens im Dorf sein, bevor eine
    /// Farm gebaut wird; darunter wird die Farm vor allem anderen priorisiert
    /// (die Farm kostet Holz, aber liefert Nahrung).</summary>
    public const int FarmMinimumFood = 200;

    /// <summary>So viele Bauarbeiter pro Baustelle, bevor der Ertrag nicht mehr
    /// steigt (4 = 2× eines Arbeiters laut AoE-II-Regel).</summary>
    public const int MaxWorkersPerSite = 4;

    /// <summary>Warteschlangenlänge, bei der die KI aufhört auszubilden.
    /// Bewusst klein (2 statt 12): jeder Platz in der Schlange bindet seine
    /// Nahrung (25 pro Kopf) schon beim Einreihen — eine lange Schlange
    /// frisst die Nahrung auf, die Miliz (60) und Aufstieg (500) brauchen.</summary>
    public const int MaxQueue = 2;

    /// <summary>Radius für die Quellensuche (Kacheln).</summary>
    public const int SourceSearchRadius = 16;

    /// <summary>Radius für die Baustellen-Suche (Kacheln).
    /// 12 statt 8: auf Standard-Maps stehen die zwei Basen in den
    /// diagonalen Ecken (z. B. (3,3) und (60,60)) — auf einer 64×64-Karte
    /// gibt es nur 4 Kacheln zwischen (60,60)…(63,63); mit Radius 12
    /// bleibt genug Spielraum, eine 3×3-Kaserne (Barracks, 175 Holz)
    /// an ein nicht-Blockiertes Feld zu setzen, selbst wenn das Dorf
    /// an der Kartenkante sitzt.
    /// 20 statt 12: seit dem Abstand von einer Kachel zu jedem eigenen
    /// Gebäude (<see cref="TryBuildNear"/>) war der Umkreis 12 in der
    /// Kartenecke nach wenigen Häusern voll; gesucht wird weiter von innen
    /// nach außen.</summary>
    public const int BuildSpotRadius = 20;

    /// <summary>Radius (Kacheln), bis zu dem die KI eine Ressource als
    /// Verankerungspunkt fürs passende Lagergebäude akzeptiert. Darin baut die
    /// KI das Lager direkt an die Quelle — nicht am Stadtzentrum. Ist die
    /// Quelle weiter weg, fällt die KI auf das Stadtzentrum zurück (wie vorher),
    /// weil ein Lager 30 Kacheln vom Dorf entfernt nutzlos wäre.</summary>
    public const int AnchorSearchRadius = 20;

    /// <summary>Maximale Chebyshev-Distanz, auf die (untätige) Dorfbewohner
    /// einen sichtbaren Feind angreifen, statt tatenlos zu stehen. Kleiner als
    /// die eigene Sichtweite der Bedrohung; große Scharen werden von den
    /// Soldaten (Raid) gedeckt, hier schmeißen nur wenige nahe Arbeiter los.</summary>
    public const int DefenseRadius = 8;

    /// <summary>Sekundenfenster: ein Treffer jünger als das wird als
    /// „unter Angriff" gewertet — bis dahin wehren sich automatisch alle
    /// freien Einheiten, danach kehrt die KI zur Ökonomie zurück.
    /// Groß genug für einen vollen Schlagtakt, klein genug, dass der
    /// Angriffszustand nicht ewig hält.</summary>
    public const double UnderAttackWindowSeconds = 6d;

    /// <summary>Radius (Chebyshev) um die eigene Siedlung, in dem ein
    /// sichtbarer feindlicher Bauer/Soldat/gebäude als „Angriff auf das
    /// Dorf" gilt und alle freien Einheiten sofort zurückschlagen. Groß
    /// genug, um die Dorf-Peripherie (Häuser, Farm, Kaserne) abzudecken,
    /// aber klein genug, dass zwei 30-Kacheln-voneinander-entfernte
    /// Dörfer das gegenseitige Nicht-Näherkommen vertragen, ohne dass die
    /// „unter Angriff"-Klausel permanent zuschlagen bleibt.</summary>
    public const int ThreatRadius = 8;

    // ---------------------------------------------------------------------
    // Schwierigkeitsgrad — Stellschrauben, nicht Regelwerk: dieselbe
    // KI, andere Zahlen (siehe <see cref="AiProfile"/>). Die Werte
    // SoldierTarget, RaidMinSoldiers, HomeGuard, RaidCooldownSeconds und
    // DefenseMax sind jetzt hier statt als feste Konstanten, weil sie
    // je Stufe unterschiedlich sind.
    // ---------------------------------------------------------------------
    /// <summary>Der Schwierigkeitsgrad dieser KI (Standard: mittel).</summary>
    public AiProfile Profile { get; }

    public EconomyAi() : this(AiProfile.Standard) { }
    public EconomyAi(AiProfile profile)
        => Profile = profile ?? throw new ArgumentNullException(nameof(profile));

    // ---------------------------------------------------------------------
    // Soll-Verteilung der Sammelarbeit je Zeitalter (Gewichte, nicht
    // Prozentmodelle). Dieselbe Kurve wie im Original: früh viel Nahrung
    // und Holz; ab der Feudalzeit Gold dazu; Stein wächst mit.
    // ---------------------------------------------------------------------
    // Gold schon in der Dunklen Zeit: die Miliz kostet 20 Gold, und ohne
    // Goldsammler bleibt die Kaserne ein leeres Gebäude.
    private static readonly Dictionary<Resource, double> W_DARK = new()
    { [Resource.Food] = 50, [Resource.Wood] = 35, [Resource.Gold] = 15 };
    private static readonly Dictionary<Resource, double> W_FEUDAL = new()
    { [Resource.Food] = 40, [Resource.Wood] = 30, [Resource.Gold] = 20, [Resource.Stone] = 10 };
    private static readonly Dictionary<Resource, double> W_CASTLE = new()
    { [Resource.Food] = 35, [Resource.Wood] = 25, [Resource.Gold] = 25, [Resource.Stone] = 15 };
    private static readonly Dictionary<Resource, double> W_IMPERIAL = new()
    { [Resource.Food] = 30, [Resource.Wood] = 25, [Resource.Gold] = 25, [Resource.Stone] = 20 };

    private static Dictionary<Resource, double> WeightsFor(Age age) => age switch
    {
        Age.Dark     => W_DARK,
        Age.Feudal   => W_FEUDAL,
        Age.Castle   => W_CASTLE,
        Age.Imperial => W_IMPERIAL,
        _            => W_DARK
    };

    /// <summary>Feste Vorrang-Reihenfolge (Nahrung zuerst, dann Holz, Gold,
    /// Stein) als deterministischer Tie-Breaker der Soll-Verteilung.</summary>
    private static readonly Resource[] PriorityOrder =
        { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone };

    /// <summary>Die Ressource, die ein Lager an sich bindet. Mühle → Nahrung,
    /// Holzfällerlager → Holz, Bergbaulager → Stein. null = Gebäude binden an
    /// das Stadtzentrum (Haus, Kaserne, Schießstand, Farm).</summary>
    private static Resource? AnchorResourceFor(BuildingType type) => type switch
    {
        BuildingType.Mill        => Resource.Food,
        BuildingType.LumberCamp  => Resource.Wood,
        BuildingType.MiningCamp  => Resource.Stone,
        _                        => null
    };

    private double _lastRaidTime = -1e9;

    /// <summary>Id des fahrenden Erkunder-Scouts (eigener Dorfbewohner).
    /// Er ist geschützt: <c>FreeWorkers</c> schließt ihn aus, damit er von
    /// Sammeln/Bauen nicht abgezogen wird. Solange <c>VisibleEnemies</c>
    /// leer ist, fährt er zur Kartenmitte Richtung den vermuteten Gegner,
    /// und jeder Schritt deckt neue Kacheln auf. Ist ein Feind sichtbar,
    /// zieht er mit zum Angriff.</summary>
    private int? _scoutId;

    /// <summary>Die Kachel, die der Scout gerade erkunden soll, oder null (K8).</summary>
    private (int X, int Y)? _scoutTarget;

    /// <summary>Ziele, die der Scout nicht erreicht hat: er stand untätig, und sie lagen noch
    /// im Nebel (K8). Bei der Wahl des nächsten Ziels scheiden sie aus.</summary>
    private readonly HashSet<(int X, int Y)> _scoutUnreachable = new();

    public void Tick(IWorldState state, IWorldActions actions, float dt, AiContext ctx)
    {
        ctx.Advance(dt);

        // 0) Scout: der Erkunder ist der einzige Dorfbewohner, den die KI
        //    für die Erkundung „fest einplant". Er sitzt nicht im FreeWorkers-
        //    Pool (sonst stiehlt ihn Sammeln/Bauen) und erkundet stattdessen
        //    die nächste Kachel im Nebel - wo der Gegner steht, weiß die KI
        //    nicht (K8). Sieht sie ein feindliches Gebäude, hält er an.
        RunScout(state, actions, ctx);

        var workers = FreeWorkers(state, _scoutId);

        // 1) Offene Baustellen füllen — die wichtigste erste Aktion,
        //    sonst steht das Dorf an einer halbfertigen Kaserne.
        FillSites(state, actions, workers);

        // 2) Gebäude planen: Haus (Pop-Lock), Farm, Holzlager, dann
        //    Militär (Kaserne, Schießstand) wenn die Wirtschaft trägt.
        PlanBuilding(state, actions, workers, ctx);

        // 3) Soldaten ausbilden — zuerst Armee, dann Dorfbewohner.
        //    Soldaten kosten Pop, aber sind der Grund, warum die KI
        //    aggressiver wirkt.
        TrainSoldiers(state, actions, ctx);
        TrainIfPossible(state, actions, ctx);

        // 4) Freie Dorfbewohner an Quellen — die Ernte bleibt wichtig.
        SendToSources(state, actions, workers);

        // 5) Verteidigung zuerst: ist die Siedlung bedroht — ein sichtbarer
        //    Feind im Umkreis der eigenen Gebäude/Einheiten — schlagen
        //    alle ungebundenen Einheiten (Dorfbewohner UND Soldaten)
        //    sofort zurück, ohne Mindestgröße, ohne Cooldown, ohne
        //    Radius pro Einheit. Der Feind ist bei uns im Dorf:
        //    Verteidigung geht vor Plünderung. Ist die Siedlung
        //    in Ruhe, laufen die alten Pfade: Angriffswelle (Soldaten,
        //    Mindestgröße + Cooldown) und die kleine lokale Defense
        //    (nur nahe, untätige Arbeiter).
        if (SettlementUnderThreat(state, out EnemyInfo threat))
            CounterAttack(state, actions, threat, ctx);
        else
        {
            Raid(state, actions, ctx);
            Defense(state, actions, ctx);
        }

        // 6) Zeitalter — immer am Ende, damit die Aufstiegs-Kosten
        //    nicht im selben Takt wie die Armee ausgebucht werden.
        AgeUpIfPossible(state, actions, ctx);
    }

    // ---------------------------------------------------------------------
    // Schritt 2: Gebäude planen (Haus, Farm, Lager) und Arbeiter schicken
    // ---------------------------------------------------------------------
    /// <summary>
    /// Gebäude planen (Haus, Farm, Lager, Kaserne, Schießstand).
    /// Nutzt den Kontext (ctx) für Logging und deterministischen Rng.
    /// </summary>
    private void PlanBuilding(IWorldState state, IWorldActions actions,
        List<UnitSnapshot> workers, AiContext ctx)
    {
        if (state.TownCenter is null) return;
        if (state.AgeTarget is not null) return;   // Aufstieg blockiert Bauplanung

        // 1. Haus — die wichtigste Investition: es hebt die Populationsgrenze
        //    um 5. Die KI baut es, SOBALD das Polster zur Grenze unter
        //    Profile.HouseHeadroom fällt — nicht erst am Limit (dann ist die
        //    Ausbildung schon stillgelegt), und beliebig oft: die Sperre nur
        //    auf offene Baustellen (sonst „höchstens ein Haus“ → Pop-Lock).
        bool housePending = state.Buildings.Any(b => b.Type == BuildingType.House
                                                     && !b.IsComplete);
        if (!housePending
            && (state.PopulationCapacity - state.PopulationCount) <= Profile.HouseHeadroom
            && state.Resources.Wood >= 25)
        {
            if (TryBuildNear(state, actions, workers, BuildingType.House, workersFor: 2, allowPool: true, anchor: null))
                return;
        }

        // 2. Farm — Nahrung ist die knappste Ressource (Ausbildung kostet
        //    25 pro Kopf); sie baut, wenn die Bank unter 200 sinkt. Gleiche
        //    Regel wie Haus: höchstens eine offene Baustelle, sonst mehrere
        //    Farmen, sooft die Nahrung knapp wird.
        //
        // Die Schwelle für ein neues Feld ist das Maximum aus FarmMinimumFood
        // und — solange nicht die Imperialzeit erreicht ist — den Nahrungskosten
        // des nächsten Zeitalters plus 100: die Nahrung muss für den Aufstieg
        // mit Reserve ankommen, sonst friert die Wirtschaft ein, während
        // Beeren und Wild vergriffen sind.
        //
        // Und: sammelt kein Dorfbewohner Nahrung, darf die Farm einen Bauern
        // aus dem Sammelpool ziehen (allowPool: true) — ohne Nahrungssammler
        // gäbe es nie wieder Nahrung und damit keinen neuen Dorfbewohner.
        // Solange jemand Nahrung sammelt, bleibt es bei freien Arbeitern
        // (allowPool: false), damit die Ernte nicht unterbrochen wird.
        bool farmPending = state.Buildings.Any(b => b.Type == BuildingType.Farm
                                                    && !b.IsComplete);
        int farmThreshold = FarmMinimumFood;
        if (state.Age < Age.Imperial)
        {
            var nextCost = AgeRules.CostOf((Age)((int)state.Age + 1));
            if (nextCost.TryGetValue(Resource.Food, out int nextFood))
                farmThreshold = Math.Max(farmThreshold, nextFood + 100);
        }
        bool anyFoodGatherer = state.Units.Any(u => u.Kind == UnitKind.Villager
                                                    && u.Gathering is { Resource: Resource.Food });
        if (!farmPending
            && state.Resources.Food < farmThreshold
            && state.Resources.Wood >= 60)
        {
            TryBuildNear(state, actions, workers, BuildingType.Farm, workersFor: 1,
                         allowPool: !anyFoodGatherer, anchor: null);
        }

        // 3. Lagerräume — direkt an die Quelle, nicht am Stadtzentrum.
        //    Ein Holzfällerlager ohne Bäume in der Nähe ist wertlos (der
        //    Arbeiter läuft trotzdem zum Wald und zurück); eine Mühle ohne
        //    Nahrung nebenan ebenso. Deshalb verankert die KI jedes Lager
        //    an seiner Ressource: FindSource liefert die Kachel, dort baut
        //    es. Keine Quelle in Reichweite → das Lager wird übergangen
        //    (es gäbe nichts abzulegen), statt sinnlos am TC zu stehen.
        PlanAnchored(state, actions, workers, BuildingType.LumberCamp, workersFor: 2, allowPool: false);
        PlanAnchored(state, actions, workers, BuildingType.Mill,       workersFor: 1, allowPool: false);
        PlanAnchored(state, actions, workers, BuildingType.MiningCamp, workersFor: 1, allowPool: false);

        // 4. Kaserne — die Basis der Aggressivität. Kosten: 175 Holz
        //    (lt. BuildingRules). Am Stadtzentrum: die Soldaten gehören in
        //    den Kampf, nicht ans Lager.
        if (state.Resources.Wood >= 150
            && !state.Buildings.Any(b => b.Type == BuildingType.Barracks))
        {
            if (TryBuildNear(state, actions, workers, BuildingType.Barracks,
                             workersFor: 1, allowPool: true, anchor: null))
                return;
        }

        // 5. Schießstand — Bogenschützen; zweite Stufe nach der Kaserne.
        //    Kosten: 175 Holz.
        if (state.Resources.Wood >= 175
            && state.Buildings.Any(b => b.Type == BuildingType.Barracks && b.IsComplete)
            && !state.Buildings.Any(b => b.Type == BuildingType.ArcheryRange))
        {
            TryBuildNear(state, actions, workers, BuildingType.ArcheryRange,
                         workersFor: 1, allowPool: true, anchor: null);
        }
    }

    /// <summary>
    /// Ein Lager (Mühle, Holzfäller- oder Bergbaulager) an seiner Ressource
    /// ausrichten: zuerst die Quelle suchen (Nahrung/Holz/Stein), dann rund
    /// um sie bauen. Gibt es keine Quelle in <see cref="AnchorSearchRadius"/>
    /// oder ist das Lager schon da/im Bau, passiert nichts — ein Lager ohne
    /// zu speichernde Quelle wäre verschenktes Holz.
    /// </summary>
    private static void PlanAnchored(IWorldState state, IWorldActions actions,
        List<UnitSnapshot> workers, BuildingType type, int workersFor, bool allowPool)
    {
        if (state.Buildings.Any(b => b.Type == type)) return;   // fertig ODER im Bau
        Resource? res = AnchorResourceFor(type);
        if (res is null) return;
        if (state.Resources.Wood < 100) return;                 // Kosten des Lagers

        var tc = state.TownCenter!;
        (int X, int Y)? anchor = state.FindSource(res.Value, tc.X + tc.Width / 2, tc.Y + tc.Height / 2, AnchorSearchRadius);
        if (anchor is null) return;   // nichts in der Nähe abzulegen → kein Lager
        if (!state.IsExplored(anchor.Value.X, anchor.Value.Y)) return;

        TryBuildNear(state, actions, workers, type, workersFor: workersFor, allowPool: allowPool,
                     anchor: (anchor.Value.X, anchor.Value.Y));
    }

    /// <summary>
    /// Der anhaltende Erkundungs-Scout: die KI plant genau einen
    /// Dorfbewohner als fahrenden Erkunder ein. Er sitzt nicht im
    /// FreeWorkers-Pool (sonst würde ihn Sammeln/Bauen abziehen). Der Weg
    /// wird nur über bekannte Kacheln geplant (FindPathKnown), jeder Schritt
    /// durch den Nebel deckt neue Kacheln auf (Explored).
    /// VERTRAG ab der Prüfung, ob der Scout noch lebt (K8, Nutzer 2026-10-11: "Aktuell scheint
    /// der Gegner zu wissen, wo ich bin, das darf so nicht sein") - die KI kennt die Lage des
    /// Gegners nicht und leitet sie auch nicht aus der Kartenform ab; bis K8 fuhr der Scout
    /// geradewegs in die gegenüberliegende Ecke, wohin der Kartengenerator das andere
    /// Stadtzentrum setzt:
    /// - Lebt der Scout nicht mehr, sind _scoutId und _scoutTarget null, und im nächsten Takt
    ///   wird ein neuer bestimmt.
    /// - Sieht die KI ein feindliches Gebäude (VisibleEnemies, IsBuilding), hält der Scout an,
    ///   wo er ist (kein Befehl) - die Soldaten übernehmen (Raid).
    /// - Hat er ein Ziel (_scoutTarget), das noch im Nebel liegt (state.IsExplored false), und
    ///   steht er nicht Idle, ist er unterwegs: kein Befehl.
    /// - Steht er Idle, obwohl sein Ziel noch im Nebel liegt, kommt er nicht hin: das Ziel kommt
    ///   in _scoutUnreachable.
    /// - Sonst (kein Ziel, Ziel erkundet oder unerreichbar) wählt er ein neues:
    ///   _scoutTarget = NextScoutTarget(state, scout); ist es nicht null,
    ///   actions.Move(scout.Id, X, Y). Ist es null, ist alles erkundet: kein Befehl.
    /// </summary>
    private void RunScout(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        var tc = state.TownCenter;
        if (tc is null) return;

        if (_scoutId is null)
        {
            // Der Scout verlässt seine Aufgabe (Sammeln/Bauen) und hält sich
            // dauerhaft in der Ferne. Um ein kleines Dorf nicht auszuhebeln
            // (der einzige freie Bauer bräuchte zum Bauen), reserviert die KI
            // nur einen, wenn es einen ÜBERSCHUSS an freien Bauern gibt.
            int freeCount = state.Units.Count(u =>
                u.Kind == UnitKind.Villager
                && u.State == UnitStateKind.Idle
                && u.Gathering is null
                && u.BuildingId is null);
            if (freeCount < 2) return;

            var candidate = state.Units.FirstOrDefault(u =>
                u.Kind == UnitKind.Villager
                && u.State == UnitStateKind.Idle
                && u.Gathering is null
                && u.BuildingId is null);
            if (candidate is null) return;
            _scoutId = candidate.Id;
        }

        // Scout existiert noch?
        var scout = state.Units.FirstOrDefault(u => u.Id == _scoutId
                                                    && u.State != UnitStateKind.Dead);
        if (scout is null)
        {
            // Der Scout ist tot: Ziel und Id vergessen, im nächsten Takt
            // wird ein neuer bestimmt.
            _scoutId = null;
            _scoutTarget = null;
            return;
        }

        // Sieht die KI ein feindliches Gebäude, hält der Scout an, wo er
        // ist — die Soldaten übernehmen ab hier (Raid).
        if (state.VisibleEnemies().Any(e => e.IsBuilding))
            return;

        // Hat er ein Ziel, das noch im Nebel liegt, und steht er nicht
        // untätig, ist er unterwegs: kein Befehl.
        if (_scoutTarget is { } target
            && !state.IsExplored(target.X, target.Y)
            && scout.State != UnitStateKind.Idle)
            return;

        // Steht er untätig, obwohl sein Ziel noch im Nebel liegt, kommt er
        // nicht hin: das Ziel kommt in die Liste der unerreichbaren.
        if (_scoutTarget is { } stuck
            && !state.IsExplored(stuck.X, stuck.Y)
            && scout.State == UnitStateKind.Idle)
        {
            _scoutUnreachable.Add(stuck);
            _scoutTarget = null;
        }

        // Sonst (kein Ziel, Ziel erkundet oder unerreichbar) wählt er ein
        // neues: die nächste Kachel im Nebel — die Lage des Gegners spielt
        // keine Rolle. Gibt es keine, ist alles erkundet: kein Befehl.
        _scoutTarget = NextScoutTarget(state, scout);
        if (_scoutTarget is { } next)
            actions.Move(scout.Id, next.X, next.Y);
    }

    /// <summary>
    /// Das nächste Erkundungsziel des Scouts (K8).
    /// VERTRAG: unter allen Kacheln der Karte (x von 0 bis state.Width - 1, y von 0 bis
    /// state.Height - 1), die noch im Nebel liegen (state.IsExplored false) und nicht in
    /// _scoutUnreachable stehen, die dem Scout am nächsten: kleinstes
    /// (x - scout.X)² + (y - scout.Y)², bei Gleichstand die mit dem kleineren y, dann dem
    /// kleineren x. Null, wenn es keine gibt. Die Lage des Gegners spielt keine Rolle - so
    /// erkundet der Scout vom Dorf aus nach außen.
    /// </summary>
    private (int X, int Y)? NextScoutTarget(IWorldState state, UnitSnapshot scout)
    {
        (int X, int Y)? best = null;
        long bestDist = long.MaxValue;
        // Alle Kacheln der Karte durchgehen: äußere Schleife über y, innere
        // über x. Nur ein echt kleinerer Abstand ersetzt das bisher Beste —
        // bei Gleichstand bleibt die zuerst gefundene Kachel (kleineres y,
        // dann kleineres x).
        for (int y = 0; y < state.Height; y++)
        {
            for (int x = 0; x < state.Width; x++)
            {
                if (state.IsExplored(x, y)) continue;          // nur Nebel
                if (_scoutUnreachable.Contains((x, y))) continue; // schon gescheitert
                long dx = x - scout.X;
                long dy = y - scout.Y;
                long dist = dx * dx + dy * dy;
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = (x, y);
                }
            }
        }
        return best;
    }

    /// <summary>
    /// Angriff in Wellen (statt Einzeln-hinrennen):
    /// <list type="bullet">
    ///   <item><b>Dorfverteidigung:</b> stehen Eindringlinge (feindliche
    ///         Einheiten, sonst Gebäude) in
    ///         <see cref="AiProfile.HomeDefenseRadius"/> um das eigene
    ///         Stadtzentrum, greifen ALLE Soldaten den der Mitte am nächsten
    ///         stehenden an — sofort, ohne Heimwache, ohne Mindestgröße und
    ///         ohne Cooldown, und nie ein Ziel außerhalb des Umkreises. Das
    ///         Dorf darf nicht brennen, während die Truppe auf die
    ///         Mindestgröße wartet.</item>
    ///   <item><b>Angriffswelle:</b> ist der Feind in der Fremde, zieht die
    ///         Truppe erst los, wenn <see cref="AiProfile.RaidMinSoldiers"/>
    ///         Soldaten bereit sind UND der Cooldown abgelaufen ist.
    ///         <see cref="AiProfile.HomeGuard"/> Einheiten bleiben als
    ///         Heimwache, die nächste Welle sammelt sich währenddessen neu.</item>
    /// </list>
    /// VERTRAG Ziel der Angriffswelle (K9, Nutzer 2026-10-11: "eventuell sollte der Gegner
    /// nicht direkt das Dorfzentrum angreifen, sondern das erste Gebäude, das er sieht"): das
    /// sichtbare feindliche Gebäude (IsBuilding), das der Truppe am nächsten steht -
    /// Chebyshev-Abstand zu soldiers[soldiers.Count / 2], bei Gleichstand das erste in
    /// visible -, ohne Vorrang für das Stadtzentrum (IsTownCenter spielt keine Rolle).
    /// Seit F1 meidet die Welle bewehrte Gebäude (IsArmed - Wachturm, Stadtzentrum, Burg):
    /// sie zieht auf das nächste unbewehrte; erst wenn nur bewehrte sichtbar sind, auf das
    /// nächste bewehrte. Nur wenn gar kein Gebäude sichtbar ist, die nächste sichtbare
    /// Einheit, ebenso gemessen. Bis K9 ging die Welle geradewegs auf ein sichtbares
    /// Stadtzentrum - und ohne eins auf (0, 0), weil FirstOrDefault auf dem Struct EnemyInfo
    /// default statt null liefert: in die Ecke, wo das Stadtzentrum des Spielers steht.
    /// </summary>
    private void Raid(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        var visible = state.VisibleEnemies().ToList();
        if (visible.Count == 0) return;

        // Soldaten ohne laufenden Auftrag; wer schon sammelt oder baut, bleibt.
        // Wer schon kämpft (HasAttack) bleibt außen vor: die KI befiehlt alle
        // 0,5 s neu, und jeder Befehl setzt den Schlagtakt (2 s Nachladezeit)
        // zurück — so würde ein Soldat nie treffen.
        var soldiers = state.Units
            .Where(u => u.Kind != UnitKind.Villager
                        && u.State != UnitStateKind.Dead
                        && u.Gathering is null
                        && u.BuildingId is null
                        && !u.HasAttack)
            .ToList();
        if (soldiers.Count == 0) return;

        // Dorfverteidigung: Eindringlinge im Umkreis des eigenen Stadtzentrums
        // (Profile.HomeDefenseRadius, Chebyshev) — zuerst feindliche Einheiten,
        // sonst die Gebäude in diesem Umkreis. Gibt es welche, greifen ALLE
        // Soldaten den der Mitte am nächsten stehenden an — ohne Heimwache und
        // nie ein Ziel außerhalb des Umkreises. Das Dorf geht vor der Welle.
        var tc = state.TownCenter;
        if (tc is not null)
        {
            int cx = tc.X + tc.Width / 2, cy = tc.Y + tc.Height / 2;
            var intruders = visible
                .Where(e => !e.IsBuilding
                    && Math.Max(Math.Abs(e.X - cx), Math.Abs(e.Y - cy)) <= Profile.HomeDefenseRadius)
                .ToList();
            if (intruders.Count == 0)
                intruders = visible.Where(e => e.IsBuilding
                    && Math.Max(Math.Abs(e.X - cx), Math.Abs(e.Y - cy)) <= Profile.HomeDefenseRadius)
                    .ToList();
            if (intruders.Count > 0)
            {
                EnemyInfo nearest = intruders[0];
                int bestDist = int.MaxValue;
                foreach (var e in intruders)
                {
                    int d = Math.Max(Math.Abs(e.X - cx), Math.Abs(e.Y - cy));
                    if (d < bestDist) { bestDist = d; nearest = e; }
                }
                foreach (var s in soldiers)
                    actions.Attack(s.Id, nearest.X, nearest.Y);
                _lastRaidTime = ctx.Time;   // Welle neu zählt
                return;
            }
        }

        // Ziel der Angriffswelle (K9): das sichtbare feindliche Gebäude
        // (IsBuilding), das der Truppe am nächsten steht — Chebyshev-Abstand
        // zu soldiers[soldiers.Count / 2], bei Gleichstand das erste in
        // visible. Ohne Vorrang für das Stadtzentrum (IsTownCenter spielt
        // keine Rolle). Seit F1 weicht die Welle schießenden Gebäuden aus
        // (IsArmed — Wachturm, Stadtzentrum, Burg): erst, wenn sonst kein
        // Gebäude sichtbar ist, zieht sie auf das nächste bewehrte. Nur wenn
        // kein Gebäude sichtbar ist, die nächste sichtbare Einheit, ebenso
        // gemessen.
        EnemyInfo? found = null;
        int raidBestDist = int.MaxValue;
        var raidBase = soldiers[soldiers.Count / 2];
        foreach (var e in visible)
        {
            if (!e.IsBuilding || e.IsArmed) continue;
            int d = Math.Max(Math.Abs(e.X - raidBase.X), Math.Abs(e.Y - raidBase.Y));
            if (d < raidBestDist) { raidBestDist = d; found = e; }
        }
        if (found is null)
        {
            // nur noch bewehrte Gebäude sichtbar (F1): das nächste davon
            foreach (var e in visible)
            {
                if (!e.IsBuilding) continue;
                int d = Math.Max(Math.Abs(e.X - raidBase.X), Math.Abs(e.Y - raidBase.Y));
                if (d < raidBestDist) { raidBestDist = d; found = e; }
            }
        }
        if (found is null)
        {
            raidBestDist = int.MaxValue;
            foreach (var e in visible)
            {
                if (e.IsBuilding) continue;
                int d = Math.Max(Math.Abs(e.X - raidBase.X), Math.Abs(e.Y - raidBase.Y));
                if (d < raidBestDist) { raidBestDist = d; found = e; }
            }
        }
        if (found is not { } target) return;

        // Angriffswelle in der Fremde: Cooldown und Mindestgröße.
        if (ctx.Time - _lastRaidTime < Profile.RaidCooldownSeconds) return;
        if (soldiers.Count < Profile.RaidMinSoldiers) return;
        int toRaid = Math.Max(1, soldiers.Count - Profile.HomeGuard);

        for (int i = 0; i < toRaid; i++)
            actions.Attack(soldiers[i].Id, target.X, target.Y);
        _lastRaidTime = ctx.Time;
    }

    /// <summary>
    /// Verteidigung: <b>untätige</b> Dorfbewohner, die einen Feind sehen,
    /// greifen ihn an — einen Dorfbewohner oder ein Gebäude — statt
    /// tatenlos zu stehen. Dorfbewohner haben im AoE einen Angriff
    /// (BaseAttack 3), der Screen lässt einen Befehl auf ein sichtbares
    /// feindliches Ziel automatisch <see cref="IWorldActions.Attack"/> werden.
    ///
    /// Bewusst klein gehalten: nur in <see cref="DefenseRadius"/> und max.
    /// <see cref="Profile.DefenseMax"/> Einheiten, und nur untätige (wer
    /// sammelt, baut oder fährt als Scout bleibt bei seiner Aufgabe). Große
    /// Scharen decken die Soldaten in <see cref="Raid"/> ab.
    /// </summary>
    private void Defense(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        var enemies = state.VisibleEnemies().ToList();
        if (enemies.Count == 0) return;

        var villagers = state.Units
            .Where(u => u.Kind == UnitKind.Villager
                        && u.State == UnitStateKind.Idle
                        && u.Gathering is null
                        && u.BuildingId is null
                        && u.Id != _scoutId)
            .OrderBy(u => u.Id)
            .ToList();

        int sent = 0;
        foreach (var v in villagers)
        {
            if (sent >= Profile.DefenseMax) break;

            // Der in Reichweite nächstgelegene sichtbare Feind.
            EnemyInfo best = enemies[0];
            int bestDist = int.MaxValue;
            foreach (var e in enemies)
            {
                int d = Math.Max(Math.Abs(e.X - v.X), Math.Abs(e.Y - v.Y));
                if (d < bestDist) { bestDist = d; best = e; }
            }
            if (bestDist > DefenseRadius) continue;

            actions.Attack(v.Id, best.X, best.Y);
            sent++;
        }
    }

    // ---------------------------------------------------------------------
    // Verteidigung: „unter Angriff" erkennen und sofort zurückschlagen
    // ---------------------------------------------------------------------
    /// <summary>
    /// Steht die Siedlung unter Feuer? Maßgabe: entweder
    ///   a) ein sichtbarer Feind in Sichtweite des Dorfzentrums
    ///      (ThreatRadius), ODER
    ///   b) die eigene Siedlung hat binnen
    ///      <see cref="UnderAttackWindowSeconds"/> einen Treffer
    ///      kassiert (Einheit oder Gebäude) und es ist ein sichtbarer
    ///      Feind in Reichweite.
    ///
    /// Vor beiden gilt: greift ein sichtbarer feindlicher Soldat (kein
    /// Gebäude) gerade etwas des Owners an (IsAttackingUs), ist die
    /// Siedlung bedroht — egal wie weit er vom Stadtzentrum entfernt
    /// steht; der nächstgelegene davon wird als Bedrohung gemeldet.
    ///
    /// <paramref name="threat"/> ist dann der sichtbare Feind, der
    /// zum Schaden kam; die Methode liefert true.
    ///
    /// Ein nur sichtbarer Feind in der Ferne reicht NICHT — dafür gibt
    /// es Defense (kleine lokale Beule) und Raid (Angriffswelle).
    /// </summary>
    private static bool SettlementUnderThreat(IWorldState state, out EnemyInfo threat)
    {
        threat = default;
        var enemies = state.VisibleEnemies();
        if (enemies.Count == 0) return false;

        int cx, cy;
        if (state.TownCenter is { } tc) { cx = tc.X + tc.Width / 2; cy = tc.Y + tc.Height / 2; }
        else { var u0 = state.Units.FirstOrDefault(); cx = u0.X; cy = u0.Y; }

        // Vor allen anderen Bedingungen: ein sichtbarer feindlicher Soldat
        // (kein Gebäude), der gerade etwas des Owners angreift (IsAttackingUs),
        // macht die Siedlung bedroht — egal wie weit er vom Stadtzentrum
        // entfernt steht (z. B. ein Haus oder Lager weiter draußen).
        EnemyInfo? attacker = null;
        int attackerDist = int.MaxValue;
        foreach (var e in enemies)
        {
            if (e.IsBuilding || !e.IsAttackingUs) continue;
            int d = Math.Max(Math.Abs(e.X - cx), Math.Abs(e.Y - cy));
            if (d < attackerDist) { attackerDist = d; attacker = e; }
        }
        if (attacker is { } a)
        {
            threat = a;
            return true;
        }

        EnemyInfo closest = enemies[0];
        int closestDist = int.MaxValue;
        foreach (var e in enemies)
        {
            int d = Math.Max(Math.Abs(e.X - cx), Math.Abs(e.Y - cy));
            if (d < closestDist) { closestDist = d; closest = e; }
        }

        // Bedingung a: naher sichtbarer Feind — Alarm auch ohne eigenen Treffer,
        // denn "Feind 5 Kacheln vom TC" ist eine Bedrohung, ein Angriff im
        /// Sinne von "ich muss sofort zurückschlagen" — nicht der
        /// "unter Feuer"-Zustand, den man nur über Treffer kennt.
        if (closestDist <= ThreatRadius)
        {
            threat = closest;
            return true;
        }

        // Bedingung b: frischer Schaden + sichtbarer Feind in Reichweite.
        bool freshHit = state.LastDamageAt > 0d
            && state.WorldTime - state.LastDamageAt <= UnderAttackWindowSeconds;
        if (freshHit && closestDist <= Math.Max(ThreatRadius, 6))
        {
            threat = closest;
            return true;
        }

        return false;
    }

    /// <summary>
    /// Sofortige Gegenwehr: alle ungebundenen eigenen Einheiten
    /// (Dorfbewohner UND Soldaten, auch der Scout — im Ernstfall zählt
    /// jeder) greifen die Bedrohung an, ohne Mindestgröße und ohne
    /// Cooldown. Einheiten, die gerade laufen, zuschlagen oder sammeln
    /// (Moving, Returning, Gathering), bleiben ihrem Ziel treu: ein
    /// erneutes Befeehlen würde den Schlagtakt zurücksetzen.
    /// Dorfbewohner haben im Kern BaseAttack > 0 und schlagen mit; der
    /// Screen übersetzt <see cref="IWorldActions.Attack"/> in einen echten
    /// Angriffszustand.
    /// </summary>
    private void CounterAttack(IWorldState state, IWorldActions actions, EnemyInfo threat, AiContext ctx)
    {
        ctx.Log?.Invoke($"VERTEIDIGUNG: Feind bei ({threat.X},{threat.Y}) — "
            + (threat.IsBuilding ? "Gebäude" : "Einheit") + " zurückschlagen");

        // Soldaten (kein Dorfbewohner) kommen in jedem Zustand außer Dead mit —
        // auch wenn sie gerade laufen (Moving): sie sind die eigentliche
        // Gegenwehr. Dorfbewohner wie bisher nur, wenn sie Idle sind. Für alle
        // gilt: nicht sammeln, nicht bauen und nicht schon im Kampf
        // (HasAttack) — ein neuer Befehl würde den Schlagtakt (2 s) zurücksetzen
        // und den laufenden Angriff zerschlagen.
        var free = state.Units.Where(u =>
            u.State != UnitStateKind.Dead
            && (u.Kind != UnitKind.Villager || u.State == UnitStateKind.Idle)
            && u.Gathering is null
            && u.BuildingId is null
            && !u.HasAttack).ToList();
        foreach (var u in free)
            actions.Attack(u.Id, threat.X, threat.Y);
    }

    /// <summary>
    /// Soldaten ausbilden: die Kaserne bildet Milizen, der Schießstand
    /// Bogenschützen. Die KI reihst ein, solange <see cref="SoldierTarget"/>
    /// (im Feld plus in Produktion) noch nicht erreicht sind und die
    /// Bevölkerungsgrenze Platz lässt. Dorfbewohnerbildung bleibt die
    /// Rücklage — Soldaten nur, wenn Pop und Rohstoffe übrig sind.
    /// </summary>
    private void TrainSoldiers(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (state.TownCenter is null) return;
        if (state.AgeTarget is not null) return;              // Aufstieg blockiert
        if (state.PopulationCount >= state.PopulationCapacity) return;

        // Wie viele Soldaten stehen im Feld (alive, nicht Dorfbewohner)?
        int inField = state.Units.Count(u => u.Kind != UnitKind.Villager
                                             && u.State != UnitStateKind.Dead);
        if (inField >= Profile.SoldierTarget) return;

        // Kaserne zuerst (Milizen), dann Schießstand (Bogenschützen).
        var barracks = state.Buildings.FirstOrDefault(b =>
            b.Type == BuildingType.Barracks && b.IsComplete);
        if (barracks is not null)
        {
            actions.TrainSoldier(BuildingType.Barracks, UnitKind.Militia);
            return;
        }
        var range = state.Buildings.FirstOrDefault(b =>
            b.Type == BuildingType.ArcheryRange && b.IsComplete);
        if (range is not null)
            actions.TrainSoldier(BuildingType.ArcheryRange, UnitKind.Archer);
    }

    /// <summary>
    /// Ein Bauplatz im Ring um einen Anker suchen (nahe → weit), die
    /// Baustelle anlegen und <paramref name="workersFor"/> Arbeiter dazu
    /// schicken — freie zuerst, bei Bedarf aufgefüllt aus dem Sammelpool
    /// (nur wenn <paramref name="allowPool"/>). <paramref name="anchor"/>
    /// ist die Quell-Kachel (z. B. der Wald für das Lagelager); ist sie
    /// null, wird um das Stadtzentrum gebaut (Haus, Kaserne, Farm). True,
    /// wenn die Baustelle stand.
    /// </summary>
    private static bool TryBuildNear(IWorldState state, IWorldActions actions,
        List<UnitSnapshot> workers, BuildingType type,
        int workersFor, bool allowPool, (int X, int Y)? anchor)
    {
        int size = BuildingRules.SizeOf(type);
        var tc = state.TownCenter!;

        // Anker: die Quell-Kachel selbst (Gebäude 3×3 groß → der Ring
        // beginnt bei der Kachel direkt daneben) oder die Dorfmitte.
        int cx, cy;
        if (anchor is { } a)
        {
            cx = a.X; cy = a.Y;
        }
        else
        {
            cx = tc.X + tc.Width / 2;
            cy = tc.Y + tc.Height / 2;
        }

        var b0 = BuildSpotRadius;
        int x0 = Math.Max(0, cx - b0);
        int y0 = Math.Max(0, cy - b0);
        int x1 = Math.Min(state.Width - 1, cx + b0);
        int y1 = Math.Min(state.Height - 1, cy + b0);

        for (int r = 1; r <= BuildSpotRadius; r++)
        {
            for (int x = x0; x <= x1; x++)
            {
                for (int y = y0; y <= y1; y++)
                {
                    // Chebyshev-Ring um den Anker
                    int d = Math.Max(Math.Abs(x - cx), Math.Abs(y - cy));
                    if (d != r) continue;

                    if (x + size > state.Width || y + size > state.Height)
                        continue;
                    // Ein Lager soll NEBEN der Quelle stehen, nicht auf ihr:
                    // eine Kachel der Grundfläche über der Quelle wäre nicht
                    // mehr abbaubar. Alle Positionen, die die Anker-Kachel
                    // überdecken, überspringen.
                    if (anchor is { } aa
                        && aa.X >= x && aa.X < x + size
                        && aa.Y >= y && aa.Y < y + size)
                        continue;
                    if (!state.IsExplored(x, y)) continue;
                    if (!state.CanPlace(type, x, y, size)) continue;

                    // Eine Kachel Abstand zu jedem eigenen Gebäude (auch
                    // Baustellen): die Grundfläche um eine Kachel vergrößert
                    // darf kein eigenes Gebäude berühren — sonst mauern Haus
                    // und Kaserne das Stadtzentrum ein, und die Sammler mit
                    // voller Last finden keinen Weg zur Abgabestelle.
                    bool tooClose = state.Buildings.Any(b =>
                        x - 1 < b.X + b.Width && x + size + 1 > b.X
                        && y - 1 < b.Y + b.Height && y + size + 1 > b.Y);
                    if (tooClose) continue;

                    // Arbeiter: zuerst die freien (in workers), dann — falls
                    // nicht genug und erlaubt — sammelnde aus dem Pool.
                    int n = workersFor;
                    var ids = new List<int>();
                    foreach (var w in workers.Take(n))
                        ids.Add(w.Id);
                    if (ids.Count < n && allowPool)
                    {
                        var freeIds = new HashSet<int>(ids);
                        foreach (var u in state.Units
                                   .Where(u => u.Kind == UnitKind.Villager
                                               && u.Gathering is not null
                                               && !freeIds.Contains(u.Id))
                                   .OrderBy(u => u.Id))
                        {
                            if (ids.Count >= n) break;
                            ids.Add(u.Id);
                        }
                    }
                    if (ids.Count == 0) continue;

                    workers.RemoveRange(0, Math.Min(workers.Count, ids.Count));

                    if (actions.Build(type, x, y, ids.ToArray()) is not null)
                        return true;
                    // abgelehnt (Rohstoffe, CanPlace-Spiel-Checks etc.) — weitersuchen
                }
            }
        }
        return false;
    }

    // ---------------------------------------------------------------------
    // Schritt 1: Baustellen füllen
    // ---------------------------------------------------------------------
    private static void FillSites(IWorldState state, IWorldActions actions, List<UnitSnapshot> workers)
    {
        foreach (var site in state.Buildings.Where(b => !b.IsComplete).OrderBy(b => b.Id))
        {
            // Wie viele weitere Arbeiter verträgt die Baustelle?
            int room = MaxWorkersPerSite - Math.Max(1, site.ActiveBuilders);
            if (room <= 0) continue;

            while (room-- > 0 && workers.Count > 0)
            {
                var worker = workers[0];
                actions.AssignBuilder(worker.Id, site.Id);
                workers.RemoveAt(0);   // die Einheit ist nicht mehr „frei"
            }
        }
    }

    // ---------------------------------------------------------------------
    // Schritt 2: Ausbilden
    // ---------------------------------------------------------------------
    private static void TrainIfPossible(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (state.TownCenter is null) return;
        if (state.AgeTarget is not null) return;           // Aufstieg blockiert Ausbildung
        if (state.PopulationCount >= state.PopulationCapacity) return;
        if (state.VillagerTrainingCount >= MaxQueue) return;
        if (state.Resources.Food < VillagerFoodCost) return;
        actions.TrainVillager();
    }

    // ---------------------------------------------------------------------
    // Schritt 4: Freie Dorfbewohner an Quellen schicken
    // ---------------------------------------------------------------------
    /// <summary>
    /// Freie Dorfbewohner an Quellen verteilen — nach der
    /// <b>Zeitalter-Soll-Verteilung</b> (<see cref="WeightsFor"/>), nicht in
    /// fester Reihenfolge. Für jeden freien Arbeiter wird die Ressource
    /// gewählt, bei der die Quote „bereits zugewiesen / Soll-Gewicht"
    /// am kleinsten ist: das ergibt eine proportional verflochtene Belegung
    /// (bei 60/40: Nahrung, Holz, Nahrung, Holz, …) und verhindert, dass
    /// die KI Gold und Stein erst bearbeitet, wenn Nahrung und Holz alle
    /// sind. Ressourcen ohne erreichbare (erforschte) Quelle fallen aus
    /// der Betrachtung heraus — ihre Gewichte werden faktisch auf die
    /// übrigen verteilt. Kein erreichbare Quelle? Der Arbeiter bleibt
    /// untätig.
    /// </summary>
    private static void SendToSources(IWorldState state, IWorldActions actions, List<UnitSnapshot> workers)
    {
        if (workers.Count == 0) return;

        var weights = WeightsFor(state.Age);
        var order = PriorityOrder.Where(r => weights.ContainsKey(r)).ToList();
        if (order.Count == 0) return;

        // Wie viele Arbeiter je Ressource auf diesem Tick schon zugeteilt
        // wurden — dafür wird die Soll-Quote gemessen.
        var assigned = new Dictionary<Resource, int>();
        foreach (var r in order) assigned[r] = 0;

        // Jeder Dorfbewohner, der schon sammelt, zählt bei der Ressource
        // seines Gathering mit — sonst zählte nur, wer in diesem Takt neu
        // zugeteilt wurde: bei einem einzelnen freien Arbeiter hatte jede
        // Ressource die Quote 0, und Nahrung (Vorrang) gewann immer.
        foreach (var u in state.Units)
        {
            if (u.Kind != UnitKind.Villager || u.Gathering is null) continue;
            if (assigned.TryGetValue(u.Gathering.Value.Resource, out int n))
                assigned[u.Gathering.Value.Resource] = n + 1;
        }

        while (workers.Count > 0)
        {
            // Für jeden freien Arbeiter die günstigste Option suchen
            // (kleinste Quote; bei Gleichstand die höhere Vorrang-Ressource —
            // deterministisch, ohne Zufall).
            UnitSnapshot bestWorker = null;
            Resource bestRes = order[0];
            (int X, int Y) bestSrc = (0, 0);
            double bestRatio = double.MaxValue;
            int bestOrder = int.MaxValue;

            foreach (var worker in workers)
            {
                foreach (var r in order)
                {
                    var src = state.FindSource(r, worker.X, worker.Y, SourceSearchRadius);
                    if (src is null) continue;
                    // Die KI darf nur an Quellen arbeiten, die ihr Besitzer
                    // kennt — eine unerforschte Kachel ist für sie nicht
                    // gezielt nutzbar.
                    if (!state.IsExplored(src.Value.X, src.Value.Y)) continue;

                    double ratio = assigned[r] / weights[r];
                    int ordinal = order.IndexOf(r);
                    if (ratio < bestRatio - 1e-9
                        || (Math.Abs(ratio - bestRatio) <= 1e-9 && ordinal < bestOrder))
                    {
                        bestRatio = ratio;
                        bestOrder = ordinal;
                        bestWorker = worker;
                        bestRes = r;
                        bestSrc = (src.Value.X, src.Value.Y);
                    }
                }
            }

            if (bestWorker is null) break;   // nichts erreichbar — Rest bleibt untätig

            assigned[bestRes]++;
            actions.Gather(bestWorker.Id, bestSrc.X, bestSrc.Y);
            workers.Remove(bestWorker);
        }
    }

    // ---------------------------------------------------------------------
    // Schritt 4: Zeitalter aufsteigen
    // ---------------------------------------------------------------------
    /// <summary>
    /// Zeitalter aufsteigen — aber nur <b>spät</b> und nur auf militärischer
    /// Basis. Im Original stellt sich die KI zuerst wehrbereit auf
    /// (Kaserne + Armee) und kauft das Zeitalter erst, wenn die Wirtschaft
    /// den Sprung verschmerzt. Ein früher Aufstieg friert dagegen die
    /// Wirtschaft für Minuten ein — während <c>AgeTarget</c> sind Planung,
    /// Ausbildung und Häuser blockiert, und die 500 Nahrung fehlen dann
    /// für die Miliz: genau dieser Fehler starb die KI früher an ihrer
    /// eigenen Armee (Kaserne fertig, 0 Soldaten, food 15).
    /// <list type="number">
    ///   <item>Kaserne steht (militärische Basis vorhanden);</item>
    ///   <item>mindestens <see cref="AiProfile.RaidMinSoldiers"/> Soldaten
    ///         stehen im Feld — die volle Soll-Armee
    ///         (<see cref="AiProfile.SoldierTarget"/>) muss NICHT stehen,
    ///         denn ohne Gold wächst sie nie, und der Aufstieg würde ewig
    ///         warten;</item>
    ///   <item>die Kosten sind aufführbar UND nach dem Kauf bleiben
    ///         mind. 100 Nahrung (Puffer für weitere Milizen).</item>
    /// </list>
    /// </summary>
    private void AgeUpIfPossible(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (state.AgeTarget is not null) return;     // Aufstieg läuft schon
        if (state.Age >= Age.Imperial) return;       // letztes Zeitalter
        if (state.TownCenter is null) return;

        // Militärische Basis: Kaserne fertig + Angriffsgruppe im Feld.
        if (!state.Buildings.Any(b => b.Type == BuildingType.Barracks && b.IsComplete))
            return;
        int inField = state.Units.Count(u => u.Kind != UnitKind.Villager
                                             && u.State != UnitStateKind.Dead);
        if (inField < Profile.RaidMinSoldiers)
            return;

        Age next = (Age)((int)state.Age + 1);
        var cost = AgeRules.CostOf(next);
        if (!state.Resources.CanAfford(CostOf(cost))) return;
        // Nahrungspuffer: nach dem Kauf mindestens 100 übrig.
        int foodCost = 0;
        foreach (var (res, amt) in cost)
            if (res == Resource.Food) foodCost = amt;
        if (state.Resources.Food < foodCost + 100) return;

        actions.AdvanceAge();
    }

    /// <summary>Kosten eines Aufstiegs aus dem AoE.Ressourcenvektor.</summary>
    private static ResourceVector CostOf(Dictionary<Resource, int> cost)
    {
        int Food = 0, Wood = 0, Gold = 0, Stone = 0;
        foreach (var (res, amt) in cost)
        {
            switch (res)
            {
                case Resource.Food: Food = amt; break;
                case Resource.Wood: Wood = amt; break;
                case Resource.Gold: Gold = amt; break;
                case Resource.Stone: Stone = amt; break;
            }
        }
        return new ResourceVector(Food, Wood, Gold, Stone);
    }

    // ---------------------------------------------------------------------
    // Helfer
    // ---------------------------------------------------------------------
    static List<UnitSnapshot> FreeWorkers(IWorldState state, int? scoutId)
        => state.Units
            .Where(u => u.Kind == UnitKind.Villager
                        && u.State == UnitStateKind.Idle
                        && u.Gathering is null
                        && u.BuildingId is null
                        && u.Id != scoutId)
            .OrderBy(u => u.Id)
            .ToList();
}
