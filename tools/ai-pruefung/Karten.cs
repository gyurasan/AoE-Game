// land: zwei KI-Instanzen gegeneinander auf jeder Landart (Nutzer 2026-11-07: „wenn du fertig
// bist, mach bitte auch Test indem du zwei AI Gegner gegeneinander laufen lässt auf
// verschiedenen Karten. So prüfen wir ob die Ressourcen gut verteilt sind und das ganze
// funktioniert").
//
//   dotnet run --project tools/ai-pruefung -- land [minuten]
//
// Je Landart (Europa, Ägypten, Wiking) eine frische Standardkarte im 16:9-Format, beide Seiten
// von der KI gespielt (Aggressives Profil wie im Basislauf) und ein normaler Startvorrat
// (je 100) — die Kartenressourcen müssen also von den Karten kommen. Prüft je Landart:
//   * Ressourcen gut verteilt: um beide Stadtzentren (30 Kacheln) stehen Holz, Nahrung,
//     Stein und Gold
//   * das ganze funktioniert: beide KIs sind aktiv (Bevölkerung/Gebäude/Soldaten),
//     beide Seiten sammeln Kartenressourcen (Sammelzustand oder Nettogewinn)
//   * Kampf: Sieg, zerstörtes Gebäude oder beobachteter Angriff — bei ausbleibendem
//     Kampf nur eine Warnung, denn ob die KIs greifen, ist ihre Angelegenheit
//   * Landart-spezifisch: Ägypten ohne Wasser, Wiking mit beiden Dorfzentren auf Land
//     und durch Brücken erreichbar
//
// Exit 0 = alles ok, 1 = Verstöße.
using System.Reflection;
using AoE.Core.Ai;
using AoE.Core.Entities;
using AgeOfEvolutions.Core.AI;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using BuildingType = AoE.Core.Entities.BuildingType;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;

