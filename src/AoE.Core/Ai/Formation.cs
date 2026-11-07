namespace AoE.Core.Ai;

/// <summary>Die Rolle einer Einheit in der Aufstellung (<see cref="Formation"/>).</summary>
public enum FormationRole
{
    /// <summary>Nahkämpfer: vorderste Glieder.</summary>
    Front,
    /// <summary>Schützen und Belagerung: in Linie hinter den Nahkämpfern.</summary>
    Ranged,
    /// <summary>Reiter: an beiden Flanken, außen neben dem ersten Glied.</summary>
    Flank,
}

/// <summary>
/// Die Aufstellung einer Armee (Nutzer 2026-11-07: „Bogenschützen hinten, Soldaten vorne
/// mit 1 Kachel Abstand zueinander … Berittene greifen im Schwall an … und das Ganze immer
/// in Formation"): wer auf welcher Kachel steht, wenn die Armee am Punkt
/// <c>anchor</c> mit Blick in Richtung <c>facing</c> steht. Reine Geometrie ohne Spielwelt;
/// <see cref="ArmyCommander"/> schickt die Einheiten auf diese Plätze.
/// </summary>
public static class Formation
{
    /// <summary>Freie Kacheln zwischen zwei Nachbarn in einem Glied und zwischen zwei Gliedern.</summary>
    public const int Gap = 1;

    /// <summary>Abstand von Kachelmitte zu Kachelmitte zweier Nachbarn: <see cref="Gap"/> + 1.</summary>
    public const int Spacing = Gap + 1;

    /// <summary>So viele Einheiten stehen höchstens nebeneinander in einem Glied.</summary>
    public const int PerRank = 5;

    /// <summary>So weit (Chebyshev-Ringe) wird um einen versperrten Platz nach einem freien gesucht.</summary>
    public const int SearchRadius = 3;

    /// <summary>Die Rolle einer Einheitenart: Schützen (Archer) und Belagerung (Catapult)
    /// <see cref="FormationRole.Ranged"/>, Reiter (Cavalry) <see cref="FormationRole.Flank"/>,
    /// alle anderen (Miliz, Speerkämpfer, Dorfbewohner) <see cref="FormationRole.Front"/>.</summary>
    public static FormationRole RoleOf(UnitKind kind) => kind switch
    {
        UnitKind.Archer or UnitKind.Catapult => FormationRole.Ranged,
        UnitKind.Cavalry => FormationRole.Flank,
        _ => FormationRole.Front,
    };

