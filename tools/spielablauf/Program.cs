// Spielablauf: stellt Abläufe aus RTSGameplayScreen ohne Grafik nach und prüft sie.
//
//   dotnet run --project tools/spielablauf -- bauen weiterbauen linksklick farm schafe wild herde bewegen minimap zoom leiste zeitalter turm menue animation fenster werkzeug feld fahne gehen karten pfad wind gang blick dunkel
//
// Dazu neubauten (Gebäude der zweiten Tastenreihe), tuer (Abliefern vor der Tür) und
// auswahl (Gebäude auswählen), angriff (Gebäude angreifen), soldaten
// (Ausbildung in Kaserne, Schießstand und Stall), forschung (Forschungen und ihre Wirkung),
// strassen (Wege: Trampelpfade werden beim Zeitaufstieg zu Feldwegen und besser, laufen 20 % schneller),
// gebaeudewege (ab der Feudalzeit führt von jedem fertigen Gebäude ein Weg bis ans Stadtzentrum),
// nebelboden (Gras und Pfade im Nebel bleiben, wie man sie zuletzt gesehen hat),
// festerboden (Gras ändert sich nie, nur Pfade, Wege und Gebäude legen sich darüber; ein Pfad bleibt),
// bauphasen, einsturz und pfeile (Animationen A1-A3: Bauphasen, Einsturz-Liste, Pfeile landen beim Treffer),
// wegkarte (was der Wege-Shader als Weg zeichnet, schräge Lücken aufgefüllt),
// zielen (Gegner an der ganzen Figur anklicken), verteidigung (Soldaten verteidigen das
// Stadtzentrum) und kampfbild (Schlagbilder und Blick gegen Einheiten). Die Geräusche
// (tools/klang) prüfen klangbrett (Drossel des SoundBoard), arbeitsklang (Werkzeugschläge im
// Takt, Hörweite und Lage) und kampfklang (Hieb, Klinge, Bogen, Tod, Einsturz).
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
using AgeOfEvolutions.Core.Audio;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using BuildingType = AoE.Core.Entities.BuildingType;
using BuildingRules = AoE.Core.Economy.BuildingRules;
using CoreVillager = AoE.Core.Entities.Villager;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using Age = AoE.Core.Economy.Age;
using AgeProgress = AoE.Core.Economy.AgeProgress;
using CorePosition = AoE.Core.Entities.Position;
using GatherJob = AoE.Core.Economy.GatherJob;
using Tech = AoE.Core.Economy.Tech;
using TechRules = AoE.Core.Economy.TechRules;

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
        Pruefe($"Karte {karte}, verteilen", () => Verteilen(karte, verstoesse));
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
    // Wege (C12): Trampelpfade werden beim Zeitaufstieg zu Wegen, laufen 20 % schneller
    if (gruppen.Contains("strassen"))
        Pruefe($"Karte {karte}, Straße", () => Strassen(karte, verstoesse));
    // Boden im Nebel (G14): der Boden zeigt den zuletzt gesehenen Stand, nicht was unsichtbar geschieht
    if (gruppen.Contains("bauphasen"))
        Pruefe($"Karte {karte}, Bauphasen", () => Bauphasen(karte, verstoesse));
    if (gruppen.Contains("einsturz"))
        Pruefe($"Karte {karte}, Einsturz", () => Einsturz(karte, verstoesse));
    if (gruppen.Contains("pfeile"))
        Pruefe($"Karte {karte}, Pfeile", () => Pfeile(karte, verstoesse));
    if (gruppen.Contains("festerboden"))
        Pruefe($"Karte {karte}, Fester Boden", () => FesterBoden(karte, verstoesse));
    if (gruppen.Contains("nebelboden"))
        Pruefe($"Karte {karte}, Boden im Nebel", () => NebelBoden(karte, verstoesse));
    // Wegkarte (C16): was der Wege-Shader als Weg zeichnet, schräge Lücken aufgefüllt
    if (gruppen.Contains("wegkarte"))
        Pruefe($"Karte {karte}, Wegkarte", () => Wegkarte(karte, verstoesse));
    // Wege von Gebäuden (C16): ab der Feudalzeit führt von jedem fertigen Gebäude ein Weg zum Wegenetz
    if (gruppen.Contains("gebaeudewege"))
        Pruefe($"Karte {karte}, Gebäudewege", () => GebaeudeWege(karte, verstoesse));
    if (gruppen.Contains("reparatur"))
        Pruefe($"Karte {karte}, Reparatur", () => Reparatur(karte, verstoesse));
    if (gruppen.Contains("turm"))
        Pruefe($"Karte {karte}, Wachturm", () => Turm(karte, verstoesse));
    if (gruppen.Contains("neubauten"))
        Pruefe($"Karte {karte}, Neubauten", () => Neubauten(karte, verstoesse));
    if (gruppen.Contains("tuer"))
        Pruefe($"Karte {karte}, Türen", () => Tueren(karte, verstoesse));
    if (gruppen.Contains("auswahl"))
        Pruefe($"Karte {karte}, Gebäudeauswahl", () => Auswahl(karte, verstoesse));
    if (gruppen.Contains("angriff"))
        Pruefe($"Karte {karte}, Angriff", () => Angriff(karte, verstoesse));
    if (gruppen.Contains("soldaten"))
        Pruefe($"Karte {karte}, Soldaten", () => Soldaten(karte, verstoesse));
    if (gruppen.Contains("forschung"))
        Pruefe($"Karte {karte}, Forschung", () => Forschung(karte, verstoesse));
    if (gruppen.Contains("schlag") && karte == 1)
        Pruefe("Schlag", () => Schlag(verstoesse));
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
    if (gruppen.Contains("dunkel"))
        Pruefe($"Karte {karte}, Dunkel", () => Dunkel(karte, verstoesse));
    if (gruppen.Contains("blick") && karte == 1)
        Pruefe("Blick", () => Blick(verstoesse));
    if (gruppen.Contains("gang") && karte == 1)
        Pruefe("Gang", () => Gang(verstoesse));
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
    if (gruppen.Contains("zielen"))
        Pruefe($"Karte {karte}, Zielen", () => Zielen(karte, verstoesse));
    if (gruppen.Contains("verteidigung"))
        Pruefe($"Karte {karte}, Verteidigung", () => Verteidigung(karte, verstoesse));
    // Das Kampfbild hängt nicht von der Karte ab
    if (gruppen.Contains("kampfbild") && karte == 1)
        Pruefe("Kampfbild", () => Kampfbild(verstoesse));
    if (gruppen.Contains("zielwechsel"))
        Pruefe($"Karte {karte}, Zielwechsel", () => Zielwechsel(karte, verstoesse));
    // Die Drossel und die reinen Rechnungen hängen nicht von der Karte ab
    if (gruppen.Contains("klangbrett") && karte == 1)
        Pruefe("Klangbrett", () => Klangbrett(verstoesse));
    if (gruppen.Contains("arbeitsklang"))
        Pruefe($"Karte {karte}, Arbeitsklang", () => Arbeitsklang(karte, verstoesse));
    if (gruppen.Contains("kampfklang"))
        Pruefe($"Karte {karte}, Kampfklang", () => Kampfklang(karte, verstoesse));
    // Niederlage: ohne Dorfbewohner UND ohne Stadtzentrum ist die Seite
    // verloren — pro Karte, weil jeder Ablauf eine eigene Welt braucht.
    if (gruppen.Contains("niederlage"))
        Pruefe($"Karte {karte}, Niederlage", () => Niederlage(karte, verstoesse));
    // Spielstand (SV3): speichern, laden, weiterspielen - pro Karte eine eigene Welt
    if (gruppen.Contains("speichern"))
        Pruefe($"Karte {karte}, Spielstand", () => Speichern(karte, verstoesse));
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

// Gehen (C7v, H2): ein Dorfbewohner, der zwölf Kacheln weit geschickt wird, fährt an
// statt mit vollem Tempo loszuspringen, tritt nie auf eine Kachel, die er nicht
// betreten darf, kommt an und geht dabei nicht weiter als der Weg der Wegsuche -
// über freies Land gerade statt im Zickzack der Kachelmitten. Die Laufbilder folgen
// der Strecke: acht Gehphasen je Doppelschritt (C4m), jede ein Viertel einer Schrittlänge.
static void Gehen(List<string> verstoesse)
{
    const string wer = "Gehen";
    int vorher = verstoesse.Count;

    // Laufbilder nach Strecke
    var w = new Welt();
    int Phase(float gelaufen) => (int)w.Call("VillagerWalkPhase", gelaufen);
    float schritt = Gait.VILLAGER_STRIDE;
    var folge = new List<int>();
    for (float s = 0f; s < 4f * schritt; s += schritt / 40f)
    {
        int p = Phase(s);
        if (folge.Count == 0 || folge[^1] != p) folge.Add(p);
    }
    var sollFolge = Enumerable.Range(0, 8).Concat(Enumerable.Range(0, 8)).Append(0).ToList();
    if (!folge.SequenceEqual(sollFolge))
        verstoesse.Add($"{wer}: Laufbilder über zwei Doppelschritte {string.Join(",", folge)} statt {string.Join(",", sollFolge)}");
    // Die Füße rutschen nicht: die Schrittlänge ist die der Gehphasen aus tools/bilder/gang.py
    if (MathF.Abs(schritt - 5.06f) > 0.01f)
        verstoesse.Add($"{wer}: Schrittlänge {schritt} Welteinheiten statt 5,06 wie in den Gehphasen");

    // Ein Gang über zwölf Kacheln
    var v = w.Dorfbewohner().First();
    var ziel = w.FreieKachel(v, 12);
    var start = v.Position;
    var weg = w.Map.FindPath(start, w.Map.GridToWorld(ziel));
    float wegLaenge = 0f;
    var vorigerPunkt = start;
    foreach (var punkt in weg)
    {
        wegLaenge += Vector2.Distance(vorigerPunkt, punkt);
        vorigerPunkt = punkt;
    }
    w.Waehle(new List<Unit> { v });
    w.Linksklick(w.Map.GridToWorld(ziel));
    float voll = v.MovementSpeed * 40f / 60f;   // volle Strecke je Bild
    float erstes = -1f, gelaufen = 0f;
    var zuletzt = v.Position;
    string fehltritt = null;
    float dauer = w.LaufeBis(() =>
    {
        float d = Vector2.Distance(zuletzt, v.Position);
        if (erstes < 0f && d > 0f) erstes = d;
        gelaufen += d;
        zuletzt = v.Position;
        var c = w.Map.WorldToGrid(v.Position);
        if (fehltritt == null && !w.Map.IsWalkable((int)c.X, (int)c.Y))
            fehltritt = $"({(int)c.X},{(int)c.Y}) {w.Map.GetTile((int)c.X, (int)c.Y)?.Type}";
        return v.State == UnitState.Idle;
    }, 60);
    if (v.State != UnitState.Idle)
        verstoesse.Add($"{wer}: nach {dauer:0} s noch nicht angekommen");
    if (fehltritt != null)
        verstoesse.Add($"{wer}: tritt unterwegs auf die Kachel {fehltritt}");
    if (erstes < 0f || erstes > 0.4f * voll)
        verstoesse.Add($"{wer}: im ersten Bild {erstes:0.00} Welteinheiten, volles Tempo wären {voll:0.00} - er fährt nicht an");
    if (weg.Count > 0 && gelaufen > wegLaenge + 2f)
        verstoesse.Add($"{wer}: {gelaufen:0} Welteinheiten gegangen, der Weg der Wegsuche ist nur {wegLaenge:0} lang");
    if (Vector2.Distance(v.Position, w.Map.GridToWorld(ziel)) > 16f)
        verstoesse.Add($"{wer}: steht {Vector2.Distance(v.Position, w.Map.GridToWorld(ziel)):0} Welteinheiten neben dem Ziel");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Laufbilder nach Strecke, fährt an, {gelaufen:0} statt {wegLaenge:0} Welteinheiten, nur begehbare Kacheln, {dauer:0.0} s");
}

// Gang (H1): die Rechnungen fürs Gehen (Gait) und die gerade Strecke über freies Land
// (TileMap.IsSegmentWalkable), jede gegen ihren Vertrag.
static void Gang(List<string> verstoesse)
{
    const string wer = "Gang";
    int vorher = verstoesse.Count;
    void Soll(bool ok, string was)
    {
        if (!ok) verstoesse.Add($"{wer}: {was}");
    }

    // WalkFrame: Viertel eines Doppelschritts, walked = 0 mitten im Bild 0
    foreach (var (s, bild) in new[] { (0f, 0), (2.4f, 0), (2.6f, 1), (7.4f, 1), (7.6f, 2), (12.4f, 2), (12.6f, 3),
                                      (17.4f, 3), (17.6f, 0), (20f, 0), (-2.4f, 0), (-2.6f, 3), (1000f, 0), (1005f, 1), (1010f, 2) })
        Soll(Gait.WalkFrame(s, 10f) == bild, $"WalkFrame({s}, 10) = {Gait.WalkFrame(s, 10f)} statt {bild}");
    for (float s = -50f; s < 50f; s += 0.37f)
    {
        int b = Gait.WalkFrame(s, 10f);
        if (b < 0 || b > 3) { Soll(false, $"WalkFrame({s:0.00}, 10) = {b} liegt nicht zwischen 0 und 3"); break; }
    }
    // Acht Gehphasen (C4m): jedes Bild ein Achtel des Doppelschritts, walked = 0 mitten in Bild 0
    foreach (var (s, bild) in new[] { (0f, 0), (1.2f, 0), (1.3f, 1), (3.7f, 1), (3.8f, 2), (18.7f, 7), (18.8f, 0),
                                      (-1.2f, 0), (-1.3f, 7), (1001f, 0), (1003.7f, 1), (1010f, 4) })
        Soll(Gait.WalkFrame(s, 10f, 8) == bild, $"WalkFrame({s}, 10, 8) = {Gait.WalkFrame(s, 10f, 8)} statt {bild}");
    for (float s = -50f; s < 50f; s += 0.37f)
    {
        int b = Gait.WalkFrame(s, 10f, 8);
        if (b < 0 || b > 7) { Soll(false, $"WalkFrame({s:0.00}, 10, 8) = {b} liegt nicht zwischen 0 und 7"); break; }
    }
    Soll(Gait.WALK_FRAMES == 8, $"WALK_FRAMES = {Gait.WALK_FRAMES} statt 8");

    // Bob: sin²(π walked / stride)
    for (float s = -20f; s < 40f; s += 0.53f)
    {
        float soll = MathF.Pow(MathF.Sin(MathF.PI * s / 10f), 2f);
        if (MathF.Abs(Gait.Bob(s, 10f) - soll) > 0.0005f) { Soll(false, $"Bob({s:0.00}, 10) = {Gait.Bob(s, 10f):0.000} statt {soll:0.000}"); break; }
    }

    // Approach: exponentiell, nie über das Ziel
    float a = Gait.Approach(0f, 1f, Gait.ACCELERATION_TIME);
    Soll(MathF.Abs(a - (1f - MathF.Exp(-1f))) < 0.001f, $"Approach(0, 1, ACCELERATION_TIME) = {a:0.000} statt {1f - MathF.Exp(-1f):0.000}");
    Soll(Gait.Approach(0.9f, 1f, 100f) <= 1f, "Approach schießt über das Ziel hinaus");
    float ab = Gait.Approach(0.5f, 0.2f, 0.1f);
    Soll(ab < 0.5f && ab >= 0.2f, $"Approach(0.5, 0.2, 0.1) = {ab:0.000} - bremst nicht richtig");
    Soll(Gait.Approach(0.3f, 1f, 0f) == 0.3f && Gait.Approach(0.3f, 1f, -1f) == 0.3f, "Approach ändert das Tempo ohne verstrichene Zeit");

    // ArrivalSpeed
    foreach (var (rest, soll) in new[] { (20f, 1f), (10f, 1f), (5f, 0.7f), (0f, 0.4f), (-3f, 0.4f) })
        Soll(MathF.Abs(Gait.ArrivalSpeed(rest) - soll) < 0.001f, $"ArrivalSpeed({rest}) = {Gait.ArrivalSpeed(rest):0.000} statt {soll}");

    // Lean
    foreach (var (tempo, soll) in new[] { (0f, 0f), (1f, Gait.LEAN), (2f, Gait.LEAN), (-1f, 0f), (0.5f, Gait.LEAN / 2f) })
        Soll(MathF.Abs(Gait.Lean(tempo) - soll) < 0.0001f, $"Lean({tempo}) = {Gait.Lean(tempo):0.0000} statt {soll:0.0000}");

    // Ease und EaseSpeed
    foreach (var (p, e, v) in new[] { (0f, 0f, 0f), (1f, 1f, 0f), (0.5f, 0.5f, 1f), (0.25f, 0.15625f, 0.75f), (-1f, 0f, 0f), (2f, 1f, 0f) })
    {
        Soll(MathF.Abs(Gait.Ease(p) - e) < 0.0001f, $"Ease({p}) = {Gait.Ease(p):0.0000} statt {e}");
        Soll(MathF.Abs(Gait.EaseSpeed(p) - v) < 0.0001f, $"EaseSpeed({p}) = {Gait.EaseSpeed(p):0.0000} statt {v}");
    }
    float vorige = -1f;
    for (float p = 0f; p <= 1f; p += 0.01f)
    {
        float e = Gait.Ease(p);
        if (e < vorige) { Soll(false, $"Ease fällt bei {p:0.00}"); break; }
        vorige = e;
    }

    // IsSegmentWalkable auf echten Karten
    for (int karte = 1; karte <= 3; karte++)
    {
        var w = new Welt();
        var m = w.Map;
        Vector2 Mitte(int x, int y) => m.GridToWorld(new Vector2(x, y));
        bool Frei(int x, int y) => m.IsWalkable(x, y);
        // eine Reihe aus sieben freien Kacheln, auch darüber und darunter frei
        bool gerade = false, ueberWasser = false, quer = false;
        for (int y = 2; y < m.Height - 2 && !gerade; y++)
            for (int x = 1; x < m.Width - 8 && !gerade; x++)
                if (Enumerable.Range(x, 7).All(i => Frei(i, y) && Frei(i, y - 1) && Frei(i, y + 1)))
                {
                    gerade = true;
                    Soll(m.IsSegmentWalkable(Mitte(x, y), Mitte(x + 6, y), 6f),
                         $"Karte {karte}: die freie Reihe ({x},{y}) bis ({x + 6},{y}) gilt nicht als begehbar");
                    Soll(m.IsSegmentWalkable(Mitte(x, y), Mitte(x, y), 6f), $"Karte {karte}: ein Punkt auf freier Kachel gilt nicht als begehbar");
                }
        // zwei freie Kacheln mit einer gesperrten dazwischen
        for (int y = 1; y < m.Height - 1 && !ueberWasser; y++)
            for (int x = 1; x < m.Width - 3 && !ueberWasser; x++)
                if (Frei(x, y) && !Frei(x + 1, y) && Frei(x + 2, y))
                {
                    ueberWasser = true;
                    Soll(!m.IsSegmentWalkable(Mitte(x, y), Mitte(x + 2, y), 0f),
                         $"Karte {karte}: die Strecke ({x},{y}) bis ({x + 2},{y}) über die gesperrte Kachel ({x + 1},{y}) gilt als begehbar");
                    Soll(!m.IsSegmentWalkable(Mitte(x, y), Mitte(x + 1, y), 0f),
                         $"Karte {karte}: eine Strecke, die auf der gesperrten Kachel ({x + 1},{y}) endet, gilt als begehbar");
                }
        // eine freie Reihe direkt über einer gesperrten Kachel: mit Abstand nicht, ohne schon
        for (int y = 1; y < m.Height - 2 && !quer; y++)
            for (int x = 1; x < m.Width - 4 && !quer; x++)
                if (Enumerable.Range(x, 3).All(i => Frei(i, y) && Frei(i, y - 1)) && !Frei(x + 1, y + 1))
                {
                    quer = true;
                    Soll(m.IsSegmentWalkable(Mitte(x, y), Mitte(x + 2, y), 0f),
                         $"Karte {karte}: die freie Reihe ({x},{y}) bis ({x + 2},{y}) gilt ohne Abstand nicht als begehbar");
                    Soll(!m.IsSegmentWalkable(Mitte(x, y), Mitte(x + 2, y), 20f),
                         $"Karte {karte}: mit 20 Welteinheiten Abstand streift die Reihe ({x},{y}) die gesperrte Kachel ({x + 1},{y + 1}) - gilt aber als begehbar");
                }
        Soll(gerade && ueberWasser && quer, $"Karte {karte}: keine passende Stelle gefunden (gerade {gerade}, gesperrt {ueberWasser}, Abstand {quer})");
        Soll(!m.IsSegmentWalkable(new Vector2(-5f, 40f), new Vector2(40f, 40f), 0f), "eine Strecke, die links außerhalb der Karte beginnt, gilt als begehbar");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Laufbilder, Wippen, Anfahren, Abbremsen, Vorlage und weicher Tierschritt nach Vertrag; gerade Strecken auf drei Karten");
}

// Blick (J1, J2): die Figuren zeigen sich nur von der Seite. Sie blicken zur Seite,
// in die sie gehen - fast senkrecht zur Seite ihres Ziels -, und bei der Arbeit zu
// dem, woran sie arbeiten. Dafür stellen sich Dorfbewohner neben Quelle und Baustelle
// bevorzugt seitlich statt darüber oder darunter, und Schafe und Wild machen kaum
// rein senkrechte Schritte. Entstanden am 2026-10-05: der Nutzer sah Figuren, die nicht
// in Laufrichtung blickten; eine Messung fand Dorfbewohner, die genau über ihrem Baum
// standen und mit der Axt zur Seite ins Leere schlugen.
static void Blick(List<string> verstoesse)
{
    const string wer = "Blick";
    int vorher = verstoesse.Count;
    void Soll(bool ok, string was)
    {
        if (!ok) verstoesse.Add($"{wer}: {was}");
    }

    // Die Regel selbst
    Soll(Gait.FacingLeft(new Vector2(-1f, 0.2f), Vector2.Zero, false), "nach links gehend blickt sie nicht nach links");
    Soll(!Gait.FacingLeft(new Vector2(1f, -0.5f), new Vector2(-50f, 0f), true), "nach rechts gehend blickt sie zum Ziel statt in Laufrichtung");
    Soll(Gait.FacingLeft(new Vector2(0.1f, 1f), new Vector2(-40f, 200f), false), "fast senkrecht gehend blickt sie nicht zur Seite ihres Ziels (links)");
    Soll(!Gait.FacingLeft(new Vector2(-0.1f, -1f), new Vector2(40f, -200f), true), "fast senkrecht gehend blickt sie nicht zur Seite ihres Ziels (rechts)");
    Soll(Gait.FacingLeft(new Vector2(0f, 1f), new Vector2(5f, 100f), true) && !Gait.FacingLeft(new Vector2(0f, 1f), new Vector2(5f, 100f), false),
         "liegt das Ziel genau darunter, ändert sich die Blickrichtung");
    Soll(Gait.FacingLeft(Vector2.Zero, new Vector2(-20f, 0f), false), "im Stand blickt sie nicht zu ihrer Arbeit links");
    Soll(Gait.FacingLeft(Vector2.Zero, Vector2.Zero, true) && !Gait.FacingLeft(Vector2.Zero, Vector2.Zero, false),
         "ohne Bewegung und Ziel ändert sich die Blickrichtung");

    bool Links(Welt w, Unit u)
    {
        var m = ((System.Collections.IDictionary)w.Get("_unitMotion"))[u]!;
        return (bool)m.GetType().GetField("FacingLeft")!.GetValue(m)!;
    }

    // Gehen in alle Richtungen
    {
        var w = new Welt();
        var v = w.Dorfbewohner().First();
        w.Waehle(new List<Unit> { v });
        int seitlich = 0, seitlichFalsch = 0, senkrecht = 0, senkrechtRichtig = 0;
        var rng = new Random(11);
        for (int lauf = 0; lauf < 16; lauf++)
        {
            var hier = w.Map.WorldToGrid(v.Position);
            float winkel = lauf * MathF.Tau / 16f + (float)rng.NextDouble() * 0.3f;
            var wunsch = hier + new Vector2(MathF.Cos(winkel), MathF.Sin(winkel)) * (5 + rng.Next(6));
            var ziel = Enumerable.Range(0, w.Map.Width).SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)))
                .Where(c => w.Map.IsWalkable((int)c.X, (int)c.Y) && w.Map.GetTile((int)c.X, (int)c.Y).ResourceType == null)
                .OrderBy(c => Vector2.Distance(c, wunsch)).First();
            var endpunkt = w.Map.GridToWorld(ziel);
            w.Linksklick(endpunkt);
            var zuletzt = v.Position;
            w.LaufeBis(() =>
            {
                w.Call("UpdateUnitMotion", 1f / 60f);
                var d = v.Position - zuletzt;
                zuletzt = v.Position;
                if (d.Length() < 0.01f) return v.State == UnitState.Idle;
                bool links = Links(w, v);
                if (MathF.Abs(d.X) > 0.3f * d.Length())
                {
                    seitlich++;
                    if ((d.X < 0) != links) seitlichFalsch++;
                }
                else if (MathF.Abs(endpunkt.X - v.Position.X) > 8f)
                {
                    senkrecht++;
                    if ((endpunkt.X < v.Position.X) == links) senkrechtRichtig++;
                }
                return v.State == UnitState.Idle;
            }, 60);
        }
        Soll(seitlich > 100 && seitlichFalsch == 0, $"seitwärts gehend {seitlichFalsch} von {seitlich} Bildern mit falscher Blickrichtung");
        Soll(senkrecht == 0 || senkrechtRichtig >= 0.95f * senkrecht,
             $"fast senkrecht gehend blickt er nur in {senkrechtRichtig} von {senkrecht} Bildern zur Seite seines Ziels");
        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: Gehen - seitwärts {seitlich} Bilder richtig, fast senkrecht {senkrechtRichtig} von {senkrecht} zur Seite des Ziels");
    }

    // Arbeit an Holz, Stein und Gold: seitlich neben der Quelle, Blick zu ihr
    int arbeiten = 0, darueber = 0;
    for (int karte = 1; karte <= 3; karte++)
    {
        foreach (var res in new[] { Resource.Wood, Resource.Stone, Resource.Gold })
        {
            var w = new Welt();
            var v = w.Dorfbewohner().First();
            var start = w.Map.WorldToGrid(v.Position);
            var quellen = Enumerable.Range(0, w.Map.Width).SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => (x, y)))
                .Where(p => w.Map.GetTile(p.x, p.y) is { } t && t.ResourceType == res && t.ResourceAmount > 0)
                .OrderBy(p => Vector2.Distance(new Vector2(p.x, p.y), start)).ToList();
            if (quellen.Count == 0) continue;
            var q = quellen[0];
            // Direkt als Auftrag: ein Klick sammelt nur an schon erkundeten Kacheln
            v.Job = new GatherJob(0, res, new CorePosition(q.x, q.y));
            w.Call("FollowJob", v);
            w.LaufeBis(() => { w.Call("UpdateUnitMotion", 1f / 60f); return v.Job?.Phase == AoE.Core.Economy.GatherPhase.Gathering; }, 60);
            if (v.Job?.Phase != AoE.Core.Economy.GatherPhase.Gathering) continue;
            for (int i = 0; i < 30; i++) w.Call("UpdateUnitMotion", 1f / 60f);
            var quelle = v.Job.Source;
            var stand = w.Map.WorldToGrid(v.Position);
            bool seiteFrei = w.Map.IsWalkable(quelle.X - 1, quelle.Y) || w.Map.IsWalkable(quelle.X + 1, quelle.Y)
                             || Enumerable.Range(-1, 3).Any(dy => w.Map.IsWalkable(quelle.X - 1, quelle.Y + dy) || w.Map.IsWalkable(quelle.X + 1, quelle.Y + dy));
            arbeiten++;
            if ((int)stand.X == quelle.X)
            {
                darueber++;
                Soll(!seiteFrei, $"{res}: steht genau über/unter der Quelle ({quelle.X},{quelle.Y}), obwohl daneben Platz ist");
            }
            else
            {
                float qx = quelle.X * 32 + 16;
                Soll((qx < v.Position.X) == Links(w, v), $"{res}: arbeitet an ({quelle.X},{quelle.Y}) und blickt von ihr weg");
            }
        }
    }
    Soll(arbeiten >= 6, $"nur {arbeiten} Sammelaufträge kamen zustande");

    // Am Bau: Blick zur Baustelle
    {
        var w = new Welt();
        var v = w.Dorfbewohner().First();
        var platz = w.Bauplatz(BuildingType.House, v, 6);
        w.Waehle(new List<Unit> { v });
        w.Call("PlaceBuilding", BuildingType.House, platz);
        w.LaufeBis(() => { w.Call("UpdateUnitMotion", 1f / 60f); return v.State == UnitState.Building; }, 60);
        for (int i = 0; i < 30; i++) w.Call("UpdateUnitMotion", 1f / 60f);
        if (v.State == UnitState.Building && v.BuildSite is { } site)
        {
            float mitte = (site.X + site.Width / 2f) * 32f;
            if (MathF.Abs(mitte - v.Position.X) > 8f)
                Soll((mitte < v.Position.X) == Links(w, v), "baut und blickt von der Baustelle weg");
            Soll(WorldToGridX(w, v) < site.X || WorldToGridX(w, v) >= site.X + site.Width,
                 "steht über oder unter der Baustelle, obwohl ihre Seiten frei sind");
        }
        else
            Soll(false, "der Bauauftrag kam nicht zustande");
    }

    // Schafe und Wild: kaum rein senkrechte Schritte, Blick in Schrittrichtung
    {
        var w = new Welt();
        int schritte = 0, senkrecht = 0, falsch = 0;
        var lage = new Dictionary<WildAnimal, (int x, int y)>();
        w.LaufeBis(() =>
        {
            for (int x = 0; x < w.Map.Width; x++)
                for (int y = 0; y < w.Map.Height; y++)
                {
                    var tile = w.Map.GetTile(x, y);
                    var tier = tile?.Animal;
                    if (tier == null || tier.Slaughtered) continue;
                    if (lage.TryGetValue(tier, out var alt) && alt != (x, y))
                    {
                        schritte++;
                        int dx = x - alt.x;
                        if (dx == 0) senkrecht++;
                        else if ((dx < 0) != tier.FacingLeft) falsch++;
                    }
                    lage[tier] = (x, y);
                }
            return false;
        }, 90);
        Soll(schritte >= 30, $"in 90 s nur {schritte} Tierschritte");
        Soll(falsch == 0, $"{falsch} von {schritte} Tierschritten mit falscher Blickrichtung");
        Soll(senkrecht <= 0.1f * schritte, $"{senkrecht} von {schritte} Tierschritten rein senkrecht - dabei zeigt das Tier die Seite");
        if (falsch == 0)
            Console.WriteLine($"  ok  {wer}: Tiere - {schritte} Schritte, {senkrecht} rein senkrecht, alle in Blickrichtung");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Arbeit ({arbeiten} Aufträge, {darueber} genau über/unter der Quelle, wo seitlich kein Platz war), Bau - Blick zur Arbeit");
}

static int WorldToGridX(Welt w, Unit u) => (int)w.Map.WorldToGrid(u.Position).X;

