"""Verträge, Tests und Abnahmen für L1 (Kernregeln der neuen Gebäude) und L2
(Baumenü, zweite Tastenreihe, Bilder). Eine Quelle für Spiegel und Arbeitsbaum:

    py vertrag.py spiegel      # in die Scratchpad-Kopie
    py vertrag.py arbeitsbaum  # in D:/Apps/AgeOfEmpire
"""
import sys
from pathlib import Path

HIER = Path(__file__).resolve().parent
ZIEL = {"spiegel": HIER / "spiegel", "arbeitsbaum": Path("D:/Apps/AgeOfEmpire")}[sys.argv[1]]


def patch(rel, paare):
    p = ZIEL / rel
    t = p.read_bytes().decode("utf-8")
    bom = t.startswith("\ufeff")
    crlf = "\r\n" in t
    t = t.lstrip("\ufeff").replace("\r\n", "\n")
    for alt, neu in paare:
        assert t.count(alt) == 1, (rel, t.count(alt), alt[:80])
        t = t.replace(alt, neu)
    if crlf:
        t = t.replace("\n", "\r\n")
    p.write_bytes((("\ufeff" if bom else "") + t).encode("utf-8"))
    print("Vertrag:", rel)


# --- L1: BuildingRules -------------------------------------------------------
patch("src/AoE.Core/Economy/Construction.cs", [
    ("""    /// Kosten laut Spezifikation, bei jedem Aufruf ein neues Wörterbuch:
    /// Stadtzentrum 275 Holz und 100 Stein, Haus 25 Holz, Mühle, Holzfällerlager
    /// und Bergbaulager je 100 Holz, Farm 60 Holz, Kaserne 175 Holz, Wachturm
    /// 50 Holz und 125 Stein. Für alle anderen Typen ein leeres Wörterbuch.
""",
     """    /// Kosten laut Spezifikation, bei jedem Aufruf ein neues Wörterbuch:
    /// Stadtzentrum 275 Holz und 100 Stein, Haus 25 Holz, Mühle, Holzfällerlager
    /// und Bergbaulager je 100 Holz, Farm 60 Holz, Kaserne, Schießstand, Stall,
    /// Markt und Kloster je 175 Holz, Schmiede 150 Holz, Belagerungswerkstatt und
    /// Universität je 200 Holz, Burg 650 Stein, Wachturm 50 Holz und 125 Stein,
    /// Palisadenmauer 2 Holz, Steinmauer 5 Stein, Wunder je 1000 Holz, Stein und
    /// Gold. Damit kostet jeder Typ etwas; nur ein Wert außerhalb der Aufzählung
    /// ergäbe ein leeres Wörterbuch.
"""),
    ("""    /// Bauzeit in Sekunden mit einem einzigen Bauarbeiter: Haus 25, Mühle,
    /// Holzfällerlager und Bergbaulager je 35, Farm 15, Kaserne 50,
    /// Wachturm 80, Stadtzentrum 150. Für alle anderen Typen 60.
""",
     """    /// Bauzeit in Sekunden mit einem einzigen Bauarbeiter, Werte aus AoE II:
    /// Palisadenmauer 5, Steinmauer 8, Farm 15, Haus 25, Mühle, Holzfällerlager
    /// und Bergbaulager je 35, Schmiede, Kloster und Belagerungswerkstatt je 40,
    /// Kaserne, Schießstand und Stall je 50, Markt und Universität je 60,
    /// Wachturm 80, Stadtzentrum 150, Burg 200, Wunder 3500. Für einen Wert
    /// außerhalb der Aufzählung 60.
"""),
    ("""    /// Kantenlänge der quadratischen Grundfläche in Kacheln: Stadtzentrum 4,
    /// Farm und Kaserne 3, alle anderen 2.
""",
     """    /// Kantenlänge der quadratischen Grundfläche in Kacheln: Palisadenmauer und
    /// Steinmauer 1 (ein Mauerstück je Kachel), Haus, Mühle, Holzfällerlager,
    /// Bergbaulager und Wachturm 2, Farm, Kaserne, Schießstand, Stall, Schmiede
    /// und Kloster 3, Stadtzentrum, Markt, Belagerungswerkstatt, Universität und
    /// Burg 4, Wunder 5. Für einen Wert außerhalb der Aufzählung 2.
"""),
])

