using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;
using Resource = AoE.Core.Entities.Resource;
using UnitType = AgeOfEvolutions.Core.Data.UnitType;

/// <summary>
/// Gruppe jagd (F11, Nutzer 2026-11-04): „Reh flieht vor Dorfbewohnern und muss erst
/// erlegt werden", „Wildschwein greift den Jäger an und lässt sich zum Stadtzentrum
/// locken" und „Erlegtes Wild verliert langsam Nahrung". Die Abläufe: ein Dorfbewohner
/// erlegt das fliehende Reh per Klick und zerlegt das Aas, ein Wildschwein beißt zu,
/// ein Bogenschütze lockt es über die Karte, und Aas schrumpft.
/// </summary>
static class Jagd
{
    public static void Pruefen(int karte, List<string> verstoesse)
    {
        RehFluchtUndJagd(karte, verstoesse);
        EberWehrtSich(karte, verstoesse);
        EberLaesstSichLocken(karte, verstoesse);
        AasVerdirbt(karte, verstoesse);
    }

    /// <summary>
    /// Stellt Reh oder Wildschwein auf eine freie Wiese und meldet es im Wander-Bestand
    /// an. WanderTimer groß - ohne Anlass steht das Tier, mit Anlass im Flucht- bzw.
    /// Verfolgungstakt (F11).
    /// </summary>
    static WildAnimal Wild(Welt w, FoodSource art, int x, int y)
    {
        var t = w.Map.GetTile(x, y);
        t.Food = art;
        t.ResourceType = Resource.Food;
        t.ResourceAmount = art == FoodSource.Deer ? w.Map.Settings.DeerFood : w.Map.Settings.BoarFood;
        t.Animal = new WildAnimal { Health = w.Map.PredatorMaxHealth(art), WanderTimer = 100000f };
        w.Map.RebuildWildAnimals();
        return t.Animal;
    }

