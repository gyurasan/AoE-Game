// Kartenprüfung: erzeugt viele Karten und prüft Regeln des Kartengenerators.
//
//   dotnet run --project tools/kartenpruefung -- rohstoffe start wild groessen
//
// Jede genannte Regelgruppe wird geprüft; ohne Angabe nur die Statistik.
// Exit-Code 1, sobald eine Regel auf irgendeiner Karte verletzt ist – damit
// taugt das Programm als Abnahme für den Ollama-Harness.
using AgeOfEvolutions.Core.Data;
using Resource = AoE.Core.Entities.Resource;

const int KARTEN = 50;
// Schalter wie --nologo reicht dotnet run unter Umständen durch – das sind keine Regelgruppen
var gruppen = new HashSet<string>(args.Where(a => !a.StartsWith("-")).Select(a => a.ToLowerInvariant()));
var verstoesse = new List<string>();

var erwartet = new Dictionary<TileType, Resource>
{
    [TileType.Forest] = Resource.Wood,
    [TileType.Mountain] = Resource.Stone,
    [TileType.GoldMine] = Resource.Gold,
};
var abbaubar = new Dictionary<TileType, int>();
var anzahl = new Dictionary<TileType, int>();
var naechster = new Dictionary<(int Spieler, Resource Art), List<int>>();
int fische = 0, kartenMitFisch = 0;
int reheGesamt = 0, schafeGesamt = 0, ohneReh = 0, kaninchenGesamt = 0, schweineGesamt = 0, beerenGesamt = 0;
var startJagd = new List<int>();   // Abstand der nächsten Rehe zu jedem Stadtzentrum

for (int k = 0; k < KARTEN; k++)
{
    var map = new TileMap(64, 64, 32);
    int fischeHier = 0;

    for (int x = 0; x < map.Width; x++)
    for (int y = 0; y < map.Height; y++)
    {
        var t = map.GetTile(x, y);
        anzahl[t.Type] = anzahl.GetValueOrDefault(t.Type) + 1;
        if (erwartet.TryGetValue(t.Type, out var soll) && t.ResourceType == soll && t.ResourceAmount > 0)
            abbaubar[t.Type] = abbaubar.GetValueOrDefault(t.Type) + 1;

        // Fisch: im Wasser, als Nahrung, und vom Ufer aus erreichbar. Vergleich
        // über den Namen, damit die Prüfung schon übersetzt, bevor es
        // FoodSource.Fish gibt – sie ist vor dem Fischen entstanden.
        if (t.Food.ToString() != "Fish")
            continue;
        fischeHier++;
        bool ufer = false;
        for (int dx = -1; dx <= 1; dx++)
        for (int dy = -1; dy <= 1; dy++)
            if ((dx != 0 || dy != 0) && map.IsWalkable(x + dx, y + dy))
                ufer = true;
        if (gruppen.Contains("fisch") && (t.Type != TileType.Water || t.ResourceType != Resource.Food
                                          || t.ResourceAmount <= 0 || !ufer))
            verstoesse.Add($"Karte {k}: Fisch bei ({x}, {y}) ungueltig – Typ {t.Type}, " +
                           $"Ressource {t.ResourceType}, Menge {t.ResourceAmount}, Ufer daneben: {ufer}");
    }
    fische += fischeHier;
    if (fischeHier > 0) kartenMitFisch++;

    // Abstand der nächsten Stein- und Goldkachel zur Mitte jedes Stadtzentrums
    foreach (var b in map.Buildings)
    {
        int mx = b.X + b.Width / 2, my = b.Y + b.Height / 2;
        foreach (var art in new[] { Resource.Stone, Resource.Gold })
        {
            int best = int.MaxValue;
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var t = map.GetTile(x, y);
                if (t.ResourceType == art && t.ResourceAmount > 0)
                    best = Math.Min(best, Math.Abs(x - mx) + Math.Abs(y - my));
            }
            var liste = naechster.TryGetValue((b.OwnerId, art), out var l) ? l : naechster[(b.OwnerId, art)] = new();
            liste.Add(best);
            if (gruppen.Contains("start") && best > 14)
                verstoesse.Add($"Karte {k}: Spieler {b.OwnerId} hat {art} erst in {(best == int.MaxValue ? "keiner" : best.ToString())} Kacheln Entfernung (erlaubt: 14)");
        }
    }

    // Wild (C7d): fast so viele Rehe wie Schafe, auf jeder Karte welche, und jeder
    // Spieler hat eine Rehherde in Reichweite seines Stadtzentrums
    int rehe = 0, schafe = 0, kaninchen = 0, schweine = 0, beeren = 0;
    for (int x = 0; x < map.Width; x++)
    for (int y = 0; y < map.Height; y++)
    {
        var futter = map.GetTile(x, y).Food;
        if (futter == FoodSource.Deer) rehe++;
        else if (futter == FoodSource.Sheep) schafe++;
        else if (futter == FoodSource.Rabbit) kaninchen++;
        else if (futter == FoodSource.Boar) schweine++;
        else if (futter == FoodSource.Berries) beeren++;
    }
    reheGesamt += rehe;
    schafeGesamt += schafe;
    beerenGesamt += beeren;
    if (rehe == 0) ohneReh++;
    if (gruppen.Contains("wild") && rehe == 0)
        verstoesse.Add($"Karte {k}: kein einziges Reh");
    kaninchenGesamt += kaninchen;
    schweineGesamt += schweine;
    if (gruppen.Contains("wild") && (kaninchen == 0 || schweine == 0))
        verstoesse.Add($"Karte {k}: {kaninchen} Kaninchen und {schweine} Wildschweine - beide Arten gehören auf jede Karte");
    int reichweite = MapSettings.Default.DeerStartDistanceMax + 4;   // dazu die Herde: 2 Kacheln je Richtung
    foreach (var b in map.Buildings.Where(b => b.Type == "Stadtzentrum"))
    {
        int best = int.MaxValue;
        for (int x = 0; x < map.Width; x++)
        for (int y = 0; y < map.Height; y++)
            if (map.GetTile(x, y).Food == FoodSource.Deer)
                best = Math.Min(best, Math.Abs(x - b.X) + Math.Abs(y - b.Y));
        startJagd.Add(best);
        if (gruppen.Contains("wild") && best > reichweite)
            verstoesse.Add($"Karte {k}: Spieler {b.OwnerId} hat das nächste Reh erst in {(best == int.MaxValue ? "keiner" : best.ToString())} Kacheln Entfernung (erlaubt: {reichweite})");
    }
}

