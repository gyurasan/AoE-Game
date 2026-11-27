using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using AttackAlerts = AoE.Core.Combat.AttackAlerts;
using AttackSpot = AoE.Core.Combat.AttackSpot;
using UnitState = AoE.Core.Entities.UnitState;

/// <summary>
/// Angriffsort auf der Minikarte (AM1, Nutzer 2026-11-27: „Wird man angegriffen sollte auf der
/// Minikarte angezeigt werden wo der Angriff stattfindet. Es muss nicht genau sein und kann mit
/// einem kleinen roten leicht durchsichtigem Kreis dargestellt werden“): jeder Treffer gegen
/// Spieler 0 meldet seinen Ort an das Feld _attackAlerts - beim Getroffenen, nicht beim
/// Angreifer, in Kacheln. Geprüft an allen Trefferwegen, die Spieler 0 erreichen können:
/// Schuss eines Bogenschützen, Schlag gegen ein Gebäude (Ort = Gebäudemitte), Pfeil eines
/// feindlichen Wachturms, Biss eines Wolfs; ein Treffer gegen Spieler 1 legt keinen Ort an.
/// Den Kreis selbst zeichnet DrawMinimap - das sieht nur das Foto.
/// </summary>
static class Angriffsort
{
    // So nah muss der Ort am Getroffenen liegen (Kacheln) - im Bild des Treffers gemessen
    const float NAH = 0.5f;

    public static void Pruefen(int karte, List<string> verstoesse)
    {
        Fernschuss(karte, verstoesse);
        GegnerGetroffen(karte, verstoesse);
        Gebaeude(karte, verstoesse);
        Turm(karte, verstoesse);
        Wolf(karte, verstoesse);
    }

    /// <summary>Eine Welt ohne Tiere und ohne die Einheiten der Startdörfer - nur die
    /// Rollen des Ablaufs werden getroffen.</summary>
    static Welt NeueWelt()
    {
        var w = Raubtiere.NeueWelt();
        foreach (var u in w.Map.Units.ToList())
        {
            (u.OwnerId == 0 ? w.P1 : w.P2).RemoveUnit(u);
            w.Map.Units.Remove(u);
        }
        return w;
    }

    static AttackAlerts Orte(Welt w) => (AttackAlerts)w.Get("_attackAlerts");

