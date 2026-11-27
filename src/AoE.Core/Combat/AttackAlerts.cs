using System;
using System.Collections.Generic;

namespace AoE.Core.Combat;

/// <summary>
/// Ein Angriffsort auf der Minikarte (AM1): Mitte in Kacheln, erster und letzter Treffer
/// in Spielsekunden.
/// </summary>
public readonly record struct AttackSpot(float X, float Y, double FirstHit, double LastHit);

/// <summary>
/// Angriffsorte für die Minikarte (AM1, Nutzer 2026-11-27: „Wird man angegriffen sollte auf
/// der Minikarte angezeigt werden wo der Angriff stattfindet. Es muss nicht genau sein und
/// kann mit einem kleinen roten leicht durchsichtigem Kreis dargestellt werden“). Reine Logik
/// ohne MonoGame: das Spiel meldet jeden Treffer gegen Spieler 0 mit Ort (Kacheln) und
/// Spielzeit, die Minikarte zeichnet je Ort einen Kreis. Treffer nahe beieinander zählen zu
/// EINEM Ort, damit ein Gefecht ein Kreis bleibt und keine Wolke aus Kreisen wird; der Ort
/// verschwindet <see cref="HoldSeconds"/> nach seinem letzten Treffer.
/// </summary>
public sealed class AttackAlerts
{
    /// <summary>Treffer bis zu so vielen Kacheln Luftlinie von einem Ort zählen zu ihm.</summary>
    public const float MergeRadiusTiles = 8f;

    /// <summary>So lange nach dem letzten Treffer bleibt ein Ort stehen (Sekunden).</summary>
    public const double HoldSeconds = 6.0;

    /// <summary>In den letzten so vielen Sekunden davon blendet der Kreis aus.</summary>
    public const double FadeSeconds = 1.5;

    /// <summary>Höchstens so viele Orte zugleich; ein neuer verdrängt den ältesten.</summary>
    public const int MaxSpots = 8;

    /// <summary>So oft je Sekunde pulsiert ein Kreis (Nutzer: „leicht pulsierend“).</summary>
    public const double PulseHz = 1.25;

    /// <summary>So weit wächst und schrumpft er dabei (Anteil des Radius).</summary>
    public const float PulseAmplitude = 0.15f;

    private readonly List<AttackSpot> _spots = new();

    /// <summary>Die gemerkten Orte in der Reihenfolge ihres Entstehens.</summary>
    public IReadOnlyList<AttackSpot> Spots => _spots;

    /// <summary>
    /// Ein Treffer an (<paramref name="x"/>, <paramref name="y"/>) in Kacheln zur Spielzeit
    /// <paramref name="now"/>.
    /// VERTRAG: zuerst Prune(now). Dann unter allen Orten in _spots den mit dem kleinsten
    /// Abstand (Luftlinie in Kacheln: MathF.Sqrt(dx * dx + dy * dy) mit dx = s.X - x,
    /// dy = s.Y - y) suchen, bei Gleichstand den zuerst gelisteten. Ist dieser Abstand höchstens
    /// MergeRadiusTiles: den Ort an DERSELBEN Stelle der Liste durch s with { LastHit = now }
    /// ersetzen - Mitte und FirstHit bleiben (ein Kreis, der bei jedem Treffer springt, lenkt
    /// ab), fertig. Sonst: hat _spots schon MaxSpots Einträge, den mit dem kleinsten LastHit
    /// entfernen (bei Gleichstand den zuerst gelisteten); dann new AttackSpot(x, y, now, now)
    /// hinten anfügen.
    /// </summary>
    public void Report(float x, float y, double now)
    {
        // Abgelaufene Orte zuerst wegwerfen.
        Prune(now);

        // Nächsten Ort suchen; bei Gleichstand gewinnt der zuerst gelistete.
        int best = -1;
        float bestDist = float.MaxValue;
        for (int i = 0; i < _spots.Count; i++)
        {
            float dx = _spots[i].X - x, dy = _spots[i].Y - y;
            float d = MathF.Sqrt(dx * dx + dy * dy);
            if (d < bestDist)
            {
                best = i;
                bestDist = d;
            }
        }

        // Treffer nah beieinander zählen zu EINEM Ort: nur den letzten Treffer aktualisieren.
        if (best >= 0 && bestDist <= MergeRadiusTiles)
        {
            _spots[best] = _spots[best] with { LastHit = now };
            return;
        }

        // Liste voll: den Ort mit dem ältesten letzten Treffer verdrängen.
        if (_spots.Count >= MaxSpots)
        {
            int oldest = 0;
            for (int i = 1; i < _spots.Count; i++)
            {
                if (_spots[i].LastHit < _spots[oldest].LastHit)
                {
                    oldest = i;
                }
            }

            _spots.RemoveAt(oldest);
        }

        // Neuer Ort hinten an.
        _spots.Add(new AttackSpot(x, y, now, now));
    }

    /// <summary>
    /// Vergessen, was vorbei ist.
    /// VERTRAG: entfernt jeden Ort mit now - LastHit > HoldSeconds (genau HoldSeconds bleibt
    /// noch); die Reihenfolge der übrigen bleibt.
    /// </summary>
    public void Prune(double now)
    {
        // Orte vergessen, deren letzter Treffer länger her ist als HoldSeconds.
        _spots.RemoveAll(s => now - s.LastHit > HoldSeconds);
    }

    /// <summary>
    /// Deckkraft des Kreises zwischen 0 und 1: voll, solange Treffer kommen, in den letzten
    /// <see cref="FadeSeconds"/> blendet er aus.
    /// VERTRAG: age = now - spot.LastHit; ist age kleiner als 0, 1f; ist age mindestens
    /// HoldSeconds, 0f; left = HoldSeconds - age; ist left kleiner als FadeSeconds,
    /// (float)(left / FadeSeconds); sonst 1f.
    /// </summary>
    public static float Alpha(AttackSpot spot, double now)
    {
        double age = now - spot.LastHit;

        // Noch in der Zukunft (Uhr zurückgesetzt): voll sichtbar.
        if (age < 0)
        {
            return 1f;
        }

        // Abgelaufen: unsichtbar.
        if (age >= HoldSeconds)
        {
            return 0f;
        }

        // In den letzten FadeSeconds linear ausblenden.
        double left = HoldSeconds - age;
        return left < FadeSeconds ? (float)(left / FadeSeconds) : 1f;
    }

    /// <summary>
    /// Leichtes Pulsieren: Faktor für den Radius des Kreises um 1, im Takt
    /// <see cref="PulseHz"/>, ab dem ersten Treffer - ein neuer Kreis wächst zuerst.
    /// VERTRAG: t = now - spot.FirstHit; ist t kleiner als 0, 1f; sonst
    /// 1f + PulseAmplitude * MathF.Sin((float)(2 * Math.PI * PulseHz * t)).
    /// </summary>
    public static float Pulse(AttackSpot spot, double now)
    {
        // Takt beginnt mit dem ersten Treffer.
        double t = now - spot.FirstHit;

        // Noch in der Zukunft (Uhr zurückgesetzt): kein Puls.
        if (t < 0)
        {
            return 1f;
        }

        return 1f + PulseAmplitude * MathF.Sin((float)(2 * Math.PI * PulseHz * t));
    }
}