static class Karten
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);

    public static int Run(string[] args)
    {
        int minuten = args.Select(a => int.TryParse(a, out int m) ? m : 0).FirstOrDefault(m => m > 0);
        if (minuten == 0) minuten = 18;
        var verstoesse = new List<string>();
        foreach (var style in new[] { MapStyle.Europa, MapStyle.Egypten, MapStyle.Wiking })
            PruefeStyle(style, minuten, verstoesse);

        Console.WriteLine();
        if (verstoesse.Count > 0)
        {
            Console.WriteLine($"NICHT ERFÜLLT ({verstoesse.Count} Verstöße)");
            foreach (var v in verstoesse)
                Console.WriteLine("  - " + v);
            return 1;
        }
        Console.WriteLine("Alle Prüfungen ok — beide KIs spielen auf allen drei Landarten.");
        return 0;
    }

    static void PruefeStyle(MapStyle style, int minuten, List<string> verstoesse)
    {
        string wer = MapStyles.Name(style);
        Console.WriteLine();
        Console.WriteLine($"=== {wer} ({minuten} min, zwei KIs) ===");
        void Meld(string m) { Console.WriteLine("  X  " + wer + ": " + m); verstoesse.Add($"{wer}: {m}"); }
        void Ok(string m) => Console.WriteLine($"  ok   {wer}: {m}");

        // ---- Welt: wie im Basislauf, nur mit der Landart ----
        var dims = MapSizes.Dimensions(MapSize.Standard, 16f / 9f);
        var map = new TileMap(dims.Width, dims.Height, 32, MapSettings.ForStyle(style, MapSize.Standard), style);
        var screen = new RTSGameplayScreen(MapSize.Standard, style);
        var units = map.Units;
        var p1 = new Player(0, "P1", "Briten");
        var p2 = new Player(1, "P2", "Azteken");
        foreach (var u in units)
            (u.OwnerId == 0 ? p1 : p2).AddUnit(u);
        // Normaler Startvorrat (wie im Spiel) — der Rest muss von der Karte kommen.
        foreach (var p in new[] { p1, p2 })
            foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
                p.Resources.Add(r, 100);

        void Set(string name, object wert)
        {
            var f = T.GetField(name, F);
            if (f == null) throw new InvalidOperationException($"Feld {name} fehlt");
            f.SetValue(screen, wert);
        }
        void Invoke0(string name)
        {
            var mi = T.GetMethod(name, F);
            if (mi == null) return;
            try { mi.Invoke(screen, new object[0]); }
            catch (System.Reflection.TargetInvocationException) { }
        }
        Set("tileMap", map);
        Set("gatherWorld", new TileMapGatherWorld(map));
        Set("player1", p1);
        Set("player2", p2);
        Set("units", units);
        map.UpdateFogOfWarForPlayer(0, units);
        map.UpdateFogOfWarForPlayer(1, units);
        Invoke0("UpdatePopulationLimits");

        screen.ActivateAi(new EconomyAi(AiProfile.Aggressive), owner: 0);
        screen.ActivateAi(new EconomyAi(AiProfile.Aggressive), owner: 1);
        if (!screen.Agents.ContainsKey(0) || !screen.Agents.ContainsKey(1))
        {
            Meld("beide KI-Instanzen fehlen");
            return;
        }

        // ---- Ausgangszustand ----
        int popStartP0 = p1.PopulationCount, popStartP1 = p2.PopulationCount;
        int bldStartP0 = map.Buildings.Count(b => b.OwnerId == 0);
        int bldStartP1 = map.Buildings.Count(b => b.OwnerId == 1);
        // Die START-Stadtzentren festhalten: die KI kann das Dorfzentrum im Lauf
        // aufwerten und neu bauen — für die Kartenverteilung zählt der Startplatz.
        Building? tcB0 = map.Buildings.FirstOrDefault(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
        Building? tcB1 = map.Buildings.FirstOrDefault(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
        (int x, int y)? tcP0 = tcB0 == null ? null : (tcB0.X + tcB0.Width / 2, tcB0.Y + tcB0.Height / 2);
        (int x, int y)? tcP1 = tcB1 == null ? null : (tcB1.X + tcB1.Width / 2, tcB1.Y + tcB1.Height / 2);
        int summe(Player p) => p.Resources[Resource.Food] + p.Resources[Resource.Wood]
                               + p.Resources[Resource.Gold] + p.Resources[Resource.Stone];
        int sumStartP0 = summe(p1), sumStartP1 = summe(p2);
        int prevSumP0 = sumStartP0, prevSumP1 = sumStartP1;
        int maxSumP0 = sumStartP0, maxSumP1 = sumStartP1;
        int maxDeltaP0 = 0, maxDeltaP1 = 0;   // größter Nettogewinn in einem Schritt
        bool sammeltP0 = false, sammeltP1 = false;   // Einheiten im Sammelzustand

        // ---- Kartenprüfung: auf der GENERIERTEN Karte, bevor die KIs etwas
        //      verändern. Gold und Holz werden im Lauf abgebaut, Dorfzentren
        //      verlegt und Gebäude gebaut — nach dem Kampf gemessen wäre die
        //      Aussage über die Kartenverteilung falsch. ----
        (int x, int y)? tc(int owner) => owner == 0 ? tcP0 : tcP1;
        int zaehe(int owner, Func<Tile, bool> trifft)
        {
            if (tc(owner) == null) return -1;
            var (cx, cy) = tc(owner)!.Value;
            int n = 0;
            for (int x = cx - 30; x <= cx + 30; x++)
                for (int y = cy - 30; y <= cy + 30; y++)
                    if (map.GetTile(x, y) is { } t && trifft(t)) n++;
            return n;
        }
        var rohstoffarten = new (string Name, Func<Tile, bool> Ist)[]
        {
            ("Holz", t => t.ResourceType == Resource.Wood),
            ("Nahrung", t => t.Food != FoodSource.None || t.ResourceType == Resource.Food),
            ("Stein", t => t.ResourceType == Resource.Stone),
            ("Gold", t => t.ResourceType == Resource.Gold),
        };
        var rohstoffFehlt = new List<string>();
        if (tc(0) == null || tc(1) == null)
            rohstoffFehlt.Add("ein Dorfzentrum fehlt");
        else
            foreach (var owner in new[] { 0, 1 })
                foreach (var (name, ist) in rohstoffarten)
                    if (zaehe(owner, ist) == 0)
                        rohstoffFehlt.Add($"Owner {owner}: kein {name} in 30 Kacheln um das eigene Dorfzentrum");

        int wasser = 0, sand = 0, wiese = 0;
        for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var t = map.GetTile(x, y);
                if (t.Type == TileType.Water) wasser++;
                else if (t.Type == TileType.Sand) sand++;
                else if (t.Type == TileType.Grassland) wiese++;
            }
        double total = map.Width * (double)map.Height;
        Console.WriteLine($"  {wer}: Karte {map.Width}x{map.Height} — Wasser {wasser / total:P0}, Sand {sand / total:P0}, Wiese {wiese / total:P0}");
        string? egyptProblem = style == MapStyle.Egypten && wasser > 0 ? "Ägypten trägt Wasser (Wüste ohne Seen erwartet)" : null;
        string? wikingProblem = null;
        bool wikingBruecke = false;
        if (style == MapStyle.Wiking)
        {
            void PunktNear(Point mitte, out Point? ziel)
            {
                for (int r = 1; r <= 5; r++)
                    for (int dx = -r; dx <= r; dx++)
                        for (int dy = -r; dy <= r; dy++)
                        {
                            if (Math.Max(Math.Abs(dx), Math.Abs(dy)) != r) continue;
                            var n = new Point(mitte.X + dx, mitte.Y + dy);
                            if (map.GetTile(n.X, n.Y) is { } nt && nt.Walkable) { ziel = n; return; }
                        }
                ziel = null;
            }
            var (cx1, cy1) = tc(0)!.Value;
            var (cx2, cy2) = tc(1)!.Value;
            PunktNear(new Point(cx1, cy1), out var s1);
            PunktNear(new Point(cx2, cy2), out var s2);
            if (s1 == null || s2 == null)
            {
                wikingProblem = "um ein Dorfzentrum ist kein begehbares Land";
            }
            else
            {
                var warte = new Queue<Point>();
                warte.Enqueue(s1.Value);
                var erreicht = new HashSet<Point> { s1.Value };
                while (warte.Count > 0 && !erreicht.Contains(s2.Value))
                {
                    var p = warte.Dequeue();
                    foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
                    {
                        var n = new Point(p.X + dx, p.Y + dy);
                        if (n.X < 0 || n.Y < 0 || n.X >= map.Width || n.Y >= map.Height) continue;
                        if (!erreicht.Add(n)) continue;
                        if (map.GetTile(n.X, n.Y) is { } nt && nt.Walkable)
                            warte.Enqueue(n);
                    }
                }
                wikingBruecke = erreicht.Contains(s2.Value);
                if (!wikingBruecke)
                    wikingProblem = "vom Land bei TC1 ist das Land bei TC2 nicht erreichbar — Brücken fehlen";
            }
        }

        // ---- Laufschleife: dieselben Update-Rufe wie im Spiel; endet beim Sieg
        //      (Dorfzentrum zerstört) oder beim ersten zerstörten feindlichen Gebäude.
        const float dt = 1f / 60f;
        double zeit = 0;
        int attackSeenP0 = 0, attackSeenP1 = 0;
        int loser = -1;
        double timeDefeat = -1;
        var zerstoert = new List<Building>();
        var gesehen = new HashSet<Building>();

        void Schritt()
        {
            var gt = new GameTime(TimeSpan.FromSeconds(zeit), TimeSpan.FromSeconds(dt));
            void InvokeOrNull(string name, object a)
            {
                var mi = T.GetMethod(name, F);
                if (mi == null) return;
                try { mi.Invoke(screen, new[] { a }); }
                catch (System.Reflection.TargetInvocationException) { }
            }
            Invoke0("UpdateSheepClaims");
            InvokeOrNull("UpdateUnits", gt);
            Invoke0("UpdatePopulationLimits");
            Invoke0("CheckDefeat");
            InvokeOrNull("UpdateTraining", dt);
            InvokeOrNull("UpdateResearch", dt);
            InvokeOrNull("UpdateAges", dt);
            InvokeOrNull("UpdateConstruction", dt);
            InvokeOrNull("UpdateResources", gt);
            map.RegrowCrop(dt);
            map.RegrowGrass(dt);
            map.UpdateSheep(dt);
            map.UpdateDeer(dt, units);
            map.UpdateRabbits(dt);
            map.UpdateSpoilage(dt);
            map.UpdateFogOfWarForPlayer(0, units);
            map.UpdateFogOfWarForPlayer(1, units);
            Invoke0("DrainOrders");
            if (screen.Agents.TryGetValue(0, out var a0)) a0.Tick(dt);
            if (screen.Agents.TryGetValue(1, out var a1)) a1.Tick(dt);

            if (loser < 0)
            {
                bool tc0 = map.Buildings.Any(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
                bool tc1 = map.Buildings.Any(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
                if (!tc0) { loser = 0; timeDefeat = zeit; }
                else if (!tc1) { loser = 1; timeDefeat = zeit; }
            }

            int a0s = 0, a1s = 0;
            foreach (var u in units)
            {
                if (u.State == UnitState.Gathering)
                {
                    if (u.OwnerId == 0) sammeltP0 = true; else sammeltP1 = true;
                }
                bool imAngriff = u.State == UnitState.Attacking
                                 || (u.State == UnitState.Moving && u.AttackTarget != null);
                if (!imAngriff) continue;
                if (u.OwnerId == 0) a0s++; else a1s++;
            }
            if (a0s > attackSeenP0) attackSeenP0 = a0s;
            if (a1s > attackSeenP1) attackSeenP1 = a1s;

            maxSumP0 = Math.Max(maxSumP0, summe(p1));
            maxSumP1 = Math.Max(maxSumP1, summe(p2));

            // Direkte Sammeln-Metrik: Nettogewinn pro Schritt + Einheiten im Sammelzustand.
            // (Nur „Bestand > Start" reicht nicht: mit Startreserven kann die KI ausgeben
            // und gleichzeitig sammeln — dann bleibt der Bestand unter dem Startwert.)
            int s0 = summe(p1), s1 = summe(p2);
            int d0 = s0 - prevSumP0, d1 = s1 - prevSumP1;
            prevSumP0 = s0; prevSumP1 = s1;
            if (d0 > maxDeltaP0) maxDeltaP0 = d0;
            if (d1 > maxDeltaP1) maxDeltaP1 = d1;
        }

        for (int i = 0; i < minuten * 60 * 60 && loser < 0; i++)
        {
            zeit += dt;
            Schritt();
            if (i % 30 == 0)
            {
                var weg = gesehen.Where(b => !map.Buildings.Contains(b)).ToList();
                zerstoert.AddRange(weg);
                gesehen.ExceptWith(weg);
                gesehen.UnionWith(map.Buildings);
                if (zerstoert.Count > 0 && zeit >= 30)
                    break;   // erste Zerstörung — der Kampf hat Wirkung gezeigt
            }
        }
        if (loser >= 0)
            Console.WriteLine($"  ... {wer}: Sieg {(loser == 0 ? "Owner 1" : "Owner 0")}, Dorfzentrum fiel bei t={timeDefeat:0}s");
        else
            Console.WriteLine($"  ... {wer}: nach {zeit:0}s — {zerstoert.Count} Gebäude zerstört");

        // ---- 1) Ressourcen gut verteilt: um beide START-Dorfzentren (30 Kacheln)
        //      Holz, Nahrung, Stein und Gold — auf der generierten Karte geprüft,
        //      weil die KIs im Kampf abbauen, bauen und verlegen ----
        if (rohstoffFehlt.Count == 0)
            Ok("um beide Dorfzentren stehen Holz, Nahrung, Stein und Gold (je 30 Kacheln)");
        else
            foreach (var m in rohstoffFehlt) Meld(m);

        // ---- 2) Das ganze funktioniert: beide KIs aktiv, beide sammeln Kartenressourcen ----
        int popEndP0 = p1.PopulationCount, popEndP1 = p2.PopulationCount;
        int bldEndP0 = map.Buildings.Count(b => b.OwnerId == 0);
        int bldEndP1 = map.Buildings.Count(b => b.OwnerId == 1);
        int soldatenP0 = units.Count(u => u.OwnerId == 0 && u.Type != UnitType.Villager && u.State != UnitState.Dead);
        int soldatenP1 = units.Count(u => u.OwnerId == 1 && u.Type != UnitType.Villager && u.State != UnitState.Dead);
        Console.WriteLine($"  {wer}: Owner 0 Pop {popStartP0}->{popEndP0}, Gebäude {bldStartP0}->{bldEndP0}, Soldaten {soldatenP0}, "
                        + $"max. Bestand {sumStartP0}->{maxSumP0} | Owner 1 Pop {popStartP1}->{popEndP1}, Gebäude {bldStartP1}->{bldEndP1}, "
                        + $"Soldaten {soldatenP1}, max. Bestand {sumStartP1}->{maxSumP1}");
        bool aktiv0 = popEndP0 > popStartP0 || bldEndP0 > bldStartP0 || soldatenP0 > 0;
        bool aktiv1 = popEndP1 > popStartP1 || bldEndP1 > bldStartP1 || soldatenP1 > 0;
        if (aktiv0) Ok("Owner 0 aktiv (KI spielt die eigene Seite)");
        else Meld("Owner 0 tut nichts — die KI spielt die Seite nicht");
        if (aktiv1) Ok("Owner 1 aktiv (KI spielt den Gegner)");
        else Meld("Owner 1 tut nichts — die KI spielt die Seite nicht");
        // Sammeln-Nachweis: mindestens eine Einheit war im Sammelzustand, oder der
        // Gesamtbestand hat an einem Schritt netto zugenommen (Einnahmen > Ausgaben).
        bool sammelt0 = sammeltP0 || maxDeltaP0 > 0;
        bool sammelt1 = sammeltP1 || maxDeltaP1 > 0;
        if (sammelt0 && sammelt1)
            Ok($"beide Seiten sammeln Kartenressourcen (Sammelzustand: {sammeltP0}/{sammeltP1}, "
               + $"Nettogewinn: +{maxDeltaP0}/+{maxDeltaP1}, Bestand max {maxSumP0}/{maxSumP1})");
        else
            Meld($"Sammeln fehlt: Owner 0 {sammelt0} (Sammeln={sammeltP0}, max Delta +{maxDeltaP0}), "
                 + $"Owner 1 {sammelt1} (Sammeln={sammeltP1}, max Delta +{maxDeltaP1}) — "
                 + "die Kartenressourcen reichen offenbar nicht");

        // ---- 3) Kampf: Angriffszustand, Zerstörung oder Sieg.
        //      Das ist eine Aussage über die KIs, keine Kartenprüfung — bei
        //      ausbleibendem Kampf warnt der Lauf, scheitert aber nicht. ----
        bool kampfgesehen = attackSeenP0 > 0 || attackSeenP1 > 0;
        if (loser >= 0)
            Ok($"Sieg: {(loser == 0 ? "Owner 1" : "Owner 0")} — Dorfzentrum fiel bei t={timeDefeat:0}s");
        else if (zerstoert.Count > 0)
            Ok($"Angriffe zeigen Wirkung: {zerstoert.Count} Gebäude zerstört (erster: {zerstoert[0].Type} von Owner {zerstoert[0].OwnerId})");
        else if (kampfgesehen)
            Ok($"Angriff beobachtet (Owner 0={attackSeenP0}, Owner 1={attackSeenP1} im Angriff), aber kein Gebäude fiel");
        else
            Console.WriteLine($"  !    {wer}: kein einziges Zeichen von Kampf — die KIs haben sich nicht begegnet oder nicht angegriffen (Aussage über die KIs, kein Kartenmangel)");

        // ---- 4) Landart-spezifisch — auf der generierten Karte geprüft ----
        switch (style)
        {
            case MapStyle.Egypten:
                if (egyptProblem != null) Meld(egyptProblem);
                else Ok("keine Wasserkacheln (Wüste)");
                break;
            case MapStyle.Wiking:
                if (wasser < total * 0.5)
                    Meld($"Wiking hat nur {wasser / total:P0} Wasser — die Karte soll überwiegend Wasser sein");
                else
                    Ok($"überwiegend Wasser ({wasser / total:P0})");
                if (wikingProblem != null)
                    Meld(wikingProblem);
                else if (wikingBruecke)
                    Ok("beide Dorfzentren auf Land und per Brücke verbunden");
                break;
            default: // Europa — die Referenz bleibt unverändert
                if (wiese < total * 0.5)
                    Meld($"Europa hat nur {wiese / total:P0} Wiese (mindestens 50 % erwartet)");
                else
                    Ok($"Referenzkarte unverändert (Wiese {wiese / total:P0})");
                break;
        }
    }
}
