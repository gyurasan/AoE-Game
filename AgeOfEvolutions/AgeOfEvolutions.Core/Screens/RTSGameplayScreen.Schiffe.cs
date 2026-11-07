using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using UnitState = AoE.Core.Entities.UnitState;
using CoreTransportShip = AoE.Core.Entities.TransportShip;

namespace AgeOfEvolutions.Core.Screens;

// Schiffe (Nutzer 2026-11-07: „In der Wiking Version müssen wir dann noch Hafen erstellen können.
// Die Inseln müssen da komplett von Wasser umgeben sein und man wird nur mit booten von einer zur
// nächsten insel kommen.“ und „auch die Schiffe Truppentransport schiffe und Kriegsschiffe
// erstellen“). Der Hafen bildet Transport- und Kriegsschiffe aus; Schiffe fahren nur auf Wasser
// (TileMap.FindShipPath). Landeinheiten steigen per Linksklick auf das eigene Transportschiff ein,
// ein Linksklick des Schiffs auf Land setzt sie an der nächsten erreichbaren Küste ab.
public partial class RTSGameplayScreen
{
    /// <summary>Wie nah (Kacheln, Mitte zu Mitte) eine Einheit dem Schiff kommen muss, um
    /// einzusteigen - eine Kachel Küste plus die Diagonale.</summary>
    private const float BOARD_REACH_TILES = 1.7f;

    /// <summary>Wie nah (Kacheln) das Schiff der Anlegestelle an Land kommen muss, um abzusetzen.</summary>
    private const float UNLOAD_REACH_TILES = 1.7f;

    /// <summary>Meldung, wenn das Transportschiff voll ist.</summary>
    internal const string SHIP_FULL_MESSAGE = "Schiff voll - höchstens 5 Einheiten an Bord";

    /// <summary>Meldung, wenn das Schiff die Küste der Einheiten nicht erreicht.</summary>
    internal const string SHIP_NO_SHORE_MESSAGE = "Das Schiff erreicht diese Küste nicht";

    /// <summary>Meldung, wenn eine Einheit die Anlegestelle zu Fuß nicht erreicht (Wald oder
    /// Gebäude schneiden ihr Stück der Küste ab).</summary>
    internal const string SHIP_NO_WALK_MESSAGE = "Einige Einheiten erreichen die Anlegestelle nicht";

    /// <summary>
    /// Schiffsbilder (Gruppe schiffe in tools/bilder/bilder.json, _blau und _rot) und ihre Länge in
    /// Welteinheiten: ein Dorfbewohner ist 24 hoch, eine Kachel 32 breit - ein Schiff ist etwa zwei
    /// Kacheln lang. Der Bug zeigt im Bild nach rechts; Aspect ist Höhe durch Breite des Bilds
    /// (schräg von vorn gesehen, wie die Belagerungswaffen).
    /// </summary>
    private static readonly (UnitType Unit, string Asset, float Length, float Aspect)[] ShipSprites =
    {
        (UnitType.TransportShip, "Einheiten/transportschiff", 64f, 0.97f),
        (UnitType.WarShip, "Einheiten/kriegsschiff", 72f, 0.79f),
    };
    private readonly Dictionary<(UnitType Unit, int Owner), Texture2D> _shipSprites = new();

    /// <summary>Wie tief der Kiel unter der Lage des Schiffs liegt (Welteinheiten) - die Lage ist
    /// die Wasserlinie in der Mitte des Rumpfs.</summary>
    private const float SHIP_DRAFT = 6f;

    /// <summary>Anteil des Bilds, der über Wasser zu sehen ist - der Kiel taucht ein.</summary>
    private const float SHIP_ABOVE_WATER = 0.9f;

    private static float ShipLength(UnitType type)
    {
        foreach (var (unit, _, length, _) in ShipSprites)
            if (unit == type) return length;
        return 64f;
    }

    private static float ShipAspect(UnitType type)
    {
        foreach (var (unit, _, _, aspect) in ShipSprites)
            if (unit == type) return aspect;
        return 0.8f;
    }

