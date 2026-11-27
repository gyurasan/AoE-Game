using System.Linq;
using AoE.Core.Combat;
using Xunit;

namespace AoE.Tests;

/// <summary>Angriffsorte für die Minikarte (AM1): Zusammenfassen naher Treffer, Vergessen
/// nach <see cref="AttackAlerts.HoldSeconds"/>, Obergrenze und Ausblenden.</summary>
public class AttackAlertsTests
{
    [Fact]
    public void Erster_Treffer_legt_einen_Ort_an()
    {
        var a = new AttackAlerts();
        a.Report(10f, 12f, 5.0);
        Assert.Equal(new[] { new AttackSpot(10f, 12f, 5.0, 5.0) }, a.Spots);
    }

    [Fact]
    public void Naher_Treffer_frischt_den_Ort_auf_ohne_ihn_zu_verschieben()
    {
        var a = new AttackAlerts();
        a.Report(10f, 10f, 1.0);
        a.Report(15f, 10f, 3.0);
        Assert.Equal(new[] { new AttackSpot(10f, 10f, 1.0, 3.0) }, a.Spots);
    }

    [Fact]
    public void Genau_am_Zusammenfassungsradius_zaehlt_noch_zum_Ort()
    {
        var a = new AttackAlerts();
        a.Report(10f, 10f, 0.0);
        a.Report(10f + AttackAlerts.MergeRadiusTiles, 10f, 1.0);
        Assert.Equal(new[] { new AttackSpot(10f, 10f, 0.0, 1.0) }, a.Spots);
    }

    [Fact]
    public void Knapp_ausserhalb_des_Radius_entsteht_ein_zweiter_Ort()
    {
        var a = new AttackAlerts();
        a.Report(10f, 10f, 0.0);
        // Luftlinie, nicht Kachelabstand: (6, 6) sind 8,49 Kacheln
        a.Report(16f, 16f, 1.0);
        Assert.Equal(new[] { new AttackSpot(10f, 10f, 0.0, 0.0), new AttackSpot(16f, 16f, 1.0, 1.0) }, a.Spots);
    }

    [Fact]
    public void Schraeg_zaehlt_die_Luftlinie()
    {
        var a = new AttackAlerts();
        a.Report(10f, 10f, 0.0);
        a.Report(15f, 15f, 1.0);   // 7,07 Kacheln Luftlinie (5 + 5 entlang der Kacheln)
        Assert.Equal(new[] { new AttackSpot(10f, 10f, 0.0, 1.0) }, a.Spots);
    }

    [Fact]
    public void Ferner_Treffer_haengt_einen_neuen_Ort_hinten_an()
    {
        var a = new AttackAlerts();
        a.Report(10f, 10f, 0.0);
        a.Report(40f, 10f, 1.0);
        a.Report(10f, 40f, 2.0);
        Assert.Equal(new[] { 10f, 40f, 10f }, a.Spots.Select(s => s.X));
        Assert.Equal(new[] { 10f, 10f, 40f }, a.Spots.Select(s => s.Y));
    }

    [Fact]
    public void Der_naechste_Ort_wird_aufgefrischt_nicht_der_erste_im_Radius()
    {
        var a = new AttackAlerts();
        a.Report(0f, 0f, 0.0);
        a.Report(12f, 0f, 0.5);
        a.Report(7f, 0f, 2.0);   // 7 vom ersten, 5 vom zweiten - beide im Radius
        Assert.Equal(new[] { new AttackSpot(0f, 0f, 0.0, 0.0), new AttackSpot(12f, 0f, 0.5, 2.0) }, a.Spots);
    }

    [Fact]
    public void Bei_gleichem_Abstand_der_zuerst_gelistete()
    {
        var a = new AttackAlerts();
        a.Report(0f, 0f, 0.0);
        a.Report(12f, 0f, 0.5);
        a.Report(6f, 0f, 2.0);
        Assert.Equal(new[] { new AttackSpot(0f, 0f, 0.0, 2.0), new AttackSpot(12f, 0f, 0.5, 0.5) }, a.Spots);
    }

    [Fact]
    public void Prune_vergisst_erst_nach_HoldSeconds()
    {
        var a = new AttackAlerts();
        a.Report(5f, 5f, 2.0);
        a.Prune(2.0 + AttackAlerts.HoldSeconds);
        Assert.Single(a.Spots);
        a.Prune(2.0 + AttackAlerts.HoldSeconds + 0.01);
        Assert.Empty(a.Spots);
    }

    [Fact]
    public void Prune_behaelt_die_Reihenfolge_der_uebrigen()
    {
        var a = new AttackAlerts();
        a.Report(0f, 0f, 0.0);
        a.Report(20f, 0f, 3.0);
        a.Report(40f, 0f, 1.0);
        a.Report(60f, 0f, 5.0);
        a.Prune(8.0);   // fort: 0 (8 s) und 40 (7 s), bleiben: 20 (5 s) und 60 (3 s)
        Assert.Equal(new[] { 20f, 60f }, a.Spots.Select(s => s.X));
    }

