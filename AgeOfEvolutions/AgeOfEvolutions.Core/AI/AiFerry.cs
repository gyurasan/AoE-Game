using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Screens;
using Microsoft.Xna.Framework;
using BuildingType = AoE.Core.Entities.BuildingType;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using CoreTransportShip = AoE.Core.Entities.TransportShip;

namespace AgeOfEvolutions.Core.AI;

/// <summary>
/// Der Fährdienst der KI (Wiking-Karte, Nutzer 2026-11-07: „man wird nur mit booten von einer zur
/// nächsten insel kommen“). Die KI aus AoE.Core kennt keine Schiffe: sie schickt ihre Einheiten
/// wie überall per Move und Attack los. Liegt das Ziel auf einer anderen Landmasse, gibt die
/// Brücke den Befehl hierher (<see cref="Request"/>): die Einheit wartet auf ein Transportschiff,
/// steigt ein, wird an der Küste nahe dem Ziel abgesetzt und läuft von dort weiter.
/// Dazu baut der Dienst auf Inselkarten selbst einen Hafen und hält TRANSPORTS Transportschiffe
/// bereit, ab der Feudalzeit auch WARSHIPS Kriegsschiffe, die den Hafen schützen.
/// </summary>
public sealed class AiFerry
{
    /// <summary>So viele Transportschiffe hält die KI bereit.</summary>
    public const int TRANSPORTS = 2;

    /// <summary>So viele Kriegsschiffe baut die KI ab der Feudalzeit.</summary>
    public const int WARSHIPS = 2;

    /// <summary>Sekunden, die ein Schiff höchstens auf Nachzügler wartet, bevor es ablegt.</summary>
    public const float LOAD_PATIENCE = 20f;

    /// <summary>Ab so vielen Dorfbewohnern baut die KI den Hafen.</summary>
    public const int DOCK_VILLAGERS = 8;

    /// <summary>Wie weit (Kacheln) vom Stadtzentrum die KI einen Hafenplatz sucht.</summary>
    public const int DOCK_SEARCH_RADIUS = 26;

    /// <summary>Seeerkundung: so weit (Kacheln) liegt das nächste Ziel des Schiffs mindestens.</summary>
    public const int SCOUT_HOP = 12;

    /// <summary>Holz, das nach einem Kauf übrig bleiben soll - die Wirtschaft braucht es weiter.</summary>
    private const int WOOD_RESERVE = 50;

    private readonly RTSGameplayScreen _screen;

    public AiFerry(RTSGameplayScreen screen, int owner)
    {
        _screen = screen ?? throw new ArgumentNullException(nameof(screen));
        Owner = owner;
    }

    public int Owner { get; }

    /// <summary>Optionales Debug-Log (wie AiBridge.DebugLog).</summary>
    public Action<string>? Log { get; set; }

    // Wer übergesetzt werden will: Einheit → Zielkachel
    private readonly Dictionary<Unit, Point> _wanted = new();

    // Laufende Fahrten je Schiff
    private readonly Dictionary<Unit, Trip> _trips = new();

    private sealed class Trip
    {
        public List<Unit> Riders = new();
        public Point Target;
        public float Loading;
        public bool Sailing;
        public int Landings;
    }

    private float _shipyardTimer;
    private bool _islandMap;

    // Seeerkundung: das erkundende Schiff, sein Ziel, Ziele, die es nicht aufdecken konnte, und ob
    // schon ein feindliches Gebäude gesichtet wurde (dann endet die Erkundung)
    private Unit? _scoutShip;
    private Point? _scoutTarget;
    private readonly HashSet<Point> _scoutMissed = new();
    private bool _enemySighted;

    /// <summary>Das Schiff, das gerade das Meer erkundet, oder null.</summary>
    public Unit? ScoutShip => _scoutShip;

    /// <summary>Wie viele Einheiten gerade auf ein Schiff warten oder an Bord unterwegs sind.</summary>
    public int Waiting => _wanted.Count;