    /// <summary>Lädt die Schiffsbilder je Spielerfarbe; fehlt eines, zeichnet DrawShip einen
    /// einfachen Rumpf mit Segel.</summary>
    private void LoadShipSprites()
    {
        foreach (var (owner, colour) in new[] { (0, "_blau"), (1, "_rot") })
            foreach (var (unit, asset, _, _) in ShipSprites)
                if (LoadOptional(asset + colour) is { } tex)
                    _shipSprites[(unit, owner)] = tex;
    }

    /// <summary>Trefferfläche eines Schiffs in Weltkoordinaten: so lang wie das Schiff, so hoch wie
    /// sein Bild über Wasser, Unterkante am Kiel.</summary>
    private static Rectangle ShipWorldRect(Unit unit)
    {
        float length = ShipLength(unit.Type);
        float height = length * ShipAspect(unit.Type) * SHIP_ABOVE_WATER;
        return new Rectangle((int)(unit.Position.X - length / 2f), (int)(unit.Position.Y + SHIP_DRAFT - height),
                             (int)length, (int)height);
    }

    // --- Fahren -------------------------------------------------------------------------

    /// <summary>
    /// Wie StepAlongPath, nur auf Wasser: geradeaus, solange die Strecke befahrbar ist
    /// (IsSegmentNavigable), sonst von Kachelmitte zu Kachelmitte; liegt die nächste Kachel nicht
    /// mehr frei (ein Hafen wurde hineingebaut), wird der Weg neu geplant. Ein Kriegsschiff plant
    /// auf ein fahrendes Ziel neu, sobald es sich um mehr als eine Kachel bewegt hat.
    /// Gibt true zurück, sobald der Weg abgefahren ist.
    /// </summary>
    private bool StepShip(Unit unit, float dt)
    {
        if (unit.Path.Count == 0)
        {
            unit.Pace = 0f;
            return true;
        }

        if (unit.UnitTarget is { } foe && foe.State != UnitState.Dead && unit.AttackPower > 0
            && Vector2.Distance(foe.Position, unit.TargetPosition) > tileMap.TileSize)
        {
            unit.TargetPosition = foe.Position;
            var replanned = PathToStand(unit, AttackStandPoint(unit, foe));
            if (replanned.Count > 0)
                unit.Path = replanned;
        }

        while (unit.Path.Count >= 2 && tileMap.IsSegmentNavigable(unit.Position, unit.Path[1], 8f))
            unit.Path.RemoveAt(0);

        var direction = unit.Path[0] - unit.Position;
        float distance = direction.Length();
        if (distance < 2f)
        {
            unit.Path.RemoveAt(0);
            return false;
        }
        direction /= distance;
        float remaining = distance;
        for (int i = 1; i < unit.Path.Count; i++)
            remaining += Vector2.Distance(unit.Path[i - 1], unit.Path[i]);
        unit.Pace = Gait.Approach(unit.Pace, Gait.ArrivalSpeed(remaining), dt);

        var before = tileMap.WorldToGrid(unit.Position);
        var next = unit.Position + direction * unit.MovementSpeed * 40 * unit.Pace * dt * _unitSpeed;
        var nextCell = tileMap.WorldToGrid(next);
        if (nextCell != before && !tileMap.IsNavigable((int)nextCell.X, (int)nextCell.Y))
        {
            unit.Path = tileMap.FindShipPath(unit.Position, unit.Path[unit.Path.Count - 1]);
            return unit.Path.Count == 0;
        }
        unit.Position = next;
        return false;
    }

