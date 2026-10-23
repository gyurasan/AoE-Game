using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;
using Resource = AoE.Core.Entities.Resource;
using GatherPhase = AoE.Core.Economy.GatherPhase;

/// <summary>
/// Gruppe raubtiere (RT): Bären und Wölfe im Spielbildschirm. Wunsch des Nutzers (2026-10-23):
/// „Kommen Dorfbewohner oder Soldaten den Tieren zu nahe werden sie von ihnen angegriffen ... von
/// Gebäuden halten sie Abstand (mindestens 4-5 Kacheln)" und „Sie verfolgen aber dann keine
/// Menschen wenn man davon läuft nur wenn man zu nahe zu Ihnen hinläuft". Die Abläufe:
/// annähern und weglaufen, Gegenwehr des Soldaten, Jagd per Klick bis zum Fleisch, Bogenschütze
/// aus sicherer Entfernung, Rückzug vor einem neuen Gebäude.
/// </summary>
static class Raubtiere
{
    public static void Pruefen(int karte, List<string> verstoesse)
    {
        Annaehern(karte, verstoesse);
        Gegenwehr(karte, verstoesse);
        Jagd(karte, verstoesse);
        Fernkampf(karte, verstoesse);
        Rueckzug(karte, verstoesse);
    }

    /// <summary>Eine Welt ohne die Raubtiere der Kartenerzeugung - jeder Ablauf stellt seine eigenen auf.</summary>
    static Welt NeueWelt(List<(string Name, Vector2 Position)> log = null)
    {
        var w = new Welt();
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", Vector2.Zero);
        if (log != null)
            w.Set("SoundLog", log);
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
                if (w.Map.GetTile(x, y) is { } t && t.Food.IsPredator())
                {
                    t.Food = FoodSource.None;
                    t.Animal = null;
                    t.ResourceType = null;
                    t.ResourceAmount = 0;
                }
        w.Map.RebuildWildAnimals();
        // Die aufgestellten Raubtiere wandern nicht von selbst - so zeigt sich, dass sie niemandem folgen
        w.Map.Settings.BearWanderSecondsMin = w.Map.Settings.BearWanderSecondsMax = 100000f;
        w.Map.Settings.WolfWanderSecondsMin = w.Map.Settings.WolfWanderSecondsMax = 100000f;
        return w;
    }

    static bool Frei(Welt w, int x, int y) => w.Map.GetTile(x, y) is { } t && w.Map.IsWalkable(x, y)
        && t.Type == TileType.Grassland && t.ResourceType == null && t.Food == FoodSource.None && !t.Farm
        && string.IsNullOrEmpty(t.Building);

    /// <summary>
    /// Eine freie Wiese irgendwo auf der Karte, möglichst nah am eigenen Stadtzentrum, mindestens 6
    /// Kacheln von jedem Gebäude, mit freiem Umkreis von zwei Kacheln, ohne Einheit in drei Kacheln
    /// Nähe und mit einer freien Anlaufreihe drei bis fünf Kacheln zur Seite D (-1 links, +1 rechts) -
    /// von dort kommen Dorfbewohner, Miliz und Bogenschütze. Die Karten sind zufällig: eine feste Seite
    /// oder ein enger Umkreis fanden auf manchen Karten keinen Platz.
    /// </summary>
    static (int X, int Y, int D) Platz(Welt w)
    {
        var (sx, sy) = w.Map.StartPosition(0);
        var kandidaten = new List<(int X, int Y, int D)>();
        for (int x = 3; x < w.Map.Width - 3; x++)
            for (int y = 3; y < w.Map.Height - 3; y++)
            {
                if (w.Map.BuildingDistance(x, y) < 6) continue;
                bool frei = true;
                for (int dx = -2; dx <= 2 && frei; dx++)
                    for (int dy = -2; dy <= 2 && frei; dy++)
                        frei = Frei(w, x + dx, y + dy);
                if (!frei) continue;
                var mitte = w.Map.GridToWorld(new Vector2(x, y));
                if (w.Map.Units.Any(u => Vector2.Distance(u.Position, mitte) < 3 * w.Map.TileSize)) continue;
                foreach (int seite in new[] { -1, 1 })
                    if (Enumerable.Range(3, 3).All(k => Frei(w, x + seite * k, y)))
                    {
                        kandidaten.Add((x, y, seite));
                        break;
                    }
            }
        if (kandidaten.Count == 0)
            throw new InvalidOperationException("kein freier Platz für ein Raubtier auf der ganzen Karte");
        return kandidaten.OrderBy(k => Math.Abs(k.X - sx) + Math.Abs(k.Y - sy)).First();
    }