    /// <summary>
    /// Ob (<paramref name="x"/>, <paramref name="y"/>) von <paramref name="unit"/> aus nur über
    /// Wasser zu erreichen ist: beide Kacheln liegen auf Land, aber auf verschiedenen Landmassen.
    /// Steht die Einheit auf Wasser (Hafenfläche, beim Anlegen abgedrängt), zählt die Landmasse
    /// daneben (TileMap.LandRegionAround).
    /// </summary>
    public bool NeedsFerry(Unit unit, int x, int y)
    {
        var map = _screen.tileMap;
        if (unit.IsShip) return false;
        var at = map.WorldToGrid(unit.Position);
        int from = map.LandRegionAround((int)at.X, (int)at.Y);
        int to = map.LandRegion(x, y);
        return from != 0 && to != 0 && from != to;
    }

    /// <summary>Die Einheit will nach (x, y) auf einer anderen Insel; ein neuer Wunsch ersetzt den alten.</summary>
    public void Request(Unit unit, int x, int y) => _wanted[unit] = new Point(x, y);

    /// <summary>Die Einheit hat einen Befehl auf ihrer eigenen Insel bekommen - sie fährt nicht mehr mit
    /// und läuft auch nicht mehr zum Schiff.</summary>
    public void Cancel(Unit unit)
    {
        _wanted.Remove(unit);
        foreach (var trip in _trips.Values)
            trip.Riders.Remove(unit);
        unit.BoardShip = null;
    }

