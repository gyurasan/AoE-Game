// Entwicklung: die KI allein wie im Spiel - Standardkarte im 16:9-Format, Standardprofil,
// Startvorräte des Spiels, der Mensch tut nichts. Je Karte (drei frisch erzeugte) 20 Minuten
// Spielzeit, alle 5 Minuten ihr Stand; die Abnahme K4 läuft mit 25, weil manche Karten den
// Aufstieg erst in Minute 19 beginnen. Verlangt:
//   * kein Stillstand: ab Minute 3 nie zwei Minuten hintereinander dieselben Vorräte und
//     dieselbe Bevölkerung - so stand die Wirtschaft, als Häuser und Kaserne das
//     Stadtzentrum einmauerten und die Sammler ihre volle Last nirgends abgeben konnten
//   * am Ende mindestens die Feudalzeit (ein laufender Aufstieg zählt)
//   * am Ende mindestens AiProfile.Standard.RaidMinSoldiers Soldaten
//
//   dotnet run --project tools/ai-pruefung -- entwicklung [minuten]
//
// Entstanden am 2026-10-11: der Nutzer meldete, dass der Gegner sich nicht weiterentwickelt
// und keine Soldaten ausbildet. Vorher: Dunkle Zeit nach 20 Minuten, höchstens 5 Milizen,
// auf manchen Karten ab Minute 4 völliger Stillstand.
using System.Reflection;
using AoE.Core.Ai;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using Age = AoE.Core.Economy.Age;
using BuildingType = AoE.Core.Entities.BuildingType;