# --- L1: BuildingEntity.Create ------------------------------------------------
patch("src/AoE.Core/Entities/Buildings.cs", [
    ("""        return new BuildingEntity(BuildingType.Tower, ownerId, position, stats);
    }
}
""",
     """        return new BuildingEntity(BuildingType.Tower, ownerId, position, stats);
    }

    /// <summary>
    /// Ein Gebäude beliebigen Typs - das Spiel setzt darüber jedes Gebäude des
    /// Baumenüs. Für Stadtzentrum, Haus, Mühle, Holzfällerlager, Bergbaulager,
    /// Kaserne und Wachturm genau die Werte der Create-Methode des Typs (sie
    /// aufrufen). Für die übrigen Typen gelten Werte nach AoE II; BaseAttack,
    /// Range und Speed sind 0, wo nichts anderes steht:
    ///
    ///   Typ             HitPoints  BaseArmor  VisionRange
    ///   ArcheryRange       1500        1           5
    ///   Stable             1500        1           5
    ///   Blacksmith         1800        1           5
    ///   Market             2100        1           6
    ///   SiegeWorkshop      2100        1           5
    ///   University         2100        1           6
    ///   Monastery          2100        1           6
    ///   Castle             4800        8          11     BaseAttack 11, Range 8
    ///   PalisadeWall        250        2           2
    ///   StoneWall          1800        8           2
    ///   Wonder             4800        3           8
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">
    /// <paramref name="type"/> ist <see cref="BuildingType.Farm"/> - eine Farm ist
    /// ein Feld, kein Gebäude - oder kein Wert der Aufzählung.
    /// </exception>
    public static BuildingEntity Create(BuildingType type, int ownerId, Position position)
    {
        throw new NotImplementedException();
    }
}
"""),
])

