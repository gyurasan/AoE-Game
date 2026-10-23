using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Ein Schlag eines Raubtiers gegen eine Einheit (RT) - der Spielbildschirm spielt dazu den Ton,
/// warnt den Spieler und lässt Soldaten sich wehren. X, Y: Kachel des Raubtiers; Damage: die
/// abgezogenen Lebenspunkte; Killed: die Einheit hat danach 0 Lebenspunkte (State Dead).
/// </summary>
public readonly record struct PredatorStrike(FoodSource Kind, int X, int Y, Unit Victim, int Damage, bool Killed);

// -----------------------------------------------------------------
// Raubtiere: Bären und Wölfe (RT)
// -----------------------------------------------------------------
// Wunsch des Nutzers (2026-10-23): „Bären und Wölfe in hoher qualität. Kommen Dorfbewohner oder
// Soldaten den Tieren zu nahe werden sie von ihnen angegriffen. Wie Wildschweine (nur seltener)
// laufen sie herum aber von Gebäuden halten sie Abstand (mindestens 4-5 Kacheln)" und „Sie
// verfolgen aber dann keine Menschen wenn man davon läuft nur wenn man zu nahe zu Ihnen hinläuft".
// Raubtiere stehen wie das übrige Wild auf einer Kachel (Food Bear/Wolf, WildAnimal mit
// Lebenspunkten). Lebend trägt die Kachel keine Nahrung; ein erlegter Bär liegt als Fleisch da
// (Slaughtered, BearFood Nahrung), ein erlegter Wolf verschwindet.
public partial class TileMap
{
    /// <summary>Sekunden, die ein Raubtier nach jedem Schlag zuschlägt und zurücknimmt (WildAnimal.Strike).</summary>
    public const float PREDATOR_STRIKE_SECONDS = 0.45f;

    /// <summary>
    /// Wie weit (Kacheln, Chebyshev) ein Raubtier, das zu nah an einem Gebäude steht, nach einem
    /// Ausweg mit genug Abstand sucht - um Wald und Felsen herum (PredatorStep).
    /// </summary>
    public const int PREDATOR_ESCAPE_TILES = 12;

    private readonly List<(int x, int y)> _bearCoords = new List<(int, int)>();
    private readonly List<(int x, int y)> _wolfCoords = new List<(int, int)>();

    /// <summary>Anzahl der Bären und der Wölfe auf dieser Karte (auch erlegte Bären, solange ihr Fleisch liegt).</summary>
    public int BearCount => _bearCoords.Count;
    public int WolfCount => _wolfCoords.Count;

    /// <summary>Die Kachelliste einer Raubtierart: Bear die Bären, Wolf die Wölfe, sonst null.</summary>
    private List<(int x, int y)> PredatorCoords(FoodSource kind) => kind switch
    {
        FoodSource.Bear => _bearCoords,
        FoodSource.Wolf => _wolfCoords,
        _ => null,
    };

    /// <summary>
    /// Volle Lebenspunkte einer Raubtierart.
    /// VERTRAG: Bear → Settings.BearHealth, Wolf → Settings.WolfHealth, jede andere Art → 0.
    /// </summary>
    public int PredatorMaxHealth(FoodSource kind)
    {
        // Bär und Wolf haben je eigene volle Lebenspunkte, jede andere Art zählt nicht.
        return kind switch
        {
            FoodSource.Bear => Settings.BearHealth,
            FoodSource.Wolf => Settings.WolfHealth,
            _ => 0,
        };
    }

    /// <summary>
    /// Steht auf (x, y) ein lebendes Raubtier?
    /// VERTRAG: true genau dann, wenn GetTile(x, y) nicht null ist, ihr Food.IsPredator() gilt,
    /// ihr Animal nicht null ist, Animal.Slaughtered false ist und Animal.Health größer 0.
    /// </summary>
    public bool IsLivePredator(int x, int y)
    {
        // Lebend heißt: Kachel vorhanden, Raubtier, Tier nicht null, nicht erlegt und Lebenspunkte da.
        Tile t = GetTile(x, y);
        return t is not null
            && t.Food.IsPredator()
            && t.Animal is { Slaughtered: false } tier
            && tier.Health > 0;
    }