static class Entwicklung
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);
    const int KARTEN = 3;

    public static int Run(string[] args)
    {
        int minuten = args.Select(a => int.TryParse(a, out int m) ? m : 0).FirstOrDefault(m => m > 0);
        if (minuten == 0) minuten = 20;
        var verstoesse = new List<string>();
        for (int karte = 1; karte <= KARTEN; karte++)
        {
            try
            {
                Karte(karte, minuten, verstoesse);
            }
            catch (Exception e)
            {
                var grund = e is TargetInvocationException { InnerException: not null } ti ? ti.InnerException : e;
                verstoesse.Add($"Karte {karte}: abgebrochen - {grund.GetType().Name}: {grund.Message}");
            }
        }
        if (verstoesse.Count > 0)
        {
            Console.WriteLine($"\nNICHT ERFÜLLT ({verstoesse.Count}):");
            foreach (var v in verstoesse)
                Console.WriteLine("  - " + v);
            return 1;
        }
        Console.WriteLine($"\nEntwicklung stimmt ({KARTEN} Karten, je {minuten} min).");
        return 0;
    }

    static void Karte(int karte, int minuten, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Entwicklung";
        int vorher = verstoesse.Count;

        var screen = new RTSGameplayScreen(MapSize.Standard);
        var dims = MapSizes.Dimensions(MapSize.Standard, 16f / 9f);
        var map = new TileMap(dims.Width, dims.Height, 32, MapSettings.ForSize(MapSize.Standard));
        var units = map.Units;
        var p1 = new Player(0, "P1", "Briten");
        var p2 = new Player(1, "P2", "Azteken");
        foreach (var u in units)
            (u.OwnerId == 0 ? p1 : p2).AddUnit(u);
        void Set(string name, object wert) => (T.GetField(name, F)
            ?? throw new InvalidOperationException($"Feld RTSGameplayScreen.{name} nicht gefunden - umbenannt?")).SetValue(screen, wert);
        object Get(string name) => (T.GetField(name, F)
            ?? throw new InvalidOperationException($"Feld RTSGameplayScreen.{name} nicht gefunden - umbenannt?")).GetValue(screen);
        object Call(string name, params object[] a)
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

        // Nur die KI spielt, mit dem Profil aus dem Spiel
        screen.ActivateAi(new EconomyAi(), owner: 1);
        var agent = screen.Agents[1];
        var bridge = new AgeOfEvolutions.Core.AI.AiBridge(screen) { Owner = 1 };

        const float dt = 1f / 60f;
        double zeit = 0, nebel = 0;
        int minute = 0;
        (ResourceVector, int) zuvor = default;
        int stillstandAb = -1;
        while (minute < minuten)
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
            map.RegrowCrop(dt);
            map.RegrowGrass(dt);
            map.UpdateSheep(dt); map.UpdateDeer(dt); map.UpdateRabbits(dt); map.UpdateBoars(dt);
            nebel -= dt;
            if (nebel <= 0)
            {
                nebel = 0.25;
                map.UpdateFogOfWarForPlayer(0, units);
                map.UpdateFogOfWarForPlayer(1, units);
            }
            Call("DrainOrders");
            agent.Tick(dt);

            if (zeit < (minute + 1) * 60)
                continue;
            minute++;
            var jetzt = (bridge.Resources, bridge.PopulationCount);
            if (minute > 3 && jetzt == zuvor && stillstandAb < 0)
                stillstandAb = minute - 1;
            zuvor = jetzt;
            if (minute % 5 == 0)
                Console.WriteLine($"    {wer}, {minute} min: {Stand(bridge)}");
        }

        int soldaten = bridge.Units.Count(u => u.Kind != UnitKind.Villager && u.State != UnitStateKind.Dead);
        bool feudal = bridge.Age >= Age.Feudal || bridge.AgeTarget != null;
        if (stillstandAb >= 0)
            verstoesse.Add($"{wer}: ab Minute {stillstandAb} Stillstand - Vorräte und Bevölkerung ändern sich nicht mehr ({Stand(bridge)}; {Stadtzentrum(map)})");
        if (!feudal)
            verstoesse.Add($"{wer}: nach {minuten} min noch {bridge.Age}, erwartet mindestens Feudalzeit ({Stand(bridge)})");
        if (soldaten < AiProfile.Standard.RaidMinSoldiers)
            verstoesse.Add($"{wer}: nach {minuten} min {soldaten} Soldaten, erwartet mindestens {AiProfile.Standard.RaidMinSoldiers} ({Stand(bridge)})");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {Stand(bridge)}");
    }

    static string Stand(AgeOfEvolutions.Core.AI.AiBridge b)
    {
        var r = b.Resources;
        int dorf = b.Units.Count(u => u.Kind == UnitKind.Villager);
        int soldaten = b.Units.Count(u => u.Kind != UnitKind.Villager);
        var gebaeude = b.Buildings.GroupBy(x => x.Type + (x.IsComplete ? "" : "(Bau)")).Select(g => $"{g.Key}={g.Count()}");
        return $"{b.Age}{(b.AgeTarget is { } z ? $"->{z} {b.AgeProgress:P0}" : "")}, Pop {b.PopulationCount}/{b.PopulationCapacity} "
               + $"({dorf} Dorfbewohner, {soldaten} Soldaten), N{r.Food} H{r.Wood} G{r.Gold} S{r.Stone}, {string.Join(" ", gebaeude)}";
    }

    // Wie viele Kacheln rund ums Stadtzentrum noch begehbar sind - dort geben die Sammler ab
    static string Stadtzentrum(TileMap map)
    {
        var tc = map.Buildings.FirstOrDefault(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
        if (tc == null)
            return "kein Stadtzentrum";
        int frei = 0;
        for (int x = tc.X - 1; x <= tc.X + tc.Width; x++)
            for (int y = tc.Y - 1; y <= tc.Y + tc.Height; y++)
            {
                bool ring = x == tc.X - 1 || x == tc.X + tc.Width || y == tc.Y - 1 || y == tc.Y + tc.Height;
                if (ring && map.IsWalkable(x, y))
                    frei++;
            }
        return $"Stadtzentrum bei {tc.X},{tc.Y}: {frei} begehbare Kacheln ringsum";
    }
}