    /// <summary>
    /// Befehl an Schiffe (Linksklick auf <paramref name="gridPos"/>, ohne Gegner darunter).
    /// VERTRAG: je Schiff: Job und BuildSite null; Einheiten, die auf dieses Schiff zuliefen
    /// (BoardShip), bleiben stehen (BoardShip null). Ein Transportschiff mit Passagieren und Land
    /// unter dem Klick fährt an die Anlegestelle (TileMap.Landing zur Landmasse des Klicks), merkt
    /// sie sich in UnloadAt und den Klick in TargetPosition - UpdateShips setzt dort ab. Sonst
    /// fährt das Schiff auf den Klick (bei mehreren Schiffen je eines versetzt), auf Land bis an
    /// die nächste erreichbare Wasserkachel. Ohne Weg bleibt es untätig.
    /// </summary>
    internal void CommandShips(List<Unit> ships, Vector2 gridPos)
    {
        int gx = (int)gridPos.X, gy = (int)gridPos.Y;
        bool land = tileMap.GetTile(gx, gy) is { } tile && tile.Type != TileType.Water;
        int index = 0;
        foreach (var ship in ships)
        {
            ship.Job = null;
            ship.BuildSite = null;
            foreach (var u in units)
                if (u.BoardShip == ship)
                    u.BoardShip = null;

            if (land && ship.Passengers.Count > 0)
            {
                var at = tileMap.WorldToGrid(ship.Position);
                // Am liebsten ein Ufer, von dem aus das Ziel zu Fuß erreichbar ist - wenn dort Platz
                // für alle ist; sonst ein Uferstück der Insel, auf dem alle Platz finden
                var reach = tileMap.IsWalkable(gx, gy) ? tileMap.WalkReach(new Point(gx, gy)) : null;
                if (reach == null || reach.Count < ship.Passengers.Count)
                {
                    var roomy = tileMap.RoomyLand(tileMap.LandRegion(gx, gy), ship.Passengers.Count);
                    reach = roomy.Count > 0 ? roomy : null;
                }
                var spot = tileMap.Landing((int)at.X, (int)at.Y, gx, gy, tileMap.LandRegion(gx, gy), reach)
                           ?? tileMap.Landing((int)at.X, (int)at.Y, gx, gy, tileMap.LandRegion(gx, gy));
                if (spot is { } landing2)
                {
                    ship.UnloadAt = landing2.Land;
                    ship.TargetPosition = GridToWorld(gridPos);
                    ship.Path = tileMap.FindShipPath(ship.Position, GridToWorld(new Vector2(landing2.Water.X, landing2.Water.Y)));
                    ship.State = ship.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
                    index++;
                    continue;
                }
            }

            // Mehrere Schiffe: je eines im Abstand von anderthalb Kacheln um den Klick (goldener Winkel)
            var target = GridToWorld(gridPos);
            if (index > 0)
            {
                float angle = index * 2.39996f;
                target += new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * (1.5f * tileMap.TileSize * MathF.Sqrt(index));
            }
            ship.TargetPosition = target;
            ship.Path = tileMap.FindShipPath(ship.Position, target);
            ship.State = ship.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
            index++;
        }
    }

    // --- Ein- und Aussteigen -------------------------------------------------------------

    /// <summary>Das eigene Transportschiff unter dem Klick auf <paramref name="gridPos"/>: seine
    /// Trefferfläche (ShipWorldRect, um eine Kante größer - Bug, Heck und Segel liegen am Rand
    /// des Bilds) enthält die Mitte der Kachel, oder es liegt auf ihr.</summary>
    private Unit OwnTransportAt(Vector2 gridPos, int ownerId)
    {
        var center = GridToWorld(gridPos);
        var point = new Point((int)center.X, (int)center.Y);
        foreach (var u in units)
        {
            if (u.OwnerId != ownerId || u.Type != UnitType.TransportShip || u.State == UnitState.Dead)
                continue;
            var rect = ShipWorldRect(u);
            rect.Inflate(10, 10);
            if (tileMap.WorldToGrid(u.Position) == gridPos || rect.Contains(point))
                return u;
        }
        return null;
    }