    /// <summary>
    /// Abstand der Kachel (x, y) zum nächsten Gebäude in Kacheln - Chebyshev bis zur nächsten
    /// Gebäudekachel, 0 auf einem Gebäude. Zählt jedes Gebäude in Buildings, beider Spieler, auch
    /// Baustellen.
    /// VERTRAG: für jedes b in Buildings: dx = Math.Max(0, Math.Max(b.X - x, x - (b.X + b.Width - 1))),
    /// dy = Math.Max(0, Math.Max(b.Y - y, y - (b.Y + b.Height - 1))), d = Math.Max(dx, dy);
    /// Rückgabe das kleinste d, ohne Gebäude int.MaxValue.
    /// </summary>
    public int BuildingDistance(int x, int y)
    {
        // Chebyshev-Abstand zur nächsten Gebäudekachel; auf einem Gebäude selbst 0.
        int best = int.MaxValue;
        foreach (Building b in Buildings)
        {
            int dx = Math.Max(0, Math.Max(b.X - x, x - (b.X + b.Width - 1)));
            int dy = Math.Max(0, Math.Max(b.Y - y, y - (b.Y + b.Height - 1)));
            best = Math.Min(best, Math.Max(dx, dy));
        }
        return best;
    }

    /// <summary>
    /// Stellt ein lebendes Raubtier der Art kind auf die Kachel (x, y) - beim Erzeugen der Karte
    /// (PlacePredators) und für Tests.
    /// VERTRAG: false und nichts ändern, wenn kind.IsPredator() nicht gilt, GetTile(x, y) null ist,
    /// ihr Type nicht TileType.Grassland ist, sie ein Gebäude trägt (!string.IsNullOrEmpty(Building)),
    /// Farm true ist, Food nicht FoodSource.None ist oder ResourceType nicht null ist. Sonst:
    /// Food = kind, ResourceType = null, ResourceAmount = 0, Animal = NewAnimal(max) mit
    /// max = Settings.BearWanderSecondsMax (Bär) bzw. Settings.WolfWanderSecondsMax (Wolf), danach
    /// Animal.Health = PredatorMaxHealth(kind); (x, y) an PredatorCoords(kind) anhängen; true.
    /// </summary>
    public bool PlacePredator(FoodSource kind, int x, int y)
    {
        // Nur auf freier Graslandkachel ohne Gebäude, Farm, Rohstoff und anderem Wild.
        if (!kind.IsPredator())
            return false;
        Tile t = GetTile(x, y);
        if (t is null
            || t.Type != TileType.Grassland
            || !string.IsNullOrEmpty(t.Building)
            || t.Farm
            || t.Food != FoodSource.None
            || t.ResourceType is not null)
            return false;

        // Wanderzeit je nach Art, danach volle Lebenspunkte.
        float max = kind == FoodSource.Bear ? Settings.BearWanderSecondsMax : Settings.WolfWanderSecondsMax;
        t.Food = kind;
        t.ResourceType = null;
        t.ResourceAmount = 0;
        t.Animal = NewAnimal(max);
        t.Animal.Health = PredatorMaxHealth(kind);
        PredatorCoords(kind).Add((x, y));
        return true;
    }

