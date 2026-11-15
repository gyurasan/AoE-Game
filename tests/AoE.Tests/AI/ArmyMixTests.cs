using System.Linq;
using AoE.Core.Ai;
using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Gemischte Armee je Stufe (Nutzer 2026-11-07): Sparta bildet Nahkämpfer, Bogenschützen und
/// Reiter nach Anteilen aus und baut einen Stall, Defense mischt Bogenschützen bei und baut
/// Wachtürme, Normal bleibt wie bisher (nur Milizen, solange eine Kaserne steht).
/// </summary>
public class ArmyMixTests
{
    private static FakeWorld Welt(Age age, bool range = true, bool stable = true, bool workshop = false)
    {
        var w = new FakeWorld { Age = age, PopulationCount = 10, PopulationCapacity = 60,
                                Resources = new ResourceVector(3000, 3000, 3000, 3000) };
        w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
        w.AddBuilding(BuildingType.Barracks, 20, 10, complete: true);
        if (range) w.AddBuilding(BuildingType.ArcheryRange, 10, 20, complete: true);
        if (stable) w.AddBuilding(BuildingType.Stable, 20, 20, complete: true);
        if (workshop) w.AddBuilding(BuildingType.SiegeWorkshop, 25, 25, complete: true);
        return w;
    }

    private static void Tick(AiProfile p, FakeWorld w) => new EconomyAi(p).Tick(w, w, 0.5f, new AiContext(1));

    private const string Miliz = "soldier(Barracks,Militia)";
    private const string Bogen = "soldier(ArcheryRange,Archer)";
    private const string Reiter = "soldier(Stable,Cavalry)";
    private const string Katapult = "soldier(SiegeWorkshop,Catapult)";

    [Fact]
    public void Sparta_bildet_alle_drei_Arten_aus_nach_Fehlbetrag()
    {
        var w = Welt(Age.Feudal);
        Tick(AiProfile.Sparta, w);
        // ohne Soldaten: Fehlbetrag 0,25 / 0,35 / 0,25 (F15: Nahkampf-Anteil 25 %)
        // → Bogen zuerst, dann Miliz und Reiter (Gleichstand in Kandidatenreihenfolge)
        int b = w.Log.IndexOf(Bogen), m = w.Log.IndexOf(Miliz), r = w.Log.IndexOf(Reiter);
        Assert.True(b >= 0 && m > b && r > m, string.Join(" ", w.Log));
    }

    [Fact]
    public void Sparta_fuellt_die_fehlenden_Arten_auf()
    {
        var w = Welt(Age.Feudal);
        for (int i = 0; i < 6; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, w);
        // 6 Nahkämpfer: Miliz über dem Anteil; Bogen 0,35·7 = 2,45 vor Reiter 0,25·7 = 1,75
        Assert.DoesNotContain(Miliz, w.Log);
        int b = w.Log.IndexOf(Bogen), r = w.Log.IndexOf(Reiter);
        Assert.True(b >= 0 && r > b, string.Join(" ", w.Log));
    }

    [Fact]
    public void Sparta_Reiter_vor_Bogen_wenn_Reiter_weiter_unter_Anteil()
    {
        var w = Welt(Age.Feudal);
        for (int i = 0; i < 4; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        for (int i = 0; i < 4; i++) w.AddSoldier(30, 30, UnitKind.Archer);
        Tick(AiProfile.Sparta, w);
        // total 8: Miliz 0,4·9 − 4 < 0, Bogen 0,35·9 − 4 < 0, Reiter 0,25·9 = 2,25 → nur Reiter
        Assert.Contains(Reiter, w.Log);
        Assert.DoesNotContain(Bogen, w.Log);
        Assert.DoesNotContain(Miliz, w.Log);
    }

    [Fact]
    public void Sparta_in_der_Dunklen_Zeit_nur_Miliz()
    {
        var w = Welt(Age.Dark);
        for (int i = 0; i < 4; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, w);
        // Schießstand und Stall bilden erst ab der Feudalzeit aus; kein Fehlbetrag → Kaserne bildet weiter aus
        Assert.Contains(Miliz, w.Log);
        Assert.DoesNotContain(Bogen, w.Log);
        Assert.DoesNotContain(Reiter, w.Log);
    }

    [Fact]
    public void Sparta_in_der_Dunklen_Zeit_Milizen_nur_bis_zu_ihrem_Anteil_am_Soll()
    {
        // F15: Anteil Nahkampf 25 % von 20 = 5, doch der Deckel liegt nie unter der
        // Aufstieg-Mindestarmee (6) — sonst kommt Sparta aus der Dunklen Zeit nie weg.
        // Bei 8 Milizen wartet Sparta auf Schießstand, Stall und Werkstatt der späteren
        // Zeitalter, statt das Soll mit Milizen zu füllen.
        var w = Welt(Age.Dark);
        for (int i = 0; i < 8; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, w);
        Assert.DoesNotContain(w.Log, l => l.StartsWith("soldier("));

        var sechs = Welt(Age.Dark);
        for (int i = 0; i < 6; i++) sechs.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, sechs);
        Assert.DoesNotContain(sechs.Log, l => l.StartsWith("soldier("));

        var vier = Welt(Age.Dark);
        for (int i = 0; i < 4; i++) vier.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, vier);
        Assert.Contains(Miliz, vier.Log);