Console.WriteLine($"{KARTEN} Karten, 64 x 64");
foreach (var (typ, soll) in erwartet)
{
    int n = anzahl.GetValueOrDefault(typ), a = abbaubar.GetValueOrDefault(typ);
    Console.WriteLine($"  {typ,-9} {n,6} Kacheln, abbaubar als {soll}: {a,6}");
    if (gruppen.Contains("rohstoffe") && a != n)
        verstoesse.Add($"{typ}: {n - a} von {n} Kacheln tragen nicht {soll}");
}
Console.WriteLine($"  Fisch: {fische} Schwaerme, auf {kartenMitFisch}/{KARTEN} Karten");
// Jede Karte hat Seen (AddLakes legt 2 bis 4 an); ein paar ohne Fisch sind Zufall,
// die Mehrheit ohne Fisch hiesse: es wird keiner gesetzt.
if (gruppen.Contains("fisch") && kartenMitFisch < KARTEN * 9 / 10)
    verstoesse.Add($"nur {kartenMitFisch} von {KARTEN} Karten haben Fisch (erwartet: mindestens 90 %)");
float verhaeltnis = schafeGesamt > 0 ? reheGesamt / (float)schafeGesamt : 0f;
Console.WriteLine($"  Wild: {reheGesamt / (float)KARTEN:0.0} Rehe und {schafeGesamt / (float)KARTEN:0.0} Schafe je Karte " +
                  $"(Rehe = {verhaeltnis:P0} der Schafe), {ohneReh} Karten ohne Reh, " +
                  $"nächstes Reh im Schnitt {startJagd.Where(d => d != int.MaxValue).DefaultIfEmpty(0).Average():0.0} Kacheln vom Stadtzentrum");
Console.WriteLine($"  Kleinwild: {kaninchenGesamt / (float)KARTEN:0.0} Kaninchen und {schweineGesamt / (float)KARTEN:0.0} Wildschweine je Karte");
Console.WriteLine($"  Beerenbuusche: {beerenGesamt / (float)KARTEN:0.0} je Karte");
if (gruppen.Contains("wild") && (verhaeltnis < 0.55f || verhaeltnis > 1.0f))
    verstoesse.Add($"Rehe sind {verhaeltnis:P0} der Schafe - erwartet: etwa 60 bis 100 % (seit 2026-10-04 ~70 %)");
int felsen = anzahl.GetValueOrDefault(TileType.Rock);
Console.WriteLine($"  Deko-Felsen (Rock): {felsen}");
if (gruppen.Contains("rohstoffe") && felsen > 0)
    verstoesse.Add($"{felsen} graue Deko-Felsen ohne Ressource");