// Dunkel (K): ein Klick ins Schwarze wirkt immer, egal was darunter liegt - sonst
// verriete er, dass dort Wasser, Wald oder ein fremdes Gebäude ist. Der Weg wird nur mit
// dem geplant, was der Spieler weiß (TileMap.FindPathKnown): nie gesehene Kacheln gelten
// als begehbar, der Weg ist so lang wie auf dieser Karte nötig, kein Bogen um Unbekanntes.
// Stößt die Einheit unterwegs auf ein Hindernis, plant sie neu - betreten wird es nie,
// und sie hält so nah am Ziel, wie es wirklich geht. Entstanden am 2026-10-05: der
// Nutzer klickte ins Schwarze, und manchmal geschah nichts.
static void Dunkel(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Dunkel";
    int vorher = verstoesse.Count;
    void Soll(bool ok, string was)
    {
        if (!ok) verstoesse.Add($"{wer}: {was}");
    }

    var w = new Welt();
    var m = w.Map;
    bool Bekannt(int x, int y) => !m.IsTileExplored(x, y, 0) || m.IsWalkable(x, y);
    var richtungen = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
    Dictionary<(int, int), int> Abstaende((int x, int y) von, Func<int, int, bool> frei)
    {
        var d = new Dictionary<(int, int), int> { [von] = 0 };
        var q = new Queue<(int x, int y)>();
        q.Enqueue(von);
        while (q.Count > 0)
        {
            var c = q.Dequeue();
            foreach (var (dx, dy) in richtungen)
            {
                var n = (c.x + dx, c.y + dy);
                if (n.Item1 < 0 || n.Item2 < 0 || n.Item1 >= m.Width || n.Item2 >= m.Height) continue;
                if (d.ContainsKey(n) || !frei(n.Item1, n.Item2)) continue;
                d[n] = d[c] + 1;
                q.Enqueue(n);
            }
        }
        return d;
    }
    float Naechster(Dictionary<(int, int), int> erreichbar, (int x, int y) ziel)
        => erreichbar.Keys.Min(k => MathF.Sqrt((k.Item1 - ziel.x) * (k.Item1 - ziel.x) + (k.Item2 - ziel.y) * (k.Item2 - ziel.y)));
    (int x, int y) Kachel(Vector2 welt) => ((int)m.WorldToGrid(welt).X, (int)m.WorldToGrid(welt).Y);

    var v = w.Dorfbewohner().First();
    var s = Kachel(v.Position);
    var alle = Enumerable.Range(0, m.Width).SelectMany(x => Enumerable.Range(0, m.Height).Select(y => (x, y))).ToList();
    float Weite((int x, int y) k) => MathF.Sqrt((k.x - s.x) * (k.x - s.x) + (k.y - s.y) * (k.y - s.y));
    (int x, int y) verborgen = alle.Where(k => !m.IsTileExplored(k.x, k.y, 0) && !m.IsWalkable(k.x, k.y) && Weite(k) >= 8 && Weite(k) <= 60)
                        .OrderBy(Weite).FirstOrDefault((-1, -1));
    (int x, int y) gesehen = alle.Where(k => m.IsTileExplored(k.x, k.y, 0) && !m.IsWalkable(k.x, k.y) && Weite(k) >= 2)
                      .OrderBy(Weite).FirstOrDefault((-1, -1));
    Soll(verborgen.x >= 0, "keine verborgene gesperrte Kachel in 8 bis 60 Kacheln gefunden");
    Soll(gesehen.x >= 0, "keine gesehene gesperrte Kachel gefunden");
    if (verborgen.x < 0 || gesehen.x < 0) return;

    // Planung ins Schwarze: geradewegs, so kurz wie auf der Karte des Spielers
    var bekanntAb = Abstaende(s, Bekannt);
    var ziel = m.GridToWorld(new Vector2(verborgen.x, verborgen.y));
    var weg = m.FindPathKnown(0, v.Position, ziel);
    Soll(weg.Count > 0 && weg[^1] == ziel, $"ins Schwarze auf ({verborgen.x},{verborgen.y}) {m.GetTile(verborgen.x, verborgen.y).Type}: kein Weg bis genau zum Klick");
    Soll(weg.All(p => Bekannt(Kachel(p).x, Kachel(p).y)), "der Weg führt über eine Kachel, die der Spieler als gesperrt kennt");
    Soll(bekanntAb.TryGetValue(verborgen, out int soll) && weg.Count == soll,
         $"der Weg ins Schwarze hat {weg.Count} Schritte, auf der Karte des Spielers sind es {(bekanntAb.TryGetValue(verborgen, out var b) ? b : -1)} - er nutzt Wissen, das der Spieler nicht hat");

    // Planung zu einer gesehenen Sperre: so nah wie auf der Karte des Spielers möglich
    var zuGesehen = m.FindPathKnown(0, v.Position, m.GridToWorld(new Vector2(gesehen.x, gesehen.y)));
    float bestes = Naechster(bekanntAb, gesehen);
    (int x, int y) ende = zuGesehen.Count > 0 ? Kachel(zuGesehen[^1]) : s;
    float erreicht = MathF.Sqrt((ende.x - gesehen.x) * (ende.x - gesehen.x) + (ende.y - gesehen.y) * (ende.y - gesehen.y));
    Soll(MathF.Abs(erreicht - bestes) < 0.01f,
         $"zur gesehenen Sperre ({gesehen.x},{gesehen.y}) endet der Weg {erreicht:0.0} Kacheln entfernt, möglich wären {bestes:0.0}");

    // Verhalten: Klick ins Schwarze auf die verborgene Sperre
    var echtAb = Abstaende(s, (x, y) => m.IsWalkable(x, y));
    float moeglich = Naechster(echtAb, verborgen);
    w.Waehle(new List<Unit> { v });
    w.Linksklick(ziel);
    var start = v.Position;
    w.LaufeBis(() => false, 1f);
    Soll(Vector2.Distance(start, v.Position) >= 10f,
         $"nach dem Klick ins Schwarze auf ({verborgen.x},{verborgen.y}) {m.GetTile(verborgen.x, verborgen.y).Type} hat er sich in 1 s nur {Vector2.Distance(start, v.Position):0.0} Welteinheiten bewegt");
    string fehltritt = null;
    w.LaufeBis(() =>
    {
        var c = Kachel(v.Position);
        if (fehltritt == null && !m.IsWalkable(c.x, c.y))
            fehltritt = $"({c.x},{c.y}) {m.GetTile(c.x, c.y)?.Type}";
        return v.State == UnitState.Idle;
    }, 120);
    Soll(v.State == UnitState.Idle, "nach 120 s noch unterwegs");
    Soll(fehltritt == null, $"betritt unterwegs die gesperrte Kachel {fehltritt}");
    var halt = Kachel(v.Position);
    float abstand = MathF.Sqrt((halt.x - verborgen.x) * (halt.x - verborgen.x) + (halt.y - verborgen.y) * (halt.y - verborgen.y));
    Soll(abstand <= moeglich + 1.5f, $"hält {abstand:0.0} Kacheln vom Ziel, möglich wären {moeglich:0.0}");

    // Verhalten: Klick auf das verborgene Stadtzentrum des Gegners
    var w2 = new Welt();
    var v2 = w2.Dorfbewohner().First();
    var feind = w2.Map.Buildings.First(g => g.OwnerId != 0);
    Soll(!w2.Map.IsTileExplored(feind.X, feind.Y, 0), "das gegnerische Stadtzentrum ist schon erkundet - der Ablauf prüft dann nichts");
    w2.Waehle(new List<Unit> { v2 });
    var start2 = v2.Position;
    w2.Linksklick(w2.Map.GridToWorld(new Vector2(feind.X + 1, feind.Y + 1)));
    w2.LaufeBis(() => false, 1f);
    Soll(Vector2.Distance(start2, v2.Position) >= 10f,
         $"nach dem Klick auf das verborgene gegnerische Stadtzentrum hat er sich in 1 s nur {Vector2.Distance(start2, v2.Position):0.0} Welteinheiten bewegt");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: ins Schwarze auf {m.GetTile(verborgen.x, verborgen.y).Type} geplant wie mit dem Wissen des Spielers ({weg.Count} Schritte), gelaufen ohne Fehltritt, hält {abstand:0.0} Kacheln vom Ziel (möglich {moeglich:0.0}); verborgenes Stadtzentrum: läuft los");
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

// Mehrere selektierte Einheiten bekommen einen Laufbefehl auf denselben
// Punkt — dann verteilen sie sich im Umkreis, statt auf einen Pixel zu
// haufen (Nutzer 2026-10-11: „sie verteilen sich nicht ein wenig um den
// angegebenen Punkt herum … man sieht nur den vordersten"). Ein einzelner
// läuft weiterhin exakt auf den Punkt.
static void Verteilen(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, verteilen";
    int vorher = verstoesse.Count;
    var v = w.Dorfbewohner().Take(3).ToList();
    if (v.Count < 3)
    {
        verstoesse.Add($"{wer}: Testaufbau - nur {v.Count} Dorfbewohner");
        return;
    }
    // Start: die erste Dorfbewohner-Kachel ist frei und bekannt; die anderen
    // drei in den beiden Nachbarkacheln rechts (falls frei), sonst auf der
    // Startkachel. (Analog: NichtDurchGebaeude.)
    var startKachel = w.Map.WorldToGrid(v[0].Position);
    // Drei Startkacheln: 0 = Start, 1 = rechts, 2 = oben (falls frei).
    var p1 = startKachel + new Vector2(1, 0);
    var p2 = startKachel + new Vector2(0, 1);
    // Falls die Neben-Kacheln nicht frei sind, alle drei auf dem Start.
    bool p1frei = w.Map.IsWalkable((int)p1.X, (int)p1.Y) && w.Map.GetTile((int)p1.X, (int)p1.Y)?.Building == null;
    bool p2frei = w.Map.IsWalkable((int)p2.X, (int)p2.Y) && w.Map.GetTile((int)p2.X, (int)p2.Y)?.Building == null;
    v[0].Position = w.Map.GridToWorld(startKachel);
    (v[1].Position, v[2].Position) = (
        p1frei ? w.Map.GridToWorld(p1) : w.Map.GridToWorld(startKachel),
        p2frei ? w.Map.GridToWorld(p2) : w.Map.GridToWorld(startKachel));

    // Ziel: eine freie Kachel ~5 Kacheln vom Start, erreichbar für alle drei.
    Vector2? ziel = null;
    var kandidaten = Enumerable.Range(0, 40).SelectMany(x => Enumerable.Range(0, 40).Select(y => new Vector2(x, y)))
        .Where(c => c.Y >= 1 && c.Y <= w.Map.Height - 2 && c.X >= 1 && c.X <= w.Map.Width - 2
                    && w.Map.IsWalkable((int)c.X, (int)c.Y) && w.Map.GetTile((int)c.X, (int)c.Y).ResourceType == null
                    && string.IsNullOrEmpty(w.Map.GetTile((int)c.X, (int)c.Y).Building))
        .OrderBy(c => Math.Abs(Vector2.Distance(c, startKachel) - 5f))
        .ToList();
    foreach (var c in kandidaten)
    {
        if (v.All(u => w.Map.FindPath(u.Position, w.Map.GridToWorld(c)).Count > 0))
        { ziel = c; break; }
    }
    if (ziel == null)
    {
        verstoesse.Add($"{wer}: Testaufbau - keine erreichbare Zielkachel gefunden");
        return;
    }
    var zielWelt = w.Map.GridToWorld(ziel.Value);

    w.Waehle(v);
    w.Linksklick(zielWelt);

    float dauer = w.LaufeBis(() => v.All(u => u.State == UnitState.Idle), 30f);
    if (v.Any(u => u.State != UnitState.Idle))
    {
        verstoesse.Add($"{wer}: nach {dauer:0} s sind {v.Count(u => u.State == UnitState.Idle)}/3 noch nicht angekommen ({string.Join(",", v.Select(u => u.State))})");
        return;
    }

    // Exaktheiten-Checks:
    // 1) Der erste in der Liste (index 0) läuft auf den exakten Punkt.
    float d0 = Vector2.Distance(v[0].Position, zielWelt);
    if (d0 > 12f)
        verstoesse.Add($"{wer}: v[0] ist {d0:0} px vom angeklickten Punkt entfernt (sollte exakt dorthin laufen)");

    float minAbstand = float.MaxValue;
    for (int i = 0; i < 3; i++)
        for (int j = i + 1; j < 3; j++)
            minAbstand = Math.Min(minAbstand, Vector2.Distance(v[i].Position, v[j].Position));
    if (minAbstand < 8f)
        verstoesse.Add($"{wer}: minimale Zweier-Distanz {minAbstand:0} px — die Einheiten liegen sich im Weg, verteilen sich nicht");

    float d1 = Vector2.Distance(v[1].Position, zielWelt);
    float d2 = Vector2.Distance(v[2].Position, zielWelt);
    if (d1 < 1f)
        verstoesse.Add($"{wer}: v[1] steht exakt auf dem angeklickten Punkt ({d1:0}px) — sollte um den Punkt verteilt sein");
    if (d2 < 1f)
        verstoesse.Add($"{wer}: v[2] steht exakt auf dem angeklickten Punkt ({d2:0}px) — sollte um den Punkt verteilt sein");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: v[0] {d0:0}px, v[1] {d1:0}px, v[2] {d2:0}px; Min-Zweier {minAbstand:0}px");
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

// Niederlage: ein Spieler ohne LEBCNDEN Dorfbewohner UND ohne Stadtzentrum
// (fertig oder im Bau) hat das Spiel verloren. Der Ablauf tötet alle
// Dorfbewohner von P1 (TC steht noch → kein Spielende), dann das TC (→
// Spielende), prüft, dass es nur einmal ausgelöst wird, und dass P2 damit
// gewinnt (P2 hat ja noch Leute und Gebäude).
static void Niederlage(int karte, List<string> verstoesse)
{
    var w = new Welt();
    string wer = $"Karte {karte}, Niederlage";
    int vorher = verstoesse.Count;

    MethodInfo regel = typeof(RTSGameplayScreen).GetMethod("DefeatRule",
        BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException("Methode DefeatRule nicht gefunden - umbenannt?");
    int Regel() => (int)regel.Invoke(null, new object[] { w.Get("units"), w.Map.Buildings, 0 });

    // VOLL: Dorfbewohner + TC stehen → niemand verloren.
    if (Regel() != -1)
        verstoesse.Add($"{wer}: voller Start (Dorfbewohner + TC) gilt schon als Niederlage");

    // Alle Dorfbewohner getötet, TC steht noch → es geht weiter.
    foreach (var v in w.Dorfbewohner().ToList())
        w.Call("DestroyUnit", v);
    if (w.Dorfbewohner().Any())
        verstoesse.Add($"{wer}: es verbleibt ein Dorfbewohner nach DestroyUnit");
    if (Regel() != -1)
        verstoesse.Add($"{wer}: ohne Dorfbewohner, aber mit stehendem TC gilt das Spiel als verloren");

    // Jetzt kommt das TC weg.
    var tc = w.Map.Buildings.FirstOrDefault(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter)
        ?? throw new InvalidOperationException("kein Stadtzentrum von P1 gefunden");
    w.Call("DestroyBuilding", tc);
    if (Regel() != 0)
        verstoesse.Add($"{wer}: ohne Dorfbewohner UND ohne TC gilt das Spiel immer noch nicht als verloren");

    // CheckDefeat() soll die End-Anzeige exakt ein einziges Mal auslösen:
    // der erste Aufruf setzt _gameOverShown=true und schreibt die HUD-Meldung,
    // jeder weitere Aufruf muss mit 'if (_gameOverShown) return;' abbrechen.
    w.Call("CheckDefeat");
    string meldung = (string)w.Get("hudMessage") ?? "";
    if (meldung == "")
        verstoesse.Add($"{wer}: CheckDefeat hat keine End-Meldung gesetzt");
    bool guard = (bool)Screen_of(w).GetType().GetField("_gameOverShown",
        BindingFlags.NonPublic | BindingFlags.Instance)
        .GetValue(Screen_of(w));
    if (!guard)
        verstoesse.Add($"{wer}: _gameOverShown wurde nicht gesetzt — GameOverScreen würde doppelt gebaut");
    w.Call("CheckDefeat");
    // Zweite Aufrufe müssen überspringen: der Guard ist der einzige Nachweis,
    // denn ShowHudMessage überschreibt die Meldung sonst noch einmal (und
    // ScreenManager.AddScreen würde denselben Screen doppelt schieben).

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Leute+TC => weiter; nur TC weg => noch weiter; beides weg => verloren (Meldung: \"{meldung}\")");
}

// Der RTSGameplayScreen hinter einer Welt — für Guard-Prüfungen im Niederlage-Ablauf.
static object Screen_of(Welt w)
    => w.GetType().GetField("_screen", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(w);

// Trampelpfade (G5): wer eine Kachel betritt, tritt sie aus (TileMap.Trample). Ein
// Dorfbewohner läuft zwölfmal zwischen zwei Kacheln hin und her; der Ablauf zählt
// selbst, wie oft er welche Kachel betritt. Jede Kachel ist danach so ausgetreten,
// wie es ihren Schritten entspricht, abzüglich dessen, was in der Zeit nachwuchs -
// und keine, die er nicht betreten hat. Ohne Verkehr vergehen Tritte, aus denen
// noch kein Pfad wurde, gleichmäßig, in WEAR_REGROW_SECONDS ganz; ein Pfad bleibt (G15).
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
    var pfade = abgenutzt.Where(t => t.IsPath).ToList();
    w.Map.RegrowGrass(TileMap.WEAR_REGROW_SECONDS / 2f);
    foreach (var (t, wear) in stand)
    {
        float erwartet = pfade.Contains(t) ? wear : MathF.Max(0f, wear - 0.5f);
        if (MathF.Abs(t.Wear - erwartet) > 0.001f)
        {
            verstoesse.Add($"{wer}: nach halber Nachwachszeit {t.Wear:0.000} statt {erwartet:0.000} auf ({t.X},{t.Y})"
                           + (pfade.Contains(t) ? " - ein Pfad bleibt (G15)" : ""));
            break;
        }
    }
    w.Map.RegrowGrass(TileMap.WEAR_REGROW_SECONDS);
    if (abgenutzt.Any(t => !pfade.Contains(t) && t.Wear > 0f))
        verstoesse.Add($"{wer}: nach voller Nachwachszeit noch Tritte ohne Pfad übrig");
    if (pfade.Count == 0)
        verstoesse.Add($"{wer}: kein Pfad entstanden, obwohl {oft} Kacheln zehnmal betreten wurden");
    else if (pfade.Any(t => t.Wear != stand[t] || !t.IsPath))
        verstoesse.Add($"{wer}: ein Pfad wuchs wieder zu");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: {abgenutzt.Count} Kacheln ausgetreten, {pfad} davon als Pfad, nur auf dem Weg; "
                          + $"Tritte ohne Pfad wachsen zu, {pfade.Count} Pfadkacheln bleiben");
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
    float klein = 768 / (ts * 4f);
    if (Math.Abs(nah[768] - klein) > 0.1f)
        verstoesse.Add($"Zoom 1280x768: ganz nah {nah[768]:0.0} Kacheln übereinander, erwartet {klein:0.0} (MAX_ZOOM 4)");
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
    if (!ohne.SequenceEqual(new[] { "." }))
        verstoesse.Add($"{wer}: ohne Auswahl Tasten [{string.Join(" ", ohne)}], erwartet nur .");

    // Q, W und A gehören dem ausgewählten Stadtzentrum (B1; W ist der Webstuhl, P2)
    w.Call("SelectBuilding", w.Map.Buildings.First(b => b.OwnerId == 0 && b.Type == "Stadtzentrum"));
    w.Call("LayoutButtons");
    var mitTc = w.Tasten().Select(t => t.Label).ToList();
    if (!mitTc.SequenceEqual(new[] { "Q", "W", "A", "." }))
        verstoesse.Add($"{wer}: mit Stadtzentrum Tasten [{string.Join(" ", mitTc)}], erwartet Q, W, A und .");
    int nahrung = w.P1.Resources[Resource.Food];
    w.Klick(w.Tasten().First(t => t.Label == "Q").Rect.Center);
    if (w.P1.Resources[Resource.Food] != nahrung - 25)
        verstoesse.Add($"{wer}: Klick auf Q - Nahrung {nahrung} -> {w.P1.Resources[Resource.Food]}, erwartet 25 weniger");

    w.Waehle(new List<Unit> { w.Dorfbewohner().First() });
    w.Call("LayoutButtons");
    var mit = w.Tasten().Select(t => t.Label).ToList();
    foreach (var soll in new[] { "H", "M", "F", "B", "G", "." })
        if (!mit.Contains(soll))
            verstoesse.Add($"{wer}: mit Dorfbewohner fehlt die Taste {soll} (da: {string.Join(" ", mit)})");
    if (mit.Contains("Q") || mit.Contains("A"))
        verstoesse.Add($"{wer}: mit Dorfbewohner auch Q oder A (da: {string.Join(" ", mit)}) - die gehören dem Stadtzentrum");
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
        Console.WriteLine($"  ok  {wer}: ohne Auswahl nur ., mit Stadtzentrum Q A ., mit Dorfbewohner die Bautasten; Q, H und Minimap per Klick");
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
    w.Call("SelectBuilding", w.Map.Buildings.First(b => b.OwnerId == 0 && b.Type == "Stadtzentrum"));
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

// Wege (C12): Trampelpfade, die in der Dunklen Zeit betreten wurden,
// rücken mit jedem Aufstieg eine Stufe hinauf — Feudal 1, Ritter 2, Imperial 3.
// Auf der neuen Stufe (1 und höher) läuft der Läufer rund 20 % schneller als
// auf der Wiese, und neue Tritte werden ab der Feudalzeit sofort auf der
// erreichten Stufe geboren.
static void Strassen(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Wege";
    var w = new Welt();
    int vorher = verstoesse.Count;
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);

    var arbeiter = w.Dorfbewohner().ToList();
    if (arbeiter.Count < 1)
    {
        verstoesse.Add($"{wer}: kein Dorfbewohner auf der Karte");
        return;
    }
    var laeufer = arbeiter[0];

    // Drei parallele 11er-Reihen mit freier Startkachel davor und freiem
    // Zielkachel am Ende: die Pfad-Reihe (wird betreten), eine Kontrollreihe
    // (bleibt in der Prüfung unbeteiligt) und eine Neue Reihe (wird erst ab
    // der Feudalzeit betreten).
    int sx = -1, sy = -1;
    string dbg = null;
    for (int y = 0; sy < 0 && y + 5 < w.Map.Height; y++)
        for (int x = 2; x + 10 < w.Map.Width; x++)
        {
            bool d = ReiheFrei(w.Map, x, y, 11) && ReiheFrei(w.Map, x, y + 2, 11) && ReiheFrei(w.Map, x, y + 4, 11);
            if (d)
            {
                for (int ry = 0; d && ry < 3; ry++)
                {
                    int yy = ry * 2;
                    var t = w.Map.GetTile(x - 2, y + yy);
                    d = t != null
                        && w.Map.IsWalkable(x - 2, y + yy)
                        && string.IsNullOrEmpty(t.Building)
                        && t.ResourceType == null
                        && t.Food == FoodSource.None
                        && t.Wear <= 0f;
                    if (!d && dbg == null)
                        dbg = $"({x},{y}) Start({x - 2},{y + yy}): t={(t == null ? "null" : t.Type + " Walk=" + t.Walkable + " B='" + t.Building + "' Res=" + t.ResourceType + " Food=" + t.Food + " Wear=" + t.Wear + " IsW=" + w.Map.IsWalkable(x - 2, y + yy))}";
                }
            }
            if (d)
            {
                sx = x; sy = y;
                break;
            }
        }
    if (sx < 0)
    {
        verstoesse.Add($"{wer}: keine drei parallelen freien 11er-Reihen mit freier Startkachel auf der Karte | {dbg}");
        return;
    }
    int pfadY = sy, refY = sy + 2, neuY = sy + 4;

    // Nebel: die Teststrecken werden für den Spieler bekannt.
    var maske = new char[w.Map.Width * w.Map.Height];
    for (int fx = Math.Max(0, sx - 2); fx < sx + 11 && fx < w.Map.Width; fx++)
        foreach (int ry in new[] { pfadY, refY, neuY })
            maske[ry * w.Map.Width + fx] = '1';
    w.Map.RestoreExplored(0, new string(maske));

    void startRang(int y)
    {
        w.Waehle(new List<Unit> { laeufer });
        if (laeufer.Path == null)
            laeufer.Path = new List<Vector2>();
        else
            laeufer.Path.Clear();
        laeufer.Position = w.Map.GridToWorld(new Vector2(sx - 2, y));
        laeufer.State = UnitState.Idle;
        w.LaufeBis(() => false, 0.2f);   // Zustand setzen lassen
    }

    // Der Läufer läuft von der Startkachel aus die ganze Reihe entlang
    // (Start zwei Kacheln davor, Ziel eine Kachel jenseits des Reihe-Endes,
    // damit jede Kachel der Reihe betreten wird) und liefert die dafür
    // benötigte Zeit in Sekunden.
    float lauf(int y)
    {
        startRang(y);
        var ziel = w.Map.GridToWorld(new Vector2(sx + 10, y));
        w.Linksklick(ziel);
        return w.LaufeBis(() => Vector2.Distance(laeufer.Position, ziel) <= 0.75f * w.Map.TileSize, 60f);
    }

    bool ankam(int y)
        => Vector2.Distance(laeufer.Position, w.Map.GridToWorld(new Vector2(sx + 10, y))) <= 0.75f * w.Map.TileSize;

    // 1) Dunkle Zeit: betretene Reihe und Wiese sind gleich schnell —
    //    ohne Weg-Stufe gibt es keinen Bonus.
    float tWiese = lauf(refY);
    if (!ankam(refY))
    {
        verstoesse.Add($"{wer}: der Läufer kam auf der Kontrollreihe gar nicht an");
        return;
    }
    float tPfadDunkel = lauf(pfadY);
    if (!ankam(pfadY))
    {
        verstoesse.Add($"{wer}: der Läufer kam auf der Pfadreihe gar nicht an");
        return;
    }
    if (Math.Abs(tPfadDunkel - tWiese) > 0.10f * tWiese)
        verstoesse.Add($"{wer}: Dunkle Zeit — betretene Reihe {tPfadDunkel:0.00} s gegen {tWiese:0.00} s Wiese, erwartet gleich schnell");
    // Ein echter Trampelpfad entsteht erst durch viele Gänge (C16: ROAD_WEAR_MIN, sechs Tritte;
    // dazwischen wächst das Gras nach, also ein paar mehr) - die Pfadreihe so oft gehen, bis sie
    // die Schwelle erreicht, höchstens zwölfmal; die Kontrollreihe bleibt bei einem Gang
    var pfadkachel = w.Map.GetTile(sx + 5, pfadY);
    float schwelle = AgeOfEvolutions.Core.Data.TileMap.ROAD_WEAR_MIN - 0.001f;
    int gaenge = 1;
    while (gaenge < 12 && Enumerable.Range(sx, 10).Any(x => w.Map.GetTile(x, pfadY).Wear < schwelle))
    {
        lauf(pfadY);
        gaenge++;
    }
    if (pfadkachel.Wear < schwelle)
        verstoesse.Add($"{wer}: nach {gaenge} Gängen ist die Reihe kein Trampelpfad (Wear {pfadkachel.Wear:0.00})");
    if (pfadkachel.RoadLevel != 0)
        verstoesse.Add($"{wer}: Dunkle Zeit — der Pfad hat Stufe {pfadkachel.RoadLevel}, erwartet 0");

    // 2) Aufstieg auf die Feudalzeit: die Trampelpfade werden zu Feldwegen.
    //    Der Aufstieg läuft über den Bildschirm (UpdateAges meldet das neue
    //    Zeitalter und hebt die Pfade auf die neue Stufe).
    foreach (var r in new[] { Resource.Wood, Resource.Stone, Resource.Gold, Resource.Food })
        w.P1.Resources.Add(r, 20000);
    var ages = w.P1.Ages;
    ages.TryStart(w.P1.Resources);
    w.Call("UpdateAges", 2000f);
    if (ages.Current != Age.Feudal)
    {
        verstoesse.Add($"{wer}: Aufstieg auf die Feudalzeit im Test gescheitert (jetzt {ages.Current})");
        return;
    }
    for (int x = sx; x < sx + 10; x++)
        if (w.Map.GetTile(x, pfadY).RoadLevel != 1)
        {
            verstoesse.Add($"{wer}: nach dem Aufstieg ist Kachel ({x},{pfadY}) Stufe {w.Map.GetTile(x, pfadY).RoadLevel}, erwartet 1");
            return;
        }
    // Ein einzelner Gang macht keinen Weg (C16): die Kontrollreihe bleibt Wiese
    if (w.Map.GetTile(sx + 5, refY).RoadLevel != 0)
        verstoesse.Add($"{wer}: die nur einmal begangene Kontrollreihe wurde beim Aufstieg Weg (Stufe {w.Map.GetTile(sx + 5, refY).RoadLevel})");
    // Die unbetretene Reihe bleibt Wiese - nur ein Weg vom Stadtzentrum zum Wegenetz (C16)
    // darf sie beim Aufstieg kreuzen, also höchstens ein, zwei Kacheln
    int neuWege = Enumerable.Range(sx, 11).Count(x => w.Map.GetTile(x, neuY).RoadLevel != 0);
    if (neuWege > 2)
        verstoesse.Add($"{wer}: unbetretene Reihe hat nach dem Aufstieg {neuWege} Wegkacheln, erwartet höchstens 2");

    // 3) Auf dem Feldweg läuft der Läufer rund 20 % schneller als auf der Wiese.
    float tFeldweg = lauf(pfadY);
    if (tFeldweg >= 0.92f * tWiese)
        verstoesse.Add($"{wer}: Feudalzeit — Feldweg {tFeldweg:0.00} s gegen {tWiese:0.00} s Wiese, erwartet rund 20 % schneller");
    if (tFeldweg <= 0.70f * tWiese)
        verstoesse.Add($"{wer}: Feudalzeit — Feldweg {tFeldweg:0.00} s ist unverhältnismäßig schnell (Wiese {tWiese:0.00} s)");

    // 4) Neue Tritte in der Feudalzeit: ein Gang macht noch keinen Weg und keinen Bonus; nach
    //    sechs Gängen ist die Reihe Weg (Stufe 1), und der siebte Gang ist rund 20 % schneller.
    float tNeuErst = lauf(neuY);
    var neuekachel = w.Map.GetTile(sx + 5, neuY);
    if (neuekachel.RoadLevel != 0)
        verstoesse.Add($"{wer}: nach einem Gang in der Feudalzeit ist die neue Reihe schon Weg (Stufe {neuekachel.RoadLevel})");
    if (Math.Abs(tNeuErst - tWiese) > 0.10f * tWiese)
        verstoesse.Add($"{wer}: erster Gang über die neue Reihe {tNeuErst:0.00} s gegen {tWiese:0.00} s Wiese, erwartet gleich schnell");
    int neuGaenge = 1;
    while (neuGaenge < 12 && Enumerable.Range(sx, 10).Any(x => w.Map.GetTile(x, neuY).RoadLevel < 1))
    {
        lauf(neuY);
        neuGaenge++;
    }
    if (neuekachel.RoadLevel != 1)
        verstoesse.Add($"{wer}: nach {neuGaenge} Gängen in der Feudalzeit ist die neue Reihe Stufe {neuekachel.RoadLevel}, erwartet 1");
    float tNeu = lauf(neuY);
    if (tNeu >= 0.92f * tWiese)
        verstoesse.Add($"{wer}: neuer Weg {tNeu:0.00} s — nicht rund 20 % schneller als die Wiese ({tWiese:0.00} s)");
    if (tNeu <= 0.70f * tWiese)
        verstoesse.Add($"{wer}: neuer Weg {tNeu:0.00} s — unverhältnismäßig schnell (Wiese {tWiese:0.00} s)");

    // 5) Jede weitere Zeit hebt die Stufen an: Ritterzeit 2, Imperialzeit 3.
    foreach (var stufe in new (Age Zeitalter, int Stufe)[]
    {
        (Age.Castle, 2), (Age.Imperial, 3),
    })
    {
        foreach (var r in new[] { Resource.Wood, Resource.Stone, Resource.Gold, Resource.Food })
            w.P1.Resources.Add(r, 20000);
        ages.TryStart(w.P1.Resources);
        w.Call("UpdateAges", 2000f);
        if (ages.Current != stufe.Zeitalter)
        {
            verstoesse.Add($"{wer}: Aufstieg auf {stufe.Zeitalter} im Test gescheitert (jetzt {ages.Current})");
            return;
        }
        for (int x = sx; x < sx + 10; x++)
            if (w.Map.GetTile(x, pfadY).RoadLevel != stufe.Stufe)
            {
                verstoesse.Add($"{wer}: {stufe.Zeitalter} — Kachel ({x},{pfadY}) ist Stufe {w.Map.GetTile(x, pfadY).RoadLevel}, erwartet {stufe.Stufe}");
                return;
            }
        if (w.Map.GetTile(sx + 5, neuY).RoadLevel != stufe.Stufe)
            verstoesse.Add($"{wer}: {stufe.Zeitalter} — neue Reihe ist Stufe {w.Map.GetTile(sx + 5, neuY).RoadLevel}, erwartet {stufe.Stufe}");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Dunkle Zeit {tWiese:0.0} / {tPfadDunkel:0.0} s gleich schnell, "
                          + $"Pfad nach {gaenge} Gängen, Feldweg {tFeldweg:0.0} s (~20 % schneller), ein Gang kein Weg "
                          + $"({tNeuErst:0.0} s), nach {neuGaenge} Gängen Weg ({tNeu:0.0} s), Stufen 1 → 2 → 3 gestiegen");
}

// Boden im Nebel (G14). Der Nutzer (2026-10-16): „Sichtbares gelende verändert den gras typ
// obwohl man niemanden dort sieht. Das sollte nicht sein. Auch wenn tiere durchlaufen die man
// nicht sieht sollte sich das Gras nicht ändern.“ Das Lebendbild des Bodenshaders (und die
// Büschel des hohen Grases) zeigt im Nebel den zuletzt gesehenen Stand: ein Trampelpfad, ein
// Weg oder ein Tier dort ändert nichts, bis wieder jemand hinsieht - dann sofort.
static void NebelBoden(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Boden im Nebel";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    w.Call("UpdateGroundMemory", true);
    var daten = (Color[])w.Get("_groundLiveData");
    if (daten == null || daten.Length != w.Map.Width * w.Map.Height)
    {
        verstoesse.Add($"{wer}: UpdateGroundMemory legt kein Lebendbild in Kartengröße an");
        return;
    }
    float hoch(int x, int y) => (float)w.Call("TallGrassAt", x, y);
    float gesehen(int x, int y) => (float)w.Call("SeenTallGrassAt", x, y);

    // Zwei Wiesenkacheln mit hohem Gras im Nebel (A, B) und eine in Sicht (C)
    (int X, int Y)? suche(bool sichtbar, (int X, int Y)? ausser = null)
    {
        for (int x = 2; x < w.Map.Width - 2; x++)
            for (int y = 2; y < w.Map.Height - 2; y++)
            {
                var t = w.Map.GetTile(x, y);
                if (t.Type == TileType.Grassland && string.IsNullOrEmpty(t.Building) && t.Food == FoodSource.None
                    && t.ResourceType == null && t.Wear <= 0f && t.RoadLevel == 0 && hoch(x, y) > 0.3f
                    && w.Map.IsTileVisible(x, y, 0) == sichtbar && (x, y) != ausser
                    && !w.Map.Units.Any(u => w.Map.WorldToGrid(u.Position) == new Vector2(x, y)))
                    return (x, y);
            }
        return null;
    }
    if (suche(false) is not { } a || suche(false, a) is not { } b || suche(true) is not { } c)
    {
        Console.WriteLine($"      {wer}: keine passenden Kacheln mit hohem Gras - übersprungen");
        return;
    }
    int idx((int X, int Y) k) => k.Y * w.Map.Width + k.X;
    var aVorher = daten[idx(a)];
    float aHochVorher = gesehen(a.X, a.Y);
    if (Math.Abs(aHochVorher - aVorher.A / 255f) > 0.001f || aHochVorher <= 0.3f)
        verstoesse.Add($"{wer}: SeenTallGrassAt {aHochVorher:0.00} passt nicht zum Lebendbild ({aVorher.A / 255f:0.00})");

    // Im Nebel geschieht etwas: ein unsichtbarer Gegner tritt einen Pfad aus, ein Tier zieht
    // durch, ein Weg entsteht - und in Sicht wird ebenfalls getreten
    for (int i = 0; i < 25; i++)
    {
        w.Map.Trample(a.X, a.Y);
        w.Map.Trample(b.X, b.Y);
        w.Map.Trample(c.X, c.Y);
    }
    w.Map.GetTile(a.X, a.Y).Food = FoodSource.Sheep;
    w.Map.GetTile(b.X, b.Y).RoadLevel = 1;
    w.Call("UpdateGroundMemory", false);
    daten = (Color[])w.Get("_groundLiveData");
    if (daten[idx(a)] != aVorher)
        verstoesse.Add($"{wer}: im Nebel änderte sich der Boden bei {a} (Pfad und Tier): {aVorher} -> {daten[idx(a)]}");
    if (Math.Abs(gesehen(a.X, a.Y) - aHochVorher) > 0.001f)
        verstoesse.Add($"{wer}: im Nebel wichen die Grasbüschel bei {a} dem unsichtbaren Tier ({aHochVorher:0.00} -> {gesehen(a.X, a.Y):0.00})");
    if (daten[idx(b)].R > 10)
        verstoesse.Add($"{wer}: im Nebel erschien bei {b} ein Trampelpfad (rot {daten[idx(b)].R})");
    // Die Wegkarte (C16) merkt sich ebenfalls nur das Gesehene
    var wegkarte = (Color[])w.Get("_roadMapData");
    if (wegkarte == null || wegkarte[idx(b)].R > 10)
        verstoesse.Add($"{wer}: im Nebel erschien bei {b} ein Weg in der Wegkarte");
    if (daten[idx(c)].R < 200)
        verstoesse.Add($"{wer}: in Sicht wurde der Trampelpfad bei {c} nicht nachgezogen (rot {daten[idx(c)].R})");

    // Jemand sieht hin: dann gilt sofort der Ist-Zustand
    var spaeher = w.Dorfbewohner().First();
    spaeher.Position = w.Map.GridToWorld(new Vector2(a.X + 1, a.Y));
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    if (!w.Map.IsTileVisible(a.X, a.Y, 0))
    {
        verstoesse.Add($"{wer}: der Dorfbewohner neben {a} sieht die Kachel nicht - Prüfung unmöglich");
        return;
    }
    w.Call("UpdateGroundMemory", false);
    daten = (Color[])w.Get("_groundLiveData");
    if (daten[idx(a)].R < 200 || daten[idx(a)].A > 5 || gesehen(a.X, a.Y) > 0.02f)
        verstoesse.Add($"{wer}: in Sicht zeigt {a} den Pfad und das Tier nicht (rot {daten[idx(a)].R}, Gras {daten[idx(a)].A})");

    // Ein Komplettabgleich (Spielbeginn, Spielstand laden) nimmt auch den Nebel mit
    if (!w.Map.IsTileVisible(b.X, b.Y, 0))
    {
        w.Call("UpdateGroundMemory", true);
        daten = (Color[])w.Get("_groundLiveData");
        wegkarte = (Color[])w.Get("_roadMapData");
        if (wegkarte[idx(b)].R < 250)
            verstoesse.Add($"{wer}: UpdateGroundMemory(true) übernimmt den Weg im Nebel bei {b} nicht in die Wegkarte");
        // Unter einem Weg nur niedergetretenes Gras (rot höchstens 0,35), keine nackte Erde (C16)
        if (daten[idx(b)].R > 92 || daten[idx(b)].R < 80)
            verstoesse.Add($"{wer}: unter dem Weg bei {b} steht rot {daten[idx(b)].R} im Lebendbild, erwartet 0,35 (~89)");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Pfad, Tier und Weg im Nebel unsichtbar ({a}, {b}), in Sicht sofort ({c}), "
                          + "beim Hinsehen nachgezogen, Komplettabgleich nimmt den Nebel mit");
}

// Animationen (A1-A3). Der Nutzer (2026-10-17): „ich würde noch gerne mehr Animationen im Game
// haben. Zum Beispiel wenn ein Gebäude zerstört wird soll das animiert werden. Wenn ein Geäude
// erstellt wird sollten ebenfalls 3-4 Darstellung sich im Bau befindliches Gebäude Dargestellt
// werden. Zudem soll man die Pfeile der Bogenschützen sehen.“ Ohne Grafik prüfbar ist die Logik:
// die Bauphasen, die Einsturz-Liste und wann ein Pfeil fliegt; wie es aussieht, zeigen Fotos.
static object Feldwert(object obj, string name)
    => obj.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj);

// Bauphasen (A1): vier Phasen zu je einem Viertel des Baufortschritts
static void Bauphasen(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Bauphasen";
    int vorher = verstoesse.Count;
    var phase = typeof(RTSGameplayScreen).GetMethod("ConstructionStage", BindingFlags.NonPublic | BindingFlags.Static);
    foreach (var (p, soll) in new[] { (0f, 0), (0.1f, 0), (0.249f, 0), (0.25f, 1), (0.49f, 1), (0.5f, 2), (0.74f, 2), (0.75f, 3), (0.99f, 3), (1f, 3) })
    {
        int ist = (int)phase.Invoke(null, new object[] { p });
        if (ist != soll)
            verstoesse.Add($"{wer}: Fortschritt {p:0.000} ergibt Phase {ist}, erwartet {soll}");
    }
    // Ohne geladene Bilder (kein Grafikgerät) gibt es kein Gebäudebild - dann der gezeichnete Bauplatz
    var w = new Welt();
    var tc = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
    if (w.Call("BuildingSpriteOf", tc) != null)
        verstoesse.Add($"{wer}: BuildingSpriteOf liefert ohne geladene Bilder ein Bild");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: 0 bis 24 % abgesteckt, ab 25 % Fundament, ab 50 % Rohbau, ab 75 % fast fertig");
}

// Einsturz (A2): ein zerstörtes Gebäude verschwindet aus der Spiellogik sofort, die Animation
// merkt sich Lage und Größe und altert mit der Spielschleife, bis die Trümmer vergangen sind
static void Einsturz(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Einsturz";
    int vorher = verstoesse.Count;
    var w = new Welt();
    var liste = (System.Collections.IList)w.Get("_collapses");
    var platz = w.FreieKachel(w.Dorfbewohner().First(), 6);
    var haus = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Haus", 1, 2);
    w.Call("DestroyBuilding", haus);
    if (w.Map.Buildings.Contains(haus))
        verstoesse.Add($"{wer}: das zerstörte Haus steht noch auf der Karte");
    if (liste.Count != 1)
    {
        verstoesse.Add($"{wer}: nach DestroyBuilding {liste.Count} Einstürze, erwartet einer");
        return;
    }
    var c = liste[0];
    var lage = ((int)Feldwert(c, "X"), (int)Feldwert(c, "Y"), (int)Feldwert(c, "Width"), (int)Feldwert(c, "Height"));
    if (lage != (haus.X, haus.Y, haus.Width, haus.Height) || (float)Feldwert(c, "Age") != 0f)
        verstoesse.Add($"{wer}: Einsturz bei {lage}, Alter {Feldwert(c, "Age")}, erwartet ({haus.X}, {haus.Y}, {haus.Width}, {haus.Height}) und 0");
    // Die Spielschleife lässt ihn altern
    w.LaufeBis(() => false, 2f);
    float alter = (float)Feldwert(c, "Age");
    if (Math.Abs(alter - 2f) > 0.05f)
        verstoesse.Add($"{wer}: nach 2 s Spielschleife ist der Einsturz {alter:0.00} s alt");
    // Trümmer bleiben RUBBLE_SECONDS, dann ist er fort
    float trummer = (float)typeof(RTSGameplayScreen).GetField("RUBBLE_SECONDS", BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
    w.Call("UpdateEffects", trummer - 2f - 0.5f);
    if (liste.Count != 1)
        verstoesse.Add($"{wer}: die Trümmer verschwanden vor {trummer} s");
    w.Call("UpdateEffects", 1f);
    if (liste.Count != 0)
        verstoesse.Add($"{wer}: nach {trummer} s liegen die Trümmer noch");
    // Eine Mauer stürzt genauso ein, zwei Gebäude zugleich je für sich
    var a = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Haus", 1, 2);
    var b2 = w.Map.AddBuilding((int)platz.X + 3, (int)platz.Y, "Palisadenmauer", 1, 1);
    w.Call("DestroyBuilding", a);
    w.Call("UpdateEffects", 1f);
    w.Call("DestroyBuilding", b2);
    if (liste.Count != 2 || (float)Feldwert(liste[0], "Age") != 1f || (float)Feldwert(liste[1], "Age") != 0f)
        verstoesse.Add($"{wer}: zwei Einstürze nacheinander: {liste.Count} in der Liste, Alter falsch");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Haus sofort fort, Einsturz an seiner Stelle, altert mit der Spielschleife, Trümmer {trummer} s");
}

// Pfeile (A3): ein Bogenschütze lässt jeden Pfeil so los, dass er genau beim Treffer ankommt;
// Nahkämpfer schießen keine Pfeile
static void Pfeile(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Pfeile";
    int vorher = verstoesse.Count;
    var typ = typeof(RTSGameplayScreen);
    var flug = typ.GetMethod("ArrowFlight", BindingFlags.NonPublic | BindingFlags.Static);
    foreach (var (d, soll) in new[] { (0f, 0.15f), (26f, 0.15f), (130f, 0.5f), (156f, 0.6f), (2000f, 0.6f) })
    {
        float ist = (float)flug.Invoke(null, new object[] { d });
        if (Math.Abs(ist - soll) > 0.001f)
            verstoesse.Add($"{wer}: Flugzeit über {d} px {ist:0.000} s, erwartet {soll:0.000}");
    }
    // Die Flugbahn: von From nach To im Bogen, auf halber Strecke ARROW_ARC · Strecke über der Linie
    var pfeilTyp = typ.GetNestedType("Arrow", BindingFlags.NonPublic);
    var probe = Activator.CreateInstance(pfeilTyp, true);
    pfeilTyp.GetField("From").SetValue(probe, new Vector2(100f, 200f));
    pfeilTyp.GetField("To").SetValue(probe, new Vector2(200f, 200f));
    pfeilTyp.GetField("Flight").SetValue(probe, 0.4f);
    var bahn = typ.GetMethod("ArrowAt", BindingFlags.NonPublic | BindingFlags.Static);
    (Vector2 P, float Winkel) wo(float t)
    {
        var args = new object[] { probe, t, 0f };
        var p = (Vector2)bahn.Invoke(null, args);
        return (p, (float)args[2]);
    }
    var (start, w0) = wo(0f);
    var (mitte, wm) = wo(0.2f);
    var (ende, we) = wo(0.4f);
    var (danach, _) = wo(1f);
    if (Vector2.Distance(start, new Vector2(100f, 200f)) > 0.01f || Vector2.Distance(ende, new Vector2(200f, 200f)) > 0.01f
        || Vector2.Distance(danach, ende) > 0.01f)
        verstoesse.Add($"{wer}: Flugbahn beginnt bei {start}, endet bei {ende} (danach {danach}), erwartet (100, 200) und (200, 200)");
    if (Vector2.Distance(mitte, new Vector2(150f, 200f - 22f)) > 0.01f)
        verstoesse.Add($"{wer}: auf halber Strecke bei {mitte}, erwartet (150, 178) - 22 px über der Linie");
    if (!(w0 < -0.5f && Math.Abs(wm) < 0.01f && we > 0.5f))
        verstoesse.Add($"{wer}: der Pfeil zeigt erst {w0:0.00}, oben {wm:0.00}, am Ende {we:0.00} - erwartet steigend, waagrecht, fallend");

    // Zweikampf: Bogenschütze gegen Miliz auf drei Kacheln
    var w = new Welt();
    var pfeile = (System.Collections.IList)w.Get("_arrows");
    var platz = w.FreieKachel(w.Dorfbewohner().First(), 4);
    var schuetze = NeueEinheit(w, UnitType.Archer, 0, platz);
    var gegner = NeueEinheit(w, UnitType.Militia, 1, platz);
    gegner.Position = schuetze.Position - new Vector2(3f * 32f, 0f);
    w.Call("AttackUnit", new List<Unit> { schuetze }, gegner);
    var unterwegs = new Dictionary<object, int>();
    var landungen = new List<int>();
    var treffer = new List<int>();
    int bild = 0, lp = gegner.Health, abseits = 0, losgelassen = 0;
    w.LaufeBis(() =>
    {
        bild++;
        foreach (var a in pfeile.Cast<object>().Where(a => !unterwegs.ContainsKey(a)).ToList())
        {
            unterwegs[a] = bild;
            losgelassen++;
            var von = (Vector2)Feldwert(a, "From");
            var nach = (Vector2)Feldwert(a, "To");
            if (Vector2.Distance(von, schuetze.Position) > 16f || Vector2.Distance(nach, gegner.Position) > 16f)
                abseits++;
        }
        foreach (var a in unterwegs.Keys.Where(a => !pfeile.Contains(a)).ToList())
        {
            landungen.Add(bild);
            unterwegs.Remove(a);
        }
        if (gegner.Health < lp)
            treffer.Add(bild);
        lp = gegner.Health;
        return gegner.State == UnitState.Dead || !w.Map.Units.Contains(gegner) || treffer.Count >= 4;
    }, 40f);
    if (treffer.Count < 2)
        verstoesse.Add($"{wer}: Testaufbau - nur {treffer.Count} Treffer in 40 s");
    if (losgelassen < treffer.Count)
        verstoesse.Add($"{wer}: {treffer.Count} Treffer, aber nur {losgelassen} Pfeile");
    foreach (var t in treffer)
        if (!landungen.Any(l => Math.Abs(l - t) <= 1))
        {
            verstoesse.Add($"{wer}: Treffer in Bild {t} ohne Pfeil, der dort landet (Landungen {string.Join(", ", landungen)})");
            break;
        }
    if (abseits > 0)
        verstoesse.Add($"{wer}: {abseits} Pfeile nicht vom Schützen zum Gegner");

    // Ein Nahkämpfer schießt nicht
    var w2 = new Welt();
    var pfeile2 = (System.Collections.IList)w2.Get("_arrows");
    var platz2 = w2.FreieKachel(w2.Dorfbewohner().First(), 4);
    var miliz = NeueEinheit(w2, UnitType.Militia, 0, platz2);
    var opfer = NeueEinheit(w2, UnitType.Villager, 1, platz2);
    opfer.Position = miliz.Position - new Vector2(32f, 0f);
    w2.Call("AttackUnit", new List<Unit> { miliz }, opfer);
    int nahPfeile = 0;
    w2.LaufeBis(() => { nahPfeile = Math.Max(nahPfeile, pfeile2.Count); return opfer.Health < opfer.MaxHealth / 2; }, 20f);
    if (nahPfeile > 0)
        verstoesse.Add($"{wer}: eine Miliz hat {nahPfeile} Pfeile geschossen");

    // Auf ein Gebäude: der Pfeil fliegt in seine Mitte und landet beim Treffer
    var w3 = new Welt();
    var pfeile3 = (System.Collections.IList)w3.Get("_arrows");
    var platz3 = w3.FreieKachel(w3.Dorfbewohner().First(), 6);
    var haus = w3.Map.AddBuilding((int)platz3.X, (int)platz3.Y, "Haus", 1, 2);
    var schuetze3 = NeueEinheit(w3, UnitType.Archer, 0, new Vector2(platz3.X - 3, platz3.Y + 1));
    w3.Call("AttackBuilding", new List<Unit> { schuetze3 }, haus);
    var mitteHaus = new Vector2((haus.X + 1f) * 32f, (haus.Y + 1f) * 32f - 32f * 0.4f);
    int gebaeudePfeile = 0, falschesZiel = 0, lpHaus = haus.Health;
    var gesehen = new HashSet<object>();
    w3.LaufeBis(() =>
    {
        foreach (var a in pfeile3.Cast<object>().Where(gesehen.Add))
        {
            gebaeudePfeile++;
            if (Vector2.Distance((Vector2)Feldwert(a, "To"), mitteHaus) > 1f)
                falschesZiel++;
        }
        return gebaeudePfeile >= 2 && haus.Health < lpHaus;
    }, 30f);
    if (gebaeudePfeile == 0)
        verstoesse.Add($"{wer}: auf das Haus flog kein Pfeil ({schuetze3.State})");
    if (falschesZiel > 0)
        verstoesse.Add($"{wer}: {falschesZiel} Pfeile nicht auf die Mitte des Hauses");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Flugzeit und Bahn, {losgelassen} Pfeile im Zweikampf landen mit den {treffer.Count} Treffern, "
                          + $"Miliz ohne Pfeile, {gebaeudePfeile} Pfeile aufs Haus");
}

// Fester Boden (G15). Der Nutzer (2026-10-17): „gras und pfäde dürfen sich nicht mehr ändern
// wenn sie mal erstelt sind. Über gras kann nur noch der Pfad oder Strassen gelegt werden. Aber
// Gras in ein anderes Gras nicht“. Das hohe Gras (TallGrassAt) und das Lebendbild des
// Bodenshaders (UpdateGroundMemory) bleiben, während Tiere wandern, Tritte vergehen, Bäume
// fallen und Minen leer werden; nur ein Pfad, ein Weg, ein Gebäude oder ein Feld legt sich darüber.
static void FesterBoden(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Fester Boden";
    int vorher = verstoesse.Count;
    var w = new Welt();
    int breite = w.Map.Width, hoehe = w.Map.Height;
    float hoch(int x, int y) => (float)w.Call("TallGrassAt", x, y);
    Color[] bild()
    {
        w.Call("UpdateGroundMemory", true);
        return (Color[])((Color[])w.Get("_groundLiveData")).Clone();
    }
    int idx(int x, int y) => y * breite + x;
    bool bedeckt(Tile t) => t.IsPath || t.RoadLevel >= 1 || !string.IsNullOrEmpty(t.Building) || t.Farm;

    // 1) Zwei Minuten Spiel: die Tiere wandern, auch durchs hohe Gras - der Boden bleibt
    var anfang = bild();
    var tiereVorher = new HashSet<(int, int)>();
    for (int x = 0; x < breite; x++)
        for (int y = 0; y < hoehe; y++)
            if (w.Map.GetTile(x, y).Food.IsWild())
                tiereVorher.Add((x, y));
    w.LaufeBis(() => false, 120f);
    var danach = bild();
    int gewandert = 0, geaendert = 0;
    (int, int)? erstes = null;
    for (int x = 0; x < breite; x++)
        for (int y = 0; y < hoehe; y++)
        {
            var t = w.Map.GetTile(x, y);
            if (t.Food.IsWild() != tiereVorher.Contains((x, y)))
                gewandert++;
            if (bedeckt(t))
                continue;
            var a = anfang[idx(x, y)];
            var b = danach[idx(x, y)];
            if (a.R != b.R || a.G != b.G || a.A != b.A)
            {
                geaendert++;
                erstes ??= (x, y);
            }
        }
    if (gewandert == 0)
        verstoesse.Add($"{wer}: in zwei Minuten ist kein Tier gewandert - Prüfung ohne Wirkung");
    if (geaendert > 0)
        verstoesse.Add($"{wer}: in zwei Minuten änderte sich der Boden auf {geaendert} Kacheln ohne Pfad, Weg, Gebäude oder Feld, "
                       + $"etwa bei {erstes} (Tiere wanderten auf {gewandert} Kacheln)");

    // Wiesenkacheln mit hohem Gras, frei von allem, was darüberliegt
    var frei = new List<(int X, int Y)>();
    for (int x = 2; x < breite - 2; x++)
        for (int y = 2; y < hoehe - 2; y++)
        {
            var t = w.Map.GetTile(x, y);
            if (t.Type == TileType.Grassland && t.GroundType == TileType.Grassland && !bedeckt(t) && t.Wear <= 0f
                && t.Food == FoodSource.None && t.ResourceType == null && hoch(x, y) > 0.3f)
                frei.Add((x, y));
        }
    if (frei.Count < 4)
    {
        Console.WriteLine($"      {wer}: zu wenig hohes Gras auf der Karte - Teil 2 bis 4 übersprungen");
    }
    else
    {
        // 2) Fünf Tritte ändern nichts, der sechste legt einen Pfad darüber, und der bleibt
        var (px, py) = frei[0];
        float grasVorher = hoch(px, py);
        var farbeVorher = bild()[idx(px, py)];
        for (int i = 0; i < 5; i++)
            w.Map.Trample(px, py);
        var fuenf = bild()[idx(px, py)];
        if (Math.Abs(hoch(px, py) - grasVorher) > 0.001f || fuenf != farbeVorher)
            verstoesse.Add($"{wer}: fünf Tritte bei ({px},{py}) änderten das Gras ({grasVorher:0.00} -> {hoch(px, py):0.00}, {farbeVorher} -> {fuenf})");
        w.Map.Trample(px, py);
        var pfad = bild()[idx(px, py)];
        if (hoch(px, py) > 0.001f || pfad.R < 250)
            verstoesse.Add($"{wer}: nach sechs Tritten bei ({px},{py}) kein Pfad (Gras {hoch(px, py):0.00}, rot {pfad.R})");
        w.Map.RegrowGrass(TileMap.WEAR_REGROW_SECONDS * 10f);
        var spaeter = bild()[idx(px, py)];
        if (spaeter != pfad)
            verstoesse.Add($"{wer}: der Pfad bei ({px},{py}) wuchs wieder zu ({pfad} -> {spaeter})");

        // 3) Ein Tier im hohen Gras drückt es nicht weg
        var (tx, ty) = frei[1];
        float grasTier = hoch(tx, ty);
        var farbeTier = bild()[idx(tx, ty)];
        w.Map.GetTile(tx, ty).Food = FoodSource.Sheep;
        if (Math.Abs(hoch(tx, ty) - grasTier) > 0.001f || bild()[idx(tx, ty)] != farbeTier)
            verstoesse.Add($"{wer}: ein Schaf bei ({tx},{ty}) drückte das hohe Gras weg ({grasTier:0.00} -> {hoch(tx, ty):0.00})");
        w.Map.GetTile(tx, ty).Food = FoodSource.None;

        // 4) Ein Weg legt sich über das Gras, auch über einen Pfad - darunter nur niedergetretenes
        //    Gras (rot 0,35), die Kante zeichnet der Weg selbst (C16)
        var (sx, sy) = frei[2];
        w.Map.GetTile(sx, sy).RoadLevel = 1;
        if (hoch(sx, sy) > 0.001f || bild()[idx(sx, sy)].R != 0)
            verstoesse.Add($"{wer}: unter dem Weg bei ({sx},{sy}) wächst noch hohes Gras ({hoch(sx, sy):0.00}) oder steht ein Pfad");
        for (int i = 0; i < 6; i++)
            w.Map.Trample(sx, sy);
        byte unterWeg = bild()[idx(sx, sy)].R;
        if (unterWeg < 80 || unterWeg > 92)
            verstoesse.Add($"{wer}: unter dem Weg mit Pfad bei ({sx},{sy}) rot {unterWeg}, erwartet 0,35 (~89)");

        // 5) Ein Beerenbusch verdeckt das hohe Gras
        var (bx, by) = frei[3];
        w.Map.GetTile(bx, by).Food = FoodSource.Berries;
        if (hoch(bx, by) > 0.001f)
            verstoesse.Add($"{wer}: unter dem Beerenbusch bei ({bx},{by}) wächst hohes Gras ({hoch(bx, by):0.00})");
        w.Map.GetTile(bx, by).Food = FoodSource.None;
    }

    // 6) Ein gefällter Baum und eine leere Mine lassen den Boden, wie er war
    var vorFall = bild();
    Tile wald = null, mine = null;
    for (int x = 0; x < breite && (wald == null || mine == null); x++)
        for (int y = 0; y < hoehe; y++)
        {
            var t = w.Map.GetTile(x, y);
            if (wald == null && t.Type == TileType.Forest)
                wald = t;
            else if (mine == null && t.Type is TileType.Mountain or TileType.GoldMine)
                mine = t;
        }
    foreach (var quelle in new[] { wald, mine })
    {
        if (quelle == null)
            continue;
        var art = quelle.Type;
        w.Map.ClearResource(quelle);
        var nach = bild()[idx(quelle.X, quelle.Y)];
        if (quelle.Type != TileType.Grassland)
            verstoesse.Add($"{wer}: {art} bei ({quelle.X},{quelle.Y}) wurde nach dem Abbau nicht Wiese - Prüfung ohne Wirkung");
        else if (nach != vorFall[idx(quelle.X, quelle.Y)] || hoch(quelle.X, quelle.Y) > 0.001f)
            verstoesse.Add($"{wer}: nach dem Abbau von {art} bei ({quelle.X},{quelle.Y}) änderte sich der Boden "
                           + $"({vorFall[idx(quelle.X, quelle.Y)]} -> {nach}, hohes Gras {hoch(quelle.X, quelle.Y):0.00})");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: zwei Minuten mit Tieren ({gewandert} Kacheln gewandert) ohne Änderung, fünf Tritte "
                          + "unsichtbar, der sechste ein Pfad, der bleibt, Schaf und Abbau ändern nichts, Weg und Beerenbusch verdecken das Gras");
}

// Wegkarte (C16): welche Kacheln der Wege-Shader als Weg zeichnet (RoadDrawnAt) und wie
// RoadDrawData schräge Lücken auffüllt - der Shader verbindet nur gerade Nachbarn.
static void Wegkarte(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Wegkarte";
    int vorher = verstoesse.Count;
    var w = new Welt();
    // Ein freier 5x5-Fleck Wiese
    (int X, int Y)? fleck = null;
    for (int y = 2; fleck == null && y < w.Map.Height - 7; y++)
        for (int x = 2; fleck == null && x < w.Map.Width - 7; x++)
        {
            bool frei = true;
            for (int dx = 0; frei && dx < 5; dx++)
                for (int dy = 0; frei && dy < 5; dy++)
                {
                    var t = w.Map.GetTile(x + dx, y + dy);
                    frei = t.Type == TileType.Grassland && string.IsNullOrEmpty(t.Building) && !t.Farm
                           && t.ResourceType == null && t.Food == FoodSource.None && t.RoadLevel == 0;
                }
            if (frei)
                fleck = (x, y);
        }
    if (fleck is not { } f)
    {
        Console.WriteLine($"      {wer}: kein freier Fleck - übersprungen");
        return;
    }
    bool gezeichnet(int dx, int dy) => (bool)w.Call("RoadDrawnAt", f.X + dx, f.Y + dy);
    AgeOfEvolutions.Core.Data.Tile kachel(int dx, int dy) => w.Map.GetTile(f.X + dx, f.Y + dy);

    // RoadDrawnAt: Weg auf Wiese ja; ein Schaf darauf stört nicht; Gold, Feld und Gebäude nicht
    kachel(0, 0).RoadLevel = 1;
    if (!gezeichnet(0, 0))
        verstoesse.Add($"{wer}: ein Weg auf der Wiese wird nicht gezeichnet");
    if (gezeichnet(1, 0))
        verstoesse.Add($"{wer}: Wiese ohne Weg-Stufe wird als Weg gezeichnet");
    kachel(0, 0).Food = FoodSource.Sheep;
    kachel(0, 0).ResourceType = Resource.Food;
    if (!gezeichnet(0, 0))
        verstoesse.Add($"{wer}: ein Schaf auf dem Weg unterbricht ihn");
    kachel(0, 0).Food = FoodSource.None;
    kachel(0, 0).ResourceType = null;
    kachel(4, 4).RoadLevel = 1;
    kachel(4, 4).ResourceType = Resource.Gold;
    if (gezeichnet(4, 4))
        verstoesse.Add($"{wer}: auf einer Goldkachel wird ein Weg gezeichnet");
    kachel(4, 4).ResourceType = null;
    kachel(4, 4).Farm = true;
    if (gezeichnet(4, 4))
        verstoesse.Add($"{wer}: auf einem Feld wird ein Weg gezeichnet");
    kachel(4, 4).Farm = false;
    kachel(4, 4).RoadLevel = 0;

    // RoadDrawData: (0, 0) und (1, 1) nur schräg benachbart - (1, 0) wird aufgefüllt;
    // (2, 3) und (3, 2) schräg nach rechts oben, (3, 3) ist ein Gebäude - dann (2, 2)
    kachel(1, 1).RoadLevel = 1;
    kachel(2, 3).RoadLevel = 1;
    kachel(3, 2).RoadLevel = 1;
    w.Map.AddBuilding(f.X + 3, f.Y + 3, "Palisadenmauer", 0, 1);
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    w.Call("UpdateGroundMemory", true);
    var gesehen = (Color[])w.Get("_roadMapData");
    var zeichnen = (Color[])w.Call("RoadDrawData");
    int i(int dx, int dy) => (f.Y + dy) * w.Map.Width + f.X + dx;
    if (gesehen[i(1, 0)].R > 10 || gesehen[i(3, 3)].G < 250)
        verstoesse.Add($"{wer}: _roadMapData stimmt nicht (Lücke {gesehen[i(1, 0)].R}, Gebäude {gesehen[i(3, 3)].G})");
    if (zeichnen[i(1, 0)].R < 250 || zeichnen[i(0, 1)].R > 10)
        verstoesse.Add($"{wer}: die schräge Lücke (0,0)-(1,1) ist nicht über (1,0) gefüllt");
    if (zeichnen[i(2, 2)].R < 250 || zeichnen[i(3, 3)].R > 10)
        verstoesse.Add($"{wer}: die Lücke (2,3)-(3,2) ist nicht über (2,2) gefüllt (dort, wo kein Gebäude steht)");
    if (gesehen[i(2, 2)].R > 10)
        verstoesse.Add($"{wer}: RoadDrawData hat das Gesehene (_roadMapData) verändert");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Wiese ja, Schaf stört nicht, Gold und Feld nicht, schräge Lücken über die freie Kachel gefüllt");
}

// Wege von Gebäuden (C16): ab der Feudalzeit führt von jedem fertigen Gebäude ein
// Weg zum Wegenetz oder zum Stadtzentrum. In der Dunklen Zeit nicht; beim Aufstieg
// bekommen die schon stehenden Gebäude ihren Weg, danach jedes neu fertige. Mauern
// bleiben ohne Weg, und wer selbst noch in der Dunklen Zeit ist, bekommt keinen -
// auch wenn der andere Spieler weiter ist. Gebaut wird ohne Laufweg (der Erbauer
// steht schon an der Baustelle), damit kein Trampelpfad das Ergebnis verfälscht.
static void GebaeudeWege(int karte, List<string> verstoesse)
{
    var wer = $"Karte {karte}, Gebäudewege";
    int vorher = verstoesse.Count;
    var w = new Welt();
    var tc1 = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
    var tc2 = w.Map.Buildings.First(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);

    int wege()
    {
        int n = 0;
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
                if (w.Map.GetTile(x, y).RoadLevel >= 1)
                    n++;
        return n;
    }

    bool imRing(Building g, int x, int y)
        => x >= g.X - 1 && x <= g.X + g.Width && y >= g.Y - 1 && y <= g.Y + g.Height
           && !(x >= g.X && x < g.X + g.Width && y >= g.Y && y < g.Y + g.Height);

    // Ob Wegkacheln (vier Nachbarn) vom Ring um von bis zum Ring um bis reichen
    bool kette(Building von, Building bis)
    {
        var offen = new Queue<(int X, int Y)>();
        var gesehen = new HashSet<(int, int)>();
        for (int x = von.X - 1; x <= von.X + von.Width; x++)
            for (int y = von.Y - 1; y <= von.Y + von.Height; y++)
                if (imRing(von, x, y) && w.Map.GetTile(x, y) is { RoadLevel: >= 1 } && gesehen.Add((x, y)))
                    offen.Enqueue((x, y));
        while (offen.Count > 0)
        {
            var (x, y) = offen.Dequeue();
            if (imRing(bis, x, y))
                return true;
            foreach (var n in new[] { (x + 1, y), (x - 1, y), (x, y + 1), (x, y - 1) })
                if (w.Map.GetTile(n.Item1, n.Item2) is { RoadLevel: >= 1 } && gesehen.Add(n))
                    offen.Enqueue(n);
        }
        return false;
    }

    bool ringOhneWeg(Building g)
    {
        for (int x = g.X - 1; x <= g.X + g.Width; x++)
            for (int y = g.Y - 1; y <= g.Y + g.Height; y++)
                if (imRing(g, x, y) && w.Map.GetTile(x, y) is { RoadLevel: >= 1 })
                    return false;
        return true;
    }

    // Wie viele Kacheln der kürzeste mögliche Weg eines Gebäudes auf (x, y) hat: von der Reihe
    // unter der Grundfläche über Kacheln, die einen Weg tragen dürfen, bis an das Stadtzentrum
    // tc - wie TileMap.ConnectBuilding (seit C16c ohne Höchstlänge); null, wenn es keinen gibt.
    // Ohne das wählte die Prüfung auf mancher Karte einen Platz hinter Wald oder Gold, an dem es
    // zu Recht keinen Weg gibt.
    int? weglaenge(Building tc, int x, int y, int groesse)
    {
        bool fuss(int fx, int fy) => fx >= x && fx < x + groesse && fy >= y && fy < y + groesse;
        var laenge = new Dictionary<(int, int), int>();
        var offen = new Queue<(int X, int Y)>();
        for (int fx = x; fx < x + groesse; fx++)
            if (w.Map.CanCarryRoad(fx, y + groesse))
            {
                laenge[(fx, y + groesse)] = 1;
                offen.Enqueue((fx, y + groesse));
            }
        while (offen.Count > 0)
        {
            var (cx, cy) = offen.Dequeue();
            if (imRing(tc, cx, cy))
                return laenge[(cx, cy)];
            foreach (var n in new[] { (cx + 1, cy), (cx - 1, cy), (cx, cy + 1), (cx, cy - 1) })
                if (!laenge.ContainsKey(n) && !fuss(n.Item1, n.Item2) && w.Map.CanCarryRoad(n.Item1, n.Item2))
                {
                    laenge[n] = laenge[(cx, cy)] + 1;
                    offen.Enqueue(n);
                }
        }
        return null;
    }

    bool anschliessbar(Building tc, int x, int y, int groesse) => weglaenge(tc, x, y, groesse) != null;

    // Ein Bauplatz etwa abstand Kacheln von der Mitte des Stadtzentrums, auf und um
    // den noch kein Weg liegt, mit freier Kachel unter der Vorderseite und möglichem Anschluss
    Vector2? platz(Building tc, int groesse, float abstand)
    {
        var mitte = new Vector2(tc.X + tc.Width / 2f, tc.Y + tc.Height / 2f);
        Vector2? bester = null;
        float besterFehler = float.MaxValue;
        for (int x = 1; x + groesse < w.Map.Width - 1; x++)
            for (int y = 1; y + groesse < w.Map.Height - 1; y++)
            {
                if (!w.Map.CanPlaceBuilding(x, y, groesse))
                    continue;
                bool frei = w.Map.CanCarryRoad(x, y + groesse);
                for (int bx = x - 1; frei && bx <= x + groesse; bx++)
                    for (int by = y - 1; frei && by <= y + groesse; by++)
                        frei = w.Map.GetTile(bx, by) is { RoadLevel: 0 }
                               && !w.Map.Units.Any(u => w.Map.WorldToGrid(u.Position) == new Vector2(bx, by));
                if (!frei)
                    continue;
                float fehler = Math.Abs(Vector2.Distance(new Vector2(x + groesse / 2f, y + groesse / 2f), mitte) - abstand);
                if (fehler < besterFehler && anschliessbar(tc, x, y, groesse))
                {
                    besterFehler = fehler;
                    bester = new Vector2(x, y);
                }
            }
        return bester;
    }

    Building fertig(BuildingType typ, string name, int owner, Vector2 p, int groesse)
    {
        var b = w.Map.AddBuilding((int)p.X, (int)p.Y, name, owner, groesse);
        b.Construction = new AoE.Core.Economy.Construction(BuildingRules.BuildSecondsOf(typ));
        var bauer = w.Map.Units.First(u => u.OwnerId == owner && u.Core is CoreVillager && u.BuildSite == null);
        bauer.BuildSite = b;
        bauer.State = UnitState.Building;
        bauer.Path?.Clear();
        w.Call("UpdateConstruction", 10000f);
        if (!b.IsComplete)
            verstoesse.Add($"{wer}: {name} von Spieler {owner + 1} wurde im Test nicht fertig");
        return b;
    }

    void aufstieg(Player p, Age ziel)
    {
        foreach (var r in new[] { Resource.Wood, Resource.Stone, Resource.Gold, Resource.Food })
            p.Resources.Add(r, 20000);
        p.Ages.TryStart(p.Resources);
        w.Call("UpdateAges", 2000f);
        if (p.Ages.Current != ziel)
            verstoesse.Add($"{wer}: Aufstieg auf {ziel} im Test gescheitert (jetzt {p.Ages.Current})");
    }

    // 1) Dunkle Zeit: ein fertiges Haus bekommt keinen Weg
    if (platz(tc1, 2, 7) is not { } pA)
    {
        verstoesse.Add($"{wer}: kein Bauplatz für das erste Haus");
        return;
    }
    var hausA = fertig(BuildingType.House, "Haus", 0, pA, 2);
    if (wege() != 0)
        verstoesse.Add($"{wer}: Dunkle Zeit - nach dem Haus liegen {wege()} Wegkacheln, erwartet keine");

    // 2) Aufstieg auf die Feudalzeit: das Haus aus der Dunklen Zeit bekommt seinen Weg
    aufstieg(w.P1, Age.Feudal);
    if (verstoesse.Count > vorher)
        return;
    int nachAufstieg = wege();
    if (!kette(hausA, tc1))
        verstoesse.Add($"{wer}: nach dem Aufstieg führt vom Haus ({hausA.X},{hausA.Y}) kein Weg zum Stadtzentrum ({nachAufstieg} Wegkacheln)");
    // Noch lag kein Weg: genau der kürzeste Weg vom Haus, und das Stadtzentrum bekommt keinen
    if (weglaenge(tc1, hausA.X, hausA.Y, 2) is { } kuerzester && nachAufstieg != kuerzester)
        verstoesse.Add($"{wer}: nach dem Aufstieg {nachAufstieg} Wegkacheln, erwartet {kuerzester} (der kürzeste Weg vom Haus)");

    // 3) Feudalzeit: ein neues Haus bekommt beim Fertigwerden seinen Weg
    if (platz(tc1, 2, 11) is not { } pB)
    {
        verstoesse.Add($"{wer}: kein Bauplatz für das zweite Haus");
        return;
    }
    var hausB = fertig(BuildingType.House, "Haus", 0, pB, 2);
    int nachB = wege();
    if (nachB <= nachAufstieg)
        verstoesse.Add($"{wer}: Feudalzeit - das neue Haus ({hausB.X},{hausB.Y}) hat keinen Weg bekommen");
    else if (!kette(hausB, tc1))
        verstoesse.Add($"{wer}: Feudalzeit - vom neuen Haus ({hausB.X},{hausB.Y}) führt kein Weg zum Stadtzentrum");

    // 3b) Auch ein Haus weit draußen bekommt seinen Weg bis ans Stadtzentrum (C16c - bis dahin
    //     endete die Suche nach 30 Kacheln)
    int weit = 0;
    if (platz(tc1, 2, 26) is { } pD)
    {
        var hausD = fertig(BuildingType.House, "Haus", 0, pD, 2);
        weit = wege() - nachB;
        if (!kette(hausD, tc1))
            verstoesse.Add($"{wer}: vom Haus weit draußen ({hausD.X},{hausD.Y}) führt kein Weg zum Stadtzentrum ({weit} neue Wegkacheln)");
    }
    int nachD = wege();

    // 4) Eine Mauer bekommt keinen Weg
    if (platz(tc1, 1, 9) is { } pM)
    {
        fertig(BuildingType.PalisadeWall, "Palisadenmauer", 0, pM, 1);
        if (wege() != nachD)
            verstoesse.Add($"{wer}: die Palisadenmauer hat {wege() - nachD} Wegkacheln bekommen, erwartet keine");
    }

    // 5) Der Gegner ist noch in der Dunklen Zeit: sein Haus bleibt ohne Weg ...
    if (platz(tc2, 2, 7) is not { } pC)
    {
        verstoesse.Add($"{wer}: kein Bauplatz für das Haus des Gegners");
        return;
    }
    int vorC = wege();
    var hausC = fertig(BuildingType.House, "Haus", 1, pC, 2);
    if (wege() != vorC || !ringOhneWeg(hausC))
        verstoesse.Add($"{wer}: Gegner in der Dunklen Zeit - sein Haus hat einen Weg bekommen ({wege() - vorC} Kacheln)");

    // ... bis er selbst in die Feudalzeit aufsteigt
    aufstieg(w.P2, Age.Feudal);
    if (verstoesse.Count == vorher && !kette(hausC, tc2))
        verstoesse.Add($"{wer}: nach dem Aufstieg des Gegners führt von seinem Haus kein Weg zu seinem Stadtzentrum");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Dunkle Zeit kein Weg, Aufstieg {nachAufstieg} Kacheln, neues Haus +{nachB - nachAufstieg}, "
                          + $"weit draußen +{weit}, Mauer ohne Weg, Gegner erst nach seinem Aufstieg");
}