    /// <summary>
    /// Die Plätze aller <paramref name="units"/> in der Aufstellung.
    /// VERTRAG:
    /// - Richtung: f = <paramref name="facing"/> normiert (Länge 1); ist facing (0, 0), gilt
    ///   f = (1, 0). Die Querachse ist p = (-f.Y, f.X). „Vorn" ist +f, „hinten" -f.
    /// - Die Einheiten werden nach <see cref="RoleOf"/>(Kind) in drei Gruppen geteilt (Front,
    ///   Ranged, Flank). In jeder Gruppe wird nach Id aufsteigend sortiert - eine feste
    ///   Reihenfolge: jede Einheit behält ihren Platz, solange die Gruppe gleich bleibt, und rückt
    ///   beim Marsch mit dem Anker nach. (Bis 2026-11-07 nach der Querlage sortiert: Vorder- und
    ///   Hintermann einer Spalte haben dieselbe Querlage und tauschten bei jedem Takt die Plätze -
    ///   im Spiel stand die Welle so nur zu einem Viertel auf ihren Plätzen.)
    /// - Schrittweite S = <see cref="Spacing"/> / Math.Max(|f.X|, |f.Y|): entlang der Achsen genau
    ///   Spacing, schräg länger - so liegen zwei Nachbarn auf ihrer Hauptachse immer genau Spacing
    ///   Kacheln auseinander und haben auch diagonal eine freie Kachel zwischen sich (mit S = Spacing
    ///   stünden sie bei 45° Ecke an Ecke).
    /// - Idealplätze (Gleitkomma):
    ///   * Front: die i-te (0-basiert, in Sortierreihenfolge) steht im Glied r = i / PerRank in
    ///     Spalte c = i % PerRank; n = Zahl der Front-Einheiten in diesem Glied
    ///     (Math.Min(PerRank, front.Count - r * PerRank)). Querlage q = (c - (n - 1) / 2f) * S,
    ///     Tiefe t = -r * S. Platz = anchor + p * q + f * t.
    ///   * Ranged: genauso, aber hinter allen Front-Gliedern: frontRanks = Zahl der Front-Glieder
    ///     (Aufrunden von front.Count / PerRank, 0 ohne Front-Einheiten); das j-te Ranged steht im
    ///     Glied r = j / PerRank, Spalte c = j % PerRank, n entsprechend aus ranged.Count;
    ///     q = (c - (n - 1) / 2f) * S, t = -(frontRanks + r) * S.
    ///   * Flank: halbe Breite W = (Math.Min(PerRank, Math.Max(1, Math.Max(front.Count,
    ///     ranged.Count))) - 1) / 2f * S. Die k-te Flank-Einheit steht auf der Seite
    ///     s = (k % 2 == 0) ? -1 : +1 in der Reihe r = k / 2: q = s * (W + S), t = -r * S.
    /// - Runden: Kachel = ((int)MathF.Floor(x + 0.5f), (int)MathF.Floor(y + 0.5f)).
    /// - Vergabe in der Reihenfolge Front (sortiert), dann Ranged (sortiert), dann Flank
    ///   (sortiert): die Kachel jeder Einheit wählt <see cref="PickTile"/> aus ihrem Idealplatz
    ///   und den schon vergebenen Kacheln.
    /// - Rückgabe: für jede Einheit aus <paramref name="units"/> genau ein Eintrag Id → Kachel.
    ///   Leere Liste: leeres Wörterbuch.
    /// </summary>
    public static Dictionary<int, (int X, int Y)> Slots((int X, int Y) anchor, (float X, float Y) facing,
        IReadOnlyList<UnitSnapshot> units, Func<int, int, bool> walkable)
    {
        var result = new Dictionary<int, (int X, int Y)>();
        var taken = new HashSet<(int X, int Y)>();

        float len = MathF.Sqrt(facing.X * facing.X + facing.Y * facing.Y);
        float fx = len > 0f ? facing.X / len : 1f;
        float fy = len > 0f ? facing.Y / len : 0f;
        float px = -fy, py = fx;
        float step = Spacing / Math.Max(Math.Abs(fx), Math.Abs(fy));

        var front = units.Where(u => RoleOf(u.Kind) == FormationRole.Front).OrderBy(u => u.Id).ToList();
        var ranged = units.Where(u => RoleOf(u.Kind) == FormationRole.Ranged).OrderBy(u => u.Id).ToList();
        var flank = units.Where(u => RoleOf(u.Kind) == FormationRole.Flank).OrderBy(u => u.Id).ToList();

        int frontRanks = (front.Count + PerRank - 1) / PerRank;
        float w = (Math.Min(PerRank, Math.Max(1, Math.Max(front.Count, ranged.Count))) - 1) / 2f * step;

        void Place(UnitSnapshot u, float x, float y)
        {
            var tile = PickTile(x, y, anchor, taken, walkable);
            result[u.Id] = tile;
            taken.Add(tile);
        }

        for (int i = 0; i < front.Count; i++)
        {
            int r = i / PerRank;
            int c = i % PerRank;
            int n = Math.Min(PerRank, front.Count - r * PerRank);
            float q = (c - (n - 1) / 2f) * step;
            float t = -r * step;
            Place(front[i], anchor.X + px * q + fx * t, anchor.Y + py * q + fy * t);
        }

        for (int j = 0; j < ranged.Count; j++)
        {
            int r = j / PerRank;
            int c = j % PerRank;
            int n = Math.Min(PerRank, ranged.Count - r * PerRank);
            float q = (c - (n - 1) / 2f) * step;
            float t = -(frontRanks + r) * step;
            Place(ranged[j], anchor.X + px * q + fx * t, anchor.Y + py * q + fy * t);
        }

        for (int k = 0; k < flank.Count; k++)
        {
            int r = k / 2;
            float s = (k % 2 == 0) ? -1f : 1f;
            float q = s * (w + step);
            float t = -r * step;
            Place(flank[k], anchor.X + px * q + fx * t, anchor.Y + py * q + fy * t);
        }

        return result;
    }