    static WildAnimal Aufstellen(Welt w, FoodSource art, (int X, int Y, int D) p)
    {
        if (!w.Map.PlacePredator(art, p.X, p.Y))
            throw new InvalidOperationException($"PlacePredator({art}, {p.X}, {p.Y}) lehnt ab");
        var tier = w.Map.GetTile(p.X, p.Y).Animal;
        tier.WanderTimer = 100000f;
        return tier;
    }

    static Unit Neu(Welt w, UnitType typ, int x, int y)
    {
        var u = w.Map.AddUnit(typ, x, y, 0);
        w.P1.AddUnit(u);
        return u;
    }

    static void Gehe(Welt w, Unit u, int x, int y)
    {
        u.Job = null;
        u.BuildSite = null;
        u.TargetPosition = w.Map.GridToWorld(new Vector2(x, y));
        u.Path = w.Map.FindPath(u.Position, u.TargetPosition);
        u.State = u.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
    }

    // 1. Zu nahe heran: der Bär schlägt zu. Weg: er bleibt, wo er ist, und schlägt nicht mehr zu.
    static void Annaehern(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubtiere annähern";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var p = Platz(w);
        var baer = Aufstellen(w, FoodSource.Bear, p);
        var v = w.Dorfbewohner().First();
        v.Position = w.Map.GridToWorld(new Vector2(p.X + p.D * 4, p.Y));
        Gehe(w, v, p.X + p.D, p.Y);
        float zeit = w.LaufeBis(() => v.Health < v.MaxHealth, 30f);
        if (v.Health == v.MaxHealth)
            verstoesse.Add($"{wer}: der Dorfbewohner steht neben dem Bären ({p.X}, {p.Y}) und wird in 30 s nicht angegriffen");
        if (w.Map.LocatePredator(baer) != (p.X, p.Y))
            verstoesse.Add($"{wer}: der Bär ist von ({p.X}, {p.Y}) nach {w.Map.LocatePredator(baer)} gelaufen");
        if (v.PreyTarget != null || v.State == UnitState.Attacking)
            verstoesse.Add($"{wer}: der Dorfbewohner greift den Bären von selbst an - nur Soldaten wehren sich");
        if (baer.Strike <= 0f && v.Health < v.MaxHealth && w.LaufeBis(() => baer.Strike > 0f, 2.5f) >= 2.5f)
            verstoesse.Add($"{wer}: der Bär zeigt beim Zuschlagen kein Angriffsbild (Strike bleibt 0)");

        // Weglaufen: fünf Kacheln zurück - danach kein Treffer mehr, und der Bär folgt nicht
        Gehe(w, v, p.X + p.D * 5, p.Y);
        w.LaufeBis(() => v.Path.Count == 0 && v.State == UnitState.Idle, 20f);
        int hp = v.Health;
        w.LaufeBis(() => false, 15f);
        if (v.State == UnitState.Dead || v.Health < hp)
            verstoesse.Add($"{wer}: der Dorfbewohner wird auch fünf Kacheln weiter noch getroffen ({hp} -> {v.Health})");
        if (w.Map.LocatePredator(baer) != (p.X, p.Y))
            verstoesse.Add($"{wer}: der Bär folgt dem Dorfbewohner - jetzt auf {w.Map.LocatePredator(baer)}");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s getroffen ({v.MaxHealth - hp} Schaden), weggelaufen, der Bär bleibt und schlägt nicht mehr zu");
    }

