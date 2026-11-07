using AoE.Core.Ai;
using Xunit;

namespace AoE.Tests;

/// <summary>Tests der Aufstellung (<see cref="Formation.Slots"/>): Glieder, Schützen dahinter,
/// Reiter an den Flanken, eine Kachel frei zwischen Nachbarn, Ausweichen um Hindernisse.</summary>
public class FormationTests
{
    private static UnitSnapshot U(int id, UnitKind kind, int x, int y) =>
        new() { Id = id, Kind = kind, X = x, Y = y, Health = 40, MaxHealth = 40 };

    private static bool Open(int x, int y) => x >= 0 && y >= 0 && x < 64 && y < 64;

    private static float Depth((int X, int Y) slot, (int X, int Y) anchor, (float X, float Y) f)
    {
        float len = MathF.Sqrt(f.X * f.X + f.Y * f.Y);
        return ((slot.X - anchor.X) * f.X + (slot.Y - anchor.Y) * f.Y) / len;
    }

    [Fact]
    public void Linie_aus_drei_Nahkaempfern_quer_zur_Blickrichtung()
    {
        // Blick nach Osten: Querachse p = (0, 1) - die Linie steht senkrecht (x gleich, y 2 auseinander)
        var units = new[] { U(1, UnitKind.Militia, 10, 10), U(2, UnitKind.Militia, 10, 30), U(3, UnitKind.Militia, 10, 20) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Open);
        // feste Reihenfolge nach Id, nicht nach der Lage der Einheiten
        Assert.Equal((20, 18), s[1]);
        Assert.Equal((20, 20), s[2]);
        Assert.Equal((20, 22), s[3]);
    }

    [Theory]
    [InlineData(1f, 0f)]
    [InlineData(1f, 1f)]
    [InlineData(-2f, 1f)]
    [InlineData(0f, -1f)]
    public void Stehende_Aufstellung_bleibt_und_rueckt_als_Ganzes_vor(float fx, float fy)
    {
        // 12 Nahkämpfer (drei Glieder), 6 Schützen, 4 Reiter: stehen sie auf ihren Plätzen, ergibt der
        // nächste Takt dieselbe Zuteilung - bis 2026-11-07 tauschten Vorder- und Hintermann gleicher
        // Querlage bei jedem Takt die Plätze, und die Welle kam nie zur Ruhe
        var units = new List<UnitSnapshot>();
        int id = 1;
        for (int i = 0; i < 12; i++) units.Add(U(id++, UnitKind.Militia, 5 + i, 50));
        for (int i = 0; i < 6; i++) units.Add(U(id++, UnitKind.Archer, 5 + i, 52));
        for (int i = 0; i < 4; i++) units.Add(U(id++, UnitKind.Cavalry, 5 + i, 54));
        var s1 = Formation.Slots((32, 32), (fx, fy), units, Open);
        var stehend = units.Select(u => U(u.Id, u.Kind, s1[u.Id].X, s1[u.Id].Y)).ToList();
        var s2 = Formation.Slots((32, 32), (fx, fy), stehend, Open);
        foreach (var u in units)
            Assert.Equal(s1[u.Id], s2[u.Id]);

        // Anker 3 Kacheln weiter: jeder Platz verschiebt sich um dasselbe
        var s3 = Formation.Slots((35, 32), (fx, fy), stehend, Open);
        foreach (var u in units)
            Assert.Equal((s1[u.Id].X + 3, s1[u.Id].Y), s3[u.Id]);
    }

    [Fact]
    public void Schuetzen_stehen_ein_Glied_hinter_den_Nahkaempfern()
    {
        var units = new[] { U(1, UnitKind.Militia, 5, 19), U(2, UnitKind.Militia, 5, 21),
                            U(3, UnitKind.Archer, 5, 19), U(4, UnitKind.Archer, 5, 21) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Open);
        Assert.Equal((20, 19), s[1]);
        Assert.Equal((20, 21), s[2]);
        Assert.Equal((18, 19), s[3]);
        Assert.Equal((18, 21), s[4]);
    }

