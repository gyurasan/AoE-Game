using AoE.Core.Economy;
using AoE.Core.Entities;

namespace AoE.Core.Ai;

/// <summary>
/// Die eingebaute Wirtschaft-KI: ein Regel-Modell, das eine funktionierende
/// AoE-Wirtschaft spielt — Dorfbewohner sammeln, bauen, ausbilden und steigen
/// auf. <see cref="IAi"/> ist die Schnittstelle, an die sich auch eine externe
/// AI (LLM, Skript, Netzwerk) hängt.
///
/// Entscheidungsstrategie (deterministisch, <see cref="ctx"/>.Rng bleibt ungenutzt):
/// <ol>
///   <li>Offene Baustellen füllen — jeder freie Platz (max. 4/Baustelle) bekommt
///      einen freien Dorfbewohner. <see cref="IWorldActions.AssignBuilder"/>.</li>
///   <li>Wenn Pop an der Obergrenze und eine Ausbildung eingereiht ist: Haus
///      platzieren und 2-3 freie Arbeiter hin schicken. <see cref="IWorldActions.Build"/>.</li>
///   <li>Im Stadtzentrum einen Dorfbewohner ausbilden — solange Pop < Obergrenze
///      und die Warteschlange voll ist oder kurz. <see cref="IWorldActions.TrainVillager"/>.</li>
///   <li>Freie Dorfbewohner an Quellen schickten — Nahrung vor Holz, dann Stein und Gold.
///      <see cref="IWorldActions.Gather"/>.</li>
///   <li>Zeitalter aufsteigen — sobald die Rohstoffe ausreichen und kein Aufstieg
///      läuft. <see cref="IWorldActions.AdvanceAge"/>.</li>
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

    /// <summary>Warteschlangenlänge, bei der die KI aufhört auszubilden; 12 gibt
    /// einen Sicherheitsaufschlag unter <c>TrainingQueue.MAX_LENGTH</c> (15).</summary>
    public const int MaxQueue = 12;

    /// <summary>Radius für die Quellensuche (Kacheln).</summary>
    public const int SourceSearchRadius = 16;

    /// <summary>Radius für die Baustellen-Suche (Kacheln).</summary>
    public const int BuildSpotRadius = 8;

    public void Tick(IWorldState state, IWorldActions actions, float dt, AiContext ctx)
    {
        ctx.Advance(dt);

        var workers = FreeWorkers(state);
        FillSites(state, actions, workers);
        PlanBuilding(state, actions, workers);   // neu: Gebäude planen + Arbeiter schicken
        TrainIfPossible(state, actions);
        SendToSources(state, actions, workers);
        AgeUpIfPossible(state, actions);
    }

    // ---------------------------------------------------------------------
    // Schritt 2: Gebäude planen (Haus, Farm, Lager) und Arbeiter schicken
    // ---------------------------------------------------------------------
    private static void PlanBuilding(IWorldState state, IWorldActions actions, List<UnitSnapshot> workers)
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
    private static void TrainIfPossible(IWorldState state, IWorldActions actions)
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
    private static void AgeUpIfPossible(IWorldState state, IWorldActions actions)
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
    static List<UnitSnapshot> FreeWorkers(IWorldState state)
        => state.Units
            .Where(u => u.Kind == UnitKind.Villager
                        && u.State == UnitStateKind.Idle
                        && u.Gathering is null
                        && u.BuildingId is null)
            .OrderBy(u => u.Id)
            .ToList();
}