    // 1. Ein Dorfbewohner klickt das Reh: kein Ernteauftrag, sondern Jagd. Das Reh
    //    flieht, wird erlegt und liegt als Fleisch da, das der Jäger zerlegt.
    static void RehFluchtUndJagd(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Reh-Flucht und Jagd";
        int vorher = verstoesse.Count;
        TileMap.TestNoFog = true;
        try
        {
            var w = Raubtiere.NeueWelt();
            var p = Raubtiere.Platz(w);
            var reh = Wild(w, FoodSource.Deer, p.X, p.Y);
            var jaeger = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D * 3, p.Y);
            jaeger.Job = null;
            jaeger.State = UnitState.Idle;

            w.Call("IssueCommand", 0, new List<Unit> { jaeger }, new Vector2(p.X, p.Y));
            if (!ReferenceEquals(jaeger.PreyTarget, reh))
            {
                verstoesse.Add($"{wer}: ein Dorfbewohner greift das Reh per Klick nicht an (Job={jaeger.Job})");
                return;
            }
            if (jaeger.Job != null)
                verstoesse.Add($"{wer}: der Dorfbewohner hat trotz Jagd einen Ernteauftrag aufs lebende Reh");

            var start = w.Map.LocatePredator(reh).Value;
            w.LaufeBis(() => reh.Slaughtered || reh.Fleeing, 20f);
            if (!reh.Slaughtered && !reh.Fleeing)
                verstoesse.Add($"{wer}: das Reh flieht nicht vor dem heranlaufenden Jäger");

            float zeit = w.LaufeBis(() => reh.Slaughtered, 120f);
            if (!reh.Slaughtered)
            {
                verstoesse.Add($"{wer}: ein Dorfbewohner erlegt das Reh nicht in 120 s (LP {reh.Health}, {jaeger.State})");
                return;
            }
            var lage = w.Map.LocatePredator(reh).Value;
            if ((lage.X, lage.Y) == (start.X, start.Y))
                verstoesse.Add($"{wer}: das Reh stand die ganze Jagd still ({start})");
            var t = w.Map.GetTile(lage.X, lage.Y);
            if (t.ResourceType != Resource.Food || t.ResourceAmount <= 0)
            {
                verstoesse.Add($"{wer}: das erlegte Reh liegt nicht als Fleisch da ({t.ResourceType}, {t.ResourceAmount})");
                return;
            }
            int vor = t.ResourceAmount;
            w.LaufeBis(() => t.ResourceAmount < vor, 30f);
            if (t.ResourceAmount >= vor)
                verstoesse.Add($"{wer}: der Jäger zerlegt das erlegte Reh nicht ({t.ResourceAmount}/{vor})");
            if (verstoesse.Count == vorher)
                Console.WriteLine($"  ok  {wer}: Reh geflohen, nach {zeit:0.0} s erlegt, Aas wird zerlegt ({vor} → {t.ResourceAmount})");
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    // 2. Ein Wildschwein beißt jeden, der ihm zu nahe kommt - auch den Dorfbewohner,
    //    der es jagen will.
    static void EberWehrtSich(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Wildschwein wehrt sich";
        int vorher = verstoesse.Count;
        TileMap.TestNoFog = true;
        try
        {
            var w = Raubtiere.NeueWelt();
            var p = Raubtiere.Platz(w);
            var eber = Wild(w, FoodSource.Boar, p.X, p.Y);
            var dorfie = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D, p.Y);
            dorfie.Job = null;
            dorfie.State = UnitState.Idle;
            int lp = dorfie.Health;
            float zeit = w.LaufeBis(() => dorfie.Health < lp, 15f);
            if (dorfie.Health >= lp)
            {
                verstoesse.Add($"{wer}: das Wildschwein beißt den Dorfbewohner direkt neben sich nicht ({dorfie.Health}/{lp})");
                return;
            }
            if (verstoesse.Count == vorher)
                Console.WriteLine($"  ok  {wer}: Biss nach {zeit:0.0} s ({lp} → {dorfie.Health} LP), Eber steht noch auf (LP {eber.Health})");
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    // 3. Locken: ein Bogenschütze schießt von fern auf den Eber - der vergisst seinen
    //    Wandertakt und trabt dem Schützen nach (bis vors Stadtzentrum). Geprüft wird die
    //    Entfernung zum Schützen selbst: der läuft erst in Reichweite und steht auf
    //    manchen Karten seitlich vom Aufstellungspunkt.
    static void EberLaesstSichLocken(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Wildschwein locken";
        int vorher = verstoesse.Count;
        TileMap.TestNoFog = true;
        try
        {
            var w = Raubtiere.NeueWelt();
            var p = Raubtiere.Platz(w);
            var eber = Wild(w, FoodSource.Boar, p.X, p.Y);
            var schuetze = Raubtiere.Neu(w, UnitType.Archer, p.X + p.D * 5, p.Y);
            w.Call("IssueCommand", 0, new List<Unit> { schuetze }, new Vector2(p.X, p.Y));
            var start = w.Map.LocatePredator(eber).Value;
            float anfang = DistanzZu(w, eber, schuetze);
            if (anfang < 3f)
            {
                verstoesse.Add($"{wer}: der Schütze steht zu nah am Eber zum Locken ({anfang:0.0})");
                return;
            }
            // Erfolg: der Eber steht direkt am Schützen. Weil der aus 4 Kacheln schießt,
            // kann der Eber nur von selbst herangekommen sein.
            float zeit = w.LaufeBis(() => schuetze.State == UnitState.Dead
                                          || DistanzZu(w, eber, schuetze) is var d && d >= 0f && d <= 1.5f, 60f);
            if (schuetze.State == UnitState.Dead)
            {
                verstoesse.Add($"{wer}: der Eber erlegt den Schützen, ehe der Locken zeigt ({eber.Health} LP Eber)");
                return;
            }
            float jetzt = DistanzZu(w, eber, schuetze);
            if (jetzt < 0f || jetzt > 1.5f)
            {
                verstoesse.Add($"{wer}: der beschossene Eber folgt dem Schützen nicht " +
                               $"({anfang:0.0} → {jetzt:0.0} Kacheln, Eber {eber.Health} LP, {schuetze.State})");
                return;
            }
            if (w.Map.LocatePredator(eber).Value == start && zeit > 1f)
            {
                verstoesse.Add($"{wer}: der Eber steht nur in Reichweite des Schützen, gefolgt ist er nicht");
                return;
            }
            if (verstoesse.Count == vorher)
                Console.WriteLine($"  ok  {wer}: Eber folgt nach {zeit:0.0} s auf {jetzt:0.0} Kacheln an den Schützen");
        }
        finally
        {
            TileMap.TestNoFog = false;
        }
    }

    /// <summary>Luftlinie vom Tier zur Einheit in Kacheln; -1, wenn die Karte das Tier nicht mehr kennt.</summary>
    static float DistanzZu(Welt w, WildAnimal tier, Unit einheit)
    {
        if (w.Map.LocatePredator(tier) is not { } lage)
            return -1f;
        var mitte = w.Map.GridToWorld(new Vector2(lage.X, lage.Y));
        return Vector2.Distance(mitte, einheit.Position) / w.Map.TileSize;
    }

    // 4. Erlegtes Wild verdirbt: liegt es unangetastet, geht die Nahrung langsam weg.
    static void AasVerdirbt(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Aas verdirbt";
        int vorher = verstoesse.Count;
        var w = Raubtiere.NeueWelt();
        var p = Raubtiere.Platz(w);
        var reh = Wild(w, FoodSource.Deer, p.X, p.Y);
        if (!w.Map.DamageAnimal(p.X, p.Y, 9999))
        {
            verstoesse.Add($"{wer}: DamageAnimal erlegt das Reh nicht");
            return;
        }
        var t = w.Map.GetTile(p.X, p.Y);
        int anfang = t.ResourceAmount;
        // Kein Sammler in der Nähe: niemand frisst am Aas - der Verderb allein wirkt.
        w.LaufeBis(() => false, 150f);
        int schwund = anfang - t.ResourceAmount;
        if (schwund < 40)
        {
            verstoesse.Add($"{wer}: in 150 s fehlen nur {schwund} Nahrung (erwartet rund 60)");
            return;
        }
        int stufe = w.Map.MeatStage(p.X, p.Y);
        if (stufe < 1)
            verstoesse.Add($"{wer}: unter drei Viertel fehlt die Aasstufe (Nahrung {t.ResourceAmount}/{anfang}, Stufe {stufe})");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {anfang} → {t.ResourceAmount} Nahrung in 60 s, Stufe {stufe}");
    }
}
