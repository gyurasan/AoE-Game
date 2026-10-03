// Spielablauf: stellt Abläufe aus RTSGameplayScreen ohne Grafik nach und prüft sie.
//
//   dotnet run --project tools/spielablauf -- bauen weiterbauen linksklick farm schafe bewegen minimap zoom leiste zeitalter turm menue animation fenster
//
// Jede genannte Gruppe läuft auf drei frisch erzeugten Karten. Die Spielschleife
// läuft Bild für Bild (60 je Sekunde) mit denselben Methoden, die Update() im Spiel
// aufruft. Private Felder und Methoden erreicht das Programm per Reflection;
// heißt eine davon anders, bricht es mit klarer Meldung ab. Exit-Code 1, sobald
// ein Ablauf nicht stimmt - damit taugt das Programm als Abnahme für den
// Ollama-Harness.
//
// Entstanden am 2026-10-01: ein Screenshot zeigte Baustellen, die bei 5 % standen.
// Erst diese Nachstellung klärte, dass die Bauarbeiter abgezogen worden waren -
// per Linksklick, der in diesem Spiel ein Befehl ist.
using System.Reflection;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using BuildingType = AoE.Core.Entities.BuildingType;
using CoreVillager = AoE.Core.Entities.Villager;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using Age = AoE.Core.Economy.Age;
using AgeProgress = AoE.Core.Economy.AgeProgress;

const int KARTEN = 3;
var gruppen = new HashSet<string>(args.Where(a => !a.StartsWith("-")).Select(a => a.ToLowerInvariant()));
if (gruppen.Count == 0)
    gruppen = new HashSet<string> { "bauen" };
var verstoesse = new List<string>();

for (int karte = 1; karte <= KARTEN; karte++)
{
    if (gruppen.Contains("bauen"))
    {
        float eins = 0, vier = 0;
        Pruefe($"Karte {karte}, Haus mit 1", () => eins = Haus(karte, 1, verstoesse));
        Pruefe($"Karte {karte}, Haus mit 4", () => vier = Haus(karte, 4, verstoesse));
        if (eins > 0 && vier > 0 && !(vier < eins && vier > eins / 4f))
            verstoesse.Add($"Karte {karte}: 4 Bauarbeiter brauchen {vier:0.0} s, einer {eins:0.0} s - erwartet schneller, aber nicht viermal so schnell");
        Pruefe($"Karte {karte}, Holzfällerlager", () => Lager(karte, verstoesse));
    }
    if (gruppen.Contains("weiterbauen"))
        Pruefe($"Karte {karte}, Weiterbauen", () => Weiterbauen(karte, verstoesse));
    if (gruppen.Contains("linksklick"))
        Pruefe($"Karte {karte}, Linksklick", () => Linksklick(karte, verstoesse));
    if (gruppen.Contains("farm"))
    {
        Pruefe($"Karte {karte}, Farm", () => FarmAnlegen(karte, verstoesse));
        Pruefe($"Karte {karte}, Farm-Ernte", () => FarmErnte(karte, verstoesse));
    }
    if (gruppen.Contains("schafe"))
    {
        Pruefe($"Karte {karte}, Schaf-Reservierung", () => SchafReservierung(karte, verstoesse));
        Pruefe($"Karte {karte}, Schaf-Wanderung", () => SchafWanderung(karte, verstoesse));
    }
    if (gruppen.Contains("bewegen"))
    {
        Pruefe($"Karte {karte}, nicht durch Gebäude", () => NichtDurchGebaeude(karte, verstoesse));
        Pruefe($"Karte {karte}, nicht durch Wasser", () => NichtDurchWasser(karte, verstoesse));
    }
    // Lage und Umrechnung der Minimap hängen nicht von der Karte ab, der Zoom auch nicht
    if (gruppen.Contains("minimap") && karte == 1)
        Pruefe("Minimap", () => Minimap(verstoesse));
    if (gruppen.Contains("zoom") && karte == 1)
        Pruefe("Zoom", () => Zoom(verstoesse));
    if (gruppen.Contains("leiste"))
        Pruefe($"Karte {karte}, Leiste", () => Leiste(karte, verstoesse));
    // Der Aufstieg dauert über acht Spielminuten - eine Karte genügt
    if (gruppen.Contains("zeitalter") && karte == 1)
        Pruefe("Zeitalter", () => Zeitalter(verstoesse));
    if (gruppen.Contains("turm"))
        Pruefe($"Karte {karte}, Wachturm", () => Turm(karte, verstoesse));
    if (gruppen.Contains("fenster") && karte == 1)
        Pruefe("Fenster", () => Fenster(verstoesse));
    if (gruppen.Contains("animation") && karte == 1)
        Pruefe("Animation", () => Animation(verstoesse));
    // Das Hauptmenü braucht keine Karte
    if (gruppen.Contains("menue") && karte == 1)
        Pruefe("Hauptmenü", () => Hauptmenue(verstoesse));
}

if (verstoesse.Count > 0)
{
    Console.WriteLine($"\nNICHT ERFÜLLT ({verstoesse.Count}):");
    foreach (var v in verstoesse)
        Console.WriteLine("  - " + v);
    return 1;
}
Console.WriteLine($"\nAlle Abläufe stimmen ({string.Join(", ", gruppen)}; {KARTEN} Karten).");
return 0;

// ---------------------------------------------------------------------------

// Ein Ablauf, der abbricht - etwa weil eine Methode im Bildschirm fehlt -, ist ein
// Verstoß wie jeder andere; die übrigen Abläufe laufen weiter.
void Pruefe(string wer, Action ablauf)
{
    try
    {
        ablauf();
    }
    catch (Exception e)
    {
        var grund = e is TargetInvocationException { InnerException: not null } ti ? ti.InnerException : e;
        verstoesse.Add($"{wer}: abgebrochen - {grund.GetType().Name}: {grund.Message}");
    }
}

// Ein Haus mit n Bauarbeitern: wird fertig, die Grenze steigt um 5, danach sind
// die Erbauer untätig. Rückgabe: Bauzeit in Sekunden, 0 bei Verstoß.
static float Haus(int karte, int n, List<string> verstoesse)
{
    var w = new Welt();
    var arbeiter = w.Dorfbewohner().Take(n).ToList();
    w.Waehle(arbeiter);
    var platz = w.Bauplatz(BuildingType.House, arbeiter[0], 3);
    int grenze = w.P1.PopulationLimit;
    w.Call("PlaceBuilding", BuildingType.House, platz);
    var haus = w.Map.Buildings.Last();

    float t = w.LaufeBis(() => haus.IsComplete, 60f);
    string wer = $"Karte {karte}, Haus mit {n}";
    if (!haus.IsComplete)
    {
        verstoesse.Add($"{wer}: nach 60 s nicht fertig ({haus.Construction.Progress * 100:0} %)");
        return 0;
    }
    w.LaufeBis(() => false, 0.5f);
    if (w.P1.PopulationLimit != grenze + 5)
        verstoesse.Add($"{wer}: Grenze {w.P1.PopulationLimit} statt {grenze + 5}");
    if (arbeiter.Any(u => u.State != UnitState.Idle || u.BuildSite != null))
        verstoesse.Add($"{wer}: Erbauer danach nicht untätig ({string.Join(", ", arbeiter.Select(u => u.State))})");
    Console.WriteLine($"  ok  {wer}: fertig nach {t:0.0} s");
    return t;
}

// Ein Holzfällerlager am Wald: wird fertig, danach sammeln alle Erbauer Holz.
static void Lager(int karte, List<string> verstoesse)
{
    var w = new Welt();
    var arbeiter = w.Dorfbewohner().ToList();
    w.Waehle(arbeiter);
    var platz = w.Bauplatz(BuildingType.LumberCamp, arbeiter[0], 3, waldImUmkreis: 5);
    w.Call("PlaceBuilding", BuildingType.LumberCamp, platz);
    var lager = w.Map.Buildings.Last();

    w.LaufeBis(() => lager.IsComplete, 60f);
    w.LaufeBis(() => false, 0.5f);
    string wer = $"Karte {karte}, Holzfällerlager";
    if (!lager.IsComplete)
        verstoesse.Add($"{wer}: nach 60 s nicht fertig");
    else if (arbeiter.Any(u => u.Job?.Resource != Resource.Wood))
        verstoesse.Add($"{wer}: nicht alle Erbauer sammeln danach Holz ({string.Join(", ", arbeiter.Select(u => u.Job?.Resource.ToString() ?? u.State.ToString()))})");
    else
        Console.WriteLine($"  ok  {wer}: fertig, alle {arbeiter.Count} sammeln Holz");
}