// Prüft, dass eine Reihe frei ist: Gras oder Sand, keine Ressource, kein
// Gebäude, kein Feld, kein Weg — begehbar.
static bool ReiheFrei(TileMap map, int x, int y, int laenge)
{
    if (y < 0 || y >= map.Height || x < 0 || x + laenge > map.Width)
        return false;
    for (int i = 0; i < laenge; i++)
    {
        var t = map.GetTile(x + i, y);
        if (t == null || t.RoadLevel >= 1
            || !string.IsNullOrEmpty(t.Building) || t.Farm
            || t.ResourceType != null || t.Food != FoodSource.None
            || t.Type is not (TileType.Grassland or TileType.Sand))
            return false;
    }
    return true;
}

// Reparatur (C15): beschädigte Gebäude lassen sich wieder herstellen —
// Kosten und Dauer fallen anteilig zum fehlenden Schadensvolumen: zu 50 %
// angeschlagen kostet und dauert die Reparatur rund die Hälfte. Mehr
// Arbeiter beschleunigen (AoE-II-Formel), ein unverletztes Gebäude wird
// weder repariert noch bezahlt.
static void Reparatur(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Reparatur";
    var w = new Welt();
    int vorher = verstoesse.Count;
    var arbeiter = w.Dorfbewohner().ToList();
    if (arbeiter.Count < 3)
    {
        verstoesse.Add($"{wer}: weniger als 3 Dorfbewohner auf der Karte");
        return;
    }

    // Haus in der Nähe des Dorfs (1 Erbauer), danach zu 50 % beschädigt.
    w.Waehle(new List<Unit> { arbeiter[0] });
    var platz = w.Bauplatz(BuildingType.House, arbeiter[0], 3);
    w.Call("PlaceBuilding", BuildingType.House, platz);
    var haus = w.Map.Buildings.Last();
    w.LaufeBis(() => haus.IsComplete, 60f);
    if (!haus.IsComplete)
    {
        verstoesse.Add($"{wer}: das Haus ist nach 60 s nicht fertig");
        return;
    }
    int max = haus.MaxHealth;
    haus.Health = max / 2;
    var erwartet = BuildingRules.RepairCostOf(BuildingType.House, haus.Health, max);

    // 1) Ein Reparateur: anteilige Kosten vor dem Auftrag, anteilige
    //    Dauer bis zum vollen Zustand.
    int holzVorher = w.P1.Resources[Resource.Wood];
    w.Waehle(new List<Unit> { arbeiter[1] });
    w.Linksklick(w.Map.GridToWorld(new Vector2(haus.X + 1, haus.Y + 1)));
    w.LaufeBis(() => false, 0.4f);
    int bezahlt = holzVorher - w.P1.Resources[Resource.Wood];
    if (bezahlt != erwartet[Resource.Wood])
        verstoesse.Add($"{wer}: 50 % Schaden kostet {bezahlt} Holz, erwartet {erwartet[Resource.Wood]} (die Hälfte von {25})");
    if (haus.Repair == null)
    {
        verstoesse.Add($"{wer}: nach dem Klick auf das beschädigte Haus fehlt der Reparatur-Auftrag");
        return;
    }
    if (arbeiter[1].BuildSite != haus)
        verstoesse.Add($"{wer}: der Dorfbewohner wurde nicht zur Reparatur geschickt");
    w.LaufeBis(() => arbeiter[1].BuildSite == haus && arbeiter[1].State == UnitState.Building, 30f);
    float tEins = w.LaufeBis(() => haus.Health >= max, 90f);
    float soll = BuildingRules.RepairSecondsOf(BuildingType.House, max / 2, max);
    if (haus.Health < max)
        verstoesse.Add($"{wer}: nach 90 s Reparatur stehen immer noch {haus.Health}/{max} LP");
    if (Math.Abs(tEins - soll) > 0.2f * soll)
        verstoesse.Add($"{wer}: 1 Reparateur braucht {tEins:0.0} s, erwartet rund {soll:0.0} s (die halbe Bauzeit)");
    w.LaufeBis(() => false, 0.5f);
    if (haus.Repair != null)
        verstoesse.Add($"{wer}: nach der Fertigstellung fehlt der Auftrag");
    if (arbeiter[1].BuildSite == haus)
        verstoesse.Add($"{wer}: nach der Fertigstellung fehlt der Arbeiter");

    // 2) Mehr Reparateure sind schneller (2 + n-2)/3 = (n + 2) / 3 schnell).
    w.Waehle(new List<Unit> { arbeiter[0] });
    var platzB = w.Bauplatz(BuildingType.House, arbeiter[0], 3, nahAn: platz, hoechstens: 8);
    w.Call("PlaceBuilding", BuildingType.House, platzB);
    var hausB = w.Map.Buildings.Last();
    w.LaufeBis(() => hausB.IsComplete, 60f);
    if (!hausB.IsComplete)
    {
        verstoesse.Add($"{wer}: das zweite Haus ist nach 60 s nicht fertig");
        return;
    }
    hausB.Health = max / 2;
    w.Waehle(new List<Unit> { arbeiter[1], arbeiter[2] });
    w.Linksklick(w.Map.GridToWorld(new Vector2(hausB.X + 1, hausB.Y + 1)));
    w.LaufeBis(() => false, 0.1f);   // den Befehl ankommen lassen
    if (hausB.Repair == null)
    {
        verstoesse.Add($"{wer}: beim zweiten Haus fehlt der Reparatur-Auftrag");
        return;
    }
    int reparateure = Math.Max(1, arbeiter.Count(u => u.BuildSite == hausB));
    w.LaufeBis(() => arbeiter.Any(u => u.BuildSite == hausB && u.State == UnitState.Building), 30f);
    float tViele = w.LaufeBis(() => hausB.Health >= max, 90f);
    if (hausB.Health < max)
        verstoesse.Add($"{wer}: das zweite Haus ist nach 90 s nicht repariert");
    float vieleSoll = soll / ((reparateure + 2f) / 3f);
    if (Math.Abs(tViele - vieleSoll) > 0.2f * vieleSoll)
        verstoesse.Add($"{wer}: {reparateure} Reparateure brauchen {tViele:0.0} s, erwartet rund {vieleSoll:0.0} s");
    if (reparateure >= 2 && tViele >= tEins)
        verstoesse.Add($"{wer}: {reparateure} Reparateure ({tViele:0.0} s) sind nicht schneller als einer ({tEins:0.0} s)");

    // 3) Ein unverletztes Gebäude wird weder repariert noch bezahlt.
    int holzVor3 = w.P1.Resources[Resource.Wood];
    w.Waehle(new List<Unit> { arbeiter[2] });
    w.Linksklick(w.Map.GridToWorld(new Vector2(haus.X + 1, haus.Y + 1)));
    w.LaufeBis(() => false, 0.4f);
    if (w.P1.Resources[Resource.Wood] != holzVor3)
        verstoesse.Add($"{wer}: das unverletzte Haus wurde bezahlt ({holzVor3} → {w.P1.Resources[Resource.Wood]})");
    if (haus.Repair != null)
        verstoesse.Add($"{wer}: dem unverletzten Haus fehlt ein Reparatur-Auftrag");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: 50 % Schaden → {bezahlt} Holz (Hälfte) und {tEins:0.0} s mit 1, {tViele:0.0} s mit {reparateure}; unverletzt wird nichts bezahlt");
}