    [Fact]
    public void Zweites_Glied_und_Schuetzen_hinter_allen_Gliedern()
    {
        var units = new List<UnitSnapshot>();
        for (int i = 0; i < 7; i++) units.Add(U(i + 1, UnitKind.Militia, 0, i));
        units.Add(U(20, UnitKind.Archer, 0, 0));
        var s = Formation.Slots((30, 30), (1f, 0f), units, Open);
        // Glied 0: fünf Einheiten bei x 30, y 26..34; Glied 1: zwei bei x 28, y 29 und 31
        for (int i = 1; i <= 5; i++) Assert.Equal(30, s[i].X);
        Assert.Equal(new[] { 26, 28, 30, 32, 34 }, Enumerable.Range(1, 5).Select(i => s[i].Y).ToArray());
        Assert.Equal((28, 29), s[6]);
        Assert.Equal((28, 31), s[7]);
        // der Schütze hinter beiden Gliedern
        Assert.Equal((26, 30), s[20]);
    }

    [Fact]
    public void Reiter_an_beiden_Flanken()
    {
        var units = new[] { U(1, UnitKind.Militia, 0, 19), U(2, UnitKind.Militia, 0, 20), U(3, UnitKind.Militia, 0, 21),
                            U(8, UnitKind.Cavalry, 0, 10), U(9, UnitKind.Cavalry, 0, 30), U(10, UnitKind.Cavalry, 0, 31) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Open);
        // W = (3 - 1) / 2 * 2 = 2 → Reiter bei Querlage ±4; der dritte eine Reihe dahinter links
        Assert.Equal((20, 16), s[8]);
        Assert.Equal((20, 24), s[9]);
        Assert.Equal((18, 16), s[10]);
    }

    [Fact]
    public void Blick_nach_Norden_Schuetzen_suedlich()
    {
        var units = new[] { U(1, UnitKind.Militia, 20, 20), U(2, UnitKind.Archer, 20, 20) };
        var s = Formation.Slots((20, 20), (0f, -1f), units, Open);
        Assert.Equal((20, 20), s[1]);
        Assert.Equal((20, 22), s[2]);
    }

    [Fact]
    public void Gemischte_Armee_eine_Kachel_frei_zwischen_allen()
    {
        var units = new List<UnitSnapshot>();
        int id = 1;
        for (int i = 0; i < 6; i++) units.Add(U(id++, UnitKind.Militia, 3, i));
        for (int i = 0; i < 5; i++) units.Add(U(id++, UnitKind.Archer, 3, i));
        for (int i = 0; i < 4; i++) units.Add(U(id++, UnitKind.Cavalry, 3, i));
        foreach (var facing in new[] { (1f, 0f), (-1f, 0f), (0f, 1f), (0f, -1f) })
        {
            var s = Formation.Slots((32, 32), facing, units, Open);
            Assert.Equal(units.Count, s.Count);
            var slots = s.Values.ToList();
            for (int a = 0; a < slots.Count; a++)
                for (int b = a + 1; b < slots.Count; b++)
                    Assert.True(Math.Max(Math.Abs(slots[a].X - slots[b].X), Math.Abs(slots[a].Y - slots[b].Y)) >= 2,
                        $"Blick {facing}: {slots[a]} und {slots[b]} zu dicht");
            // jeder Schütze steht hinter jedem Nahkämpfer
            foreach (var arch in units.Where(u => u.Kind == UnitKind.Archer))
                foreach (var mel in units.Where(u => u.Kind == UnitKind.Militia))
                    Assert.True(Depth(s[arch.Id], (32, 32), facing) < Depth(s[mel.Id], (32, 32), facing));
        }
    }

    [Fact]
    public void Schraeger_Blick_alle_Plaetze_verschieden_und_Schuetzen_hinten()
    {
        var units = new List<UnitSnapshot>();
        int id = 1;
        for (int i = 0; i < 5; i++) units.Add(U(id++, UnitKind.Militia, i, 0));
        for (int i = 0; i < 5; i++) units.Add(U(id++, UnitKind.Archer, i, 0));
        var s = Formation.Slots((30, 30), (1f, 1f), units, Open);
        Assert.Equal(10, s.Values.Distinct().Count());
        // auch diagonal eine freie Kachel zwischen je zwei Nachbarn (Chebyshev mindestens 2)
        var all = s.Values.ToList();
        for (int i = 0; i < all.Count; i++)
            for (int j = i + 1; j < all.Count; j++)
                Assert.True(Math.Max(Math.Abs(all[i].X - all[j].X), Math.Abs(all[i].Y - all[j].Y)) >= 2,
                    $"{all[i]} und {all[j]} stehen Ecke an Ecke");
        float frontMin = units.Where(u => u.Kind == UnitKind.Militia).Min(u => Depth(s[u.Id], (30, 30), (1f, 1f)));
        float rangedMax = units.Where(u => u.Kind == UnitKind.Archer).Max(u => Depth(s[u.Id], (30, 30), (1f, 1f)));
        Assert.True(rangedMax < frontMin, $"Schützen {rangedMax} nicht hinter Nahkämpfern {frontMin}");
    }