    /// <summary>
    /// Schickt Landeinheiten an Bord von <paramref name="ship"/>.
    /// VERTRAG: jede Einheit bekommt ihre eigene Anlegestelle (TileMap.Landing) an ihrer
    /// eigenen Landmasse, die sie zu Fuß erreicht - Wald oder Gebäude schneiden sonst Stücke ab;
    /// die Mitte der Gruppe zählt als Zielvorzug. Das Schiff fährt an die Stelle, an der die
    /// meisten Einheiten warten (Gleichstand: die näher an der Mitte). Reicht eine Einheit keine
    /// Anlegestelle, bleibt sie so, wie sie ist (Auftrag läuft weiter), und die Leiste meldet
    /// SHIP_NO_WALK_MESSAGE (Spieler 0). Reicht keine einzige, meldet die Leiste
    /// SHIP_NO_SHORE_MESSAGE (Spieler 0) und nichts geschieht. Sonst lässt jede fahrbare Einheit
    /// Auftrag, Baustelle und Ziele fallen, merkt sich das Schiff (BoardShip) und läuft zu ihrer
    /// Landkachel. UpdateShips lässt sie einsteigen.
    /// </summary>
    internal void BoardUnits(List<Unit> walkers, Unit ship)
    {
        walkers = walkers.Where(w => !w.IsShip && w.State != UnitState.Dead).ToList();
        if (walkers.Count == 0)
            return;
        var mid = walkers.Aggregate(Vector2.Zero, (s, w) => s + w.Position) / walkers.Count;
        var midCell = tileMap.WorldToGrid(mid);
        var at = tileMap.WorldToGrid(ship.Position);

        // Je Einheit ihre eigene Anlegestelle - nur ein Ufer, das sie zu Fuß erreicht
        var boarders = new List<Unit>();
        var spots = new Dictionary<Unit, (Point Water, Point Land)>();
        foreach (var w in walkers)
        {
            var own = tileMap.WorldToGrid(w.Position);
            var onFoot = tileMap.WalkReach(new Point((int)own.X, (int)own.Y));
            var spot = tileMap.Landing((int)at.X, (int)at.Y, (int)midCell.X, (int)midCell.Y,
                                       tileMap.LandRegionAround((int)own.X, (int)own.Y), onFoot);
            if (spot is { } s)
            {
                boarders.Add(w);
                spots[w] = s;
            }
        }
        if (boarders.Count == 0)
        {
            if (ship.OwnerId == 0)
                ShowHudMessage(SHIP_NO_SHORE_MESSAGE);
            return;
        }
        if (boarders.Count < walkers.Count && ship.OwnerId == 0)
            ShowHudMessage(SHIP_NO_WALK_MESSAGE);

        // Das Schiff fährt an die Stelle, an der die meisten Einheiten warten
        (Point Water, Point Land) gewaehlt = spots[boarders[0]];
        int beste = 0;
        float bestD = Vector2.Distance(GridToWorld(new Vector2(gewaehlt.Land.X, gewaehlt.Land.Y)), mid);
        foreach (var g in spots.Values.GroupBy(v => v))
        {
            int count = g.Count();
            float d = Vector2.Distance(GridToWorld(new Vector2(g.First().Land.X, g.First().Land.Y)), mid);
            if (count > beste || (count == beste && d < bestD))
            {
                beste = count;
                gewaehlt = g.First();
                bestD = d;
            }
        }

        var landPos = GridToWorld(new Vector2(gewaehlt.Land.X, gewaehlt.Land.Y));
        float reach = BOARD_REACH_TILES * tileMap.TileSize;
        if (Vector2.Distance(ship.Position, landPos) > reach)
        {
            var water = GridToWorld(new Vector2(gewaehlt.Water.X, gewaehlt.Water.Y));
            ship.UnitTarget = null;
            ship.UnloadAt = null;
            ship.TargetPosition = water;
            ship.Path = tileMap.FindShipPath(ship.Position, water);
            if (ship.Path.Count > 0)
                ship.State = UnitState.Moving;
        }

        foreach (var w in boarders)
        {
            var ownLand = GridToWorld(new Vector2(spots[w].Land.X, spots[w].Land.Y));
            w.Job = null;
            w.BuildSite = null;
            w.AttackTarget = null;
            w.UnitTarget = null;
            w.PreyTarget = null;
            w.BoardShip = ship;
            w.TargetPosition = ownLand;
            w.Path = tileMap.FindPath(w.Position, ownLand);
            w.State = w.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
        }
    }

