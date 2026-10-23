using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;

/// <summary>
/// Gruppe raubtieranim (RT): Bären und Wölfe in Bewegung. Wunsch des Nutzers (2026-10-23): „zudem
/// müssen Bären und Wölfe animiert werden. Vorallem bei bewegung und Angriffen“ und „Beim Wolf und
/// Bären sollten es mehr sein als 8 da Beim Angriff auch mindest 8 zustäzlich generiert werden
/// müssen“. Gezeichnet wird headless nicht - geprüft wird, welche Phase der Spielbildschirm wählt:
/// beim Laufen die acht Phasen nach der gegangenen Strecke (PredatorWalkFrame), beim Angriff die acht
/// am Schlagtakt (PredatorAttackFrame) - Ausholen nur mit Opfer in Reichweite, der Biss genau mit dem
/// Schlag. Dass die Bilder geladen und gezeichnet werden, prüft checks/rt_raubtiere.py (rt4), wie
/// sie aussehen, ein Foto.
/// </summary>
static class RaubtierAnimation
{
    public static void Pruefen(int karte, List<string> verstoesse)
    {
        Zeitplan(karte, verstoesse);
        Takt(karte, verstoesse);
        Lauf(karte, verstoesse);
    }

    static int Phase(Welt w, WildAnimal tier) => (int)w.Call("PredatorAttackFrame", tier);

    // 1. Der Zeitplan selbst: nach dem Schlag 3 bis 7, davor mit Opfer 0 bis 2, sonst nichts
    static void Zeitplan(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsphasen nach Zeitplan";
        var w = new Welt();
        float schlag = TileMap.PREDATOR_STRIKE_SECONDS;
        float aus = (float)Welt.Konstante("PREDATOR_WINDUP_SECONDS");
        var faelle = new (string Was, WildAnimal Tier, int Soll)[]
        {
            ("eben zugeschlagen", new WildAnimal { Strike = schlag, AttackCooldown = 1.5f, Engaged = true }, 3),
            ("ein Fünftel des Schlags vorbei", new WildAnimal { Strike = schlag * 0.79f, Engaged = true }, 4),
            ("Schlag fast vorbei", new WildAnimal { Strike = 0.001f }, 7),
            ("Schlag vorbei, Opfer weg", new WildAnimal { Strike = 0f, AttackCooldown = 1f }, -1),
            ("ausholen, Anfang", new WildAnimal { Engaged = true, AttackCooldown = aus * 0.95f }, 0),
            ("ausholen, Mitte", new WildAnimal { Engaged = true, AttackCooldown = aus * 0.5f }, 1),
            ("ausholen, gleich trifft es", new WildAnimal { Engaged = true, AttackCooldown = aus * 0.05f }, 2),
            ("Opfer in Reichweite, Schlag noch fern", new WildAnimal { Engaged = true, AttackCooldown = aus * 2f }, -1),
            ("kein Opfer, holt nicht aus", new WildAnimal { Engaged = false, AttackCooldown = aus * 0.5f }, -1),
            ("mitten im Schritt holt es nicht aus", new WildAnimal { Engaged = true, AttackCooldown = aus * 0.5f, Glide = 0.3f }, -1),
        };
        foreach (var (was, tier, soll) in faelle)
        {
            int ist = Phase(w, tier);
            if (ist != soll)
                verstoesse.Add($"{wer}: {was} - Phase {ist}, erwartet {soll}");
        }
    }

