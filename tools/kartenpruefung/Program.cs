// Kartenprüfung: erzeugt viele Karten und prüft Regeln des Kartengenerators.
//
//   dotnet run --project tools/kartenpruefung -- rohstoffe start
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

if (verstoesse.Count > 0)
{
    Console.WriteLine($"NICHT ERFUELLT ({verstoesse.Count} Verstoesse, die ersten 10):");
    foreach (var v in verstoesse.Take(10))
        Console.WriteLine("  - " + v);
    return 1;
}
Console.WriteLine(gruppen.Count > 0 ? $"erfuellt: {string.Join(", ", gruppen)}" : "nur Statistik, keine Regel geprueft");
return 0;
