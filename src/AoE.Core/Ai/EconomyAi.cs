using AoE.Core.Economy;
using AoE.Core.Entities;

namespace AoE.Core.Ai;

/// <summary>
/// Die eingebaute Wirtschaft-KI: ein Regel-Modell, das eine funktionierende
/// AoE-Wirtschaft spielt — Dorfbewohner sammeln, bauen, ausbilden und steigen
/// auf. Dazu (seit der Aggressivitäts-Erweiterung) auch: Kasernen, Soldaten,
/// Erkundung und Plünderung. <see cref="IAi"/> ist die Schnittstelle, an
/// die sich auch eine externe AI (LLM, Skript, Netzwerk) hängt.
///
/// Entscheidungsstrategie (deterministisch, <see cref="ctx"/>.Rng bleibt
/// ungenutzt):
/// <ol>
///   <li>Offene Baustellen füllen — jeder freie Platz (max. 4/Baustelle)
///      bekommt einen freien Dorfbewohner (AssignBuilder).</li>
///   <li>Gebäude planen: Haus (Pop-Lock), Farm, Holzlager, Kaserne,
///      Schießstand (Build).</li>
///   <li>Dorfbewohner und Soldaten ausbilden — Soldaten haben Vorrang,
///      solange Pop unter der Grenze und Ressourcen da (TrainVillager,
///      TrainSoldier).</li>
///   <li>Freie Dorfbewohner an Quellen (Gather) — Nahrung vor Holz, dann
///      Stein und Gold.</li>
///   <li>Erkundung: Einheiten auf die Front schicken (Move), wenn es eine
///      gibt. Jedes Mal, wenn ein Erkundungsziel wieder frei wird.</li>
///   <li>Plünderung: 4+ Soldaten, Feind sichtbar, 15s seit letztem Angriff
///      → alle 2+ Soldaten auf den nächsten sichtbaren Feind (Attack).</li>
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

    /// <summary>Warteschlangenlänge, bei der die KI aufhört auszubilden; 12
    /// gibt einen Sicherheitsaufschlag unter <c>TrainingQueue.MAX_LENGTH</c> (15).</summary>
    public const int MaxQueue = 12;

    /// <summary>Radius für die Quellensuche (Kacheln).</summary>
    public const int SourceSearchRadius = 16;

    /// <summary>Radius für die Baustellen-Suche (Kacheln).
    /// 12 statt 8: auf Standard-Maps stehen die zwei Basen in den
    /// diagonalen Ecken (z. B. (3,3) und (60,60)) — auf einer 64×64-Karte
    /// gibt es nur 4 Kacheln zwischen (60,60)…(63,63); mit Radius 12
    /// bleibt genug Spielraum, eine 3×3-Kaserne (Barracks, 175 Holz)
    /// an ein nicht-Blockiertes Feld zu setzen, selbst wenn das Dorf
    /// an der Kartenkante sitzt.</summary>
    public const int BuildSpotRadius = 12;

    /// <summary>So viele Soldaten hält die KI im Bestand, bevor sie neue
    /// ausbildet. Der Wert ist klein, weil Soldaten Pop kosten — die KI
    /// hält die Wirtschaft als Hauptzweck und die Armee als Ergänzung.</summary>
    public const int SoldierTarget = 4;

    /// <summary>So viel Zeit (Sekunden) muss zwischen zwei Plünderungen
    /// liegen, sonst würde das Dorf jede halbe Minute verlassen.</summary>
    public const double RaidCooldownSeconds = 8d;

    /// <summary>So viele Soldaten muss die KI haben, bevor sie angreift.
    /// 1 reicht: im Moment der ersten Sicht eines Feindes ist jede Streitmacht
    /// ein Angriff — die KI ist bewusst aggressiv angesetzt. Der Abzug der
    /// Einheiten zu einem Gebäude ist der eigentliche Raubzug.</summary>
    public const int RaidMinSoldiers = 1;

    /// <summary>So viele Soldaten bleiben beim Plündern als Verteidigung
    /// auf der Heimatseite. Mit 0 würde die KI das eigene Dorf
    /// unverteidigt lassen.</summary>
    public const int HomeGuard = 1;

    private double _lastRaidTime = -1e9;

    /// <summary>Id des fahrenden Erkunder-Scouts (eigener Dorfbewohner).
    /// Er ist geschützt: <c>FreeWorkers</c> schließt ihn aus, damit er von
    /// Sammeln/Bauen nicht abgezogen wird. Solange <c>VisibleEnemies</c>
    /// leer ist, fährt er zur Kartenmitte Richtung den vermuteten Gegner,
    /// und jeder Schritt deckt neue Kacheln auf. Ist ein Feind sichtbar,
    /// zieht er mit zum Angriff.</summary>
    private int? _scoutId;

    public void Tick(IWorldState state, IWorldActions actions, float dt, AiContext ctx)
    {
        ctx.Advance(dt);

        // 0) Scout: der Erkunder ist der einzige Dorfbewohner, den die KI
        //    für die Erkundung „fest einplant". Er sitzt nicht im FreeWorkers-
        //    Pool (sonst stiehlt ihn Sammeln/Bauen), fährt stattdessen
        //    dauerhaft Richtung Kartenmitte — dorthin, wo der Gegner ist.
        //    Solange <c>VisibleEnemies</c> leer ist, wird er nie zu etwas
        //    anderem abkommandiert. Ist ein Feind sichtbar, zieht er mit.
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

        // 5) Plünderung — nur mit genug Soldaten und ohne laufende
        //    Ausbildung (sonst bricht die KI die Kaserne auf).
        Raid(state, actions, ctx);

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
    private static void PlanBuilding(IWorldState state, IWorldActions actions,
        List<UnitSnapshot> workers, AiContext ctx)
    {
        if (state.TownCenter is null) return;
        if (state.AgeTarget is not null) return;   // Aufstieg blockiert Bauplanung

        // 1. Haus — die wichtigste Investition: sie hebt die Populationsgrenze
        //    um 5. Sobald das Dorf an der Grenze hängt, ist die Ausbildung
        //    sonst stillgelegt und die Wirtschaft stirbt ab (Deadlock).
        //    Dann dürfen auch Dorfbewohner abgezweigt werden, die schon
        //    sammeln — das Haus muss fertig werden.
        if (state.PopulationCount >= state.PopulationCapacity
            && !state.Buildings.Any(b => b.Type == BuildingType.House)   // fertig ODER im Bau — kein zweites
            && state.Resources.Wood >= 25)
        {
            if (TryBuildNear(state, actions, workers, BuildingType.House, workersFor: 2, allowPool: true))
                return;
        }

        // 2. Farm — Nahrung ist die knappste Ressource (Ausbildung kostet
        //    25 pro Kopf); sie baut nur, wenn die Bank unter 200 sinkt.
        if (state.Resources.Food < FarmMinimumFood
            && !state.Buildings.Any(b => b.Type == BuildingType.Farm)
            && state.Resources.Wood >= 60)
        {
            TryBuildNear(state, actions, workers, BuildingType.Farm, workersFor: 1, allowPool: false);
        }

        // 3. Holzfällerlager — sobald sich 200 Holz angesammelt haben, kann
        //    das Dorf es sich leisten (100 Lager plus das nächste Haus).
        if (state.Resources.Wood >= 200
            && !state.Buildings.Any(b => b.Type == BuildingType.LumberCamp))
        {
            TryBuildNear(state, actions, workers, BuildingType.LumberCamp, workersFor: 2, allowPool: false);
        }

        // 4. Kaserne — die Basis der Aggressivität. Kosten: 175 Holz
        //    (lt. BuildingRules). Die Kaserne ist das wichtigste Gebäude
        //    nach dem Haus: sie bildet die Miliz, die der KI die Angriffe
        //    liefert. Platzieren wir sie, solange genug Holz im Dorf ist,
        //    und nehmen 2 Arbeiter (1 frei + 1 aus dem Pool), sonst
        //    steht sie nie fertig — und die Kaserne ohne Arbeiter
        //    wird nie fertig.
        if (state.Resources.Wood >= 150
            && !state.Buildings.Any(b => b.Type == BuildingType.Barracks))
        {
            if (TryBuildNear(state, actions, workers, BuildingType.Barracks,
                             workersFor: 1, allowPool: true))
                return;
        }

        // 5. Schießstand — Bogenschützen; zweite Stufe nach der Kaserne.
        //    Kosten: 175 Holz.
        if (state.Resources.Wood >= 175
            && state.Buildings.Any(b => b.Type == BuildingType.Barracks && b.IsComplete)
            && !state.Buildings.Any(b => b.Type == BuildingType.ArcheryRange))
        {
            TryBuildNear(state, actions, workers, BuildingType.ArcheryRange,
                         workersFor: 1, allowPool: true);
        }
    }

    /// <summary>
    /// Der anhaltende Erkundungs-Scout: die KI plant genau einen
    /// Dorfbewohner als fahrenden Erkunder ein. Er sitzt nicht im
    /// FreeWorkers-Pool (sonst würde ihn Sammeln/Bauen abziehen).
    /// Jedes Takt fährt er zur gegenüberliegenden Karteck — der
    /// voraussichtlichen Basis-Position des Gegners. Der Weg wird nur
    /// über bekannte Kacheln geplant (FindPathKnown), jeder Schritt durch
    /// den Nebel deckt eine neue Kachel auf (Explored). Der Scout steht
    /// dort, bis der Gegner sichtbar ist — dann hält er dort, damit die
    /// eigenen Soldaten (Raid) die Basis angreifen können.
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
        if (scout is null) { _scoutId = null; return; }

        // Ziel: die gegenüberliegende Karteck — die voraussichtliche
        // Basis-Position des Gegners. Die KI fährt immer dorthin, bis
        // der Scout in Sichtweite der gegnerischen Gebäude steht.
        int mid = Math.Min(state.Width, state.Height);
        int baseX = tc.X + tc.Width / 2;
        int baseY = tc.Y + tc.Height / 2;
        bool xLow = baseX < mid / 2;
        bool yLow = baseY < mid / 2;
        (int X, int Y) dst = (
            xLow ? state.Width - 3 : 3,
            yLow ? state.Height - 3 : 3);

        // Ist der Gegner (ein Gebäude) sichtbar? Dann ist die Erkundung
        // fast vollständig — der Scout hält in der Nähe und die KI kann
        // mit den Soldaten angreifen.
        bool enemyVisible = state.VisibleEnemies().Any(e => e.IsBuilding);
        int dist = Math.Max(Math.Abs(dst.X - scout.X), Math.Abs(dst.Y - scout.Y));

        if (enemyVisible && dist <= 8)
        {
            // Der Scout ist in Reichweite der gegnerischen Basis —
            // er hält hier. Die Soldaten übernehmen ab hier.
            return;
        }

        if (dist <= 1)
        {
            // Am Ziel, aber noch kein Feindgebäude sichtbar — noch bleiben.
            return;
        }

        actions.Move(scout.Id, dst.X, dst.Y);
    }

    /// <summary>
    /// Plünderung: mit genug Soldaten (mindestens <see cref="RaidMinSoldiers"/>
    /// außer Dienst) und einem sichtbaren Feind zieht die KI die Armee raus.
    /// <b>Einheiten</b> sind die primären Ziele (sie sind die gegnerische
    /// Armee), <b>Gebäude</b> der Fallback, wenn nur Bauwerke sichtbar
    /// sind.
    /// </summary>
    private void Raid(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (ctx.Time - _lastRaidTime < RaidCooldownSeconds) return;
        var visible = state.VisibleEnemies().ToList();
        if (visible.Count == 0) return;

        // Einheiten zuerst (die Armee), dann Gebäude.
        var primary = visible.Where(e => !e.IsBuilding).ToList();
        if (primary.Count == 0)
            primary = visible.Where(e => e.IsBuilding).ToList();
        if (primary.Count == 0) return;

        // Soldaten ohne laufenden Auftrag; wer schon sammelt oder baut, bleibt.
        var soldiers = state.Units
            .Where(u => u.Kind != UnitKind.Villager
                        && u.State != UnitStateKind.Dead
                        && u.Gathering is null
                        && u.BuildingId is null)
            .ToList();
        if (soldiers.Count < RaidMinSoldiers)
        {
            return;
        }
        int toRaid = Math.Max(1, soldiers.Count - HomeGuard);

        // Das nächste sichtbare Ziel (Chebyshev) aus Sicht eines Soldaten
        // mit Basisposition.
        var basePos = soldiers[(soldiers.Count - 1) / 2];
        EnemyInfo target = primary[0];
        int bestDist = int.MaxValue;
        foreach (var e in primary)
        {
            int d = Math.Max(Math.Abs(e.X - basePos.X), Math.Abs(e.Y - basePos.Y));
            if (d < bestDist) { bestDist = d; target = e; }
        }

        for (int i = 0; i < toRaid; i++)
            actions.Attack(soldiers[i].Id, target.X, target.Y);
        _lastRaidTime = ctx.Time;
    }

    /// <summary>
    /// Soldaten ausbilden: die Kaserne bildet Milizen, der Schießstand
    /// Bogenschützen. Die KI reihst ein, solange <see cref="SoldierTarget"/>
    /// (im Feld plus in Produktion) noch nicht erreicht sind und die
    /// Bevölkerungsgrenze Platz lässt. Dorfbewohnerbildung bleibt die
    /// Rücklage — Soldaten nur, wenn Pop und Rohstoffe übrig sind.
    /// </summary>
    private static void TrainSoldiers(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (state.TownCenter is null) return;
        if (state.AgeTarget is not null) return;              // Aufstieg blockiert
        if (state.PopulationCount >= state.PopulationCapacity) return;

        // Wie viele Soldaten stehen im Feld (alive, nicht Dorfbewohner)?
        int inField = state.Units.Count(u => u.Kind != UnitKind.Villager
                                             && u.State != UnitStateKind.Dead);
        if (inField >= SoldierTarget) return;

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
    /// Ein Bauplatz im Ring um das Stadtzentrum (nahe → weit) suchen, die
    /// Baustelle anlegen und <paramref name="workersFor"/> Arbeiter dazu
    /// schicken — freie zuerst, bei Bedarf aufgefüllt aus dem Sammelpool
    /// (nur wenn <paramref name="allowPool"/>). True, wenn die Baustelle stand.
    /// </summary>
    private static bool TryBuildNear(IWorldState state, IWorldActions actions,
        List<UnitSnapshot> workers, BuildingType type,
        int workersFor, bool allowPool)
    {
        int size = BuildingRules.SizeOf(type);
        var tc = state.TownCenter!;
        int x0 = Math.Max(0, tc.X - BuildSpotRadius);
        int y0 = Math.Max(0, tc.Y - BuildSpotRadius);
        int x1 = Math.Min(state.Width, tc.X + tc.Width + BuildSpotRadius);
        int y1 = Math.Min(state.Height, tc.Y + tc.Height + BuildSpotRadius);

        for (int r = 1; r <= BuildSpotRadius; r++)
        {
            for (int x = x0; x < x1; x++)
            {
                for (int y = y0; y < y1; y++)
                {
                    // Chebyshev-Ring um das Stadtzentrum (Gebäuderahmens)
                    int dx = Math.Max(0, Math.Max(tc.X - x, x - (tc.X + tc.Width - 1)));
                    int dy = Math.Max(0, Math.Max(tc.Y - y, y - (tc.Y + tc.Height - 1)));
                    if (Math.Max(dx, dy) != r) continue;

                    if (!state.IsExplored(x, y)) continue;
                    if (!state.CanPlace(type, x, y, size)) continue;

                    // Arbeiter: zuerst die freien (in workers), dann — falls
                    // nicht genug und erlaubt — sammelnde aus dem Pool. Das
                    // Haus ist die einzige Ausnahme: ohne abgezogene Ernte wird
                    // es nie fertig und das Dorf bleibt an der Grenze sitzen.
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
                    // abgelehnt (Baumarkt leer, CanPlace-Spiel-Kontroll etc.) —
                    // weiter suchen
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
    // Schritt 3: Freie Dorfbewohner an Quellen schicken
    // ---------------------------------------------------------------------
    private static void SendToSources(IWorldState state, IWorldActions actions, List<UnitSnapshot> workers)
    {
        if (workers.Count == 0) return;

        // Ressourcen-Reihenfolge nach Anforderung: Nahrung zuerst, dann
        // Holz, dann Stein, Gold.
        var priority = new[] { Resource.Food, Resource.Wood, Resource.Stone, Resource.Gold };

        for (int i = 0; i < workers.Count; i++)
        {
            var worker = workers[i];
            (int X, int Y)? src = null;
            foreach (var type in priority)
            {
                src = state.FindSource(type, worker.X, worker.Y, SourceSearchRadius);
                // Die KI darf nur an Quellen arbeiten, die ihr Besitzer kennt —
                // eine unerforschte Kachel ist für ihn nicht gezielt nutzbar.
                if (src is not null)
                {
                    if (!state.IsExplored(src.Value.X, src.Value.Y))
                        src = null;
                    else
                        break;
                }
            }
            if (src is null) continue;    // keine Quelle frei — bleibt untätig

            actions.Gather(worker.Id, src.Value.X, src.Value.Y);
            workers.RemoveAt(i);
            i--;
        }
    }

    // ---------------------------------------------------------------------
    // Schritt 4: Zeitalter aufsteigen
    // ---------------------------------------------------------------------
    private static void AgeUpIfPossible(IWorldState state, IWorldActions actions, AiContext ctx)
    {
        if (state.AgeTarget is not null) return;     // Aufstieg läuft schon
        if (state.Age >= Age.Imperial) return;       // letztes Zeitalter

        Age next = (Age)((int)state.Age + 1);
        var cost = AgeRules.CostOf(next);
        if (!state.Resources.CanAfford(CostOf(cost))) return;
        if (state.TownCenter is null) return;

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