    /// <summary>
    /// Ein- und Aussteigen, einmal je Update nach den Einheiten.
    /// VERTRAG:
    /// - Einheiten mit BoardShip: ist das Schiff nicht mehr da, BoardShip null. Steht die Einheit
    ///   höchstens BOARD_REACH_TILES Kacheln vom Schiff entfernt, steigt sie ein (Board), solange
    ///   weniger als TransportShip.CAPACITY an Bord sind - sonst bleibt sie stehen (BoardShip
    ///   null) und die Leiste meldet SHIP_FULL_MESSAGE (Spieler 0). Warten beide untätig - die
    ///   Einheit am Treffpunkt (TargetPosition, höchstens eine Kachel), das Schiff ohne UnloadAt -,
    ///   reicht BOARD_REACH_TILES + 1 (das Schiff wird beim Anlegen leicht abgedrängt). Steht sie
    ///   untätig fern von ihrem Treffpunkt, gibt sie auf (BoardShip null).
    /// - Passagiere fahren mit (Position = Lage des Schiffs).
    /// - Ein Schiff mit UnloadAt und Passagieren, das höchstens UNLOAD_REACH_TILES Kacheln von
    ///   der Landkachel entfernt ist, setzt ab (Unload). Ist es untätig und weiter weg, gibt es
    ///   das Absetzen auf (UnloadAt null).
    /// </summary>
    private void UpdateShips()
    {
        float ts = tileMap.TileSize;
        List<(Unit Walker, Unit Ship)> boarding = null;
        List<Unit> unloading = null;
        foreach (var u in units)
        {
            if (u.BoardShip is { } ship)
            {
                float distance = Vector2.Distance(u.Position, ship.Position);
                bool atMeeting = u.State == UnitState.Idle && Vector2.Distance(u.Position, u.TargetPosition) <= ts;
                if (ship.State == UnitState.Dead || !units.Contains(ship))
                    u.BoardShip = null;
                else if (distance <= BOARD_REACH_TILES * ts
                         || (atMeeting && ship.State == UnitState.Idle && ship.UnloadAt == null
                             && distance <= (BOARD_REACH_TILES + 1f) * ts))
                    (boarding ??= new()).Add((u, ship));
                else if (u.State == UnitState.Idle && !atMeeting)
                {
                    u.BoardShip = null;
                    if (u.OwnerId == 0)
                        ShowHudMessage(SHIP_NO_WALK_MESSAGE);
                }
            }
            if (!u.IsShip)
                continue;
            foreach (var p in u.Passengers)
                p.Position = u.Position;
            if (u.UnloadAt is { } landing)
            {
                if (u.Passengers.Count == 0)
                    u.UnloadAt = null;
                else if (Vector2.Distance(u.Position, GridToWorld(new Vector2(landing.X, landing.Y))) <= UNLOAD_REACH_TILES * ts)
                    (unloading ??= new()).Add(u);
                else if (u.State == UnitState.Idle)
                    u.UnloadAt = null;
            }
        }

        if (boarding != null)
        {
            bool full = false;
            foreach (var (walker, ship) in boarding)
            {
                if (ship.Passengers.Count >= CoreTransportShip.CAPACITY)
                {
                    walker.BoardShip = null;
                    full |= walker.OwnerId == 0;
                    continue;
                }
                Board(walker, ship);
            }
            if (full)
                ShowHudMessage(SHIP_FULL_MESSAGE);
        }
        if (unloading != null)
            foreach (var ship in unloading)
                Unload(ship);
    }

