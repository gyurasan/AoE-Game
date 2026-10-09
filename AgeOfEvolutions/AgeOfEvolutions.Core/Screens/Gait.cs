using System;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// Wie Figuren gehen. Schrittbilder und Wippen hängen an der gelaufenen Strecke,
/// nicht an der Uhr - so rutschen die Füße nicht, wenn eine Figur schneller oder
/// langsamer geht. Dorfbewohner fahren an und bremsen vor dem Ziel, statt mit vollem
/// Tempo loszuspringen und auf der Stelle stehenzubleiben; sie neigen sich beim Gehen
/// leicht nach vorn, statt von Seite zu Seite zu kippeln. Schafe und Wild gleiten weich
/// von Kachel zu Kachel. Strecken in Welteinheiten (eine Kachel = 32).
/// </summary>
public static class Gait
{
    /// <summary>
    /// Schrittlänge in Welteinheiten: so weit kommt eine Figur, während ein Fuß fest am Boden
    /// steht - gemessen an den Gehphasen aus tools/bilder/gang.py (C4m): 200 px Schritt im
    /// Rohbild bei einer Figur von 948 px = 24 Welteinheiten. Gilt für Dorfbewohner, Miliz,
    /// Bogenschütze und Späher; so rutschen die Füße nicht über den Boden.
    /// </summary>
    public const float VILLAGER_STRIDE = 5.06f;

    /// <summary>Gehphasen je Doppelschritt (C4m): lauf1 bis lauf8 je Figur.</summary>
    public const int WALK_FRAMES = 8;

    /// <summary>Zeitkonstante beim Anfahren und Abbremsen, in Sekunden (siehe <see cref="Approach"/>).</summary>
    public const float ACCELERATION_TIME = 0.12f;

    /// <summary>Auf so vielen Welteinheiten vor dem Ziel bremst ein Dorfbewohner ab.</summary>
    public const float BRAKE_DISTANCE = 10f;

    /// <summary>Langsamer als dieser Anteil am vollen Tempo wird er beim Abbremsen nicht - sonst käme er nie an.</summary>
    public const float MIN_ARRIVAL_SPEED = 0.4f;

    /// <summary>Vorlage bei vollem Tempo, in Bogenmaß.</summary>
    public const float LEAN = 0.06f;

    /// <summary>
    /// Welches von <paramref name="frames"/> Laufbildern eine Figur zeigt, die
    /// <paramref name="walked"/> Welteinheiten gegangen ist - die Reihenfolge der Laufbilder.
    /// VERTRAG: ein Doppelschritt ist 2 * stride lang, jedes Bild hat den frames-ten Teil davon,
    /// und walked = 0 liegt mitten im Bild 0: Bild k für walked in
    /// [(k - 0,5) * 2 * stride / frames, (k + 0,5) * 2 * stride / frames), periodisch mit
    /// 2 * stride, auch für negative walked. Ergebnis immer 0 bis frames - 1. Mit frames = 4
    /// (Standard) also Bild 0 für [-stride/4, stride/4), Bild 1 für [stride/4, 3*stride/4) usw.
    /// </summary>
    public static int WalkFrame(float walked, float stride, int frames = 4)
    {
        float c = walked / (2f * stride);
        c -= MathF.Floor(c);
        return (int)MathF.Floor(c * frames + 0.5f) % frames;
    }

    /// <summary>
    /// Wie hoch die Figur beim Gehen gerade ist, 0 bis 1.
    /// VERTRAG: sin²(π * walked / stride) - 0, wenn die Beine gespreizt sind (walked ein
    /// Vielfaches von stride, mitten in Bild 0 oder 2), 1 im Stand dazwischen (walked =
    /// stride / 2 + Vielfaches von stride), weich dazwischen.
    /// </summary>
    public static float Bob(float walked, float stride)
    {
        float s = MathF.Sin(MathF.PI * walked / stride);
        return s * s;
    }

    /// <summary>
    /// Nähert ein Tempo (Anteil am vollen Tempo) dem Ziel an.
    /// VERTRAG: current + (target - current) * (1 - exp(-dt / ACCELERATION_TIME)); bei
    /// dt &lt;= 0 unverändert current. Schießt nie über target hinaus.
    /// </summary>
    public static float Approach(float current, float target, float dt)
    {
        if (dt <= 0f)
            return current;
        return current + (target - current) * (1f - MathF.Exp(-dt / ACCELERATION_TIME));
    }