// Neue Gebäude (L2): unter der ersten Tastenreihe liegt eine zweite mit den Gebäuden,
// die das Zeitalter freischaltet - Dunkle Zeit Kaserne und Palisadenmauer, ab der
// Feudalzeit Schießstand, Stall, Schmiede, Markt und Steinmauer, ab der Ritterzeit
// Stadtzentrum, Belagerungswerkstatt, Universität, Kloster und Burg, in der
// Imperialzeit das Wunder. Jedes lässt sich per Taste wählen, kostet laut
// BuildingRules und steht danach als richtiger Kerntyp auf der Karte; nach einem
// Mauerstück bleibt der Setzmodus an, damit man eine Reihe legen kann.
static void Neubauten(int karte, List<string> verstoesse)
{
    const int bw = 2406, bh = 1353;
    string wer = $"Karte {karte}, Neubauten";
    var w = new Welt();
    int vorher = verstoesse.Count;
    w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
    var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
        ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
    foreach (var r in new[] { Resource.Wood, Resource.Stone, Resource.Gold, Resource.Food })
        w.P1.Resources.Add(r, 20000);

    var neu = new (Keys Taste, BuildingType Typ, string Name, Age Ab)[]
    {
        (Keys.K, BuildingType.Barracks, "Kaserne", Age.Dark),
        (Keys.P, BuildingType.PalisadeWall, "Palisadenmauer", Age.Dark),
        (Keys.S, BuildingType.ArcheryRange, "Schießstand", Age.Feudal),
        (Keys.L, BuildingType.Stable, "Stall", Age.Feudal),
        (Keys.E, BuildingType.Blacksmith, "Schmiede", Age.Feudal),
        (Keys.R, BuildingType.Market, "Markt", Age.Feudal),
        (Keys.W, BuildingType.StoneWall, "Steinmauer", Age.Feudal),
        (Keys.Z, BuildingType.TownCenter, "Stadtzentrum", Age.Castle),
        (Keys.X, BuildingType.SiegeWorkshop, "Belagerungswerkstatt", Age.Castle),
        (Keys.U, BuildingType.University, "Universität", Age.Castle),
        (Keys.O, BuildingType.Monastery, "Kloster", Age.Castle),
        (Keys.C, BuildingType.Castle, "Burg", Age.Castle),
        (Keys.N, BuildingType.Wonder, "Wunder", Age.Imperial),
    };
    var arbeiter = w.Dorfbewohner().Take(3).ToList();

    // Ein Tastendruck, wie das Spiel ihn sieht: ein Bild gedrückt, eines losgelassen
    void Taste(Keys k)
    {
        var gt = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60));
        var maus = new MouseState(bw / 2, bh / 2, 0, ButtonState.Released, ButtonState.Released,
                                  ButtonState.Released, ButtonState.Released, ButtonState.Released);
        w.Call("HandleRtsInput", gt, new KeyboardState(k), maus);
        w.Call("HandleRtsInput", gt, new KeyboardState(), maus);
    }

    // Ohne Dorfbewohner gibt es keine Bautasten, also nur die erste Reihe
    w.Waehle(new List<Unit>());
    w.Call("LayoutButtons");
    var ohne = w.Tasten();
    int obenY = ohne[0].Rect.Y;
    if (ohne.Any(t => t.Rect.Y != obenY))
        verstoesse.Add($"{wer}: ohne Auswahl Tasten außerhalb der ersten Reihe ({string.Join(" ", ohne.Select(t => t.Label))})");

    foreach (var age in new[] { Age.Dark, Age.Feudal, Age.Castle, Age.Imperial })
    {
        if (age != Age.Dark)
        {
            ages.TryStart(w.P1.Resources);
            ages.Update(1000f);
        }
        if (ages.Current != age)
        {
            verstoesse.Add($"{wer}: Aufstieg nach {age} gescheitert (jetzt {ages.Current})");
            return;
        }
        w.Waehle(arbeiter);
        w.Call("LayoutButtons");
        var tasten = w.Tasten();
        var oben = tasten.Where(t => t.Rect.Y == obenY).ToList();
        var unten = tasten.Where(t => t.Rect.Y != obenY).ToList();
        var obenSoll = new[] { "H", "M", "F", "B", "G", "T", "." }
            .Where(l => l != "T" || age >= Age.Feudal).ToArray();
        var untenSoll = neu.Where(e => e.Ab <= age).Select(e => e.Taste.ToString()).ToArray();
        if (!oben.Select(t => t.Label).SequenceEqual(obenSoll))
            verstoesse.Add($"{wer}, {age}: erste Reihe [{string.Join(" ", oben.Select(t => t.Label))}], erwartet [{string.Join(" ", obenSoll)}]");
        if (!unten.Select(t => t.Label).SequenceEqual(untenSoll))
            verstoesse.Add($"{wer}, {age}: zweite Reihe [{string.Join(" ", unten.Select(t => t.Label))}], erwartet [{string.Join(" ", untenSoll)}]");
        if (unten.Count == 0 || oben.Count == 0)
            continue;
        int y = unten[0].Rect.Y;
        if (unten.Any(t => t.Rect.Y != y))
            verstoesse.Add($"{wer}, {age}: die zweite Reihe liegt nicht auf einer Höhe");
        if (y < oben.Max(t => t.Rect.Bottom))
            verstoesse.Add($"{wer}, {age}: zweite Reihe (y {y}) nicht unter der ersten (unten {oben.Max(t => t.Rect.Bottom)})");
        if (unten.Max(t => t.Rect.Bottom) > bh)
            verstoesse.Add($"{wer}, {age}: zweite Reihe ragt unten aus dem Fenster");
        if (unten[0].Rect.X != oben[0].Rect.X)
            verstoesse.Add($"{wer}, {age}: zweite Reihe beginnt bei x {unten[0].Rect.X}, die erste bei {oben[0].Rect.X}");
        for (int i = 1; i < unten.Count; i++)
            if (unten[i].Rect.Left < unten[i - 1].Rect.Right)
                verstoesse.Add($"{wer}, {age}: Tasten {unten[i - 1].Label} und {unten[i].Label} überlappen");
        foreach (var e in neu)
        {
            Taste(e.Taste);
            bool an = Equals(w.Get("placing"), e.Typ);
            if (e.Ab <= age && !an)
                verstoesse.Add($"{wer}, {age}: Taste {e.Taste} setzt nicht {e.Name} (Setzmodus {w.Get("placing") ?? "aus"})");
            if (e.Ab > age && w.Get("placing") != null)
                verstoesse.Add($"{wer}, {age}: Taste {e.Taste} - Setzmodus {w.Get("placing")}, {e.Name} gibt es erst ab {e.Ab}");
            if (an)
            {
                Taste(e.Taste);   // dieselbe Taste noch einmal schaltet aus
                if (w.Get("placing") != null)
                    verstoesse.Add($"{wer}, {age}: Taste {e.Taste} zweimal - Setzmodus bleibt an");
            }
            w.Set("placing", null);
        }
        foreach (var t in new[] { "Q", "A", "." })
            if (tasten.Count(x => x.Label == t) > 1)
                verstoesse.Add($"{wer}, {age}: Taste {t} doppelt");
    }

    // Imperialzeit: jedes neue Gebäude setzen. Ohne Nebel - sonst ist das erkundete
    // Startgebiet nach ein paar großen Gebäuden voll
    TileMap.TestNoFog = true;
    try
    {
    foreach (var e in neu)
    {
        // Stadtzentrum: es gibt genau EINES (Spielregel, DefeatRule) — ein
        // zweites lässt sich nicht setzen.
        if (e.Typ == BuildingType.TownCenter)
            continue;
        w.Waehle(arbeiter);
        w.Call("TogglePlacing", e.Typ);
        var kosten = AoE.Core.Economy.BuildingRules.CostOf(e.Typ);
        var vor = kosten.Keys.ToDictionary(r => r, r => w.P1.Resources[r]);
        Vector2 platz;
        try { platz = w.Bauplatz(e.Typ, arbeiter[0], 6); }
        catch (InvalidOperationException) { verstoesse.Add($"{wer}: kein Bauplatz für {e.Name}"); continue; }
        int anzahl = w.Map.Buildings.Count;
        w.Call("PlaceBuilding", e.Typ, platz);
        if (w.Map.Buildings.Count != anzahl + 1)
        {
            verstoesse.Add($"{wer}: {e.Name} nicht gesetzt");
            continue;
        }
        var b = w.Map.Buildings.Last();
        if (b.Core.BuildingType != e.Typ || b.Type != e.Name)
            verstoesse.Add($"{wer}: gesetzt {b.Type}/{b.Core.BuildingType}, erwartet {e.Name}/{e.Typ}");
        int seite = AoE.Core.Economy.BuildingRules.SizeOf(e.Typ);
        if (b.Width != seite || b.Height != seite)
            verstoesse.Add($"{wer}: {e.Name} {b.Width}x{b.Height}, erwartet {seite}x{seite}");
        foreach (var (r, menge) in kosten)
            if (w.P1.Resources[r] != vor[r] - menge)
                verstoesse.Add($"{wer}: {e.Name} - {r} {vor[r]} -> {w.P1.Resources[r]}, erwartet {menge} weniger");
        if (b.IsComplete || b.Construction == null)
            verstoesse.Add($"{wer}: {e.Name} ist keine Baustelle");
        bool mauer = e.Typ is BuildingType.PalisadeWall or BuildingType.StoneWall;
        if (mauer && !Equals(w.Get("placing"), e.Typ))
            verstoesse.Add($"{wer}: nach einem Stück {e.Name} ist der Setzmodus aus - für eine Mauerreihe soll er an bleiben");
        if (!mauer && w.Get("placing") != null)
            verstoesse.Add($"{wer}: nach {e.Name} bleibt der Setzmodus {w.Get("placing")} an");
        w.Set("placing", null);
    }
    }
    finally
    {
        TileMap.TestNoFog = false;
    }

    // Ein Mauerstück wird von einem Arbeiter fertig gebaut
    var w2 = new Welt();
    var maurer = w2.Dorfbewohner().First();
    w2.Waehle(new List<Unit> { maurer });
    var stelle = w2.Bauplatz(BuildingType.PalisadeWall, maurer, 3);
    w2.Call("PlaceBuilding", BuildingType.PalisadeWall, stelle);
    var stueck = w2.Map.Buildings.Last();
    float dauer = w2.LaufeBis(() => stueck.IsComplete, 30f);
    if (stueck.Core.BuildingType != BuildingType.PalisadeWall || !stueck.IsComplete)
        verstoesse.Add($"{wer}: Palisadenstück nach 30 s nicht fertig");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: zweite Reihe je Zeitalter, Tasten, 13 Gebäude gesetzt, Palisade fertig nach {dauer:0.0} s");
}

