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
///   <li>Offene Baustellen füllen — jeder offene Platz (max. 4/Baustelle) bekommt
///      einen freien Dorfbewohner zugewiesen. <see cref="IWorldActions.AssignBuilder"/>.</li>
///   <li>Im Stadtzentrum einen Dorfbewohner ausbilden — solange das Dorf die
///      Bevölkerungsgrenze nicht erreicht und die Warteschlange leer oder kurz ist.
///      <see cref="IWorldActions.TrainVillager"/>.</li>
///   <li>Freie Dorfbewohner an Quellen schickten — Holz vor Nahrung, dann Stein
///      und Gold (die Reihenfolge der AoE-II-Ökonomie).
///      <see cref="IWorldActions.Gather"/>.</li>
///   <li>Zeitalter aufsteigen — sobald die Rohstoffe ausreichen und kein Aufstieg
///      läuft. <see cref="IWorldActions.AdvanceAge"/>.</li>
/// </ol>
/// </summary>
public sealed class EconomyAi : IAi
{
    /// <summary>So viel (Nahrung) braucht die KI pro Ausbildung — muss mit
    /// <c>BuildingRules</c> / <c>Player.VillagerCost</c> im Spiel übereinstimmen.</summary>
    public const int VillagerFoodCost = 25;

    /// <summary>So viele Bauarbeiter pro Baustelle, bevor der Ertrag nicht mehr
    /// steigt (4 = 2× eines Arbeiters laut AoE-II-Regel).</summary>
    public const int MaxWorkersPerSite = 4;

    /// <summary>Warteschlangenlänge, bei der die KI aufhört auszubilden, bevor
    /// die Ausbildung voll ist (15 = <c>TrainingQueue.MAX_LENGTH</c>, 12 gibt
    /// einen Sicherheitsaufschlag).</summary>
    public const int MaxQueue = 12;

    /// <summary>Radius für die Quellensuche (Kacheln).</summary>
    public const int SourceSearchRadius = 16;

    public void Tick(IWorldState state, IWorldActions actions, float dt, AiContext ctx)
    {
        ctx.Advance(dt);

        var workers = FreeWorkers(state);
        FillSites(state, actions, workers);
        TrainIfPossible(state, actions);
        SendToSources(state, actions, workers);
        AgeUpIfPossible(state, actions);
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

        // Ressourcen-Reihenfolge wie in AoE II: Holz zuerst, dann Nahrung,
        // dann Stein, Gold.
        var priority = new[] { Resource.Wood, Resource.Food, Resource.Stone, Resource.Gold };

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