    /// <summary>
    /// Bären und Wolfsrudel beim Erzeugen der Karte; InitializeRandomMap ruft es zuletzt auf, nach
    /// PlaceStartingPositions - dann stehen die Stadtzentren schon, und BuildingDistance hält die
    /// Raubtiere auch von ihnen fern. Sie stehen weit weg von beiden Startplätzen und jedem Gebäude.
    /// VERTRAG: erst die Bären, dann die Wölfe. Bären: groups = _random.Next(Settings.BearGroupsMin,
    /// Settings.BearGroupsMax + 1), je Gruppe count = _random.Next(Settings.BearsPerGroupMin,
    /// Settings.BearsPerGroupMax + 1). Wölfe ebenso mit WolfPacksMin/Max und WolvesPerPackMin/Max.
    /// Je Gruppe bis zu 40 Versuche: cx = _random.Next(2, Width - 2), cy = _random.Next(2, Height - 2);
    /// verwerfen (nächster Versuch), wenn TooCloseToStart(cx, cy, Settings.PredatorStartDistance),
    /// wenn GetTile(cx, cy)?.Type nicht TileType.Grassland ist oder wenn BuildingDistance(cx, cy)
    /// kleiner als Settings.PredatorBuildingDistance ist. Sonst count-mal: sx = cx + _random.Next(-2, 3),
    /// sy = cy + _random.Next(-2, 3); nur wenn BuildingDistance(sx, sy) mindestens
    /// Settings.PredatorBuildingDistance ist, PlacePredator(art, sx, sy). Hat mindestens ein
    /// PlacePredator der Gruppe true geliefert, ist die Gruppe fertig (keine weiteren Versuche).
    /// </summary>
    private void PlacePredators()
    {
        // Erst die Bären, dann die Wölfe; je Gruppe ein Zentrum und ein Rudel drumherum.
        PlacePredatorGroup(FoodSource.Bear,
            _random.Next(Settings.BearGroupsMin, Settings.BearGroupsMax + 1),
            () => _random.Next(Settings.BearsPerGroupMin, Settings.BearsPerGroupMax + 1));
        PlacePredatorGroup(FoodSource.Wolf,
            _random.Next(Settings.WolfPacksMin, Settings.WolfPacksMax + 1),
            () => _random.Next(Settings.WolvesPerPackMin, Settings.WolvesPerPackMax + 1));

        // Eine Gruppe: bis zu 40 Zentren suchen, dann das Rudel drumherum aufstellen.
        void PlacePredatorGroup(FoodSource kind, int groups, Func<int> count)
        {
            for (int g = 0; g < groups; g++)
            {
                bool placed = false;
                for (int attempt = 0; attempt < 40 && !placed; attempt++)
                {
                    int cx = _random.Next(2, Width - 2);
                    int cy = _random.Next(2, Height - 2);
                    if (TooCloseToStart(cx, cy, Settings.PredatorStartDistance))
                        continue;
                    if (GetTile(cx, cy)?.Type != TileType.Grassland)
                        continue;
                    if (BuildingDistance(cx, cy) < Settings.PredatorBuildingDistance)
                        continue;

                    // Das Rudel: um das Zentrum herum, aber immer mit genug Abstand zu Gebäuden.
                    int n = count();
                    for (int i = 0; i < n; i++)
                    {
                        int sx = cx + _random.Next(-2, 3);
                        int sy = cy + _random.Next(-2, 3);
                        if (BuildingDistance(sx, sy) < Settings.PredatorBuildingDistance)
                            continue;
                        if (PlacePredator(kind, sx, sy))
                            placed = true;
                    }
                }
            }
        }
    }