    [Fact]
    public void Versperrter_Platz_weicht_auf_freie_Nachbarkachel_aus()
    {
        var blocked = new HashSet<(int, int)> { (20, 20), (20, 18) };
        bool Walk(int x, int y) => Open(x, y) && !blocked.Contains((x, y));
        var units = new[] { U(1, UnitKind.Militia, 0, 18), U(2, UnitKind.Militia, 0, 20), U(3, UnitKind.Militia, 0, 22) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Walk);
        Assert.Equal(3, s.Values.Distinct().Count());
        foreach (var slot in s.Values)
            Assert.True(Walk(slot.X, slot.Y), $"{slot} ist versperrt");
        Assert.Equal((20, 22), s[3]);   // freier Idealplatz bleibt
        // Ausweichplätze liegen im Suchradius um den Idealplatz
        Assert.True(Math.Max(Math.Abs(s[1].X - 20), Math.Abs(s[1].Y - 18)) <= Formation.SearchRadius);
        Assert.True(Math.Max(Math.Abs(s[2].X - 20), Math.Abs(s[2].Y - 20)) <= Formation.SearchRadius);
        // der nächste freie Platz: für (20, 20) ist das (20, 21) oder (20, 19) bzw. (19, 20)/(21, 20) - Abstand 1
        Assert.Equal(1, Math.Max(Math.Abs(s[2].X - 20), Math.Abs(s[2].Y - 20)));
    }

    [Theory]
    // Mitte versperrt, drei Nachbarn mit Abstand 1 auch: der vierte (20, 21) ist näher als jede Ecke
    [InlineData(new[] { 20, 20, 20, 19, 19, 20, 21, 20 }, 20, 21)]
    // Mitte versperrt: alle vier mit Abstand 1 frei, Gleichstand → kleinstes dy, dann dx: (20, 19)
    [InlineData(new[] { 20, 20 }, 20, 19)]
    // Mitte und (20, 19) versperrt: Gleichstand (19, 20) / (21, 20) / (20, 21) → dy 0 vor dy 1, dx -1 vor +1
    [InlineData(new[] { 20, 20, 20, 19 }, 19, 20)]
    // ganzer Ring 1 bis auf die Ecke (21, 21) versperrt → die Ecke (Abstand √2) vor Ring 2
    [InlineData(new[] { 20, 20, 20, 19, 19, 20, 21, 20, 20, 21, 19, 19, 21, 19, 19, 21 }, 21, 21)]
    public void Ausweichen_nimmt_die_naechste_freie_Kachel(int[] gesperrt, int ex, int ey)
    {
        var blocked = new HashSet<(int, int)>();
        for (int i = 0; i < gesperrt.Length; i += 2) blocked.Add((gesperrt[i], gesperrt[i + 1]));
        bool Walk(int x, int y) => Open(x, y) && !blocked.Contains((x, y));
        var s = Formation.Slots((20, 20), (1f, 0f), new[] { U(1, UnitKind.Militia, 0, 0) }, Walk);
        Assert.Equal((ex, ey), s[1]);
    }

    [Fact]
    public void Ausweichen_misst_zum_Idealpunkt_nicht_zur_Kachel()
    {
        // Blick (3, 1): Schritt 2 / 0,949 = 2,108, der Idealplatz des ersten Nahkämpfers liegt bei
        // (30.33, 29.0) → Kachel (30, 29). Ist sie versperrt, gewinnt im Ring 1 die Kachel mit der
        // kleinsten Entfernung zum Idealpunkt: (31, 29) mit 0,67² ≈ 0,44 - gemessen zur Kachel (30, 29)
        // stünden vier Nachbarn gleich weit, und (30, 28) gewänne.
        var blocked = new HashSet<(int, int)> { (30, 29) };
        bool Walk(int x, int y) => Open(x, y) && !blocked.Contains((x, y));
        var units = new[] { U(1, UnitKind.Militia, 40, 20), U(2, UnitKind.Militia, 30, 30) };
        var s = Formation.Slots((30, 30), (3f, 1f), units, Walk);
        Assert.Equal((31, 29), s[1]);
    }