// Türen (T1): wer abliefert, steht direkt vor der Tür der Abgabestelle - an der
// Unterkante der Grundfläche, in der Spalte der Tür (DoorFraction, am Bild gemessen),
// nicht irgendwo im Ring ums Gebäude oder eine Kachel daneben. Ist die Türkachel
// verstellt, steht er so nah an der Tür wie möglich, mit den Füßen am Gebäude.
static void Tueren(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Türen";
    int vorher = verstoesse.Count;
    const int T = 32;
    var notiz = new List<string>();

    static bool Nimmt(Building g, Resource r) => g.Core.DropOffType switch
    {
        AoE.Core.Entities.ResourceDropOff.TownCenter => true,
        AoE.Core.Entities.ResourceDropOff.Mill => r == Resource.Food,
        AoE.Core.Entities.ResourceDropOff.LumberCamp => r == Resource.Wood,
        AoE.Core.Entities.ResourceDropOff.MiningCamp => r is Resource.Gold or Resource.Stone,
        _ => false,
    };

    static float Abstand(Vector2 p, Building g)
    {
        float dx = Math.Max(Math.Max(g.X * T - p.X, 0), p.X - (g.X + g.Width) * T);
        float dy = Math.Max(Math.Max(g.Y * T - p.Y, 0), p.Y - (g.Y + g.Height) * T);
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    // Wo der Dorfbewohner beim Abliefern steht (sein Ort in dem Bild, in dem die
    // Traglast gutgeschrieben wird) und an welchem Gebäude
    (Vector2 Ort, Building Ziel)? Abgabe(Welt w, Unit u, Resource r, float sekunden)
    {
        int traglast = 0;
        Vector2? ort = null;
        w.LaufeBis(() =>
        {
            int jetzt = u.Job?.Carrying ?? 0;
            if (traglast > 0 && jetzt == 0 && u.Job != null)
                ort = u.Position;
            traglast = jetzt;
            return ort != null;
        }, sekunden);
        if (ort == null)
            return null;
        var ziel = w.Map.Buildings.Where(g => g.OwnerId == 0 && g.IsComplete && Nimmt(g, r))
            .OrderBy(g => Abstand(ort.Value, g)).First();
        return (ort.Value, ziel);
    }

    void Pruefe(Welt w, string fall, (Vector2 Ort, Building Ziel)? ergebnis, string erwartet)
    {
        if (ergebnis is not { } e)
        {
            verstoesse.Add($"{wer}, {fall}: keine Abgabe beobachtet");
            return;
        }
        var b = e.Ziel;
        if (b.Type != erwartet)
            verstoesse.Add($"{wer}, {fall}: abgeliefert an {b.Type}, erwartet {erwartet}");
        float anteil = (float)w.Call("DoorFraction", b);
        float tuerX = (b.X + anteil * b.Width) * T, kante = (b.Y + b.Height) * T;
        int spalte = Math.Clamp((int)(tuerX / T), b.X, b.X + b.Width - 1);
        if (w.Map.IsWalkable(spalte, b.Y + b.Height))
        {
            if (Math.Abs(e.Ort.X - tuerX) > 0.3f * T)
                verstoesse.Add($"{wer}, {fall}: steht {e.Ort.X - tuerX:+0;-0} px neben der Tür von {b.Type} (Tür bei x {tuerX:0}, er bei {e.Ort.X:0})");
            if (e.Ort.Y < kante || e.Ort.Y > kante + 0.2f * T)
                verstoesse.Add($"{wer}, {fall}: steht nicht an der Unterkante von {b.Type} (Kante y {kante:0}, er bei {e.Ort.Y:0})");
        }
        else
        {
            // Ersatz: die begehbare Ringkachel, deren Mitte der Tür am nächsten liegt
            var tuer = new Vector2(tuerX, kante);
            var ersatz = Enumerable.Range(b.X - 1, b.Width + 2)
                .SelectMany(x => Enumerable.Range(b.Y - 1, b.Height + 2).Select(y => (x, y)))
                .Where(c => (c.x == b.X - 1 || c.x == b.X + b.Width || c.y == b.Y - 1 || c.y == b.Y + b.Height)
                            && w.Map.IsWalkable(c.x, c.y))
                .OrderBy(c => Vector2.Distance(w.Map.GridToWorld(new Vector2(c.x, c.y)), tuer))
                .First();
            if (w.Map.WorldToGrid(e.Ort) != new Vector2(ersatz.x, ersatz.y))
                verstoesse.Add($"{wer}, {fall}: Türkachel verstellt - steht in Kachel {w.Map.WorldToGrid(e.Ort)}, die der Tür nächste freie ist ({ersatz.x}, {ersatz.y})");
            if (Abstand(e.Ort, b) > 0.2f * T)
                verstoesse.Add($"{wer}, {fall}: Türkachel verstellt, aber {Abstand(e.Ort, b):0} px vom Gebäude entfernt");
        }
        notiz.Add($"{fall} {e.Ort.X - tuerX:+0;-0}/{e.Ort.Y - kante:0} px");
    }

    // Ein Dorfbewohner holt Holz vom nächsten Wald und bringt es zurück
    Unit Holzholer(Welt w)
    {
        var u = w.Dorfbewohner().First();
        var start = w.Map.WorldToGrid(u.Position);
        Vector2? baum = null;
        float best = float.MaxValue;
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
            {
                var t = w.Map.GetTile(x, y);
                if (t?.ResourceType != Resource.Wood || t.ResourceAmount <= 0)
                    continue;
                float d = Vector2.Distance(start, new Vector2(x, y));
                if (d < best && Enumerable.Range(-1, 3).Any(dx => w.Map.IsWalkable(x + dx, y)))
                {
                    best = d;
                    baum = new Vector2(x, y);
                }
            }
        w.Waehle(new List<Unit> { u });
        w.Linksklick(w.Map.GridToWorld(baum ?? throw new InvalidOperationException("kein Wald")));
        return u;
    }

    TileMap.TestNoFog = true;
    try
    {
        // Stadtzentrum, Dunkle Zeit
        var w = new Welt();
        var u = Holzholer(w);
        Pruefe(w, "Stadtzentrum", Abgabe(w, u, Resource.Wood, 120f), "Stadtzentrum");

        // Stadtzentrum, Imperialzeit - dort liegt die Tür woanders
        w = new Welt();
        var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
            ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
        foreach (var r in new[] { Resource.Food, Resource.Gold, Resource.Wood, Resource.Stone })
            w.P1.Resources.Add(r, 20000);
        for (int i = 0; i < 3; i++)
        {
            ages.TryStart(w.P1.Resources);
            ages.Update(1000f);
        }
        u = Holzholer(w);
        Pruefe(w, "Stadtzentrum Imperialzeit", Abgabe(w, u, Resource.Wood, 120f), "Stadtzentrum");

        // Türkachel verstellt: so nah an der Tür wie möglich, am Gebäude
        w = new Welt();
        var tc = w.Map.Buildings.First(g => g.OwnerId == 0 && g.Type == "Stadtzentrum");
        float f = (float)w.Call("DoorFraction", tc);
        int sp = Math.Clamp((int)(tc.X + f * tc.Width), tc.X, tc.X + tc.Width - 1);
        if (w.Map.IsWalkable(sp, tc.Y + tc.Height))
            w.Map.AddBuilding(sp, tc.Y + tc.Height, "Haus", 0, 1);
        u = Holzholer(w);
        Pruefe(w, "Tür verstellt", Abgabe(w, u, Resource.Wood, 120f), "Stadtzentrum");

        // Eingesperrt: wer die Abgabestelle nicht erreichen kann, liefert nicht ab -
        // der Auftrag endet, die Traglast bleibt
        w = new Welt();
        u = Holzholer(w);
        w.LaufeBis(() => u.Job?.Phase == AoE.Core.Economy.GatherPhase.ToDropOff, 120f);
        var zelle = w.Map.WorldToGrid(u.Position);
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && w.Map.IsWalkable((int)zelle.X + dx, (int)zelle.Y + dy))
                    w.Map.AddBuilding((int)zelle.X + dx, (int)zelle.Y + dy, "Haus", 0, 1);
        int holzVorher = w.P1.Resources[Resource.Wood];
        int last = u.Job?.Carrying ?? 0;
        w.Call("FollowJob", u);
        w.LaufeBis(() => false, 10f);
        if (last == 0)
            verstoesse.Add($"{wer}, eingesperrt: Dorfbewohner trug nichts");
        else if (w.P1.Resources[Resource.Wood] != holzVorher)
            verstoesse.Add($"{wer}, eingesperrt: lieferte {w.P1.Resources[Resource.Wood] - holzVorher} Holz ab, obwohl die Abgabestelle nicht erreichbar ist");

        // Holzfällerlager: die Erbauer sammeln danach Holz und liefern dort ab
        w = new Welt();
        var arbeiter = w.Dorfbewohner().ToList();
        w.Waehle(arbeiter);
        var platz = w.Bauplatz(BuildingType.LumberCamp, arbeiter[0], 3, waldImUmkreis: 5);
        w.Call("PlaceBuilding", BuildingType.LumberCamp, platz);
        var lager = w.Map.Buildings.Last();
        w.LaufeBis(() => lager.IsComplete, 60f);
        if (!lager.IsComplete)
            verstoesse.Add($"{wer}: Holzfällerlager nach 60 s nicht fertig");
        else
        {
            // Die Karten sind jedes Mal neu: liegt der Baum näher am Stadtzentrum, liefert
            // der Dorfbewohner zu Recht dort ab. Jede Abgabe prüft die Tür ihres Gebäudes;
            // das Lager kommt dran, sobald er es anläuft
            bool amLager = false;
            for (int i = 0; i < 4 && !amLager; i++)
            {
                var abgabe = Abgabe(w, arbeiter[0], Resource.Wood, 120f);
                amLager = abgabe?.Ziel == lager;
                Pruefe(w, amLager ? "Holzfällerlager" : $"Abgabe {i + 1} am Stadtzentrum", abgabe,
                       amLager ? "Holzfällerlager" : "Stadtzentrum");
            }
            if (!amLager)
                Console.WriteLine($"  --  {wer}: in vier Abgaben nie am Holzfällerlager - der Wald liegt näher am Stadtzentrum");
        }
    }
    finally
    {
        TileMap.TestNoFog = false;
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: abgeliefert direkt vor der Tür ({string.Join(", ", notiz)})");
}

// Gebäude auswählen (B1): ein Rechtsklick auf ein Gebäude wählt es aus und hebt die
// Einheitenauswahl auf; ein Linksklick ebenso, solange nichts ausgewählt ist - mit
// Dorfbewohnern bleibt er ein Befehl. Q (Dorfbewohner) und A (Zeitalter) gibt es nur
// mit ausgewähltem eigenen Stadtzentrum, ein zweites Stadtzentrum bildet selbst aus.
// Fremde Gebäude nur, wenn erkundet; sie zeigen ihren Status, aber keine Tasten.
static void Auswahl(int karte, List<string> verstoesse)
{
    const int bw = 2406, bh = 1353;
    string wer = $"Karte {karte}, Gebäudeauswahl";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
    const float zoom = 2f;   // bei Zoom 1 läge ein Stadtzentrum in der Kartenecke unter der Minimap
    w.Set("cameraZoom", zoom);
    w.Set("cameraPosition", Vector2.Zero);
    w.P1.Resources.Add(Resource.Food, 2000);
    var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
        ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
    var tc = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Type == "Stadtzentrum");
    var fremd = w.Map.Buildings.First(b => b.OwnerId != 0 && b.Type == "Stadtzentrum");
    var dorf = w.Dorfbewohner().ToList();
    int T = w.Map.TileSize;
    Vector2 Mitte(Building b) => new Vector2((b.X + b.Width / 2f) * T, (b.Y + b.Height / 2f) * T);
    object Gewaehlt() => w.Get("selectedBuilding");
    List<string> Tasten() { w.Call("LayoutButtons"); return w.Tasten().Select(t => t.Label).ToList(); }
    string Status(Building b) => (string)w.Call("BuildingStatus", b);
    var gt = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60));
    void Maus(Vector2 p, ButtonState links, ButtonState rechts)
        => w.Call("HandleRtsInput", gt, new KeyboardState(),
                  new MouseState((int)p.X, (int)p.Y, 0, links, ButtonState.Released, rechts, ButtonState.Released, ButtonState.Released));
    // Kamera so, dass der Weltpunkt mittig im freien Kartenbereich steht (zwischen
    // oberer und unterer Leiste); die Maus dabei mittig, damit kein Kantenscrollen
    // auslöst. Zurück kommt der Bildschirmpunkt des Weltpunkts nach ClampCamera
    Vector2 ZeigeAuf(Vector2 welt)
    {
        int mitte = 56 + (bh - 56 - 200) / 2;   // Mitte des freien Kartenbereichs (HUD_TOP 56, Leiste 200)
        w.Set("cameraPosition", new Vector2(bw / 2f, mitte) / zoom - welt);
        Maus(new Vector2(bw / 2f, mitte), ButtonState.Released, ButtonState.Released);
        return (welt + (Vector2)w.Get("cameraPosition")) * zoom;
    }
    // Ein Punkt des Gebäudes, der nach dem Ausrichten nicht unter Leiste oder Minimap
    // liegt - ein Stadtzentrum in der Kartenecke kann die Kamera nicht mittig zeigen
    Vector2 Sichtbar(Building b)
    {
        foreach (float fy in new[] { 0.5f, 0.3f, 0.1f })
            foreach (float fx in new[] { 0.5f, 0.3f, 0.1f })
            {
                var welt = new Vector2((b.X + fx * b.Width) * T, (b.Y + fy * b.Height) * T);
                var bild = ZeigeAuf(welt);
                if (!(bool)w.Call("IsOverHud", new Point((int)bild.X, (int)bild.Y)))
                    return welt;
            }
        verstoesse.Add($"{wer}: Testaufbau - kein Punkt von {b.Type} bei {b.X},{b.Y} liegt frei im Bild");
        return Mitte(b);
    }
    void Rechtsklick(Vector2 welt)
    {
        var p = ZeigeAuf(welt);
        Maus(p, ButtonState.Released, ButtonState.Pressed);
        Maus(p, ButtonState.Released, ButtonState.Released);
    }
    void Linksklick(Vector2 welt)
    {
        var p = ZeigeAuf(welt);
        Maus(p, ButtonState.Pressed, ButtonState.Released);
        Maus(p, ButtonState.Released, ButtonState.Released);
    }
    void Taste(Keys k)
    {
        var maus = new MouseState(bw / 2, bh / 2, 0, ButtonState.Released, ButtonState.Released,
                                  ButtonState.Released, ButtonState.Released, ButtonState.Released);
        w.Call("HandleRtsInput", gt, new KeyboardState(k), maus);
        w.Call("HandleRtsInput", gt, new KeyboardState(), maus);
    }

    // Unerkundet: das fremde Stadtzentrum lässt sich nicht auswählen
    if (w.Map.IsTileExplored(fremd.X, fremd.Y, 0))
        Console.WriteLine($"  --  {wer}: fremdes Stadtzentrum schon erkundet, Fall unerkundet übersprungen");
    else
    {
        Rechtsklick(Mitte(fremd));
        if (Gewaehlt() != null)
            verstoesse.Add($"{wer}: unerkundetes fremdes Stadtzentrum ausgewählt");
    }

    // Rechtsklick aufs Stadtzentrum, während Dorfbewohner ausgewählt sind
    Rechtsklick(dorf[0].Position - new Vector2(0, 10));
    if (!w.Auswahl().Contains(dorf[0]))
        verstoesse.Add($"{wer}: Rechtsklick auf einen Dorfbewohner wählt ihn nicht aus");
    Rechtsklick(Mitte(tc));
    if (Gewaehlt() != tc)
        verstoesse.Add($"{wer}: Rechtsklick aufs Stadtzentrum wählt es nicht aus");
    if (w.Auswahl().Count > 0 || dorf.Any(u => u.IsSelected))
        verstoesse.Add($"{wer}: mit dem Stadtzentrum bleiben Einheiten ausgewählt");
    // Auch oben im Bild, über der Grundfläche: dort sieht man das Gebäude
    w.Call("SelectBuilding", new object[] { null });
    Rechtsklick(new Vector2((tc.X + tc.Width / 2f) * T, (tc.Y + tc.Height) * T - 2));
    if (Gewaehlt() != tc)
        verstoesse.Add($"{wer}: Rechtsklick knapp über der Unterkante trifft das Stadtzentrum nicht");
    var mitTc = Tasten();
    // W ist seit P2 der Webstuhl
    if (!mitTc.SequenceEqual(new[] { "Q", "W", "A", "." }))
        verstoesse.Add($"{wer}: mit Stadtzentrum Tasten [{string.Join(" ", mitTc)}], erwartet Q W A .");
    string s = Status(tc);
    if (!s.StartsWith("Stadtzentrum") || !s.Contains($"{tc.Health}/{tc.MaxHealth} LP"))
        verstoesse.Add($"{wer}: Status '{s}' nennt nicht Name und Lebenspunkte");

    // Q bildet aus, der Status zeigt es
    Taste(Keys.Q);
    Taste(Keys.Q);
    if (tc.Training.Count != 2)
        verstoesse.Add($"{wer}: zweimal Q - {tc.Training.Count} in der Ausbildung, erwartet 2");
    w.LaufeBis(() => false, 5f);
    s = Status(tc);
    if (!s.Contains("bildet aus: Dorfbewohner") || !s.Contains("%") || !s.Contains("+1 in der Warteschlange"))
        verstoesse.Add($"{wer}: Status während der Ausbildung '{s}'");
    if (s.Any(c => c < 32 || c > 254))
        verstoesse.Add($"{wer}: Status enthält Zeichen außerhalb 32 bis 254 ('{s}')");

    // Ein Dorfbewohner ausgewählt: kein Q, kein A, Q bildet nicht aus
    Rechtsklick(dorf[0].Position - new Vector2(0, 10));
    if (Gewaehlt() != null)
        verstoesse.Add($"{wer}: Rechtsklick auf einen Dorfbewohner lässt das Gebäude ausgewählt");
    var mitDorf = Tasten();
    if (mitDorf.Contains("Q") || mitDorf.Contains("A") || !mitDorf.Contains("H"))
        verstoesse.Add($"{wer}: mit Dorfbewohner Tasten [{string.Join(" ", mitDorf)}], erwartet Bautasten ohne Q und A");
    int inAusbildung = tc.Training.Count;
    Taste(Keys.Q);
    if (tc.Training.Count != inAusbildung)
        verstoesse.Add($"{wer}: Q ohne ausgewähltes Stadtzentrum bildet aus");

    // Linksklick mit Dorfbewohnern aufs Stadtzentrum ist ein Befehl, keine Auswahl
    Linksklick(Mitte(tc));
    if (Gewaehlt() != null)
        verstoesse.Add($"{wer}: Linksklick mit Dorfbewohnern wählt das Stadtzentrum aus");

    // Rechtsklick ins Leere: nichts ausgewählt, nur die Taste "."
    var leer = w.Map.GridToWorld(w.FreieKachel(dorf[0], 8));
    Rechtsklick(leer);
    var ohne = Tasten();
    if (Gewaehlt() != null || w.Auswahl().Count > 0 || !ohne.SequenceEqual(new[] { "." }))
        verstoesse.Add($"{wer}: nach Rechtsklick ins Leere Gebäude {Gewaehlt() ?? "keins"}, Tasten [{string.Join(" ", ohne)}]");

    // Ohne Auswahl wählt auch ein Linksklick das Gebäude
    Linksklick(Mitte(tc));
    if (Gewaehlt() != tc)
        verstoesse.Add($"{wer}: Linksklick ohne Auswahl wählt das Stadtzentrum nicht aus");

    // A startet den Aufstieg, der Status zeigt ihn
    Taste(Keys.A);
    if (ages.Target != Age.Feudal)
        verstoesse.Add($"{wer}: A mit Stadtzentrum startet keinen Aufstieg");
    else if (!Status(tc).Contains("Aufstieg in die Feudalzeit"))
        verstoesse.Add($"{wer}: Status während des Aufstiegs '{Status(tc)}'");

    TileMap.TestNoFog = true;
    try
    {
        // Fremd und erkundet: auswählbar, Status mit (Gegner), keine Tasten, Q wirkungslos
        Rechtsklick(Sichtbar(fremd));
        if (Gewaehlt() != fremd)
            verstoesse.Add($"{wer}: erkundetes fremdes Stadtzentrum nicht auswählbar");
        else
        {
            if (!Status(fremd).Contains("(Gegner)"))
                verstoesse.Add($"{wer}: Status fremd '{Status(fremd)}' ohne (Gegner)");
            var tf = Tasten();
            if (!tf.SequenceEqual(new[] { "." }))
                verstoesse.Add($"{wer}: mit fremdem Gebäude Tasten [{string.Join(" ", tf)}], erwartet nur .");
            int nahrung = w.P1.Resources[Resource.Food];
            Taste(Keys.Q);
            if (fremd.Training.Count != 0 || w.P1.Resources[Resource.Food] != nahrung)
                verstoesse.Add($"{wer}: Q bei fremdem Stadtzentrum bildet aus");
        }

        // Ein zweites Stadtzentrum bildet selbst aus
        var platz = w.Bauplatz(BuildingType.TownCenter, dorf[0], 10);
        var zweites = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Stadtzentrum", 0, 4);
        Rechtsklick(Sichtbar(zweites));
        int ersteVorher = tc.Training.Count;
        Taste(Keys.Q);
        if (Gewaehlt() != zweites || zweites.Training.Count != 1 || tc.Training.Count != ersteVorher)
            verstoesse.Add($"{wer}: zweites Stadtzentrum - ausgewählt {Gewaehlt() == zweites}, Ausbildung dort {zweites.Training.Count}, im ersten {tc.Training.Count} (vorher {ersteVorher})");

        // Eine Baustelle zeigt ihren Fortschritt
        w.Waehle(new List<Unit> { dorf[1] });
        w.Set("selectedBuilding", null);
        var hausPlatz = w.Bauplatz(BuildingType.House, dorf[1], 4);
        w.Call("PlaceBuilding", BuildingType.House, hausPlatz);
        var haus = w.Map.Buildings.Last();
        w.LaufeBis(() => haus.Construction?.Progress > 0.1f, 30f);
        Rechtsklick(Sichtbar(haus));
        if (Gewaehlt() != haus || !Status(haus).Contains("Bau ") || !Status(haus).Contains("%"))
            verstoesse.Add($"{wer}: Baustelle - ausgewählt {Gewaehlt() == haus}, Status '{Status(haus)}'");
    }
    finally
    {
        TileMap.TestNoFog = false;
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Rechts- und Linksklick, Q/A nur am eigenen Stadtzentrum, zweites bildet aus, fremd nur erkundet, Status mit Ausbildung, Aufstieg und Bau");
}

// Angriff (K2): mit Dorfbewohnern ausgewählt wählt ein Linksklick auf ein erkundetes
// fremdes Gebäude den Angriff. Sie laufen hin, schlagen alle 2 s zu, die Gebäudestärke
// sinkt sichtbar, bei 0 ist das Gebäude weg und seine Kacheln sind wieder frei. Eigene
// Gebäude greift man so nicht an, und ein neuer Befehl bricht den Angriff ab.
static void Angriff(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Angriff";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    var dorf = w.Dorfbewohner().ToList();
    int T = w.Map.TileSize;
    Vector2 Mitte(Building b) => new Vector2((b.X + b.Width / 2f) * T, (b.Y + b.Height / 2f) * T);
    var buildings = w.Map.Buildings;

    // Ein fremdes Haus in der Nähe, erkundet
    var platz = w.Bauplatz(BuildingType.House, dorf[0], 6);
    var haus = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Haus", 1, 2);
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    if (!w.Map.IsTileExplored(haus.X, haus.Y, 0))
    {
        verstoesse.Add($"{wer}: Testaufbau - fremdes Haus nicht erkundet");
        return;
    }
    int voll = haus.Health;

    w.Waehle(dorf);
    w.Linksklick(Mitte(haus));
    if (dorf.Any(u => u.AttackTarget != haus))
        verstoesse.Add($"{wer}: Linksklick aufs fremde Haus - nicht alle greifen an ({string.Join(", ", dorf.Select(u => u.AttackTarget?.Type ?? "nichts"))})");

    w.LaufeBis(() => false, 20f);
    int nach20 = haus.Health;
    if (nach20 >= voll)
        verstoesse.Add($"{wer}: nach 20 s Stärke {nach20}/{voll} - sinkt nicht");
    if (dorf.Count(u => u.State == UnitState.Attacking) < dorf.Count)
        verstoesse.Add($"{wer}: nach 20 s greifen nur {dorf.Count(u => u.State == UnitState.Attacking)} von {dorf.Count} an");
    foreach (var u in dorf)
    {
        float dx = Math.Max(Math.Max(haus.X * T - u.Position.X, 0), u.Position.X - (haus.X + haus.Width) * T);
        float dy = Math.Max(Math.Max(haus.Y * T - u.Position.Y, 0), u.Position.Y - (haus.Y + haus.Height) * T);
        if (MathF.Sqrt(dx * dx + dy * dy) > 0.25f * T)
            verstoesse.Add($"{wer}: ein Angreifer steht {MathF.Sqrt(dx * dx + dy * dy):0} px vom Haus entfernt");
    }
    var status = (string)w.Call("BuildingStatus", haus);
    if (!status.Contains($"{haus.Health}/{voll} LP"))
        verstoesse.Add($"{wer}: Status '{status}' zeigt die gesunkene Stärke nicht");
    w.LaufeBis(() => false, 10f);
    if (haus.Health >= nach20 && buildings.Contains(haus))
        verstoesse.Add($"{wer}: Stärke sinkt nicht weiter ({nach20} -> {haus.Health})");

    float dauer = 30f + w.LaufeBis(() => !buildings.Contains(haus), 200f);
    if (buildings.Contains(haus))
        verstoesse.Add($"{wer}: Haus nach {dauer:0} s noch da ({haus.Health}/{voll})");
    else
    {
        for (int x = haus.X; x < haus.X + haus.Width; x++)
            for (int y = haus.Y; y < haus.Y + haus.Height; y++)
                if (!w.Map.IsWalkable(x, y) || !string.IsNullOrEmpty(w.Map.GetTile(x, y).Building))
                    verstoesse.Add($"{wer}: Kachel {x},{y} nach der Zerstörung nicht frei");
        if (dorf.Any(u => u.AttackTarget != null || u.State != UnitState.Idle))
            verstoesse.Add($"{wer}: nach der Zerstörung nicht alle untätig ({string.Join(", ", dorf.Select(u => u.State))})");
    }

    // Ein eigenes Gebäude greift ein Linksklick nicht an
    var eigen = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Haus", 0, 2);
    int eigenVoll = eigen.Health;
    w.Waehle(dorf);
    w.Linksklick(Mitte(eigen));
    w.LaufeBis(() => false, 10f);
    if (dorf.Any(u => u.AttackTarget != null) || eigen.Health < eigenVoll)
        verstoesse.Add($"{wer}: Linksklick aufs eigene Haus greift es an");

    // Ein neuer Befehl bricht den Angriff ab
    var zweites = w.Map.AddBuilding((int)platz.X + 3, (int)platz.Y, "Haus", 1, 2);
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    if (w.Map.CanPlaceBuilding(zweites.X, zweites.Y, 1) || !buildings.Contains(zweites))
        verstoesse.Add($"{wer}: Testaufbau - zweites Haus nicht gesetzt");
    w.Waehle(dorf);
    w.Linksklick(Mitte(zweites));
    w.LaufeBis(() => false, 3f);
    w.Linksklick(w.Map.GridToWorld(w.FreieKachel(dorf[0], 6)));
    if (dorf.Any(u => u.AttackTarget != null))
        verstoesse.Add($"{wer}: ein Laufbefehl bricht den Angriff nicht ab");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Stärke {voll} -> {nach20} nach 20 s, Haus nach {dauer:0} s zerstört, Kacheln frei; eigenes Haus unberührt, neuer Befehl bricht ab");
}

// Soldaten (P1): Kaserne, Schießstand und Stall bilden aus, wenn sie ausgewählt sind -
// Miliz, Bogenschütze und Späher zu Kosten und Zeiten von AoE II. Die Einheit steht
// danach am Gebäude und gehört dem Spieler. Soldaten sammeln nicht, greifen aber
// Gebäude an. Baukürzel wirken nicht, solange ein Gebäude ausgewählt ist.
static void Soldaten(int karte, List<string> verstoesse)
{
    const int bw = 2406, bh = 1353;
    string wer = $"Karte {karte}, Soldaten";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    int T = w.Map.TileSize;
    var ages = (AgeProgress)(typeof(Player).GetProperty("Ages")?.GetValue(w.P1)
        ?? throw new InvalidOperationException("Player.Ages nicht gefunden"));
    foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
        w.P1.Resources.Add(r, 5000);
    ages.TryStart(w.P1.Resources);
    ages.Update(1000f);   // Feudalzeit: Schießstand und Stall
    var dorf = w.Dorfbewohner().ToList();
    var gt = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60));
    void Taste(Keys k)
    {
        var maus = new MouseState(bw / 2, bh / 2, 0, ButtonState.Released, ButtonState.Released,
                                  ButtonState.Released, ButtonState.Released, ButtonState.Released);
        w.Call("HandleRtsInput", gt, new KeyboardState(k), maus);
        w.Call("HandleRtsInput", gt, new KeyboardState(), maus);
    }
    List<(string Label, string Name)> Tasten()
    {
        w.Call("LayoutButtons");
        return ((System.Collections.IEnumerable)w.Get("_buttons")).Cast<object>()
            .Select(b => ((string)b.GetType().GetField("Label").GetValue(b), (string)b.GetType().GetField("Name").GetValue(b)))
            .ToList();
    }
    float Abstand(Vector2 p, Building g)
    {
        float dx = Math.Max(Math.Max(g.X * T - p.X, 0), p.X - (g.X + g.Width) * T);
        float dy = Math.Max(Math.Max(g.Y * T - p.Y, 0), p.Y - (g.Y + g.Height) * T);
        return MathF.Sqrt(dx * dx + dy * dy);
    }
    int Anzahl(UnitType typ) => w.Map.Units.Count(u => u.OwnerId == 0 && u.Type == typ);

    TileMap.TestNoFog = true;
    try
    {
        // Platz in der Bevölkerung: vier Häuser
        for (int i = 0; i < 4; i++)
        {
            var h = w.Bauplatz(BuildingType.House, dorf[0], 9 + i * 2);
            w.Map.AddBuilding((int)h.X, (int)h.Y, "Haus", 0, 2);
        }
        var faelle = new (BuildingType Typ, string Gebaeude, UnitType Einheit, string Name, Type Kern, Dictionary<Resource, int> Kosten, float Sekunden)[]
        {
            (BuildingType.Barracks, "Kaserne", UnitType.Militia, "Miliz", typeof(AoE.Core.Entities.Militia),
             new Dictionary<Resource, int> { [Resource.Food] = 60, [Resource.Gold] = 20 }, 21f),
            (BuildingType.ArcheryRange, "Schießstand", UnitType.Archer, "Bogenschütze", typeof(AoE.Core.Entities.Archer),
             new Dictionary<Resource, int> { [Resource.Wood] = 25, [Resource.Gold] = 45 }, 35f),
            (BuildingType.Stable, "Stall", UnitType.Scout, "Späher", typeof(AoE.Core.Entities.Scout),
             new Dictionary<Resource, int> { [Resource.Food] = 80 }, 30f),
        };
        var notiz = new List<string>();
        foreach (var f in faelle)
        {
            var platz = w.Bauplatz(f.Typ, dorf[0], 6);
            var g = w.Map.AddBuilding((int)platz.X, (int)platz.Y, f.Gebaeude, 0, AoE.Core.Economy.BuildingRules.SizeOf(f.Typ));
            w.Call("SelectBuilding", g);
            var tasten = Tasten();
            if (!tasten.Any(t => t.Label == "Q" && t.Name == f.Name))
                verstoesse.Add($"{wer}: {f.Gebaeude} ausgewählt - Tasten [{string.Join(" ", tasten.Select(t => t.Label + ":" + t.Name))}], erwartet Q:{f.Name}");
            var vor = f.Kosten.Keys.ToDictionary(r => r, r => w.P1.Resources[r]);
            int anzahl = Anzahl(f.Einheit);
            w.Set("hudMessage", null);
            Taste(Keys.Q);
            // Q gehört dem ausgewählten Gebäude - kein Hinweis aufs Stadtzentrum daneben
            if (w.Get("hudMessage") is string meldung && meldung.Contains("Stadtzentrum"))
                verstoesse.Add($"{wer}: Q in {f.Gebaeude} meldet zusätzlich '{meldung}'");
            if (g.Training.Count != 1)
                verstoesse.Add($"{wer}: Q in {f.Gebaeude} - {g.Training.Count} in der Ausbildung, erwartet 1");
            foreach (var (r, menge) in f.Kosten)
                if (w.P1.Resources[r] != vor[r] - menge)
                    verstoesse.Add($"{wer}: {f.Name} - {r} {vor[r]} -> {w.P1.Resources[r]}, erwartet {menge} weniger");
            w.LaufeBis(() => false, 1f);
            string status = (string)w.Call("BuildingStatus", g);
            if (!status.Contains($"bildet aus: {f.Name}"))
                verstoesse.Add($"{wer}: Status von {f.Gebaeude} '{status}'");
            float dauer = 1f + w.LaufeBis(() => Anzahl(f.Einheit) > anzahl, f.Sekunden + 5f);
            var neu = w.Map.Units.LastOrDefault(u => u.OwnerId == 0 && u.Type == f.Einheit);
            if (Anzahl(f.Einheit) <= anzahl || neu == null)
            {
                verstoesse.Add($"{wer}: {f.Name} nach {dauer:0} s nicht ausgebildet");
                continue;
            }
            if (Math.Abs(dauer - f.Sekunden) > 1.5f)
                verstoesse.Add($"{wer}: {f.Name} nach {dauer:0.0} s, erwartet {f.Sekunden} s");
            if (Abstand(neu.Position, g) > 2.5f * T)
                verstoesse.Add($"{wer}: {f.Name} steht {Abstand(neu.Position, g) / T:0.0} Kacheln vom {f.Gebaeude}");
            if (neu.Core.GetType() != f.Kern)
                verstoesse.Add($"{wer}: {f.Name} ist im Kern {neu.Core.GetType().Name}, erwartet {f.Kern.Name}");
            notiz.Add($"{f.Name} {dauer:0} s");
        }

        // Baukürzel wirken nicht, solange ein Gebäude ausgewählt ist
        Taste(Keys.H);
        if (w.Get("placing") != null)
            verstoesse.Add($"{wer}: mit ausgewähltem Gebäude schaltet H den Setzmodus ein");

        // Ein Soldat sammelt nicht, greift aber ein fremdes Gebäude an. Die Tastendrücke
        // oben haben die Kamera eingepasst - für Linksklick (Welt = Bild) zurück auf den Ursprung
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", Vector2.Zero);
        var miliz = w.Map.Units.FirstOrDefault(u => u.OwnerId == 0 && u.Type == UnitType.Militia);
        if (miliz != null)
        {
            var start = w.Map.WorldToGrid(miliz.Position);
            Vector2? baum = null;
            for (int x = 0; x < w.Map.Width && baum == null; x++)
                for (int y = 0; y < w.Map.Height && baum == null; y++)
                    if (w.Map.GetTile(x, y)?.ResourceType == Resource.Wood && Vector2.Distance(start, new Vector2(x, y)) < 15)
                        baum = new Vector2(x, y);
            if (baum != null)
            {
                w.Waehle(new List<Unit> { miliz });
                w.Linksklick(w.Map.GridToWorld(baum.Value));
                if (miliz.Job != null)
                    verstoesse.Add($"{wer}: Miliz bekommt einen Sammelauftrag");
            }
            // Für den Angriff auf den Platz eines Dorfbewohners: am Gebäude kann die Miliz
            // zwischen Häusern und Kartenrand eingesperrt stehen (Testaufbau, nicht Spiel)
            miliz.Path.Clear();
            miliz.State = UnitState.Idle;
            miliz.Position = dorf[1].Position;
            var hp = w.Bauplatz(BuildingType.House, miliz, 4);
            var feind = w.Map.AddBuilding((int)hp.X, (int)hp.Y, "Haus", 1, 2);
            int voll = feind.Health;
            w.Waehle(new List<Unit> { miliz });
            w.Linksklick(new Vector2((feind.X + 1) * T, (feind.Y + 1) * T));
            if (miliz.AttackTarget != feind)
                verstoesse.Add($"{wer}: Linksklick aufs fremde Haus - die Miliz greift nicht an ({miliz.AttackTarget?.Type ?? "nichts"})");
            float bis = w.LaufeBis(() => feind.Health < voll, 60f);
            if (feind.Health >= voll)
                verstoesse.Add($"{wer}: Miliz greift das fremde Haus nicht an ({feind.Health}/{voll} nach 60 s)");
            else
                notiz.Add($"Miliz: Haus {voll} -> {feind.Health} nach {bis:0} s");
        }

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {string.Join(", ", notiz)}; H wirkungslos mit Gebäude, Miliz sammelt nicht");
    }
    finally
    {
        TileMap.TestNoFog = false;
    }
}