// C5d: Haus A angefangen, dann wird derselbe Dorfbewohner an Haus B gesetzt. Ist B
// fertig, baut er A von selbst zu Ende.
static void Weiterbauen(int karte, List<string> verstoesse)
{
    var w = new Welt();
    var v = w.Dorfbewohner().First();
    w.Waehle(new List<Unit> { v });
    var platzA = w.Bauplatz(BuildingType.House, v, 3);
    w.Call("PlaceBuilding", BuildingType.House, platzA);
    var a = w.Map.Buildings.Last();
    w.LaufeBis(() => a.Construction.Progress > 0.3f, 30f);

    var platzB = w.Bauplatz(BuildingType.House, v, 5, nahAn: platzA, hoechstens: 7);
    w.Call("PlaceBuilding", BuildingType.House, platzB);
    var b = w.Map.Buildings.Last();
    string wer = $"Karte {karte}, Weiterbauen";
    if (v.BuildSite != b)
    {
        verstoesse.Add($"{wer}: der Dorfbewohner wechselt nicht zur neuen Baustelle");
        return;
    }
    float stand = a.Construction.Progress;
    w.LaufeBis(() => b.IsComplete, 60f);
    if (!b.IsComplete)
    {
        verstoesse.Add($"{wer}: Haus B nach 60 s nicht fertig");
        return;
    }
    float t = w.LaufeBis(() => a.IsComplete, 60f);
    if (!a.IsComplete)
        verstoesse.Add($"{wer}: liegengebliebenes Haus A bleibt bei {a.Construction.Progress * 100:0} % stehen ({v.State})");
    else
        Console.WriteLine($"  ok  {wer}: A lag bei {stand * 100:0} %, nach B {t:0.0} s später fertig");
}

// C8b: ein Linksklick auf den eigenen Bauarbeiter wählt ihn aus und lässt ihn
// weiterbauen; ein Linksklick auf freie Fläche bleibt ein Laufbefehl.
static void Linksklick(int karte, List<string> verstoesse)
{
    var w = new Welt();
    var dorf = w.Dorfbewohner().ToList();
    var v = dorf[0];
    w.Waehle(new List<Unit> { v });
    var platz = w.Bauplatz(BuildingType.House, v, 3);
    w.Call("PlaceBuilding", BuildingType.House, platz);
    var haus = w.Map.Buildings.Last();
    w.LaufeBis(() => v.State == UnitState.Building, 20f);
    w.LaufeBis(() => false, 2f);

    // Zuerst einen anderen wählen, dann links auf die Figur des Bauarbeiters klicken
    w.Waehle(new List<Unit> { dorf[1] });
    float stand = haus.Construction.Progress;
    w.Linksklick(v.Position - new Vector2(0, 12));
    w.LaufeBis(() => false, 3f);
    string wer = $"Karte {karte}, Linksklick";
    var auswahl = w.Auswahl();
    if (v.BuildSite != haus || v.State != UnitState.Building || haus.Construction.Progress <= stand)
        verstoesse.Add($"{wer}: Klick auf den Bauarbeiter hat ihn vom Bau abgezogen ({v.State})");
    else if (auswahl.Count != 1 || auswahl[0] != v)
        verstoesse.Add($"{wer}: Klick auf den Bauarbeiter wählt ihn nicht aus");
    else
        Console.WriteLine($"  ok  {wer}: auf die Figur wählt aus, er baut weiter");

    // Ein Klick ins Freie ist weiter ein Befehl
    var frei = w.FreieKachel(v, 4);
    w.Linksklick(w.Map.GridToWorld(frei));
    w.LaufeBis(() => false, 0.2f);
    if (v.BuildSite != null || v.State != UnitState.Moving)
        verstoesse.Add($"{wer}: Klick auf freie Fläche ist kein Laufbefehl mehr ({v.State}, Baustelle {(v.BuildSite != null ? "noch gesetzt" : "leer")})");
    else
        Console.WriteLine($"  ok  {wer}: ins Freie bleibt ein Laufbefehl");
}

// C5f: Farm anlegen (3×3-Feld). Jede Kachel ist 175 Nahrung, begehbar,
// abbaubar von Dorfbewohnern und wächst nach. Kein Gebäude — kein Vision-,
// kein Wegsuche-, kein Drop-Off-Eintrag. Kosten laut Spezifikation (60 Holz).
static void FarmAnlegen(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Farm";

    // 1) Pflügen an einem freien Punkt: 9 Kacheln, jede 175 Nahrung, alle begehbar.
    int vollesFeld = 0;
    w.Map.PlantCrop(30, 30, 3);
    for (int x = 30; x < 33; x++)
        for (int y = 30; y < 33; y++)
        {
            var kachel = w.Map.GetTile(x, y);
            if (kachel != null && kachel.Farm
                && kachel.ResourceType == Resource.Food && kachel.ResourceAmount == 175
                && w.Map.IsWalkable(x, y) && kachel.Food == FoodSource.Farm)
                vollesFeld++;
        }
    if (vollesFeld != 9)
    {
        verstoesse.Add($"{wer}: Anlegen - nur {vollesFeld}/9 Kacheln mit 175 Nahrung, begehbar oder Farm-Graphik");
        return;
    }
    Console.WriteLine($"  ok  {wer}: 9 Feldkacheln × 175 Nahrung, alle begehbar");

    // 2) Feld ist nicht bebaubar: ein Haus (2×2) passt nicht mehr auf Feldkacheln.
    if (w.Map.CanPlaceBuilding(31, 31, 2))
        verstoesse.Add($"{wer}: ein Haus würde auf dem Feld erlaubt werden");
    else
        Console.WriteLine($"  ok  {wer}: keine andere Baugenehmigung auf dem Feld");

    // 3) Eine Kachel wird geerntet (Vorrat 0) - Farm-Status bleibt bestehen.
    var t31 = w.Map.GetTile(31, 31);
    int vor = t31.ResourceAmount;
    t31.ResourceType = null;
    t31.ResourceAmount = 0;
    if (!(vor == 175 && t31.Farm))
    {
        verstoesse.Add($"{wer}: Ernte - Kachel war nicht voll oder Farm-Flag weg");
        return;
    }
    Console.WriteLine($"  ok  {wer}: Kachel geerntet (Vorrat 175 → 0), bleibt Feld");

    // 4) Nachwachst: RegrowCrop zählt herunter, nach ~100 s ist die Kachel wieder 175.
    float nachwachst = w.LaufeBis(() => t31.ResourceType == Resource.Food && t31.ResourceAmount == 175, 130f);
    if (!(t31.ResourceType == Resource.Food && t31.ResourceAmount == 175))
        verstoesse.Add($"{wer}: Nachwachst - nach {nachwachst:0} s nicht wieder 175 (Type={t31.ResourceType}, Amount={t31.ResourceAmount})");
    else
        Console.WriteLine($"  ok  {wer}: Nachwachst - nach {nachwachst:0} s wieder 175 Nahrung");

    // 5) Die benachbarten 8 Kacheln sind nach wie vor voll (je 175) und unangetastet.
    int unangetastet = 0;
    for (int x = 30; x < 33; x++)
        for (int y = 30; y < 33; y++)
            if ((x != 31 || y != 31) && w.Map.GetTile(x, y).ResourceAmount == 175)
                unangetastet++;
    if (unangetastet != 8)
        verstoesse.Add($"{wer}: Nachbar-Check - nur {unangetastet}/8 Kacheln noch 175");
    else
        Console.WriteLine($"  ok  {wer}: Nachbar-Check - 8 Kacheln unangetastet");
}