    /// <summary>
    /// Ein Takt des Fährdiensts (alle 0,5 s mit der KI).
    /// VERTRAG, in dieser Reihenfolge:
    /// 1. Aufräumen: Wünsche toter Einheiten und solcher, deren Ziel jetzt auf ihrer Landmasse liegt
    ///    (abgesetzt), fallen weg.
    /// 2. Fahrten: ein gesunkenes Schiff beendet seine Fahrt. Beim Laden legt das Schiff ab
    ///    (RTSGameplayScreen.CommandShips auf das Ziel des ersten Fahrgasts), sobald jemand an Bord
    ///    ist und keiner mehr zuläuft, das Schiff voll ist oder LOAD_PATIENCE Sekunden vergangen
    ///    sind; wer dann noch zuläuft, lässt das Schiff los (BoardShip null) und wartet aufs
    ///    nächste. Ohne Fahrgäste endet die Fahrt, wenn keiner mehr zuläuft oder 2 × LOAD_PATIENCE
    ///    vergangen sind (dann lassen die Zulaufenden ebenso los). Nach dem Absetzen (UnloadAt
    ///    null) endet sie, sind noch Fahrgäste an Bord, legt es bis zu dreimal neu an.
    /// 3. Wartende, die zu keiner Fahrt gehören, verteilen sich auf freie Transportschiffe: je
    ///    Schiff bis zu TransportShip.CAPACITY von derselben Landmasse, die nächsten zuerst
    ///    (RTSGameplayScreen.BoardUnits).
    /// 4. Alle 5 Sekunden, nur auf Inselkarten (eigenes und gegnerisches Stadtzentrum auf
    ///    verschiedenen Landmassen): Hafen bauen (UpdateShipyard).
    /// 5. Auf Inselkarten, solange kein feindliches Gebäude gesichtet ist: Seeerkundung (ScoutSea).
    /// </summary>
    public void Tick(float dt)
    {
        var map = _screen.tileMap;
        var units = _screen.units;

        // 1. Aufräumen
        foreach (var (unit, target) in _wanted.ToList())
        {
            bool aboard = _trips.Keys.Any(s => s.Passengers.Contains(unit));
            string why = unit.State == UnitState.Dead ? "tot"
                : !aboard && !units.Contains(unit) ? "nicht mehr auf der Karte"
                : !aboard && !NeedsFerry(unit, target.X, target.Y) ? "angekommen"
                : null;
            if (why == null) continue;
            _wanted.Remove(unit);
            Log?.Invoke($"Fähre: Einheit {unit.Id} fällt weg ({why})");
        }

        // 2. Fahrten
        foreach (var (ship, trip) in _trips.ToList())
        {
            if (ship.State == UnitState.Dead || !units.Contains(ship))
            {
                _trips.Remove(ship);
                continue;
            }
            if (trip.Sailing)
            {
                if (ship.UnloadAt != null)
                    continue;
                if (ship.Passengers.Count > 0 && trip.Landings < 3)
                    Sail(ship, trip);
                else
                    _trips.Remove(ship);
                continue;
            }

            trip.Loading += dt;
            trip.Riders.RemoveAll(r => r.State == UnitState.Dead
                                       || (r.BoardShip != ship && !ship.Passengers.Contains(r)));
            bool walking = trip.Riders.Any(r => r.BoardShip == ship);
            int aboard = ship.Passengers.Count;
            if (aboard > 0 && (!walking || aboard >= CoreTransportShip.CAPACITY || trip.Loading >= LOAD_PATIENCE))
            {
                Release(ship, trip);
                Sail(ship, trip);
            }
            else if (aboard == 0 && (!walking || trip.Loading >= 2f * LOAD_PATIENCE))
            {
                Release(ship, trip);
                _trips.Remove(ship);
            }
        }

        // 3. Wartende auf freie Schiffe
        var riding = new HashSet<Unit>(_trips.Values.SelectMany(t => t.Riders));
        var waiting = _wanted.Keys.Where(u => units.Contains(u) && u.BoardShip == null && !riding.Contains(u)).ToList();
        if (waiting.Count > 0)
        {
            var free = units.Where(s => s.OwnerId == Owner && s.Type == UnitType.TransportShip
                                        && s.State != UnitState.Dead && !_trips.ContainsKey(s)
                                        && s.Passengers.Count == 0 && s.UnloadAt == null).ToList();
            foreach (var ship in free)
            {
                if (waiting.Count == 0) break;
                var lead = waiting[0];
                var leadCell = map.WorldToGrid(lead.Position);
                int region = map.LandRegionAround((int)leadCell.X, (int)leadCell.Y);
                var group = waiting.Where(u =>
                    {
                        var c = map.WorldToGrid(u.Position);
                        return map.LandRegionAround((int)c.X, (int)c.Y) == region;
                    })
                    .OrderBy(u => Vector2.DistanceSquared(u.Position, lead.Position))
                    .Take(CoreTransportShip.CAPACITY)
                    .ToList();
                _screen.BoardUnits(group, ship);
                var boarding = group.Where(u => u.BoardShip == ship).ToList();
                waiting.RemoveAll(group.Contains);
                if (boarding.Count == 0)
                    continue;   // das Schiff erreicht diese Küste nicht - das nächste versucht es
                _trips[ship] = new Trip { Riders = boarding, Target = _wanted[lead] };
                Log?.Invoke($"Fähre: {boarding.Count} Einheiten steigen ein, Ziel ({_wanted[lead].X},{_wanted[lead].Y})");
            }
        }

        // 4. Hafen und Schiffe
        _shipyardTimer -= dt;
        if (_shipyardTimer <= 0f)
        {
            _shipyardTimer = 5f;
            _islandMap = IsIslandMap();
            if (_islandMap)
                UpdateShipyard();
        }

        // 5. Seeerkundung
        if (_islandMap)
            ScoutSea();
    }