    static Unit Neu(Welt w, UnitType typ, int besitzer, int x, int y)
    {
        var u = w.Map.AddUnit(typ, x, y, besitzer);
        u.TargetPosition = u.Position;
        u.State = UnitState.Idle;
        (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
        return u;
    }

    static void Gehe(Welt w, Unit u, int x, int y)
    {
        u.TargetPosition = w.Map.GridToWorld(new Vector2(x, y));
        u.Path = w.Map.FindPath(u.Position, u.TargetPosition);
        u.State = u.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
    }

    static string Liste(AttackAlerts a)
        => a.Spots.Count == 0 ? "keiner" : string.Join(", ", a.Spots.Select(s => $"({s.X:0.0}, {s.Y:0.0})"));

    /// <summary>Der Ort, der <paramref name="soll"/> (Kacheln) am nächsten liegt, oder null.</summary>
    static AttackSpot? Naechster(AttackAlerts a, Vector2 soll)
        => a.Spots.Count == 0 ? null : a.Spots.MinBy(s => Vector2.Distance(new Vector2(s.X, s.Y), soll));

    /// <summary>Prüft, dass genau ein Ort besteht und er höchstens NAH Kacheln von
    /// <paramref name="soll"/> liegt; Rückgabe true, wenn ja.</summary>
    static bool EinOrtBei(Welt w, string wer, string was, Vector2 soll, List<string> verstoesse)
    {
        var a = Orte(w);
        if (a.Spots.Count != 1)
        {
            verstoesse.Add($"{wer}: nach {was} {a.Spots.Count} Angriffsorte ({Liste(a)}), erwartet einer bei ({soll.X:0.0}, {soll.Y:0.0})");
            return false;
        }
        var s = a.Spots[0];
        float d = Vector2.Distance(new Vector2(s.X, s.Y), soll);
        if (d > NAH)
        {
            verstoesse.Add($"{wer}: nach {was} liegt der Angriffsort bei ({s.X:0.0}, {s.Y:0.0}), {d:0.0} Kacheln vom Getroffenen ({soll.X:0.0}, {soll.Y:0.0}) - Ort in Kacheln, beim Getroffenen");
            return false;
        }
        return true;
    }

    static Vector2 Kachel(Welt w, Unit u) => u.Position / w.Map.TileSize;

    // 1. Ein feindlicher Bogenschütze trifft aus der Ferne: der Ort liegt beim Dorfbewohner.
    static void Fernschuss(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsort Fernschuss";
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            var p = Raubtiere.Platz(w);
            var dorf = Neu(w, UnitType.Villager, 0, p.X, p.Y);
            var schuetze = Neu(w, UnitType.Archer, 1, p.X + p.D * 5, p.Y);
            if (Orte(w).Spots.Count != 0)
                verstoesse.Add($"{wer}: vor dem ersten Treffer schon Angriffsorte ({Liste(Orte(w))})");
            w.Call("AttackUnit", new List<Unit> { schuetze }, dorf);
            Vector2 bei = Vector2.Zero, schuss = Vector2.Zero;
            float zeit = w.LaufeBis(() =>
            {
                bei = Kachel(w, dorf);
                schuss = Kachel(w, schuetze);
                return dorf.Health < dorf.MaxHealth;
            }, 20f);
            if (dorf.Health >= dorf.MaxHealth)
            {
                verstoesse.Add($"{wer}: der Bogenschütze trifft in 20 s nicht ({schuetze.State}) - Aufbau nicht möglich");
                return;
            }
            if (Vector2.Distance(bei, schuss) < 2f)
            {
                verstoesse.Add($"{wer}: Aufbau - der Schütze steht beim Treffer nur {Vector2.Distance(bei, schuss):0.0} Kacheln entfernt");
                return;
            }
            if (EinOrtBei(w, wer, "dem Schuss", bei, verstoesse))
                Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s getroffen, Ort beim Dorfbewohner, {Vector2.Distance(bei, schuss):0.0} Kacheln vom Schützen");
        }
        finally { TileMap.TestNoFog = false; }
    }

