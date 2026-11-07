// Stufen: zwei KI-Instanzen gegeneinander, je Paarung eine frisch erzeugte Standardkarte im
// 16:9-Format mit den Startvorräten des Spiels (Nutzer 2026-11-07: „Zum Testen kannst du einfach
// zwei AI-Instanzen gegeneinander antreten lassen"). Prüft die drei KI-Stufen:
//   * Defense greift nie an: keine Angriffswelle, kein Angriff auf ein feindliches Gebäude weiter
//     als 2 × HomeDefenseRadius vom eigenen Stadtzentrum; baut Wachtürme, bildet Bogenschützen aus
//   * Normal greift selbst an (wie bisher in Wellen): ein Angriff auf ein feindliches Gebäude
//     fern vom eigenen Dorf
//   * This is Sparta: gemischte Armee (Bogenschützen und Reiter aus dem eigenen Stall), Wellen in
//     Aufstellung - beim Marsch steht der Großteil auf seinen Plätzen, die Schützen hinter den
//     Nahkämpfern, eine Kachel frei zwischen Nachbarn -, und die Reiter greifen im Schwall an und
//     ziehen sich wieder zurück
// Gemessen wird jede Spielsekunde; am Ende je Paarung der Ausgang (zerstörte Gebäude, Dorfzentren).
//
//   dotnet run --project tools/ai-pruefung -- stufen [minuten] [sparta|defense|duell|basis] [karten N] [verlauf] [wiking]
//
// „wiking" spielt auf der Inselkarte (Nutzer 2026-11-07: „man wird nur mit booten von einer zur nächsten
// insel kommen“): dort zählt zusätzlich, ob die angreifenden Stufen übersetzen (ÜBERFAHRT, BRÜCKENKOPF).
//
// „verlauf" druckt die Meldungen der Armeeführung (Welle, Kampf, Belagerung, Reiter, Rückzug) mit Uhrzeit.
// Je Paarung 2 frisch erzeugte Karten („karten N" ändert das). Ohne Paarung laufen „sparta"
// (Sparta gegen Normal) und „defense" (Defense gegen Normal);
// „duell" ist Sparta gegen Defense, „basis" Normal gegen Normal (zum Vergleich der Ausgänge).
using System.Reflection;
using AoE.Core.Ai;
using AgeOfEvolutions.Core.AI;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using Age = AoE.Core.Economy.Age;
using BuildingType = AoE.Core.Entities.BuildingType;
using UnitState = AoE.Core.Entities.UnitState;