    /// <summary>
    /// Taktet alle Raubtiere: sie schlagen zu, wenn ihnen eine Einheit zu nahe ist, und wandern
    /// sonst - seltener als die Wildschweine und nie näher als Settings.PredatorBuildingDistance
    /// an ein Gebäude heran; wer zu nah steht, zieht sich zurück. Sie verfolgen niemanden.
    /// Aufruf: jedes Bild aus dem Spielbildschirm (UpdateUnits) mit allen Einheiten.
    /// VERTRAG: strikes = neue Liste. Für kind in der Reihenfolge Bear, Wolf: coords =
    /// PredatorCoords(kind); coords.RemoveAll(c => GetTile(c.x, c.y)?.Food != kind). Dann für
    /// i = 0 bis coords.Count - 1: (sx, sy) = coords[i], t = GetTile(sx, sy), tier = t.Animal;
    /// überspringen, wenn tier null ist, Slaughtered gilt oder Health kleiner oder gleich 0 ist.
    /// 1. tier.Glide, tier.Strike und tier.AttackCooldown je um dt senken, nicht unter 0.
    /// 2. Opfer suchen, nur wenn tier.Glide gleich 0 ist (mitten im Schritt schlägt es nicht):
    ///    center = GridToWorld(new Vector2(sx, sy)), reach = Settings.PredatorReachTiles * TileSize;
    ///    das Opfer ist unter allen u aus units mit u.State != UnitState.Dead und u.Health größer 0
    ///    das mit dem kleinsten Vector2.Distance(u.Position, center), sofern dieser Abstand kleiner
    ///    oder gleich reach ist (bei Gleichstand das erste in units).
    ///    tier.Engaged = (es gibt ein Opfer).
    /// 3. Mit Opfer: ist victim.Position.X != center.X, dann tier.FacingLeft = victim.Position.X
    ///    kleiner center.X. Ist tier.AttackCooldown kleiner oder gleich 0: damage = Math.Max(1,
    ///    (Bear: Settings.BearDamage, Wolf: Settings.WolfDamage) - victim.Armor); victim.Health =
    ///    Math.Max(0, victim.Health - damage); ist sie 0, victim.State = UnitState.Dead;
    ///    tier.AttackCooldown = Settings.BearReloadSeconds bzw. Settings.WolfReloadSeconds;
    ///    tier.Strike = PREDATOR_STRIKE_SECONDS; strikes.Add(new PredatorStrike(kind, sx, sy, victim,
    ///    damage, victim.Health == 0)). Mit Opfer wandert das Tier nicht (WanderTimer bleibt) -
    ///    weiter mit dem nächsten Tier.
    /// 4. Ohne Opfer: tier.WanderTimer -= dt; near = BuildingDistance(sx, sy) kleiner
    ///    Settings.PredatorBuildingDistance. Ist (tier.WanderTimer kleiner oder gleich 0 oder near)
    ///    und tier.Glide kleiner oder gleich 0: tier.WanderTimer = _RandomRange(_wildRng, min, max)
    ///    mit Settings.BearWanderSecondsMin/Max bzw. WolfWanderSecondsMin/Max; liefert
    ///    PredatorStep(t, kind) eine Kachel, wird sie coords[i].
    /// Rückgabe strikes.
    /// </summary>
    public List<PredatorStrike> UpdatePredators(float dt, IReadOnlyList<Unit> units)
    {
        List<PredatorStrike> strikes = new List<PredatorStrike>();
        foreach (FoodSource kind in new[] { FoodSource.Bear, FoodSource.Wolf })
        {
            List<(int x, int y)> coords = PredatorCoords(kind);
            // Kacheln, auf denen das Tier nicht mehr steht (erlegt, umgezogen), fallen raus.
            coords.RemoveAll(c => GetTile(c.x, c.y)?.Food != kind);

            for (int i = 0; i < coords.Count; i++)
            {
                (int sx, int sy) = coords[i];
                Tile t = GetTile(sx, sy);
                WildAnimal tier = t.Animal;
                if (tier is null || tier.Slaughtered || tier.Health <= 0)
                    continue;

                // 1. Animationen und Abkühlungen ablaufen lassen.
                tier.Glide = Math.Max(0, tier.Glide - dt);
                tier.Strike = Math.Max(0, tier.Strike - dt);
                tier.AttackCooldown = Math.Max(0, tier.AttackCooldown - dt);

                // 2. Opfer suchen - nur mitten im Schritt (Glide 0) schlägt es zu.
                Unit victim = null;
                Vector2 center = GridToWorld(new Vector2(sx, sy));
                if (tier.Glide == 0)
                {
                    float reach = Settings.PredatorReachTiles * TileSize;
                    float best = float.MaxValue;
                    foreach (Unit u in units)
                    {
                        if (u.State == UnitState.Dead || u.Health <= 0)
                            continue;
                        float d = Vector2.Distance(u.Position, center);
                        if (d <= reach && d < best)
                        {
                            best = d;
                            victim = u;
                        }
                    }
                }

                // Ausholen zeigt das Bild nur, solange jemand in Reichweite steht.
                tier.Engaged = victim is not null;

                // 3. Mit Opfer: zuschlagen, aber niemandem folgen.
                if (victim is not null)
                {
                    if (victim.Position.X != center.X)
                        tier.FacingLeft = victim.Position.X < center.X;
                    if (tier.AttackCooldown <= 0)
                    {
                        int raw = kind == FoodSource.Bear ? Settings.BearDamage : Settings.WolfDamage;
                        int damage = Math.Max(1, raw - victim.Armor);
                        victim.Health = Math.Max(0, victim.Health - damage);
                        if (victim.Health == 0)
                            victim.State = UnitState.Dead;
                        tier.AttackCooldown = kind == FoodSource.Bear ? Settings.BearReloadSeconds : Settings.WolfReloadSeconds;
                        tier.Strike = PREDATOR_STRIKE_SECONDS;
                        strikes.Add(new PredatorStrike(kind, sx, sy, victim, damage, victim.Health == 0));
                    }
                    // Mit Opfer wandert es nicht - WanderTimer bleibt stehen.
                    continue;
                }

                // 4. Ohne Opfer: im eigenen Takt wandern; zu nah an einem Gebäude zieht es sich zurück.
                tier.WanderTimer -= dt;
                bool near = BuildingDistance(sx, sy) < Settings.PredatorBuildingDistance;
                if ((tier.WanderTimer <= 0 || near) && tier.Glide <= 0)
                {
                    float min = kind == FoodSource.Bear ? Settings.BearWanderSecondsMin : Settings.WolfWanderSecondsMin;
                    float max = kind == FoodSource.Bear ? Settings.BearWanderSecondsMax : Settings.WolfWanderSecondsMax;
                    tier.WanderTimer = _RandomRange(_wildRng, min, max);
                    if (PredatorStep(t, kind) is { } next)
                        coords[i] = next;
                }
            }
        }
        return strikes;
    }