    /// <summary>
    /// Seeerkundung: die Landarmee findet auf einer Inselkarte keinen Gegner - die nächste Kachel im
    /// Nebel ist fast immer Meer. Bis ein feindliches Gebäude gesichtet ist, fährt deshalb ein Schiff
    /// von Nebel zu Nebel; sieht es eines, kennt die Armeeführung ihr Ziel (VisibleEnemies) und setzt
    /// über. Wo der Gegner liegt, weiß die KI dabei nicht - wie der Späher an Land (K8).
    /// VERTRAG:
    /// - Gesichtet: ein Feldgebäude des Gegners ist für den Owner gerade sichtbar (IsTileVisible) -
    ///   danach endet die Erkundung für immer, das Schiff bleibt, wo es ist.
    /// - Das Schiff: das bisherige, solange es lebt und keine Fahrt des Fährdiensts hat; sonst ein
    ///   Kriegsschiff, sonst ein leeres Transportschiff ohne Fahrt.
    /// - Nur ein stehendes Schiff (Idle) bekommt ein neues Ziel. Liegt sein altes Ziel dann noch im
    ///   Nebel, kam es nicht hin (Land, anderes Gewässer) - das Ziel wird nie wieder gewählt.
    /// - Neues Ziel: die Kachel im Nebel (IsTileExplored false) nächst dem Schiff (kleinster
    ///   quadrierter Abstand, dann kleineres Y, dann X), aber mindestens SCOUT_HOP Kacheln entfernt,
    ///   solange es so eine gibt - die nächste liegt sonst gleich hinter dem Rand der Sicht, und das
    ///   Schiff hielte alle paar Kacheln an; das Schiff fährt per CommandShips dorthin
    ///   (FindShipPath fährt bis ans nächste erreichbare Wasser).
    /// </summary>
    private void ScoutSea()
    {
        var map = _screen.tileMap;
        var units = _screen.units;
        if (!_enemySighted)
            _enemySighted = map.Buildings.Any(b => b.OwnerId != Owner && Seen(b));
        if (_enemySighted)
        {
            _scoutShip = null;
            return;
        }
        var ship = _scoutShip;
        if (ship != null && (ship.State == UnitState.Dead || !units.Contains(ship) || _trips.ContainsKey(ship)
                             || ship.Passengers.Count > 0))
            ship = null;
        if (ship == null)
        {
            ship = units.Where(u => u.OwnerId == Owner && u.State != UnitState.Dead
                                    && (u.Type == UnitType.WarShip
                                        || (u.Type == UnitType.TransportShip && !_trips.ContainsKey(u)
                                            && u.Passengers.Count == 0 && u.UnloadAt == null)))
                        .OrderBy(u => u.Type == UnitType.WarShip ? 0 : 1)
                        .FirstOrDefault();
            if (ship != _scoutShip)
            {
                _scoutShip = ship;
                _scoutTarget = null;
                if (ship != null)
                    Log?.Invoke($"Fähre: {ship.Type} {ship.Id} erkundet das Meer");
            }
            if (ship == null) return;
        }
        if (ship.State != UnitState.Idle) return;
        if (_scoutTarget is { } old && !map.IsTileExplored(old.X, old.Y, Owner))
            _scoutMissed.Add(old);
        var from = map.WorldToGrid(ship.Position);
        Point? best = null, near = null;
        long bestDist = long.MaxValue, nearDist = long.MaxValue;
        const long hop = (long)SCOUT_HOP * SCOUT_HOP;
        for (int y = 0; y < map.Height; y++)
            for (int x = 0; x < map.Width; x++)
            {
                if (map.IsTileExplored(x, y, Owner)) continue;
                var p = new Point(x, y);
                if (_scoutMissed.Contains(p)) continue;
                long dx = x - (int)from.X, dy = y - (int)from.Y, d = dx * dx + dy * dy;
                if (d >= hop && d < bestDist) { bestDist = d; best = p; }
                if (d < nearDist) { nearDist = d; near = p; }
            }
        best ??= near;
        _scoutTarget = best;
        if (best is { } next)
            _screen.CommandShips(new List<Unit> { ship }, new Vector2(next.X, next.Y));
    }

    /// <summary>Ob eine Kachel des Gebäudes für den Owner gerade sichtbar ist.</summary>
    private bool Seen(Building b)
    {
        var map = _screen.tileMap;
        for (int x = b.X; x < b.X + b.Width; x++)
            for (int y = b.Y; y < b.Y + b.Height; y++)
                if (map.IsTileVisible(x, y, Owner))
                    return true;
        return false;
    }

    /// <summary>Wer noch zum Schiff läuft, lässt es los und wartet wieder auf ein freies (Schritt 3).</summary>
    private static void Release(Unit ship, Trip trip)
    {
        foreach (var r in trip.Riders)
        {
            if (r.BoardShip != ship) continue;
            r.BoardShip = null;
            r.Path.Clear();
            r.TargetPosition = r.Position;
            r.State = UnitState.Idle;
        }
        trip.Riders.RemoveAll(r => !ship.Passengers.Contains(r));
    }