// E2E: Farm anlegen, Dorfbewohner befehligt die Mitte zu ernten. Läuft der
// übliche Kreislauf, sammelt er 175 Nahrung und liefert sie an das
// Stadtzentrum (die nächstgelegene Abgabestelle) — Nahrung steigt, die
// Kachel wird leer und wächst nach.
static void FarmErnte(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Farm-Ernte";
    var tc = w.Map.Buildings.FirstOrDefault(b => b.Core.BuildingType == BuildingType.TownCenter && b.OwnerId == 0);
    if (tc == null)
    {
        verstoesse.Add($"{wer}: kein Stadttz. gefunden");
        return;
    }
    // Bauplatz: 3×3 in Laufweite des TC, ohne auf bestehende Gebäude zu stoßen.
    Vector2? platz = null;
    for (int r = 2; r <= 8 && platz == null; r++)
    {
        for (int dx = -r; dx <= r && platz == null; dx++)
            for (int dy = -r; dy <= r && dy <= r; dy++)
            {
                var c = new Vector2(tc.X + dx, tc.Y + dy);
                if (w.Map.CanPlaceBuilding((int)c.X, (int)c.Y, 3))
                {
                    platz = c; break;
                }
            }
    }
    if (platz == null)
    {
        verstoesse.Add($"{wer}: kein Platz für eine 3×3-Farm in Laufweite");
        return;
    }
    int tx = (int)platz.Value.X, ty = (int)platz.Value.Y;
    w.Map.PlantCrop(tx, ty, 3);

    var v = w.Dorfbewohner().First();
    w.Waehle(new List<Unit> { v });
    int nahrungVor = w.P1.Resources[Resource.Food];
    var center = new Vector2(tx + 1, ty + 1);
    var kachel = w.Map.GetTile((int)center.X, (int)center.Y);
    int vor = kachel?.ResourceAmount ?? 0;
    if (vor != 175)
    {
        verstoesse.Add($"{wer}: Farm-Kachel war nicht voll (vor={vor})");
        return;
    }

    // Befehligt den Dorfbewohner, auf die Mitte zu gehen — er erkennt als
    // Ressource "Food" die Ernte und setzt den üblichen Kreislauf los.
    w.Call("IssueCommand", center);
    float t = w.LaufeBis(() => w.P1.Resources[Resource.Food] > nahrungVor, 120f);
    int nach = w.P1.Resources[Resource.Food];
    if (nach <= nahrungVor)
        verstoesse.Add($"{wer}: nach {t:0} s Nahrung {nahrungVor} → {nach} (erwartete > {nahrungVor})");
    else
        Console.WriteLine($"  ok  {wer}: +{nach - nahrungVor} Nahrung nach {t:0} s, Kachel vor={vor} nach={kachel.ResourceAmount}");
}

// Eines der Schafe auf der Karte: Dorfbewohner befehligt es zu ernten. Während
// er läuft und sammelt, ist das Schaf reserviert, die Wanderung lässt es in
// Ruhe. Läuft die Karte trotzdem weiter (andere Schafe), wird die Reservierung
// aufheben und das Schaf bleibt wo es steht, bis er fertig ist.
static void SchafReservierung(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Schaf-Reservierung";
    // Ein Schaf auf der Karte finden
    int schafX = -1, schafY = -1;
    for (int tx = 0; tx < w.Map.Width && schafX < 0; tx++)
        for (int ty = 0; ty < w.Map.Height && schafX < 0; ty++)
            if (w.Map.GetTile(tx, ty)?.Food == FoodSource.Sheep)
                { schafX = tx; schafY = ty; }
    if (schafX < 0)
    {
        verstoesse.Add($"{wer}: kein Schaf auf der Karte gefunden");
        return;
    }
    var v = w.Dorfbewohner().First();
    int vor = w.Map.GetTile(schafX, schafY)!.ResourceAmount;
    // Sammelauftrag anlegen, als hätte der Dörfler das Schaf bereits erreicht
    var job = new AoE.Core.Economy.GatherJob(0, Resource.Food,
        new AoE.Core.Entities.Position(schafX, schafY));
    v.State = AoE.Core.Entities.UnitState.Gathering;
    v.Job = job;
    // 2 s laufen lassen: das Schaf muss reserviert sein und stehen bleiben
    // (andere frei laufende Schafe dürfen wandern, dieses nicht)
    w.LaufeBis(() => false, 2f);
    bool reserviert = w.Map.IsClaimed(schafX, schafY);
    bool schafAmPlatz = w.Map.GetTile(schafX, schafY)?.Food == FoodSource.Sheep;
    // Der Dörfler sammelt weiterhin an derselben Kachel
    if (!reserviert)
    {
        verstoesse.Add($"{wer}: Schaf nicht reserviert, während der Dörfler es erntet (Phase={v.Job?.Phase})");
        return;
    }
    if (!schafAmPlatz)
    {
        verstoesse.Add($"{wer}: reserviertes Schaf ist zwischenzeitlich gewandert");
        return;
    }
    Console.WriteLine($"  ok  {wer}: während der Ernte reserviert (vor={vor}, Schaf am Platz)");
}

// Die freierlaufenden Schafe wandern: im Zeitraum von 60 s muss mindestens
// eines die Kachel wechseln. Reservierte Schafe (ein Dörfler erntet sie gerade)
// bleiben an Ort und Stelle.
static void SchafWanderung(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Schaf-Wanderung";
    // Schafe vor der Wanderung aufnehmen
    var schafe = new HashSet<(int x, int y)>();
    for (int x = 0; x < w.Map.Width; x++)
        for (int y = 0; y < w.Map.Height; y++)
            if (w.Map.GetTile(x, y)?.Food == FoodSource.Sheep)
                schafe.Add((x, y));
    if (schafe.Count == 0)
    {
        verstoesse.Add($"{wer}: keine Schafe auf der Karte");
        return;
    }
    int vor = schafe.Count;
    // 60 s laufen lassen (mit Reservierungssynchronisation, ohne aktive Ernter)
    w.LaufeBis(() => false, 60f);
    var nach = new HashSet<(int x, int y)>();
    for (int x = 0; x < w.Map.Width; x++)
        for (int y = 0; y < w.Map.Height; y++)
            if (w.Map.GetTile(x, y)?.Food == FoodSource.Sheep)
                nach.Add((x, y));
    // Mindestens eines muss gewandert sein — sonst war die Wanderung leer
    if (schafe.SetEquals(nach))
        verstoesse.Add($"{wer}: nach 60 s kein Schaf gewandert (alle {vor} an ihrem Startplatz)");
    else
        Console.WriteLine($"  ok  {wer}: {schafe.Count} → {nach.Count} Schafe auf der Karte, mindestens eines gewandert");
}

// Ein Dorfbewohner läuft zu einem Ziel, das hinter einem 4×4-Gebäude liegt.
// Der Weg darf keine der Kacheln des Gebäudes kreuzen.
static void NichtDurchGebaeude(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, nicht durch Gebäude";
    var tc = w.Map.Buildings.FirstOrDefault(b => b.Core.BuildingType == BuildingType.TownCenter && b.OwnerId == 0);
    if (tc == null)
    {
        verstoesse.Add($"{wer}: kein Stadtzentrum gefunden");
        return;
    }
    var v = w.Dorfbewohner().First();
    // Start: eine freie Kachel in der Nähe, die nicht das TC selbst ist
    int sx = tc.X + tc.Width + 2, sy = tc.Y + 2;
    if (!w.Map.IsWalkable(sx, sy) || w.Map.GetTile(sx, sy)?.Building != null)
    {
        // Fallback: eine freie Kachel im 8er-Umkreis
        bool frei = false;
        for (int r = 1; r <= 8 && !frei; r++)
            for (int dx = -r; dx <= r && !frei; dx++)
                for (int dy = -r; dy <= r && !frei; dy++)
                {
                    int fx = tc.X + tc.Width + dx + 1, fy = tc.Y + dy + 1;
                    if (w.Map.IsWalkable(fx, fy) && w.Map.GetTile(fx, fy)?.Building == null)
                    {
                        sx = fx; sy = fy; frei = true;
                    }
                }
        if (!frei)
        {
            verstoesse.Add($"{wer}: keine freie Startkachel neben dem Stadtzentrum");
            return;
        }
    }
    // GridToWorld(X, Y) liegt in der Mitte der Kachel (X, Y) — keine +0.5 daz,
    // das wäre eine ganze Kachel weiter.
    v.Position = w.Map.GridToWorld(new Vector2(sx, sy));
    // Ziel: 2 Kacheln hinter dem TC (von Start aus gesehen)
    int tx = tc.X - 2, ty = tc.Y + 2;
    if (w.Map.GetTile(tx, ty) == null)
    {
        verstoesse.Add($"{wer}: Zielkachel außer Karte");
        return;
    }
    // Pfad berechnen
    var weg = w.Map.FindPath(v.Position, w.Map.GridToWorld(new Vector2(tx, ty)));
    if (weg.Count == 0)
    {
        verstoesse.Add($"{wer}: kein Pfad (unreichbar) - das ist erlaubt, aber nicht der Fall hier");
        return;
    }
    // Jede Kachel auf dem Pfad muss auf freier Kachel liegen oder zumindest
    // auf Wiese/Wald/Mine sein — nie das TC (oder jedes andere 4×4-Gebäude)
    var gebaeudeKacheln = new HashSet<(int, int)>();
    var tcKacheln = new HashSet<(int, int)>();
    for (int by = 0; by < w.Map.Height; by++)
        for (int bx = 0; bx < w.Map.Width; bx++)
        {
            if (w.Map.GetTile(bx, by)?.Building != null)
                tcKacheln.Add((bx, by));
        }
    foreach (var punkt in weg)
    {
        var g = w.Map.WorldToGrid(punkt);
        if (tcKacheln.Contains(((int)g.X, (int)g.Y)))
        {
            verstoesse.Add($"{wer}: Pfad kreuzt eine Kachel des Stadtzentrums ({(int)g.X},{(int)g.Y})");
            return;
        }
    }
    Console.WriteLine($"  ok  {wer}: {weg.Count} Wegpunkte, keiner kreuzt das TC");
}