    [Fact]
    public void Ausweichplatz_haelt_Abstand_zum_Nachbarn()
    {
        // zwei Nahkämpfer, Blick nach Osten: Idealplätze (20, 19) und (20, 21). (20, 19) und drei
        // Nachbarn sind versperrt, der erste weicht auf (20, 20) aus. Der zweite stünde auf seinem
        // Idealplatz (20, 21) direkt daneben - er nimmt die nächste Kachel mit Abstand: (20, 22).
        var blocked = new HashSet<(int, int)> { (20, 19), (20, 18), (19, 19), (21, 19) };
        bool Walk(int x, int y) => Open(x, y) && !blocked.Contains((x, y));
        var units = new[] { U(1, UnitKind.Militia, 0, 0), U(2, UnitKind.Militia, 0, 0) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Walk);
        Assert.Equal((20, 20), s[1]);
        Assert.Equal((20, 22), s[2]);
    }

    [Fact]
    public void Ohne_Platz_mit_Abstand_die_naechste_freie_Kachel()
    {
        // nur drei Kacheln begehbar, alle in einer Reihe: Abstand geht nicht für alle
        var open = new HashSet<(int, int)> { (20, 20), (20, 21), (20, 22) };
        bool Walk(int x, int y) => open.Contains((x, y));
        var units = new[] { U(1, UnitKind.Militia, 0, 0), U(2, UnitKind.Militia, 0, 0), U(3, UnitKind.Militia, 0, 0) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, Walk);
        Assert.Equal((20, 20), s[1]);   // Ideal (20, 18) versperrt: Ring 2
        Assert.Equal((20, 22), s[2]);   // Ideal (20, 20) vergeben, (20, 21) ohne Abstand
        Assert.Equal((20, 21), s[3]);   // Ideal (20, 22) vergeben, nur noch ohne Abstand
    }

    [Fact]
    public void PickTile_Durchgaenge_und_Anker()
    {
        var taken = new HashSet<(int, int)> { (10, 10) };
        // Ring 0 frei, aber Nachbar vergeben → Ring 1 mit Abstand: zum Idealpunkt (11, 10) am nächsten (12, 10)
        Assert.Equal((12, 10), Formation.PickTile(11f, 10f, (0, 0), taken, (x, y) => true));
        // nichts frei mit Abstand im Suchradius, aber (11, 10) frei → Durchgang 2
        bool Eng(int x, int y) => (x, y) == (11, 10) || (x, y) == (10, 10);
        Assert.Equal((11, 10), Formation.PickTile(11f, 10f, (0, 0), taken, Eng));
        // gar nichts frei → Anker
        Assert.Equal((5, 6), Formation.PickTile(11f, 10f, (5, 6), taken, (x, y) => false));
        // taken bleibt unverändert
        Assert.Single(taken);
    }

    [Fact]
    public void Am_Kartenrand_nur_begehbare_Plaetze()
    {
        var units = new List<UnitSnapshot>();
        for (int i = 0; i < 5; i++) units.Add(U(i + 1, UnitKind.Militia, 0, i));
        var s = Formation.Slots((0, 1), (1f, 0f), units, Open);
        Assert.Equal(5, s.Values.Distinct().Count());
        foreach (var slot in s.Values)
            Assert.True(Open(slot.X, slot.Y), $"{slot} liegt außerhalb der Karte");
    }

    [Fact]
    public void Blick_null_gilt_als_Osten_und_leere_Liste_gibt_leeres_Ergebnis()
    {
        var units = new[] { U(1, UnitKind.Militia, 0, 0), U(2, UnitKind.Archer, 0, 0) };
        var s = Formation.Slots((20, 20), (0f, 0f), units, Open);
        Assert.Equal((20, 20), s[1]);
        Assert.Equal((18, 20), s[2]);
        Assert.Empty(Formation.Slots((20, 20), (1f, 0f), Array.Empty<UnitSnapshot>(), Open));
    }

    [Fact]
    public void Alles_versperrt_gibt_den_Anker()
    {
        var units = new[] { U(1, UnitKind.Militia, 0, 0) };
        var s = Formation.Slots((20, 20), (1f, 0f), units, (_, _) => false);
        Assert.Equal((20, 20), s[1]);
    }

    [Theory]
    [InlineData(0, "Defense")]
    [InlineData(1, "Normal")]
    [InlineData(2, "This is Sparta")]
    [InlineData(3, "This is Sparta")]
    public void Stufen_und_Profile(int level, string name)
    {
        Assert.Equal(name, AgeOfEvolutions.Core.Settings.GameplayOptions.AiName(level));
        var p = AiProfile.ForLevel(level);
        Assert.Equal(Math.Min(level, 2), AiProfile.LevelOf(p));
        Assert.Equal(level != 0, p.Raids);
        Assert.Equal(level != 1, p.FormationTactics);
    }
}