    /// <summary>
    /// <paramref name="walker"/> steigt in <paramref name="ship"/> ein: aus units (und der Auswahl)
    /// heraus, in ship.Passengers hinein; Weg, Auftrag und Ziele sind leer, der Zustand Idle.
    /// Wer die Einheit angriff, lässt von ihr ab.
    /// </summary>
    private void Board(Unit walker, Unit ship)
    {
        walker.BoardShip = null;
        walker.Job = null;
        walker.BuildSite = null;
        walker.AttackTarget = null;
        walker.UnitTarget = null;
        walker.PreyTarget = null;
        walker.Path.Clear();
        walker.State = UnitState.Idle;
        walker.Position = ship.Position;
        walker.IsSelected = false;
        selectedUnits.Remove(walker);
        units.Remove(walker);
        _unitMotion.Remove(walker);
        ship.Passengers.Add(walker);
        foreach (var u in units)
        {
            if (u.UnitTarget != walker)
                continue;
            u.UnitTarget = null;
            u.Path.Clear();
            u.State = UnitState.Idle;
        }
    }

    /// <summary>
    /// Setzt die Passagiere von <paramref name="ship"/> an UnloadAt ab: je eine freie Landkachel
    /// (TileMap.ShoreCells) ab der Anlegestelle, Mitte der Kachel, untätig, wieder in units. Wer
    /// keinen Platz findet, bleibt an Bord. Danach laufen die Abgesetzten zum angeklickten Punkt
    /// (TargetPosition des Schiffs), wenn er auf derselben Landmasse begehbar ist und nicht schon
    /// die Anlegestelle selbst. UnloadAt ist danach null.
    /// </summary>
    internal void Unload(Unit ship)
    {
        if (ship.UnloadAt is not { } landing)
            return;
        ship.UnloadAt = null;
        var cells = tileMap.ShoreCells(landing, ship.Passengers.Count);
        var landed = new List<Unit>();
        for (int i = 0; i < cells.Count; i++)
        {
            var p = ship.Passengers[i];
            p.Position = GridToWorld(new Vector2(cells[i].X, cells[i].Y));
            p.TargetPosition = p.Position;
            p.Path.Clear();
            p.State = UnitState.Idle;
            units.Add(p);
            landed.Add(p);
        }
        ship.Passengers.RemoveRange(0, cells.Count);
        if (landed.Count == 0)
            return;

        var goal = tileMap.WorldToGrid(ship.TargetPosition);
        if (goal != new Vector2(landing.X, landing.Y)
            && tileMap.IsWalkable((int)goal.X, (int)goal.Y)
            && tileMap.SameLand((int)goal.X, (int)goal.Y, landing.X, landing.Y))
            IssueCommand(ship.OwnerId, landed, goal);
    }

    /// <summary>
    /// Ein Schiff sinkt (DestroyUnit): alle Passagiere ertrinken - aus der Einheitenliste ihres
    /// Spielers (Bevölkerung), Zustand Dead. Einheiten, die auf das Schiff zuliefen, bleiben stehen.
    /// </summary>
    private void SinkPassengers(Unit ship, Data.Player owner)
    {
        foreach (var u in units)
            if (u.BoardShip == ship)
                u.BoardShip = null;
        if (ship.Passengers.Count == 0)
            return;
        int drowned = ship.Passengers.Count;
        foreach (var p in ship.Passengers)
        {
            p.State = UnitState.Dead;
            owner.RemoveUnit(p);
        }
        ship.Passengers.Clear();
        if (ship.OwnerId == 0)
            ShowHudMessage($"{CoreUnits.GermanName(ship.Type)} versenkt - {drowned} Einheiten ertrunken");
    }