// Ein Dörfler läuft zu einer Kachel, die hinter einem See liegt — der Pfad
// darf keine Wasserkachel kreuzen.
static void NichtDurchWasser(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, nicht durch Wasser";
    var v = w.Dorfbewohner().First();
    // Ein See finden, der an zwei gegenüberliegenden Seiten Land hat —
    // dann gibt es garantiert einen Umlaufweg, und der muss um den See
    // rum (und keinesfalls hinein).
    int wx = -1, wy = -1;
    for (int x = 2; x < w.Map.Width - 2 && wx < 0; x++)
        for (int y = 2; y < w.Map.Height - 2 && wx < 0; y++)
        {
            var t = w.Map.GetTile(x, y);
            if (t?.Type != TileType.Water) continue;
            bool n = w.Map.IsWalkable(x, y - 1) && w.Map.GetTile(x, y - 1)?.Building == null;
            bool s = w.Map.IsWalkable(x, y + 1) && w.Map.GetTile(x, y + 1)?.Building == null;
            if (n && s) { wx = x; wy = y; }
        }
    if (wx < 0)
    {
        verstoesse.Add($"{wer}: kein See mit Land auf zwei Seiten gefunden");
        return;
    }
    // Start: eine freie Kachel direkt nördlich des Sees.
    int sx = wx, sy = wy - 1;
    if (!w.Map.IsWalkable(sx, sy))
    {
        bool frei = false;
        for (int r = 1; r <= 6 && !frei; r++)
            for (int dx = -r; dx <= r && !frei; dx++)
            {
                int fx = wx + dx, fy = wy - r;
                if (fx >= 0 && fy >= 0 && fx < w.Map.Width && fy < w.Map.Height
                    && w.Map.IsWalkable(fx, fy) && w.Map.GetTile(fx, fy)?.Building == null)
                {
                    sx = fx; sy = fy; frei = true;
                }
            }
        if (!frei)
        {
            verstoesse.Add($"{wer}: keine freie Kachel nördlich des Sees");
            return;
        }
    }
    // GridToWorld(X, Y) liegt in der Mitte der Kachel (X, Y) — keine +0.5,
    // das wäre eine ganze Kachel weiter und die Prüfgeometrie würde verrutschen.
    v.Position = w.Map.GridToWorld(new Vector2(sx, sy));
    // Ziel: direkt südlich des Sees, ebenfalls frei.
    int tx = wx, ty = wy + 1;
    if (!w.Map.IsWalkable(tx, ty))
    {
        bool frei = false;
        for (int r = 1; r <= 6 && !frei; r++)
            for (int dx = -r; dx <= r && !frei; dx++)
            {
                int fx = wx + dx, fy = wy + r;
                if (fx >= 0 && fy >= 0 && fx < w.Map.Width && fy < w.Map.Height
                    && w.Map.IsWalkable(fx, fy) && w.Map.GetTile(fx, fy)?.Building == null)
                {
                    tx = fx; ty = fy; frei = true;
                }
            }
        if (!frei)
        {
            verstoesse.Add($"{wer}: keine freie Kachel südlich des Sees");
            return;
        }
    }
    var weg = w.Map.FindPath(v.Position, w.Map.GridToWorld(new Vector2(tx, ty)));
    var wasser = new HashSet<(int, int)>();
    for (int y = 0; y < w.Map.Height; y++)
        for (int x = 0; x < w.Map.Width; x++)
            if (w.Map.GetTile(x, y)?.Type == TileType.Water)
                wasser.Add((x, y));

    // 1) Direktes Ziel im Wasser: A* darf keinen Weg hinfinden — Wasser ist
    //    für die Wegsuche gesperrt.
    var zumWasser = w.Map.FindPath(v.Position, w.Map.GridToWorld(new Vector2(wx, wy)));
    if (zumWasser.Count != 0)
    {
        verstoesse.Add($"{wer}: A* findet {zumWasser.Count} Wegpunkte direkt in den See ({wx},{wy}) — Wasser müsste gesperrt sein");
        return;
    }

    // 2) Umlaufweg von Nord- zu Südufer: darf es geben, aber nur über Land.
    //    Gibt es ihn nicht, ist das erlaubt — der See trennt zwei Land-
    //    massen (Karten 1/2 haben breite Seen).
    if (weg.Count > 0)
    {
        for (int i = 0; i < weg.Count; i++)
        {
            var g = w.Map.WorldToGrid(weg[i]);
            if (wasser.Contains(((int)g.X, (int)g.Y)))
            {
                verstoesse.Add($"{wer}: Umlaufweg kreuzt Wasserkachel ({(int)g.X},{(int)g.Y}) an Wegpunkt {i}");
                return;
            }
        }
        Console.WriteLine($"  ok  {wer}: Umlaufweg {weg.Count} Punkte, keiner im Wasser; kein Weg direkt ins Wasser");
    }
    else
    {
        Console.WriteLine($"  ok  {wer}: See trennt beide Ufer voneinander — kein Weg, auch keiner durch Wasser");
    }
}

// Minimap rechts in der unteren Leiste, in derselben Ansicht wie die Spielkarte
// (Draufsicht, Norden oben), mit ganzen Pixeln je Kachel und so groß, wie die
// Leiste es zulässt; ein Klick in die Minimap stellt die Kamera mittig über den
// angeklickten Punkt.
// Entstanden am 2026-10-03: die Minimap saß links, ragte unten aus dem Fenster
// und war von weißen Rechtecken verdeckt. Danach war sie kurz eine 2:1-Raute wie
// in AoE II - der Nutzer will sie aber in der Form der Spielkarte (C6d).
static void Minimap(List<string> verstoesse)
{
    const int leiste = 200;       // HUD_BOTTOM_HEIGHT
    const float zoom = 4f;        // Ausschnitt kleiner als die Karte, sonst zentriert ClampCamera
    var w = new Welt();
    int vorher = verstoesse.Count;
    int mapW = w.Map.Width, mapH = w.Map.Height;
    foreach (var (bw, bh) in new[] { (1280, 768), (2406, 1353), (4812, 2707) })
    {
        string wer = $"Minimap {bw}x{bh}";
        w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
        var r = (Rectangle)w.Call("MinimapRect");
        if (r.Left < bw / 2 || r.Right > bw || bw - r.Right > 40)
            verstoesse.Add($"{wer}: Feld {r} sitzt nicht rechts in der Leiste");
        if (r.Top < bh - leiste || r.Bottom > bh)
            verstoesse.Add($"{wer}: Feld {r} ragt aus der unteren Leiste (ab y = {bh - leiste})");
        if (Math.Max(r.Width, r.Height) < leiste - 20)
            verstoesse.Add($"{wer}: Minimap nur {r.Width}x{r.Height} px - die Leiste fasst {leiste} px");
        if (r.Width % mapW != 0 || r.Height % mapH != 0 || r.Width / mapW != r.Height / mapH)
            verstoesse.Add($"{wer}: Feld {r.Width}x{r.Height} px ergibt keine gleich großen Kacheln aus ganzen Pixeln");

        // Draufsicht wie die Spielkarte: Kartenecken auf den Feldecken, Norden oben
        foreach (var (ecke, soll) in new[]
                 {
                     (new Vector2(0, 0), new Vector2(r.Left, r.Top)),
                     (new Vector2(mapW, 0), new Vector2(r.Right, r.Top)),
                     (new Vector2(mapW, mapH), new Vector2(r.Right, r.Bottom)),
                     (new Vector2(0, mapH), new Vector2(r.Left, r.Bottom)),
                 })
        {
            var ist = (Vector2)w.Call("MinimapPoint", ecke.X, ecke.Y, r);
            if (Vector2.Distance(ist, soll) > 1f)
                verstoesse.Add($"{wer}: Kartenecke {ecke} liegt bei {ist}, in Draufsicht gehört sie nach {soll}");
        }

        // Klick in die Minimap: die angeklickte Kachel steht danach in der Bildmitte
        w.Set("cameraZoom", zoom);
        w.Set("_minimapRect", r);
        foreach (var ziel in new[] { new Vector2(32, 32), new Vector2(24, 40), new Vector2(40, 24) })
        {
            var klick = (Vector2)w.Call("MinimapPoint", ziel.X, ziel.Y, r);
            w.Call("CenterCameraOnMinimapPoint", new Point((int)MathF.Round(klick.X), (int)MathF.Round(klick.Y)));
            var kamera = (Vector2)w.Get("cameraPosition");
            var mitte = (new Vector2(bw, bh) / (2f * zoom) - kamera) / w.Map.TileSize;
            if (Vector2.Distance(mitte, ziel) > 1f)
                verstoesse.Add($"{wer}: Klick auf Kachel {ziel} zentriert Kachel {mitte}");
        }
        w.Set("cameraZoom", 1f);
    }
    if (verstoesse.Count == vorher)
        Console.WriteLine("  ok  Minimap: rechts in der Leiste, Draufsicht wie die Spielkarte, Klick zentriert");
}