static class Stufen
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);

    /// <summary>Messwerte einer Seite über das ganze Spiel.</summary>
    sealed class Seite
    {
        public int Owner;
        public AiProfile Profil = AiProfile.Standard;
        public string Name = "";
        public EconomyAi Ki = null!;
        public AiBridge Bruecke = null!;
        public readonly List<string> Log = new();
        public int Wellen, Reiterangriffe, Reiterrueckzuege;
        public int Ueberfahrten, Brueckenkoepfe;   // Wiking: Wellen, die übersetzen, und die drüben ankamen
        public int MaxBogen, MaxReiter, MaxSoldaten, MaxTuerme;
        public bool Stall;
        public Age Zeitalter;
        public double FeudalAb = -1;              // Spielsekunde, ab der die Seite in der Feudalzeit war
        public double GesichtetAb = -1;           // Spielsekunde, ab der die Seite ein feindliches Gebäude sah
        public int FernAngriffe;                  // Angriffe auf feindliche Gebäude fern vom eigenen Dorf
        public string? ErsterFernAngriff;
        public int DefenseVerstoesse;
        public string? ErsterVerstoss;
        public int MaxEntfernung;                 // weitester eigener Soldat vom eigenen Stadtzentrum
        public bool WelleGesehen;                 // Army.Phase war je nicht Gather
        public int Marschproben, MarschAufPlatz;  // Summe der Anteile in Prozent
        public int Reihenproben, ReihenOk;        // Schützen hinter den Nahkämpfern
        public int AbstandProben, AbstandOk;      // nächster Nachbar mindestens 2 Kacheln
        public int Heimproben, HeimAufPlatz;
        public int Zerstoert;                     // Gebäude dieser Seite, die gefallen sind
    }

    public static int Run(string[] args)
    {
        int minuten = args.Select(a => int.TryParse(a, out int m) ? m : 0).FirstOrDefault(m => m > 0);
        if (minuten == 0) minuten = 30;
        var paarungen = new List<(string Name, AiProfile A, AiProfile B)>();
        if (args.Contains("sparta")) paarungen.Add(("sparta", AiProfile.Sparta, AiProfile.Standard));
        if (args.Contains("defense")) paarungen.Add(("defense", AiProfile.Defense, AiProfile.Standard));
        if (args.Contains("duell")) paarungen.Add(("duell", AiProfile.Sparta, AiProfile.Defense));
        if (args.Contains("basis")) paarungen.Add(("basis", AiProfile.Standard, AiProfile.Standard));
        if (paarungen.Count == 0)
        {
            paarungen.Add(("sparta", AiProfile.Sparta, AiProfile.Standard));
            paarungen.Add(("defense", AiProfile.Defense, AiProfile.Standard));
        }

        var verstoesse = new List<string>();
        if (args.Contains("wiking")) _stil = MapStyle.Wiking;
        int karten = 2;
        int ki = Array.IndexOf(args, "karten");
        if (ki >= 0 && ki + 1 < args.Length && int.TryParse(args[ki + 1], out int k) && k > 0) karten = k;
        var bilanz = new Bilanz();
        foreach (var (name, a, b) in paarungen)
            for (int karte = 1; karte <= karten; karte++)
            {
                string titel = karten > 1 ? $"{name} (Karte {karte})" : name;
                try
                {
                    // jede zweite Karte mit getauschten Startplätzen - die Zufallskarte soll nicht immer
                    // dieselbe Stufe benachteiligen
                    if (karte % 2 == 0) Spiel(titel, b, a, minuten, verstoesse, args.Contains("verlauf"), bilanz);
                    else Spiel(titel, a, b, minuten, verstoesse, args.Contains("verlauf"), bilanz);
                }
                catch (Exception e)
                {
                    var grund = e is TargetInvocationException { InnerException: not null } ti ? ti.InnerException : e;
                    verstoesse.Add($"{titel}: abgebrochen - {grund.GetType().Name}: {grund.Message}\n{grund.StackTrace}");
                }
            }

        // Über alle Spiele: die Zufallskarte entscheidet mit (Wölfe und Bären an einem Dorf, ein Späher,
        // der fällt, bevor er das gegnerische Dorf findet) - verlangt wird, dass jede Stufe sich
        // mindestens einmal zeigen konnte
        if (paarungen.Any(p => ReferenceEquals(p.A, AiProfile.Sparta) || ReferenceEquals(p.B, AiProfile.Sparta)) && bilanz.SpartaGeprueft == 0)
            verstoesse.Add("This is Sparta kam in keinem Spiel aus der Dunklen Zeit - nichts geprüft");
        if (bilanz.NormalMitWellenstaerke > 0 && bilanz.NormalAngriffe == 0)
            verstoesse.Add($"Normal hat in keinem von {bilanz.NormalMitWellenstaerke} Spielen mit Wellenstärke selbst angegriffen");
        Console.WriteLine($"\n  Bilanz: Sparta in {bilanz.SpartaGeprueft} Spielen geprüft; Normal griff in {bilanz.NormalAngriffe} "
            + $"von {bilanz.NormalMitWellenstaerke} Spielen mit Wellenstärke selbst an");
        if (verstoesse.Count > 0)
        {
            Console.WriteLine($"\nNICHT ERFÜLLT ({verstoesse.Count}):");
            foreach (var v in verstoesse)
                Console.WriteLine("  - " + v);
            return 1;
        }
        Console.WriteLine($"\nKI-Stufen stimmen ({paarungen.Count} Paarungen, je {minuten} min).");
        return 0;
    }

    /// <summary>So lange muss Sparta in der Feudalzeit gewesen sein, damit Stall, gemischte Armee und Welle verlangt werden.</summary>
    const double SpartaFeudalSekunden = 8 * 60;

    sealed class Bilanz
    {
        public int SpartaGeprueft, NormalMitWellenstaerke, NormalAngriffe;
    }

    static string StufeName(AiProfile p) => GameplayOptionsName(AiProfile.LevelOf(p));
    static string GameplayOptionsName(int level) => level switch { 0 => "Defense", 2 => "This is Sparta", _ => "Normal" };

    static readonly string[] Armeemeldungen = { "WELLE", "KAMPF", "NEU AUFSTELLEN", "BELAGERUNG", "RÜCKZUG", "REITER", "ERKUNDUNG",
                                                "ÜBERFAHRT", "BRÜCKENKOPF", "Fähre" };

    /// <summary>Die Landart der Spiele (Schalter „wiking“).</summary>
    static MapStyle _stil = MapStyle.Europa;

    static void Spiel(string paarung, AiProfile profilA, AiProfile profilB, int minuten, List<string> verstoesse, bool verlauf,
                      Bilanz bilanz)
    {
        double zeit = 0;
        var uhr = System.Diagnostics.Stopwatch.StartNew();
        var screen = new RTSGameplayScreen(MapSize.Standard);
        var dims = MapSizes.Dimensions(MapSize.Standard, 16f / 9f);
        var map = _stil == MapStyle.Europa
            ? new TileMap(dims.Width, dims.Height, 32, MapSettings.ForSize(MapSize.Standard))
            : new TileMap(dims.Width, dims.Height, 32, MapSettings.ForStyle(_stil, MapSize.Standard), _stil);
        var units = map.Units;
        var p1 = new Player(0, "P1", "Briten");
        var p2 = new Player(1, "P2", "Azteken");
        foreach (var u in units)
            (u.OwnerId == 0 ? p1 : p2).AddUnit(u);
        void Set(string name, object wert) => (T.GetField(name, F)
            ?? throw new InvalidOperationException($"Feld RTSGameplayScreen.{name} nicht gefunden - umbenannt?")).SetValue(screen, wert);
        object Get(string name) => (T.GetField(name, F)
            ?? throw new InvalidOperationException($"Feld RTSGameplayScreen.{name} nicht gefunden - umbenannt?")).GetValue(screen)!;
        object? Call(string name, params object[] a)
        {
            var m = T.GetMethods(F).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == a.Length)
                ?? throw new InvalidOperationException($"Methode RTSGameplayScreen.{name}({a.Length} Argumente) nicht gefunden - umbenannt?");
            return m.Invoke(screen, a);
        }
        Set("tileMap", map);
        Set("gatherWorld", new TileMapGatherWorld(map));
        Set("player1", p1);
        Set("player2", p2);
        Set("units", units);
        map.UpdateFogOfWarForPlayer(0, units);
        map.UpdateFogOfWarForPlayer(1, units);
        Call("UpdatePopulationLimits");

        var seiten = new Seite[2];
        foreach (var (o, p) in new[] { (0, profilA), (1, profilB) })
        {
            var s = new Seite { Owner = o, Profil = p, Name = $"Owner {o} ({StufeName(p)})", Ki = new EconomyAi(p) };
            screen.ActivateAi(s.Ki, owner: o);
            var agent = screen.Agents[o];
            s.Bruecke = agent.Bridge;
            agent.Log = msg =>
            {
                s.Log.Add(msg);
                if (verlauf && Armeemeldungen.Any(msg.StartsWith))
                    Console.WriteLine($"      [{zeit / 60:00.0}] {s.Name}: {msg}");
                if (msg.StartsWith("WELLE:")) s.Wellen++;
                if (msg.StartsWith("ÜBERFAHRT")) s.Ueberfahrten++;
                if (msg.StartsWith("BRÜCKENKOPF")) s.Brueckenkoepfe++;
                if (msg.StartsWith("REITER:") && msg.Contains("Schwall")) s.Reiterangriffe++;
                if (msg.StartsWith("REITER: Rückzug")) s.Reiterrueckzuege++;
            };
            seiten[o] = s;
        }
        Console.WriteLine($"  Paarung {paarung}: {seiten[0].Name} gegen {seiten[1].Name}, Karte {map.Width}×{map.Height}, {minuten} min");

        var gesehen = new HashSet<Building>(map.Buildings);
        int tcGefallen = -1;
        double tcZeit = 0;
        const float dt = 1f / 60f;
        double nebel = 0, probe = 0;
        int minute = 0;
        while (minute < minuten && tcGefallen < 0)
        {
            zeit += dt;
            Set("_gameSeconds", (double)Get("_gameSeconds") + dt);
            var gt = new GameTime(TimeSpan.FromSeconds(zeit), TimeSpan.FromSeconds(dt));
            Call("UpdateSheepClaims");
            Call("UpdateUnits", gt);
            Call("UpdatePopulationLimits");
            Call("UpdateTraining", dt);
            Call("UpdateResearch", dt);
            Call("UpdateAges", dt);
            Call("UpdateConstruction", dt);
            Call("UpdateRepair", dt);
            map.Market.Update(dt);
            map.RegrowCrop(dt);
            map.RegrowGrass(dt);
            map.UpdateSheep(dt); map.UpdateDeer(dt, units); map.UpdateRabbits(dt); map.UpdateSpoilage(dt);
            nebel -= dt;
            if (nebel <= 0)
            {
                nebel = 0.25;
                map.UpdateFogOfWarForPlayer(0, units);
                map.UpdateFogOfWarForPlayer(1, units);
            }
            Call("DrainOrders");
            screen.Agents[0].Tick(dt);
            screen.Agents[1].Tick(dt);

            probe -= dt;
            if (probe <= 0)
            {
                probe = 1;
                foreach (var s in seiten)
                    Messen(s, seiten[1 - s.Owner], map, units, zeit);
                foreach (var b in gesehen.Where(b => !map.Buildings.Contains(b)).ToList())
                {
                    seiten[b.OwnerId].Zerstoert++;
                    gesehen.Remove(b);
                    if (b.Core.BuildingType == BuildingType.TownCenter && tcGefallen < 0)
                    {
                        tcGefallen = b.OwnerId;
                        tcZeit = zeit;
                    }
                }
                gesehen.UnionWith(map.Buildings);
            }

            if (zeit < (minute + 1) * 60)
                continue;
            minute++;
            if (minute % 5 == 0)
                foreach (var s in seiten)
                    Console.WriteLine($"    {minute,2} min {s.Name}: {Stand(s, map)}");
        }

        // ---- Bericht
        Console.WriteLine($"  Ausgang {paarung} nach {zeit / 60:0.0} min ({uhr.Elapsed.TotalSeconds:0} s Rechenzeit): "
            + (tcGefallen >= 0 ? $"Dorfzentrum von {seiten[tcGefallen].Name} fiel bei {tcZeit / 60:0.0} min; " : "beide Dorfzentren stehen; ")
            + $"zerstörte Gebäude {seiten[0].Name} {seiten[0].Zerstoert}, {seiten[1].Name} {seiten[1].Zerstoert}");
        foreach (var s in seiten)
        {
            Console.WriteLine($"    {s.Name}: {Stand(s, map)}");
            Console.WriteLine($"      Wellen {s.Wellen}, Fernangriffe {s.FernAngriffe}, weitester Soldat {s.MaxEntfernung} Kacheln vom Stadtzentrum, "
                + $"höchstens {s.MaxSoldaten} Soldaten ({s.MaxBogen} Bogen, {s.MaxReiter} Reiter), Türme {s.MaxTuerme}, Stall {(s.Stall ? "ja" : "nein")}, "
                + $"Feudal {(s.FeudalAb < 0 ? "-" : $"{s.FeudalAb / 60:0.0} min")}, Gegner gesichtet {(s.GesichtetAb < 0 ? "nie" : $"{s.GesichtetAb / 60:0.0} min")}"
                + (_stil == MapStyle.Wiking ? $", Überfahrten {s.Ueberfahrten}, Brückenköpfe {s.Brueckenkoepfe}" : ""));
            if (s.Ki.Army is not null)
                Console.WriteLine($"      Aufstellung: Marsch {s.Marschproben} Proben, in Form {Prozent(s.MarschAufPlatz, s.Marschproben * 100)}; "
                    + $"Schützen hinten {s.ReihenOk}/{s.Reihenproben}; Abstand ≥ 2 {s.AbstandOk}/{s.AbstandProben}; "
                    + $"Heimgruppe auf dem Platz {Prozent(s.HeimAufPlatz, s.Heimproben * 100)} ({s.Heimproben} Proben); "
                    + $"Reiter: {s.Reiterangriffe} Schwall, {s.Reiterrueckzuege} Rückzug");
        }

        // ---- Abnahme
        foreach (var s in seiten)
        {
            string wer = $"{paarung}, {s.Name}";
            var gegner = seiten[1 - s.Owner];
            if (ReferenceEquals(s.Profil, AiProfile.Defense))
            {
                if (s.WelleGesehen || s.Wellen > 0)
                    verstoesse.Add($"{wer}: hat eine Angriffswelle gestartet ({s.Wellen})");
                if (s.DefenseVerstoesse > 0)
                    verstoesse.Add($"{wer}: {s.DefenseVerstoesse}× ein feindliches Gebäude fern vom eigenen Dorf angegriffen, zuerst {s.ErsterVerstoss}");
                if (s.Zeitalter >= Age.Feudal && s.MaxTuerme == 0)
                    verstoesse.Add($"{wer}: in der Feudalzeit keinen Wachturm gebaut");
                if (s.Zeitalter >= Age.Feudal && s.MaxBogen == 0)
                    verstoesse.Add($"{wer}: in der Feudalzeit keine Bogenschützen");
                if (s.AbstandProben >= 10 && s.AbstandOk * 10 < s.AbstandProben * 8)
                    verstoesse.Add($"{wer}: Abstand in der stehenden Aufstellung nur {s.AbstandOk}/{s.AbstandProben}× eingehalten");
                if (s.Heimproben > 30 && s.HeimAufPlatz < s.Heimproben * 50)
                    verstoesse.Add($"{wer}: Heimgruppe steht nur zu {Prozent(s.HeimAufPlatz, s.Heimproben * 100)} auf ihren Plätzen");
            }
            else if (ReferenceEquals(s.Profil, AiProfile.Standard))
            {
                // Normal ist unverändert (Wellen wie bisher); ob sie in einem Spiel angreift, hängt an
                // der Karte - verlangt wird es über alle Spiele (Bilanz), hier nur der Bericht
                if (s.MaxSoldaten >= s.Profil.RaidMinSoldiers + s.Profil.HomeGuard)
                {
                    bilanz.NormalMitWellenstaerke++;
                    if (s.FernAngriffe > 0) bilanz.NormalAngriffe++;
                    else Console.WriteLine($"  Hinweis {wer}: mit Wellenstärke ({s.MaxSoldaten} Soldaten), aber kein Angriff in diesem Spiel");
                }
                else
                    Console.WriteLine($"  Hinweis {wer}: nie Wellenstärke erreicht ({s.MaxSoldaten} Soldaten)");
            }
            else if (ReferenceEquals(s.Profil, AiProfile.Sparta) && (s.FeudalAb < 0 || zeit - s.FeudalAb < SpartaFeudalSekunden))
                // Schießstand, Stall und gemischte Armee gibt es erst ab der Feudalzeit
                Console.WriteLine($"  Hinweis {wer}: " + (s.FeudalAb < 0 ? "kam nicht aus der Dunklen Zeit" : $"erst ab {s.FeudalAb / 60:0.0} min in der Feudalzeit")
                    + $" ({Stand(s, map)}) - Stufe nicht geprüft");
            else if (ReferenceEquals(s.Profil, AiProfile.Sparta))
            {
                bilanz.SpartaGeprueft++;
                if (!s.Stall) verstoesse.Add($"{wer}: keinen Stall gebaut");
                if (s.MaxBogen == 0) verstoesse.Add($"{wer}: keine Bogenschützen");
                if (s.MaxReiter == 0) verstoesse.Add($"{wer}: keine Reiter");
                if (s.Wellen == 0) verstoesse.Add($"{wer}: keine Angriffswelle");
                if (s.Marschproben < 20)
                    verstoesse.Add($"{wer}: nur {s.Marschproben} Marschproben - die Welle marschiert kaum in Aufstellung");
                else if (s.MarschAufPlatz < s.Marschproben * 50)
                    verstoesse.Add($"{wer}: beim Marsch nur {Prozent(s.MarschAufPlatz, s.Marschproben * 100)} in Form");
                if (s.Reihenproben >= 10 && s.ReihenOk * 10 < s.Reihenproben * 7)
                    verstoesse.Add($"{wer}: Schützen nur {s.ReihenOk}/{s.Reihenproben}× hinter den Nahkämpfern");
                if (s.AbstandProben >= 10 && s.AbstandOk * 10 < s.AbstandProben * 8)
                    verstoesse.Add($"{wer}: Abstand in der stehenden Aufstellung nur {s.AbstandOk}/{s.AbstandProben}× eingehalten");
                if (s.Reiterangriffe > 0 && s.Reiterrueckzuege == 0)
                    verstoesse.Add($"{wer}: Reiter greifen an ({s.Reiterangriffe}), ziehen sich aber nie zurück");
                if (s.MaxReiter > 0 && s.Reiterangriffe == 0)
                    verstoesse.Add($"{wer}: Reiter vorhanden, aber kein Schwall");
            }
            _ = gegner;
        }
    }

    static string Prozent(int teil, int ganz) => ganz == 0 ? "-" : $"{100.0 * teil / ganz:0} %";

    static (int X, int Y)? Zentrum(TileMap map, int owner)
    {
        var tc = map.Buildings.FirstOrDefault(b => b.OwnerId == owner && b.Core.BuildingType == BuildingType.TownCenter);
        return tc is null ? null : (tc.X + tc.Width / 2, tc.Y + tc.Height / 2);
    }

    static int Cheb(int ax, int ay, int bx, int by) => Math.Max(Math.Abs(ax - bx), Math.Abs(ay - by));

    static void Messen(Seite s, Seite gegner, TileMap map, List<Unit> units, double zeit)
    {
        var home = Zentrum(map, s.Owner);
        var bruecke = s.Bruecke;
        var eigene = bruecke.Units.Where(u => u.State != UnitStateKind.Dead).ToList();
        var soldaten = eigene.Where(u => u.Kind != UnitKind.Villager).ToList();
        s.MaxSoldaten = Math.Max(s.MaxSoldaten, soldaten.Count);
        s.MaxBogen = Math.Max(s.MaxBogen, soldaten.Count(u => u.Kind == UnitKind.Archer));
        s.MaxReiter = Math.Max(s.MaxReiter, soldaten.Count(u => u.Kind == UnitKind.Cavalry));
        s.MaxTuerme = Math.Max(s.MaxTuerme, bruecke.Buildings.Count(b => b.Type == BuildingType.Tower && b.IsComplete));
        s.Stall |= bruecke.Buildings.Any(b => b.Type == BuildingType.Stable && b.IsComplete);
        s.Zeitalter = bruecke.Age;
        if (s.Zeitalter >= Age.Feudal && s.FeudalAb < 0) s.FeudalAb = zeit;
        if (s.GesichtetAb < 0 && bruecke.VisibleEnemies().Any(e => e.IsBuilding)) s.GesichtetAb = zeit;
        if (home is not { } h) return;

        // Angriffe auf feindliche Gebäude: wie weit vom eigenen Dorf?
        foreach (var u in units)
        {
            if (u.OwnerId != s.Owner || u.State == UnitState.Dead || u.Type == UnitType.Villager) continue;
            var g = map.WorldToGrid(u.Position);
            s.MaxEntfernung = Math.Max(s.MaxEntfernung, Cheb((int)g.X, (int)g.Y, h.X, h.Y));
            if (u.AttackTarget is not { } ziel || ziel.OwnerId == s.Owner) continue;
            int d = Cheb(ziel.X + ziel.Width / 2, ziel.Y + ziel.Height / 2, h.X, h.Y);
            if (d > 2 * s.Profil.HomeDefenseRadius)
            {
                s.FernAngriffe++;
                s.ErsterFernAngriff ??= $"{ziel.Core.BuildingType} {d} Kacheln vom Stadtzentrum";
                if (ReferenceEquals(s.Profil, AiProfile.Defense))
                {
                    s.DefenseVerstoesse++;
                    s.ErsterVerstoss ??= $"{u.Type} auf {ziel.Core.BuildingType} {d} Kacheln vom Stadtzentrum";
                }
            }
        }

        // Aufstellung (Defense, Sparta)
        if (s.Ki.Army is not { } army) return;
        if (army.Phase != ArmyPhase.Gather) s.WelleGesehen = true;
        var byId = soldaten.ToDictionary(u => u.Id);
        var wave = army.Wave.Where(byId.ContainsKey).Select(id => byId[id]).ToList();
        if (army.Phase == ArmyPhase.March && wave.Count >= 4)
        {
            // Reiter nur in Bereitschaft: im Schwall und beim Rückzug hinter den Verband sind sie
            // absichtlich nicht auf ihrem Platz (dafür zählen Schwall und Rückzug)
            var mit = wave.Where(u => army.Slots.ContainsKey(u.Id) && !u.HasAttack
                                      && (Formation.RoleOf(u.Kind) != FormationRole.Flank
                                          || army.WaveCavalry == CavalryMode.Ready)).ToList();
            if (mit.Count >= 4)
            {
                // Form statt Nachlauf: der Verband rückt schrittweise vor, die Einheiten sind fast immer
                // unterwegs zum nächsten Platz. In Form steht, wer relativ zur Mitte des Verbands höchstens
                // eine Kachel neben seinem Platz relativ zur Mitte der Plätze steht.
                double dx = mit.Average(u => u.X - army.Slots[u.Id].X), dy = mit.Average(u => u.Y - army.Slots[u.Id].Y);
                int ox = (int)Math.Round(dx), oy = (int)Math.Round(dy);
                var platz = mit.Where(u => Cheb(u.X - ox, u.Y - oy, army.Slots[u.Id].X, army.Slots[u.Id].Y) <= 1).ToList();
                s.Marschproben++;
                s.MarschAufPlatz += 100 * platz.Count / mit.Count;
                Reihe(s, platz, (army.Anchor.X + ox, army.Anchor.Y + oy), army.Facing);
                Abstand(s, mit, army.Slots);
            }
        }
        var heim = soldaten.Where(u => !army.Wave.Contains(u.Id) && army.Slots.ContainsKey(u.Id) && !u.HasAttack).ToList();
        if (heim.Count >= 4)
        {
            s.Heimproben++;
            s.HeimAufPlatz += 100 * heim.Count(u => Cheb(u.X, u.Y, army.Slots[u.Id].X, army.Slots[u.Id].Y) <= 1) / heim.Count;
            Abstand(s, heim, army.Slots);
        }
    }

    /// <summary>Unter den Einheiten auf ihren Plätzen: Schützen hinter den Nahkämpfern (Mittel der
    /// Tiefe), und hat jede einen Nachbarn näher als 2 Kacheln?</summary>
    static void Reihe(Seite s, List<UnitSnapshot> platz, (int X, int Y) anchor, (float X, float Y) f)
    {
        float Tiefe(UnitSnapshot u) => (u.X - anchor.X) * f.X + (u.Y - anchor.Y) * f.Y;
        var nah = platz.Where(u => u.Kind is UnitKind.Militia or UnitKind.Spearman).ToList();
        var fern = platz.Where(u => u.Kind == UnitKind.Archer).ToList();
        if (nah.Count >= 2 && fern.Count >= 2)
        {
            s.Reihenproben++;
            if (fern.Average(Tiefe) + 1 < nah.Average(Tiefe)) s.ReihenOk++;
        }
    }

    /// <summary>Abstand in der stehenden Aufstellung: stehen mindestens 80 % der Gruppe genau auf ihren
    /// Plätzen, zählt die Probe - erfüllt, wenn unter diesen höchstens jeder Fünfte einen Nachbarn näher als
    /// 2 Kacheln (Chebyshev) hat (Ausweichplätze an Hindernissen). Unterwegs rücken die Einheiten
    /// verschieden schnell nach, dort misst „in Form" die Gestalt.</summary>
    static void Abstand(Seite s, List<UnitSnapshot> gruppe, IReadOnlyDictionary<int, (int X, int Y)> slots)
    {
        var genau = gruppe.Where(u => slots.TryGetValue(u.Id, out var p) && p.X == u.X && p.Y == u.Y).ToList();
        if (genau.Count < 4 || genau.Count * 5 < gruppe.Count * 4) return;
        s.AbstandProben++;
        int dicht = genau.Count(u => genau.Any(v => v.Id != u.Id && Cheb(u.X, u.Y, v.X, v.Y) < 2));
        if (dicht * 5 <= genau.Count) s.AbstandOk++;
    }

    static string Stand(Seite s, TileMap map)
    {
        var b = s.Bruecke;
        var r = b.Resources;
        int dorf = b.Units.Count(u => u.Kind == UnitKind.Villager);
        var arten = b.Units.Where(u => u.Kind != UnitKind.Villager).GroupBy(u => u.Kind).Select(g => $"{g.Key}={g.Count()}");
        var gebaeude = b.Buildings.GroupBy(x => x.Type + (x.IsComplete ? "" : "(Bau)")).Select(g => $"{g.Key}={g.Count()}");
        string phase = s.Ki.Army is { } a ? $", Armee {a.Phase} (Welle {a.Wave.Count})" : "";
        return $"{b.Age}{(b.AgeTarget is { } z ? $"->{z}" : "")}, {dorf} Dorfbewohner, Soldaten [{string.Join(" ", arten)}]{phase}, "
               + $"N{r.Food} H{r.Wood} G{r.Gold} S{r.Stone}, {string.Join(" ", gebaeude)}";
    }
}