    // 2. Ein Soldat, den ein Wolf beißt, wehrt sich und erlegt ihn; der Wolf verschwindet.
    static void Gegenwehr(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubtiere Gegenwehr";
        int vorher = verstoesse.Count;
        var log = new List<(string Name, Vector2 Position)>();
        var w = NeueWelt(log);
        var p = Platz(w);
        var wolf = Aufstellen(w, FoodSource.Wolf, p);
        var miliz = Neu(w, UnitType.Militia, p.X + p.D * 3, p.Y);
        Gehe(w, miliz, p.X + p.D, p.Y);
        w.LaufeBis(() => miliz.Health < miliz.MaxHealth, 20f);
        if (miliz.Health == miliz.MaxHealth)
        {
            verstoesse.Add($"{wer}: die Miliz neben dem Wolf wird in 20 s nicht gebissen");
            return;
        }
        w.LaufeBis(() => miliz.PreyTarget != null, 0.5f);
        if (!ReferenceEquals(miliz.PreyTarget, wolf))
            verstoesse.Add($"{wer}: die gebissene Miliz wehrt sich nicht (PreyTarget {(miliz.PreyTarget == null ? "null" : "anderes Tier")}, {miliz.State})");
        float zeit = w.LaufeBis(() => w.Map.LocatePredator(wolf) == null, 40f);
        if (w.Map.LocatePredator(wolf) != null)
            verstoesse.Add($"{wer}: die Miliz erlegt den Wolf nicht in 40 s (Wolf {wolf.Health} LP)");
        else
        {
            var t = w.Map.GetTile(p.X, p.Y);
            if (t.Food != FoodSource.None || t.Animal != null || w.Map.WolfCount != 0)
                verstoesse.Add($"{wer}: der erlegte Wolf bleibt liegen (Food {t.Food}, WolfCount {w.Map.WolfCount})");
            w.LaufeBis(() => false, 0.5f);
            if (miliz.PreyTarget != null || miliz.State != UnitState.Idle)
                verstoesse.Add($"{wer}: nach dem Wolf steht die Miliz nicht untätig (PreyTarget {miliz.PreyTarget}, {miliz.State})");
            if (!log.Any(l => l.Name == "treffer") || !log.Any(l => l.Name == "tod"))
                verstoesse.Add($"{wer}: Biss und Tod sind nicht zu hören ({string.Join(", ", log.Select(l => l.Name).Distinct())})");
        }
        if (miliz.State == UnitState.Dead)
            verstoesse.Add($"{wer}: die Miliz stirbt gegen einen einzelnen Wolf");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: gebissen, wehrt sich, Wolf nach {zeit:0.0} s erlegt und verschwunden, Miliz mit {miliz.Health}/{miliz.MaxHealth} LP");
    }