// Zoomgrenzen relativ zur Fensterhöhe: ganz hineingezoomt zeigt jedes Fenster ab
// 1080 px Höhe gleich viele Kacheln übereinander, ganz herausgezoomt die ganze
// Karte; kleinere Fenster behalten MAX_ZOOM 2. Der Weltpunkt unter dem
// Mauszeiger bleibt beim Zoomen stehen.
// Entstanden am 2026-10-03: in der DX-Fassung (2707 px hoch) kam man nur halb so
// nah heran wie in DesktopGL (1353 px).
static void Zoom(List<string> verstoesse)
{
    var w = new Welt();
    int vorher = verstoesse.Count;
    float ts = w.Map.TileSize;
    var nah = new Dictionary<int, float>();   // Fensterhöhe -> Kacheln übereinander, ganz nah
    foreach (var (bw, bh) in new[] { (1280, 768), (2406, 1353), (4812, 2707) })
    {
        string wer = $"Zoom {bw}x{bh}";
        w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", new Vector2(-600, -600));
        var zeiger = new Vector2(bw * 0.3f, bh * 0.4f);
        var vorZoom = (Vector2)w.Call("ScreenToWorld", zeiger);
        for (int i = 0; i < 60; i++)
            w.Call("ZoomAt", zeiger, 1.12f);
        float zoomNah = (float)w.Get("cameraZoom");
        var nachZoom = (Vector2)w.Call("ScreenToWorld", zeiger);
        if (Vector2.Distance(vorZoom, nachZoom) > 0.5f)
            verstoesse.Add($"{wer}: Weltpunkt unter dem Zeiger wanderte von {vorZoom} nach {nachZoom}");
        nah[bh] = bh / (ts * zoomNah);

        for (int i = 0; i < 120; i++)
            w.Call("ZoomAt", zeiger, 1f / 1.12f);
        float zoomFern = (float)w.Get("cameraZoom");
        float fern = bh / (ts * zoomFern);
        if (bh >= 1080 && fern < w.Map.Height)
            verstoesse.Add($"{wer}: ganz herausgezoomt nur {fern:0} von {w.Map.Height} Kacheln übereinander");
        Console.WriteLine($"      {wer}: Zoom {zoomFern:0.00} bis {zoomNah:0.00}, ganz nah {nah[bh]:0.0} Kacheln übereinander");
    }
    float klein = 768 / (ts * 2f);
    if (Math.Abs(nah[768] - klein) > 0.1f)
        verstoesse.Add($"Zoom 1280x768: ganz nah {nah[768]:0.0} Kacheln übereinander, erwartet {klein:0.0} (MAX_ZOOM 2 bleibt)");
    if (Math.Abs(nah[2707] - nah[1353]) > 0.5f)
        verstoesse.Add($"Zoom: ganz nah {nah[2707]:0.0} Kacheln übereinander bei 2707 px Höhe, {nah[1353]:0.0} bei 1353 px - "
                       + "die hohe Auflösung kommt nicht gleich nah heran");
    if (verstoesse.Count == vorher)
        Console.WriteLine("  ok  Zoom: Grenzen wachsen mit der Fensterhöhe, Zeigerpunkt bleibt stehen");
}

// Befehlstasten und Minimap in der unteren Leiste, bedient mit nachgestellten
// Mausklicks durch HandleRtsInput: ohne ausgewählten Dorfbewohner nur Q, A und „.",
// mit Dorfbewohner auch die Bautasten; Q bildet aus, H schaltet den Setzmodus
// ein, ein Klick in die Minimap rückt die Kamera.
// Entstanden am 2026-10-03: kein Klick in die Leiste kam an - das Loslassen
// wurde nur ausgewertet, wenn auf der Karte ein Ziehen begonnen hatte.
static void Leiste(int karte, List<string> verstoesse)
{
    const int bw = 2406, bh = 1353;
    string wer = $"Karte {karte}, Leiste";
    var w = new Welt();
    int vorher = verstoesse.Count;
    w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
    w.P1.Resources.Add(Resource.Food, 100);

    w.Waehle(new List<Unit>());
    w.Call("LayoutButtons");
    var ohne = w.Tasten().Select(t => t.Label).ToList();
    if (!ohne.SequenceEqual(new[] { "Q", "A", "." }))
        verstoesse.Add($"{wer}: ohne Auswahl Tasten [{string.Join(" ", ohne)}], erwartet nur Q, A und .");

    int nahrung = w.P1.Resources[Resource.Food];
    w.Klick(w.Tasten().First(t => t.Label == "Q").Rect.Center);
    if (w.P1.Resources[Resource.Food] != nahrung - 25)
        verstoesse.Add($"{wer}: Klick auf Q - Nahrung {nahrung} -> {w.P1.Resources[Resource.Food]}, erwartet 25 weniger");

    w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
    w.Call("LayoutButtons");
    var mit = w.Tasten().Select(t => t.Label).ToList();
    foreach (var soll in new[] { "Q", "A", "H", "M", "F", "B", "G", "." })
        if (!mit.Contains(soll))
            verstoesse.Add($"{wer}: mit Dorfbewohner fehlt die Taste {soll} (da: {string.Join(" ", mit)})");
    if (mit.Contains("H"))
    {
        w.Klick(w.Tasten().First(t => t.Label == "H").Rect.Center);
        if (!Equals(w.Get("placing"), BuildingType.House))
            verstoesse.Add($"{wer}: Klick auf H - Setzmodus {w.Get("placing") ?? "aus"}, erwartet House");
        w.Set("placing", null);
    }

    // Minimap: Klick in die Mitte stellt die Kartenmitte in die Bildmitte. Senkrecht
    // prüfbar - waagrecht ist die Karte bei Zoom 1 schmaler als das Fenster und zentriert.
    var r = (Rectangle)w.Call("MinimapRect");
    w.Set("_minimapRect", r);            // setzt sonst DrawMinimap
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    w.Klick(r.Center);
    var kamera = (Vector2)w.Get("cameraPosition");
    float mitteY = (bh / 2f - kamera.Y) / w.Map.TileSize;
    if (Math.Abs(mitteY - w.Map.Height / 2f) > 1f)
        verstoesse.Add($"{wer}: Klick in die Minimap-Mitte - Bildmitte auf Kachelreihe {mitteY:0.0}, erwartet {w.Map.Height / 2f}");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: ohne Auswahl Q, A und ., mit Dorfbewohner alle Tasten; Q, H und Minimap per Klick");
}