    // 2. Ein Wolf beißt einen Dorfbewohner neben sich: der erste Biss ohne Ausholen, jeder weitere
    //    nach 0, 1, 2 - und der Biss (Phase 3) genau im Bild des Schlags. Geht er weg, holt der Wolf
    //    nicht mehr aus.
    static void Takt(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Angriffsphasen im Schlagtakt";
        var w = Raubtiere.NeueWelt();
        var p = Raubtiere.Platz(w);
        var wolf = Raubtiere.Aufstellen(w, FoodSource.Wolf, p);
        const float dt = 1f / 60f;
        // Eine Sekunde allein: kein Ausholen, kein Biss
        for (int i = 0; i < 60; i++)
        {
            w.Call("UpdatePredators", dt);
            if (Phase(w, wolf) >= 0)
            {
                verstoesse.Add($"{wer}: ohne Opfer zeigt der Wolf Angriffsphase {Phase(w, wolf)}");
                return;
            }
        }
        var dorfi = Raubtiere.Neu(w, UnitType.Villager, p.X + p.D, p.Y);
        var folge = new List<(int Phase, bool Schlag)>();
        float vorher = wolf.Strike;
        for (int i = 0; i < 60 * 5; i++)
        {
            w.Call("UpdatePredators", dt);
            folge.Add((Phase(w, wolf), wolf.Strike > vorher + 1e-4f));
            vorher = wolf.Strike;
        }
        var schlaege = Enumerable.Range(0, folge.Count).Where(i => folge[i].Schlag).ToList();
        if (schlaege.Count < 3)
        {
            verstoesse.Add($"{wer}: in 5 Sekunden nur {schlaege.Count} Bisse - der Takt stimmt nicht");
            return;
        }
        if (schlaege[0] != 0)
            verstoesse.Add($"{wer}: der erste Biss kam erst im Bild {schlaege[0]} statt sofort");
        foreach (int i in schlaege)
            if (folge[i].Phase != 3)
                verstoesse.Add($"{wer}: im Bild des Schlags zeigt der Wolf Phase {folge[i].Phase} statt den Biss (3)");
        // Nach jedem Biss 3 bis 7 aufsteigend, alle fünf
        foreach (int i in schlaege)
        {
            var danach = folge.Skip(i).TakeWhile(f => f.Phase >= 3).Select(f => f.Phase).ToList();
            if (!danach.Zip(danach.Skip(1)).All(z => z.Second >= z.First) || danach.Distinct().Count() != 5)
                verstoesse.Add($"{wer}: nach dem Biss im Bild {i} nicht 3 bis 7 der Reihe nach: {string.Join(",", danach.Distinct())}");
        }
        // Vor jedem weiteren Biss 0, 1, 2 der Reihe nach, direkt davor 2
        foreach (int i in schlaege.Skip(1))
        {
            var davor = folge.Take(i).Reverse().TakeWhile(f => f.Phase is >= 0 and <= 2).Select(f => f.Phase).Reverse().ToList();
            if (davor.Count == 0 || davor[^1] != 2 || davor.Distinct().Count() != 3
                || !davor.Zip(davor.Skip(1)).All(z => z.Second >= z.First))
                verstoesse.Add($"{wer}: vor dem Biss im Bild {i} holt der Wolf nicht mit 0, 1, 2 aus: {string.Join(",", davor.Distinct())}");
        }
        // Der Dorfbewohner geht weg: zu Ende beißen ja, ausholen nein
        dorfi.Position = w.Map.GridToWorld(new Vector2(p.X + p.D * 6, p.Y));
        var weg = new List<int>();
        for (int i = 0; i < 60 * 3; i++)
        {
            w.Call("UpdatePredators", dt);
            weg.Add(Phase(w, wolf));
        }
        if (weg.Any(f => f is >= 0 and <= 2))
            verstoesse.Add($"{wer}: ohne Opfer in Reichweite holt der Wolf trotzdem aus");
        if (weg.Skip((int)(60 * TileMap.PREDATOR_STRIKE_SECONDS) + 1).Any(f => f >= 0))
            verstoesse.Add($"{wer}: ohne Opfer zeigt der Wolf nach dem letzten Biss weiter Angriffsphasen");
    }

    // 3. Laufen: acht Phasen der Reihe nach, so viele Doppelschritte, wie die Schrittlänge auf die
    //    Strecke passt (die Pfoten rutschen nicht), im Stand -1. Gerade und schräg.
    static void Lauf(int karte, List<string> verstoesse)
    {
        foreach (var (art, laenge) in new[] { (FoodSource.Bear, "BEAR_STRIDE"), (FoodSource.Wolf, "WOLF_STRIDE") })
            foreach (var (fx, fy) in new[] { (-1, 0), (1, 1) })
            {
                string wer = $"Karte {karte}, {art} läuft ({fx}, {fy})";
                var w = Raubtiere.NeueWelt();
                var p = Raubtiere.Platz(w);
                var tier = Raubtiere.Aufstellen(w, art, p);
                var tile = w.Map.GetTile(p.X, p.Y);
                float stride = (float)Welt.Konstante(laenge);
                tier.FromX = fx;
                tier.FromY = fy;
                var folge = new List<int>();
                for (float g = TileMap.WILD_STEP_SECONDS; g > 0f; g -= 1f / 120f)
                {
                    tier.Glide = g;
                    folge.Add((int)w.Call("PredatorWalkFrame", tile));
                }
                tier.Glide = 0f;
                if ((int)w.Call("PredatorWalkFrame", tile) != -1)
                    verstoesse.Add($"{wer}: im Stand nicht das Standbild (-1)");
                if (folge[0] != 0)
                    verstoesse.Add($"{wer}: der Schritt beginnt mit Phase {folge[0]} statt 0");
                if (folge.Any(f => f < 0 || f > 7))
                    verstoesse.Add($"{wer}: Phase außerhalb von 0 bis 7");
                int wechsel = 0;
                for (int i = 1; i < folge.Count; i++)
                {
                    if (folge[i] == folge[i - 1]) continue;
                    wechsel++;
                    if (folge[i] != (folge[i - 1] + 1) % 8)
                        verstoesse.Add($"{wer}: Sprung von Phase {folge[i - 1]} nach {folge[i]}");
                }
                float strecke = w.Map.TileSize * MathF.Sqrt(fx * fx + fy * fy);
                float soll = strecke / (2f * stride) * 8f;
                if (Math.Abs(wechsel - soll) > 1.5f)
                    verstoesse.Add($"{wer}: {wechsel} Phasenwechsel auf {strecke:0} Welteinheiten, bei Schrittlänge {stride} erwartet etwa {soll:0.0} - die Pfoten rutschen");
            }
    }
}