        // 5 = über dem Anteil (5), aber unter dem Aufstieg-Deckel (6): Miliz bildet weiter aus
        var fünf = Welt(Age.Dark);
        for (int i = 0; i < 5; i++) fünf.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Sparta, fünf);
        Assert.Contains(Miliz, fünf.Log);
    }

    [Fact]
    public void Sparta_bildet_ab_Ritterzeit_Katapulte_aus()
    {
        // F15: 15 % der Armee sind Belagerungsgeräte — aber erst ab der Ritterzeit
        var ritter = Welt(Age.Castle, workshop: true);
        Tick(AiProfile.Sparta, ritter);
        Assert.Contains(Katapult, ritter.Log);

        var feudale = Welt(Age.Feudal, workshop: true);
        Tick(AiProfile.Sparta, feudale);
        Assert.DoesNotContain(Katapult, feudale.Log);

        // ohne Werkstatt steht nichts zum Ausbilden
        var ohne = Welt(Age.Castle);
        Tick(AiProfile.Sparta, ohne);
        Assert.DoesNotContain(Katapult, ohne.Log);
    }

    [Fact]
    public void Sparta_baut_die_Belagerungswerkstatt_ab_der_Ritterzeit()
    {
        var w = Welt(Age.Castle);
        for (int i = 0; i < 4; i++) w.AddVillager();   // bauen kann die KI nur mit Leuten
        for (int i = 0; i < 200 && !w.Log.Any(s => s.StartsWith("build(SiegeWorkshop,")); i++)
            Tick(AiProfile.Sparta, w);
        Assert.True(w.Log.Any(s => s.StartsWith("build(SiegeWorkshop,")), string.Join(" | ", w.Log));

        // Normal und Defense bauen sie nie (SiegePercent 0)
        var normal = Welt(Age.Castle);
        for (int i = 0; i < 4; i++) normal.AddVillager();
        for (int i = 0; i < 200 && !normal.Log.Any(s => s.StartsWith("build(SiegeWorkshop,")); i++)
            Tick(AiProfile.Standard, normal);
        Assert.DoesNotContain(normal.Log, s => s.StartsWith("build(SiegeWorkshop,"));
    }

    [Theory]
    [InlineData(0, 6)]   // Defense: greift nie an, steigt trotzdem auf
    [InlineData(1, 5)]   // Normal wie bisher (RaidMinSoldiers)
    [InlineData(2, 6)]   // Sparta: über dem Miliz-Anteil des Solls (5) — die Armee muss wachsen
    public void Aufstieg_braucht_die_Mindestarmee_der_Stufe(int stufe, int armee)
    {
        var p = AiProfile.ForLevel(stufe);
        Assert.Equal(armee, p.AgeUpArmy);
        foreach (int n in new[] { armee - 1, armee })
        {
            var w = new FakeWorld { Age = Age.Dark, PopulationCount = 60, PopulationCapacity = 60,
                                    Resources = new ResourceVector(3000, 3000, 3000, 3000) };
            w.AddBuilding(BuildingType.TownCenter, 10, 10, complete: true);
            w.AddBuilding(BuildingType.Barracks, 20, 10, complete: true);
            for (int i = 0; i < n; i++) w.AddSoldier(30, 30, UnitKind.Militia);
            Tick(p, w);
            Assert.Equal(n >= armee, w.Log.Contains("ageup()"));
        }
    }

    [Fact]
    public void Eingereihte_zaehlen_mit()
    {
        // 7 Milizen im Feld, eine in der Warteschlange: Miliz-Anteil am Soll (5, F15) erreicht
        var w = Welt(Age.Dark);
        for (int i = 0; i < 7; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        w.Queued[UnitKind.Militia] = 1;
        Tick(AiProfile.Sparta, w);
        Assert.DoesNotContain(w.Log, l => l.StartsWith("soldier("));

        // Feudalzeit, 4 Nahkämpfer im Feld, 3 Bogenschützen eingereiht: total 7, Bogen 0,35·8 - 3 < 0,
        // Reiter 0,25·8 = 2 → nur Reiter
        var f = Welt(Age.Feudal);
        for (int i = 0; i < 4; i++) f.AddSoldier(30, 30, UnitKind.Militia);
        f.Queued[UnitKind.Archer] = 3;
        Tick(AiProfile.Sparta, f);
        Assert.Contains(Reiter, f.Log);
        Assert.DoesNotContain(Bogen, f.Log);

        // Soll erreicht mit Warteschlange: nichts mehr
        var voll = Welt(Age.Feudal);
        for (int i = 0; i < 15; i++) voll.AddSoldier(30, 30, UnitKind.Archer);
        voll.Queued[UnitKind.Cavalry] = 5;
        Tick(AiProfile.Sparta, voll);
        Assert.DoesNotContain(voll.Log, l => l.StartsWith("soldier("));
    }

    [Fact]
    public void Normal_zaehlt_die_Warteschlange_nicht_wie_bisher()
    {
        var w = Welt(Age.Feudal);
        for (int i = 0; i < 6; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        w.Queued[UnitKind.Militia] = 5;
        Tick(AiProfile.Standard, w);
        Assert.Contains(Miliz, w.Log);
    }

    [Fact]
    public void Unfertiger_Stall_bildet_nicht_aus()
    {
        var w = Welt(Age.Feudal, stable: false);
        w.AddBuilding(BuildingType.Stable, 20, 20, complete: false);
        Tick(AiProfile.Sparta, w);
        Assert.DoesNotContain(Reiter, w.Log);
        Assert.Contains(Bogen, w.Log);
    }

    [Fact]
    public void Normal_bleibt_wie_bisher_nur_Miliz()
    {
        var w = Welt(Age.Feudal);
        Tick(AiProfile.Standard, w);
        Assert.Contains(Miliz, w.Log);
        Assert.DoesNotContain(Bogen, w.Log);
        Assert.DoesNotContain(Reiter, w.Log);
    }

    [Fact]
    public void Defense_mischt_Bogenschuetzen_bei_aber_keine_Reiter()
    {
        var w = Welt(Age.Feudal);
        for (int i = 0; i < 3; i++) w.AddSoldier(30, 30, UnitKind.Militia);
        Tick(AiProfile.Defense, w);
        // total 3: Miliz 0,6·4 − 3 < 0, Bogen 0,4·4 = 1,6 → nur Bogen
        Assert.Contains(Bogen, w.Log);
        Assert.DoesNotContain(Miliz, w.Log);
        Assert.DoesNotContain(Reiter, w.Log);
    }

    [Fact]
    public void Am_Soll_keine_Ausbildung()
    {
        var w = Welt(Age.Feudal);
        for (int i = 0; i < AiProfile.Sparta.SoldierTarget; i++) w.AddSoldier(30, 30, UnitKind.Archer);
        Tick(AiProfile.Sparta, w);
        Assert.DoesNotContain(w.Log, l => l.StartsWith("soldier("));
    }

    // ------------------------------------------------------------- Gebäude

    private static FakeWorld Baugrund(Age age)
    {
        var w = Welt(age, range: true, stable: false);
        w.AddVillager(14, 14);
        w.AddVillager(15, 14);
        // ein Sammler: TryBuildNear darf ihn holen (allowPool), auch wenn FillSites die freien
        // Arbeiter schon an eine offene Baustelle geschickt hat - so prüfen die Negativfälle
        // wirklich die Bedingungen und nicht nur fehlende Arbeiter
        w.AddVillager(16, 14, UnitStateKind.Gathering, gathering: (30, 30, Resource.Wood));
        return w;
    }

    [Fact]
    public void Sparta_baut_in_der_Feudalzeit_einen_Stall()
    {
        var w = Baugrund(Age.Feudal);
        Tick(AiProfile.Sparta, w);
        Assert.Contains(w.Log, l => l.StartsWith("build(Stable,"));
    }

    [Fact]
    public void Sparta_baut_keinen_zweiten_Stall_und_nicht_in_der_Dunklen_Zeit()
    {
        var w = Baugrund(Age.Feudal);
        w.AddBuilding(BuildingType.Stable, 40, 40, complete: false);   // Baustelle zählt
        Tick(AiProfile.Sparta, w);
        Assert.DoesNotContain(w.Log, l => l.StartsWith("build(Stable,"));

        var dunkel = Baugrund(Age.Dark);
        Tick(AiProfile.Sparta, dunkel);
        Assert.DoesNotContain(dunkel.Log, l => l.StartsWith("build(Stable,"));
    }

    [Fact]
    public void Normal_und_Defense_bauen_keinen_Stall()
    {
        foreach (var p in new[] { AiProfile.Standard, AiProfile.Defense })
        {
            var w = Baugrund(Age.Feudal);
            Tick(p, w);
            Assert.DoesNotContain(w.Log, l => l.StartsWith("build(Stable,"));
        }
    }

    [Fact]
    public void Defense_baut_Wachtuerme_bis_zur_Zahl()
    {
        var w = Baugrund(Age.Feudal);
        Tick(AiProfile.Defense, w);
        Assert.Contains(w.Log, l => l.StartsWith("build(Tower,"));

        // ein fertiger Turm: der zweite kommt noch
        var eins = Baugrund(Age.Feudal);
        eins.AddBuilding(BuildingType.Tower, 40, 40, complete: true);
        Tick(AiProfile.Defense, eins);
        Assert.Contains(eins.Log, l => l.StartsWith("build(Tower,"));

        // zwei Türme stehen: keiner mehr
        var zwei = Baugrund(Age.Feudal);
        zwei.AddBuilding(BuildingType.Tower, 40, 40, complete: true);
        zwei.AddBuilding(BuildingType.Tower, 50, 40, complete: true);
        Tick(AiProfile.Defense, zwei);
        Assert.DoesNotContain(zwei.Log, l => l.StartsWith("build(Tower,"));
    }

    [Fact]
    public void Defense_baut_keinen_Turm_neben_offener_Turmbaustelle_ohne_Stein_oder_zu_frueh()
    {
        var offen = Baugrund(Age.Feudal);
        offen.AddBuilding(BuildingType.Tower, 40, 40, complete: false);
        Tick(AiProfile.Defense, offen);
        Assert.DoesNotContain(offen.Log, l => l.StartsWith("build(Tower,"));

        var arm = Baugrund(Age.Feudal);
        arm.Resources = new ResourceVector(3000, 3000, 3000, 100);
        Tick(AiProfile.Defense, arm);
        Assert.DoesNotContain(arm.Log, l => l.StartsWith("build(Tower,"));

        var dunkel = Baugrund(Age.Dark);
        Tick(AiProfile.Defense, dunkel);
        Assert.DoesNotContain(dunkel.Log, l => l.StartsWith("build(Tower,"));
    }

    [Fact]
    public void Normal_und_Sparta_bauen_keine_Tuerme()
    {
        foreach (var p in new[] { AiProfile.Standard, AiProfile.Sparta })
        {
            var w = Baugrund(Age.Feudal);
            w.AddBuilding(BuildingType.Stable, 40, 40, complete: true);   // Sparta: Stall steht schon
            Tick(p, w);
            Assert.DoesNotContain(w.Log, l => l.StartsWith("build(Tower,"));
        }
    }
}