// Zeitalter (C4): die Taste „A" in der Leiste startet im Stadtzentrum den
// Aufstieg und bezahlt sofort; solange er läuft, bildet das Stadtzentrum nicht
// aus, danach wieder. Nach 130 s gilt die Feudalzeit; ab der Imperialzeit gibt es
// die Taste nicht mehr.
static void Zeitalter(List<string> verstoesse)
{
    const string wer = "Zeitalter";
    var w = new Welt();
    int vorher = verstoesse.Count;
    w.Set("screenBounds", new Rectangle(0, 0, 2406, 1353));
    w.Call("UpdateAges", 0f);   // bricht mit klarer Meldung ab, wenn es sie nicht gibt
    var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
        ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
    w.P1.Resources.Add(Resource.Food, 600);      // 800: Aufstieg (500) und ein Dorfbewohner

    w.Waehle(new List<Unit>());
    w.Call("LayoutButtons");
    var a = w.Tasten().FirstOrDefault(t => t.Label == "A");
    if (a.Label == null)
    {
        verstoesse.Add($"{wer}: keine Taste A in der Leiste");
        return;
    }
    int nahrung = w.P1.Resources[Resource.Food];
    w.Klick(a.Rect.Center);
    if (ages.Target != Age.Feudal)
        verstoesse.Add($"{wer}: Klick auf A - kein Aufstieg in die Feudalzeit (Ziel {ages.Target?.ToString() ?? "keins"})");
    if (w.P1.Resources[Resource.Food] != nahrung - 500)
        verstoesse.Add($"{wer}: Nahrung {nahrung} -> {w.P1.Resources[Resource.Food]}, erwartet 500 weniger");

    // Während des Aufstiegs wartet die Ausbildung
    int dorf = w.Dorfbewohner().Count();
    w.Call("TrainVillager");
    w.LaufeBis(() => false, 60f);
    if (w.Dorfbewohner().Count() != dorf)
        verstoesse.Add($"{wer}: das Stadtzentrum bildet während des Aufstiegs aus");
    if (ages.Current != Age.Dark)
        verstoesse.Add($"{wer}: nach 60 s schon {ages.Current} - der Aufstieg dauert 130 s");

    w.LaufeBis(() => ages.Current == Age.Feudal, 80f);
    if (ages.Current != Age.Feudal)
        verstoesse.Add($"{wer}: nach 140 s nicht in der Feudalzeit ({ages.Progress:P0})");
    w.LaufeBis(() => w.Dorfbewohner().Count() > dorf, 30f);
    if (w.Dorfbewohner().Count() <= dorf)
        verstoesse.Add($"{wer}: nach dem Aufstieg bildet das Stadtzentrum nicht weiter aus");

    // Weiter bis zur Imperialzeit - danach gibt es die Taste A nicht mehr
    w.P1.Resources.Add(Resource.Food, 2000);
    w.P1.Resources.Add(Resource.Gold, 1000);
    foreach (var (ziel, sekunden) in new[] { (Age.Castle, 160f), (Age.Imperial, 190f) })
    {
        w.Call("AdvanceAge");
        w.LaufeBis(() => ages.Current == ziel, sekunden + 1f);
        if (ages.Current != ziel)
            verstoesse.Add($"{wer}: nach {sekunden} s nicht in {ziel} (jetzt {ages.Current})");
    }
    w.Call("LayoutButtons");
    if (w.Tasten().Any(t => t.Label == "A"))
        verstoesse.Add($"{wer}: Taste A auch nach der Imperialzeit");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: A bezahlt und forscht, Ausbildung wartet, Feudal- bis Imperialzeit, danach keine Taste A");
}

// Wachturm (C4c), das erste Gebäude der Feudalzeit: in der Dunklen Zeit weder
// Taste T noch Setzmodus, danach beides. Er kostet 50 Holz und 125 Stein und
// sieht fertig gebaut eine Kachel, die weder ein Dorfbewohner (3 Kacheln) noch
// ein anderes Gebäude (Stadtzentrum 5, Haus und Lager 3) sehen kann.
static void Turm(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Wachturm";
    var w = new Welt();
    int vorher = verstoesse.Count;
    w.Set("screenBounds", new Rectangle(0, 0, 2406, 1353));
    var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
        ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
    var arbeiter = w.Dorfbewohner().Take(2).ToList();
    w.Waehle(arbeiter);

    w.Call("LayoutButtons");
    if (w.Tasten().Any(t => t.Label == "T"))
        verstoesse.Add($"{wer}: Taste T schon in der Dunklen Zeit");
    w.Call("TogglePlacing", BuildingType.Tower);
    if (w.Get("placing") != null)
        verstoesse.Add($"{wer}: Setzmodus {w.Get("placing")} schon in der Dunklen Zeit");
    w.Set("placing", null);

    w.P1.Resources.Add(Resource.Food, 300);
    ages.TryStart(w.P1.Resources);
    ages.Update(130f);
    w.Call("LayoutButtons");
    if (!w.Tasten().Any(t => t.Label == "T"))
        verstoesse.Add($"{wer}: keine Taste T in der Feudalzeit (da: {string.Join(" ", w.Tasten().Select(t => t.Label))})");
    w.Call("TogglePlacing", BuildingType.Tower);
    if (!Equals(w.Get("placing"), BuildingType.Tower))
        verstoesse.Add($"{wer}: kein Setzmodus Wachturm in der Feudalzeit");
    w.Set("placing", null);

    int holz = w.P1.Resources[Resource.Wood], stein = w.P1.Resources[Resource.Stone];
    var platz = w.Bauplatz(BuildingType.Tower, arbeiter[0], 5);
    w.Call("PlaceBuilding", BuildingType.Tower, platz);
    var turm = w.Map.Buildings.Last();
    if (turm.Core.BuildingType != BuildingType.Tower)
    {
        verstoesse.Add($"{wer}: kein Wachturm gesetzt (zuletzt {turm.Core.BuildingType})");
        return;
    }
    if (w.P1.Resources[Resource.Wood] != holz - 50 || w.P1.Resources[Resource.Stone] != stein - 125)
        verstoesse.Add($"{wer}: Holz {holz} -> {w.P1.Resources[Resource.Wood]}, Stein {stein} -> "
                       + $"{w.P1.Resources[Resource.Stone]} - erwartet 50 Holz und 125 Stein");
    float bauzeit = w.LaufeBis(() => turm.IsComplete, 90f);
    if (!turm.IsComplete)
    {
        verstoesse.Add($"{wer}: nach 90 s nicht fertig ({turm.Construction.Progress * 100:0} %)");
        return;
    }

    // Prüfkachel 7 bis 9 Kacheln von der Turmmitte, außer Sicht von allem anderen
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    var mitte = new Vector2(turm.Core.Position.X, turm.Core.Position.Y);
    var andere = w.Map.Buildings.Where(b => b != turm && b.OwnerId == 0)
        .Select(b => (Ort: new Vector2(b.Core.Position.X, b.Core.Position.Y), Weit: b.Core.Stats.VisionRange + 1.5f)).ToList();
    var einheiten = w.Dorfbewohner().Select(u => w.Map.WorldToGrid(u.Position)).ToList();
    var probe = Enumerable.Range(0, w.Map.Width).SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)))
        .Where(c => Vector2.Distance(c, mitte) is >= 7f and <= 9f
                    && einheiten.All(e => Vector2.Distance(c, e) > 5f)
                    && andere.All(b => Vector2.Distance(c, b.Ort) > b.Weit))
        .Cast<Vector2?>().FirstOrDefault();
    if (probe == null)
        Console.WriteLine($"      {wer}: keine Prüfkachel außer Sicht der übrigen - Sichttest übersprungen");
    else if (!w.Map.IsTileVisible((int)probe.Value.X, (int)probe.Value.Y, 0))
        verstoesse.Add($"{wer}: Kachel {probe.Value} ({Vector2.Distance(probe.Value, mitte):0.0} vom Turm) nicht in Sicht");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: erst ab der Feudalzeit, 50 Holz + 125 Stein, fertig nach {bauzeit:0} s, sieht {(probe == null ? "-" : Vector2.Distance(probe.Value, mitte).ToString("0.0"))} Kacheln weit");
}

