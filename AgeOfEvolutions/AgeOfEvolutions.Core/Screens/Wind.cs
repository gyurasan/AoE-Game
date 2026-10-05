using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// Der Wind über der Karte: Böen, die als Bänder von links über das Land ziehen, und
/// wie sich die Bäume darin wiegen. Dieselben Böen bewegen den Weizen
/// (<c>WheatWind</c> in Content/Effects/Weizen.fx) - eine Böe läuft so sichtbar über
/// Feld und Wald. Lagen in Kacheln, Zeiten in Sekunden.
/// </summary>
public static class Wind
{
    /// <summary>Woher der Wind weht: von links und etwas von oben.</summary>
    public static readonly Vector2 Direction = Vector2.Normalize(new Vector2(1f, 0.35f));

    /// <summary>In so viele Streifen schneidet <see cref="SwayStrips"/> einen Baum.</summary>
    public const int TREE_STRIPS = 12;

    /// <summary>
    /// Wie stark an der Stelle <paramref name="tile"/> zur Zeit <paramref name="time"/>
    /// eine Böe weht, von 0 (still) bis 1.
    /// VERTRAG: genau die Rechnung von <c>gust</c> in WheatWind (Weizen.fx), in C#:
    /// zwei Wellen sin((dot(tile, d) - 1.2 * time) * 2π / λ) mit d1 = normalize(1, 0.2),
    /// λ1 = 4 und d2 = normalize(1, 0.5), λ2 = 6; jede geschärft zu
    /// pow(max(welle, 0), 3); davon das Maximum, mal (0.7 + 0.3 * sin(0.4 * time)).
    /// Stetig in Lage und Zeit, immer zwischen 0 und 1.
    /// </summary>
    public static float Gust(Vector2 tile, float time)
    {
        var d1 = Vector2.Normalize(new Vector2(1f, 0.2f));
        var d2 = Vector2.Normalize(new Vector2(1f, 0.5f));
        float welle1 = MathF.Sin((Vector2.Dot(tile, d1) - 1.2f * time) * MathF.Tau / 4f);
        float welle2 = MathF.Sin((Vector2.Dot(tile, d2) - 1.2f * time) * MathF.Tau / 6f);
        float g = MathF.Max(MathF.Pow(MathF.Max(welle1, 0f), 3f), MathF.Pow(MathF.Max(welle2, 0f), 3f));
        return g * (0.7f + 0.3f * MathF.Sin(0.4f * time));
    }

    /// <summary>
    /// Wie weit sich die Spitze eines Baums neigt, als Anteil an seiner Höhe; positiv
    /// nach rechts. <paramref name="tile"/> ist der Stammfuß in Kacheln,
    /// <paramref name="look"/> eine feste Zahl je Baum (aus dem Ortshash), die sein
    /// eigenes Schwingen bestimmt.
    /// VERTRAG:
    /// - Grundneigung mit dem Wind: 0.035 * Gust(tile, time) * Direction.X.
    /// - Dazu schwingt jeder Baum für sich: Amplitude 0.006 + 0.014 * Gust(tile, time),
    ///   Frequenz zwischen 0,45 und 0,8 Schwingungen je Sekunde und Phase zwischen 0 und
    ///   2π, beide aus look abgeleitet - verschiedene Bäume schwingen verschieden.
    /// - Ergebnis immer zwischen -0,05 und 0,05; stetig in der Zeit (kein Sprung von
    ///   Bild zu Bild); bei gleichen Eingaben immer derselbe Wert.
    /// </summary>
    public static float TreeSway(Vector2 tile, float time, int look)
    {
        int l = look & 0x7FFFFFFF;
        float g = Gust(tile, time);
        float grund = 0.035f * g * Direction.X;
        float f = 0.45f + 0.35f * ((l & 0xFFFF) % 1000) / 1000f;
        float phase = MathF.Tau * (((l >> 16) & 0x7FFF) % 1000) / 1000f;
        float schwingung = (0.006f + 0.014f * g) * MathF.Sin(MathF.Tau * f * time + phase);
        return Math.Clamp(grund + schwingung, -0.05f, 0.05f);
    }

    /// <summary>
    /// Wie ein Baumbild, gebogen vom Wind, in <paramref name="target"/> gezeichnet wird:
    /// in <see cref="TREE_STRIPS"/> waagerechten Streifen. Der Fuß (Unterkante) bleibt
    /// stehen, die Spitze rückt um <paramref name="sway"/> * target.Height Bildpunkte zur
    /// Seite, dazwischen wächst die Verschiebung mit h^1,5 (h = Höhe über dem Fuß als
    /// Anteil an target.Height, 0 am Fuß, 1 an der Spitze) - der Stamm bleibt fast
    /// gerade, die Krone schwingt.
    /// VERTRAG:
    /// - Die Streifen teilen das Bild (0 bis <paramref name="texHeight"/>) lückenlos in
    ///   gleich hohe Teile, von oben nach unten (der letzte nimmt den Rest).
    /// - Source: der Bildteil des Streifens (X 0, Breite <paramref name="texWidth"/>).
    /// - Position: linke obere Ecke auf dem Bildschirm, als Vector2 (nicht gerundet):
    ///   x = target.X + Verschiebung bei der Höhe der Streifenmitte, y = target.Y +
    ///   Oberkante des Streifens * target.Height / texHeight. So schließen die Streifen
    ///   senkrecht lückenlos aneinander und füllen target.Y bis target.Bottom.
    /// - Scale: (target.Width / texWidth, target.Height / texHeight).
    /// </summary>
    public static IEnumerable<(Rectangle Source, Vector2 Position, Vector2 Scale)> SwayStrips(
        Rectangle target, int texWidth, int texHeight, float sway)
    {
        float stripHeight = texHeight / (float)TREE_STRIPS;
        for (int i = 0; i < TREE_STRIPS; i++)
        {
            int oben = i * (int)stripHeight;
            int hoehe = i == TREE_STRIPS - 1 ? texHeight - oben : (int)stripHeight;
            float h = 1f - (oben + hoehe / 2f) / texHeight;
            float verschiebung = sway * target.Height * MathF.Pow(h, 1.5f);
            var source = new Rectangle(0, oben, texWidth, hoehe);
            var position = new Vector2(target.X + verschiebung, target.Y + oben * target.Height / (float)texHeight);
            var scale = new Vector2(target.Width / (float)texWidth, target.Height / (float)texHeight);
            yield return (source, position, scale);
        }
    }
}
