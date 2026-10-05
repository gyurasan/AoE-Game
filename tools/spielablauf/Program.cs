// Spielablauf: stellt Abläufe aus RTSGameplayScreen ohne Grafik nach und prüft sie.
//
//   dotnet run --project tools/spielablauf -- bauen weiterbauen linksklick farm schafe wild herde bewegen minimap zoom leiste zeitalter turm menue animation fenster werkzeug feld fahne gehen karten pfad wind
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
using CorePosition = AoE.Core.Entities.Position;
using GatherJob = AoE.Core.Economy.GatherJob;

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
    if (gruppen.Contains("wild"))
    {
        Pruefe($"Karte {karte}, Reh-Reservierung", () => RehReservierung(karte, verstoesse));
        Pruefe($"Karte {karte}, Reh-Wanderung", () => RehWanderung(karte, verstoesse));
    }
    if (gruppen.Contains("fahne") && karte == 1)
        Pruefe("Fahne und Windrad", () => FahneUndWindrad(verstoesse));
    if (gruppen.Contains("herde"))
    {
        Pruefe($"Karte {karte}, Herde zieht", () => HerdeZieht(karte, verstoesse));
        Pruefe($"Karte {karte}, Tierschritt", () => TierSchritt(karte, verstoesse));
        if (karte == 1)
            Pruefe("Gangbild", () => Gangbild(verstoesse));
        Pruefe($"Karte {karte}, Schlachten", () => Schlachten(karte, verstoesse));
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
    if (gruppen.Contains("werkzeug") && karte == 1)
        Pruefe("Werkzeug", () => Werkzeug(verstoesse));
    if (gruppen.Contains("feld") && karte == 1)
        Pruefe("Feld", () => Feld(verstoesse));
    if (gruppen.Contains("animation") && karte == 1)
        Pruefe("Animation", () => Animation(verstoesse));
    if (gruppen.Contains("gehen") && karte == 1)
        Pruefe("Gehen", () => Gehen(verstoesse));
    // Das Hauptmenü braucht keine Karte
    if (gruppen.Contains("menue") && karte == 1)
        Pruefe("Hauptmenü", () => Hauptmenue(verstoesse));
    if (gruppen.Contains("karten") && karte == 1)
        Pruefe("Kartengrößen", () => Kartengroessen(verstoesse));
    if (gruppen.Contains("pfad"))
        Pruefe($"Karte {karte}, Trampelpfad", () => Pfad(karte, verstoesse));
    // Der Wind hängt nicht von der Karte ab
    if (gruppen.Contains("wind") && karte == 1)
        Pruefe("Wind", () => WindProbe(verstoesse));
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

// Wie SchafReservierung, aber für ein Reh (FoodSource.Deer): während ein
// Dorfbewohner jagt, bleibt das Reh reserviert und am Platz.
static void RehReservierung(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Reh-Reservierung";
    int rehX = -1, rehY = -1;
    for (int tx = 0; tx < w.Map.Width && rehX < 0; tx++)
        for (int ty = 0; ty < w.Map.Height && rehX < 0; ty++)
            if (w.Map.GetTile(tx, ty)?.Food == FoodSource.Deer)
                { rehX = tx; rehY = ty; }
    if (rehX < 0)
    {
        verstoesse.Add($"{wer}: kein Reh auf der Karte gefunden");
        return;
    }
    var v = w.Dorfbewohner().First();
    int vor = w.Map.GetTile(rehX, rehY)!.ResourceAmount;
    var job = new AoE.Core.Economy.GatherJob(0, Resource.Food,
        new AoE.Core.Entities.Position(rehX, rehY));
    v.State = AoE.Core.Entities.UnitState.Gathering;
    v.Job = job;
    w.LaufeBis(() => false, 2f);
    bool reserviert = w.Map.IsClaimed(rehX, rehY);
    bool rehAmPlatz = w.Map.GetTile(rehX, rehY)?.Food == FoodSource.Deer;
    if (!reserviert)
    {
        verstoesse.Add($"{wer}: Reh nicht reserviert, während der Dörfler es jagt (Phase={v.Job?.Phase})");
        return;
    }
    if (!rehAmPlatz)
    {
        verstoesse.Add($"{wer}: reserviertes Reh ist zwischenzeitlich gewandert");
        return;
    }
    Console.WriteLine($"  ok  {wer}: während der Jagd reserviert (vor={vor}, Reh am Platz)");
}

// Wie SchafWanderung, aber für die Rehe.
static void RehWanderung(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Reh-Wanderung";
    var rehe = new HashSet<(int x, int y)>();
    for (int x = 0; x < w.Map.Width; x++)
        for (int y = 0; y < w.Map.Height; y++)
            if (w.Map.GetTile(x, y)?.Food == FoodSource.Deer)
                rehe.Add((x, y));
    if (rehe.Count == 0)
    {
        verstoesse.Add($"{wer}: keine Rehe auf der Karte");
        return;
    }
    int vor = rehe.Count;
    w.LaufeBis(() => false, 60f);
    var nach = new HashSet<(int x, int y)>();
    for (int x = 0; x < w.Map.Width; x++)
        for (int y = 0; y < w.Map.Height; y++)
            if (w.Map.GetTile(x, y)?.Food == FoodSource.Deer)
                nach.Add((x, y));
    if (rehe.SetEquals(nach))
        verstoesse.Add($"{wer}: nach 60 s kein Reh gewandert (alle {vor} an ihrem Startplatz)");
    else
        Console.WriteLine($"  ok  {wer}: {rehe.Count} → {nach.Count} Rehe auf der Karte, mindestens eines gewandert");
}

// Herde (C7t): jedes freie Tier wandert in seinem eigenen Takt - in 60 s zieht
// mindestens die Hälfte der Schafe und der Rehe weiter, nicht immer dasselbe.
// Dabei bleibt jedes Tier dasselbe Objekt mit demselben Aussehen, und keines
// geht verloren oder verdoppelt sich.
static void HerdeZieht(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Herde zieht";
    int vorher = verstoesse.Count;
    Dictionary<WildAnimal, (int x, int y, int look)> Bestand(FoodSource art)
    {
        var bestand = new Dictionary<WildAnimal, (int x, int y, int look)>();
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
            {
                var t = w.Map.GetTile(x, y)!;
                if (t.Food != art) continue;
                if (t.Animal == null)
                    verstoesse.Add($"{wer}: {art} auf ({x}, {y}) hat kein WildAnimal");
                else if (!bestand.TryAdd(t.Animal, (x, y, t.Animal.Look)))
                    verstoesse.Add($"{wer}: dasselbe Tier steht auf zwei Kacheln");
            }
        return bestand;
    }
    var schafe = Bestand(FoodSource.Sheep);
    var rehe = Bestand(FoodSource.Deer);
    var kaninchen = Bestand(FoodSource.Rabbit);
    var wildschweine = Bestand(FoodSource.Boar);
    w.LaufeBis(() => false, 60f);
    foreach (var (art, quelle, vor) in new[] { ("Schafe", FoodSource.Sheep, schafe), ("Rehe", FoodSource.Deer, rehe),
                                               ("Kaninchen", FoodSource.Rabbit, kaninchen), ("Wildschweine", FoodSource.Boar, wildschweine) })
    {
        if (vor.Count == 0)
        {
            verstoesse.Add($"{wer}: keine {art} auf der Karte");
            continue;
        }
        var nach = Bestand(quelle);
        if (nach.Count != vor.Count || nach.Keys.Any(k => !vor.ContainsKey(k)))
            verstoesse.Add($"{wer}: {art}: {vor.Count} vorher, {nach.Count} nachher - Tiere verloren oder neu entstanden");
        int gezogen = vor.Count(kv => nach.TryGetValue(kv.Key, out var n) && (n.x, n.y) != (kv.Value.x, kv.Value.y));
        if (gezogen * 2 < vor.Count)
            verstoesse.Add($"{wer}: {art}: nur {gezogen} von {vor.Count} in 60 s weitergezogen - nicht jedes Tier hat seinen Takt");
        if (vor.Any(kv => nach.TryGetValue(kv.Key, out var n) && n.look != kv.Value.look))
            verstoesse.Add($"{wer}: {art}: ein Tier hat beim Wandern sein Aussehen gewechselt");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {gezogen} von {vor.Count} {art} weitergezogen, jedes bleibt dasselbe Tier");
    }
}

// Tierschritt (C7t): ein Schritt dauert TileMap.WILD_STEP_SECONDS. Direkt danach
// zeigt das Tier zurück auf die Nachbarkachel, von der es kam, und blickt in
// Schrittrichtung; nach der halben Schrittdauer ist es halb angekommen, und
// solange es läuft, beginnt es keinen neuen Schritt.
static void TierSchritt(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Tierschritt";
    int vorher = verstoesse.Count;
    const float schritt = TileMap.WILD_STEP_SECONDS;
    (Tile Kachel, WildAnimal Tier)? Frisch()
    {
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
            {
                var t = w.Map.GetTile(x, y)!;
                if (t.Animal != null && t.Food.IsWild() && t.Animal.Glide > schritt - 0.02f)
                    return (t, t.Animal);
            }
        return null;
    }
    w.LaufeBis(() => Frisch() != null, 10f);
    var frisch = Frisch();
    if (frisch == null)
    {
        verstoesse.Add($"{wer}: in 10 s hat kein Tier einen Schritt begonnen");
        return;
    }
    var (kachel, tier) = frisch.Value;
    int vonX = tier.FromX, vonY = tier.FromY;
    int radius = Math.Max(MapSettings.Default.SheepWanderRadius, MapSettings.Default.DeerWanderRadius);
    if ((vonX, vonY) == (0, 0) || Math.Abs(vonX) > radius || Math.Abs(vonY) > radius)
        verstoesse.Add($"{wer}: das Tier auf ({kachel.X}, {kachel.Y}) kam angeblich von ({vonX}, {vonY}) - kein Nachbar im Wanderradius");
    var alt = w.Map.GetTile(kachel.X + vonX, kachel.Y + vonY);
    if (alt == null || alt.Animal == tier)
        verstoesse.Add($"{wer}: die alte Kachel ({kachel.X + vonX}, {kachel.Y + vonY}) fehlt oder trägt das Tier noch");
    if (vonX != 0 && tier.FacingLeft != (vonX > 0))
        verstoesse.Add($"{wer}: Schritt um ({-vonX}, {-vonY}), aber das Tier blickt nach {(tier.FacingLeft ? "links" : "rechts")}");

    w.LaufeBis(() => false, schritt / 2f);
    if (kachel.Animal != tier)
        verstoesse.Add($"{wer}: das Tier hat mitten im Schritt einen neuen begonnen");
    else if (Math.Abs(tier.Glide - schritt / 2f) > 0.05f)
        verstoesse.Add($"{wer}: nach der halben Schrittdauer läuft der Schritt noch {tier.Glide:0.00} s statt {schritt / 2f:0.00} s");
    w.LaufeBis(() => false, schritt / 2f + 0.05f);
    if (kachel.Animal == tier && tier.Glide > 0f)
        verstoesse.Add($"{wer}: nach {schritt:0.0} s ist der Schritt nicht zu Ende (noch {tier.Glide:0.00} s)");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Schritt um ({-vonX}, {-vonY}) in {schritt:0.0} s, das Tier blickt in Laufrichtung");
}

// Gehen (C7v): ein gehender Dorfbewohner wechselt die Laufbilder in der Folge
// Schritt, Stand, Gegenschritt, Stand; gespreizt, wenn er beim Wippen unten ist,
// im Stand, wenn er oben ist.
static void Gehen(List<string> verstoesse)
{
    const string wer = "Gehen";
    int vorher = verstoesse.Count;
    var w = new Welt();
    float takt = (float)typeof(RTSGameplayScreen).GetField("VILLAGER_STEP_RATE", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    int Phase(float t) => (int)w.Call("VillagerWalkPhase", t);
    var folge = new List<int>();
    float umlauf = MathHelper.TwoPi / takt;   // ein Doppelschritt: Schritt, Stand, Gegenschritt, Stand
    for (float t = 0.001f; t < 2f * umlauf; t += umlauf / 200f)
    {
        int p = Phase(t);
        if (p < 0 || p > 3)
        {
            verstoesse.Add($"{wer}: Laufbild {p} außerhalb 0 bis 3");
            break;
        }
        if (folge.Count == 0 || folge[^1] != p) folge.Add(p);
        float wippen = MathF.Abs(MathF.Sin(t * takt));
        bool gespreizt = p is 0 or 2;
        if (gespreizt && wippen > 0.75f || !gespreizt && wippen < 0.65f)
        {
            verstoesse.Add($"{wer}: bei t={t:0.000} Laufbild {p}, die Figur wippt aber auf {wippen:0.00} - Beine und Wippen passen nicht zusammen");
            break;
        }
    }
    var erwartet = new List<int> { 0, 1, 2, 3, 0, 1, 2, 3, 0 };
    if (!folge.SequenceEqual(erwartet.Take(folge.Count)) || folge.Count < 8)
        verstoesse.Add($"{wer}: Laufbilder {string.Join(",", folge)} statt 0,1,2,3,0,1,2,3");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Schritt, Stand, Gegenschritt, Stand im Takt des Wippens ({takt:0} rad/s)");
}

// Fahne und Windrad (C6a): das Fahnentuch steht am Mast fest und schlägt zum freien
// Ende hin aus, höchstens eine Ausschlagbreite, und es bewegt sich mit der Zeit;
// das Windrad dreht sich gleichmäßig mit MILL_SAIL_SPEED.
static void FahneUndWindrad(List<string> verstoesse)
{
    const string wer = "Fahne und Windrad";
    int vorher = verstoesse.Count;
    var w = new Welt();
    float Welle(float frei, float zeit) => (float)w.Call("FlagWave", frei, zeit);
    float Winkel(float zeit) => (float)w.Call("MillSailAngle", zeit);
    float amMast = 0f, amEnde = 0f, mitte = 0f;
    for (float t = 0f; t < 3f; t += 0.02f)
    {
        amMast = Math.Max(amMast, Math.Abs(Welle(0f, t)));
        amEnde = Math.Max(amEnde, Math.Abs(Welle(1f, t)));
        mitte = Math.Max(mitte, Math.Abs(Welle(0.5f, t)));
    }
    if (amMast > 0.001f)
        verstoesse.Add($"{wer}: das Tuch bewegt sich am Mast ({amMast:0.000})");
    if (amEnde < 0.95f || amEnde > 1.001f)
        verstoesse.Add($"{wer}: das freie Ende schlägt bis {amEnde:0.00} aus statt bis 1");
    if (!(mitte > amMast && mitte < amEnde))
        verstoesse.Add($"{wer}: zur Tuchmitte hin wächst der Ausschlag nicht ({amMast:0.00} < {mitte:0.00} < {amEnde:0.00})");
    if (Math.Abs(Welle(1f, 0f) - Welle(1f, 0.1f)) < 0.05f)
        verstoesse.Add($"{wer}: die Fahne steht still ({Welle(1f, 0f):0.00} -> {Welle(1f, 0.1f):0.00} in 0,1 s)");
    float speed = (float)typeof(RTSGameplayScreen).GetField("MILL_SAIL_SPEED", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    float a0 = Winkel(1f), a1 = Winkel(2f);
    float schritt = (a1 - a0 + MathHelper.TwoPi) % MathHelper.TwoPi;
    if (speed <= 0f || Math.Abs(schritt - speed % MathHelper.TwoPi) > 0.001f)
        verstoesse.Add($"{wer}: das Windrad dreht sich in 1 s um {schritt:0.000} statt {speed:0.000}");
    for (float t = 0f; t < 60f; t += 0.7f)
        if (Winkel(t) < 0f || Winkel(t) >= MathHelper.TwoPi)
        {
            verstoesse.Add($"{wer}: Drehwinkel {Winkel(t):0.00} außerhalb 0 bis 2π");
            break;
        }
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Tuch am Mast fest, am Ende bis {amEnde:0.00} Ausschlag, Windrad {speed:0.0} rad/s");
}

// Schlachten (C7s): sobald ein Dorfbewohner an einem Schaf bzw. Reh sammelt, ist
// es geschlachtet - es bleibt liegen, auch wenn er abgezogen wird, und wandert
// nie mehr weiter. Ein Tier, an dem noch niemand sammelt, ist nicht geschlachtet.
static void Schlachten(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Schlachten";
    int vorher = verstoesse.Count;
    foreach (var (art, name) in new[] { (FoodSource.Sheep, "Schaf"), (FoodSource.Deer, "Reh"),
                                        (FoodSource.Rabbit, "Kaninchen"), (FoodSource.Boar, "Wildschwein") })
    {
        var w = new Welt();
        Tile kachel = null;
        for (int x = 0; x < w.Map.Width && kachel == null; x++)
            for (int y = 0; y < w.Map.Height && kachel == null; y++)
                if (w.Map.GetTile(x, y) is { Animal: not null } t && t.Food == art)
                    kachel = t;
        if (kachel == null)
        {
            Console.WriteLine($"  --  {wer}: kein {name} auf dieser Karte, übersprungen");
            continue;
        }
        var tier = kachel.Animal;
        if (tier.Slaughtered)
            verstoesse.Add($"{wer}: ein {name}, an dem niemand sammelt, ist schon geschlachtet");
        var v = w.Dorfbewohner().First();
        v.Position = w.Map.GridToWorld(new Vector2(kachel.X, kachel.Y));
        v.Job = new GatherJob(0, Resource.Food, new CorePosition(kachel.X, kachel.Y));
        w.Call("FollowJob", v);
        w.LaufeBis(() => v.Job?.Phase == AoE.Core.Economy.GatherPhase.Gathering, 20f);
        w.LaufeBis(() => false, 0.5f);
        if (v.Job?.Phase != AoE.Core.Economy.GatherPhase.Gathering && !tier.Slaughtered)
        {
            verstoesse.Add($"{wer}: Voraussetzung fehlt - der Dorfbewohner sammelt nicht am {name} (Phase {v.Job?.Phase})");
            continue;
        }
        if (!tier.Slaughtered)
        {
            verstoesse.Add($"{wer}: der Dorfbewohner sammelt am {name}, es ist aber nicht geschlachtet");
            continue;
        }
        // Dorfbewohner abziehen: das Fleisch bleibt liegen
        v.Job = null;
        v.State = UnitState.Idle;
        w.LaufeBis(() => false, 30f);
        if (kachel.Animal != tier || kachel.Food != art)
            verstoesse.Add($"{wer}: das geschlachtete {name} ist weitergewandert, nachdem der Dorfbewohner abgezogen wurde");
        else if (!tier.Slaughtered || tier.Glide > 0f)
            verstoesse.Add($"{wer}: das {name} läuft nach dem Abziehen wieder (geschlachtet={tier.Slaughtered}, Schritt {tier.Glide:0.00} s)");
    }
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: geschlachtet, sobald gesammelt wird; das Fleisch bleibt liegen, auch ohne Dorfbewohner");
}

// Gangbild (C7w): solange ein Tier unterwegs ist, wechseln die Laufbilder in der
// Folge Schritt, Stand, Gegenschritt, Stand - WALK_CYCLES_PER_STEP Durchgänge je
// Kachelschritt -, ein stehendes Tier zeigt sein Standbild.
static void Gangbild(List<string> verstoesse)
{
    const string wer = "Gangbild";
    int vorher = verstoesse.Count;
    var w = new Welt();
    int Phase(float glide) => (int)w.Call("WalkPhase", glide);
    if (Phase(0f) != -1)
        verstoesse.Add($"{wer}: ein stehendes Tier zeigt das Laufbild {Phase(0f)} statt seines Standbilds");
    var folge = new List<int>();
    for (float g = TileMap.WILD_STEP_SECONDS; g > 0f; g -= 1f / 60f)
    {
        int p = Phase(g);
        if (folge.Count == 0 || folge[^1] != p) folge.Add(p);
    }
    int zyklen = (int)typeof(RTSGameplayScreen).GetField("WALK_CYCLES_PER_STEP", BindingFlags.NonPublic | BindingFlags.Static)!.GetValue(null)!;
    var soll = Enumerable.Range(0, 4 * zyklen).Select(i => i % 4).ToList();
    if (zyklen < 1 || !folge.SequenceEqual(soll))
        verstoesse.Add($"{wer}: Laufbilder im Schritt {string.Join(",", folge)} statt {string.Join(",", soll)}");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: {zyklen} Durchgänge Schritt, Stand, Gegenschritt, Stand je Kachelschritt, im Stand das Standbild");
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

// Wind (G9): Böen ziehen von links über das Land, dieselben wie über den Weizen
// (WheatWind in Weizen.fx), und die Bäume wiegen sich darin - jeder für sich, im
// Mittel mit dem Wind geneigt. SwayStrips schneidet ein Baumbild in Streifen, die
// lückenlos aneinanderschließen: der Fuß steht, die Spitze schwingt aus.
static void WindProbe(List<string> verstoesse)
{
    const string wer = "Wind";
    int vorher = verstoesse.Count;

    // Gust rechnet genau wie gust in WheatWind
    static float Welle(Vector2 p, float t, Vector2 d, float lambda)
        => MathF.Pow(MathF.Max(MathF.Sin((Vector2.Dot(p, Vector2.Normalize(d)) - 1.2f * t) * MathF.Tau / lambda), 0f), 3f);
    float groessteAbweichung = 0f;
    for (float x = -3f; x <= 30f; x += 1.7f)
        for (float y = -2f; y <= 20f; y += 2.3f)
            for (float t = 0f; t <= 40f; t += 3.1f)
            {
                var p = new Vector2(x, y);
                float soll = MathF.Max(Welle(p, t, new Vector2(1f, 0.2f), 4f), Welle(p, t, new Vector2(1f, 0.5f), 6f))
                             * (0.7f + 0.3f * MathF.Sin(0.4f * t));
                float ist = Wind.Gust(p, t);
                if (ist < 0f || ist > 1f)
                {
                    verstoesse.Add($"{wer}: Gust({x:0.0}, {y:0.0}, t={t:0.0}) = {ist:0.000} liegt nicht zwischen 0 und 1");
                    return;
                }
                groessteAbweichung = MathF.Max(groessteAbweichung, MathF.Abs(ist - soll));
            }
    if (groessteAbweichung > 0.001f)
        verstoesse.Add($"{wer}: Gust weicht bis {groessteAbweichung:0.0000} von der Rechnung in WheatWind ab - Weizen und Bäume liefen nicht in denselben Böen");

    // TreeSway: in den Grenzen, stetig, im Mittel mit dem Wind, jeder Baum anders
    var baum = new Vector2(12.3f, 7.8f);
    float summe = 0f, kleinste = float.MaxValue, groesste = float.MinValue;
    int bilder = 0;
    for (float t = 0f; t < 120f; t += 1f / 60f, bilder++)
    {
        float s = Wind.TreeSway(baum, t, 4711);
        if (MathF.Abs(s) > 0.05f)
        {
            verstoesse.Add($"{wer}: TreeSway {s:0.000} bei t={t:0.00} - höchstens 0,05 erlaubt");
            break;
        }
        float naechstes = Wind.TreeSway(baum, t + 1f / 60f, 4711);
        if (MathF.Abs(naechstes - s) > 0.004f)
        {
            verstoesse.Add($"{wer}: TreeSway springt bei t={t:0.00} von {s:0.000} auf {naechstes:0.000} in einem Bild");
            break;
        }
        summe += s;
        kleinste = MathF.Min(kleinste, s);
        groesste = MathF.Max(groesste, s);
    }
    if (summe / bilder <= 0.002f)
        verstoesse.Add($"{wer}: im Mittel neigt sich der Baum nicht mit dem Wind nach rechts ({summe / bilder:0.0000})");
    if (groesste - kleinste < 0.015f)
        verstoesse.Add($"{wer}: der Baum schwingt kaum ({kleinste:0.000} bis {groesste:0.000})");
    if (Wind.TreeSway(baum, 33.3f, 4711) != Wind.TreeSway(baum, 33.3f, 4711))
        verstoesse.Add($"{wer}: TreeSway liefert bei gleichen Eingaben verschiedene Werte");
    int verschieden = 0;
    for (float t = 0f; t < 10f; t += 0.5f)
        if (MathF.Abs(Wind.TreeSway(baum, t, 4711) - Wind.TreeSway(baum, t, 90210)) > 0.002f)
            verschieden++;
    if (verschieden < 5)
        verstoesse.Add($"{wer}: zwei Bäume an derselben Stelle schwingen gleich - look ändert nichts");

    // SwayStrips: lückenlos, Fuß fest, Spitze ausgelenkt
    var ziel = new Rectangle(100, 200, 60, 120);
    var streifen = Wind.SwayStrips(ziel, 64, 128, 0.04f).ToList();
    if (streifen.Count != Wind.TREE_STRIPS)
        verstoesse.Add($"{wer}: {streifen.Count} Streifen statt {Wind.TREE_STRIPS}");
    else
    {
        int oben = 0;
        float hoch = ziel.Height / 128f;
        foreach (var (quelle, lage, mass) in streifen)
        {
            if (quelle.X != 0 || quelle.Width != 64 || quelle.Y != oben)
            {
                verstoesse.Add($"{wer}: Streifen {quelle} schließt nicht an (erwartet X 0, Breite 64, Y {oben})");
                break;
            }
            if (MathF.Abs(lage.Y - (ziel.Y + oben * hoch)) > 0.01f)
            {
                verstoesse.Add($"{wer}: Streifen ab Bildzeile {oben} liegt bei y {lage.Y:0.00} statt {ziel.Y + oben * hoch:0.00}");
                break;
            }
            if (MathF.Abs(mass.X - 60f / 64f) > 0.001f || MathF.Abs(mass.Y - hoch) > 0.001f)
            {
                verstoesse.Add($"{wer}: Streifen ab Bildzeile {oben} mit Maßstab {mass} statt ({60f / 64f:0.000}, {hoch:0.000})");
                break;
            }
            float mitte = 1f - (oben + quelle.Height / 2f) / 128f;   // Höhe über dem Fuß
            float soll = ziel.X + 0.04f * ziel.Height * MathF.Pow(mitte, 1.5f);
            if (MathF.Abs(lage.X - soll) > 0.05f)
            {
                verstoesse.Add($"{wer}: Streifen ab Bildzeile {oben} bei x {lage.X:0.00} statt {soll:0.00} - die Biegung folgt nicht h^1,5");
                break;
            }
            oben += quelle.Height;
        }
        if (oben != 128)
            verstoesse.Add($"{wer}: die Streifen decken {oben} von 128 Bildzeilen");
    }
    if (Wind.SwayStrips(ziel, 64, 128, 0f).Any(s => MathF.Abs(s.Position.X - ziel.X) > 0.001f))
        verstoesse.Add($"{wer}: ohne Wind sind die Streifen verschoben");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Böen wie über dem Weizen, Bäume neigen sich im Mittel mit dem Wind ({kleinste:0.000} bis {groesste:0.000}), Streifen lückenlos");
}

// Trampelpfade (G5): wer eine Kachel betritt, tritt sie aus (TileMap.Trample). Ein
// Dorfbewohner läuft zwölfmal zwischen zwei Kacheln hin und her; der Ablauf zählt
// selbst, wie oft er welche Kachel betritt. Jede Kachel ist danach so ausgetreten,
// wie es ihren Schritten entspricht, abzüglich dessen, was in der Zeit nachwuchs -
// und keine, die er nicht betreten hat. Ohne Verkehr wächst das Gras gleichmäßig
// nach, in WEAR_REGROW_SECONDS ganz.
static void Pfad(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Trampelpfad";
    int vorher = verstoesse.Count;
    var v = w.Dorfbewohner().First();
    var start = w.Map.WorldToGrid(v.Position);
    var ziel = w.FreieKachel(v, 10);
    w.Waehle(new List<Unit> { v });
    var schritte = new Dictionary<(int, int), int>();
    var zuletzt = ((int)start.X, (int)start.Y);
    float dauer = 0f;
    for (int lauf = 0; lauf < 12; lauf++)
    {
        w.Linksklick(w.Map.GridToWorld(lauf % 2 == 0 ? ziel : start));
        dauer += w.LaufeBis(() =>
        {
            var c = w.Map.WorldToGrid(v.Position);
            var kachel = ((int)c.X, (int)c.Y);
            if (kachel != zuletzt)
            {
                schritte[kachel] = schritte.GetValueOrDefault(kachel) + 1;
                zuletzt = kachel;
            }
            return v.State == UnitState.Idle;
        }, 60);
    }

    var abgenutzt = new List<Tile>();
    for (int x = 0; x < w.Map.Width; x++)
        for (int y = 0; y < w.Map.Height; y++)
            if (w.Map.GetTile(x, y).Wear > 0f)
                abgenutzt.Add(w.Map.GetTile(x, y));
    float nachgewachsen = dauer / TileMap.WEAR_REGROW_SECONDS;
    foreach (var (kachel, n) in schritte)
    {
        var t = w.Map.GetTile(kachel.Item1, kachel.Item2);
        float soll = MathF.Min(1f, n * TileMap.WEAR_PER_STEP);
        if (t.Wear > soll + 0.001f || t.Wear < soll - nachgewachsen - 0.001f)
        {
            verstoesse.Add($"{wer}: ({t.X},{t.Y}) {n}-mal betreten, Abnutzung {t.Wear:0.000} statt {soll - nachgewachsen:0.000} bis {soll:0.000}");
            break;
        }
    }
    foreach (var t in abgenutzt.Where(t => !schritte.ContainsKey((t.X, t.Y))).Take(3))
        verstoesse.Add($"{wer}: Kachel ({t.X},{t.Y}) ist ausgetreten ({t.Wear:0.00}), obwohl niemand sie betreten hat");
    int oft = schritte.Count(s => s.Value >= 10);
    if (oft == 0)
        verstoesse.Add($"{wer}: keine Kachel zehnmal betreten - der Dorfbewohner lief nicht hin und her");
    int pfad = abgenutzt.Count(t => t.Wear >= 0.4f);

    var stand = abgenutzt.ToDictionary(t => t, t => t.Wear);
    w.Map.RegrowGrass(TileMap.WEAR_REGROW_SECONDS / 2f);
    foreach (var (t, wear) in stand)
    {
        if (MathF.Abs(t.Wear - MathF.Max(0f, wear - 0.5f)) > 0.001f)
        {
            verstoesse.Add($"{wer}: nach halber Nachwachszeit {t.Wear:0.000} statt {MathF.Max(0f, wear - 0.5f):0.000} auf ({t.X},{t.Y})");
            break;
        }
    }
    w.Map.RegrowGrass(TileMap.WEAR_REGROW_SECONDS);
    if (abgenutzt.Any(t => t.Wear > 0f))
        verstoesse.Add($"{wer}: nach voller Nachwachszeit noch Abnutzung übrig");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: {abgenutzt.Count} Kacheln ausgetreten, {pfad} davon als Pfad, nur auf dem Weg; wächst wieder zu");
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
        // Unten bündig in der Leiste, nach oben über sie hinaus (C11: 80 % größer als die
        // frühere 192-px-Minimap, die in die Leiste passen musste); das Feld dahinter
        // zählt für Klicks zur Leiste, links daneben gilt ein Klick weiter der Karte
        if (r.Bottom > bh || r.Bottom < bh - 12 || r.Top < 40)
            verstoesse.Add($"{wer}: Feld {r} sitzt nicht unten bündig in der Leiste (ab y = {bh - leiste})");
        if (r.Width != r.Height || r.Width < 192 * 1.7f)
            verstoesse.Add($"{wer}: Minimap {r.Width}x{r.Height} px - erwartet quadratisch und gut 80 % größer als die frühere (192 px)");
        var feld = (Rectangle)w.Call("MinimapPanel");
        if (!feld.Contains(r) || feld.Right != bw || feld.Bottom < bh)
            verstoesse.Add($"{wer}: das Feld {feld} hinter der Minimap {r} reicht nicht bis zum Fensterrand");
        if (!(bool)w.Call("IsOverHud", new Point(r.Center.X, r.Top + 2)))
            verstoesse.Add($"{wer}: ein Klick auf den oberen Teil der Minimap gilt der Karte statt der Leiste");
        if ((bool)w.Call("IsOverHud", new Point(feld.Left - 20, r.Top + 2)))
            verstoesse.Add($"{wer}: links neben der Minimap gilt ein Klick nicht mehr der Karte");

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
// ein anderes Gebäude (Stadtzentrum 5, Haus und Lager 3) sehen kann - halb gebaut
// noch nicht: eine Baustelle deckt keinen Nebel auf (C7b).
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
    // Prüfkachel 7 bis 9 Kacheln von der Turmmitte, außer Sicht von allem anderen
    var mitte = new Vector2(turm.Core.Position.X, turm.Core.Position.Y);
    Vector2? Pruefkachel()
    {
        var andere = w.Map.Buildings.Where(b => b != turm && b.OwnerId == 0)
            .Select(b => (Ort: new Vector2(b.Core.Position.X, b.Core.Position.Y), Weit: b.Core.Stats.VisionRange + 1.5f)).ToList();
        var einheiten = w.Dorfbewohner().Select(u => w.Map.WorldToGrid(u.Position)).ToList();
        return Enumerable.Range(0, w.Map.Width).SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)))
            .Where(c => Vector2.Distance(c, mitte) is >= 7f and <= 9f
                        && einheiten.All(e => Vector2.Distance(c, e) > 5f)
                        && andere.All(b => Vector2.Distance(c, b.Ort) > b.Weit))
            .Cast<Vector2?>().FirstOrDefault();
    }

    // Halb gebaut sieht der Turm noch nichts
    w.LaufeBis(() => turm.Construction.Progress >= 0.5f, 90f);
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    var halb = Pruefkachel();
    if (halb != null && w.Map.IsTileVisible((int)halb.Value.X, (int)halb.Value.Y, 0))
        verstoesse.Add($"{wer}: Baustelle bei {turm.Construction.Progress * 100:0} % deckt schon Kachel {halb.Value} "
                       + $"({Vector2.Distance(halb.Value, mitte):0.0} vom Turm) auf - Sicht erst fertig gebaut");

    float bauzeit = w.LaufeBis(() => turm.IsComplete, 90f);
    if (!turm.IsComplete)
    {
        verstoesse.Add($"{wer}: nach 90 s nicht fertig ({turm.Construction.Progress * 100:0} %)");
        return;
    }

    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    var probe = Pruefkachel();
    if (probe == null)
        Console.WriteLine($"      {wer}: keine Prüfkachel außer Sicht der übrigen - Sichttest übersprungen");
    else if (!w.Map.IsTileVisible((int)probe.Value.X, (int)probe.Value.Y, 0))
        verstoesse.Add($"{wer}: Kachel {probe.Value} ({Vector2.Distance(probe.Value, mitte):0.0} vom Turm) nicht in Sicht");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: erst ab der Feudalzeit, 50 Holz + 125 Stein, halb gebaut ohne Sicht"
                          + $"{(halb == null ? " (ungeprüft)" : "")}, fertig nach {bauzeit:0} s, sieht "
                          + $"{(probe == null ? "-" : Vector2.Distance(probe.Value, mitte).ToString("0.0"))} Kacheln weit");
}