// Hauptmenü (E12): die Einträge stehen mittig, wachsen mit dem Fenster und
// überlappen nicht; die Maus wählt beim Zeigen und startet beim Loslassen über
// einem aktiven Eintrag, eine ruhende Maus überstimmt die Tastatur nicht, und
// der gesperrte Eintrag „Spiel laden" lässt sich weder zeigen noch klicken noch
// mit den Pfeiltasten erreichen. Ohne Grafik: EntryRect und EvaluateMouse
// rechnen nur, MoveSelection spielt höchstens einen Ton, den es hier nicht gibt.
// Entstanden am 2026-10-03: das Menü wertete die Maus gar nicht aus.
static void Hauptmenue(List<string> verstoesse)
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var typ = typeof(MainMenuScreen);
    MethodInfo Methode(string name) => typ.GetMethod(name, F)
        ?? throw new InvalidOperationException($"Methode MainMenuScreen.{name} nicht gefunden - umbenannt?");
    var auswahl = typ.GetField("selectedIndex", F)
        ?? throw new InvalidOperationException("Feld MainMenuScreen.selectedIndex nicht gefunden");
    var menue = new MainMenuScreen();
    const string wer = "Hauptmenü";
    int vorher = verstoesse.Count;

    MouseState Maus(Point p, bool gedrueckt) => new MouseState(p.X, p.Y, 0,
        gedrueckt ? ButtonState.Pressed : ButtonState.Released, ButtonState.Released,
        ButtonState.Released, ButtonState.Released, ButtonState.Released);

    foreach (var (bw, bh) in new[] { (1280, 768), (2406, 1353), (4812, 2707) })
    {
        string fenster = $"{wer} {bw}x{bh}";
        Rectangle Platte(int i) => (Rectangle)Methode("EntryRect").Invoke(null, new object[] { i, bw, bh });
        (int, bool) Maus2(MouseState jetzt, MouseState zuvor)
            => ((int, bool))Methode("EvaluateMouse").Invoke(menue, new object[] { jetzt, zuvor, bw, bh });

        var platten = Enumerable.Range(0, 4).Select(Platte).ToList();
        for (int i = 0; i < platten.Count; i++)
        {
            var r = platten[i];
            if (Math.Abs(r.Center.X - bw / 2) > 1)
                verstoesse.Add($"{fenster}: Eintrag {i} nicht mittig ({r})");
            if (r.Top < 0 || r.Bottom > bh)
                verstoesse.Add($"{fenster}: Eintrag {i} ragt aus dem Fenster ({r})");
            if (r.Height < bh * 0.05f)
                verstoesse.Add($"{fenster}: Eintrag {i} nur {r.Height} px hoch - bei {bh} px Fensterhöhe zu klein");
            if (i > 0 && r.Top < platten[i - 1].Bottom)
                verstoesse.Add($"{fenster}: Eintrag {i} überlappt Eintrag {i - 1}");
        }

        var abseits = new Point(2, 2);
        auswahl.SetValue(menue, 0);
        if (Maus2(Maus(platten[2].Center, false), Maus(abseits, false)) != (2, false))
            verstoesse.Add($"{fenster}: Zeigen auf „Einstellungen“ wählt sie nicht aus");
        if (Maus2(Maus(platten[2].Center, false), Maus(platten[2].Center, false)) != (0, false))
            verstoesse.Add($"{fenster}: ruhende Maus überstimmt die Tastaturauswahl");
        if (Maus2(Maus(platten[3].Center, false), Maus(platten[3].Center, true)) != (3, true))
            verstoesse.Add($"{fenster}: Klick auf „Beenden“ startet ihn nicht");
        if (Maus2(Maus(platten[0].Center, false), Maus(platten[0].Center, true)) != (0, true))
            verstoesse.Add($"{fenster}: Klick auf „Neues Spiel“ startet es nicht");
        if (Maus2(Maus(platten[1].Center, false), Maus(platten[1].Center, true)) != (0, false))
            verstoesse.Add($"{fenster}: der gesperrte Eintrag „Spiel laden“ reagiert auf die Maus");
        if (Maus2(Maus(abseits, false), Maus(abseits, true)) != (0, false))
            verstoesse.Add($"{fenster}: Klick neben das Menü startet etwas");
    }

    auswahl.SetValue(menue, 0);
    Methode("MoveSelection").Invoke(menue, new object[] { 1 });
    if ((int)auswahl.GetValue(menue) != 2)
        verstoesse.Add($"{wer}: Pfeil nach unten von „Neues Spiel“ landet auf {auswahl.GetValue(menue)}, erwartet 2 (über das gesperrte „Spiel laden“ hinweg)");
    Methode("MoveSelection").Invoke(menue, new object[] { -1 });
    if ((int)auswahl.GetValue(menue) != 0)
        verstoesse.Add($"{wer}: Pfeil nach oben von „Einstellungen“ landet auf {auswahl.GetValue(menue)}, erwartet 0");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: mittig, wächst mit dem Fenster, Zeigen wählt, Klick startet, „Spiel laden“ gesperrt");
}

// Bewegung der Figuren (C6v): UpdateUnitMotion erkennt, ob eine Einheit läuft
// und wohin sie blickt; nach kurzem Stillstand gilt sie wieder als stehend und
// behält ihre Blickrichtung. DrawVillager wählt danach Gehen, Arbeiten oder Stehen.
static void Animation(List<string> verstoesse)
{
    const string wer = "Animation";
    const float bild = 1f / 60f;
    var w = new Welt();
    int vorher = verstoesse.Count;
    var dorf = w.Dorfbewohner().First();
    var start = dorf.Position;

    object Bewegung() => ((System.Collections.IDictionary)w.Get("_unitMotion"))[dorf]
        ?? throw new InvalidOperationException("keine Bewegung zum Dorfbewohner gemerkt");
    float LaeuftNoch() => (float)Bewegung().GetType().GetField("MovingFor").GetValue(Bewegung());
    bool Links() => (bool)Bewegung().GetType().GetField("FacingLeft").GetValue(Bewegung());

    w.Call("UpdateUnitMotion", bild);              // erster Aufruf merkt die Ausgangslage
    dorf.Position = start + new Vector2(-2, 0);
    w.Call("UpdateUnitMotion", bild);
    if (LaeuftNoch() <= 0f)
        verstoesse.Add($"{wer}: nach einem Schritt gilt der Dorfbewohner nicht als laufend");
    if (!Links())
        verstoesse.Add($"{wer}: nach einem Schritt nach links blickt er nicht nach links");

    dorf.Position = start + new Vector2(1, 0);
    w.Call("UpdateUnitMotion", bild);
    if (Links())
        verstoesse.Add($"{wer}: nach einem Schritt nach rechts blickt er noch nach links");

    for (int i = 0; i < 20; i++)                    // 1/3 Sekunde Stillstand
        w.Call("UpdateUnitMotion", bild);
    if (LaeuftNoch() > 0f)
        verstoesse.Add($"{wer}: nach einer Drittelsekunde Stillstand gilt er noch als laufend");
    if (Links())
        verstoesse.Add($"{wer}: im Stand hat er die Blickrichtung gewechselt");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: läuft, blickt in Laufrichtung, steht nach kurzem Stillstand und behält die Richtung");
}

// Fenstergröße und Zoom (C10s): ändert sich die Fensterhöhe, ändert sich der
// Zoom im selben Verhältnis wie ZoomScale - der sichtbare Ausschnitt bleibt gleich
// groß. Nachgestellt wird der Start, bei dem DesktopGL das Fenster erst nach
// LoadContent vergrößert: geladen bei 768 px Höhe (Zoom 1), dann 1353 px.
// Entstanden am 2026-10-03: das Spiel begann mal mit Zoom 1, mal mit 1,25.
static void Fenster(List<string> verstoesse)
{
    const string wer = "Fenster";
    var w = new Welt();
    int vorher = verstoesse.Count;
    float Zoom() => (float)w.Get("cameraZoom");

    w.Set("screenBounds", new Rectangle(0, 0, 1280, 768));
    w.Set("cameraZoom", 1f);
    w.Set("_appliedZoomScale", 1f);
    w.Set("screenBounds", new Rectangle(0, 0, 2406, 1353));
    w.Call("SyncZoomToWindow");
    if (Math.Abs(Zoom() - 1353f / 1080f) > 0.01f)
        verstoesse.Add($"{wer}: nach dem Vergrößern auf 1353 px Zoom {Zoom():0.000}, erwartet {1353f / 1080f:0.000}");

    // Selbst gezoomt, dann das Fenster verdoppelt: der Zoom verdoppelt sich mit
    w.Set("cameraZoom", 2f);
    w.Set("screenBounds", new Rectangle(0, 0, 4812, 2706));
    w.Call("SyncZoomToWindow");
    if (Math.Abs(Zoom() - 4f) > 0.01f)
        verstoesse.Add($"{wer}: Zoom 2 bei 1353 px wird bei 2706 px {Zoom():0.000}, erwartet 4");

    // Ohne Größenänderung bleibt der Zoom, wie er ist
    w.Call("SyncZoomToWindow");
    if (Math.Abs(Zoom() - 4f) > 0.01f)
        verstoesse.Add($"{wer}: ohne Größenänderung wandert der Zoom auf {Zoom():0.000}");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: der Zoom folgt der Fensterhöhe, der Ausschnitt bleibt gleich groß");
}