// Forschungen (P2): ein ausgewähltes eigenes Gebäude forscht mit Q, W oder E, bezahlt beim
// Start und braucht so lange, wie TechRules sagt; ein Gebäude forscht eine zur Zeit,
// verschiedene Gebäude gleichzeitig, und solange es forscht, bildet es nicht aus. Danach
// wirkt die Forschung auf alles, was der Spieler hat und noch bekommt, nicht auf den Gegner:
// Webstuhl auf Dorfbewohner, Pferdekummet auf neue Felder, Doppelaxt aufs Holzhacken,
// Goldbergbau aufs Goldschürfen, die Schmiede auf Miliz, Späher und Bogenschützen,
// Maurerkunst auf Gebäude. Und jede Wirkung ist im Kampf beweisbar: die Miliz schlägt
// mit Schmiedekunst einen Punkt härter zu, der Webstuhl macht einem eigenen Dorfbewohner
// einen Punkt weniger Schaden an, und Maurerkunst nimmt einem Gebäude einen Punkt ab.
// Wird ein forschendes Gebäude zerstört, ist die Forschung wieder offen und die Kosten
// kommen zurück.
static void Forschung(int karte, List<string> verstoesse)
{
    const int bw = 2406, bh = 1353;
    string wer = $"Karte {karte}, Forschung";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("screenBounds", new Rectangle(0, 0, bw, bh));
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    var ages = w.P1.Ages;
    var techs = w.P1.Techs;
    foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
        w.P1.Resources.Add(r, 5000);
    var dorf = w.Dorfbewohner().ToList();
    var gt = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(1.0 / 60));
    void Taste(Keys k)
    {
        var maus = new MouseState(bw / 2, bh / 2, 0, ButtonState.Released, ButtonState.Released,
                                  ButtonState.Released, ButtonState.Released, ButtonState.Released);
        w.Call("HandleRtsInput", gt, new KeyboardState(k), maus);
        w.Call("HandleRtsInput", gt, new KeyboardState(), maus);
    }
    List<(string Label, string Name)> Tasten()
    {
        w.Call("LayoutButtons");
        return ((System.Collections.IEnumerable)w.Get("_buttons")).Cast<object>()
            .Select(b => ((string)b.GetType().GetField("Label").GetValue(b), (string)b.GetType().GetField("Name").GetValue(b)))
            .ToList();
    }
    string Liste() => string.Join(" ", Tasten().Select(t => t.Label + ":" + t.Name));
    bool HatTaste(string label, string name) => Tasten().Any(t => t.Label == label && t.Name == name);
    string Meldung() => w.Get("hudMessage") as string ?? "";
    Building Setze(BuildingType typ, string name, float abstand)
    {
        var platz = w.Bauplatz(typ, dorf[0], abstand);
        return w.Map.AddBuilding((int)platz.X, (int)platz.Y, name, 0, AoE.Core.Economy.BuildingRules.SizeOf(typ));
    }
    Unit Neu(UnitType typ, int besitzer)
    {
        var c = w.FreieKachel(dorf[0], 4);
        var u = w.Map.AddUnit(typ, (int)c.X, (int)c.Y, besitzer);
        (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
        return u;
    }
    // Forschung mit ihrer Taste im ausgewählten Gebäude starten: Taste da, Kosten abgebucht,
    // die Forschung läuft und hat danach keine Taste mehr
    void Starte(Building g, Keys k, Tech tech)
    {
        w.Call("SelectBuilding", g);
        string name = TechRules.NameOf(tech);
        if (!HatTaste(k.ToString(), name))
            verstoesse.Add($"{wer}: {g.Type} ausgewählt - Tasten [{Liste()}], erwartet {k}:{name}");
        var kosten = TechRules.CostOf(tech);
        var vor = kosten.Keys.ToDictionary(r => r, r => w.P1.Resources[r]);
        Taste(k);
        foreach (var (r, menge) in kosten)
            if (w.P1.Resources[r] != vor[r] - menge)
                verstoesse.Add($"{wer}: {name} - {r} {vor[r]} -> {w.P1.Resources[r]}, erwartet {menge} weniger");
        if (g.Research.Current != tech || !techs.IsPending(tech))
            verstoesse.Add($"{wer}: Taste {k} in {g.Type} - es forscht {g.Research.Current?.ToString() ?? "nichts"}, erwartet {tech}");
        if (HatTaste(k.ToString(), name))
            verstoesse.Add($"{wer}: {name} läuft, hat aber noch eine Taste [{Liste()}]");
    }
    // Wartet, bis tech erforscht ist, und prüft die Dauer (seit dem Start, bisher schon vergangen)
    void Warte(Tech tech, ref float uhr)
    {
        uhr += w.LaufeBis(() => techs.IsResearched(tech), TechRules.SecondsOf(tech) + 15f - uhr);
        if (!techs.IsResearched(tech))
            verstoesse.Add($"{wer}: {TechRules.NameOf(tech)} nach {uhr:0} s nicht erforscht");
        else if (Math.Abs(uhr - TechRules.SecondsOf(tech)) > 1.5f)
            verstoesse.Add($"{wer}: {TechRules.NameOf(tech)} nach {uhr:0.0} s erforscht, erwartet {TechRules.SecondsOf(tech)} s");
    }

    TileMap.TestNoFog = true;
    try
    {
        // Platz in der Bevölkerung: vier Häuser
        for (int i = 0; i < 4; i++)
        {
            var h = w.Bauplatz(BuildingType.House, dorf[0], 9 + i * 2);
            w.Map.AddBuilding((int)h.X, (int)h.Y, "Haus", 0, 2);
        }
        var haus = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.House);
        var tc = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
        var tcFeind = w.Map.Buildings.First(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
        var notiz = new List<string>();

        // --- Dunkle Zeit: Webstuhl im Stadtzentrum (W), Q bildet dort weiter aus ---
        w.Call("SelectBuilding", tc);
        if (!HatTaste("Q", "Dorfbewohner"))
            verstoesse.Add($"{wer}: Stadtzentrum ausgewählt - Tasten [{Liste()}], erwartet Q:Dorfbewohner");
        Taste(Keys.Q);                              // ein Dorfbewohner wartet auf den Webstuhl
        int dorfVor = w.Dorfbewohner().Count();
        Starte(tc, Keys.W, Tech.Loom);
        // Wirkungsbeleg im Kampf: eine fremde Miliz versetzt einen eigenen Dorfbewohner
        // genau einen Punkt weniger Schaden (Rüstung +1) als vor dem Webstuhl.
        var angreiferFeind = Neu(UnitType.Militia, 1);
        int schlagAufDorfVor = angreiferFeind.DamageAgainst(dorf[0]);
        int gold = w.P1.Resources[Resource.Gold];
        Taste(Keys.W);                              // läuft schon: nichts
        if (w.P1.Resources[Resource.Gold] != gold)
            verstoesse.Add($"{wer}: W ein zweites Mal kostet {gold - w.P1.Resources[Resource.Gold]} Gold");
        w.Set("hudMessage", null);
        Taste(Keys.A);
        if (ages.IsResearching)
            verstoesse.Add($"{wer}: A startet den Aufstieg, während das Stadtzentrum forscht");
        else if (!Meldung().Contains("beschäftigt"))
            verstoesse.Add($"{wer}: A während des Webstuhls meldet '{Meldung()}', erwartet 'Das Gebäude ist beschäftigt'");
        float uhr = w.LaufeBis(() => false, 1f);
        string status = (string)w.Call("BuildingStatus", tc);
        if (!status.Contains("forscht: Webstuhl"))
            verstoesse.Add($"{wer}: Status des Stadtzentrums '{status}', erwartet 'forscht: Webstuhl'");
        if (tc.Training.Progress > 0f)
            verstoesse.Add($"{wer}: während des Webstuhls bildet das Stadtzentrum aus ({tc.Training.Progress:P0})");
        Warte(Tech.Loom, ref uhr);
        if (Meldung() != "Webstuhl erforscht")
            verstoesse.Add($"{wer}: nach dem Webstuhl meldet die Leiste '{Meldung()}'");
        if (w.Dorfbewohner().Count() != dorfVor)
            verstoesse.Add($"{wer}: ein Dorfbewohner kam, solange das Stadtzentrum forschte");
        var schwach = w.Map.Units.Where(u => u.OwnerId == 0 && u.Core is CoreVillager
                                            && (u.MaxHealth != 40 || u.Core.Stats.BaseArmor != 1)).ToList();
        if (schwach.Count > 0)
            verstoesse.Add($"{wer}: Webstuhl - {schwach.Count} eigene Dorfbewohner mit {schwach[0].MaxHealth} LP, Rüstung {schwach[0].Core.Stats.BaseArmor}; erwartet 40 und 1");
        var feindDorf = w.Map.Units.FirstOrDefault(u => u.OwnerId == 1 && u.Core is CoreVillager);
        if (feindDorf != null && feindDorf.MaxHealth != 25)
            verstoesse.Add($"{wer}: Webstuhl stärkt auch den Gegner ({feindDorf.MaxHealth} LP)");
        int schlagAufDorfNach = angreiferFeind.DamageAgainst(dorf[0]);
        if (schlagAufDorfNach != schlagAufDorfVor - 1)
            verstoesse.Add($"{wer}: Webstuhl - fremde Miliz versetzt dem eigenen Dorfbewohner {schlagAufDorfNach} Schaden, erwartet {schlagAufDorfVor} (vorher) - 1");
        if (HatTaste("W", "Webstuhl"))
            verstoesse.Add($"{wer}: Webstuhl ist erforscht, hat aber noch eine Taste");
        w.LaufeBis(() => w.Dorfbewohner().Count() > dorfVor, 30f);
        var neuDorf = w.Dorfbewohner().Except(dorf).LastOrDefault();
        if (neuDorf == null)
            verstoesse.Add($"{wer}: der wartende Dorfbewohner kommt nach dem Webstuhl nicht");
        else if (neuDorf.MaxHealth != 40 || neuDorf.Health != 40)
            verstoesse.Add($"{wer}: neuer Dorfbewohner nach dem Webstuhl {neuDorf.Health}/{neuDorf.MaxHealth} LP, erwartet 40/40");
        notiz.Add($"Webstuhl {uhr:0} s");

        // --- Feudalzeit: vier Gebäude forschen gleichzeitig ---
        ages.TryStart(w.P1.Resources);
        ages.Update(1000f);
        var muehle = Setze(BuildingType.Mill, "Mühle", 7);
        var lager = Setze(BuildingType.LumberCamp, "Holzfällerlager", 9);
        var bergbau = Setze(BuildingType.MiningCamp, "Bergbaulager", 11);
        var schmiede = Setze(BuildingType.Blacksmith, "Schmiede", 13);
        var feld1 = w.Bauplatz(BuildingType.Farm, dorf[0], 6);
        w.Call("PlaceBuildingFor", 0, BuildingType.Farm, feld1, new List<Unit>());
        var miliz = Neu(UnitType.Militia, 0);
        var spaeher = Neu(UnitType.Scout, 0);
        var bogen = Neu(UnitType.Archer, 0);
        var milizFeind = Neu(UnitType.Militia, 1);
        var bogenFeind = Neu(UnitType.Archer, 1);

        // Wirkungsbeleg im Kampf: die eigene Miliz versetzt eine fremde Miliz genau
        // einen Punkt mehr Schaden (Angriff +1) als vor der Schmiedekunst.
        int schlagMilizVor = miliz.DamageAgainst(milizFeind);

        Starte(muehle, Keys.Q, Tech.HorseCollar);
        Starte(lager, Keys.Q, Tech.DoubleBitAxe);
        Starte(bergbau, Keys.Q, Tech.GoldMining);
        Starte(schmiede, Keys.Q, Tech.Forging);
        // Die Schmiede forscht schon: W startet nichts und kostet nichts
        int nahrung = w.P1.Resources[Resource.Food];
        gold = w.P1.Resources[Resource.Gold];
        w.Set("hudMessage", null);
        Taste(Keys.W);
        if (techs.IsPending(Tech.Fletching) || w.P1.Resources[Resource.Food] != nahrung || w.P1.Resources[Resource.Gold] != gold)
            verstoesse.Add($"{wer}: W in der forschenden Schmiede startet Befiederte Pfeile oder kostet");
        else if (!Meldung().Contains("beschäftigt"))
            verstoesse.Add($"{wer}: W in der forschenden Schmiede meldet '{Meldung()}', erwartet 'Das Gebäude ist beschäftigt'");
        uhr = 0;
        foreach (var tech in new[] { Tech.HorseCollar, Tech.DoubleBitAxe, Tech.GoldMining, Tech.Forging })
            Warte(tech, ref uhr);
        notiz.Add($"gleichzeitig bis {uhr:0} s");

        // Pferdekummet: neue Felder tragen 250, auch nach dem Nachwachsen; das alte bleibt
        var feld2 = w.Bauplatz(BuildingType.Farm, dorf[0], 6);
        w.Call("PlaceBuildingFor", 0, BuildingType.Farm, feld2, new List<Unit>());
        var alt = w.Map.GetTile((int)feld1.X, (int)feld1.Y);
        var neuFeld = w.Map.GetTile((int)feld2.X, (int)feld2.Y);
        if (alt.ResourceAmount != 175)
            verstoesse.Add($"{wer}: das Feld von vor dem Pferdekummet trägt {alt.ResourceAmount}, erwartet 175");
        if (!neuFeld.Farm || neuFeld.ResourceAmount != 250)
            verstoesse.Add($"{wer}: neues Feld nach dem Pferdekummet trägt {neuFeld.ResourceAmount}, erwartet 250");
        neuFeld.ResourceAmount = 0;
        w.Map.RegrowCrop(0.01f);
        w.Map.RegrowCrop(TileMap.FARM_REGROW_SECONDS + 1f);
        if (neuFeld.ResourceAmount != 250)
            verstoesse.Add($"{wer}: das neue Feld wächst auf {neuFeld.ResourceAmount} nach, erwartet 250");

        // Schmiedekunst: eigene Miliz und Späher schlagen härter, Bogenschütze und Gegner nicht
        if (miliz.Core.Stats.BaseAttack != 5 || spaeher.Core.Stats.BaseAttack != 4)
            verstoesse.Add($"{wer}: Schmiedekunst - Miliz Angriff {miliz.Core.Stats.BaseAttack}, Späher {spaeher.Core.Stats.BaseAttack}; erwartet 5 und 4");
        if (bogen.Core.Stats.BaseAttack != 4 || milizFeind.Core.Stats.BaseAttack != 4)
            verstoesse.Add($"{wer}: Schmiedekunst wirkt auf Bogenschütze ({bogen.Core.Stats.BaseAttack}) oder gegnerische Miliz ({milizFeind.Core.Stats.BaseAttack})");
        int schlagMilizNach = miliz.DamageAgainst(milizFeind);
        if (schlagMilizNach != schlagMilizVor + 1)
            verstoesse.Add($"{wer}: Schmiedekunst - Miliz versetzt {schlagMilizNach} Schaden, erwartet {schlagMilizVor} (vorher) + 1");

        // Doppelaxt: ein Dorfbewohner hackt 20 % schneller, 0,39 * 1,2 Holz je Sekunde
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", Vector2.Zero);
        var holzer = dorf[dorf.Count - 1];
        var start = w.Map.WorldToGrid(holzer.Position);
        Vector2? baum = null;
        for (int x = 0; x < w.Map.Width && baum == null; x++)
            for (int y = 0; y < w.Map.Height && baum == null; y++)
                if (w.Map.GetTile(x, y)?.ResourceType == Resource.Wood && w.Map.GetTile(x, y).ResourceAmount >= 20
                    && Vector2.Distance(start, new Vector2(x, y)) < 15)
                    baum = new Vector2(x, y);
        if (baum == null)
            verstoesse.Add($"{wer}: kein Baum in 15 Kacheln für die Doppelaxt");
        else
        {
            w.Waehle(new List<Unit> { holzer });
            w.Linksklick(w.Map.GridToWorld(baum.Value));
            w.LaufeBis(() => holzer.Job?.Phase == AoE.Core.Economy.GatherPhase.Gathering, 60f);
            if (holzer.Job?.Phase != AoE.Core.Economy.GatherPhase.Gathering)
                verstoesse.Add($"{wer}: der Holzfäller kommt nicht zum Baum ({holzer.Job?.Phase.ToString() ?? "kein Auftrag"})");
            else
            {
                var job = holzer.Job;
                int anfang = job.Carrying;
                float t = w.LaufeBis(() => holzer.Job != job || job.Phase != AoE.Core.Economy.GatherPhase.Gathering, 40f);
                int menge = job.Carrying - anfang;
                float rate = menge / t;
                if (menge < 5)
                    verstoesse.Add($"{wer}: Doppelaxt - nur {menge} Holz in {t:0.0} s gehackt");
                else if (Math.Abs(rate - 0.39f * 1.2f) > 0.02f)
                    verstoesse.Add($"{wer}: Doppelaxt - {rate:0.000} Holz je Sekunde, erwartet {0.39f * 1.2f:0.000} (ohne 0,390)");
                else
                    notiz.Add($"Holz {rate:0.000}/s");
            }
        }

        // Goldbergbau: Gold wird 15 % schneller gesammelt, 0,39 * 1,15 Gold je Sekunde
        var goldarbeiter = Neu(UnitType.Villager, 0);
        Vector2? ader = null;
        var startAder = w.Map.WorldToGrid(goldarbeiter.Position);
        for (int x = 0; x < w.Map.Width && ader == null; x++)
            for (int y = 0; y < w.Map.Height && ader == null; y++)
                if (w.Map.GetTile(x, y)?.ResourceType == Resource.Gold && w.Map.GetTile(x, y).ResourceAmount >= 20
                    && Vector2.Distance(startAder, new Vector2(x, y)) < 15)
                    ader = new Vector2(x, y);
        if (ader == null)
            verstoesse.Add($"{wer}: keine Goldader in 15 Kacheln für den Goldbergbau");
        else
        {
            w.Waehle(new List<Unit> { goldarbeiter });
            w.Linksklick(w.Map.GridToWorld(ader.Value));
            w.LaufeBis(() => goldarbeiter.Job?.Phase == AoE.Core.Economy.GatherPhase.Gathering, 60f);
            if (goldarbeiter.Job?.Phase != AoE.Core.Economy.GatherPhase.Gathering)
                verstoesse.Add($"{wer}: der Goldgräber kommt nicht zu der Ader ({goldarbeiter.Job?.Phase.ToString() ?? "kein Auftrag"})");
            else
            {
                var jobG = goldarbeiter.Job;
                int anfangG = jobG.Carrying;
                float tG = w.LaufeBis(() => goldarbeiter.Job != jobG || jobG.Phase != AoE.Core.Economy.GatherPhase.Gathering, 40f);
                int mengeG = jobG.Carrying - anfangG;
                float rateG = mengeG / tG;
                if (mengeG < 5)
                    verstoesse.Add($"{wer}: Goldbergbau - nur {mengeG} Gold in {tG:0.0} s");
                else if (Math.Abs(rateG - 0.39f * 1.15f) > 0.02f)
                    verstoesse.Add($"{wer}: Goldbergbau - {rateG:0.000} Gold je Sekunde, erwartet {0.39f * 1.15f:0.000} (ohne 0,390)");
                else
                    notiz.Add($"Gold {rateG:0.000}/s");
            }
        }

        // Befiederte Pfeile: eigener Bogenschütze Angriff und Reichweite +1, der Gegner nicht
        uhr = 0;
        Starte(schmiede, Keys.W, Tech.Fletching);
        Warte(Tech.Fletching, ref uhr);
        if (bogen.Core.Stats.BaseAttack != 5 || bogen.Core.Stats.Range != 5)
            verstoesse.Add($"{wer}: Befiederte Pfeile - Bogenschütze Angriff {bogen.Core.Stats.BaseAttack}, Reichweite {bogen.Core.Stats.Range}; erwartet 5 und 5");
        if (bogenFeind.Core.Stats.BaseAttack != 4 || bogenFeind.Core.Stats.Range != 4)
            verstoesse.Add($"{wer}: Befiederte Pfeile wirken auf den gegnerischen Bogenschützen");

        // Schuppenpanzer: die forschende Schmiede wird zerstört - Kosten zurück, wieder offen
        Starte(schmiede, Keys.E, Tech.ScaleMailArmor);
        w.LaufeBis(() => false, 5f);
        nahrung = w.P1.Resources[Resource.Food];
        w.Call("DestroyBuilding", schmiede);
        if (w.P1.Resources[Resource.Food] != nahrung + 100)
            verstoesse.Add($"{wer}: Schmiede zerstört - Nahrung {nahrung} -> {w.P1.Resources[Resource.Food]}, erwartet die 100 zurück");
        if (techs.IsPending(Tech.ScaleMailArmor) || techs.IsResearched(Tech.ScaleMailArmor))
            verstoesse.Add($"{wer}: Schmiede zerstört - Schuppenpanzer ist nicht wieder offen");
        var schmiede2 = Setze(BuildingType.Blacksmith, "Schmiede", 13);
        uhr = 0;
        Starte(schmiede2, Keys.E, Tech.ScaleMailArmor);
        Warte(Tech.ScaleMailArmor, ref uhr);
        if (miliz.Core.Stats.BaseArmor != 1 || spaeher.Core.Stats.BaseArmor != 0 || milizFeind.Core.Stats.BaseArmor != 0)
            verstoesse.Add($"{wer}: Schuppenpanzer - Rüstung Miliz {miliz.Core.Stats.BaseArmor}, Späher {spaeher.Core.Stats.BaseArmor}, gegnerische Miliz {milizFeind.Core.Stats.BaseArmor}; erwartet 1, 0, 0");
        w.Call("SelectBuilding", schmiede2);
        if (Tasten().Any(t => t.Name is "Schmiedekunst" or "Befiederte Pfeile" or "Schuppenpanzer"))
            verstoesse.Add($"{wer}: alles erforscht, die Schmiede hat noch Tasten [{Liste()}]");

        // Eine neue Miliz kommt mit Schmiedekunst und Schuppenpanzer
        var kaserne = Setze(BuildingType.Barracks, "Kaserne", 10);
        w.Call("SelectBuilding", kaserne);
        int milizen = w.Map.Units.Count(u => u.OwnerId == 0 && u.Type == UnitType.Militia);
        Taste(Keys.Q);
        w.LaufeBis(() => w.Map.Units.Count(u => u.OwnerId == 0 && u.Type == UnitType.Militia) > milizen, 30f);
        var neueMiliz = w.Map.Units.LastOrDefault(u => u.OwnerId == 0 && u.Type == UnitType.Militia);
        if (neueMiliz == null || neueMiliz == miliz)
            verstoesse.Add($"{wer}: die Kaserne bildet keine Miliz aus");
        else if (neueMiliz.Core.Stats.BaseAttack != 5 || neueMiliz.Core.Stats.BaseArmor != 1)
            verstoesse.Add($"{wer}: neue Miliz mit Angriff {neueMiliz.Core.Stats.BaseAttack}, Rüstung {neueMiliz.Core.Stats.BaseArmor}; erwartet 5 und 1");

        // --- Ritterzeit: Maurerkunst in der Universität ---
        ages.TryStart(w.P1.Resources);
        ages.Update(1000f);
        var uni = Setze(BuildingType.University, "Universität", 12);
        int tcLp = tc.MaxHealth, tcRuestung = tc.Core.Stats.BaseArmor, hausLp = haus.MaxHealth, feindLp = tcFeind.MaxHealth;
        uhr = 0;
        // Wirkungsbeleg im Kampf: dieselbe Miliz versetzt dem eigenen Haus genau einen
        // Punkt weniger Schaden (Gebäuderüstung +1) als vor der Maurerkunst.
        int hausSchlagVor = AoE.Core.Combat.BuildingCombat.DamagePerHit(miliz.Core, haus.Core);
        Starte(uni, Keys.Q, Tech.Masonry);
        Warte(Tech.Masonry, ref uhr);
        if (tc.MaxHealth != tcLp + tcLp / 10 || tc.Health != tc.MaxHealth || tc.Core.Stats.BaseArmor != tcRuestung + 1)
            verstoesse.Add($"{wer}: Maurerkunst - Stadtzentrum {tc.Health}/{tc.MaxHealth} LP, Rüstung {tc.Core.Stats.BaseArmor}; erwartet {tcLp + tcLp / 10} LP und Rüstung {tcRuestung + 1}");
        if (haus.MaxHealth != hausLp + hausLp / 10)
            verstoesse.Add($"{wer}: Maurerkunst - Haus {haus.MaxHealth} LP, erwartet {hausLp + hausLp / 10}");
        if (tcFeind.MaxHealth != feindLp)
            verstoesse.Add($"{wer}: Maurerkunst stärkt das gegnerische Stadtzentrum ({feindLp} -> {tcFeind.MaxHealth})");
        int hausSchlagNach = AoE.Core.Combat.BuildingCombat.DamagePerHit(miliz.Core, haus.Core);
        if (hausSchlagNach != hausSchlagVor - 1)
            verstoesse.Add($"{wer}: Maurerkunst - Miliz versetzt dem Haus {hausSchlagNach} Schaden, erwartet {hausSchlagVor} (vorher) - 1");
        var hp = w.Bauplatz(BuildingType.House, dorf[0], 8);
        w.Call("PlaceBuildingFor", 0, BuildingType.House, hp, new List<Unit>());
        var neuesHaus = w.Map.Buildings.LastOrDefault(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.House);
        if (neuesHaus == null || neuesHaus == haus || neuesHaus.MaxHealth != 440)
            verstoesse.Add($"{wer}: neues Haus nach der Maurerkunst mit {neuesHaus?.MaxHealth} LP, erwartet 440");
        notiz.Add($"Maurerkunst {uhr:0} s");

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {string.Join(", ", notiz)}; Wirkung nur beim eigenen Spieler, Abbruch erstattet");
    }
    finally
    {
        TileMap.TestNoFog = false;
    }
}

// Schlag (C4n): die Schlagbilder folgen dem Schlagtakt AttackTimer - nach dem Treffer
// durchziehen und zurücknehmen, dann bereit, ausholen, über den Kopf, und das Trefferbild
// steht genau dann, wenn die Stärke sinkt (AttackTimer erreicht RELOAD_SECONDS).
static void Schlag(List<string> verstoesse)
{
    const string wer = "Schlag";
    int vorher = verstoesse.Count;
    var w = new Welt();
    float takt = AoE.Core.Combat.BuildingCombat.RELOAD_SECONDS;
    int Bild(float t) => (int)w.Call("AttackPhase", t);
    var folge = new List<int>();
    for (float t = 0f; t < takt; t += takt / 400f)
    {
        int b = Bild(t);
        if (b < 0 || b > 7) { verstoesse.Add($"{wer}: AttackPhase({t:0.000}) = {b} liegt nicht zwischen 0 und 7"); break; }
        if (folge.Count == 0 || folge[^1] != b) folge.Add(b);
    }
    var soll = new List<int> { 6, 7, 0, 1, 2, 3, 4, 5 };
    if (!folge.SequenceEqual(soll))
        verstoesse.Add($"{wer}: Schlagbilder über einen Takt {string.Join(",", folge)} statt {string.Join(",", soll)}");
    if (Bild(takt * 0.999f) != 5)
        verstoesse.Add($"{wer}: kurz vor dem Treffer Bild {Bild(takt * 0.999f)} statt 5 (Hieb)");
    if (Bild(takt * 0.01f) != 6)
        verstoesse.Add($"{wer}: direkt nach dem Treffer Bild {Bild(takt * 0.01f)} statt 6 (durchziehen)");
    if (Bild(takt) != 5 || Bild(takt * 3f) != 5)
        verstoesse.Add($"{wer}: über dem Takt Bild {Bild(takt)} statt 5 - der Anteil ist nicht begrenzt");
    if (Bild(takt * 0.4f) != 0)
        verstoesse.Add($"{wer}: mitten im Takt Bild {Bild(takt * 0.4f)} statt 0 (bereit)");
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: {string.Join(",", folge)} je Takt, Hieb genau beim Treffer");
}

// Kartengrößen (C11): Standard 64, Groß 90, Maximal 128 Kacheln Seitenlänge;
// die Karte übernimmt das Seitenverhältnis des Bildschirms (bei jeder Größe
// die gleiche Fläche wie die quadratische Referenz), beide Stadtzentren stehen
// in ihren Ecken, und die Minimap zeigt die ganze Karte.
static void Kartengroessen(List<string> verstoesse)
{
    const string wer = "Kartengrößen";
    int vorher = verstoesse.Count;

    // Der Bildschirm, den das Spiel zur Kartenformung nutzt — hier fest,
    // damit die Prüfung reproduzierbar bleibt. Der Spielscreen leitet die
    // Karte aus screenBounds.Width/screenBounds.Height ab (Menüleisten fliegen
    // NICHT in die Rechnung ein).
    int sw = 2406, sh = 1353;
    float aspect = (float)sw / sh;
    var erwartet = new Dictionary<MapSize, int> { [MapSize.Standard] = 64, [MapSize.Large] = 90, [MapSize.Max] = 128 };
    foreach (var (groesse, seite) in erwartet)
    {
        (int mw, int mh) = MapSizes.Dimensions(groesse, aspect);
        if (MapSizes.Side(groesse) != seite)
            verstoesse.Add($"{wer}: {groesse} hat {MapSizes.Side(groesse)} Kacheln Referenzseite statt {seite}");
        var w = new Welt(groesse, aspect);
        if (w.Map.Width != mw || w.Map.Height != mh)
            verstoesse.Add($"{wer}: {groesse} ist {w.Map.Width}x{w.Map.Height} statt {mw}x{mh} (Seitenverhältnis {aspect:0.###})");
        // Die Fläche bleibt die der quadratischen Referenz (Rohstoffdichte).
        if (Math.Abs(w.Map.Width * w.Map.Height - seite * seite) > seite * seite / 100f)
            verstoesse.Add($"{wer}: {groesse}: Fläche {w.Map.Width * w.Map.Height} Kacheln weicht >1 % von {seite * seite} ab");
        var zentren = w.Map.Buildings.Where(b => b.Type == "Stadtzentrum").Select(b => (b.X, b.Y)).OrderBy(p => p).ToList();
        var soll = new List<(int, int)> { (3, 3), (mw - 4, mh - 4) };
        if (!zentren.SequenceEqual(soll))
            verstoesse.Add($"{wer}: {groesse}: Stadtzentren bei {string.Join(", ", zentren)} statt (3, 3) und ({mw - 4}, {mh - 4})");
        w.Set("screenBounds", new Rectangle(0, 0, sw, sh));
        var r = (Rectangle)w.Call("MinimapRect");
        // Die Minimap trägt das Seitenverhältnis der Karte und spannt sie.
        if (Math.Abs((float)r.Width / r.Height - mw / (float)mh) > 0.02f)
            verstoesse.Add($"{wer}: {groesse}: Minimap {r.Width}x{r.Height} statt Kartenformat {mw}:{mh}");
        var ecke = (Vector2)w.Call("MinimapPoint", (float)mw, (float)mh, r);
        if (Math.Abs(ecke.X - r.Right) > 0.5f || Math.Abs(ecke.Y - r.Bottom) > 0.5f)
            verstoesse.Add($"{wer}: {groesse}: die Kartenecke liegt in der Minimap bei {ecke} statt bei ({r.Right}, {r.Bottom})");
    }
    if (Enum.GetValues<MapSize>().Select(MapSizes.Name).Distinct().Count() != 3)
        verstoesse.Add($"{wer}: die drei Größen haben keine verschiedenen Namen");
    if (verstoesse.Count == vorher)
    {
        var dims = MapSizes.Dimensions(MapSize.Standard, aspect);
        var dimm = MapSizes.Dimensions(MapSize.Large, aspect);
        var dimx = MapSizes.Dimensions(MapSize.Max, aspect);
        Console.WriteLine($"  ok  {wer}: {dims} / {dimm} / {dimx} Kacheln im Bildformat, Stadtzentren in den Ecken");
    }
}