    [Fact]
    public void Dauerbeschuss_haelt_den_Ort_ueber_HoldSeconds_hinaus()
    {
        var a = new AttackAlerts();
        a.Report(5f, 5f, 0.0);
        a.Report(6f, 5f, 5.0);
        a.Report(5f, 6f, 10.0);   // 10 s nach dem ersten, 5 s nach dem letzten Treffer
        a.Prune(14.0);
        Assert.Equal(new[] { new AttackSpot(5f, 5f, 0.0, 10.0) }, a.Spots);
    }

    [Fact]
    public void Report_vergisst_vorher_den_abgelaufenen_Ort()
    {
        var a = new AttackAlerts();
        a.Report(5f, 5f, 0.0);
        a.Report(5f, 5f, 7.0);   // derselbe Platz, aber der alte Ort ist seit 1 s vorbei
        Assert.Equal(new[] { new AttackSpot(5f, 5f, 7.0, 7.0) }, a.Spots);
    }

    [Fact]
    public void Ueber_MaxSpots_verdraengt_ein_neuer_Ort_den_mit_dem_aeltesten_Treffer()
    {
        var a = new AttackAlerts();
        for (int i = 0; i < AttackAlerts.MaxSpots; i++)
            a.Report(i * 20f, 0f, i * 0.1);
        a.Report(0f, 0f, 1.0);       // der erste ist jetzt frisch, ältester ist der zweite (0,1 s)
        a.Report(0f, 100f, 1.1);
        Assert.Equal(AttackAlerts.MaxSpots, a.Spots.Count);
        Assert.DoesNotContain(a.Spots, s => s.X == 20f && s.Y == 0f);
        Assert.Equal(new AttackSpot(0f, 0f, 0.0, 1.0), a.Spots[0]);
        Assert.Equal(new AttackSpot(0f, 100f, 1.1, 1.1), a.Spots[^1]);
    }

    [Fact]
    public void Ueber_MaxSpots_bei_Gleichstand_faellt_der_zuerst_gelistete()
    {
        var a = new AttackAlerts();
        for (int i = 0; i < AttackAlerts.MaxSpots; i++)
            a.Report(i * 20f, 0f, 0.0);
        a.Report(0f, 100f, 0.0);
        Assert.Equal(AttackAlerts.MaxSpots, a.Spots.Count);
        Assert.Equal(20f, a.Spots[0].X);
        Assert.Equal(new AttackSpot(0f, 100f, 0.0, 0.0), a.Spots[^1]);
    }

    [Theory]
    [InlineData(10.0, 1f)]     // eben getroffen
    [InlineData(9.0, 1f)]      // Zeit vor dem Treffer (geladener Stand): voll
    [InlineData(14.0, 1f)]     // noch 2 s übrig
    [InlineData(14.5, 1f)]     // genau FadeSeconds übrig: noch voll
    [InlineData(15.0, 2f / 3f)] // noch 1 s von 1,5
    [InlineData(15.25, 0.5f)]  // noch 0,75 s
    [InlineData(16.0, 0f)]     // HoldSeconds vorbei
    [InlineData(20.0, 0f)]
    public void Alpha_voll_bis_zum_Ausblenden(double now, float erwartet)
        => Assert.Equal(erwartet, AttackAlerts.Alpha(new AttackSpot(3f, 4f, 2.0, 10.0), now), 4);

    // Pulsieren (Nutzer 2026-11-27: „darf etwas dicker sein der rote kreis und leicht
    // pulsierend“): Takt ab dem ERSTEN Treffer, 1,25 je Sekunde, ±15 %
    [Theory]
    [InlineData(1.0, 1f)]       // vor dem ersten Treffer (geladener Stand): ruhig
    [InlineData(2.0, 1f)]       // eben entstanden: Ausgangsgröße, wächst dann
    [InlineData(2.2, 1.15f)]    // Viertel Takt (0,8 s): am größten
    [InlineData(2.4, 1f)]       // halber Takt
    [InlineData(2.6, 0.85f)]    // drei Viertel: am kleinsten
    [InlineData(2.8, 1f)]       // ein ganzer Takt
    [InlineData(5.4, 1.15f)]    // viereinviertel Takte
    public void Pulse_schwingt_ab_dem_ersten_Treffer(double now, float erwartet)
        => Assert.Equal(erwartet, AttackAlerts.Pulse(new AttackSpot(3f, 4f, 2.0, 9.0), now), 3);

    [Fact]
    public void Pulse_bleibt_in_den_Grenzen()
    {
        var s = new AttackSpot(0f, 0f, 0.0, 0.0);
        for (double t = 0; t < 3; t += 0.01)
        {
            float p = AttackAlerts.Pulse(s, t);
            Assert.InRange(p, 1f - AttackAlerts.PulseAmplitude - 1e-5f, 1f + AttackAlerts.PulseAmplitude + 1e-5f);
        }
    }
}