    /// <summary>Legt ab: an die Küste nahe dem Ziel des ersten Fahrgasts, der eins hat.</summary>
    private void Sail(Unit ship, Trip trip)
    {
        foreach (var p in ship.Passengers)
        {
            if (_wanted.TryGetValue(p, out var t))
            {
                trip.Target = t;
                break;
            }
        }
        _screen.CommandShips(new List<Unit> { ship }, new Vector2(trip.Target.X, trip.Target.Y));
        trip.Sailing = true;
        trip.Landings++;
        Log?.Invoke($"Fähre: legt mit {ship.Passengers.Count} an Bord ab nach ({trip.Target.X},{trip.Target.Y})");
    }

    /// <summary>Inselkarte: das eigene und das gegnerische Stadtzentrum liegen auf verschiedenen Landmassen.</summary>
    private bool IsIslandMap()
    {
        var map = _screen.tileMap;
        var own = map.Buildings.FirstOrDefault(b => b.OwnerId == Owner && b.Core.BuildingType == BuildingType.TownCenter);
        var foe = map.Buildings.FirstOrDefault(b => b.OwnerId != Owner && b.Core.BuildingType == BuildingType.TownCenter);
        if (own == null || foe == null) return false;
        return !map.SameLand(own.X + own.Width / 2, own.Y + own.Height / 2, foe.X + foe.Width / 2, foe.Y + foe.Height / 2);
    }

    /// <summary>
    /// Hafen und Schiffe der KI.
    /// VERTRAG: ohne eigenen Hafen (auch keine Baustelle) und mit mindestens DOCK_VILLAGERS
    /// Dorfbewohnern und Holz für Hafen plus WOOD_RESERVE legt sie einen an (FindDockSite) und
    /// schickt die zwei nächsten Dorfbewohner hin. Mit fertigem Hafen bildet sie Transportschiffe
    /// aus, bis TRANSPORTS da oder bestellt sind, danach ab der Feudalzeit Kriegsschiffe bis
    /// WARSHIPS - immer nur, wenn danach noch WOOD_RESERVE Holz übrig ist.
    /// </summary>
    private void UpdateShipyard()
    {
        var map = _screen.tileMap;
        var player = Owner == 0 ? _screen.player1 : _screen.player2;
        var docks = map.Buildings.Where(b => b.OwnerId == Owner && b.Core.BuildingType == BuildingType.Dock).ToList();
        int wood = player.Resources[Resource.Wood];
        if (docks.Count == 0)
        {
            var villagers = _screen.units.Where(u => u.OwnerId == Owner && u.Type == UnitType.Villager).ToList();
            var cost = AoE.Core.Economy.BuildingRules.CostOf(BuildingType.Dock);
            if (villagers.Count < DOCK_VILLAGERS || wood < cost[Resource.Wood] + WOOD_RESERVE)
                return;
            // Vom Dorfbewohner aus, der dem Stadtzentrum am nächsten steht - und nur, wer mit ihm
            // zusammenhängend steht, baut (das Stadtzentrum teilt kleine Inseln manchmal in zwei Hälften)
            var tc = map.Buildings.First(b => b.OwnerId == Owner && b.Core.BuildingType == BuildingType.TownCenter);
            var tcCenter = new Vector2((tc.X + tc.Width / 2f) * map.TileSize, (tc.Y + tc.Height / 2f) * map.TileSize);
            var anchor = villagers.OrderBy(v => Vector2.DistanceSquared(v.Position, tcCenter)).First();
            if (FindDockSite(anchor) is not { } site)
                return;
            var reach = Reachable(anchor, DOCK_SEARCH_RADIUS + 4);
            var center = new Vector2((site.X + 1.5f) * map.TileSize, (site.Y + 1.5f) * map.TileSize);
            var builders = villagers.Where(v => reach.Contains(Cell(v)))
                                    .OrderBy(v => Vector2.DistanceSquared(v.Position, center)).Take(2).ToList();
            if (_screen.PlaceBuildingFor(Owner, BuildingType.Dock, new Vector2(site.X, site.Y), builders))
                Log?.Invoke($"Fähre: Hafen bei ({site.X},{site.Y})");
            return;
        }

        var dock = docks.FirstOrDefault(d => d.IsComplete);
        if (dock == null || dock.Training.Count > 0)
            return;
        int transports = _screen.units.Count(u => u.OwnerId == Owner && u.Type == UnitType.TransportShip);
        if (transports < TRANSPORTS)
        {
            if (wood >= 125 + WOOD_RESERVE)
                _screen.TrainSoldierForOwner(Owner, dock, UnitType.TransportShip);
            return;
        }
        int warships = _screen.units.Count(u => u.OwnerId == Owner && u.Type == UnitType.WarShip);
        if (warships < WARSHIPS && player.Ages.Current >= AoE.Core.Economy.Age.Feudal
            && wood >= 90 + WOOD_RESERVE && player.Resources[Resource.Gold] >= 30)
            _screen.TrainSoldierForOwner(Owner, dock, UnitType.WarShip);
    }