    /// <summary>
    /// Die Kachel für eine Einheit mit dem Idealplatz (<paramref name="x"/>, <paramref name="y"/>),
    /// wenn die Kacheln in <paramref name="taken"/> schon vergeben sind. Nutzer 2026-11-07: „mit
    /// 1 Kachel Abstand zueinander" - auch dort, wo Häuser und Bäume Plätze versperren.
    /// VERTRAG:
    /// - Gerundete Kachel (ix, iy) = ((int)MathF.Floor(x + 0.5f), (int)MathF.Floor(y + 0.5f)).
    /// - Ring d (d = 0 bis <see cref="SearchRadius"/>): alle Kacheln (ix + dx, iy + dy) mit
    ///   Math.Max(|dx|, |dy|) == d (Ring 0 ist nur (ix, iy)), geordnet nach dem Abstandsquadrat
    ///   (Gleitkomma) von (X, Y) zum Idealpunkt (x, y), bei Gleichstand nach Y, dann nach X
    ///   (jeweils aufsteigend).
    /// - Eine Kachel ist frei, wenn <paramref name="walkable"/>(X, Y) true ist und sie nicht in
    ///   <paramref name="taken"/> steht. Sie ist frei mit Abstand, wenn sie frei ist und keine
    ///   ihrer acht Nachbarkacheln (Chebyshev-Abstand 1) in <paramref name="taken"/> steht.
    /// - Durchgang 1: die Ringe d = 0, 1, …, SearchRadius der Reihe nach, in jedem Ring die
    ///   Kacheln in obiger Ordnung - die erste Kachel, die frei mit Abstand ist, wird zurückgegeben.
    /// - Durchgang 2 (nur wenn Durchgang 1 nichts fand): dieselbe Reihenfolge, die erste freie
    ///   Kachel wird zurückgegeben.
    /// - Findet auch Durchgang 2 nichts: <paramref name="anchor"/> (auch wenn vergeben oder versperrt).
    /// - <paramref name="taken"/> wird nicht verändert (das macht der Aufrufer).
    /// </summary>
    public static (int X, int Y) PickTile(float x, float y, (int X, int Y) anchor,
        HashSet<(int X, int Y)> taken, Func<int, int, bool> walkable)
    {
        int ix = (int)MathF.Floor(x + 0.5f), iy = (int)MathF.Floor(y + 0.5f);

        var order = new List<(int X, int Y)>();
        for (int d = 0; d <= SearchRadius; d++)
        {
            var ring = new List<(int X, int Y)>();
            for (int dx = -d; dx <= d; dx++)
            {
                for (int dy = -d; dy <= d; dy++)
                {
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == d)
                        ring.Add((ix + dx, iy + dy));
                }
            }
            order.AddRange(ring
                .OrderBy(c => (c.X - x) * (c.X - x) + (c.Y - y) * (c.Y - y))
                .ThenBy(c => c.Y)
                .ThenBy(c => c.X));
        }

        bool Free((int X, int Y) c) => walkable(c.X, c.Y) && !taken.Contains(c);

        bool Spaced((int X, int Y) c)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0)
                        continue;
                    if (taken.Contains((c.X + dx, c.Y + dy)))
                        return false;
                }
            }
            return true;
        }

        foreach (var c in order)
            if (Free(c) && Spaced(c))
                return c;

        foreach (var c in order)
            if (Free(c))
                return c;

        return anchor;
    }
}
