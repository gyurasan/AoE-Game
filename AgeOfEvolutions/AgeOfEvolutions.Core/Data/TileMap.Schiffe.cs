using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using MapGrid = AoE.Core.Map.MapGrid;
using CorePosition = AoE.Core.Entities.Position;
using CorePathfinding = AoE.Core.Pathfinding.Pathfinding;

namespace AgeOfEvolutions.Core.Data;

// -----------------------------------------------------------------
// Schiffe und Hafen (Wiking-Karte)
// -----------------------------------------------------------------
// Wunsch des Nutzers (2026-11-07): „In der Wiking Version müssen wir dann noch Hafen erstellen
// können. Die Inseln müssen da komplett von Wasser umgeben sein und man wird nur mit booten von
// einer zur nächsten insel kommen.“ Schiffe fahren nur auf Wasser (IsNavigable), der Hafen
// steht auf Wasser am Ufer (CanPlaceDock), und Anlegestellen (Landing) verbinden Wasser und Land.
public partial class TileMap
{
    /// <summary>
    /// Ob ein Schiff die Kachel befahren kann: Wasser ohne Gebäude — ein Hafen sperrt seine
    /// Fläche auch für Schiffe. Außerhalb der Karte false.
    /// </summary>
    public bool IsNavigable(int x, int y)
    {
        var t = GetTile(x, y);
        return t != null && t.Type == TileType.Water && string.IsNullOrEmpty(t.Building);
    }

    // Gitter für die Schiffswege, wird wiederverwendet
    private MapGrid _waterGrid;