    /// <summary>
    /// Der Hafenplatz, dessen Mitte dem eigenen Stadtzentrum am nächsten liegt: höchstens
    /// DOCK_SEARCH_RADIUS Kacheln entfernt, RTSGameplayScreen.CanPlace für den Hafen, und eine
    /// Landkachel am Rand ist von <paramref name="walker"/> aus zu Fuß erreichbar (Wald, Felsen und
    /// das Stadtzentrum selbst schneiden sonst ein Stück Ufer ab, und die Bauleute kämen nicht hin).
    /// null, wenn es keinen gibt.
    /// </summary>
    public Point? FindDockSite(Unit walker)
    {
        var map = _screen.tileMap;
        var tc = map.Buildings.FirstOrDefault(b => b.OwnerId == Owner && b.Core.BuildingType == BuildingType.TownCenter);
        if (tc == null || walker == null) return null;
        int cx = tc.X + tc.Width / 2, cy = tc.Y + tc.Height / 2;
        var reach = Reachable(walker, DOCK_SEARCH_RADIUS + 4);
        int size = AoE.Core.Economy.BuildingRules.SizeOf(BuildingType.Dock);
        Point? best = null;
        int bestDist = int.MaxValue;
        for (int x = cx - DOCK_SEARCH_RADIUS; x <= cx + DOCK_SEARCH_RADIUS; x++)
        {
            for (int y = cy - DOCK_SEARCH_RADIUS; y <= cy + DOCK_SEARCH_RADIUS; y++)
            {
                int dx = x + size / 2 - cx, dy = y + size / 2 - cy;
                int dist = dx * dx + dy * dy;
                if (dist >= bestDist || !_screen.CanPlace(Owner, BuildingType.Dock, new Vector2(x, y)))
                    continue;
                if (!TouchesLand(x, y, size, reach))
                    continue;
                best = new Point(x, y);
                bestDist = dist;
            }
        }
        return best;
    }

    // Grenzt die Grundfläche an eine Kachel aus reach?
    private static bool TouchesLand(int x, int y, int size, HashSet<Point> reach)
    {
        for (int i = 0; i < size; i++)
        {
            foreach (var (rx, ry) in new[] { (x + i, y - 1), (x + i, y + size), (x - 1, y + i), (x + size, y + i) })
                if (reach.Contains(new Point(rx, ry)))
                    return true;
        }
        return false;
    }

    private Point Cell(Unit u)
    {
        var c = _screen.tileMap.WorldToGrid(u.Position);
        return new Point((int)c.X, (int)c.Y);
    }

    /// <summary>Alle begehbaren Kacheln, die <paramref name="walker"/> zu Fuß erreicht (vier
    /// Richtungen, seine eigene Kachel eingeschlossen), höchstens radius Kacheln von ihm.</summary>
    public HashSet<Point> Reachable(Unit walker, int radius)
    {
        var map = _screen.tileMap;
        var start = Cell(walker);
        var seen = new HashSet<Point> { start };
        var queue = new Queue<Point>();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var n = new Point(p.X + dx, p.Y + dy);
                if (Math.Abs(n.X - start.X) > radius || Math.Abs(n.Y - start.Y) > radius) continue;
                if (!map.IsWalkable(n.X, n.Y) || !seen.Add(n)) continue;
                queue.Enqueue(n);
            }
        }
        return seen;
    }
}