// Kartengrößen (C11): Standard 64, Groß 90, Maximal 128 Kacheln Seitenlänge; beide
// Stadtzentren stehen in ihren Ecken, und die Minimap ist bei jeder Größe gleich
// groß und zeigt die ganze Karte.
static void Kartengroessen(List<string> verstoesse)
{
    const string wer = "Kartengrößen";
    int vorher = verstoesse.Count;
    var erwartet = new Dictionary<MapSize, int> { [MapSize.Standard] = 64, [MapSize.Large] = 90, [MapSize.Max] = 128 };
    int feld = -1;
    foreach (var (groesse, seite) in erwartet)
    {
        if (MapSizes.Side(groesse) != seite)
            verstoesse.Add($"{wer}: {groesse} hat {MapSizes.Side(groesse)} Kacheln Seitenlänge statt {seite}");
        var w = new Welt(groesse);
        if (w.Map.Width != seite || w.Map.Height != seite)
            verstoesse.Add($"{wer}: {groesse} ist {w.Map.Width}x{w.Map.Height} statt {seite}x{seite}");
        var zentren = w.Map.Buildings.Where(b => b.Type == "Stadtzentrum").Select(b => (b.X, b.Y)).OrderBy(p => p).ToList();
        var soll = new List<(int, int)> { (3, 3), (seite - 4, seite - 4) };
        if (!zentren.SequenceEqual(soll))
            verstoesse.Add($"{wer}: {groesse}: Stadtzentren bei {string.Join(", ", zentren)} statt (3, 3) und ({seite - 4}, {seite - 4})");
        w.Set("screenBounds", new Rectangle(0, 0, 2406, 1353));
        var r = (Rectangle)w.Call("MinimapRect");
        if (feld < 0) feld = r.Width;
        if (r.Width != r.Height || r.Width != feld)
            verstoesse.Add($"{wer}: {groesse}: Minimap {r.Width}x{r.Height} statt {feld}x{feld} wie bei der Standardkarte");
        var ecke = (Vector2)w.Call("MinimapPoint", (float)seite, (float)seite, r);
        if (Math.Abs(ecke.X - r.Right) > 0.5f || Math.Abs(ecke.Y - r.Bottom) > 0.5f)
            verstoesse.Add($"{wer}: {groesse}: die Kartenecke liegt in der Minimap bei {ecke} statt bei ({r.Right}, {r.Bottom})");
    }
    if (Enum.GetValues<MapSize>().Select(MapSizes.Name).Distinct().Count() != 3)
        verstoesse.Add($"{wer}: die drei Größen haben keine verschiedenen Namen");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: 64, 90 und 128 Kacheln, Stadtzentren in den Ecken, Minimap immer {feld} px");
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

        var platten = Enumerable.Range(0, 5).Select(Platte).ToList();
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
        // Einträge: 0 Neues Spiel, 1 Karte, 2 Spiel laden (gesperrt), 3 Einstellungen, 4 Beenden
        auswahl.SetValue(menue, 0);
        if (Maus2(Maus(platten[3].Center, false), Maus(abseits, false)) != (3, false))
            verstoesse.Add($"{fenster}: Zeigen auf „Einstellungen“ wählt sie nicht aus");
        if (Maus2(Maus(platten[3].Center, false), Maus(platten[3].Center, false)) != (0, false))
            verstoesse.Add($"{fenster}: ruhende Maus überstimmt die Tastaturauswahl");
        if (Maus2(Maus(platten[4].Center, false), Maus(platten[4].Center, true)) != (4, true))
            verstoesse.Add($"{fenster}: Klick auf „Beenden“ startet ihn nicht");
        if (Maus2(Maus(platten[0].Center, false), Maus(platten[0].Center, true)) != (0, true))
            verstoesse.Add($"{fenster}: Klick auf „Neues Spiel“ startet es nicht");
        if (Maus2(Maus(platten[1].Center, false), Maus(platten[1].Center, true)) != (1, true))
            verstoesse.Add($"{fenster}: Klick auf „Karte“ wechselt die Größe nicht");
        if (Maus2(Maus(platten[2].Center, false), Maus(platten[2].Center, true)) != (0, false))
            verstoesse.Add($"{fenster}: der gesperrte Eintrag „Spiel laden“ reagiert auf die Maus");
        if (Maus2(Maus(abseits, false), Maus(abseits, true)) != (0, false))
            verstoesse.Add($"{fenster}: Klick neben das Menü startet etwas");
    }

    auswahl.SetValue(menue, 1);
    Methode("MoveSelection").Invoke(menue, new object[] { 1 });
    if ((int)auswahl.GetValue(menue) != 3)
        verstoesse.Add($"{wer}: Pfeil nach unten von „Karte“ landet auf {auswahl.GetValue(menue)}, erwartet 3 (über das gesperrte „Spiel laden“ hinweg)");
    Methode("MoveSelection").Invoke(menue, new object[] { -1 });
    if ((int)auswahl.GetValue(menue) != 1)
        verstoesse.Add($"{wer}: Pfeil nach oben von „Einstellungen“ landet auf {auswahl.GetValue(menue)}, erwartet 1");

    // Der Karteneintrag nennt die Größe und wechselt Standard, Groß, Maximal im Kreis
    var groesse = typ.GetField("mapSize", F)
        ?? throw new InvalidOperationException("Feld MainMenuScreen.mapSize nicht gefunden");
    var folge = new List<MapSize>();
    var jetzt = MapSize.Standard;
    for (int i = 0; i < 4; i++)
    {
        groesse.SetValue(menue, jetzt);
        var text = (string)Methode("EntryText").Invoke(menue, new object[] { 1 });
        if (text != $"Karte: {MapSizes.Name(jetzt)}")
            verstoesse.Add($"{wer}: der Karteneintrag heißt „{text}“ statt „Karte: {MapSizes.Name(jetzt)}“");
        folge.Add(jetzt);
        jetzt = (MapSize)Methode("NextMapSize").Invoke(null, new object[] { jetzt })!;
    }
    if (!folge.SequenceEqual(new[] { MapSize.Standard, MapSize.Large, MapSize.Max, MapSize.Standard }))
        verstoesse.Add($"{wer}: die Kartengröße wechselt {string.Join(" -> ", folge)} statt Standard -> Large -> Max -> Standard");
    if ((string)Methode("EntryText").Invoke(menue, new object[] { 0 }) != "Neues Spiel")
        verstoesse.Add($"{wer}: der erste Eintrag heißt nicht mehr „Neues Spiel“");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: mittig, wächst mit dem Fenster, Zeigen wählt, Klick startet, „Spiel laden“ gesperrt, Karte wechselt die Größe");
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