    /// <summary>
    /// Der Weg eines Schiffs von <paramref name="start"/> nach <paramref name="end"/>
    /// (Weltkoordinaten) über befahrbares Wasser.
    /// VERTRAG: wie FindPathKnown, nur mit IsNavigable statt dem Wissen eines Spielers —
    /// Wasser bleibt Wasser, ein Schiff verrät damit nichts. Ziel ist die Kachel unter
    /// <paramref name="end"/>, wenn sie vom Start aus über Wasser erreichbar ist, sonst die
    /// erreichbare Wasserkachel, die ihr am nächsten liegt. Rückgabe: Kachelmitten ohne die
    /// Startkachel; ist das Ziel genau die Kachel unter end, liegt der letzte Punkt auf end.
    /// Liegt der Start auf keiner befahrbaren Kachel (Schiff am Hafen aufgesetzt), beginnt
    /// die Suche an der nächsten befahrbaren Nachbarkachel.
    /// </summary>
    public List<Vector2> FindShipPath(Vector2 start, Vector2 end)
    {
        var path = new List<Vector2>();
        _waterGrid ??= new MapGrid(Width, Height);
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                _waterGrid.GetTile(x, y).IsPassable = IsNavigable(x, y);

        int sx = Math.Clamp((int)MathF.Floor(start.X / TileSize), 0, Width - 1);
        int sy = Math.Clamp((int)MathF.Floor(start.Y / TileSize), 0, Height - 1);
        if (!IsNavigable(sx, sy))
        {
            // nächste befahrbare Kachel ringsum (Ring 1 bis 2)
            bool found = false;
            for (int r = 1; r <= 2 && !found; r++)
                for (int dx = -r; dx <= r && !found; dx++)
                    for (int dy = -r; dy <= r && !found; dy++)
                        if (IsNavigable(sx + dx, sy + dy))
                        {
                            sx += dx;
                            sy += dy;
                            found = true;
                        }
            if (!found) return path;
            path.Add(GridToWorld(new Vector2(sx, sy)));
        }

        int zx = Math.Clamp((int)MathF.Floor(end.X / TileSize), 0, Width - 1);
        int zy = Math.Clamp((int)MathF.Floor(end.Y / TileSize), 0, Height - 1);

        // Breitensuche: die erreichbare Wasserkachel, die dem Ziel am nächsten liegt
        var visited = new bool[Width, Height];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((sx, sy));
        visited[sx, sy] = true;
        var best = (x: sx, y: sy);
        int bestDist = (sx - zx) * (sx - zx) + (sy - zy) * (sy - zy);
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (dx, dy) in FourDirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                if (visited[nx, ny] || !IsNavigable(nx, ny)) continue;
                visited[nx, ny] = true;
                queue.Enqueue((nx, ny));
                int dist = (nx - zx) * (nx - zx) + (ny - zy) * (ny - zy);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = (nx, ny);
                }
            }
        }
        if (best.x == sx && best.y == sy)
            return path;

        var steps = CorePathfinding.FindPath(_waterGrid, new CorePosition(sx, sy), new CorePosition(best.x, best.y));
        if (steps == null || steps.Count == 0)
            return path;
        for (int i = 1; i < steps.Count; i++)
            path.Add(GridToWorld(new Vector2(steps[i].X, steps[i].Y)));
        if (best.x == zx && best.y == zy && path.Count > 0)
            path[path.Count - 1] = end;
        return path;
    }

    /// <summary>
    /// Ob ein Schiff geradeaus von <paramref name="from"/> nach <paramref name="to"/> fahren
    /// kann: wie IsSegmentWalkable, nur auf befahrbarem Wasser (IsNavigable) und mit
    /// <paramref name="clearance"/> Abstand quer zur Strecke.
    /// </summary>
    public bool IsSegmentNavigable(Vector2 from, Vector2 to, float clearance)
    {
        bool Frei(Vector2 p) => p.X >= 0 && p.Y >= 0 && IsNavigable((int)(p.X / TileSize), (int)(p.Y / TileSize));
        var d = to - from;
        float len = d.Length();
        if (len < 0.0001f)
            return Frei(from);
        int n = (int)MathF.Ceiling(len / 4f);
        var quer = new Vector2(-d.Y, d.X) / len * clearance;
        for (int i = 0; i <= n; i++)
        {
            var p = from + d * (i / (float)n);
            if (!Frei(p) || !Frei(p + quer) || !Frei(p - quer))
                return false;
        }
        return true;
    }

    /// <summary>So viele befahrbare Kacheln hängen mindestens zusammen, damit Wasser als offen gilt
    /// (<see cref="OpenWater"/>) - ein Tümpel hinter dem Hafen ist kein Hafenbecken.</summary>
    public const int DOCK_OPEN_WATER = 40;

    /// <summary>
    /// Ob ein Hafen der Kantenlänge <paramref name="size"/> mit der linken oberen Ecke auf
    /// (x, y) Platz hat. Er steht am Strand: halb im Wasser, halb auf Land, oder ganz im Wasser
    /// direkt davor (Nutzer 2026-11-07: „es muss am strand möglich sein, also ein Teil im Wasser
    /// ein Teil auf land“, „den hafen können wir auch direkt ins Wasser stellen solange er direkt am
    /// Stran ist, also 1 kachel maximal von land“).
    /// VERTRAG: jede Kachel der Grundfläche liegt auf der Karte und trägt kein Gebäude; sie ist
    /// entweder Wasser (ein Fischschwarm darf darunter liegen - er weicht dem Hafen, ClearFish)
    /// oder freies Bauland (Wiese oder Sand, begehbar, bebaubar, ohne Ressource, kein Feld).
    /// Mindestens <paramref name="size"/> Kacheln sind Wasser. Im Ring direkt um die Grundfläche
    /// (die vier Seiten, ohne Ecken) liegt mindestens eine begehbare Landkachel ohne Gebäude — der
    /// Strand höchstens eine Kachel entfernt, von dort bauen die Dorfbewohner — und mindestens eine
    /// befahrbare Wasserkachel, von der aus offenes Wasser erreichbar ist (OpenWater mit der
    /// Grundfläche als gesperrt) — dort legen die Schiffe ab.
    /// </summary>
    public bool CanPlaceDock(int x, int y, int size)
    {
        int water = 0, land = 0;
        for (int bx = x; bx < x + size; bx++)
        {
            for (int by = y; by < y + size; by++)
            {
                var t = GetTile(bx, by);
                if (t == null || !string.IsNullOrEmpty(t.Building))
                    return false;
                if (t.Type == TileType.Water)
                    water++;
                else if ((t.Type == TileType.Grassland || t.Type == TileType.Sand) && t.Walkable && t.Buildable
                         && !t.ResourceType.HasValue && !t.Farm)
                    land++;
                else
                    return false;
            }
        }
        if (water < size)
            return false;
        bool ringLand = false, ringWater = false;
        var footprint = new Rectangle(x, y, size, size);
        foreach (var (rx, ry) in DockRing(x, y, size))
        {
            if (IsWalkable(rx, ry)) ringLand = true;
            else if (!ringWater && IsNavigable(rx, ry) && OpenWater(rx, ry, DOCK_OPEN_WATER, footprint)) ringWater = true;
        }
        return ringLand && ringWater;
    }

    /// <summary>
    /// Ob von der befahrbaren Kachel (x, y) aus mindestens <paramref name="min"/> befahrbare
    /// Kacheln zusammenhängen (vier Richtungen, Kacheln in <paramref name="blocked"/> zählen als
    /// gesperrt) - false, wenn (x, y) selbst nicht befahrbar ist.
    /// </summary>
    public bool OpenWater(int x, int y, int min = DOCK_OPEN_WATER, Rectangle? blocked = null)
    {
        bool Free(int px, int py) => IsNavigable(px, py) && !(blocked is { } b && b.Contains(px, py));
        if (!Free(x, y)) return false;
        var seen = new HashSet<Point> { new Point(x, y) };
        var queue = new Queue<Point>();
        queue.Enqueue(new Point(x, y));
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var (dx, dy) in FourDirs)
            {
                var n = new Point(p.X + dx, p.Y + dy);
                if (!Free(n.X, n.Y) || !seen.Add(n)) continue;
                if (seen.Count >= min) return true;
                queue.Enqueue(n);
            }
        }
        return seen.Count >= min;
    }

    /// <summary>
    /// Die Landmasse, zu der (x, y) gehört - auch wenn die Kachel selbst Wasser ist (eine Einheit
    /// auf der Hafenfläche oder beim Anlegen abgedrängt): dann die Landmasse, die unter den acht
    /// Nachbarn am häufigsten vorkommt, sonst im Ring zwei Kacheln weit; 0, wenn kein Land so nah ist.
    /// </summary>
    public int LandRegionAround(int x, int y)
    {
        int own = LandRegion(x, y);
        if (own != 0) return own;
        for (int r = 1; r <= 2; r++)
        {
            var counts = new Dictionary<int, int>();
            for (int dx = -r; dx <= r; dx++)
                for (int dy = -r; dy <= r; dy++)
                    if (Math.Max(Math.Abs(dx), Math.Abs(dy)) == r && LandRegion(x + dx, y + dy) is var n && n != 0)
                        counts[n] = counts.GetValueOrDefault(n) + 1;
            if (counts.Count > 0)
                return counts.OrderByDescending(c => c.Value).First().Key;
        }
        return 0;
    }

    /// <summary>
    /// Fischschwärme auf der Grundfläche (x, y, size) verschwinden - dort steht jetzt der Hafen.
    /// </summary>
    public void ClearFish(int x, int y, int size)
    {
        for (int bx = x; bx < x + size; bx++)
            for (int by = y; by < y + size; by++)
                if (GetTile(bx, by) is { Food: FoodSource.Fish } t)
                {
                    t.Food = FoodSource.None;
                    t.ResourceType = null;
                    t.ResourceAmount = 0;
                }
    }

    /// <summary>Die Kacheln direkt an den vier Seiten einer Grundfläche (ohne Ecken).</summary>
    private static IEnumerable<(int x, int y)> DockRing(int x, int y, int size)
    {
        for (int i = 0; i < size; i++)
        {
            yield return (x + i, y - 1);
            yield return (x + i, y + size);
            yield return (x - 1, y + i);
            yield return (x + size, y + i);
        }
    }

    /// <summary>
    /// Die Anlegestelle eines Schiffs, das von der Wasserkachel (<paramref name="fromX"/>,
    /// <paramref name="fromY"/>) aus an die Landmasse <paramref name="landRegion"/> will, so nah
    /// wie möglich an (<paramref name="targetX"/>, <paramref name="targetY"/>).
    /// VERTRAG: unter allen befahrbaren Wasserkacheln, die das Schiff erreicht (Breitensuche in
    /// vier Richtungen über IsNavigable; liegt der Start selbst nicht auf befahrbarem Wasser, gilt
    /// jede befahrbare Nachbarkachel als Start), und allen begehbaren Landkacheln der Region
    /// direkt daneben (acht Richtungen) gewinnt das Paar, dessen Landkachel dem Ziel am nächsten
    /// liegt (quadrierter Abstand; bei Gleichstand die zuerst gefundene). Ist
    /// <paramref name="allowed"/> gesetzt, zählen nur Landkacheln daraus (etwa die, die eine Einheit
    /// zu Fuß erreicht - Wald schneidet sonst Uferstücke ab). null, wenn das Schiff diese
    /// Landmasse nirgends erreicht.
    /// </summary>
    public (Point Water, Point Land)? Landing(int fromX, int fromY, int targetX, int targetY, int landRegion,
                                             ISet<Point> allowed = null)
    {
        if (landRegion == 0) return null;
        var visited = new bool[Width, Height];
        var queue = new Queue<(int x, int y)>();
        void Seed(int x, int y)
        {
            if (!IsNavigable(x, y) || visited[x, y]) return;
            visited[x, y] = true;
            queue.Enqueue((x, y));
        }
        Seed(fromX, fromY);
        if (queue.Count == 0)
            for (int dx = -2; dx <= 2; dx++)
                for (int dy = -2; dy <= 2; dy++)
                    Seed(fromX + dx, fromY + dy);

        (Point Water, Point Land)? best = null;
        long bestDist = long.MaxValue;
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    if (dx == 0 && dy == 0) continue;
                    int lx = cx + dx, ly = cy + dy;
                    if (!IsWalkable(lx, ly) || LandRegion(lx, ly) != landRegion) continue;
                    if (allowed != null && !allowed.Contains(new Point(lx, ly))) continue;
                    long d = (long)(lx - targetX) * (lx - targetX) + (long)(ly - targetY) * (ly - targetY);
                    if (d < bestDist)
                    {
                        bestDist = d;
                        best = (new Point(cx, cy), new Point(lx, ly));
                    }
                }
            }
            foreach (var (dx, dy) in FourDirs)
                Seed(cx + dx, cy + dy);
        }
        return best;
    }

    /// <summary>
    /// Alle begehbaren Kacheln, die man von <paramref name="from"/> aus zu Fuß erreicht (vier
    /// Richtungen, from selbst eingeschlossen, wenn begehbar), höchstens <paramref name="limit"/>.
    /// </summary>
    public HashSet<Point> WalkReach(Point from, int limit = 6000)
    {
        var seen = new HashSet<Point>();
        if (!IsWalkable(from.X, from.Y))
        {
            // Wer auf einer gesperrten Kachel steht (am Gebäude), startet bei den Nachbarn
            foreach (var (dx, dy) in FourDirs)
                if (IsWalkable(from.X + dx, from.Y + dy))
                    seen.Add(new Point(from.X + dx, from.Y + dy));
        }
        else
            seen.Add(from);
        var queue = new Queue<Point>(seen);
        while (queue.Count > 0 && seen.Count < limit)
        {
            var p = queue.Dequeue();
            foreach (var (dx, dy) in FourDirs)
            {
                var n = new Point(p.X + dx, p.Y + dy);
                if (IsWalkable(n.X, n.Y) && seen.Add(n))
                    queue.Enqueue(n);
            }
        }
        return seen;
    }

    /// <summary>
    /// Alle begehbaren Kacheln der Landmasse <paramref name="landRegion"/>, deren zu Fuß
    /// zusammenhängendes Stück (vier Richtungen) mindestens <paramref name="min"/> Kacheln hat -
    /// Gebäude riegeln sonst kleine Uferstücke ab, auf denen nicht alle Fahrgäste Platz finden.
    /// </summary>
    public HashSet<Point> RoomyLand(int landRegion, int min)
    {
        var result = new HashSet<Point>();
        var seen = new HashSet<Point>();
        for (int sx = 0; sx < Width; sx++)
        {
            for (int sy = 0; sy < Height; sy++)
            {
                var s = new Point(sx, sy);
                if (seen.Contains(s) || !IsWalkable(sx, sy) || LandRegion(sx, sy) != landRegion) continue;
                var piece = new List<Point> { s };
                seen.Add(s);
                for (int i = 0; i < piece.Count; i++)
                {
                    foreach (var (dx, dy) in FourDirs)
                    {
                        var n = new Point(piece[i].X + dx, piece[i].Y + dy);
                        if (!IsWalkable(n.X, n.Y) || !seen.Add(n)) continue;
                        piece.Add(n);
                    }
                }
                if (piece.Count >= min)
                    result.UnionWith(piece);
            }
        }
        return result;
    }

    /// <summary>
    /// Freie Landkacheln zum Aussteigen: von <paramref name="land"/> aus in Breitensuche (vier
    /// Richtungen) über begehbare Kacheln derselben Landmasse ohne Gebäude, höchstens
    /// <paramref name="count"/> Stück, die erste ist <paramref name="land"/> selbst (wenn begehbar).
    /// Liefert weniger, wenn die Küste nicht mehr hergibt.
    /// </summary>
    public List<Point> ShoreCells(Point land, int count)
    {
        var result = new List<Point>();
        int region = LandRegion(land.X, land.Y);
        if (region == 0 || !IsWalkable(land.X, land.Y)) return result;
        var visited = new HashSet<Point> { land };
        var queue = new Queue<Point>();
        queue.Enqueue(land);
        while (queue.Count > 0 && result.Count < count)
        {
            var p = queue.Dequeue();
            result.Add(p);
            foreach (var (dx, dy) in FourDirs)
            {
                var n = new Point(p.X + dx, p.Y + dy);
                if (!visited.Add(n)) continue;
                if (!IsWalkable(n.X, n.Y) || LandRegion(n.X, n.Y) != region) continue;
                queue.Enqueue(n);
            }
        }
        return result;
    }
}