# --- L1: Tests -----------------------------------------------------------------
patch("tests/AoE.Tests/ConstructionTests.cs", [
    ("""        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 50, [Resource.Stone] = 125 },
                     BuildingRules.CostOf(BuildingType.Tower));
    }

    [Fact]
    public void Kosten_UnbekannterTyp_SindLeer()
    {
        Assert.Empty(BuildingRules.CostOf(BuildingType.Wonder));
    }
""",
     """        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 50, [Resource.Stone] = 125 },
                     BuildingRules.CostOf(BuildingType.Tower));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.ArcheryRange));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Stable));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Market));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 175 }, BuildingRules.CostOf(BuildingType.Monastery));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 150 }, BuildingRules.CostOf(BuildingType.Blacksmith));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 200 }, BuildingRules.CostOf(BuildingType.SiegeWorkshop));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 200 }, BuildingRules.CostOf(BuildingType.University));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Stone] = 650 }, BuildingRules.CostOf(BuildingType.Castle));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 2 }, BuildingRules.CostOf(BuildingType.PalisadeWall));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Stone] = 5 }, BuildingRules.CostOf(BuildingType.StoneWall));
        Assert.Equal(new Dictionary<Resource, int> { [Resource.Wood] = 1000, [Resource.Stone] = 1000, [Resource.Gold] = 1000 },
                     BuildingRules.CostOf(BuildingType.Wonder));
    }

    [Fact]
    public void Kosten_JederTypKostetEtwas()
    {
        foreach (var typ in System.Enum.GetValues<BuildingType>())
            Assert.NotEmpty(BuildingRules.CostOf(typ));
    }
"""),
    ("""    [InlineData(BuildingType.Tower, 80f)]
    [InlineData(BuildingType.Wonder, 60f)]
""",
     """    [InlineData(BuildingType.Tower, 80f)]
    [InlineData(BuildingType.PalisadeWall, 5f)]
    [InlineData(BuildingType.StoneWall, 8f)]
    [InlineData(BuildingType.Blacksmith, 40f)]
    [InlineData(BuildingType.Monastery, 40f)]
    [InlineData(BuildingType.SiegeWorkshop, 40f)]
    [InlineData(BuildingType.ArcheryRange, 50f)]
    [InlineData(BuildingType.Stable, 50f)]
    [InlineData(BuildingType.Market, 60f)]
    [InlineData(BuildingType.University, 60f)]
    [InlineData(BuildingType.Castle, 200f)]
    [InlineData(BuildingType.Wonder, 3500f)]
"""),
    ("""    [InlineData(BuildingType.Tower, 2)]
    public void Groesse_InKacheln(BuildingType typ, int kanten)
""",
     """    [InlineData(BuildingType.Tower, 2)]
    [InlineData(BuildingType.PalisadeWall, 1)]
    [InlineData(BuildingType.StoneWall, 1)]
    [InlineData(BuildingType.ArcheryRange, 3)]
    [InlineData(BuildingType.Stable, 3)]
    [InlineData(BuildingType.Blacksmith, 3)]
    [InlineData(BuildingType.Monastery, 3)]
    [InlineData(BuildingType.Market, 4)]
    [InlineData(BuildingType.SiegeWorkshop, 4)]
    [InlineData(BuildingType.University, 4)]
    [InlineData(BuildingType.Castle, 4)]
    [InlineData(BuildingType.Wonder, 5)]
    public void Groesse_InKacheln(BuildingType typ, int kanten)
"""),
    ("""    [Fact]
    public void Wachturm_SiehtZehnKachelnWeit()
""",
     """    [Theory]
    [InlineData(BuildingType.ArcheryRange, 1500, 1, 5)]
    [InlineData(BuildingType.Stable, 1500, 1, 5)]
    [InlineData(BuildingType.Blacksmith, 1800, 1, 5)]
    [InlineData(BuildingType.Market, 2100, 1, 6)]
    [InlineData(BuildingType.SiegeWorkshop, 2100, 1, 5)]
    [InlineData(BuildingType.University, 2100, 1, 6)]
    [InlineData(BuildingType.Monastery, 2100, 1, 6)]
    [InlineData(BuildingType.Castle, 4800, 8, 11)]
    [InlineData(BuildingType.PalisadeWall, 250, 2, 2)]
    [InlineData(BuildingType.StoneWall, 1800, 8, 2)]
    [InlineData(BuildingType.Wonder, 4800, 3, 8)]
    public void Erzeugen_NeueTypen_WerteNachAoE2(BuildingType typ, int lebenspunkte, int ruestung, int sicht)
    {
        var gebaeude = BuildingEntity.Create(typ, 1, new Position(7, 8));

        Assert.Equal(typ, gebaeude.BuildingType);
        Assert.Equal(1, gebaeude.OwnerId);
        Assert.Equal(new Position(7, 8), gebaeude.Position);
        Assert.Equal(lebenspunkte, gebaeude.Stats.HitPoints);
        Assert.Equal(ruestung, gebaeude.Stats.BaseArmor);
        Assert.Equal(sicht, gebaeude.Stats.VisionRange);
    }

    [Fact]
    public void Erzeugen_Burg_SchiesstWieImOriginal()
    {
        var burg = BuildingEntity.Create(BuildingType.Castle, 0, new Position(3, 3));
        Assert.Equal(11, burg.Stats.BaseAttack);
        Assert.Equal(8, burg.Stats.Range);
    }

    [Fact]
    public void Erzeugen_BisherigeTypen_WieIhreCreateMethode()
    {
        var ort = new Position(4, 5);
        var paare = new (BuildingType Typ, BuildingEntity Bisher)[]
        {
            (BuildingType.TownCenter, BuildingEntity.CreateTownCenter(0, ort)),
            (BuildingType.House, BuildingEntity.CreateHouse(0, ort)),
            (BuildingType.Mill, BuildingEntity.CreateMill(0, ort)),
            (BuildingType.LumberCamp, BuildingEntity.CreateLumberCamp(0, ort)),
            (BuildingType.MiningCamp, BuildingEntity.CreateMiningCamp(0, ort)),
            (BuildingType.Barracks, BuildingEntity.CreateBarracks(0, ort)),
            (BuildingType.Tower, BuildingEntity.CreateTower(0, ort)),
        };
        foreach (var (typ, bisher) in paare)
        {
            var neu = BuildingEntity.Create(typ, 0, ort);
            Assert.Equal(typ, neu.BuildingType);
            Assert.Equal(bisher.Stats.HitPoints, neu.Stats.HitPoints);
            Assert.Equal(bisher.Stats.BaseArmor, neu.Stats.BaseArmor);
            Assert.Equal(bisher.Stats.VisionRange, neu.Stats.VisionRange);
            Assert.Equal(bisher.DropOffType, neu.DropOffType);
        }
    }

    [Fact]
    public void Erzeugen_Farm_IstKeinGebaeude()
    {
        Assert.Throws<System.ArgumentOutOfRangeException>(
            () => BuildingEntity.Create(BuildingType.Farm, 0, new Position(1, 1)));
    }

    [Fact]
    public void Wachturm_SiehtZehnKachelnWeit()
"""),
])