// Spielstand (SV3): mitten im Spiel speichern, über JSON laden und weiterspielen. Die
// geladene Welt muss genau so weitermachen wie die, aus der sie kam: dasselbe Haus wird zur
// selben Zeit fertig, dieselben Dorfbewohner sammeln dasselbe Holz und stehen am Ende an
// derselben Stelle. Dazu prüft die Gruppe, was der Spielbildschirm selbst einträgt
// (Spielzeit, KI, Kamera) - und dass die Kamera auch in einem doppelt so großen Fenster auf
// denselben Weltpunkt zeigt. Mit AOE_SPIELSTAND_DATEI schreibt sie den Spielstand von Karte 1
// dorthin; checks/sv_laden.py startet damit das echte Spiel (--laden).
static void Speichern(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Spielstand";
    int vorher = verstoesse.Count;
    var a = new Welt();
    var arbeiter = a.Dorfbewohner().ToList();
    a.Waehle(arbeiter.Take(2).ToList());
    a.Call("PlaceBuilding", BuildingType.LumberCamp, a.Bauplatz(BuildingType.LumberCamp, arbeiter[0], 3, waldImUmkreis: 5));
    var lager = a.Map.Buildings.Last();
    a.Waehle(arbeiter.Skip(2).ToList());
    a.Call("PlaceBuilding", BuildingType.House, a.Bauplatz(BuildingType.House, arbeiter[2], 5));
    var haus = a.Map.Buildings.Last();
    a.LaufeBis(() => false, 12f);
    if (lager.IsComplete || haus.IsComplete)
    {
        verstoesse.Add($"{wer}: nach 12 s schon fertig - der Ablauf speichert zu spät");
        return;
    }

    // Was der Spielbildschirm selbst einträgt; die Kamera blickt auf die Kartenmitte
    var fenster = new Rectangle(0, 0, 1920, 1080);
    a.Set("screenBounds", fenster);
    a.Set("_gameSeconds", 1234.5);
    a.Set("animationTime", 77.25f);
    a.Set("_aiProfile", AoE.Core.Ai.AiProfile.Hard);
    float zoom = 1.7f * (float)a.Prop("ZoomScale");
    var mitte = new Vector2(a.Map.Width * a.Map.TileSize / 2f, a.Map.Height * a.Map.TileSize / 2f);
    Vector2 Blick(Rectangle f, float z, int frei)
        => new Vector2(f.Width / (2f * z), (int)Welt.Konstante("HUD_TOP_HEIGHT") / z + frei / (2f * z));
    a.Set("cameraZoom", zoom);
    a.Set("cameraPosition", Blick(fenster, zoom, (int)a.Prop("FreeHeight")) - mitte);

    var stand = (SaveGame)a.Call("CaptureSave");
    if (stand.GameSeconds != 1234.5 || stand.AnimationTime != 77.25f)
        verstoesse.Add($"{wer}: Spielzeit {stand.GameSeconds} / Takt {stand.AnimationTime} statt 1234.5 / 77.25");
    if (stand.AiDifficulty != 2 || !stand.AiEnabled || stand.BothSidesAi)
        verstoesse.Add($"{wer}: KI {stand.AiDifficulty}, an {stand.AiEnabled}, beide {stand.BothSidesAi} statt 2 (Schwer), an, nur Gegner");
    if (Math.Abs((DateTime.Now - stand.SavedAt).TotalMinutes) > 1)
        verstoesse.Add($"{wer}: Speicherzeit {stand.SavedAt} ist nicht jetzt");
    if (Math.Abs(stand.RelativeZoom - 1.7f) > 0.001f)
        verstoesse.Add($"{wer}: relativer Zoom {stand.RelativeZoom} statt 1.7");
    if (Vector2.Distance(new Vector2(stand.CameraCenterX, stand.CameraCenterY), mitte) > 0.5f)
        verstoesse.Add($"{wer}: Kameramitte ({stand.CameraCenterX}, {stand.CameraCenterY}) statt der Kartenmitte {mitte}");
    if (stand.Units.Count != a.Map.Units.Count || stand.Buildings.Count != a.Map.Buildings.Count)
        verstoesse.Add($"{wer}: {stand.Units.Count} Einheiten / {stand.Buildings.Count} Gebäude statt {a.Map.Units.Count} / {a.Map.Buildings.Count}");

    string json = SaveGameStore.Serialize(stand);
    var datei = Environment.GetEnvironmentVariable("AOE_SPIELSTAND_DATEI");
    if (!string.IsNullOrEmpty(datei) && karte == 1)
        new SaveGameStore(datei).Save(stand);

    var b = new Welt(SaveGameStore.Deserialize(json));
    if ((double)b.Get("_gameSeconds") != 1234.5 || (float)b.Get("animationTime") != 77.25f)
        verstoesse.Add($"{wer}: geladen läuft die Spielzeit bei {b.Get("_gameSeconds")} / {b.Get("animationTime")}");
    if (!ReferenceEquals(b.Get("_aiProfile"), AoE.Core.Ai.AiProfile.Hard))
        verstoesse.Add($"{wer}: geladen ist die KI nicht mehr „Schwer“");

    // Kamera: im selben Fenster dieselbe, im doppelt so großen derselbe Weltpunkt in der Mitte
    b.Set("screenBounds", fenster);
    b.Call("RestoreCamera", stand);
    if (Math.Abs((float)b.Get("cameraZoom") - zoom) > 0.001f
        || Vector2.Distance((Vector2)b.Get("cameraPosition"), (Vector2)a.Get("cameraPosition")) > 0.5f)
        verstoesse.Add($"{wer}: geladene Kamera {b.Get("cameraPosition")} / Zoom {b.Get("cameraZoom")} statt {a.Get("cameraPosition")} / {zoom}");
    var gross = new Rectangle(0, 0, 3840, 2160);
    b.Set("screenBounds", gross);
    b.Call("RestoreCamera", stand);
    float zoomGross = (float)b.Get("cameraZoom");
    var blickGross = Blick(gross, zoomGross, (int)b.Prop("FreeHeight")) - (Vector2)b.Get("cameraPosition");
    if (Math.Abs(zoomGross - 1.7f * (float)b.Prop("ZoomScale")) > 0.001f || Vector2.Distance(blickGross, mitte) > 0.5f)
        verstoesse.Add($"{wer}: im großen Fenster Zoom {zoomGross}, Mitte {blickGross} statt {1.7f * (float)b.Prop("ZoomScale")}, {mitte}");

    // Weiterspielen: beide Welten bis das Haus steht, dann noch 90 s - lang genug, dass das
    // Lager fertig wird und Holz ankommt; erst damit vergleicht der Ablauf auch das Sammeln
    int holzBeimSpeichern = a.P1.Resources[Resource.Wood];
    var hausB = b.Map.Buildings[a.Map.Buildings.IndexOf(haus)];
    float tA = a.LaufeBis(() => haus.IsComplete, 60f);
    float tB = b.LaufeBis(() => hausB.IsComplete, 60f);
    if (!haus.IsComplete || !hausB.IsComplete || Math.Abs(tA - tB) > 0.05f)
        verstoesse.Add($"{wer}: das Haus wird ohne Laden nach {tA:0.00} s fertig ({haus.IsComplete}), geladen nach {tB:0.00} s ({hausB.IsComplete})");
    a.LaufeBis(() => false, 90f);
    b.LaufeBis(() => false, 90f);
    if (a.P1.Resources[Resource.Wood] <= holzBeimSpeichern)
        verstoesse.Add($"{wer}: in 90 s kam kein Holz an ({a.P1.Resources[Resource.Wood]}) - der Ablauf vergleicht das Sammeln nicht");
    var lagerB = b.Map.Buildings[a.Map.Buildings.IndexOf(lager)];
    if (lager.IsComplete != lagerB.IsComplete)
        verstoesse.Add($"{wer}: das Holzfällerlager ist ohne Laden {(lager.IsComplete ? "fertig" : "nicht fertig")}, geladen nicht");
    foreach (var r in new[] { Resource.Food, Resource.Wood, Resource.Gold, Resource.Stone })
        if (a.P1.Resources[r] != b.P1.Resources[r])
            verstoesse.Add($"{wer}: nach dem Weiterspielen {r} {a.P1.Resources[r]} ohne Laden, {b.P1.Resources[r]} geladen");
    for (int i = 0; i < a.Map.Units.Count; i++)
    {
        var (ua, ub) = (a.Map.Units[i], b.Map.Units[i]);
        if (ua.OwnerId == 0 && Vector2.Distance(ua.Position, ub.Position) > 1f)
            verstoesse.Add($"{wer}: Einheit {i} steht ohne Laden bei {ua.Position}, geladen bei {ub.Position}");
    }
    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: geladen wie gespeichert - Haus nach {tB:0.0} s, Holz {b.P1.Resources[Resource.Wood]}, Kamera auch im großen Fenster");
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

    // Spiel laden (SV3): ohne Spielstand gesperrt - die Auswahl verlässt den Eintrag -,
    // mit Spielstand frei, mit der Maus startbar und mit den Pfeiltasten erreichbar
    var saves = typ.GetField("saves", F)
        ?? throw new InvalidOperationException("Feld MainMenuScreen.saves nicht gefunden");
    string pfad = Path.Combine(Path.GetTempPath(), $"aoe-menue-{Guid.NewGuid():N}.json");
    saves.SetValue(menue, new SaveGameStore(pfad));
    Rectangle Laden() => (Rectangle)Methode("EntryRect").Invoke(null, new object[] { 2, 1920, 1080 });
    (int, bool) Klick(Rectangle r) => ((int, bool))Methode("EvaluateMouse").Invoke(menue,
        new object[] { Maus(r.Center, false), Maus(r.Center, true), 1920, 1080 });
    try
    {
        auswahl.SetValue(menue, 2);
        Methode("RefreshLoadEntry").Invoke(menue, null);
        if ((int)auswahl.GetValue(menue) != 0)
            verstoesse.Add($"{wer}: ohne Spielstand bleibt die Auswahl auf dem gesperrten „Spiel laden“");
        if (Klick(Laden()) != (0, false))
            verstoesse.Add($"{wer}: ohne Spielstand lässt sich „Spiel laden“ anklicken");

        File.WriteAllText(pfad, "{}");
        Methode("RefreshLoadEntry").Invoke(menue, null);
        if (Klick(Laden()) != (2, true))
            verstoesse.Add($"{wer}: mit Spielstand startet ein Klick „Spiel laden“ nicht");
        auswahl.SetValue(menue, 1);
        Methode("MoveSelection").Invoke(menue, new object[] { 1 });
        if ((int)auswahl.GetValue(menue) != 2)
            verstoesse.Add($"{wer}: mit Spielstand führt Pfeil nach unten von „Karte“ auf {auswahl.GetValue(menue)} statt auf „Spiel laden“");

        File.Delete(pfad);
        Methode("RefreshLoadEntry").Invoke(menue, null);
        if ((int)auswahl.GetValue(menue) != 0 || Klick(Laden()) != (0, false))
            verstoesse.Add($"{wer}: nach dem Löschen des Spielstands ist „Spiel laden“ nicht wieder gesperrt");
    }
    finally
    {
        if (File.Exists(pfad))
            File.Delete(pfad);
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: mittig, wächst mit dem Fenster, Zeigen wählt, Klick startet, „Spiel laden“ nur mit Spielstand, Karte wechselt die Größe");
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
// Zielen (K5, Nutzer 2026-10-11: "Wenn ich einen Gegner angreifen möchte ..., kann ich ihn nur
// schwer auswählen. Es scheint so, dass ich ihn am Fuß auswählen muss"): mit ausgewählten
// Einheiten greift ein Linksklick irgendwo auf die Figur eines sichtbaren Gegners ihn an - bis
// dahin nur auf der Kachel unter seinen Füßen. Über dem Kopf und im Nebel nicht. Den Späher zu
// Pferd, größer gezeichnet, wählt ein Klick auf den Kopf aus.
static void Zielen(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Zielen";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    var dorf = w.Dorfbewohner().ToList();
    int T = w.Map.TileSize;
    var start = w.Map.WorldToGrid(dorf[0].Position);

    bool Frei(int x, int y) => w.Map.GetTile(x, y) is { } t && w.Map.IsWalkable(x, y) && t.ResourceType == null
        && string.IsNullOrEmpty(t.Building) && !w.Map.Units.Any(u => w.Map.WorldToGrid(u.Position) == new Vector2(x, y));
    IEnumerable<Vector2> Kacheln() => Enumerable.Range(0, w.Map.Width)
        .SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)));
    Unit Neu(UnitType typ, int besitzer, Vector2 kachel)
    {
        var u = w.Map.AddUnit(typ, (int)kachel.X, (int)kachel.Y, besitzer);
        (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
        return u;
    }
    // Eine sichtbare freie Kachel etwa abstand Kacheln vom ersten Dorfbewohner, deren Nachbarn
    // darüber, rechts und darunter ebenfalls frei sind - dort landen die Klicks neben den Füßen
    Vector2 Platz(float abstand) => Kacheln()
        .Where(c => c.Y >= 2 && Frei((int)c.X, (int)c.Y) && Frei((int)c.X, (int)c.Y - 1) && Frei((int)c.X, (int)c.Y - 2)
                    && Frei((int)c.X + 1, (int)c.Y) && Frei((int)c.X, (int)c.Y + 1) && Frei((int)c.X + 1, (int)c.Y + 1)
                    && w.Map.IsTileVisible((int)c.X, (int)c.Y, 0))
        .OrderBy(c => Math.Abs(Vector2.Distance(c, start) - abstand))
        .First();

    var miliz = new List<Unit> { Neu(UnitType.Militia, 0, w.FreieKachel(dorf[0], 2)),
                                 Neu(UnitType.Militia, 0, w.FreieKachel(dorf[0], 2)) };
    var sel = dorf.Concat(miliz).ToList();
    var feind = Neu(UnitType.Militia, 1, Platz(5));
    var kachel = w.Map.WorldToGrid(feind.Position);
    int cx = (int)kachel.X, cy = (int)kachel.Y;
    // Laufbefehle gehen auf eine freie Kachel, weit genug vom Gegner
    var weg = Kacheln().Where(c => Frei((int)c.X, (int)c.Y) && Vector2.Distance(c, kachel) >= 3)
        .OrderBy(c => Math.Abs(Vector2.Distance(c, start) - 2)).First();
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    if (!w.Map.IsTileVisible(cx, cy, 0))
    {
        verstoesse.Add($"{wer}: Testaufbau - der Gegner auf {cx},{cy} ist nicht sichtbar");
        return;
    }

    string Ziele() => string.Join(", ", sel.Select(u => u.UnitTarget == feind ? "Gegner" : u.UnitTarget == null ? "nichts" : "anderes"));
    // Ein Linksklick mit der ganzen Auswahl; unter dem Punkt darf keine eigene Figur stehen
    void Klick(Vector2 punkt, string was)
    {
        if (w.Call("OwnUnitAt", punkt) != null)
            verstoesse.Add($"{wer}: Testaufbau - {was}: dort steht eine eigene Figur");
        w.Waehle(sel);
        w.Linksklick(punkt);
    }
    // Ein Laufbefehl hebt jeden Angriff auf
    bool Zuruecksetzen()
    {
        w.Waehle(sel);
        w.Linksklick(w.Map.GridToWorld(weg));
        if (sel.All(u => u.UnitTarget == null))
            return true;
        verstoesse.Add($"{wer}: Testaufbau - ein Laufbefehl hebt den Angriff nicht auf ({Ziele()})");
        return false;
    }

    // 1. Auf die Brust: der Punkt liegt schon auf der Kachel über den Füßen
    feind.Position = new Vector2(cx * T + 16, cy * T + 5);
    Klick(feind.Position + new Vector2(0, -15), "Brust");
    if (sel.Any(u => u.UnitTarget != feind))
        verstoesse.Add($"{wer}: Klick auf die Brust des Gegners (Kachel über seinen Füßen) - Ziele: {Ziele()}; erwartet alle den Gegner");

    // 2. An die Seite: der Punkt liegt auf der Kachel rechts neben den Füßen
    if (!Zuruecksetzen()) return;
    feind.Position = new Vector2(cx * T + 29, cy * T + 20);
    Klick(feind.Position + new Vector2(9, -12), "Seite");
    if (sel.Any(u => u.UnitTarget != feind))
        verstoesse.Add($"{wer}: Klick auf die Seite des Gegners (Kachel rechts neben den Füßen) - Ziele: {Ziele()}; erwartet alle den Gegner");

    // 3. Über dem Kopf (40 Pixel über den Füßen): ein Laufbefehl, kein Angriff
    if (!Zuruecksetzen()) return;
    feind.Position = new Vector2(cx * T + 16, cy * T + 20);
    Klick(feind.Position + new Vector2(0, -40), "über dem Kopf");
    if (sel.Any(u => u.UnitTarget != null))
        verstoesse.Add($"{wer}: Klick 40 px über den Füßen des Gegners greift an ({Ziele()}) - erwartet ein Laufbefehl");

    // 4. Im Nebel: einen Gegner, den Spieler 0 gerade nicht sieht, trifft der Klick nicht
    var nebel = Kacheln().Where(c => w.Map.IsWalkable((int)c.X, (int)c.Y)
                                     && !w.Map.IsTileVisible((int)c.X, (int)c.Y, 0)
                                     && !w.Map.IsTileExplored((int)c.X, (int)c.Y, 0))
        .Select(c => (Vector2?)c).FirstOrDefault();
    if (nebel is not { } n)
        verstoesse.Add($"{wer}: Testaufbau - keine begehbare Kachel im Nebel");
    else
    {
        feind.Position = new Vector2(n.X * T + 16, n.Y * T + 20);
        Klick(feind.Position + new Vector2(0, -12), "Nebel");
        if (sel.Any(u => u.UnitTarget != null))
            verstoesse.Add($"{wer}: Klick auf einen Gegner im Nebel ({n.X},{n.Y}) greift ihn an ({Ziele()})");
    }

    // 5. Den Späher zu Pferd, 1,35-mal so groß gezeichnet, wählt ein Klick auf den Kopf aus
    var spaeher = Neu(UnitType.Scout, 0, Platz(7));
    var sk = w.Map.WorldToGrid(spaeher.Position);
    spaeher.Position = new Vector2(sk.X * T + 16, sk.Y * T + 28);
    w.Waehle(sel);
    w.Linksklick(spaeher.Position + new Vector2(0, -29));
    var auswahl = w.Auswahl();
    if (auswahl.Count != 1 || auswahl[0] != spaeher)
        verstoesse.Add($"{wer}: Klick auf den Kopf des Spähers (29 px über den Füßen) wählt ihn nicht aus - ausgewählt sind {auswahl.Count} Einheiten");

    // 6. Ein eigener Kämpfer steht vor dem Gegner (K5b, Nutzer 2026-10-11: "Wenn einer meiner
    // Leute selektiert ist und ich klicke auf den Gegner, muss dieser angegriffen werden"): mit
    // Auswahl greift der Klick den Gegner an, statt den Kämpfer davor auszuwählen
    feind.Position = new Vector2(cx * T + 16, cy * T + 20);
    var kaempfer = Neu(UnitType.Militia, 0, kachel);
    kaempfer.Position = feind.Position + new Vector2(0, 6);
    var ueberlappt = feind.Position + new Vector2(0, -12);
    var helfer = dorf[0];
    if (w.Call("OwnUnitAt", ueberlappt) != kaempfer)
        verstoesse.Add($"{wer}: Testaufbau - der eigene Kämpfer steht nicht vor dem Gegner");
    else
    {
        w.Waehle(new List<Unit> { helfer });
        w.Linksklick(ueberlappt);
        if (helfer.UnitTarget != feind || w.Auswahl().Count != 1 || w.Auswahl()[0] != helfer)
            verstoesse.Add($"{wer}: ein eigener Kämpfer steht vor dem Gegner - der Klick mit einem ausgewählten Dorfbewohner {(w.Auswahl().Contains(kaempfer) ? "wählt den Kämpfer aus" : "greift nicht an")}; erwartet: der Dorfbewohner greift den Gegner an");
        // Ohne Auswahl wählt derselbe Klick den eigenen Kämpfer aus
        w.Waehle(new List<Unit>());
        w.Linksklick(ueberlappt);
        if (w.Auswahl().Count != 1 || w.Auswahl()[0] != kaempfer)
            verstoesse.Add($"{wer}: ohne Auswahl wählt ein Klick auf den eigenen Kämpfer vor dem Gegner ihn nicht aus");
    }
    kaempfer.Position = w.Map.GridToWorld(weg);

    // 7. Kampf: der an der Brust angeklickte Gegner fällt
    feind.Position = new Vector2(cx * T + 16, cy * T + 5);
    Klick(feind.Position + new Vector2(0, -15), "Brust im Kampf");
    float dauer = w.LaufeBis(() => !w.Map.Units.Contains(feind), 60f);
    if (w.Map.Units.Contains(feind))
        verstoesse.Add($"{wer}: der angeklickte Gegner steht nach {dauer:0} s noch ({feind.Health}/{feind.MaxHealth} LP)");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Brust und Seite treffen, über dem Kopf und im Nebel nicht, auch hinter einem eigenen Kämpfer; Späher am Kopf auswählbar; Gegner nach {dauer:0.0} s besiegt");
}

// Verteidigung (K6, Nutzer 2026-10-11: "Zudem sollten Soldaten jemanden, der das Dorfzentrum
// angreift, automatisch angreifen"): greift ein Gegner ein Stadtzentrum an, greifen ihn die
// untätigen Soldaten seines Besitzers in der Nähe von selbst an - auf beiden Seiten.
// Dorfbewohner nicht, Soldaten weit draußen nicht, und wer gerade einen Befehl ausführt,
// bleibt dabei.
static void Verteidigung(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Verteidigung";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    var dorf = w.Dorfbewohner().ToList();
    int T = w.Map.TileSize;

    bool Frei(int x, int y) => w.Map.GetTile(x, y) is { } t && w.Map.IsWalkable(x, y) && t.ResourceType == null
        && string.IsNullOrEmpty(t.Building) && !w.Map.Units.Any(u => w.Map.WorldToGrid(u.Position) == new Vector2(x, y));
    Unit Neu(UnitType typ, int besitzer, Vector2 kachel)
    {
        var u = w.Map.AddUnit(typ, (int)kachel.X, (int)kachel.Y, besitzer);
        (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
        return u;
    }
    Vector2 Mitte(Building b) => new Vector2((b.X + b.Width / 2f) * T, (b.Y + b.Height / 2f) * T);
    // Eine freie Kachel, deren Mitte etwa abstand Kacheln von der Mitte des Gebäudes liegt
    Vector2 Kachel(Building b, float abstand) => Enumerable.Range(0, w.Map.Width)
        .SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)))
        .Where(c => Frei((int)c.X, (int)c.Y))
        .OrderBy(c => Math.Abs(Vector2.Distance(w.Map.GridToWorld(c), Mitte(b)) / T - abstand))
        .First();
    string Ziel(Unit u, Unit feind) => u.UnitTarget == feind ? "Angreifer" : u.UnitTarget == null ? $"nichts ({u.State})" : "anderes";

    var tc = w.Map.Buildings.First(b => b.OwnerId == 0 && b.Core.BuildingType == BuildingType.TownCenter);
    var wachen = new List<Unit> { Neu(UnitType.Militia, 0, Kachel(tc, 5)), Neu(UnitType.Militia, 0, Kachel(tc, 5)),
                                  Neu(UnitType.Archer, 0, Kachel(tc, 6)) };
    var laeufer = Neu(UnitType.Militia, 0, Kachel(tc, 5));
    var fern = Neu(UnitType.Militia, 0, Kachel(tc, 24));
    var feind = Neu(UnitType.Militia, 1, Kachel(tc, 4));
    // Die Dorfbewohner stehen untätig herum - verteidigen sollen sie trotzdem nicht
    foreach (var d in dorf)
    {
        d.Job = null;
        d.BuildSite = null;
        d.Path.Clear();
        d.State = UnitState.Idle;
    }

    w.Call("AttackBuilding", new List<Unit> { feind }, tc);
    // Verteidigt wird erst, wenn der Gegner zuschlägt - nicht schon, während er anrückt;
    // sonst liefen die Wachen jedem entgegen, der irgendwo aufs Stadtzentrum zuhält
    bool zuFrueh = false;
    w.LaufeBis(() =>
    {
        if (feind.State != UnitState.Attacking && wachen.Any(u => u.UnitTarget == feind))
            zuFrueh = true;
        return feind.State == UnitState.Attacking;
    }, 40f);
    if (feind.State != UnitState.Attacking)
    {
        verstoesse.Add($"{wer}: Testaufbau - der Gegner kommt nicht ans Stadtzentrum ({feind.State})");
        return;
    }
    if (zuFrueh)
        verstoesse.Add($"{wer}: die Wachen greifen an, während der Gegner noch zum Stadtzentrum läuft - erst, wenn er zuschlägt");

    // Ein Soldat mit Laufbefehl bleibt dabei
    w.Waehle(new List<Unit> { laeufer });
    w.Linksklick(w.Map.GridToWorld(Kachel(tc, 14)));
    if (laeufer.State != UnitState.Moving)
        verstoesse.Add($"{wer}: Testaufbau - der Läufer läuft nicht ({laeufer.State})");

    float zeit = w.LaufeBis(() => wachen.All(u => u.UnitTarget == feind), 3f);
    if (!wachen.All(u => u.UnitTarget == feind))
        verstoesse.Add($"{wer}: {zeit:0.0} s nach Beginn des Angriffs aufs Stadtzentrum - untätige Soldaten 5 bis 6 Kacheln davon: {string.Join(", ", wachen.Select(u => Ziel(u, feind)))}; erwartet alle den Angreifer");
    if (laeufer.UnitTarget != null)
        verstoesse.Add($"{wer}: ein Soldat mit Laufbefehl lässt ihn für die Verteidigung fallen");
    if (fern.UnitTarget != null)
        verstoesse.Add($"{wer}: ein Soldat 24 Kacheln vom Stadtzentrum kommt zur Verteidigung");
    if (dorf.Any(d => d.UnitTarget != null))
        verstoesse.Add($"{wer}: {dorf.Count(d => d.UnitTarget != null)} Dorfbewohner verteidigen - nur Soldaten sollen es");

    float kampf = w.LaufeBis(() => !w.Map.Units.Contains(feind), 60f);
    if (w.Map.Units.Contains(feind))
        verstoesse.Add($"{wer}: der Angreifer steht nach {kampf:0} s Verteidigung noch ({feind.Health}/{feind.MaxHealth} LP)");
    if (!w.Map.Buildings.Contains(tc))
        verstoesse.Add($"{wer}: das Stadtzentrum ist gefallen");

    // Zwei Angreifer von zwei Seiten: jeder Verteidiger nimmt den, der ihm am nächsten steht
    Vector2 Naechste(Vector2 wunsch) => Enumerable.Range(0, w.Map.Width)
        .SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)))
        .Where(c => Frei((int)c.X, (int)c.Y))
        .OrderBy(c => Vector2.Distance(c, wunsch))
        .First();
    var feindA = Neu(UnitType.Militia, 1, Naechste(new Vector2(tc.X + tc.Width + 2, tc.Y + 1)));
    var feindB = Neu(UnitType.Militia, 1, Naechste(new Vector2(tc.X + 1, tc.Y + tc.Height + 2)));
    w.Call("AttackBuilding", new List<Unit> { feindA, feindB }, tc);
    w.LaufeBis(() => feindA.State == UnitState.Attacking && feindB.State == UnitState.Attacking, 40f);
    if (feindA.State != UnitState.Attacking || feindB.State != UnitState.Attacking)
        verstoesse.Add($"{wer}: Testaufbau - die zwei Gegner kommen nicht ans Stadtzentrum ({feindA.State}, {feindB.State})");
    else if (Vector2.Distance(feindA.Position, feindB.Position) < 2 * T)
        verstoesse.Add($"{wer}: Testaufbau - die zwei Gegner stehen nur {Vector2.Distance(feindA.Position, feindB.Position):0} px auseinander");
    else
    {
        // Je eine frische Wache dicht neben jedem Angreifer
        var wacheA = Neu(UnitType.Militia, 0, w.Map.WorldToGrid(feindA.Position));
        wacheA.Position = feindA.Position + new Vector2(3, 0);
        var wacheB = Neu(UnitType.Militia, 0, w.Map.WorldToGrid(feindB.Position));
        wacheB.Position = feindB.Position + new Vector2(3, 0);
        w.LaufeBis(() => wacheA.UnitTarget != null && wacheB.UnitTarget != null, 2f);
        string Wer(Unit u) => u.UnitTarget == feindA ? "A" : u.UnitTarget == feindB ? "B" : "nichts";
        if (wacheA.UnitTarget != feindA || wacheB.UnitTarget != feindB)
            verstoesse.Add($"{wer}: zwei Angreifer A und B, je eine Wache daneben - die Wache bei A nimmt {Wer(wacheA)}, die bei B {Wer(wacheB)}; erwartet jede den ihr nächsten");
    }

    // Auf der Gegenseite gilt dasselbe
    var tc2 = w.Map.Buildings.First(b => b.OwnerId == 1 && b.Core.BuildingType == BuildingType.TownCenter);
    var wache2 = Neu(UnitType.Militia, 1, Kachel(tc2, 5));
    var angreifer = Neu(UnitType.Militia, 0, Kachel(tc2, 4));
    w.Call("AttackBuilding", new List<Unit> { angreifer }, tc2);
    w.LaufeBis(() => angreifer.State == UnitState.Attacking, 40f);
    if (angreifer.State != UnitState.Attacking)
        verstoesse.Add($"{wer}: Testaufbau - der eigene Angreifer kommt nicht ans gegnerische Stadtzentrum ({angreifer.State})");
    else
    {
        w.LaufeBis(() => wache2.UnitTarget == angreifer, 3f);
        if (wache2.UnitTarget != angreifer)
            verstoesse.Add($"{wer}: ein untätiger Soldat des Gegners verteidigt sein Stadtzentrum nicht ({Ziel(wache2, angreifer)})");
    }

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: drei Wachen greifen nach {zeit:0.0} s an, Läufer, ferner Soldat und Dorfbewohner nicht; Angreifer nach {kampf:0.0} s besiegt; der Gegner verteidigt ebenso");
}

// Kampfbild (K7, Nutzer 2026-10-11: "wieso wippt der Soldat, wenn er einen gegnerischen
// Dorfbewohner angreift, anstatt wie bei einem Gebäude die Waffe zu schwingen"): wer eine
// Einheit angreift, schlägt zu wie gegen ein Gebäude (IsStriking) und blickt zu ihr.
static void Kampfbild(List<string> verstoesse)
{
    const string wer = "Kampfbild";
    int vorher = verstoesse.Count;
    var w = new Welt();
    var dorf = w.Dorfbewohner().ToList();
    bool Schlaegt(Unit u) => (bool)w.Call("IsStriking", u);
    bool Links(Unit u)
    {
        var m = ((System.Collections.IDictionary)w.Get("_unitMotion"))[u]!;
        return (bool)m.GetType().GetField("FacingLeft")!.GetValue(m)!;
    }
    void Bewegung()
    {
        for (int i = 0; i < 30; i++)
            w.Call("UpdateUnitMotion", 1f / 60f);
    }

    // Eine Miliz und, 20 Pixel links von ihr, ein gegnerischer Dorfbewohner
    var platz = w.FreieKachel(dorf[0], 4);
    var miliz = w.Map.AddUnit(UnitType.Militia, (int)platz.X, (int)platz.Y, 0);
    w.P1.AddUnit(miliz);
    var opfer = w.Map.AddUnit(UnitType.Villager, (int)platz.X, (int)platz.Y, 1);
    w.P2.AddUnit(opfer);
    opfer.Position = miliz.Position - new Vector2(20, 0);

    w.Call("AttackUnit", new List<Unit> { miliz }, opfer);
    w.LaufeBis(() => miliz.State == UnitState.Attacking, 5f);
    if (miliz.State != UnitState.Attacking)
    {
        verstoesse.Add($"{wer}: Testaufbau - die Miliz greift den Dorfbewohner nicht an ({miliz.State})");
        return;
    }
    if (!Schlaegt(miliz))
        verstoesse.Add($"{wer}: die Miliz greift einen Dorfbewohner an - IsStriking ist false, sie wippt statt zuzuschlagen");

    // Blick zum Gegner: erst steht er links, dann rechts von ihr
    opfer.Position = miliz.Position - new Vector2(20, 0);
    miliz.Path.Clear();
    Bewegung();
    if (!Links(miliz))
        verstoesse.Add($"{wer}: der angegriffene Dorfbewohner steht links - die Miliz blickt nach rechts");
    opfer.Position = miliz.Position + new Vector2(20, 0);
    Bewegung();
    if (Links(miliz))
        verstoesse.Add($"{wer}: der angegriffene Dorfbewohner steht rechts - die Miliz blickt nach links");

    // Gegen ein Gebäude wie bisher; untätig oder mit totem Ziel nicht
    var fremd = w.Map.Buildings.First(b => b.OwnerId == 1);
    miliz.UnitTarget = null;
    miliz.AttackTarget = fremd;
    if (!Schlaegt(miliz))
        verstoesse.Add($"{wer}: die Miliz greift ein Gebäude an - IsStriking ist false");
    miliz.AttackTarget = null;
    miliz.UnitTarget = opfer;
    miliz.State = UnitState.Idle;
    if (Schlaegt(miliz))
        verstoesse.Add($"{wer}: eine untätige Miliz mit Ziel - IsStriking ist true");
    miliz.State = UnitState.Attacking;
    opfer.State = UnitState.Dead;
    if (Schlaegt(miliz))
        verstoesse.Add($"{wer}: die Miliz greift einen toten Dorfbewohner an - IsStriking ist true");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Schlagbilder gegen Einheit und Gebäude, nicht untätig oder gegen Tote; Blick zum Gegner links und rechts");
}