    /// <summary>
    /// Freie Wasserkachel für ein neues Schiff am Hafen: Ring um Ring um die Grundfläche,
    /// höchstens 4 Kacheln weit, befahrbar, mit offenem Wasser dahinter (TileMap.OpenWater - der
    /// Hafen selbst schneidet sonst eine Bucht ab) und ohne Schiff darauf. null, wenn keine frei ist.
    /// </summary>
    private Vector2? ShipSpawnCell(Building dock)
    {
        for (int r = 1; r <= 4; r++)
        {
            int left = dock.X - r, right = dock.X + dock.Width - 1 + r;
            int top = dock.Y - r, bottom = dock.Y + dock.Height - 1 + r;
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    if (x != left && x != right && y != top && y != bottom)
                        continue;
                    var cell = new Vector2(x, y);
                    if (tileMap.IsNavigable(x, y) && tileMap.OpenWater(x, y)
                        && !units.Any(u => u.IsShip && WorldToGrid(u.Position) == cell))
                        return cell;
                }
            }
        }
        return null;
    }

    /// <summary>Ob der Hafen auf <paramref name="cell"/> Platz hat (TileMap.CanPlaceDock) und kein
    /// Schiff auf der Grundfläche liegt.</summary>
    private bool DockFits(Vector2 cell, int size)
    {
        int x = (int)cell.X, y = (int)cell.Y;
        if (!tileMap.CanPlaceDock(x, y, size))
            return false;
        return !units.Any(u =>
        {
            if (!u.IsShip) return false;
            var c = WorldToGrid(u.Position);
            return c.X >= x && c.X < x + size && c.Y >= y && c.Y < y + size;
        });
    }

    // --- Zeichnen -------------------------------------------------------------------------

    /// <summary>
    /// Ein Schiff in der Tiefenschicht: das Bild so lang wie ShipLength, der Kiel unter der
    /// Wasserlinie abgeschnitten, schaukelnd; nach links gespiegelt, wenn es nach links fährt.
    /// Ohne Bild ein brauner Rumpf mit hellem Segel in der Spielerfarbe.
    /// </summary>
    private void DrawShip(SpriteBatch spriteBatch, Unit unit, Vector2 screenPos, bool hover)
    {
        float phase = (unit.Id.GetHashCode() & 255) * 0.05f;
        float bob = MathF.Sin(animationTime * 1.7f + phase);
        float tilt = MathF.Sin(animationTime * 1.3f + phase * 1.7f) * 0.03f;
        bool left = _unitMotion.TryGetValue(unit, out var motion) && motion.FacingLeft;
        float length = ShipLength(unit.Type);
        var keel = screenPos + new Vector2(0f, (SHIP_DRAFT + bob * 1.2f) * cameraZoom);

        // Bugwelle: zwei helle Striche am Rumpf, solange es fährt
        if (unit.Path.Count > 0)
        {
            int foam = Math.Max(1, (int)(2 * cameraZoom));
            float half = length * 0.5f * cameraZoom;
            spriteBatch.Draw(px, new Rectangle((int)(keel.X - half), (int)keel.Y - foam, (int)(half * 2), foam), Color.White * 0.35f);
        }

        if (_shipSprites.TryGetValue((unit.Type, unit.OwnerId), out var tex))
        {
            int visible = (int)(tex.Height * SHIP_ABOVE_WATER);
            var source = new Rectangle(0, 0, tex.Width, visible);
            var origin = new Vector2(tex.Width / 2f, visible);
            float scale = length * cameraZoom / tex.Width;
            var flip = left ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            spriteBatch.Draw(tex, keel, source, Color.White, tilt, origin, scale, flip, 0f);
            if (hover)
                spriteBatch.Draw(tex, keel, source, HoverLift, tilt, origin, scale, flip, 0f);
            return;
        }

        // Ersatz ohne Bild: Rumpf, Mast, Segel
        float w = length * cameraZoom, h = length * 0.18f * cameraZoom;
        var hull = new Rectangle((int)(keel.X - w / 2), (int)(keel.Y - h), (int)w, (int)h);
        spriteBatch.Draw(px, hull, hover ? new Color(150, 100, 60) : new Color(110, 70, 40));
        var sailColor = unit.OwnerId == 0 ? new Color(90, 130, 220) : new Color(210, 80, 70);
        float sh = length * 0.45f * cameraZoom, sw = length * 0.4f * cameraZoom;
        spriteBatch.Draw(px, new Rectangle((int)(keel.X - 1), (int)(hull.Y - sh - 4 * cameraZoom), Math.Max(1, (int)(2 * cameraZoom)), (int)(sh + 4 * cameraZoom)), new Color(70, 45, 25));
        spriteBatch.Draw(px, new Rectangle((int)(keel.X - sw / 2), (int)(hull.Y - sh), (int)sw, (int)(sh * 0.8f)), sailColor);
    }
}