    /// <summary>
    /// Wie schnell ein Dorfbewohner, dem noch <paramref name="remaining"/> Welteinheiten
    /// bis zum Ziel fehlen, gehen soll, als Anteil am vollen Tempo.
    /// VERTRAG: 1, solange remaining &gt;= BRAKE_DISTANCE; darunter linear von 1 auf
    /// MIN_ARRIVAL_SPEED bei remaining = 0: MIN_ARRIVAL_SPEED + (1 - MIN_ARRIVAL_SPEED) *
    /// remaining / BRAKE_DISTANCE; für negative remaining MIN_ARRIVAL_SPEED.
    /// </summary>
    public static float ArrivalSpeed(float remaining)
    {
        if (remaining >= BRAKE_DISTANCE)
            return 1f;
        return Math.Clamp(MIN_ARRIVAL_SPEED + (1f - MIN_ARRIVAL_SPEED) * remaining / BRAKE_DISTANCE, MIN_ARRIVAL_SPEED, 1f);
    }

    /// <summary>Vorlage beim Gehen in Bogenmaß: LEAN * speed, speed auf 0 bis 1 begrenzt.</summary>
    public static float Lean(float speed)
    {
        return LEAN * Math.Clamp(speed, 0f, 1f);
    }

    /// <summary>
    /// Ob eine Figur in Seitenansicht nach links blickt. Die Bilder zeigen sie nur
    /// von der Seite (nach rechts gemalt, nach links gespiegelt): sie soll zur Seite
    /// blicken, in die sie geht, und bei der Arbeit zu dem, woran sie arbeitet.
    /// <paramref name="step"/> ist ihre Bewegung in diesem Bild (Welteinheiten, Null im
    /// Stand), <paramref name="toward"/> der Weg von ihr zu dem Punkt, auf den sie
    /// zugeht oder an dem sie arbeitet (Null, wenn es keinen gibt),
    /// <paramref name="current"/> ihre bisherige Blickrichtung.
    /// VERTRAG:
    /// - Geht sie deutlich zur Seite (|step.X| &gt; 0,3 * Länge von step): step.X &lt; 0.
    /// - Sonst, wenn das Ziel deutlich seitlich liegt (|toward.X| &gt; 8): toward.X &lt; 0 -
    ///   wer fast senkrecht geht oder arbeitet, schaut zur Seite seines Ziels.
    /// - Sonst bleibt current.
    /// </summary>
    public static bool FacingLeft(Microsoft.Xna.Framework.Vector2 step, Microsoft.Xna.Framework.Vector2 toward, bool current)
    {
        // Geht sie deutlich zur Seite, blickt sie in die Gehrichtung.
        if (MathF.Abs(step.X) > 0.3f * step.Length())
            return step.X < 0f;
        // Sonst, wenn das Ziel deutlich seitlich liegt, schaut sie zur Seite ihres Ziels.
        if (MathF.Abs(toward.X) > 8f)
            return toward.X < 0f;
        // Sonst bleibt die bisherige Blickrichtung.
        return current;
    }

    /// <summary>
    /// Ein Tierschritt von Kachel zu Kachel: wie weit das Tier nach dem Anteil
    /// <paramref name="progress"/> der Schrittzeit gekommen ist, 0 bis 1.
    /// VERTRAG: weich angefahren und abgebremst, p * p * (3 - 2 * p) mit p = progress
    /// auf 0 bis 1 begrenzt.
    /// </summary>
    public static float Ease(float progress)
    {
        float p = Math.Clamp(progress, 0f, 1f);
        return p * p * (3f - 2f * p);
    }

    /// <summary>
    /// Das Tempo des Tiers dabei, als Anteil am höchsten Tempo mitten im Schritt, 0 bis 1.
    /// VERTRAG: 4 * p * (1 - p) mit p = progress auf 0 bis 1 begrenzt - 0 am Anfang und am
    /// Ende des Schritts, 1 in der Mitte.
    /// </summary>
    public static float EaseSpeed(float progress)
    {
        float p = Math.Clamp(progress, 0f, 1f);
        return 4f * p * (1f - p);
    }
}
