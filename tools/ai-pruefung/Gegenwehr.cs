// Gegenwehr: greift der Spieler ein Gebäude der KI an, das weit vom Stadtzentrum steht,
// wehrt sich die KI - ihre Soldaten greifen die Angreifer an.
//
//   dotnet run --project tools/ai-pruefung -- gegenwehr
//
// Je Karte (drei frisch erzeugte, Standardgröße): ein fertiges Haus der KI 12 bis 14
// Kacheln von ihrem Stadtzentrum, also außerhalb von EconomyAi.ThreatRadius (8) und
// HomeDefenseRadius (Standard 10). Zwei Milizen des Spielers greifen es an, zwei Milizen
// der KI stehen am Stadtzentrum. Nur die KI (Owner 1, Standardprofil wie im Spiel) läuft.
// Verlangt binnen 30 s Spielzeit:
//   * eine Miliz der KI hat einen der Angreifer als Ziel (UnitTarget)
//   * ein Angreifer hat Lebenspunkte verloren - die KI schlägt auch zu, statt ihren
//     Schlag in jedem Takt neu anzusetzen
//
// Entstanden am 2026-10-11: der Nutzer meldete, dass die KI nicht reagiert, wenn seine
// Soldaten eines ihrer Gebäude angreifen.
using System.Reflection;
using AoE.Core.Ai;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using BuildingType = AoE.Core.Entities.BuildingType;

