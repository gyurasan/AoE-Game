using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;
using Resource = AoE.Core.Entities.Resource;
using CorePosition = AoE.Core.Entities.Position;
using GatherJob = AoE.Core.Economy.GatherJob;

/// <summary>
/// Gruppe raubflucht: Dorfbewohner fliehen auch vor Raubtieren (Nutzer 2026-11-13: „Raubtiere
/// können übrigens auch Flucht auslösen“). Ein gebissener Holzfäller flieht, merkt sich seine
/// Arbeit und sammelt danach an einer Quelle außerhalb der Gefahrenzone weiter, statt wieder in
/// den Biss zu laufen; ist das Tier erlegt, kehrt er an den alten Baum zurück. Jäger, Soldaten
/// und wer ein Wildschwein selbst gereizt hat, fliehen nicht.
/// </summary>
static class Raubflucht
{
    public static void Pruefen(int karte, List<string> verstoesse)
    {
        TileMap.TestNoFog = true;
        try
        {
            WoandersWeiter(karte, verstoesse);
            ZurueckNachDemErlegen(karte, verstoesse);
            JaegerUndSoldatFliehenNicht(karte, verstoesse);
            Wildschwein(karte, verstoesse);
            Vorbeilaufen(karte, verstoesse);
            Spielstand(karte, verstoesse);
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    /// <summary>
    /// Der gemeinsame Aufbau der Fälle 1 und 2 - oder null, wenn die Karte keinen passenden
    /// Wald hat (dann meldet der Fall „übersprungen“ und prüft nichts).
    /// VERTRAG:
    /// 1. w = Raubtiere.NeueWelt() (ohne Tiere der Kartenerzeugung, Raubtiere wandern nicht).
    /// 2. Baum suchen: alle Kacheln (x, y) mit 2 &lt;= x &lt; Map.Width - 2 und 2 &lt;= y &lt; Map.Height - 2,
    ///    tile.ResourceType == Resource.Wood, tile.ResourceAmount &gt; 0,
    ///    w.Map.BuildingDistance(x, y) &gt;= 8, keine Einheit (w.Map.Units) näher als 5 * TileSize an
    ///    w.Map.GridToWorld(new Vector2(x, y)), mindestens eine der vier Nachbarkacheln (x±1, y),
    ///    (x, y±1) ist frei (Frei, siehe unten), und es gibt eine zweite Holzkachel (ResourceType
    ///    Wood, Menge &gt; 0) mit Chebyshev-Abstand 4 bis 7 zu (x, y). Von den Kandidaten der mit dem
    ///    kleinsten Abstand |x - sx| + |y - sy| zu (sx, sy) = w.Map.StartPosition(0). Keiner: null.
    /// 3. Dorfbewohner: v = Raubtiere.Neu(w, UnitType.Villager, nx, ny) auf der ersten freien der
    ///    vier Nachbarkacheln (Reihenfolge x+1, x-1, y+1, y-1). Dann v.Job = new GatherJob(0,
    ///    Resource.Wood, new CorePosition(x, y)); w.Call("FollowJob", v); w.LaufeBis(() =&gt; v.State
    ///    == UnitState.Gathering, 20f). Sammelt er dann nicht: null.
    /// 4. Wolf: unter allen freien Kacheln (Frei) mit Abstand der Kachelmitte
    ///    (w.Map.GridToWorld) zu v.Position kleiner oder gleich 1.15f * TileSize die nächste zu
    ///    v.Position; keine: null. w.Map.PlacePredator(FoodSource.Wolf, wx, wy) (false: null),
    ///    wolf = w.Map.GetTile(wx, wy).Animal; wolf.WanderTimer = 100000f.
    /// 5. Ausweichbaum nötig: es muss eine Holzkachel (ResourceType Wood, Menge &gt; 0) geben mit
    ///    Chebyshev-Abstand höchstens 7 zu (x, y), Abstand MathF.Sqrt((ax - wx)² + (ay - wy)²)
    ///    mindestens 4 zum Wolf und mindestens einer begehbaren der acht Nachbarkacheln
    ///    (w.Map.IsWalkable). Keine: null.
    /// 6. Rückgabe (w, v, Baum (x, y), Wolfkachel (wx, wy), wolf).
    /// Frei(x, y): GetTile nicht null, w.Map.IsWalkable(x, y), Type == TileType.Grassland,
    /// ResourceType == null, Food == FoodSource.None, !Farm, Building leer.
    /// </summary>
    static (Welt W, Unit V, (int X, int Y) Baum, (int X, int Y) WolfKachel, WildAnimal Wolf)? Szene()
    {
        var w = Raubtiere.NeueWelt();
        var m = w.Map;
        var (sx, sy) = m.StartPosition(0);

        bool Frei(TileMap map, int x, int y)
        {
            var t = map.GetTile(x, y);
            return t != null && map.IsWalkable(x, y) && t.Type == TileType.Grassland
                && t.ResourceType == null && t.Food == FoodSource.None && !t.Farm
                && string.IsNullOrEmpty(t.Building);
        }

        bool IstHolz(int x, int y) => m.GetTile(x, y)?.ResourceType == Resource.Wood
            && m.GetTile(x, y).ResourceAmount > 0;

        (int X, int Y)? baum = null;
        int best = int.MaxValue;
        for (var qx = 2; qx < m.Width - 2; qx++)
        {
            for (var qy = 2; qy < m.Height - 2; qy++)
            {
                if (!IstHolz(qx, qy) || m.BuildingDistance(qx, qy) < 8)
                    continue;
                var mitte = m.GridToWorld(new Vector2(qx, qy));
                if (m.Units.Any(u => Vector2.Distance(u.Position, mitte) < 5 * m.TileSize))
                    continue;
                if (!Frei(m, qx + 1, qy) && !Frei(m, qx - 1, qy) && !Frei(m, qx, qy + 1) && !Frei(m, qx, qy - 1))
                    continue;
                var zweite = false;
                for (var ax = qx - 7; ax <= qx + 7 && !zweite; ax++)
                    for (var ay = qy - 7; ay <= qy + 7 && !zweite; ay++)
                        if (Math.Max(Math.Abs(ax - qx), Math.Abs(ay - qy)) is >= 4 and <= 7 && IstHolz(ax, ay))
                            zweite = true;
                if (!zweite)
                    continue;
                var d = Math.Abs(qx - sx) + Math.Abs(qy - sy);
                if (d < best)
                {
                    best = d;
                    baum = (qx, qy);
                }
            }
        }
        if (baum is not { } b)
            return null;

        var (x, y) = b;
        var (nx, ny) = Frei(m, x + 1, y) ? (x + 1, y)
            : Frei(m, x - 1, y) ? (x - 1, y)
            : Frei(m, x, y + 1) ? (x, y + 1)
            : (x, y - 1);
        var v = Raubtiere.Neu(w, UnitType.Villager, nx, ny);
        v.Job = new GatherJob(0, Resource.Wood, new CorePosition(x, y));
        w.Call("FollowJob", v);
        w.LaufeBis(() => v.State == UnitState.Gathering, 20f);
        if (v.State != UnitState.Gathering)
            return null;

        (int X, int Y)? wolfk = null;
        var bestW = float.MaxValue;
        for (var ax = 0; ax < m.Width; ax++)
        {
            for (var ay = 0; ay < m.Height; ay++)
            {
                if (!Frei(m, ax, ay))
                    continue;
                var d = Vector2.Distance(m.GridToWorld(new Vector2(ax, ay)), v.Position);
                if (d <= 1.15f * m.TileSize && d < bestW)
                {
                    bestW = d;
                    wolfk = (ax, ay);
                }
            }
        }
        if (wolfk is not { } wk)
            return null;
        if (!m.PlacePredator(FoodSource.Wolf, wk.X, wk.Y))
            return null;
        var wolf = m.GetTile(wk.X, wk.Y).Animal;
        wolf.WanderTimer = 100000f;

        var ausweich = false;
        for (var ax = 0; ax < m.Width && !ausweich; ax++)
        {
            for (var ay = 0; ay < m.Height && !ausweich; ay++)
            {
                if (!IstHolz(ax, ay) || Math.Max(Math.Abs(ax - x), Math.Abs(ay - y)) > 7)
                    continue;
                if (MathF.Sqrt((ax - wk.X) * (ax - wk.X) + (ay - wk.Y) * (ay - wk.Y)) < 4)
                    continue;
                for (var dx = -1; dx <= 1 && !ausweich; dx++)
                    for (var dy = -1; dy <= 1 && !ausweich; dy++)
                        if ((dx != 0 || dy != 0) && m.IsWalkable(ax + dx, ay + dy))
                            ausweich = true;
            }
        }
        if (!ausweich)
            return null;

        return (w, v, (x, y), (wk.X, wk.Y), wolf);
    }

    /// <summary>
    /// Fall 1: Der Wolf bleibt stehen - der Holzfäller flieht und sammelt außerhalb weiter.
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht woanders weiter"; vorher = verstoesse.Count.
    /// s = Szene(); null: Console.WriteLine($"      {wer}: kein Wald fern von Gebäuden - übersprungen")
    /// und return. Sonst (w, v, baum, wk, wolf) = s.Value; voll = v.MaxHealth;
    /// istFlucht = () =&gt; (bool)w.Call("IsFleeing", v).
    /// a) w.LaufeBis(() =&gt; v.Health &lt; voll &amp;&amp; istFlucht(), 6f). Verstoß und return, wenn v.Health
    ///    == voll („in 6 s nicht gebissen“) oder !istFlucht() („gebissen, flieht aber nicht“, mit
    ///    v.State und v.Job != null).
    /// b) Verstoß, wenn v.Job != null oder v.ResumeJob == null oder v.ResumeJob.Source nicht
    ///    (baum.X, baum.Y) ist („Arbeit nicht gemerkt“).
    /// c) Verstoß, wenn !w.Map.InPredatorDanger(baum.X, baum.Y) (der Baum, an dem er gebissen
    ///    wurde, muss in der Gefahrenzone liegen - sonst stimmt die Zone nicht).
    /// d) zeit = w.LaufeBis(() =&gt; !istFlucht() &amp;&amp; v.Job != null &amp;&amp; v.State == UnitState.Gathering,
    ///    45f). Verstoß und return, wenn die Bedingung danach nicht gilt („sammelt nach 45 s nicht
    ///    wieder“, mit v.State, istFlucht(), v.Job?.Source).
    /// e) quelle = v.Job.Source. Verstoß, wenn w.Map.InPredatorDanger(quelle.X, quelle.Y) („sammelt
    ///    wieder in Bissweite“) oder (quelle.X, quelle.Y) == baum.
    /// f) hp = v.Health; w.LaufeBis(() =&gt; false, 15f). Verstoß, wenn v.Health &lt; hp („danach
    ///    erneut gebissen“) oder v.State == UnitState.Dead. Verstoß, wenn
    ///    w.Map.LocatePredator(wolf) != wk („der Wolf folgt“).
    /// Ohne neue Verstöße: Console.WriteLine($"  ok  {wer}: gebissen, geflohen, nach {zeit:0.0} s
    /// an ({quelle.X}, {quelle.Y}) statt ({baum.X}, {baum.Y}) weiter, {v.Health}/{voll} LP").
    /// </summary>
    static void WoandersWeiter(int karte, List<string> verstoesse)
    {
        var wer = $"Karte {karte}, Raubflucht woanders weiter";
        var vorher = verstoesse.Count;
        if (Szene() is not { } s)
        {
            Console.WriteLine($"      {wer}: kein Wald fern von Gebäuden - übersprungen");
            return;
        }
        var (w, v, baum, wk, wolf) = s;
        var voll = v.MaxHealth;
        Func<bool> istFlucht = () => (bool)w.Call("IsFleeing", v);

        w.LaufeBis(() => v.Health < voll && istFlucht(), 6f);
        if (v.Health == voll)
        {
            verstoesse.Add($"{wer}: in 6 s nicht gebissen");
            return;
        }
        if (!istFlucht())
        {
            verstoesse.Add($"{wer}: gebissen, flieht aber nicht (State {v.State}, Job {v.Job != null})");
            return;
        }

        if (v.Job != null || v.ResumeJob == null || (v.ResumeJob.Source.X, v.ResumeJob.Source.Y) != (baum.X, baum.Y))
            verstoesse.Add($"{wer}: Arbeit nicht gemerkt");
        if (!w.Map.InPredatorDanger(baum.X, baum.Y))
            verstoesse.Add($"{wer}: der Baum ({baum.X}, {baum.Y}) liegt nicht in der Gefahrenzone");

        var zeit = w.LaufeBis(() => !istFlucht() && v.Job != null && v.State == UnitState.Gathering, 45f);
        if (istFlucht() || v.Job == null || v.State != UnitState.Gathering)
        {
            verstoesse.Add($"{wer}: sammelt nach 45 s nicht wieder (State {v.State}, flieht {istFlucht()}, Quelle {v.Job?.Source})");
            return;
        }

        var quelle = v.Job.Source;
        if (w.Map.InPredatorDanger(quelle.X, quelle.Y))
            verstoesse.Add($"{wer}: sammelt wieder in Bissweite ({quelle.X}, {quelle.Y})");
        if ((quelle.X, quelle.Y) == baum)
            verstoesse.Add($"{wer}: sammelt wieder am alten Baum ({baum.X}, {baum.Y})");

        var hp = v.Health;
        w.LaufeBis(() => false, 15f);
        if (v.Health < hp)
            verstoesse.Add($"{wer}: danach erneut gebissen ({v.Health}/{voll})");
        if (v.State == UnitState.Dead)
            verstoesse.Add($"{wer}: ist tot");
        if (w.Map.LocatePredator(wolf) != wk)
            verstoesse.Add($"{wer}: der Wolf folgt (jetzt {w.Map.LocatePredator(wolf)})");

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: gebissen, geflohen, nach {zeit:0.0} s an ({quelle.X}, {quelle.Y}) statt ({baum.X}, {baum.Y}) weiter, {v.Health}/{voll} LP");
    }

    /// <summary>
    /// Fall 2: Der Wolf wird während der Flucht erlegt - der Holzfäller kehrt an seinen Baum zurück.
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht zurück nach dem Erlegen"; Szene wie Fall 1
    /// (null: übersprungen). a) wie in Fall 1 (6 s, gebissen und flieht, sonst Verstoß und return).
    /// b) w.Map.DamageAnimal(wk.X, wk.Y, 10000) - der Wolf ist tot und verschwindet. Verstoß, wenn
    ///    w.Map.LocatePredator(wolf) danach nicht null ist.
    /// c) zeit = w.LaufeBis(() =&gt; !istFlucht() &amp;&amp; v.Job != null, 30f). Verstoß und return, wenn
    ///    danach istFlucht() oder v.Job == null („kehrt nicht zurück“).
    /// d) Verstoß, wenn v.Job.Source nicht (baum.X, baum.Y) ist („geht an eine andere Quelle,
    ///    obwohl der Wolf tot ist“).
    /// e) w.LaufeBis(() =&gt; v.State == UnitState.Gathering, 20f); Verstoß, wenn er danach nicht
    ///    sammelt.
    /// Ohne neue Verstöße: Console.WriteLine($"  ok  {wer}: Wolf erlegt, nach {zeit:0.0} s zurück an
    /// ({baum.X}, {baum.Y})").
    /// </summary>
    static void ZurueckNachDemErlegen(int karte, List<string> verstoesse)
    {
        var wer = $"Karte {karte}, Raubflucht zurück nach dem Erlegen";
        var vorher = verstoesse.Count;
        if (Szene() is not { } s)
        {
            Console.WriteLine($"      {wer}: kein Wald fern von Gebäuden - übersprungen");
            return;
        }
        var (w, v, baum, wk, wolf) = s;
        var voll = v.MaxHealth;
        Func<bool> istFlucht = () => (bool)w.Call("IsFleeing", v);

        w.LaufeBis(() => v.Health < voll && istFlucht(), 6f);
        if (v.Health == voll)
        {
            verstoesse.Add($"{wer}: in 6 s nicht gebissen");
            return;
        }
        if (!istFlucht())
        {
            verstoesse.Add($"{wer}: gebissen, flieht aber nicht (State {v.State}, Job {v.Job != null})");
            return;
        }

        w.Map.DamageAnimal(wk.X, wk.Y, 10000);
        if (w.Map.LocatePredator(wolf) != null)
            verstoesse.Add($"{wer}: Wolf ist nach 10000 Schaden noch da ({w.Map.LocatePredator(wolf)})");

        var zeit = w.LaufeBis(() => !istFlucht() && v.Job != null, 30f);
        if (istFlucht() || v.Job == null)
        {
            verstoesse.Add($"{wer}: kehrt nicht zurück (flieht {istFlucht()}, Job {v.Job != null})");
            return;
        }

        if ((v.Job.Source.X, v.Job.Source.Y) != (baum.X, baum.Y))
            verstoesse.Add($"{wer}: geht an eine andere Quelle ({v.Job.Source.X}, {v.Job.Source.Y}), obwohl der Wolf tot ist");

        w.LaufeBis(() => v.State == UnitState.Gathering, 20f);
        if (v.State != UnitState.Gathering)
            verstoesse.Add($"{wer}: sammelt danach nicht (State {v.State})");

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: Wolf erlegt, nach {zeit:0.0} s zurück an ({baum.X}, {baum.Y})");
    }

    /// <summary>
    /// Fall 3: Wer das Tier angreift, flieht nicht - weder der Jäger noch der Soldat.
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht Jäger und Soldat". Für (typ, name) in
    /// (UnitType.Villager, "Jäger") und (UnitType.Militia, "Miliz") je eine eigene Welt:
    /// w = Raubtiere.NeueWelt(); p = Raubtiere.Platz(w); wolf = Raubtiere.Aufstellen(w,
    /// FoodSource.Wolf, p); u = Raubtiere.Neu(w, typ, p.X + p.D * 3, p.Y).
    /// - Jäger: w.Call("AttackAnimal", new List&lt;Unit&gt; { u }, p.X, p.Y) - er greift den Wolf an.
    /// - Miliz: sie läuft nur hin: u.Job = null; u.TargetPosition = w.Map.GridToWorld(new
    ///   Vector2(p.X + p.D, p.Y)); u.Path = w.Map.FindPath(u.Position, u.TargetPosition);
    ///   u.State = UnitState.Moving (sie wehrt sich nach dem Biss von selbst).
    /// w.LaufeBis(() =&gt; u.Health &lt; u.MaxHealth, 20f); nicht gebissen: Verstoß, weiter mit dem
    /// nächsten Typ. Dann 3,5 s weiter (der Wolf beißt alle 1,5 s - den ersten Biss bekommt der
    /// Jäger noch im Anlaufen, wo ohnehin niemand flieht) und dabei in jedem Bild beobachten:
    /// floh = irgendwann (bool)w.Call("IsFleeing", u). Verstoß, wenn floh
    /// („{name} flieht vor dem Wolf, den er bekämpft“) oder w.Map.LocatePredator(wolf) != null und
    /// !ReferenceEquals(u.PreyTarget, wolf) („{name} kämpft nicht mehr gegen den Wolf“).
    /// Ohne neue Verstöße: Console.WriteLine($"  ok  {wer}: Jäger und Miliz kämpfen, statt zu fliehen").
    /// </summary>
    static void JaegerUndSoldatFliehenNicht(int karte, List<string> verstoesse)
    {
        var wer = $"Karte {karte}, Raubflucht Jäger und Soldat";
        var vorher = verstoesse.Count;
        foreach (var (typ, name) in new[] { (UnitType.Villager, "Jäger"), (UnitType.Militia, "Miliz") })
        {
            var w = Raubtiere.NeueWelt();
            var p = Raubtiere.Platz(w);
            var wolf = Raubtiere.Aufstellen(w, FoodSource.Wolf, p);
            var u = Raubtiere.Neu(w, typ, p.X + p.D * 3, p.Y);
            if (typ == UnitType.Villager)
            {
                w.Call("AttackAnimal", new List<Unit> { u }, p.X, p.Y);
            }
            else
            {
                u.Job = null;
                u.TargetPosition = w.Map.GridToWorld(new Vector2(p.X + p.D, p.Y));
                u.Path = w.Map.FindPath(u.Position, u.TargetPosition);
                u.State = UnitState.Moving;
            }

            w.LaufeBis(() => u.Health < u.MaxHealth, 20f);
            if (u.Health == u.MaxHealth)
            {
                verstoesse.Add($"{wer}: {name} wird in 20 s nicht gebissen");
                continue;
            }

            // Zwei weitere Bisse abwarten (Wolf alle 1,5 s) und die Flucht in jedem Bild beobachten
            bool floh = false;
            w.LaufeBis(() => { floh |= (bool)w.Call("IsFleeing", u); return false; }, 3.5f);
            if (floh)
                verstoesse.Add($"{wer}: {name} flieht vor dem Wolf, den er bekämpft");
            if (w.Map.LocatePredator(wolf) != null && !ReferenceEquals(u.PreyTarget, wolf))
                verstoesse.Add($"{wer}: {name} kämpft nicht mehr gegen den Wolf");
        }

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: Jäger und Miliz kämpfen, statt zu fliehen");
    }

    /// <summary>
    /// Fall 4: Ein Wildschwein beißt einen Unbeteiligten - er flieht; wer es selbst gereizt hat
    /// (ProvokedBy), flieht nicht (Locken bleibt möglich).
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht Wildschwein". Zwei Welten, je:
    /// w = Raubtiere.NeueWelt(); p = Raubtiere.Platz(w); Wildschwein von Hand auf (p.X, p.Y):
    /// t = w.Map.GetTile(p.X, p.Y); t.Food = FoodSource.Boar; t.ResourceType = Resource.Food;
    /// t.ResourceAmount = w.Map.Settings.BoarFood; t.Animal = new WildAnimal { Health =
    /// w.Map.PredatorMaxHealth(FoodSource.Boar), WanderTimer = 100000f }; w.Map.RebuildWildAnimals();
    /// eber = t.Animal; u = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D, p.Y) (direkt daneben);
    /// u.Job = null; u.State = UnitState.Idle; u.TargetPosition = u.Position.
    /// - Welt 1 (unbeteiligt): w.LaufeBis(() =&gt; u.Health &lt; u.MaxHealth, 10f); nicht gebissen:
    ///   Verstoß. Sonst w.LaufeBis(() =&gt; false, 0.2f); Verstoß, wenn !(bool)w.Call("IsFleeing", u)
    ///   („der vom Wildschwein gebissene Dorfbewohner flieht nicht“).
    /// - Welt 2 (gereizt): vor dem Lauf eber.ProvokedBy = u; eber.ProvokedSeconds = 100f. Dann
    ///   w.LaufeBis(() =&gt; u.Health &lt; u.MaxHealth, 10f); nicht gebissen: Verstoß. Sonst
    ///   w.LaufeBis(() =&gt; false, 0.2f); Verstoß, wenn (bool)w.Call("IsFleeing", u) („wer das
    ///   Wildschwein selbst gereizt hat, flieht - Locken unmöglich“).
    /// Ohne neue Verstöße: Console.WriteLine($"  ok  {wer}: der Unbeteiligte flieht, der Reizer nicht").
    /// </summary>
    static void Wildschwein(int karte, List<string> verstoesse)
    {
        var wer = $"Karte {karte}, Raubflucht Wildschwein";
        var vorher = verstoesse.Count;

        var w1 = Raubtiere.NeueWelt();
        var p1 = Raubtiere.Platz(w1);
        var t1 = w1.Map.GetTile(p1.X, p1.Y);
        t1.Food = FoodSource.Boar;
        t1.ResourceType = Resource.Food;
        t1.ResourceAmount = w1.Map.Settings.BoarFood;
        t1.Animal = new WildAnimal { Health = w1.Map.PredatorMaxHealth(FoodSource.Boar), WanderTimer = 100000f };
        w1.Map.RebuildWildAnimals();
        var eber1 = t1.Animal;
        var u1 = Raubtiere.Neu(w1, UnitType.Villager, p1.X + p1.D, p1.Y);
        u1.Job = null;
        u1.State = UnitState.Idle;
        u1.TargetPosition = u1.Position;

        w1.LaufeBis(() => u1.Health < u1.MaxHealth, 10f);
        if (u1.Health == u1.MaxHealth)
            verstoesse.Add($"{wer}: der unbeteiligte Dorfbewohner wird in 10 s nicht gebissen");
        else
        {
            w1.LaufeBis(() => false, 0.2f);
            if (!(bool)w1.Call("IsFleeing", u1))
                verstoesse.Add($"{wer}: der vom Wildschwein gebissene Dorfbewohner flieht nicht");
        }

        var w2 = Raubtiere.NeueWelt();
        var p2 = Raubtiere.Platz(w2);
        var t2 = w2.Map.GetTile(p2.X, p2.Y);
        t2.Food = FoodSource.Boar;
        t2.ResourceType = Resource.Food;
        t2.ResourceAmount = w2.Map.Settings.BoarFood;
        t2.Animal = new WildAnimal { Health = w2.Map.PredatorMaxHealth(FoodSource.Boar), WanderTimer = 100000f };
        w2.Map.RebuildWildAnimals();
        var eber2 = t2.Animal;
        var u2 = Raubtiere.Neu(w2, UnitType.Villager, p2.X + p2.D, p2.Y);
        u2.Job = null;
        u2.State = UnitState.Idle;
        u2.TargetPosition = u2.Position;
        eber2.ProvokedBy = u2;
        eber2.ProvokedSeconds = 100f;

        w2.LaufeBis(() => u2.Health < u2.MaxHealth, 10f);
        if (u2.Health == u2.MaxHealth)
            verstoesse.Add($"{wer}: der gereizte Dorfbewohner wird in 10 s nicht gebissen");
        else
        {
            w2.LaufeBis(() => false, 0.2f);
            if ((bool)w2.Call("IsFleeing", u2))
                verstoesse.Add($"{wer}: wer das Wildschwein selbst gereizt hat, flieht - Locken unmöglich");
        }

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: der Unbeteiligte flieht, der Reizer nicht");
    }

    /// <summary>
    /// Fall 5: Wer am Wolf vorbeiläuft und gebissen wird, läuft weiter - Bär und Wolf verfolgen
    /// niemanden, und ein Laufbefehl (etwa zu einem Bauplatz hinter dem Tier) darf nicht verloren
    /// gehen. Erst wer steht, flieht.
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht vorbeilaufen"; vorher = verstoesse.Count.
    /// w = Raubtiere.NeueWelt(); p = Raubtiere.Platz(w); wolf = Raubtiere.Aufstellen(w,
    /// FoodSource.Wolf, p); v = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D * 4, p.Y);
    /// ziel = w.Map.GridToWorld(new Vector2(p.X - p.D * 2, p.Y + 1)) (jenseits des Wolfs, 2,24
    /// Kacheln von ihm - außer Reichweite). Laufbefehl: v.Job = null; v.TargetPosition = ziel;
    /// v.Path = w.Map.FindPath(v.Position, ziel); v.State = UnitState.Moving. Ist v.Path leer:
    /// Console.WriteLine($"      {wer}: kein Weg am Wolf vorbei - übersprungen") und return.
    /// a) w.LaufeBis(() =&gt; v.Health &lt; v.MaxHealth, 15f). Nicht gebissen: Console.WriteLine(
    ///    $"      {wer}: im Vorbeilaufen nicht gebissen - übersprungen") und return.
    /// b) Verstoß, wenn (bool)w.Call("IsFleeing", v) („flieht beim Vorbeilaufen und vergisst den
    ///    Laufbefehl“) oder v.State != UnitState.Moving (mit v.State).
    /// c) zeit = w.LaufeBis(() =&gt; Vector2.Distance(v.Position, ziel) &lt;= 0.75f * w.Map.TileSize,
    ///    20f). Verstoß, wenn er danach nicht so nah am Ziel ist („kommt nicht am Ziel an“, mit
    ///    der Kachel w.Map.WorldToGrid(v.Position) und v.State).
    /// d) Verstoß, wenn w.Map.LocatePredator(wolf) != (p.X, p.Y) („der Wolf folgt“).
    /// Ohne neue Verstöße: Console.WriteLine($"  ok  {wer}: gebissen, läuft weiter, nach {zeit:0.0} s
    /// am Ziel, {v.Health}/{v.MaxHealth} LP").
    /// </summary>
    static void Vorbeilaufen(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Raubflucht vorbeilaufen";
        int vorher = verstoesse.Count;
        var w = Raubtiere.NeueWelt();
        var p = Raubtiere.Platz(w);
        var wolf = Raubtiere.Aufstellen(w, FoodSource.Wolf, p);
        var v = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D * 4, p.Y);
        var ziel = w.Map.GridToWorld(new Vector2(p.X - p.D * 2, p.Y + 1));
        v.Job = null;
        v.TargetPosition = ziel;
        v.Path = w.Map.FindPath(v.Position, ziel);
        v.State = UnitState.Moving;
        if (v.Path.Count == 0)
        {
            Console.WriteLine($"      {wer}: kein Weg am Wolf vorbei - übersprungen");
            return;
        }
        w.LaufeBis(() => v.Health < v.MaxHealth, 15f);
        if (v.Health == v.MaxHealth)
        {
            Console.WriteLine($"      {wer}: im Vorbeilaufen nicht gebissen - übersprungen");
            return;
        }
        if ((bool)w.Call("IsFleeing", v))
            verstoesse.Add($"{wer}: flieht beim Vorbeilaufen und vergisst den Laufbefehl");
        if (v.State != UnitState.Moving)
            verstoesse.Add($"{wer}: nach dem Biss im Laufen {v.State} statt Moving");
        float zeit = w.LaufeBis(() => Vector2.Distance(v.Position, ziel) <= 0.75f * w.Map.TileSize, 20f);
        if (Vector2.Distance(v.Position, ziel) > 0.75f * w.Map.TileSize)
            verstoesse.Add($"{wer}: kommt nicht am Ziel an (steht auf {w.Map.WorldToGrid(v.Position)}, {v.State})");
        if (w.Map.LocatePredator(wolf) != (p.X, p.Y))
            verstoesse.Add($"{wer}: der Wolf folgt - jetzt auf {w.Map.LocatePredator(wolf)}");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: gebissen, läuft weiter, nach {zeit:0.0} s am Ziel, {v.Health}/{v.MaxHealth} LP");
    }

    /// <summary>
    /// Fall 6: wer beim Speichern flieht, flieht geladen weiter, hat seine Arbeit noch im Kopf und
    /// tut danach genau dasselbe wie ohne Laden (gefunden 2026-11-13: die Gruppe speichern wurde
    /// rot, seit Raubtiere Flucht auslösen - der Fluchtzustand stand nicht im Spielstand).
    /// VERTRAG: wer = $"Karte {karte}, Raubflucht Spielstand". s = Szene(); null:
    /// Console.WriteLine($"      {wer}: kein Wald fern von Gebäuden - übersprungen") und return.
    /// (w, v, baum, wk, wolf) = s.Value.
    /// a) w.LaufeBis(() =&gt; (bool)w.Call("IsFleeing", v), 6f); flieht er dann nicht: Verstoß
    ///    „{wer}: Testaufbau - der Holzfäller flieht nicht“ und return.
    /// b) stand = (SaveGame)w.Call("CaptureSave"); b = new Welt(SaveGameStore.Deserialize(
    ///    SaveGameStore.Serialize(stand))); i = w.Map.Units.IndexOf(v); vb = b.Map.Units[i].
    ///    Verstoß, wenn !(bool)b.Call("IsFleeing", vb) („{wer}: geladen flieht er nicht mehr“);
    ///    Verstoß, wenn vb.ResumeJob null ist oder vb.ResumeJob.Source nicht v.ResumeJob.Source
    ///    („{wer}: geladen hat er seine Arbeit vergessen“).
    /// c) Beide Welten 30 s weiter: w.LaufeBis(() =&gt; false, 30f); b.LaufeBis(() =&gt; false, 30f).
    ///    Verstoß, wenn Vector2.Distance(v.Position, vb.Position) &gt; 1f („{wer}: nach 30 s steht er
    ///    ohne Laden bei {v.Position} ({v.State}), geladen bei {vb.Position} ({vb.State})“), und
    ///    wenn v.Job?.Source nicht gleich vb.Job?.Source ist („{wer}: ohne Laden sammelt er an
    ///    {v.Job?.Source}, geladen an {vb.Job?.Source}“). Verstoß, wenn v.Job null ist („{wer}:
    ///    Testaufbau - er arbeitet nach 30 s nicht wieder“).
    /// </summary>
    static void Spielstand(int karte, List<string> verstoesse)
    {
        var wer = $"Karte {karte}, Raubflucht Spielstand";
        var vorher = verstoesse.Count;
        if (Szene() is not { } s)
        {
            Console.WriteLine($"      {wer}: kein Wald fern von Gebäuden - übersprungen");
            return;
        }
        var (w, v, baum, wk, wolf) = s;

        w.LaufeBis(() => (bool)w.Call("IsFleeing", v), 6f);
        if (!(bool)w.Call("IsFleeing", v))
        {
            verstoesse.Add($"{wer}: Testaufbau - der Holzfäller flieht nicht");
            return;
        }

        var stand = (SaveGame)w.Call("CaptureSave");
        var b = new Welt(SaveGameStore.Deserialize(SaveGameStore.Serialize(stand)));
        var i = w.Map.Units.IndexOf(v);
        var vb = b.Map.Units[i];
        if (!(bool)b.Call("IsFleeing", vb))
            verstoesse.Add($"{wer}: geladen flieht er nicht mehr");
        if (vb.ResumeJob == null || vb.ResumeJob.Source != v.ResumeJob.Source)
            verstoesse.Add($"{wer}: geladen hat er seine Arbeit vergessen");

        w.LaufeBis(() => false, 30f);
        b.LaufeBis(() => false, 30f);
        if (Vector2.Distance(v.Position, vb.Position) > 1f)
            verstoesse.Add($"{wer}: nach 30 s steht er ohne Laden bei {v.Position} ({v.State}), geladen bei {vb.Position} ({vb.State})");
        if (v.Job?.Source != vb.Job?.Source)
            verstoesse.Add($"{wer}: ohne Laden sammelt er an {v.Job?.Source}, geladen an {vb.Job?.Source}");
        if (v.Job == null)
            verstoesse.Add($"{wer}: Testaufbau - er arbeitet nach 30 s nicht wieder");

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: geladen flieht er weiter und sammelt wie ohne Laden an {v.Job?.Source}");
    }
}