// Zielwechsel: ein Soldat bekämpft den ersten Gegner, und wenn der stirbt,
// fällt er automatisch auf den nächsten um, statt untätig zu stehen
// (Nutzer 2026-10-11: „die müssen auch automatisch angegriffen werden.
// Der Soldat darf dann nach dem ersten nicht einfach aufhören solange
// noch Gegner da sind").
static void Zielwechsel(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Zielwechsel";
    int vorher = verstoesse.Count;
    var w = new Welt();
    w.Set("cameraZoom", 1f);
    w.Set("cameraPosition", Vector2.Zero);
    var dorf = w.Dorfbewohner().ToList();
    int T = w.Map.TileSize;

    IEnumerable<Vector2> Kacheln() => Enumerable.Range(0, w.Map.Width)
        .SelectMany(x => Enumerable.Range(0, w.Map.Height).Select(y => new Vector2(x, y)));
    var start = w.Map.WorldToGrid(dorf[0].Position);
    Unit Neu(UnitType typ, int besitzer, Vector2 kachel)
    {
        var u = w.Map.AddUnit(typ, (int)kachel.X, (int)kachel.Y, besitzer);
        (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
        return u;
    }

    // Der Angreifer (Miliz) steht nah an den Dorfbewohnern (deren Sicht
    /// trägt), A 3 Kacheln weiter, B zwei dahinter in derselben Richtung —
    // alles im 6-Kachel-Umzugsradius und im Dorfbewohner-Sichtfeld, damit
    // beides sichtbar ist.
    var milizPos = w.FreieKachel(dorf[0], 2);
    var aPos = Kacheln()
        .Where(c => c.Y >= 1 && c.Y < w.Map.Height - 1 && c.X >= 1 && c.X < w.Map.Width - 1
                    && w.Map.IsTileVisible((int)c.X, (int)c.Y, 0)
                    && w.Map.IsWalkable((int)c.X, (int)c.Y)
                    && string.IsNullOrEmpty(w.Map.GetTile((int)c.X, (int)c.Y).Building)
                    && Math.Abs(Vector2.Distance(c, milizPos) - 3f) < 1f)
        .OrderBy(c => Vector2.Distance(c, milizPos) - 3f).First();
    var dir = (aPos - milizPos); float len = dir.Length(); dir = len > 0.5f ? dir / len : new Vector2(1, 0);
    var bPos = new Vector2((int)Math.Round(aPos.X + dir.X * 2.5f), (int)Math.Round(aPos.Y + dir.Y * 2.5f));
    var miliz = Neu(UnitType.Militia, 0, milizPos);
    var a = Neu(UnitType.Villager, 1, aPos);
    var b = Neu(UnitType.Villager, 1, bPos);
    w.Map.UpdateFogOfWarForPlayer(0, w.Map.Units);
    if (!w.Map.IsTileExplored((int)aPos.X, (int)aPos.Y, 0) || !w.Map.IsTileExplored((int)bPos.X, (int)bPos.Y, 0))
    {
        verstoesse.Add($"{wer}: Testaufbau - A oder B nicht sichtbar ({(int)aPos.X},{(int)aPos.Y} / {(int)bPos.X},{(int)bPos.Y})");
        return;
    }

    // A angreifen
    w.Call("AttackUnit", new List<Unit> { miliz }, a);

    // A muss sterben
    float tA = w.LaufeBis(() => a.Health <= 0 || !w.Map.Units.Contains(a), 60f);
    if (a.Health > 0 && w.Map.Units.Contains(a))
    {
        verstoesse.Add($"{wer}: A stirbt nach {tA:0} s nicht (LP {a.Health}/{a.MaxHealth})");
        return;
    }

    // Kurzes Fenster: direkt nach dem Tod von A muss der Angreifer bereits B
    // verfolgen (UnitTarget == B) – kein Idle-Zwischenzustand.
    w.LaufeBis(() => false, 0.5f);
    if (miliz.UnitTarget != b && miliz.State == UnitState.Idle)
        verstoesse.Add($"{wer}: nach Tod von A ist der Soldat untätig statt auf B zu schalten");

    // B muss folgen
    float tB = w.LaufeBis(() => b.Health <= 0 || !w.Map.Units.Contains(b), 60f);
    if (b.Health > 0 && w.Map.Units.Contains(b))
    {
        verstoesse.Add($"{wer}: B stirbt nach {tB:0} s nicht, obwohl der Soldat weiterkämpfen sollte (LP {b.Health}/{b.MaxHealth})");
    }
    else if (verstoesse.Count == vorher)
    {
        Console.WriteLine($"  ok  {wer}: A nach {tA:0} s besiegt, Soldat weicht auf B aus, B nach {tB:0} s ebenfalls besiegt");
    }
}

// Eine statische Methode des Bildschirms (Welt.Call findet nur Instanzmethoden)
static object Statisch(string name, params object[] args)
{
    var m = typeof(RTSGameplayScreen).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
        .FirstOrDefault(x => x.Name == name && x.GetParameters().Length == args.Length)
        ?? throw new InvalidOperationException(
            $"statische Methode RTSGameplayScreen.{name}({args.Length} Argumente) nicht gefunden - umbenannt?");
    return m.Invoke(null, args);
}

static Unit NeueEinheit(Welt w, UnitType typ, int besitzer, Vector2 kachel)
{
    var u = w.Map.AddUnit(typ, (int)kachel.X, (int)kachel.Y, besitzer);
    (besitzer == 0 ? w.P1 : w.P2).AddUnit(u);
    return u;
}

// Klangbrett (Nutzer 2026-10-11: "die sound effekte sind scheinbar generiert aber noch nicht
// eingebaut"): die Drossel des SoundBoard lässt jedes Geräusch höchstens alle MIN_GAP Sekunden
// zu, jedes für sich, und ein abgelehnter Versuch verschiebt sie nicht. Ohne geladene Datei
// bleibt Play stumm und belegt die Drossel nicht. Ob die Dateien wirklich laden, zeigt der
// Start des Spiels (checks/s_klang.py) - ohne Spielfenster gibt es kein Audiogerät.
static void Klangbrett(List<string> verstoesse)
{
    const string wer = "Klangbrett";
    int vorher = verstoesse.Count;
    var brett = new SoundBoard(new Random(1));

    if (brett.Count("axt") != 0 || brett.Count("gibt es nicht") != 0)
        verstoesse.Add($"{wer}: ohne Laden meldet Count schon Varianten (axt {brett.Count("axt")})");
    if (brett.Play("axt", 1f, 0f, 0.0))
        verstoesse.Add($"{wer}: Play ohne geladene Datei meldet abgespielt");
    if (!brett.Allow("axt", 0.0))
        verstoesse.Add($"{wer}: nach einem stummen Play ist axt gedrosselt - Play darf die Drossel erst nach der Datei befragen");
    if (brett.Allow("axt", 0.03))
        verstoesse.Add($"{wer}: axt 0,03 s nach dem letzten erlaubt - MIN_GAP ist {SoundBoard.MIN_GAP} s");
    if (!brett.Allow("hammer", 0.03))
        verstoesse.Add($"{wer}: hammer 0,03 s nach axt gedrosselt - jedes Geräusch hat seine eigene Drossel");
    // 0,03 wurde abgelehnt: die Drossel zählt weiter ab 0,0, also ist 0,08 erlaubt
    if (!brett.Allow("axt", 0.08))
        verstoesse.Add($"{wer}: axt 0,08 s nach dem letzten erlaubten abgelehnt - verschiebt ein abgelehnter Versuch die Drossel?");
    if (brett.Allow("axt", 0.10))
        verstoesse.Add($"{wer}: axt 0,02 s nach dem letzten erlaubt");

    // Jedes Bild ein Versuch: durch kommt, was MIN_GAP nach dem letzten Durchgelassenen liegt
    var neu = new SoundBoard(new Random(2));
    int durch = 0, soll = 0;
    double letzte = double.NegativeInfinity;
    for (int i = 0; i < 60; i++)
    {
        if (neu.Allow("tod", i / 60.0))
            durch++;
        if (i / 60.0 - letzte >= SoundBoard.MIN_GAP)
        {
            soll++;
            letzte = i / 60.0;
        }
    }
    if (durch != soll)
        verstoesse.Add($"{wer}: bei 60 Versuchen in einer Sekunde kommen {durch} durch, erwartet {soll} (Abstand {SoundBoard.MIN_GAP} s)");

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: ohne Datei stumm, Drossel {SoundBoard.MIN_GAP} s je Geräusch ({soll} je Sekunde), abgelehnte Versuche verschieben sie nicht");
}

// Arbeitsklang: ein arbeitender Dorfbewohner klingt genau dann, wenn sein Werkzeug in der
// Animation aufschlägt (StrikeLanded im Takt von SwingAngle) - der Holzfäller mit der Axt, der
// Bauarbeiter mit dem Hammer; welches Geräusch, hängt an Rohstoff und Quelle (WorkSoundFor).
// Hörweite und Stereo-Lage rechnet SoundMix aus dem Bildausschnitt.
static void Arbeitsklang(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Arbeitsklang";
    int vorher = verstoesse.Count;
    const float DT = 1f / 60f;
    var gathering = AoE.Core.Economy.GatherPhase.Gathering;
    float Rate(string werkzeug)
    {
        const BindingFlags S = BindingFlags.NonPublic | BindingFlags.Static;
        var typ = typeof(RTSGameplayScreen);
        var stile = (System.Collections.IDictionary)(typ.GetField("ToolStyles", S)?.GetValue(null)
            ?? throw new InvalidOperationException("Feld RTSGameplayScreen.ToolStyles nicht gefunden - umbenannt?"));
        var stil = stile[Enum.Parse(typ.GetNestedType("Tool", BindingFlags.NonPublic)!, werkzeug)]!;
        return (float)stil.GetType().GetProperty("Rate")!.GetValue(stil)!;
    }

    if (karte == 1)
    {
        // Der Aufschlag: dort, wo t * rate eine ganze Zahl erreicht
        bool Schlag(float t, float dt, float rate) => (bool)Statisch("StrikeLanded", t, dt, rate);
        if (!Schlag(2.01f, 0.02f, 1f))
            verstoesse.Add($"{wer}: StrikeLanded(2,01; 0,02; 1) ist false - zwischen 1,99 und 2,01 schlägt das Werkzeug auf");
        if (Schlag(2.5f, 0.02f, 1f))
            verstoesse.Add($"{wer}: StrikeLanded(2,5; 0,02; 1) ist true - mitten im Takt schlägt nichts auf");
        if (Schlag(2.01f, 0f, 1f))
            verstoesse.Add($"{wer}: StrikeLanded mit dt 0 ist true");
        if (!Schlag(0.63f, 0.02f, 1.6f))
            verstoesse.Add($"{wer}: StrikeLanded(0,63; 0,02; 1,6) ist false - bei 1,6 Schlägen je Sekunde trifft er bei 0,625 auf");
        foreach (var rate in new[] { 0.9f, 1f, 1.3f, 1.6f })
        {
            const float t0 = 0.37f;
            int zahl = Enumerable.Range(1, 600).Count(i => Schlag(t0 + i * DT, DT, rate));
            int soll = (int)MathF.Floor((t0 + 600 * DT) * rate) - (int)MathF.Floor(t0 * rate);
            if (Math.Abs(zahl - soll) > 1)
                verstoesse.Add($"{wer}: in 10 s bei {rate} Schlägen je Sekunde {zahl} Aufschläge, erwartet {soll}");
        }

        // Hörweite und Lage: Ausschnitt 800 x 600 Weltpixel, Ausklingen über 256 px
        float breite = (float)(typeof(RTSGameplayScreen).GetField("SOUND_PAN_WIDTH", BindingFlags.NonPublic | BindingFlags.Static)?.GetValue(null)
            ?? throw new InvalidOperationException("Konstante RTSGameplayScreen.SOUND_PAN_WIDTH nicht gefunden - umbenannt?"));
        (float, float) Mix(float x, float y, float x0 = 0, float y0 = 0, float x1 = 800, float y1 = 600)
            => ((float, float))Statisch("SoundMix", new Vector2(x, y), new Vector2(x0, y0), new Vector2(x1, y1), 256f);
        void ErwarteMix(string fall, (float, float) ist, float lautstaerke, float lage)
        {
            if (Math.Abs(ist.Item1 - lautstaerke) > 0.01f || Math.Abs(ist.Item2 - lage) > 0.01f)
                verstoesse.Add($"{wer}: SoundMix {fall}: Lautstärke {ist.Item1:0.00}, Lage {ist.Item2:0.00} - erwartet {lautstaerke:0.00}, {lage:0.00}");
        }
        ErwarteMix("in der Bildmitte", Mix(400, 300), 1f, 0f);
        ErwarteMix("am linken Rand", Mix(0, 300), 1f, -breite);
        ErwarteMix("am rechten Rand", Mix(800, 300), 1f, breite);
        ErwarteMix("halb rechts", Mix(600, 300), 1f, breite / 2f);
        ErwarteMix("128 px rechts neben dem Bild", Mix(928, 300), 0.5f, breite);
        ErwarteMix("128 px über dem Bild", Mix(400, -128), 0.5f, 0f);
        ErwarteMix("schräg vor der Ecke oben links (96 und 72 px)", Mix(-96, -72), 1f - 120f / 256f, -breite);
        ErwarteMix("weit rechts außerhalb", Mix(1100, 300), 0f, breite);
        ErwarteMix("bei einem Ausschnitt ohne Breite", Mix(100, 100, 100, 100, 100, 100), 1f, 0f);
    }

    // Ein Klick sammelt nur an erkundeten Kacheln, und zu Beginn ist kein Baum erkundet -
    // wie bei den Türen sieht dieser Ablauf die ganze Karte
    TileMap.TestNoFog = true;
    try
    {
        var w = new Welt();
        w.Set("cameraZoom", 1f);
        w.Set("cameraPosition", Vector2.Zero);
        var log = new List<(string Name, Vector2 Position)>();
        w.Set("SoundLog", log);
        int gesehen = 0;
        bool Laeuft(Unit u)
        {
            var bewegung = (System.Collections.IDictionary)w.Get("_unitMotion");
            if (!bewegung.Contains(u))
                return false;
            var m = bewegung[u]!;
            return (float)m.GetType().GetField("MovingFor")!.GetValue(m)! > 0f;
        }
        // Ein Bild wie in Update: Spiellogik, Bewegung, dann die Werkzeugschläge
        void Bild(Action danach = null)
        {
            w.LaufeBis(() => false, DT);
            w.Call("UpdateUnitMotion", DT);
            w.Call("UpdateWorkSounds", DT);
            danach?.Invoke();
            gesehen = log.Count;
        }
        // Neue Geräusche dieses Bilds mit dem Namen name: Ort bei u und im Takt der Animation?
        int abseits = 0, ausDemTakt = 0;
        void PruefeSchlaege(string name, Unit u, float rate)
        {
            foreach (var e in log.Skip(gesehen).Where(e => e.Name == name))
            {
                if (Vector2.Distance(e.Position, u.Position) > 1f)
                    abseits++;
                float phase = ((float)w.Get("animationTime") + (u.GetHashCode() & 0xFF) / 40f) * rate % 1f;
                if (phase > rate * DT + 0.001f)
                    ausDemTakt++;
            }
        }

        // Ein Holzfäller am nächsten Baum
        var holz = w.Dorfbewohner().First();
        var start = w.Map.WorldToGrid(holz.Position);
        Vector2? baum = null;
        float best = float.MaxValue;
        for (int x = 0; x < w.Map.Width; x++)
            for (int y = 0; y < w.Map.Height; y++)
            {
                var t = w.Map.GetTile(x, y);
                if (t?.ResourceType != Resource.Wood || t.ResourceAmount <= 0)
                    continue;
                float d = Vector2.Distance(start, new Vector2(x, y));
                if (d < best && Enumerable.Range(-1, 3).Any(dx => w.Map.IsWalkable(x + dx, y)))
                {
                    best = d;
                    baum = new Vector2(x, y);
                }
            }
        w.Waehle(new List<Unit> { holz });
        w.Linksklick(w.Map.GridToWorld(baum ?? throw new InvalidOperationException("kein Wald")));
        for (int i = 0; i < 40 * 60 && holz.Job?.Phase != gathering; i++)
            Bild();
        if (holz.Job?.Phase != gathering)
        {
            verstoesse.Add($"{wer}: Testaufbau - der Holzfäller erreicht in 40 s keinen Baum ({holz.State})");
            return;
        }
        float axtRate = Rate("Axe");
        log.Clear();
        gesehen = 0;
        int hackt = 0;
        for (int i = 0; i < 6 * 60; i++)
            Bild(() =>
            {
                if (holz.Job?.Phase == gathering && !Laeuft(holz))
                    hackt++;
                PruefeSchlaege("axt", holz, axtRate);
            });
        int axt = log.Count(e => e.Name == "axt");
        if (hackt < 3 * 60)
            verstoesse.Add($"{wer}: Testaufbau - der Holzfäller hackt in 6 s nur {hackt / 60f:0.0} s");
        else if (Math.Abs(axt - hackt / 60f * axtRate) > 2f)
            verstoesse.Add($"{wer}: der Holzfäller hackt {hackt / 60f:0.0} s bei {axtRate} Schlägen je Sekunde - {axt} Axtschläge zu hören");
        if (log.Any(e => e.Name != "axt"))
            verstoesse.Add($"{wer}: außer dem Holzfäller arbeitet niemand - trotzdem zu hören: {string.Join(", ", log.Where(e => e.Name != "axt").Select(e => e.Name).Distinct())}");

        // Ein Bauarbeiter am Haus; der Holzfäller hört auf
        holz.Job = null;
        holz.Path.Clear();
        holz.State = UnitState.Idle;
        var bauer = w.Dorfbewohner().Skip(1).First();
        w.Waehle(new List<Unit> { bauer });
        var platz = w.Bauplatz(BuildingType.House, bauer, 3);
        w.Call("PlaceBuilding", BuildingType.House, platz);
        var haus = w.Map.Buildings.Last();
        float hammerRate = Rate("Hammer");
        log.Clear();
        gesehen = 0;
        int baut = 0;
        for (int i = 0; i < 90 * 60 && !haus.IsComplete; i++)
            Bild(() =>
            {
                if (bauer.State == UnitState.Building && !Laeuft(bauer))
                    baut++;
                PruefeSchlaege("hammer", bauer, hammerRate);
            });
        int hammer = log.Count(e => e.Name == "hammer");
        if (!haus.IsComplete || baut < 3 * 60)
            verstoesse.Add($"{wer}: Testaufbau - das Haus wird in 90 s nicht fertig oder er baut nur {baut / 60f:0.0} s");
        else if (Math.Abs(hammer - baut / 60f * hammerRate) > 2f)
            verstoesse.Add($"{wer}: der Bauarbeiter baut {baut / 60f:0.0} s bei {hammerRate} Schlägen je Sekunde - {hammer} Hammerschläge zu hören");
        if (log.Any(e => e.Name != "hammer"))
            verstoesse.Add($"{wer}: nur der Bauarbeiter arbeitet - trotzdem zu hören: {string.Join(", ", log.Where(e => e.Name != "hammer").Select(e => e.Name).Distinct())}");
        log.Clear();
        gesehen = 0;
        for (int i = 0; i < 3 * 60; i++)
            Bild();
        if (log.Count > 0)
            verstoesse.Add($"{wer}: das Haus ist fertig, niemand arbeitet - trotzdem zu hören: {string.Join(", ", log.Select(e => e.Name).Distinct())}");
        if (abseits > 0)
            verstoesse.Add($"{wer}: {abseits} Werkzeugschläge erklingen nicht am Platz des Arbeiters");
        if (ausDemTakt > 0)
            verstoesse.Add($"{wer}: {ausDemTakt} Werkzeugschläge nicht im Takt der Animation (Aufschlag am Ende des Takts, animationTime wie in DrawVillager)");

        // Welches Geräusch zu welcher Arbeit (WorkSoundFor)
        var dorf = w.Dorfbewohner().Last();
        var welt = (AoE.Core.Economy.IGatherWorld)w.Get("gatherWorld");
        void ErwarteKlang(Unit u, string arbeit, string soll)
        {
            var ist = (string)w.Call("WorkSoundFor", u);
            if (ist != soll)
                verstoesse.Add($"{wer}: {arbeit} klingt {(ist == null ? "nicht" : $"nach {ist}")}, erwartet {soll ?? "Stille"}");
        }
        Tile Kachel(Func<Tile, bool> passt)
        {
            for (int x = 0; x < w.Map.Width; x++)
                for (int y = 0; y < w.Map.Height; y++)
                    if (w.Map.GetTile(x, y) is { } k && k.ResourceAmount > 0 && passt(k))
                        return k;
            return null;
        }
        dorf.Job = null;
        dorf.State = UnitState.Idle;
        ErwarteKlang(dorf, "ein untätiger Dorfbewohner", null);
        dorf.State = UnitState.Building;
        ErwarteKlang(dorf, "ein Dorfbewohner am Bau", "hammer");
        dorf.State = UnitState.Gathering;
        w.Map.PlantCrop(1, 1, 3);
        foreach (var (fall, rohstoff, quelle, soll) in new (string, Resource, FoodSource?, string)[]
                 {
                     ("Holz", Resource.Wood, null, "axt"), ("Stein", Resource.Stone, null, "spitzhacke"),
                     ("Gold", Resource.Gold, null, "spitzhacke"), ("dem Feld", Resource.Food, FoodSource.Farm, "erde"),
                     ("dem Beerenbusch", Resource.Food, FoodSource.Berries, "rascheln"),
                     ("dem Fischgrund", Resource.Food, FoodSource.Fish, null),
                 })
        {
            var k = Kachel(t => quelle == null ? t.ResourceType == rohstoff : t.Food == quelle);
            if (k == null)
                continue;   // nicht jede Karte hat Beeren oder Fische
            var job = new GatherJob(0, rohstoff, new CorePosition(k.X, k.Y));
            dorf.Job = job;
            ErwarteKlang(dorf, $"der Weg zu {fall}", null);
            job.Arrive(welt);
            if (job.Phase != gathering)
            {
                verstoesse.Add($"{wer}: Testaufbau - an {fall} ({k.X},{k.Y}) beginnt die Sammelphase nicht ({job.Phase})");
                continue;
            }
            ErwarteKlang(dorf, $"die Arbeit an {fall}", soll);
        }
        var miliz = NeueEinheit(w, UnitType.Militia, 0, w.FreieKachel(dorf, 3));
        miliz.State = UnitState.Building;
        ErwarteKlang(miliz, "eine Miliz im Zustand Building", null);

        if (verstoesse.Count == vorher)
            Console.WriteLine($"  ok  {wer}: {axt} Axtschläge in {hackt / 60f:0.0} s, {hammer} Hammerschläge in {baut / 60f:0.0} s, im Takt und am Platz; danach still; Geräusch je Arbeit stimmt");
    }
    finally
    {
        TileMap.TestNoFog = false;
    }
}

// Kampfklang: jeder Treffer klingt - der Bogen beim Schützen, Hieb (treffer) und Klinge
// (schwert, zwei bewaffnete Nahkämpfer) beim Getroffenen, der Schlag an der Wand (gebaeude)
// beim Angreifer; wer stirbt, fällt hörbar (tod), ein zerstörtes Gebäude stürzt ein (einsturz).
static void Kampfklang(int karte, List<string> verstoesse)
{
    string wer = $"Karte {karte}, Kampfklang";
    int vorher = verstoesse.Count;

    // Welches Geräusch ein Treffer macht (UnitHitSound)
    {
        var w0 = new Welt();
        var p = w0.FreieKachel(w0.Dorfbewohner().First(), 4);
        Unit Neu0(UnitType typ, int besitzer) => w0.Map.AddUnit(typ, (int)p.X, (int)p.Y, besitzer);
        var miliz = Neu0(UnitType.Militia, 0);
        var gegner = Neu0(UnitType.Militia, 1);
        var schuetze = Neu0(UnitType.Archer, 0);
        var gegnerSchuetze = Neu0(UnitType.Archer, 1);
        var dorf = Neu0(UnitType.Villager, 1);
        var spaeher = Neu0(UnitType.Scout, 1);
        void ErwarteHieb(string fall, Unit a, Unit z, string soll)
        {
            var ist = (string)Statisch("UnitHitSound", a, z);
            if (ist != soll)
                verstoesse.Add($"{wer}: {fall} klingt nach {ist ?? "nichts"}, erwartet {soll}");
        }
        ErwarteHieb("Miliz gegen Dorfbewohner", miliz, dorf, "treffer");
        ErwarteHieb("Dorfbewohner gegen Miliz", dorf, miliz, "treffer");
        ErwarteHieb("Miliz gegen Miliz", miliz, gegner, "schwert");
        ErwarteHieb("Miliz gegen Späher", miliz, spaeher, "schwert");
        ErwarteHieb("Miliz gegen Bogenschütze", miliz, gegnerSchuetze, "treffer");
        ErwarteHieb("Bogenschütze gegen Miliz", schuetze, gegner, "bogen");
        ErwarteHieb("Bogenschütze gegen Dorfbewohner", schuetze, dorf, "bogen");
    }

    // Ein Zweikampf bis zum Tod von opfer: jeder Treffer - Lebenspunkte sinken - klingt genau
    // einmal nach erwartet, beim Getroffenen (beim Bogen: beim Schützen); opfer fällt einmal
    // hörbar, an seinem Sterbeort. Andere Geräusche als erwartet und tod darf es nicht geben.
    void Zweikampf(string fall, UnitType angreiferTyp, UnitType opferTyp, float abstand, string erwartet)
    {
        var w = new Welt();
        var log = new List<(string Name, Vector2 Position)>();
        w.Set("SoundLog", log);
        var platz = w.FreieKachel(w.Dorfbewohner().First(), 4);
        var angreifer = NeueEinheit(w, angreiferTyp, 0, platz);
        var opfer = NeueEinheit(w, opferTyp, 1, platz);
        opfer.Position = angreifer.Position - new Vector2(abstand, 0);
        w.Call("AttackUnit", new List<Unit> { angreifer }, opfer);

        int treffer = 0, gesehen = 0, abseits = 0;
        int lpA = angreifer.Health, lpO = opfer.Health;
        var sterbeort = opfer.Position;
        w.LaufeBis(() =>
        {
            bool aGetroffen = angreifer.Health < lpA, oGetroffen = opfer.Health < lpO;
            treffer += (aGetroffen ? 1 : 0) + (oGetroffen ? 1 : 0);
            foreach (var e in log.Skip(gesehen).Where(e => e.Name != "tod"))
            {
                // Der Bogen erklingt beim Schützen, Hieb und Klinge beim Getroffenen
                bool passt = e.Name == "bogen"
                    ? Vector2.Distance(e.Position, angreifer.Position) < 2f
                    : (aGetroffen && Vector2.Distance(e.Position, angreifer.Position) < 2f)
                      || (oGetroffen && Vector2.Distance(e.Position, opfer.Position) < 2f);
                if (!passt)
                    abseits++;
            }
            gesehen = log.Count;
            lpA = angreifer.Health;
            lpO = opfer.Health;
            if (w.Map.Units.Contains(opfer))
                sterbeort = opfer.Position;
            return !w.Map.Units.Contains(opfer) || !w.Map.Units.Contains(angreifer);
        }, 90f);

        if (w.Map.Units.Contains(opfer) && w.Map.Units.Contains(angreifer))
        {
            verstoesse.Add($"{wer}, {fall}: Testaufbau - nach 90 s ist keiner gefallen ({angreifer.State}, LP {opfer.Health}/{opfer.MaxHealth})");
            return;
        }
        int gehoert = log.Count(e => e.Name != "tod");
        if (treffer == 0)
            verstoesse.Add($"{wer}, {fall}: Testaufbau - kein Treffer beobachtet");
        if (gehoert != treffer)
            verstoesse.Add($"{wer}, {fall}: {treffer} Treffer, aber {gehoert} Treffergeräusche");
        var fremd = log.Select(e => e.Name).Where(n => n != "tod" && n != erwartet && !(erwartet == "bogen" && n == "treffer")).Distinct().ToList();
        if (fremd.Count > 0)
            verstoesse.Add($"{wer}, {fall}: zu hören ist {string.Join(", ", fremd)}, erwartet {erwartet}");
        if (log.Count(e => e.Name == erwartet) == 0)
            verstoesse.Add($"{wer}, {fall}: kein einziges Mal {erwartet} zu hören");
        if (abseits > 0)
            verstoesse.Add($"{wer}, {fall}: {abseits} Treffergeräusche nicht am richtigen Ort (Bogen beim Schützen, Hiebe beim Getroffenen)");
        var tod = log.Where(e => e.Name == "tod").ToList();
        if (tod.Count != 1)
            verstoesse.Add($"{wer}, {fall}: einer fällt - {tod.Count}-mal tod zu hören, erwartet einmal");
        else if (!w.Map.Units.Contains(opfer) && Vector2.Distance(tod[0].Position, sterbeort) > 2f)
            verstoesse.Add($"{wer}, {fall}: tod erklingt {Vector2.Distance(tod[0].Position, sterbeort):0} px vom Sterbeort");
    }
    Zweikampf("Miliz gegen Dorfbewohner", UnitType.Militia, UnitType.Villager, 20f, "treffer");
    Zweikampf("Miliz gegen Miliz", UnitType.Militia, UnitType.Militia, 20f, "schwert");
    // Der Dorfbewohner wehrt sich und schlägt den Schützen: dort klingt es nach treffer
    Zweikampf("Bogenschütze gegen Dorfbewohner", UnitType.Archer, UnitType.Villager, 3f * 32f, "bogen");

    // Ein Gebäude: der Schlag an der Wand beim Angreifer, beim Bogenschützen der Bogen; das
    // letzte stürzt hörbar ein, in der Mitte seiner Grundfläche
    void Wand(string fall, UnitType typ, bool bisZumEinsturz)
    {
        var w = new Welt();
        var log = new List<(string Name, Vector2 Position)>();
        w.Set("SoundLog", log);
        int T = w.Map.TileSize;
        var dorf0 = w.Dorfbewohner().First();
        var platz = w.Bauplatz(BuildingType.House, dorf0, 6);
        var haus = w.Map.AddBuilding((int)platz.X, (int)platz.Y, "Haus", 1, 2);
        var mitte = new Vector2((haus.X + haus.Width / 2f) * T, (haus.Y + haus.Height / 2f) * T);
        var angreifer = NeueEinheit(w, typ, 0, w.FreieKachel(dorf0, 2));
        if (bisZumEinsturz)
            haus.Health = Math.Min(haus.Health, 12);
        w.Call("AttackBuilding", new List<Unit> { angreifer }, haus);
        string erwartet = typ == UnitType.Archer ? "bogen" : "gebaeude";

        int schlaege = 0, gesehen = 0, abseits = 0, lp = haus.Health;
        float dauer = w.LaufeBis(() =>
        {
            if (haus.Health < lp)
                schlaege++;
            foreach (var e in log.Skip(gesehen).Where(e => e.Name != "einsturz"))
                if (Vector2.Distance(e.Position, angreifer.Position) > 2f)
                    abseits++;
            gesehen = log.Count;
            lp = haus.Health;
            return bisZumEinsturz ? !w.Map.Buildings.Contains(haus) : schlaege >= 3;
        }, 120f);

        if (schlaege == 0 || (bisZumEinsturz && w.Map.Buildings.Contains(haus)))
        {
            verstoesse.Add($"{wer}, {fall}: Testaufbau - nach {dauer:0} s {schlaege} Schläge, Haus {(w.Map.Buildings.Contains(haus) ? "steht" : "fiel")} ({angreifer.State})");
            return;
        }
        int gehoert = log.Count(e => e.Name == erwartet);
        if (gehoert != schlaege)
            verstoesse.Add($"{wer}, {fall}: {schlaege} Schläge ans Haus, aber {gehoert}-mal {erwartet}");
        var fremd = log.Select(e => e.Name).Where(n => n != erwartet && n != "einsturz").Distinct().ToList();
        if (fremd.Count > 0)
            verstoesse.Add($"{wer}, {fall}: zu hören ist {string.Join(", ", fremd)}, erwartet {erwartet}");
        if (abseits > 0)
            verstoesse.Add($"{wer}, {fall}: {abseits} Schläge erklingen nicht beim Angreifer");
        var einsturz = log.Where(e => e.Name == "einsturz").ToList();
        if (!bisZumEinsturz && einsturz.Count > 0)
            verstoesse.Add($"{wer}, {fall}: das Haus steht noch - trotzdem einsturz zu hören");
        if (bisZumEinsturz && einsturz.Count != 1)
            verstoesse.Add($"{wer}, {fall}: das Haus fällt - {einsturz.Count}-mal einsturz zu hören, erwartet einmal");
        else if (bisZumEinsturz && Vector2.Distance(einsturz[0].Position, mitte) > 2f)
            verstoesse.Add($"{wer}, {fall}: einsturz erklingt {Vector2.Distance(einsturz[0].Position, mitte):0} px von der Mitte des Hauses");
    }
    Wand("Miliz gegen Haus", UnitType.Militia, true);
    Wand("Bogenschütze gegen Haus", UnitType.Archer, false);

    if (verstoesse.Count == vorher)
        Console.WriteLine($"  ok  {wer}: Hieb, Klinge und Bogen je Treffer am richtigen Ort, Schlag an der Wand, je ein tod und einsturz");
}

class Welt
{
    const BindingFlags F = BindingFlags.NonPublic | BindingFlags.Instance;
    static readonly Type T = typeof(RTSGameplayScreen);
    readonly RTSGameplayScreen _screen = new();
    public readonly TileMap Map;
    public readonly Player P1, P2;
    readonly List<Unit> _units;
    double _zeit, _nebel;

    public Welt(MapSize groesse = MapSize.Standard, float aspect = 1f)
    {
        P1 = new(0, "P1", "Briten");
        P2 = new(1, "P2", "Azteken");
        var dims = MapSizes.Dimensions(groesse, aspect);
        Map = new TileMap(dims.Width, dims.Height, 32, MapSettings.ForSize(groesse));
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

    /// <summary>
    /// Eine Welt aus einem Spielstand (SV3), so wie LoadContent sie beim Laden aufbaut:
    /// RestoreWorld, dann Spielerlisten, Sammelwelt, Einheitenliste, Sicht und Bevölkerung.
    /// </summary>
    public Welt(SaveGame stand)
    {
        Call("RestoreWorld", stand);
        Map = (TileMap)Get("tileMap");
        P1 = (Player)Get("player1");
        P2 = (Player)Get("player2");
        _units = Map.Units;
        foreach (var u in _units)
            (u.OwnerId == 0 ? P1 : P2).AddUnit(u);
        Set("gatherWorld", new TileMapGatherWorld(Map));
        Set("units", _units);
        Map.UpdateFogOfWarForPlayer(0, _units);
        Call("UpdatePopulationLimits");
    }

    /// <summary>Wert einer privaten Eigenschaft des Spielbildschirms (ZoomScale, FreeHeight).</summary>
    public object Prop(string name) => (T.GetProperty(name, F)
        ?? throw new InvalidOperationException($"Eigenschaft RTSGameplayScreen.{name} nicht gefunden - umbenannt?"))
        .GetValue(_screen);

    /// <summary>Wert einer Konstante des Spielbildschirms (HUD_TOP_HEIGHT).</summary>
    public static object Konstante(string name) => (T.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new InvalidOperationException($"Konstante RTSGameplayScreen.{name} nicht gefunden - umbenannt?"))
        .GetValue(null);

    // Seit C4b; fehlt sie, laufen die übrigen Gruppen trotzdem - die Gruppe
    // zeitalter verlangt sie ausdrücklich
    static readonly MethodInfo UpdateAges = T.GetMethod("UpdateAges", F);
    // Seit P2: die Forschungen in den Gebäuden
    static readonly MethodInfo UpdateResearch = T.GetMethod("UpdateResearch", F);

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
        // Methode nach Name UND Argumentanzahl auflösen: RTSGameplayScreen
        // hat seit der KI-Schnittstelle teils mehrere Overloads desselben Namens
        // (z. B. CanPlace/PlaceBuilding mit und ohne ownerId). GetMethod(name)
        // wirft bei mehreren Namens-Treffern AmbiguousMatchException.
        var m = T.GetMethods(F).Where(x => x.Name == name && x.GetParameters().Length == args.Length)
            .FirstOrDefault()
            ?? throw new InvalidOperationException(
                $"Methode RTSGameplayScreen.{name}({args.Length} Argumente) nicht gefunden - umbenannt?");
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
        // Wie im Spiel: Einheiten und Gebäude sind nie zugleich ausgewählt (B1)
        if (auswahl.Count > 0)
            T.GetField("selectedBuilding", F)?.SetValue(_screen, null);
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
            UpdateResearch?.Invoke(_screen, new object[] { dt });
            UpdateAges?.Invoke(_screen, new object[] { dt });
            Call("UpdateConstruction", dt);
            Call("UpdateRepair", dt);
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