# --- L2: CoreBuildings ----------------------------------------------------------
patch("AgeOfEvolutions/AgeOfEvolutions.Core/Data/CoreBuildings.cs", [
    ("""    /// 175-Nahrungsquelle (siehe <c>TileMap.PlantFarm</c>) und wächst nach.
    /// </summary>
""",
     """    /// 175-Nahrungsquelle (siehe <c>TileMap.PlantFarm</c>) und wächst nach.
    /// Die Gebäude der zweiten Tastenreihe entstehen über
    /// <see cref="BuildingEntity.Create"/>: „Schießstand" ArcheryRange, „Stall"
    /// Stable, „Schmiede" Blacksmith, „Markt" Market, „Palisadenmauer"
    /// PalisadeWall, „Steinmauer" StoneWall, „Belagerungswerkstatt"
    /// SiegeWorkshop, „Universität" University, „Kloster" Monastery, „Burg"
    /// Castle, „Wunder" Wonder.
    /// </summary>
"""),
])

# --- L2: Ablaufprüfung neubauten --------------------------------------------------
NEUBAUTEN = r'''
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
        var obenSoll = new[] { "Q", "A", "H", "M", "F", "B", "G", "T", "." }
            .Where(l => (l != "A" || age != Age.Imperial) && (l != "T" || age >= Age.Feudal)).ToArray();
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
'''

patch("tools/spielablauf/Program.cs", [
    ("// Jede genannte Gruppe läuft auf drei frisch erzeugten Karten.",
     "// Dazu neubauten (Gebäude der zweiten Tastenreihe).\n//\n// Jede genannte Gruppe läuft auf drei frisch erzeugten Karten."),
    ("""    if (gruppen.Contains("turm"))
        Pruefe($"Karte {karte}, Wachturm", () => Turm(karte, verstoesse));
""",
     """    if (gruppen.Contains("turm"))
        Pruefe($"Karte {karte}, Wachturm", () => Turm(karte, verstoesse));
    if (gruppen.Contains("neubauten"))
        Pruefe($"Karte {karte}, Neubauten", () => Neubauten(karte, verstoesse));
"""),
    ("""// Kartengrößen (C11): Standard 64""",
     NEUBAUTEN.lstrip("\n") + "\n// Kartengrößen (C11): Standard 64"),
])