    /// <summary>
    /// Ein Schritt eines Raubtiers auf eine Nachbarkachel. Gibt die neue Kachel zurück, oder null.
    /// Zulässig ist eine Nachbarkachel (dx, dy je -1 bis 1, nicht beide 0), wenn GetTile sie
    /// liefert, ihr Type TileType.Grassland ist, kein Gebäude (string.IsNullOrEmpty(Building)),
    /// Farm false, kein Rohstoff (nicht: ResourceType nicht null und ResourceAmount größer 0) und
    /// Food gleich FoodSource.None.
    /// VERTRAG: own = BuildingDistance(t.X, t.Y), limit = Settings.PredatorBuildingDistance.
    /// a) own kleiner limit (zu nah an einem Gebäude): der erste Schritt auf dem kürzesten Weg
    ///    hinaus - Breitensuche ab (t.X, t.Y) über zulässige Kacheln (Nachbarn in der Reihenfolge
    ///    dx außen, dy innen, je von -1 bis 1), nur Kacheln mit Chebyshev-Abstand höchstens
    ///    PREDATOR_ESCAPE_TILES zu (t.X, t.Y); den Vorgänger jeder erreichten Kachel in einem
    ///    Dictionary&lt;(int, int), (int, int)&gt; merken (kein Sentinel-Wert). Die zuerst erreichte
    ///    Kachel mit BuildingDistance mindestens limit ist das Ziel; von dort über die Vorgänger
    ///    zurück bis zur Kachel, deren Vorgänger (t.X, t.Y) ist - dorthin geht der Schritt (dx, dy).
    ///    Kein Ziel erreichbar: null.
    /// b) sonst wie WanderStep mit radius 1: bis zu 12 Versuche dx = _wildRng.Next(-1, 2),
    ///    dy = _wildRng.Next(-1, 2); dx und dy beide 0 → nächster Versuch; dx == 0 und Versuch
    ///    kleiner 8 → nächster Versuch; unzulässige Kachel oder BuildingDistance(nx, ny) kleiner
    ///    limit → nächster Versuch; sonst dorthin. Nach 12 Versuchen null.
    /// Dorthin ziehen: tier = t.Animal; tier.FromX = -dx, tier.FromY = -dy, tier.Glide =
    /// WILD_STEP_SECONDS, bei dx != 0 tier.FacingLeft = dx kleiner 0; neue Kachel: Food = kind,
    /// ResourceType = null, ResourceAmount = 0, Animal = tier; alte Kachel t: Food = FoodSource.None,
    /// ResourceType = null, ResourceAmount = 0, Animal = null; Rückgabe (nx, ny).
    /// </summary>
    private (int x, int y)? PredatorStep(Tile t, FoodSource kind)
    {
        // Zulässige Kachel: Grasland, frei von Gebäude, Farm, Rohstoff und anderem Wild.
        bool Free(Tile n) => n is not null
            && n.Type == TileType.Grassland
            && string.IsNullOrEmpty(n.Building)
            && !n.Farm
            && !(n.ResourceType is not null && n.ResourceAmount > 0)
            && n.Food == FoodSource.None;

        int own = BuildingDistance(t.X, t.Y);
        int limit = Settings.PredatorBuildingDistance;
        int dx, dy;

        if (own < limit)
        {
            // a) Zu nah an einem Gebäude: über die Breitensuche den ersten Schritt hinaus finden.
            Queue<(int x, int y)> queue = new Queue<(int, int)>();
            Dictionary<(int, int), (int, int)> cameFrom = new Dictionary<(int, int), (int, int)>();
            queue.Enqueue((t.X, t.Y));
            (int, int)? target = null;
            while (queue.Count > 0 && target is null)
            {
                (int qx, int qy) = queue.Dequeue();
                for (int ddx = -1; ddx <= 1; ddx++)
                {
                    for (int ddy = -1; ddy <= 1; ddy++)
                    {
                        if (ddx == 0 && ddy == 0)
                            continue;
                        int px = qx + ddx;
                        int py = qy + ddy;
                        if (Math.Max(Math.Abs(px - t.X), Math.Abs(py - t.Y)) > PREDATOR_ESCAPE_TILES)
                            continue;
                        if (cameFrom.ContainsKey((px, py)))
                            continue;
                        if (!Free(GetTile(px, py)))
                            continue;
                        cameFrom[(px, py)] = (qx, qy);
                        if (BuildingDistance(px, py) >= limit)
                        {
                            target = (px, py);
                            break;
                        }
                        queue.Enqueue((px, py));
                    }
                    if (target is not null)
                        break;
                }
            }
            if (target is null)
                return null;

            // Von der Zielkachel über die Vorgänger zurück bis direkt hinter (t.X, t.Y).
            (int cx, int cy) = target.Value;
            while (cameFrom[(cx, cy)] != (t.X, t.Y))
                (cx, cy) = cameFrom[(cx, cy)];
            dx = cx - t.X;
            dy = cy - t.Y;
        }
        else
        {
            // b) Wie WanderStep mit Radius 1: bis zu 12 zufällige Nachbarkacheln versuchen.
            for (int attempt = 0; attempt < 12; attempt++)
            {
                int rdx = _wildRng.Next(-1, 2);
                int rdy = _wildRng.Next(-1, 2);
                if (rdx == 0 && rdy == 0)
                    continue;
                if (rdx == 0 && attempt < 8)
                    continue;
                int cx = t.X + rdx;
                int cy = t.Y + rdy;
                if (!Free(GetTile(cx, cy)))
                    continue;
                if (BuildingDistance(cx, cy) < limit)
                    continue;
                dx = rdx;
                dy = rdy;
                goto move;
            }
            return null;
        }

    move:
        // Umziehen: das Tier wandert von der alten Kachel zur neuen.
        WildAnimal tier = t.Animal;
        tier.FromX = -dx;
        tier.FromY = -dy;
        tier.Glide = WILD_STEP_SECONDS;
        if (dx != 0)
            tier.FacingLeft = dx < 0;
        Tile n = GetTile(t.X + dx, t.Y + dy);
        n.Food = kind;
        n.ResourceType = null;
        n.ResourceAmount = 0;
        n.Animal = tier;
        t.Food = FoodSource.None;
        t.ResourceType = null;
        t.ResourceAmount = 0;
        t.Animal = null;
        return (t.X + dx, t.Y + dy);
    }