// Werkzeuge (C6t): jede Arbeit hat ihr Werkzeug, und der Schlag holt langsam aus
// und schlägt schnell zu. Entstanden am 2026-10-03: die Dorfbewohner kippten beim
// Arbeiten als Ganzes vor und zurück, statt ihr Werkzeug zu schwingen.
static void Werkzeug(List<string> verstoesse)
{
    const string wer = "Werkzeug";
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
    var w = new Welt();
    int vorher = verstoesse.Count;
    var dorf = w.Dorfbewohner().First();
    string Werkzeug() => w.Call("ToolFor", dorf).ToString()!;
    Tile Kachel(FoodSource quelle)
    {
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
                if (w.Map.GetTile(x, y) is { } k && k.Food == quelle && k.ResourceAmount > 0)
                    return k;
        return null;
    }
    void Erwarte(string arbeit, string werkzeug)
    {
        if (Werkzeug() != werkzeug)
            verstoesse.Add($"{wer}: {arbeit} mit {Werkzeug()}, erwartet {werkzeug}");
    }

    dorf.Job = null;
    dorf.State = UnitState.Idle;
    Erwarte("ohne Auftrag", "Hoe");
    dorf.State = UnitState.Building;
    Erwarte("am Bau", "Hammer");
    dorf.State = UnitState.Gathering;
    foreach (var (rohstoff, werkzeug) in new[] { (Resource.Wood, "Axe"), (Resource.Stone, "Pickaxe"), (Resource.Gold, "Pickaxe") })
    {
        dorf.Job = new GatherJob(0, rohstoff, new CorePosition(0, 0));
        Erwarte($"an {rohstoff}", werkzeug);
    }
    w.Map.PlantCrop(1, 1, 3);
    foreach (var (quelle, werkzeug) in new[] { (FoodSource.Farm, "Hoe"), (FoodSource.Berries, "Sickle"), (FoodSource.Fish, "Rod") })
    {
        if (Kachel(quelle) is not { } k)
            continue;   // nicht jede Karte hat Beeren oder Fische
        dorf.Job = new GatherJob(0, Resource.Food, new CorePosition(k.X, k.Y));
        Erwarte($"an {quelle}", werkzeug);
    }

    // Der Schlag: bei Takt 0 unten, nach 70 % ausgeholt, dann schnell wieder unten
    var typ = typeof(RTSGameplayScreen);
    var stile = (System.Collections.IDictionary)(typ.GetField("ToolStyles", F)?.GetValue(null)
        ?? throw new InvalidOperationException("Feld RTSGameplayScreen.ToolStyles nicht gefunden - umbenannt?"));
    var werkzeugTyp = typ.GetNestedType("Tool", BindingFlags.NonPublic)
        ?? throw new InvalidOperationException("Typ RTSGameplayScreen.Tool nicht gefunden - umbenannt?");
    var schwung = typ.GetMethod("SwingAngle", F)
        ?? throw new InvalidOperationException("Methode RTSGameplayScreen.SwingAngle nicht gefunden - umbenannt?");
    foreach (var name in new[] { "Axe", "Pickaxe", "Hoe", "Hammer" })
    {
        var stil = stile[Enum.Parse(werkzeugTyp, name)]!;
        float Wert(string feld) => (float)stil.GetType().GetProperty(feld)!.GetValue(stil)!;
        float rate = Wert("Rate"), oben = Wert("Raised"), unten = Wert("Strike");
        float Winkel(float phase) => (float)schwung.Invoke(null, new object[] { stil, phase / rate })!;

        if (!(oben < -MathF.PI / 2 && unten > 0f))
            verstoesse.Add($"{wer}: {name} holt nicht über die Senkrechte aus ({oben:0.00}) oder schlägt nicht nach vorn unten ({unten:0.00})");
        if (Math.Abs(Winkel(0f) - unten) > 0.01f || Math.Abs(Winkel(0.7f) - oben) > 0.01f)
            verstoesse.Add($"{wer}: {name} beginnt nicht unten ({Winkel(0f):0.00}) oder ist bei 70 % nicht ausgeholt ({Winkel(0.7f):0.00})");
        float ausholen = 0f, zuschlagen = 0f;
        for (int i = 0; i < 100; i++)
        {
            float a = i / 100f, b = (i + 1) / 100f;
            float schritt = Math.Abs(Winkel(Math.Min(b, 0.9999f)) - Winkel(a));
            if (b <= 0.7f) ausholen = Math.Max(ausholen, schritt);
            else if (a >= 0.7f) zuschlagen = Math.Max(zuschlagen, schritt);
        }
        if (zuschlagen < 2f * ausholen)
            verstoesse.Add($"{wer}: {name} schlägt nicht deutlich schneller zu ({zuschlagen:0.000}) als es ausholt ({ausholen:0.000})");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: jede Arbeit hat ihr Werkzeug, langsam ausgeholt und schnell zugeschlagen");
}

// Feld (C7f): ein Acker bildet den Grund, der Weizen steht darüber - und das
// Wachstum ist sichtbar. Die Zahlen rechnet WheatLook aus Vorrat und
// FarmRegrow: voll = gold und dichte Deckung, gerade geerntet = nackter
// Acker, dazwischen wächst der Weizen von grün nach gold heran.
static void Feld(List<string> verstoesse)
{
    const string wer = "Feld";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Map.PlantCrop(1, 1, 3);
    var kachel = w.Map.GetTile(2, 2)
        ?? throw new InvalidOperationException("Pflanzen hat keine Kachel hinterlassen");
    if (!kachel.Farm || kachel.ResourceAmount != 175)
        verstoesse.Add($"{wer}: Vorausetzung fehlt - die Kachel ist kein volles Feld (Farm={kachel.Farm}, Vorrat={kachel.ResourceAmount})");

    (float Deckung, Color Ton) Aussehen(Tile k)
    {
        object r = w.Call("WheatLook", k);
        var typ = r.GetType();
        return ((float)typ.GetField("Item1").GetValue(r), (Color)typ.GetField("Item2").GetValue(r));
    }

    // Just gepflanzt: dicht, im Ton des Feldbilds
    var (voll, tint) = Aussehen(kachel);
    if (voll < 0.99f)
        verstoesse.Add($"{wer}: voller Acker trägt keinen vollen Weizen (Deckung {voll:0.00})");
    if (tint != Color.White)
        verstoesse.Add($"{wer}: reifer Weizen zeigt das Feldbild nicht unverfälscht (R{tint.R} G{tint.G} B{tint.B})");

    // Jede Feldkachel zeigt ihren eigenen Teil des Feldbilds, zusammen das ganze Feld ohne
    // den Grasrand außerhalb des Zauns (links und oben 8, rechts und unten 3 von 384 Bildpunkten)
    var feldteile = new HashSet<Rectangle>();
    for (int fx = 1; fx <= 3; fx++)
        for (int fy = 1; fy <= 3; fy++)
        {
            var feldteil = (Rectangle)w.Call("FieldPart", w.Map.GetTile(fx, fy)!, 384, 384);
            if (feldteil != new Rectangle(8 + (fx - 1) * 124, 8 + (fy - 1) * 124, 124, 124))
                verstoesse.Add($"{wer}: Feldkachel ({fx}, {fy}) zeigt den Bildteil {feldteil} statt Spalte {fx - 1}, Zeile {fy - 1}");
            feldteile.Add(feldteil);
        }
    if (feldteile.Count != 9)
        verstoesse.Add($"{wer}: die neun Feldkacheln zeigen nur {feldteile.Count} verschiedene Bildteile");

    // Geerntet und noch nichts neu gewachsen: nackter Acker
    kachel.ResourceType = null;
    kachel.ResourceAmount = 0;
    kachel.FarmRegrow = TileMap.FARM_REGROW_SECONDS;
    var (leer, _) = Aussehen(kachel);
    if (leer > 0.01f)
        verstoesse.Add($"{wer}: eine gerade geerntete Kachel zeigt keinen nackten Acker (Deckung {leer:0.00})");

    // Halb nachgewachsen: halbe Deckung, Ton zwischen grün und gold
    kachel.FarmRegrow = TileMap.FARM_REGROW_SECONDS / 2f;
    var (halb, mittig) = Aussehen(kachel);
    if (halb < 0.25f || halb > 0.75f)
        verstoesse.Add($"{wer}: halbes Nachwachsen trägt keinen halben Weizen (Deckung {halb:0.00})");
    if (mittig.G < mittig.R)
        verstoesse.Add($"{wer}: halber Weizen ist rötlich statt grün-gold (R{mittig.R} G{mittig.G} B{mittig.B})");

    // Ganz jung: klar grün (Grün über Rot)
    kachel.FarmRegrow = TileMap.FARM_REGROW_SECONDS * 0.8f;
    var (_, jungton) = Aussehen(kachel);
    if (jungton.G <= jungton.R)
        verstoesse.Add($"{wer}: ganz junger Weizen ist nicht grün (R{jungton.R} G{jungton.G} B{jungton.B})");

    // Countdown abgelaufen: wieder voll, und RegrowCrop füllt auf
    kachel.FarmRegrow = 0f;
    (float wieder, _) = Aussehen(kachel);
    if (wieder < 0.99f)
        verstoesse.Add($"{wer}: abgelaufener Countdown ist nicht voller Weizen (Deckung {wieder:0.00})");
    w.Map.RegrowCrop(TileMap.FARM_REGROW_SECONDS);
    if (kachel.ResourceAmount != 175)
        verstoesse.Add($"{wer}: RegrowCrop füllt die Kachel nicht wieder auf (Vorrat={kachel.ResourceAmount})");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: nackter Acker, Weizen wächst sichtbar nach, bis der Vorrat wieder voll steht");
}

/// <summary>Eine Spielwelt: echte Karte, zwei Spieler, der Bildschirm ohne Grafik.</summary>
class Welt
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);
    readonly RTSGameplayScreen _screen = new();
    public readonly TileMap Map;
    public readonly Player P1 = new(0, "P1", "Briten"), P2 = new(1, "P2", "Azteken");
    readonly List<Unit> _units;
    double _zeit, _nebel;

    public Welt(MapSize groesse = MapSize.Standard)
    {
        int seite = MapSizes.Side(groesse);
        Map = new TileMap(seite, seite, 32, MapSettings.ForSize(groesse));
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
            Map.RegrowGrass(dt);
            Map.UpdateSheep(dt);
            Map.UpdateDeer(dt);
            Map.UpdateRabbits(dt);
            Map.UpdateBoars(dt);
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