foreach (var ((spieler, art), werte) in naechster.OrderBy(e => e.Key.Spieler))
{
    var gefunden = werte.Where(w => w != int.MaxValue).ToList();
    Console.WriteLine($"  Spieler {spieler}, naechstes {art,-5}: im Schnitt " +
                      (gefunden.Count > 0 ? $"{gefunden.Average(),5:0.0}, hoechstens {gefunden.Max(),3}" : "  -  ") +
                      $" Kacheln von der Mitte des Stadtzentrums ({gefunden.Count}/{KARTEN} Karten)");
}

// Kartengrößen (C11): jede Größe gleich dicht besetzt - Wald, Stein, Gold, Wasser,
// Schafe und Rehe je Fläche wie auf der Standardkarte -, und beide Spieler haben
// Stein und Gold in Laufweite ihres Stadtzentrums
if (gruppen.Contains("groessen"))
{
    const int PROBEN = 10;
    var arten = new (string Name, Func<Tile, bool> Zaehlt)[]
    {
        ("Wald", t => t.Type == TileType.Forest), ("Stein", t => t.Type == TileType.Mountain),
        ("Gold", t => t.Type == TileType.GoldMine), ("Wasser", t => t.Type == TileType.Water),
        ("Schafe", t => t.Food == FoodSource.Sheep), ("Rehe", t => t.Food == FoodSource.Deer),
    };
    var dichte = new Dictionary<MapSize, double[]>();
    foreach (var groesse in Enum.GetValues<MapSize>())
    {
        int seite = MapSizes.Side(groesse);
        var summe = new double[arten.Length];
        for (int k = 0; k < PROBEN; k++)
        {
            var map = new TileMap(seite, seite, 32, MapSettings.ForSize(groesse));
            if (map.Width != seite || map.Height != seite)
                verstoesse.Add($"{groesse}: Karte {map.Width}x{map.Height} statt {seite}x{seite}");
            for (int x = 0; x < map.Width; x++)
            for (int y = 0; y < map.Height; y++)
            {
                var t = map.GetTile(x, y);
                for (int a = 0; a < arten.Length; a++)
                    if (arten[a].Zaehlt(t)) summe[a] += 1.0 / (seite * seite) / PROBEN;
            }
            foreach (var b in map.Buildings.Where(b => b.Type == "Stadtzentrum"))
            {
                int mx = b.X + b.Width / 2, my = b.Y + b.Height / 2;
                foreach (var art in new[] { Resource.Stone, Resource.Gold })
                {
                    int best = int.MaxValue;
                    for (int x = 0; x < map.Width; x++)
                    for (int y = 0; y < map.Height; y++)
                    {
                        var t = map.GetTile(x, y);
                        if (t.ResourceType == art && t.ResourceAmount > 0)
                            best = Math.Min(best, Math.Abs(x - mx) + Math.Abs(y - my));
                    }
                    if (best > 14)
                        verstoesse.Add($"{groesse}, Karte {k}: Spieler {b.OwnerId} hat {art} erst in {best} Kacheln Entfernung (erlaubt: 14)");
                }
            }
        }
        dichte[groesse] = summe;
        Console.WriteLine($"  {MapSizes.Name(groesse),-8} {seite,3}x{seite,-3} je 1000 Kacheln: " +
                          string.Join(", ", arten.Select((art, a) => $"{art.Name} {summe[a] * 1000:0}")));
    }
    foreach (var groesse in Enum.GetValues<MapSize>().Where(g => g != MapSize.Standard))
        for (int a = 0; a < arten.Length; a++)
        {
            double relativ = dichte[groesse][a] / dichte[MapSize.Standard][a];
            if (relativ < 0.5 || relativ > 1.6)
                verstoesse.Add($"{MapSizes.Name(groesse)}: {arten[a].Name} {relativ:P0} so dicht wie auf der Standardkarte (erwartet 50 bis 160 % - die festen Startvorräte zählen auf der kleinen Karte mehr)");
        }
}

if (verstoesse.Count > 0)
{
    Console.WriteLine($"NICHT ERFUELLT ({verstoesse.Count} Verstoesse, die ersten 10):");
    foreach (var v in verstoesse.Take(10))
        Console.WriteLine("  - " + v);
    return 1;
}
Console.WriteLine(gruppen.Count > 0 ? $"erfuellt: {string.Join(", ", gruppen)}" : "nur Statistik, keine Regel geprueft");
return 0;