    /// <summary>
    /// Eine Einheit trifft das Raubtier auf (x, y). Gibt true zurück, wenn dieser Treffer es
    /// erlegt hat.
    /// VERTRAG: IsLivePredator(x, y) false → false, nichts ändern. Sonst t = GetTile(x, y),
    /// tier = t.Animal; tier.Health = Math.Max(0, tier.Health - Math.Max(1, damage)); größer 0 →
    /// false. Bei 0 ist es erlegt: tier.Glide = 0, tier.Strike = 0. Bär (t.Food Bear):
    /// tier.Slaughtered = true, t.ResourceType = Resource.Food, t.ResourceAmount = Settings.BearFood -
    /// er bleibt in _bearCoords und liegt als Fleisch da. Wolf: _wolfCoords.Remove((x, y)),
    /// t.Food = FoodSource.None, t.ResourceType = null, t.ResourceAmount = 0, t.Animal = null.
    /// Dann true.
    /// </summary>
    public bool DamagePredator(int x, int y, int damage)
    {
        // Nur ein lebendes Raubtier kann getroffen werden.
        if (!IsLivePredator(x, y))
            return false;
        Tile t = GetTile(x, y);
        WildAnimal tier = t.Animal;
        tier.Health = Math.Max(0, tier.Health - Math.Max(1, damage));
        if (tier.Health > 0)
            return false;

        // Erlegt: keine Animation mehr.
        tier.Glide = 0;
        tier.Strike = 0;
        if (t.Food == FoodSource.Bear)
        {
            // Bär bleibt als Fleisch liegen und wird in der Kachelliste behalten.
            tier.Slaughtered = true;
            t.ResourceType = Resource.Food;
            t.ResourceAmount = Settings.BearFood;
        }
        else
        {
            // Wolf verschwindet ganz.
            _wolfCoords.Remove((x, y));
            t.Food = FoodSource.None;
            t.ResourceType = null;
            t.ResourceAmount = 0;
            t.Animal = null;
        }
        return true;
    }

    /// <summary>
    /// Wo steht dieses Raubtier gerade? Es zieht mit seiner Kachel um, die Einheit, die es
    /// angreift, hält nur das WildAnimal.
    /// VERTRAG: animal null → null. Sonst erst _bearCoords, dann _wolfCoords der Reihe nach: die
    /// erste Kachel (x, y), deren GetTile(x, y)?.Animal per ReferenceEquals gleich animal ist,
    /// liefert (x, y). Keine → null.
    /// </summary>
    public (int X, int Y)? LocatePredator(WildAnimal animal)
    {
        // Das Tier zieht mit seiner Kachel um; die Kachel finden wir über die Kachellisten.
        if (animal is null)
            return null;
        foreach (List<(int x, int y)> coords in new[] { _bearCoords, _wolfCoords })
        {
            foreach ((int x, int y) in coords)
            {
                if (ReferenceEquals(GetTile(x, y)?.Animal, animal))
                    return (x, y);
            }
        }
        return null;
    }
}