static class Gegenwehr
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);
    const int KARTEN = 3;
    const float FRIST = 30f;

    public static int Run()
    {
        var verstoesse = new List<string>();
        for (int karte = 1; karte <= KARTEN; karte++)
        {
            try
            {
                Karte(karte, verstoesse);
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
        Console.WriteLine($"\nGegenwehr stimmt ({KARTEN} Karten).");
        return 0;
    }

    static void Karte(int karte, List<string> verstoesse)
    {
        string wer = $"Karte {karte}, Gegenwehr";
        int vorher = verstoesse.Count;

        // Welt wie tools/spielablauf/Welt, ohne ScreenManager
        var screen = new RTSGameplayScreen(MapSize.Standard);
        var dims = MapSizes.Dimensions(MapSize.Standard, 1f);
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
        object Call(string name, params object[] args)
        {
            var m = T.GetMethods(F).FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length)
                ?? throw new InvalidOperationException($"Methode RTSGameplayScreen.{name}({args.Length} Argumente) nicht gefunden - umbenannt?");
            return m.Invoke(screen, args);
        }
        Set("tileMap", map);
        Set("gatherWorld", new TileMapGatherWorld(map));
        Set("player1", p1);
        Set("player2", p2);
        Set("units", units);
        map.UpdateFogOfWarForPlayer(0, units);
        map.UpdateFogOfWarForPlayer(1, units);
        Call("UpdatePopulationLimits");

        var tc = map.Buildings.FirstOrDefault(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
        if (tc == null)
        {
            verstoesse.Add($"{wer}: Testaufbau - die KI hat kein Stadtzentrum");
            return;
        }
        int cx = tc.X + tc.Width / 2, cy = tc.Y + tc.Height / 2;

        // Eine Kachel, von der aus das Stadtzentrum zu Fuß erreichbar ist: dort stehen die
        // Milizen der KI, und von dort muss ein Weg zum Haus führen
        var start = (Vector2?)Call("SpawnCell", tc)
            ?? throw new InvalidOperationException("Testaufbau - kein freier Platz am Stadtzentrum");
        var startWelt = map.GridToWorld(start);

        // Das Haus: 12 bis 14 Kacheln vom Stadtzentrum, rundum mindestens zwei begehbare
        // Kacheln im Ring, und zu Fuß erreichbar. Unter den Kandidaten der nächste an 13.
        (int X, int Y)? platz = null;
        int bester = int.MaxValue;
        for (int x = 1; x < map.Width - 3; x++)
            for (int y = 1; y < map.Height - 3; y++)
            {
                int d = Math.Max(Math.Abs(x + 1 - cx), Math.Abs(y + 1 - cy));
                if (d < 12 || d > 14 || Math.Abs(d - 13) >= bester)
                    continue;
                if (!map.CanPlaceBuilding(x, y, 2))
                    continue;
                var ring = Ring(map, x, y).ToList();
                if (ring.Count < 4)
                    continue;
                if (map.FindPath(startWelt, map.GridToWorld(new Vector2(ring[0].X, ring[0].Y))).Count == 0)
                    continue;
                platz = (x, y);
                bester = Math.Abs(d - 13);
            }
        if (platz == null)
        {
            verstoesse.Add($"{wer}: Testaufbau - kein Platz für das Haus 12 bis 14 Kacheln vom Stadtzentrum");
            return;
        }
        var haus = map.AddBuilding(platz.Value.X, platz.Value.Y, "Haus", 1, 2);
        map.InvalidateNavigation();
        if (!haus.IsComplete)
        {
            verstoesse.Add($"{wer}: Testaufbau - das Haus der KI ist nicht fertig, es sieht dann nichts");
            return;
        }

        // Zwei Milizen der KI am Stadtzentrum, zwei des Spielers am Haus
        var verteidiger = new List<Unit>();
        for (int i = 0; i < 2; i++)
        {
            var m = map.AddUnit(UnitType.Militia, (int)start.X, (int)start.Y, 1);
            p2.AddUnit(m);
            verteidiger.Add(m);
        }
        // Drei Kacheln vom Haus: steht eine Einheit schon auf ihrer Angriffskachel, findet
        // FindPath keinen Weg (Start- gleich Zielkachel), und AttackBuilding verwirft den Befehl
        var angreifer = new List<Unit>();
        foreach (var c in Ring(map, haus.X, haus.Y, 3).Take(2))
        {
            var m = map.AddUnit(UnitType.Militia, c.X, c.Y, 0);
            p1.AddUnit(m);
            angreifer.Add(m);
        }
        Call("AttackBuilding", angreifer, haus);
        if (angreifer.Any(a => a.AttackTarget != haus))
        {
            verstoesse.Add($"{wer}: Testaufbau - die Milizen des Spielers greifen das Haus nicht an");
            return;
        }
        var voll = angreifer.ToDictionary(a => a, a => a.Health);
        int hausVoll = haus.Health;

        // Nur die KI spielt, mit dem Profil aus dem Spiel
        screen.ActivateAi(new EconomyAi(), owner: 1);
        var agent = screen.Agents[1];

        const float dt = 1f / 60f;
        double zeit = 0, nebel = 0;
        float? tZiel = null, tTreffer = null;
        while (zeit < FRIST && (tZiel == null || tTreffer == null))
        {
            zeit += dt;
            Set("_gameSeconds", (double)Get("_gameSeconds") + dt);
            var gt = new GameTime(TimeSpan.FromSeconds(zeit), TimeSpan.FromSeconds(dt));
            Call("UpdateSheepClaims");
            Call("UpdateUnits", gt);
            Call("UpdatePopulationLimits");
            Call("UpdateTraining", dt);
            Call("UpdateConstruction", dt);
            nebel -= dt;
            if (nebel <= 0)
            {
                nebel = 0.25;
                map.UpdateFogOfWarForPlayer(0, units);
                map.UpdateFogOfWarForPlayer(1, units);
            }
            Call("DrainOrders");
            agent.Tick(dt);

            if (tZiel == null && verteidiger.Any(v => v.UnitTarget != null && angreifer.Contains(v.UnitTarget)))
                tZiel = (float)zeit;
            if (tTreffer == null && angreifer.Any(a => a.Health < voll[a]))
                tTreffer = (float)zeit;
        }

        string lage = $"Haus {platz.Value.X},{platz.Value.Y} ({bester + 13} Kacheln vom Stadtzentrum {cx},{cy}), Haus {haus.Health}/{hausVoll}; "
                      + "KI-Milizen: " + string.Join(", ", verteidiger.Select(v =>
                          $"{v.State}/{(v.UnitTarget != null ? (angreifer.Contains(v.UnitTarget) ? "Angreifer" : "andere Einheit") : v.AttackTarget != null ? "Gebäude" : "kein Ziel")}"
                          + $" bei {map.WorldToGrid(v.Position).X:0},{map.WorldToGrid(v.Position).Y:0}"));
        if (tZiel == null)
            verstoesse.Add($"{wer}: nach {FRIST:0} s greift keine Miliz der KI die Angreifer an - {lage}");
        if (tTreffer == null)
            verstoesse.Add($"{wer}: nach {FRIST:0} s hat kein Angreifer Lebenspunkte verloren - {lage}");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: Ziel nach {tZiel:0.0} s, erster Treffer nach {tTreffer:0.0} s - {lage}");
    }

    /// <summary>Die begehbaren Kacheln genau <paramref name="r"/> Kacheln (Chebyshev) um
    /// ein 2×2-Gebäude bei (x, y); r = 1 ist der Ring direkt daran.</summary>
    static IEnumerable<(int X, int Y)> Ring(TileMap map, int x, int y, int r = 1)
    {
        for (int rx = x - r; rx <= x + 1 + r; rx++)
            for (int ry = y - r; ry <= y + 1 + r; ry++)
            {
                int d = Math.Max(Math.Max(x - rx, rx - x - 1), Math.Max(y - ry, ry - y - 1));
                if (d == r && map.IsWalkable(rx, ry) && map.GetTile(rx, ry)?.ResourceType == null)
                    yield return (rx, ry);
            }
    }
}