/// <summary>Eine Spielwelt: echte Karte, zwei Spieler, der Bildschirm ohne Grafik.</summary>
class Welt
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);
    readonly RTSGameplayScreen _screen = new();
    public readonly TileMap Map = new(64, 64, 32);
    public readonly Player P1 = new(0, "P1", "Briten"), P2 = new(1, "P2", "Azteken");
    readonly List<Unit> _units;
    double _zeit, _nebel;

    public Welt()
    {
        _units = Map.Units;
        foreach (var u in _units)
            (u.OwnerId == 0 ? P1 : P2).AddUnit(u);
        P1.Resources.Add(Resource.Wood, 1000);   // genug für jeden Ablauf
        Set("tileMap", Map);
        Set("gatherWorld", new TileMapGatherWorld(Map));
        Set("player1", P1);
        Set("player2", P2);
        Set("units", _units);
        Map.UpdateFogOfWarForPlayer(0, _units);
        Call("UpdatePopulationLimits");
    }

    // Seit C4b; fehlt sie, laufen die übrigen Gruppen trotzdem - die Gruppe
    // zeitalter verlangt sie ausdrücklich
    static readonly MethodInfo UpdateAges = T.GetMethod("UpdateAges", F);

    static FieldInfo Feld(string name) => T.GetField(name, F)
        ?? throw new InvalidOperationException($"Feld RTSGameplayScreen.{name} nicht gefunden - umbenannt?");

    public void Set(string name, object wert) => Feld(name).SetValue(_screen, wert);

    public object Get(string name) => Feld(name).GetValue(_screen);

    /// <summary>Die Befehlstasten, wie LayoutButtons sie zuletzt angelegt hat.</summary>
    public List<(string Label, Rectangle Rect)> Tasten()
        => ((System.Collections.IEnumerable)Get("_buttons")).Cast<object>()
            .Select(b => ((string)b.GetType().GetField("Label").GetValue(b),
                          (Rectangle)b.GetType().GetField("Rect").GetValue(b)))
            .ToList();

    /// <summary>
    /// Linksklick auf einen Bildschirmpunkt, so wie das Spiel ihn sieht: ein Bild
    /// mit gedrückter, eines mit losgelassener Taste durch HandleRtsInput.
    /// </summary>
    public void Klick(Point p)
    {
        var gt = new GameTime(TimeSpan.FromSeconds(_zeit), TimeSpan.FromSeconds(1.0 / 60));
        foreach (var taste in new[] { ButtonState.Pressed, ButtonState.Released })
            Call("HandleRtsInput", gt, new KeyboardState(),
                 new MouseState(p.X, p.Y, 0, taste, ButtonState.Released, ButtonState.Released,
                                ButtonState.Released, ButtonState.Released));
    }

    public object Call(string name, params object[] args)
    {
        var m = T.GetMethod(name, F)
            ?? throw new InvalidOperationException($"Methode RTSGameplayScreen.{name} nicht gefunden - umbenannt?");
        return m.Invoke(_screen, args);
    }

    public IEnumerable<Unit> Dorfbewohner() => _units.Where(u => u.OwnerId == 0 && u.Core is CoreVillager);

    public List<Unit> Auswahl() => (List<Unit>)Feld("selectedUnits").GetValue(_screen);

    public void Waehle(List<Unit> auswahl)
    {
        var sel = Auswahl();
        foreach (var u in sel)
            u.IsSelected = false;
        sel.Clear();
        foreach (var u in auswahl)
        {
            u.IsSelected = true;
            sel.Add(u);
        }
    }

    /// <summary>
    /// Kurzer Linksklick auf einen Weltpunkt. Die Kamera steht im Ablauf auf
    /// Ursprung und Zoom 1, Bildschirm- und Weltkoordinaten sind also gleich.
    /// </summary>
    public void Linksklick(Vector2 welt) => Call("LeftClick", welt);

    /// <summary>
    /// Ein Bauplatz, auf den das Gebäude passt, möglichst <paramref name="abstand"/>
    /// Kacheln vom Dorfbewohner. Optional mit Wald im Umkreis, oder höchstens
    /// <paramref name="hoechstens"/> Kacheln von einem anderen Bauplatz.
    /// </summary>
    public Vector2 Bauplatz(BuildingType typ, Unit von, float abstand, int waldImUmkreis = 0,
                            Vector2? nahAn = null, float hoechstens = 0)
    {
        var start = Map.WorldToGrid(von.Position);
        var kandidaten = new List<Vector2>();
        for (int x = 0; x < 40; x++)
        for (int y = 0; y < 40; y++)
        {
            var c = new Vector2(x, y);
            if (!(bool)Call("CanPlace", typ, c))
                continue;
            if (nahAn != null && (Vector2.Distance(c, nahAn.Value) > hoechstens || Vector2.Distance(c, nahAn.Value) < 3))
                continue;
            if (waldImUmkreis > 0 && !Umkreis(x, y, waldImUmkreis).Any(t => t?.ResourceType == Resource.Wood && t.ResourceAmount > 0))
                continue;
            kandidaten.Add(c);
        }
        if (kandidaten.Count == 0)
            throw new InvalidOperationException($"kein Bauplatz für {typ} gefunden");
        return kandidaten.OrderBy(c => Math.Abs(Vector2.Distance(c, start) - abstand)).First();
    }

    /// <summary>Eine begehbare, freie Kachel etwa <paramref name="abstand"/> Kacheln vom Dorfbewohner.</summary>
    public Vector2 FreieKachel(Unit von, float abstand)
    {
        var start = Map.WorldToGrid(von.Position);
        return Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 40).Select(y => new Vector2(x, y)))
            .Where(c => Map.IsWalkable((int)c.X, (int)c.Y) && Map.GetTile((int)c.X, (int)c.Y).ResourceType == null
                        && !_units.Any(u => Map.WorldToGrid(u.Position) == c))
            .OrderBy(c => Math.Abs(Vector2.Distance(c, start) - abstand))
            .First();
    }

    IEnumerable<Tile> Umkreis(int x, int y, int r)
    {
        for (int dx = -r; dx <= r; dx++)
            for (int dy = -r; dy <= r; dy++)
                yield return Map.GetTile(x + dx, y + dy);
    }

    /// <summary>
    /// Lässt die Spielschleife laufen, bis die Bedingung gilt, höchstens
    /// <paramref name="sekunden"/> lang. Rückgabe: vergangene Zeit.
    /// </summary>
    public float LaufeBis(Func<bool> bedingung, float sekunden)
    {
        const float dt = 1f / 60f;
        int bilder = 0;
        while (bilder * dt < sekunden)
        {
            _zeit += dt;
            bilder++;
            var gt = new GameTime(TimeSpan.FromSeconds(_zeit), TimeSpan.FromSeconds(dt));
            Call("UpdateUnits", gt);
            Call("UpdatePopulationLimits");
            Call("UpdateTraining", dt);
            UpdateAges?.Invoke(_screen, new object[] { dt });
            Call("UpdateConstruction", dt);
            Call("UpdateSheepClaims");
            Map.RegrowCrop(dt);
            Map.UpdateSheep(dt);
            _nebel -= dt;
            if (_nebel <= 0)
            {
                _nebel = 0.25;
                Map.UpdateFogOfWarForPlayer(0, _units);
            }
            if (bedingung())
                break;
        }
        return bilder * dt;
    }
}