    // 3. Dorfbewohner per Klick auf den Bären: sie erlegen ihn und zerlegen ihn gleich.
    static void Jagd(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubtiere Jagd";
        int vorher = verstoesse.Count;
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            var p = Platz(w);
            var baer = Aufstellen(w, FoodSource.Bear, p);
            var jaeger = w.Dorfbewohner().Take(4).ToList();
            jaeger.Add(Neu(w, UnitType.Villager, p.X + p.D * 4, p.Y + 1));
            jaeger.Add(Neu(w, UnitType.Villager, p.X + p.D * 4, p.Y - 1));
            foreach (var j in jaeger)
            {
                j.Job = null;
                j.State = UnitState.Idle;
            }
            // Ein Klick auf die Kachel über dem Bären trifft ihn auch - er ragt hinein
            w.Call("IssueCommand", 0, jaeger, new Vector2(p.X, p.Y - 1));
            var ohne = jaeger.Where(j => !ReferenceEquals(j.PreyTarget, baer)).ToList();
            if (ohne.Count > 0)
            {
                verstoesse.Add($"{wer}: Klick über den Bären - {ohne.Count} von {jaeger.Count} Dorfbewohnern greifen ihn nicht an");
                return;
            }
            // Ein anderer Befehl bricht die Jagd ab
            var abbrecher = jaeger[5];
            w.Call("IssueCommand", 0, new List<Unit> { abbrecher }, new Vector2(p.X + p.D * 6, p.Y - 1));
            if (abbrecher.PreyTarget != null)
                verstoesse.Add($"{wer}: ein Laufbefehl bricht die Jagd nicht ab");
            jaeger.Remove(abbrecher);

            float zeit = w.LaufeBis(() => baer.Slaughtered, 90f);
            var t = w.Map.GetTile(p.X, p.Y);
            if (!baer.Slaughtered)
            {
                verstoesse.Add($"{wer}: fünf Dorfbewohner erlegen den Bären nicht in 90 s (LP {baer.Health})");
                return;
            }
            if (t.ResourceType != Resource.Food || t.ResourceAmount <= 0 || t.Food != FoodSource.Bear)
                verstoesse.Add($"{wer}: der erlegte Bär liegt nicht als Fleisch da ({t.Food}, {t.ResourceType}, {t.ResourceAmount})");
            w.LaufeBis(() => jaeger.Any(j => j.Job is { Resource: Resource.Food } job && job.Source.X == p.X && job.Source.Y == p.Y), 3f);
            var zerleger = jaeger.Where(j => j.State != UnitState.Dead && j.Job is { } job
                                             && job.Source.X == p.X && job.Source.Y == p.Y).ToList();
            if (zerleger.Count == 0)
                verstoesse.Add($"{wer}: nach dem Erlegen zerlegt keiner der Jäger den Bären");
            w.LaufeBis(() => t.ResourceAmount < w.Map.Settings.BearFood, 30f);
            if (t.ResourceAmount >= w.Map.Settings.BearFood)
                verstoesse.Add($"{wer}: vom Fleisch wird in 30 s nichts gesammelt");
            if (jaeger.Where(j => j.State != UnitState.Dead).FirstOrDefault(j => j.PreyTarget != null) is { } halter)
                verstoesse.Add($"{wer}: ein Jäger hält den erlegten Bären noch als Ziel ({halter.State}, Weg {halter.Path.Count})");
            if (verstoesse.Count == vorher)
                Console.WriteLine($"  ok  {wer}: Bär nach {zeit:0.0} s erlegt, {zerleger.Count} Jäger zerlegen ihn, {jaeger.Count(j => j.State == UnitState.Dead)} gefallen; Laufbefehl bricht ab");
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    // 4. Ein Bogenschütze schießt aus sicherer Entfernung: der Bär kommt nicht zu ihm.
    static void Fernkampf(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubtiere Fernkampf";
        int vorher = verstoesse.Count;
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            var p = Platz(w);
            var baer = Aufstellen(w, FoodSource.Bear, p);
            var schuetze = Neu(w, UnitType.Archer, p.X + p.D * 5, p.Y);
            w.Call("IssueCommand", 0, new List<Unit> { schuetze }, new Vector2(p.X, p.Y));
            if (!ReferenceEquals(schuetze.PreyTarget, baer))
            {
                verstoesse.Add($"{wer}: der Bogenschütze nimmt den Bären nicht ins Ziel");
                return;
            }
            bool gewandert = false;
            float zeit = w.LaufeBis(() =>
            {
                gewandert |= !baer.Slaughtered && w.Map.LocatePredator(baer) != (p.X, p.Y);
                return baer.Slaughtered;
            }, 90f);
            if (!baer.Slaughtered)
                verstoesse.Add($"{wer}: der Bogenschütze erlegt den Bären nicht in 90 s (LP {baer.Health}, {schuetze.State})");
            if (gewandert)
                verstoesse.Add($"{wer}: der beschossene Bär geht auf den Schützen los");
            if (schuetze.Health < schuetze.MaxHealth)
                verstoesse.Add($"{wer}: der Bogenschütze wird getroffen ({schuetze.Health}/{schuetze.MaxHealth}), obwohl er auf Abstand schießt");
            if (verstoesse.Count == vorher)
                Console.WriteLine($"  ok  {wer}: Bär nach {zeit:0.0} s aus der Ferne erlegt, er bleibt stehen, der Schütze unverletzt");
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    // 5. Neben einem Raubtier entsteht ein Gebäude: es zieht sich auf Abstand zurück.
    static void Rueckzug(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubtiere Rückzug";
        int vorher = verstoesse.Count;
        var w = NeueWelt();
        var p = Platz(w);
        var wolf = Aufstellen(w, FoodSource.Wolf, p);
        w.Map.AddBuilding(p.X + 1, p.Y + 2, "Haus", 0, 2);    // Abstand 2
        int grenze = w.Map.Settings.PredatorBuildingDistance;
        float zeit = w.LaufeBis(() => w.Map.LocatePredator(wolf) is { } q && w.Map.BuildingDistance(q.X, q.Y) >= grenze, 15f);
        var lage = w.Map.LocatePredator(wolf);
        if (lage is not { } l || w.Map.BuildingDistance(l.X, l.Y) < grenze)
            verstoesse.Add($"{wer}: der Wolf steht nach 15 s noch {(lage is { } m ? w.Map.BuildingDistance(m.X, m.Y) : -1)} Kacheln vom neuen Haus");
        // Danach wandert er wieder (gewöhnlicher Takt) und hält den Abstand
        w.Map.Settings.WolfWanderSecondsMin = 4f;
        w.Map.Settings.WolfWanderSecondsMax = 9f;
        wolf.WanderTimer = 0.5f;
        int naechst = int.MaxValue;
        var orte = new HashSet<(int, int)>();
        w.LaufeBis(() =>
        {
            if (w.Map.LocatePredator(wolf) is { } q)
            {
                naechst = Math.Min(naechst, w.Map.BuildingDistance(q.X, q.Y));
                orte.Add(q);
            }
            return false;
        }, 90f);
        if (naechst < grenze)
            verstoesse.Add($"{wer}: beim Wandern kommt der Wolf bis auf {naechst} Kacheln an ein Gebäude");
        // Nicht gewandert ist nur ein Fehler, wenn er hätte wandern können: mindestens zwei freie
        // Nachbarn mit genug Abstand (am Zonenrand zwischen Wald und Fels bleibt er zu Recht stehen)
        if (orte.Count < 2 && w.Map.LocatePredator(wolf) is { } hier)
        {
            int moeglich = 0;
            for (int dx = -1; dx <= 1; dx++)
                for (int dy = -1; dy <= 1; dy++)
                    if ((dx != 0 || dy != 0) && Frei(w, hier.X + dx, hier.Y + dy)
                        && w.Map.BuildingDistance(hier.X + dx, hier.Y + dy) >= grenze)
                        moeglich++;
            if (moeglich >= 2)
                verstoesse.Add($"{wer}: der Wolf wandert in 90 s nicht, obwohl {moeglich} Nachbarkacheln frei sind");
            else
                Console.WriteLine($"  --  {wer}: der Wolf steht am Zonenrand zwischen Hindernissen ({moeglich} freie Nachbarn) - Wandern nicht prüfbar");
        }
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s auf {grenze}+ Kacheln zurückgezogen, danach {orte.Count} Kacheln besucht, nie näher als {naechst}");
    }
}