    // 2. Unsere Miliz trifft einen feindlichen Dorfbewohner: kein Ort - nur Spieler 0 wird gewarnt.
    static void GegnerGetroffen(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsort Gegner getroffen";
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            var p = Raubtiere.Platz(w);
            var feind = Neu(w, UnitType.Villager, 1, p.X, p.Y);
            var miliz = Neu(w, UnitType.Militia, 0, p.X + p.D * 3, p.Y);
            w.Call("AttackUnit", new List<Unit> { miliz }, feind);
            float zeit = w.LaufeBis(() => feind.Health < feind.MaxHealth, 20f);
            if (feind.Health >= feind.MaxHealth)
            {
                verstoesse.Add($"{wer}: die Miliz trifft in 20 s nicht ({miliz.State}) - Aufbau nicht möglich");
                return;
            }
            if (miliz.Health < miliz.MaxHealth)
            {
                verstoesse.Add($"{wer}: Aufbau - der feindliche Dorfbewohner wehrt sich");
                return;
            }
            if (Orte(w).Spots.Count != 0)
                verstoesse.Add($"{wer}: ein Treffer gegen Spieler 1 legt Angriffsorte an ({Liste(Orte(w))}) - nur Spieler 0 wird gewarnt");
            else
                Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s getroffen, kein Angriffsort");
        }
        finally { TileMap.TestNoFog = false; }
    }

    // 3. Eine feindliche Miliz schlägt auf unser Haus: der Ort ist die Hausmitte.
    static void Gebaeude(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsort Gebäude";
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            var p = Raubtiere.Platz(w);
            var haus = w.Map.AddBuilding(p.X, p.Y, "Haus", 0, 2);
            var miliz = Neu(w, UnitType.Militia, 1, p.X + p.D * 4, p.Y);
            int lp = haus.Health;
            w.Call("AttackBuilding", new List<Unit> { miliz }, haus);
            float zeit = w.LaufeBis(() => haus.Health < lp, 20f);
            if (haus.Health >= lp)
            {
                verstoesse.Add($"{wer}: die Miliz schlägt in 20 s nicht aufs Haus ({miliz.State}) - Aufbau nicht möglich");
                return;
            }
            var mitte = new Vector2(haus.X + haus.Width / 2f, haus.Y + haus.Height / 2f);
            if (EinOrtBei(w, wer, "dem Schlag aufs Haus", mitte, verstoesse))
                Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s getroffen, Ort in der Hausmitte, Angreifer {Vector2.Distance(Kachel(w, miliz), mitte):0.0} Kacheln davon");
        }
        finally { TileMap.TestNoFog = false; }
    }

    // 4. Ein feindlicher Wachturm schießt: der Ort liegt beim Getroffenen, nicht am Turm.
    static void Turm(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsort Turm";
        TileMap.TestNoFog = true;
        try
        {
            var w = NeueWelt();
            RTSGameplayScreen.TestBombard = true;   // die Welt baut ohne Beschuss (F1)
            var p = Raubtiere.Platz(w);
            var turm = w.Map.AddBuilding(p.X, p.Y, "Wachturm", 1, 2);
            var dorf = Neu(w, UnitType.Villager, 0, p.X + p.D * 4, p.Y);
            Vector2 bei = Vector2.Zero;
            float zeit = w.LaufeBis(() => { bei = Kachel(w, dorf); return dorf.Health < dorf.MaxHealth; }, 8f);
            if (dorf.Health >= dorf.MaxHealth)
            {
                verstoesse.Add($"{wer}: der Wachturm trifft den Dorfbewohner in 8 s nicht - Aufbau nicht möglich");
                return;
            }
            var mitte = new Vector2(turm.X + turm.Width / 2f, turm.Y + turm.Height / 2f);
            if (EinOrtBei(w, wer, "dem Turmschuss", bei, verstoesse))
                Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s getroffen, Ort beim Dorfbewohner, {Vector2.Distance(bei, mitte):0.0} Kacheln von der Turmmitte");
        }
        finally
        {
            RTSGameplayScreen.TestBombard = false;
            TileMap.TestNoFog = false;
        }
    }

    // 5. Ein Wolf beißt: auch das ist ein Angriff, der Ort liegt beim Gebissenen.
    static void Wolf(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsort Wolf";
        var w = NeueWelt();
        var p = Raubtiere.Platz(w);
        Raubtiere.Aufstellen(w, FoodSource.Wolf, p);
        var dorf = Neu(w, UnitType.Villager, 0, p.X + p.D * 3, p.Y);
        Gehe(w, dorf, p.X + p.D, p.Y);
        Vector2 bei = Vector2.Zero;
        float zeit = w.LaufeBis(() => { bei = Kachel(w, dorf); return dorf.Health < dorf.MaxHealth; }, 20f);
        if (dorf.Health >= dorf.MaxHealth)
        {
            verstoesse.Add($"{wer}: der Wolf beißt in 20 s nicht - Aufbau nicht möglich");
            return;
        }
        var wolf = new Vector2(p.X + 0.5f, p.Y + 0.5f);
        if (EinOrtBei(w, wer, "dem Biss", bei, verstoesse))
            Console.WriteLine($"  ok  {wer}: nach {zeit:0.0} s gebissen, Ort beim Dorfbewohner, {Vector2.Distance(bei, wolf):0.0} Kacheln vom Wolf");
    }
}
