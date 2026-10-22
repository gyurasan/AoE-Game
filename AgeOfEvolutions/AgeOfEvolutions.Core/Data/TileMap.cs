using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;
using MapGrid = AoE.Core.Map.MapGrid;
using Age = AoE.Core.Economy.Age;
using CorePosition = AoE.Core.Entities.Position;
using CorePathfinding = AoE.Core.Pathfinding.Pathfinding;
using CoreVisibility = AoE.Core.Map.VisibilitySystem;
using TileVisibility = AoE.Core.Map.TileVisibility;
using UnitEntity = AoE.Core.Entities.UnitEntity;
using BuildingEntity = AoE.Core.Entities.BuildingEntity;

namespace AgeOfEvolutions.Core.Data;
public class TileMap
{
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileSize { get; set; }
    
    private Tile[,] tiles;
    
    // Resources - List of resource patches
    public List<Resource> Resources { get; set; } = new List<Resource>();
    
    // Units on the map
    public List<Unit> Units { get; set; } = new List<Unit>();
    
    // Buildings on the map
    public List<Building> Buildings { get; set; } = new List<Building>();

    /// <summary>
    /// Die Rohstoff-Mengen dieser Karte (Klumpen, Schafe, Fisch). Ein Menü
    /// setzt eigene Werte und erzeugt daraus eine neue Karte — die Generatoren
    /// in diesem Typ lesen nur <see cref="MapSettings"/>.
    /// </summary>
    public MapSettings Settings { get; }

    // Constructor
    public TileMap(int width, int height, int tileSize = 32, MapSettings settings = null)
        : this(width, height, tileSize, settings, true)
    {
    }

    /// <summary>
    /// Gemeinsamer Konstruktor: <paramref name="generate"/> true erzeugt eine zufällige
    /// Karte, false eine leere Karte (CreateBlank).
    /// </summary>
    private TileMap(int width, int height, int tileSize, MapSettings settings, bool generate)
    {
        Width = width;
        Height = height;
        TileSize = tileSize;
        Settings = settings ?? MapSettings.Default;
        tiles = new Tile[width, height];
        if (generate)
        {
            InitializeRandomMap();
            // Den erzeugten Boden festhalten - die Wiese ändert sich danach nicht mehr (G15).
            RememberGround();
        }
        else
        {
            // Leere Karte: jede Kachel ist Wiese, keine Zufallserzeugung.
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    tiles[x, y] = new Tile(x, y, TileType.Grassland);
                }
            }
        }
    }

    /// <summary>
    /// Eine leere Karte für einen Spielstand, ohne Zufallserzeugung.
    /// VERTRAG: Width, Height und TileSize wie übergeben, Settings = <paramref name="settings"/>
    /// oder MapSettings.Default; jede Kachel ist new Tile(x, y, TileType.Grassland); keine
    /// Einheiten, keine Gebäude, keine Tiere (SheepCount, DeerCount, RabbitCount, BoarCount 0).
    /// InitializeRandomMap läuft NICHT. Dafür ein privater Konstruktor
    /// TileMap(int width, int height, int tileSize, MapSettings settings, bool generate), den der
    /// öffentliche Konstruktor mit generate = true aufruft.
    /// </summary>
    public static TileMap CreateBlank(int width, int height, int tileSize, MapSettings settings)
        => new TileMap(width, height, tileSize, settings, generate: false);

    /// <summary>
    /// Merkt sich nach der Kartenerzeugung den Boden jeder Kachel (Tile.GroundType, G15):
    /// fällt später ein Baum oder ist ein Steinbruch leer, ändert sich die Wiese darunter
    /// nicht. Der Konstruktor ruft es mit generate true direkt nach InitializeRandomMap auf.
    /// VERTRAG: für jede Kachel tile.GroundType = tile.Type.
    /// </summary>
    private void RememberGround()
    {
        // Den Boden der Kartenerzeugung festhalten (G15): die Wiese ändert sich danach nicht mehr.
        foreach (var tile in tiles)
            tile.GroundType = tile.Type;
    }

    /// <summary>
    /// Baut die Wander-Bestände der Tiere aus den Kacheln neu auf (nach dem Laden eines Spielstands).
    /// VERTRAG: leert _sheepCoords, _deerCoords, _rabbitCoords, _boarCoords und _claimed; geht dann
    /// alle Kacheln durch (x außen, y innen, je aufsteigend) und trägt jede Kachel mit
    /// Food Sheep in _sheepCoords, Deer in _deerCoords, Rabbit in _rabbitCoords und Boar in
    /// _boarCoords ein. Andere Kacheln bleiben außen vor.
    /// </summary>
    public void RebuildWildAnimals()
    {
        // Alte Bestände und Reservierungen verwerfen, dann aus den Kacheln neu aufbauen.
        _sheepCoords.Clear();
        _deerCoords.Clear();
        _rabbitCoords.Clear();
        _boarCoords.Clear();
        _claimed.Clear();

        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var t = tiles[x, y];
                switch (t.Food)
                {
                    case FoodSource.Sheep:
                        _sheepCoords.Add((x, y));
                        break;
                    case FoodSource.Deer:
                        _deerCoords.Add((x, y));
                        break;
                    case FoodSource.Rabbit:
                        _rabbitCoords.Add((x, y));
                        break;
                    case FoodSource.Boar:
                        _boarCoords.Add((x, y));
                        break;
                }
            }
        }
    }

    // Initialize a random map
    // Zufallszahlengenerator für Kartengenerierung (vorher: DateTime.Now.Ticks % Range
    // → in einer eng takteten Schleife nahezu konstante Werte, alle Flecken übereinander)
    private readonly Random _random = new Random();
    
    private void InitializeRandomMap()
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                tiles[x, y] = new Tile(x, y, GetRandomTileType(x, y));
            }
        }
        
        // Startplätze zuerst würfeln (SZ1) - Seen, Klumpen und Wild umgehen sie.
        ChooseStartPositions();

        // Add resource patches
        AddResourcePatches();

        // Place starting positions
        PlaceStartingPositions();
    }
    
    private TileType GetRandomTileType(int x, int y)
    {
        // Nahezu reine Graskarte: nur einzelne Bäume,
        // keine Rohstoff-Kacheln (die kommen als Klumpen in AddResourcePatches).
        // Graue Deko-Felsen gibt es nicht mehr – sie sahen aus wie Stein, trugen aber nie eine Ressource.
        // Ortsfester Hash, damit dieselbe Karte reproduzierbar bleibt.
        //
        // unchecked + Vorzeichenbit ausmaskieren ist hier zwingend: die
        // Multiplikation läuft ab x = 30 über int.MaxValue und wird negativ,
        // und % liefert in C# bei negativem Dividenden ein negatives Ergebnis.
        // Damit traf 'rand < 4' immer zu — die Spalten x = 30..58 waren zu
        // 100 % Wald und der Waldanteil lag bei 47,8 % statt bei 4 %.
        int hash = unchecked(x * 73856093) ^ unchecked(y * 19349663);
        int rand = (hash & 0x7FFFFFFF) % 100;
        
        if (rand < 4) return TileType.Forest;
        return TileType.Grassland;
    }
    
    // --- Naturformen ------------------------------------------------------
    
    // Weicher Kreis (Radius^2-Dämpfung an den Rändern), wie natürliche Seen.
    private bool InSoftCircle(int x, int y, int cx, int cy, int radius, Random rng)
    {
        int dx = x - cx, dy = y - cy;
        float dist = (float)Math.Sqrt(dx * dx + dy * dy);
        if (dist < radius - 1.5f) return true;
        if (dist > radius + 0.5f) return false;
        return rng.NextDouble() < 0.5;
    }
    
    private void AddLakes()
    {
        int count = _random.Next(Settings.LakesMin, Settings.LakesMax + 1);
        for (int i = 0; i < count; i++)
        {
            // Kein See in die Startzonen: ClearStartArea räumte ihn danach zu einem
            // Quadrat aus Wiese aus, mit schnurgeraden Ufern und Stränden. Ein See,
            // der hineinreicht, wird neu ausgewürfelt
            int r, cx, cy, tries = 0;
            do
            {
                r = _random.Next(3, 6);
                cx = _random.Next(4, Width - 4 - r);
                cy = _random.Next(4, Height - 4 - r);
            } while (NearStartArea(cx, cy, r) && ++tries < 20);
            if (NearStartArea(cx, cy, r))
                continue;
            for (int x = cx - r; x <= cx + r; x++)
            {
                for (int y = cy - r; y <= cy + r; y++)
                {
                    if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                    if (!InSoftCircle(x, y, cx, cy, r, _random)) continue;
                    var t = tiles[x, y];
                    t.Type = TileType.Water;
                    t.ResourceType = null;
                    t.ResourceAmount = 0;
                    // Wasser ist nicht begehbar: Walkable wird beim Kachel-
                    // Konstruktor (Wiese) einmalig gesetzt und folgt einer
                    // späten Umstellung auf Wasser nicht von selbst. Ohne
                    // diese Zeile liefen Einheiten über den See, weil die
                    // Wegsuche Walkable fragt.
                    t.Walkable = false;
                    t.Buildable = false;
                }
            }
        }
    }
    
    /// <summary>
    /// Ob ein See um (cx, cy) mit Radius r in eine der beiden Startzonen reicht
    /// (ClearStartArea: 8 × 8 Kacheln ab zwei Kacheln vor dem Stadtzentrum) - mit
    /// Luft für den ausgefransten Rand und den Strand.
    /// </summary>
    private bool NearStartArea(int cx, int cy, int r)
    {
        foreach (var (sx, sy) in new[] { (_startP1.x + 1.5f, _startP1.y + 1.5f), (_startP2.x + 1.5f, _startP2.y + 1.5f) })
        {
            float dx = cx - sx, dy = cy - sy;
            if (dx * dx + dy * dy < (r + 7f) * (r + 7f))
                return true;
        }
        return false;
    }

    // Sandstrand um jede Wasserkante (wie in AoE1).
    private void AddBeaches()
    {
        bool[,] isWater = new bool[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                isWater[x, y] = tiles[x, y].Type == TileType.Water;
        
        var sand = new List<(int x, int y)>();
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                if (isWater[x, y]) continue;
                if (tiles[x, y].Type != TileType.Grassland) continue;
                if (IsAdjacentWater(x, y, isWater)) sand.Add((x, y));
            }
        }
        foreach (var (x, y) in sand)
            tiles[x, y].Type = TileType.Sand;
    }
    
    private bool IsAdjacentWater(int x, int y, bool[,] water)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                int nx = x + dx, ny = y + dy;
                if (nx >= 0 && ny >= 0 && nx < Width && ny < Height && water[nx, ny])
                    return true;
            }
        return false;
    }
    
    // --- Ressourcenklumpen -------------------------------------------------
    
    // Klumpenzentren an zufälligen freien Stellen, die weit weg von beiden
    // Startpositionen liegen. Rückkehrwert: Liste der Zentren (für Tiere).
    private List<(int x, int y)> PlaceClusterCenters(int count, int minSize)
    {
        var centers = new List<(int, int)>();
        for (int i = 0; i < count; i++)
        {
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int cx = _random.Next(minSize, Width - minSize);
                int cy = _random.Next(minSize, Height - minSize);
                if (TooCloseToStart(cx, cy, minSize + 3)) continue;
                if (centers.Any(c => Math.Abs(c.Item1 - cx) + Math.Abs(c.Item2 - cy) < minSize * 3)) continue;
                centers.Add((cx, cy));
                break;
            }
        }
        return centers;
    }
    
    private bool TooCloseToStart(int x, int y, int dist)
    {
        if (Math.Abs(x - _startP1.x) + Math.Abs(y - _startP1.y) < dist * 2) return true;
        if (Math.Abs(x - _startP2.x) + Math.Abs(y - _startP2.y) < dist * 2) return true;
        return false;
    }
    
    private void StampResource(int cx, int cy, int radius, TileType type,
                               Resource resource, int baseAmount)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                if (!InSoftCircle(x, y, cx, cy, radius, _random)) continue;
                var t = tiles[x, y];
                if (t.Type == TileType.Water || t.Building != null) continue;
                t.Type = type;
                // Die Ressource kommt vom Klumpen, nicht aus dem Konstruktor der
                // Kachel – sonst war Stein nie abbaubar und trug Holz, wo vorher
                // ein Einzelbaum stand. Überlappen sich zwei gleiche Klumpen,
                // bleibt die Menge des ersten.
                if (t.ResourceType != resource)
                {
                    t.ResourceType = resource;
                    t.ResourceAmount = baseAmount + _random.Next(0, baseAmount / 2);
                }
                t.Food = FoodSource.None;
            }
        }
    }
    
    // Kleine Gras-Inseln im Wald (aufsammlbar ohne Bäume fällen zu müssen)
    private void StampClearing(int cx, int cy, int radius)
    {
        for (int x = cx - radius; x <= cx + radius; x++)
        {
            for (int y = cy - radius; y <= cy + radius; y++)
            {
                if (x < 0 || y < 0 || x >= Width || y >= Height) continue;
                var t = tiles[x, y];
                if (t.Type == TileType.Forest && InSoftCircle(x, y, cx, cy, radius, _random))
                    t.Type = TileType.Grassland;
            }
        }
    }
    
    private void AddResourcePatches()
    {
        // Natürliche Gewässer zuerst, dann Strände.
        AddLakes();
        AddBeaches();

        // Fische an der Küste – nach den Stränden, damit feststeht, wo Land ist
        PlaceFish();

        // Wälder (Holz): Klumpen mit Lichtung für Nahrungsklumpen. Die Zahlen
        // kommen aus MapSettings. Jeder Wald trägt seinen Radius mit sich,
        // damit die Beerenbüsche an den Rand gestellt werden können.
        var woodCenters = PlaceClusterCenters(_random.Next(Settings.WoodClustersMin, Settings.WoodClustersMax + 1), 3);
        var woods = new List<(int x, int y, int radius)>();
        foreach (var (cx, cy) in woodCenters)
        {
            int r = _random.Next(Settings.WoodRadiusMin, Settings.WoodRadiusMax + 1);
            woods.Add((cx, cy, r));
            StampResource(cx, cy, r, TileType.Forest, Resource.Wood, 100);
            if (_random.NextDouble() < 0.7)
            {
                // Lichtung am Rand des Waldes
                int ox = cx + _random.Next(-1, 2) * (r + 1);
                int oy = cy + _random.Next(-1, 2) * (r + 1);
                StampClearing(ox, oy, 1);
            }
        }

        // Steinbrüche: 4–7 Klumpen (vorher 3–4), je etwas größer
        foreach (var (cx, cy) in PlaceClusterCenters(_random.Next(Settings.StoneClustersMin, Settings.StoneClustersMax + 1), 2))
            StampResource(cx, cy, _random.Next(Settings.StoneRadiusMin, Settings.StoneRadiusMax + 1), TileType.Mountain, Resource.Stone, 80);

        // Goldklumpen: 3–6 kleine, wertvollere Klumpen (vorher 2–3)
        foreach (var (cx, cy) in PlaceClusterCenters(_random.Next(Settings.GoldClustersMin, Settings.GoldClustersMax + 1), 2))
            StampResource(cx, cy, _random.Next(Settings.GoldRadiusMin, Settings.GoldRadiusMax + 1), TileType.GoldMine, Resource.Gold, 60);

        // Startrohstoffe in Laufweite beider Stadtzentren, zur Kartenmitte hin
        PlaceStartResources(_startP1.x, _startP1.y, _startP1.x * 2 < Width ? 1 : -1, _startP1.y * 2 < Height ? 1 : -1);
        PlaceStartResources(_startP2.x, _startP2.y, _startP2.x * 2 < Width ? 1 : -1, _startP2.y * 2 < Height ? 1 : -1);

        // Nahrung: Schafherden auf der Wiese + Rehe als Wild + Beerenbüsche
        PlaceSheep(woodCenters);
        PlaceDeer(woodCenters);
        PlaceSmallGame(FoodSource.Rabbit, _rabbitCoords, Settings.RabbitGroupsMin, Settings.RabbitGroupsMax,
                       Settings.RabbitsPerGroupMin, Settings.RabbitsPerGroupMax, Settings.RabbitFood,
                       Settings.RabbitWanderSecondsMax);
        PlaceSmallGame(FoodSource.Boar, _boarCoords, Settings.BoarGroupsMin, Settings.BoarGroupsMax,
                       Settings.BoarsPerGroupMin, Settings.BoarsPerGroupMax, Settings.BoarFood,
                       Settings.BoarWanderSecondsMax);
        PlaceBerryBushes(woods);
    }

    /// <summary>
    /// Fischschwärme an der Küste: auf Wasserkacheln mit Land daneben. Von dort
    /// aus fängt ein Dorfbewohner – laut Spezifikation rund 200 Nahrung je Schwarm.
    /// </summary>
    private void PlaceFish()
    {
        float chance = Settings.FishChance;
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                var t = tiles[x, y];
                if (t.Type != TileType.Water || !IsCoast(x, y)) continue;
                if (_random.NextDouble() > chance) continue;
                t.ResourceType = Resource.Food;
                t.ResourceAmount = 200;
                t.Food = FoodSource.Fish;
            }
        }
    }

    /// <summary>Wasserkachel mit mindestens einer Landkachel unter den acht Nachbarn.</summary>
    private bool IsCoast(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                if (dx == 0 && dy == 0) continue;
                var n = GetTile(x + dx, y + dy);
                if (n != null && n.Type != TileType.Water) return true;
            }
        }
        return false;
    }
    
    // --- Startrohstoffe ---------------------------------------------------

    /// <summary>
    /// Startrohstoffe wie in AoE: jeder Spieler bekommt einen Steinbruch und
    /// eine Goldmine in Laufweite seines Stadtzentrums, zur Kartenmitte hin.
    /// Die zufälligen Klumpen halten Abstand zu den Startplätzen – ohne das lag
    /// der nächste Stein im Schnitt 33 Kacheln entfernt, Gold bis zu 98.
    /// </summary>
    private void PlaceStartResources(int tcX, int tcY, int dirX, int dirY)
    {
        int mx = tcX + 2, my = tcY + 2;   // Mitte des 4 × 4-Stadtzentrums
        PlaceStartCluster(mx + dirX * 8, my + dirY * 2, TileType.Mountain, Resource.Stone, 80);
        PlaceStartCluster(mx + dirX * 2, my + dirY * 8, TileType.GoldMine, Resource.Gold, 60);
    }

    /// <summary>
    /// Ein Klumpen mit Radius 2. Liegt auf der Mittelkachel Wasser, wird sie
    /// vorher zu Wiese – StampResource überspringt Wasser, und ohne Mittelkachel
    /// könnte der Klumpen ganz ausfallen.
    /// </summary>
    private void PlaceStartCluster(int cx, int cy, TileType type, Resource resource, int baseAmount)
    {
        var center = GetTile(cx, cy);
        if (center == null) return;
        if (center.Type == TileType.Water)
        {
            center.Type = TileType.Grassland;
            center.Walkable = true;
        }
        StampResource(cx, cy, 2, type, resource, baseAmount);
    }

    // Schafherden: 6–9 Stellen je 6–14 Schafe, nur auf Gras. Die Schaf-Kacheln
    // landen in _sheepCoords — die Wanderung (UpdateSheep) bewegt genau diese.
    // Die Aufstellung verteilt eine Herde über ein 5×5-Feld, damit die
    // verdoppelte Menge nicht auf dieselben Kacheln gestapelt wird.
    private void PlaceSheep(List<(int x, int y)> woodCenters)
    {
        int herds = _random.Next(Settings.SheepHerdsMin, Settings.SheepHerdsMax + 1);
        var herdCenters = PlaceClusterCenters(herds, 2);
        foreach (var (cx, cy) in herdCenters)
        {
            int count = _random.Next(Settings.SheepPerHerdMin, Settings.SheepPerHerdMax + 1);
            for (int i = 0; i < count; i++)
            {
                int sx = cx + _random.Next(-2, 3);
                int sy = cy + _random.Next(-2, 3);
                var t = GetTile(sx, sy);
                if (t == null || t.Type != TileType.Grassland) continue;
                if (t.Food == FoodSource.Sheep) continue;   // eine Kachel pro Schaf
                t.ResourceType = Resource.Food;
                t.ResourceAmount = Settings.SheepFood;
                t.Food = FoodSource.Sheep;
                t.Animal = NewAnimal(Settings.SheepWanderSecondsMax);
                _sheepCoords.Add((sx, sy));
            }
        }
    }

    // Rehe: etwas weniger als die Schafe (~70 % der Dichte, seit 2026-10-04), in Herden
    // auf Gras über je 5×5 Kacheln, ein Reh je Kachel; die Wanderung (UpdateDeer) bewegt
    // diese. Die ersten beiden Herden stehen je eine in Reichweite eines Stadtzentrums (DeerStartDistanceMin bis
    // -Max Kacheln, wie die Startjagd in AoE), die übrigen irgendwo auf der Karte.
    // Eine Herde, die keinen Platz auf Gras findet, sucht sich eine neue Stelle -
    // vorher blieb rund jede 75. Karte ganz ohne Rehe.
    private void PlaceDeer(List<(int x, int y)> woodCenters)
    {
        int herds = _random.Next(Settings.DeerHerdsMin, Settings.DeerHerdsMax + 1);
        var starts = new[] { (x: _startP1.x, y: _startP1.y), (x: _startP2.x, y: _startP2.y) };
        for (int h = 0; h < herds; h++)
        {
            int count = _random.Next(Settings.DeerPerHerdMin, Settings.DeerPerHerdMax + 1);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                var (cx, cy) = h < starts.Length ? NearStart(starts[h].x, starts[h].y)
                                                 : (_random.Next(2, Width - 2), _random.Next(2, Height - 2));
                if (h >= starts.Length && TooCloseToStart(cx, cy, 5)) continue;
                if (GetTile(cx, cy)?.Type != TileType.Grassland) continue;
                if (PlaceDeerHerd(cx, cy, count) > 0) break;
            }
        }
    }

    /// <summary>
    /// Eine Stelle DeerStartDistanceMin bis -Max Kacheln (waagerecht plus
    /// senkrecht) vom Startplatz (sx, sy) entfernt, zur Kartenmitte hin.
    /// </summary>
    private (int x, int y) NearStart(int sx, int sy)
    {
        int d = _random.Next(Settings.DeerStartDistanceMin, Settings.DeerStartDistanceMax + 1);
        int dx = _random.Next(0, d + 1), dy = d - dx;
        int x = sx < Width / 2 ? sx + dx : sx - dx;
        int y = sy < Height / 2 ? sy + dy : sy - dy;
        return (Math.Clamp(x, 2, Width - 3), Math.Clamp(y, 2, Height - 3));
    }

    /// <summary>
    /// Stellt bis zu count Rehe auf freie Wiese im 5×5-Feld um (cx, cy), ein Reh je
    /// Kachel; gibt zurück, wie viele es geworden sind.
    /// </summary>
    private int PlaceDeerHerd(int cx, int cy, int count)
    {
        int placed = 0;
        for (int i = 0; i < count; i++)
        {
            int sx = cx + _random.Next(-2, 3);
            int sy = cy + _random.Next(-2, 3);
            var t = GetTile(sx, sy);
            if (t == null || t.Type != TileType.Grassland) continue;
            if (t.Food != FoodSource.None || !string.IsNullOrEmpty(t.Building)) continue;
            t.ResourceType = Resource.Food;
            t.ResourceAmount = Settings.DeerFood;
            t.Food = FoodSource.Deer;
            t.Animal = NewAnimal(Settings.DeerWanderSecondsMax);
            _deerCoords.Add((sx, sy));
            placed++;
        }
        return placed;
    }

    /// <summary>
    /// Kaninchen und Wildschweine: groupsMin bis groupsMax Gruppen auf Gras, irgendwo
    /// auf der Karte außer an den Startplätzen, je Gruppe perMin bis perMax Tiere über
    /// 5×5 Kacheln, ein Tier je Kachel. Findet eine Gruppe keinen Platz auf Gras,
    /// sucht sie sich eine neue Stelle.
    /// </summary>
    private void PlaceSmallGame(FoodSource source, List<(int x, int y)> coords, int groupsMin, int groupsMax,
                                int perMin, int perMax, int food, float maxSeconds)
    {
        int groups = _random.Next(groupsMin, groupsMax + 1);
        for (int g = 0; g < groups; g++)
        {
            int count = _random.Next(perMin, perMax + 1);
            for (int attempt = 0; attempt < 40; attempt++)
            {
                int cx = _random.Next(2, Width - 2), cy = _random.Next(2, Height - 2);
                if (TooCloseToStart(cx, cy, 4)) continue;
                if (GetTile(cx, cy)?.Type != TileType.Grassland) continue;
                int placed = 0;
                for (int i = 0; i < count; i++)
                {
                    int sx = cx + _random.Next(-2, 3), sy = cy + _random.Next(-2, 3);
                    var t = GetTile(sx, sy);
                    if (t == null || t.Type != TileType.Grassland) continue;
                    if (t.Food != FoodSource.None || !string.IsNullOrEmpty(t.Building)) continue;
                    t.ResourceType = Resource.Food;
                    t.ResourceAmount = food;
                    t.Food = source;
                    t.Animal = NewAnimal(maxSeconds);
                    coords.Add((sx, sy));
                    placed++;
                }
                if (placed > 0) break;
            }
        }
    }
    
    // Beerenbüsche: rings um jeden Wald, im Band direkt am Rand (auch auf
    // gestampften Lichtungen) — die Wahrscheinlichkeit je Wald und die Büsche
    // je Wald kommen aus MapSettings (0..1). Früher um die Klumpenmitte:
    // der Klumpen trägt Radius 3–5, die ±2-Kiste lag also fast komplett im
    // eigenen Wald und wurde dort als „keine Wiese“ verworfen — die Karte
    // trug im Schnitt nur ~13 Büsche statt der ~130, die eingestellt sind.
    private void PlaceBerryBushes(List<(int x, int y, int radius)> woods)
    {
        foreach (var (cx, cy, r) in woods)
        {
            if (_random.NextDouble() > Settings.BerryChance) continue;
            int count = _random.Next(Settings.BerriesMin, Settings.BerriesMax + 1);
            for (int i = 0; i < count; i++)
            {
                for (int attempt = 0; attempt < 12; attempt++)
                {
                    float winkel = (float)_random.NextDouble() * MathF.Tau;
                    int d = r + 1 + _random.Next(0, 2);   // Band am Rand: sicher außerhalb des Soft-Kreis
                    int bx = cx + (int)MathF.Round(MathF.Cos(winkel) * d);
                    int by = cy + (int)MathF.Round(MathF.Sin(winkel) * d);
                    var t = GetTile(bx, by);
                    if (t == null || t.Type != TileType.Grassland) continue;
                    if (!string.IsNullOrEmpty(t.Building)) continue;
                    if (t.Food != FoodSource.None) continue;   // Schaf/Reh/Wild bleibt stehen
                    t.ResourceType = Resource.Food;
                    t.ResourceAmount = 125;
                    t.Food = FoodSource.Berries;
                    break;
                }
            }
        }
    }
    
    // --- Startplätze (SZ1) --------------------------------------------------

    // Linke obere Ecke des 4 × 4-Stadtzentrums je Spieler, pro Karte zufällig
    // gewählt (ChooseStartPositions). Der leeren Karte (CreateBlank) genügen die
    // alten Werte - dort kommen die Stadtzentren ohnehin aus dem Spielstand.
    private (int x, int y) _startP1 = (3, 3);
    private (int x, int y) _startP2;

    /// <summary>
    /// Die linke obere Ecke des Stadtzentrums von Spieler <paramref name="owner"/>
    /// in Kacheln (0 = Spieler 1, 1 = Spieler 2).
    /// VERTRAG: owner 0 liefert _startP1, jeder andere Wert _startP2.
    /// </summary>
    public (int x, int y) StartPosition(int owner) => owner == 0 ? _startP1 : _startP2;

    /// <summary>
    /// Wählt die beiden Startplätze zufällig. InitializeRandomMap ruft das vor
    /// AddResourcePatches auf, damit Seen, Rohstoffklumpen und Wild die
    /// Startplätze von Anfang an umgehen.
    /// VERTRAG:
    /// 1) Der erlaubte Bereich ist x von 3 bis Width - 7 und y von 3 bis
    ///    Height - 7 (darin passen ClearStartArea von x - 2 bis x + 5 und die
    ///    Dorfbewohner-Versätze bis x + 5); ist die Karte so klein, dass die
    ///    obere Grenze unter der 3 läge, ist sie die 3.
    /// 2) Die euklidische Entfernung der beiden Plätze zueinander beträgt
    ///    mindestens die halbe Kartenbreite (Width / 2,0).
    /// 3) Gewürfelt wird bis zu 400 Mal: beide Plätze unabhängig und
    ///    gleichverteilt im erlaubten Bereich; das erste Paar mit dem
    ///    Mindestabstand gewinnt.
    /// 4) Findet kein Wurf ein Paar (sehr kleine Karten), stehen die beiden
    ///    Plätze auf einander gegenüberliegenden Ecken des erlaubten Bereichs:
    ///    (3, 3) und (obere Grenzen) - die größtmögliche Entfernung.
    /// 5) _startP1 trägt den ersten Platz (Spieler 1), _startP2 den zweiten
    ///    (Spieler 2).
    /// </summary>
    private void ChooseStartPositions()
    {
        int loX = 3, hiX = Math.Max(3, Width - 7);
        int loY = 3, hiY = Math.Max(3, Height - 7);
        double mindestens = Width / 2.0;
        for (int versuch = 0; versuch < 400; versuch++)
        {
            var a = (x: _random.Next(loX, hiX + 1), y: _random.Next(loY, hiY + 1));
            var b = (x: _random.Next(loX, hiX + 1), y: _random.Next(loY, hiY + 1));
            double dx = a.x - b.x, dy = a.y - b.y;
            if (Math.Sqrt(dx * dx + dy * dy) >= mindestens)
            {
                _startP1 = a;
                _startP2 = b;
                return;
            }
        }
        // Sehr kleine Karten: die gegenüberliegenden Ecken des erlaubten Bereichs
        // sind die größtmögliche Entfernung, die geht immer.
        _startP1 = (loX, loY);
        _startP2 = (hiX, hiY);
    }

    private void PlaceStartingPositions()
    {
        // Place starting town centers
        // Player 1 - gewürfelter Startplatz (SZ1)
        var (p1x, p1y) = _startP1;
        ClearStartArea(p1x, p1y);
        AddBuilding(p1x, p1y, "Stadtzentrum", 0);

        // Player 2 - der zweite gewürfelte Startplatz, mindestens eine halbe
        // Kartenbreite von Spieler 1 entfernt
        var (p2x, p2y) = _startP2;
        ClearStartArea(p2x, p2y);
        AddBuilding(p2x, p2y, "Stadtzentrum", 1);
        
        // Initial villagers
        AddVillager(p1x + 4, p1y + 3, 0);
        AddVillager(p1x + 5, p1y + 3, 0);
        AddVillager(p1x + 3, p1y + 4, 0);
        AddVillager(p1x + 4, p1y + 4, 0);
        
        AddVillager(p2x - 1, p2y, 1);
        AddVillager(p2x - 2, p2y, 1);
        AddVillager(p2x, p2y - 1, 1);
        AddVillager(p2x - 1, p2y - 1, 1);
    }
    
    /// <summary>
    /// Räumt die Startzone eines Spielers frei: jede Kachel von (x - 2, y - 2)
    /// bis einschließlich (x + 5, y + 5) wird zu begehbarer, bebaubarer Wiese
    /// ohne Ressourcen — Stadtzentrum plus zwei Kacheln Rand. Kacheln
    /// außerhalb der Karte werden übersprungen (GetTile liefert dort null).
    /// </summary>
    private void ClearStartArea(int x, int y)
    {
        for (int tx = x - 2; tx <= x + 5; tx++)
        {
            for (int ty = y - 2; ty <= y + 5; ty++)
            {
                var tile = GetTile(tx, ty);
                if (tile == null) continue;
                tile.Type = TileType.Grassland;
                tile.ResourceType = null;
                tile.ResourceAmount = 0;
                tile.Food = FoodSource.None;
                tile.Walkable = true;
                tile.Buildable = true;
            }
        }
    }
    
    /// <summary>
    /// Setzt eine Einheit des Typs <paramref name="type"/> auf die Mitte der Kachel (x, y),
    /// nimmt sie in <see cref="Units"/> auf und gibt sie zurück - wie AddVillager, für jeden
    /// Typ (P1: ausgebildete Soldaten).
    /// </summary>
    public Unit AddUnit(UnitType type, int x, int y, int ownerId)
    {
        // Wie AddVillager, nur mit dem übergebenen Typ (P1: ausgebildete Soldaten)
        var unit = new Unit(type, ownerId)
        {
            Position = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2)
        };
        Units.Add(unit);
        return unit;
    }

    /// <summary>
    /// Setzt einen Dorfbewohner auf die Mitte der Kachel (x, y), nimmt ihn in
    /// <see cref="Units"/> auf und gibt ihn zurück.
    /// </summary>
    public Unit AddVillager(int x, int y, int ownerId)
    {
        var villager = new Unit(UnitType.Villager, ownerId)
        {
            Position = new Vector2(x * TileSize + TileSize / 2, y * TileSize + TileSize / 2)
        };
        Units.Add(villager);
        return villager;
    }
    
    /// <summary>
    /// Setzt ein quadratisches Gebäude mit der linken oberen Ecke auf (x, y)
    /// und gibt es zurück: <paramref name="size"/> Kacheln je Seite, das
    /// Stadtzentrum 4, ein Haus 2. Fertig ist es sofort - wer eine Baustelle
    /// will, setzt danach <see cref="Building.Construction"/>.
    /// </summary>
    public Building AddBuilding(int x, int y, string buildingType, int ownerId, int size = 4)
    {
        // Die Sicht rechnet von der Mitte der Grundfläche aus
        var center = new CorePosition(x + size / 2, y + size / 2);
        var building = new Building(CoreBuildings.Create(buildingType, ownerId, center))
        {
            X = x,
            Y = y,
            Type = buildingType,
            OwnerId = ownerId,
            Width = size,
            Height = size
        };
        Buildings.Add(building);

        // Mark tiles as occupied
        for (int bx = x; bx < x + building.Width && bx < Width; bx++)
        {
            for (int by = y; by < y + building.Height && by < Height; by++)
            {
                tiles[bx, by].Building = buildingType;
                // Ein Gebäude sperrt den Weg: die Wegsuchbarkeit (NavGrid)
                // liest Walkable, und Einheiten laufen nicht durch Stein und
                // Holz. Die Kacheleigenschaft selbst bleibt, weil die
                // Bauplatz- und Arbeitbarkeits-Prüfungen danach sehen.
                tiles[bx, by].Walkable = false;
                tiles[bx, by].Buildable = false;
                _navDirty = true;
            }
        }

        // Am Gitter der Kernbibliothek anmelden — dort spendet das Gebäude Sicht
        NavGrid.AddBuilding(building.Core);
        return building;
    }
    
    /// <summary>
    /// Nimmt ein zerstörtes Gebäude von der Karte (K2).
    /// VERTRAG: entfernt es aus Buildings; jede Kachel seiner Grundfläche trägt danach kein
    /// Gebäude mehr (Building null) und ist wieder begehbar und bebaubar (Walkable und
    /// Buildable true - unter Gebäuden liegt nur Wiese oder Sand); die Wegsuche erfährt es
    /// (_navDirty = true), und das Sichtsystem vergisst das Gebäude
    /// (NavGrid.RemoveBuilding(building.Core)). Ein Gebäude, das nicht auf der Karte steht,
    /// ändert nichts.
    /// </summary>
    public void RemoveBuilding(Building building)
    {
        // Nicht auf der Karte: nichts tun
        if (!Buildings.Remove(building))
            return;

        // Kacheln der Grundfläche freigeben: kein Gebäude mehr, wieder begehbar
        // und bebaubar (unter Gebäuden liegt nur Wiese oder Sand)
        for (int bx = building.X; bx < building.X + building.Width && bx < Width; bx++)
        {
            for (int by = building.Y; by < building.Y + building.Height && by < Height; by++)
            {
                tiles[bx, by].Building = null;
                tiles[bx, by].Walkable = true;
                tiles[bx, by].Buildable = true;
                _navDirty = true;
            }
        }

        // Das Sichtsystem vergisst das Gebäude
        NavGrid.RemoveBuilding(building.Core);
    }

    /// <summary>
    /// Ob ein quadratisches Gebäude mit der linken oberen Ecke auf (x, y) Platz
    /// hat: jede Kachel der Grundfläche liegt auf der Karte, ist Wiese oder
    /// Sand, begehbar und bebaubar und trägt weder Gebäude noch Ressource.
    /// Einheiten und Nebel prüft der Aufrufer.
    /// </summary>
    public bool CanPlaceBuilding(int x, int y, int size)
    {
        for (int bx = x; bx < x + size; bx++)
        {
            for (int by = y; by < y + size; by++)
            {
                var tile = GetTile(bx, by);
                if (tile == null || !tile.Walkable || !tile.Buildable
                    || !TileMapHelper.CanBuildOn(tile.Type)
                    || !string.IsNullOrEmpty(tile.Building) || tile.ResourceType.HasValue
                    || tile.Farm)
                    return false;
            }
        }
        return true;
    }

    public Tile GetTile(int x, int y)
    {
        if (x >= 0 && x < Width && y >= 0 && y < Height)
            return tiles[x, y];
        return null;
    }

    /// <summary>
    /// Macht aus einer erschöpften Quelle normales Gelände: gefällter Wald,
    /// leerer Steinbruch und leere Goldmine werden zu Wiese und bebaubar.
    /// Vorher blieb die Kachel als Wald ohne Holz stehen. Die Begehbarkeit
    /// ändert sich nicht — auch Wald und Minen sind begehbar.
    /// </summary>
    public void ClearResource(Tile tile)
    {
        if (tile.Farm)
        {
            // Eine geerntete Farm-Kachel bleibt Feld: der Vorrat ist leer und sie
            // wächst wieder nach (RegrowCrop). Sie wird nicht zu Wiese abgebaut wie
            // ein gefällter Baum würde.
            tile.ResourceType = null;
            tile.ResourceAmount = 0;
            return;
        }
        tile.ResourceType = null;
        tile.ResourceAmount = 0;
        tile.Food = FoodSource.None;
        if (tile.Type is TileType.Forest or TileType.Mountain or TileType.GoldMine)
            tile.Type = TileType.Grassland;
        tile.Buildable = string.IsNullOrEmpty(tile.Building) && TileMapHelper.CanBuildOn(tile.Type);
    }

    /// <summary>
    /// Ein geleertes Schaf oder Reh verlässt den Wander-Bestand, damit es
    /// nicht als „leere" Kachel weiterwandert. Reservierungen bleiben an der
    /// Kachel hängen, hindern aber nichts mehr — sie wird ohnehin übersprungen.
    /// </summary>
    public void ClearResourceAndRemoveSheep(Tile tile)
    {
        if (tile.Food.IsWild())
            RemoveSheep(tile.X, tile.Y);
        tile.Animal = null;
        ClearResource(tile);
    }
    
    public bool IsWalkable(int x, int y)
    {
        var tile = GetTile(x, y);
        return tile != null && tile.Walkable && string.IsNullOrEmpty(tile.Building);
    }

    /// <summary>
    /// Ob Spieler <paramref name="playerId"/> die Kachel (x, y) für begehbar halten darf:
    /// hat er sie noch nie gesehen, ja - sonst verriete die Wegsuche, was im Dunkeln
    /// liegt; hat er sie gesehen, wie sie ist (IsWalkable).
    /// VERTRAG: !IsTileExplored(x, y, playerId) || IsWalkable(x, y); außerhalb der Karte false.
    /// </summary>
    public bool IsKnownWalkable(int x, int y, int playerId)
    {
        return GetTile(x, y) != null && (!IsTileExplored(x, y, playerId) || IsWalkable(x, y));
    }

    /// <summary>
    /// Der Weg für einen Laufbefehl von Spieler <paramref name="playerId"/> - geplant nur
    /// mit dem, was er weiß (IsKnownWalkable). Ein Klick ins Schwarze wirkt so immer,
    /// egal was darunter liegt, und der Weg macht keinen Bogen um ein Hindernis, das
    /// noch niemand gesehen hat. Stößt die Einheit unterwegs darauf, plant der
    /// Spielbildschirm neu.
    /// VERTRAG:
    /// - Gesucht wird auf einem eigenen MapGrid (Width × Height), dessen IsPassable genau
    ///   IsKnownWalkable(x, y, playerId) ist, mit CorePathfinding.FindPath (vier Richtungen).
    /// - Das Ziel ist die Kachel unter <paramref name="end"/>, wenn sie von der Kachel unter
    ///   <paramref name="start"/> aus auf diesem Gitter erreichbar ist; sonst die erreichbare
    ///   Kachel, die ihr am nächsten liegt (Abstand der Kachelmitten; bei Gleichstand die,
    ///   die die Breitensuche vom Start aus zuerst findet). Erreichbar heißt: per
    ///   Breitensuche in vier Richtungen über passierbare Kacheln.
    /// - Rückgabe wie FindPath: Wegpunkte als Kachelmitten (GridToWorld) ohne die
    ///   Startkachel; ist das Ziel die Kachel unter end, liegt der letzte Wegpunkt genau
    ///   auf end. Ist das Ziel die Startkachel selbst, eine leere Liste.
    /// </summary>
    // Gitter für das Wissen des Spielers, wird wiederverwendet.
    private MapGrid _knownGrid;

    public List<Vector2> FindPathKnown(int playerId, Vector2 start, Vector2 end)
    {
        var path = new List<Vector2>();

        _knownGrid ??= new MapGrid(Width, Height);
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                _knownGrid.GetTile(x, y).IsPassable = IsKnownWalkable(x, y, playerId);
            }
        }

        // Startkachel, auf die Karte begrenzt
        var gridStart = WorldToGrid(start);
        int startX = Math.Clamp((int)gridStart.X, 0, Width - 1);
        int startY = Math.Clamp((int)gridStart.Y, 0, Height - 1);

        // Zielkachel unter end, ebenfalls begrenzt
        int zielX = Math.Clamp((int)MathF.Floor(end.X / TileSize), 0, Width - 1);
        int zielY = Math.Clamp((int)MathF.Floor(end.Y / TileSize), 0, Height - 1);

        // Breitensuche vom Start in vier Richtungen über passierbare Kacheln;
        // dabei die besuchte Kachel mit dem kleinsten quadrierten Abstand zur
        // Zielkachel merken (bei Gleichstand gewinnt die zuerst gefundene).
        var visited = new bool[Width, Height];
        var queue = new Queue<(int x, int y)>();
        queue.Enqueue((startX, startY));
        visited[startX, startY] = true;
        var best = (x: startX, y: startY);
        int bestDist = (startX - zielX) * (startX - zielX) + (startY - zielY) * (startY - zielY);
        var dirs = new[] { (0, -1), (1, 0), (0, 1), (-1, 0) };
        while (queue.Count > 0)
        {
            var (cx, cy) = queue.Dequeue();
            foreach (var (dx, dy) in dirs)
            {
                int nx = cx + dx, ny = cy + dy;
                if (nx < 0 || ny < 0 || nx >= Width || ny >= Height) continue;
                if (visited[nx, ny]) continue;
                if (!_knownGrid.GetTile(nx, ny).IsPassable) continue;
                visited[nx, ny] = true;
                queue.Enqueue((nx, ny));
                int dist = (nx - zielX) * (nx - zielX) + (ny - zielY) * (ny - zielY);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    best = (nx, ny);
                }
            }
        }

        // Beste Kachel ist die Startkachel: kein Weg
        if (best.x == startX && best.y == startY)
            return path;

        var steps = CorePathfinding.FindPath(
            _knownGrid,
            new CorePosition(startX, startY),
            new CorePosition(best.x, best.y));

        if (steps == null || steps.Count == 0)
            return path;

        // Die erste Kachel ist die, auf der die Einheit bereits steht.
        for (int i = 1; i < steps.Count; i++)
            path.Add(GridToWorld(new Vector2(steps[i].X, steps[i].Y)));

        // Ist die beste Kachel genau die Zielkachel, den letzten Wegpunkt
        // durch end ersetzen, damit die Einheit genau dort ankommt.
        if (best.x == zielX && best.y == zielY && path.Count > 0)
            path[path.Count - 1] = end;

        return path;
    }

    /// <summary>
    /// Ob eine Figur geradeaus von <paramref name="from"/> nach <paramref name="to"/>
    /// gehen kann (Weltkoordinaten), ohne Wasser, Wald, Felsen oder Gebäude zu streifen.
    /// Damit laufen Dorfbewohner über freies Land gerade, statt dem Zickzack der
    /// Kachelmitten zu folgen.
    /// VERTRAG: die Strecke wird in gleichen Schritten von höchstens 4 Welteinheiten
    /// abgetastet, Anfang und Ende eingeschlossen; an jeder Stelle liegen der Punkt
    /// selbst und die beiden Punkte im Abstand <paramref name="clearance"/> quer zur
    /// Strecke (links und rechts) auf begehbaren Kacheln (IsWalkable von WorldToGrid).
    /// Punkte mit negativer Koordinate gelten als nicht begehbar - WorldToGrid schneidet
    /// dort zur Null hin ab und landete sonst auf Kachel 0. Sind from und to gleich,
    /// zählt nur dieser Punkt (ohne Querpunkte).
    /// Mit <paramref name="playerId"/> zählt statt IsWalkable, was dieser Spieler für
    /// begehbar halten darf (IsKnownWalkable) - für Laufbefehle, die nichts verraten sollen.
    /// </summary>
    public bool IsSegmentWalkable(Vector2 from, Vector2 to, float clearance, int? playerId = null)
    {
        // Ist playerId gesetzt, zählt, was dieser Spieler für begehbar halten darf;
        // sonst die echte Begehbarkeit.
        bool Frei(Vector2 p) => p.X >= 0 && p.Y >= 0 && (playerId is int pid
            ? IsKnownWalkable((int)(p.X / TileSize), (int)(p.Y / TileSize), pid)
            : IsWalkable((int)(p.X / TileSize), (int)(p.Y / TileSize)));

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

    /// <summary>Vorrat einer vollen Farm-Kachel (AoE II: jede Zeile ist eine 175-Nahrungsquelle).</summary>
    public const int FARM_FOOD = 175;
    /// <summary>Sekunden, bis eine geerntete Farm-Kachel wieder voll ist.</summary>
    public const float FARM_REGROW_SECONDS = 100f;

    /// <summary>
    /// Plantet ein 3×3-Getreidefeld auf der linken oberen Ecke (x, y). Jede
    /// Kachel ist eine unabhängige Nahrungskachel mit dem Vorrat <paramref name="food"/>
    /// (Standard FARM_FOOD, mit Pferdekummet mehr) und wächst nach Ernte über
    /// FARM_REGROW_SECONDS wieder nach. Die Kacheln bleiben begehbar
    /// — wie ein Beerenbusch —, nur <c>Buildable</c> wird gesperrt, damit nichts
    /// anderes darauf baut.
    /// </summary>
    public void PlantCrop(int x, int y, int size, int food = FARM_FOOD)
    {
        for (int bx = x; bx < x + size && bx < Width; bx++)
        {
            for (int by = y; by < y + size && by < Height; by++)
            {
                var t = GetTile(bx, by);
                if (t == null) continue;
                t.Farm = true;
                t.FarmRegrow = 0f;
                t.FarmCol = bx - x;
                t.FarmRow = by - y;
                t.FarmSize = size;
                t.ResourceType = AoE.Core.Entities.Resource.Food;
                t.FarmFood = food;
                t.ResourceAmount = food;
                t.Food = FoodSource.Farm;
                t.Walkable = true;
                t.Buildable = false;
            }
        }
    }

    /// <summary>
    /// Wie weit ein Schritt auf eine Kachel sie austritt: nach sechs Tritten
    /// (ROAD_WEAR_MIN) ist dort ein Trampelpfad.
    /// </summary>
    public const float WEAR_PER_STEP = 0.05f;

    /// <summary>
    /// So schnell vergehen Tritte, aus denen noch kein Pfad wurde: 1 in so vielen
    /// Sekunden, also die 0,3 bis zum Pfad in 270 s. Nur oft begangene Strecken
    /// werden so zum Pfad (G15) - ein Pfad selbst wächst nicht wieder zu.
    /// </summary>
    public const float WEAR_REGROW_SECONDS = 900f;

    /// <summary>
    /// Eine Figur betritt die Kachel (x, y) und tritt den Boden dort um
    /// <see cref="WEAR_PER_STEP"/> weiter aus, höchstens bis 1. Wasser und
    /// Kacheln außerhalb der Karte bleiben unberührt. Ab der Feudalzeit
    /// (CurrentRoadLevel &gt; 0) wird die Kachel Weg auf der erreichten Stufe,
    /// sobald sie mindestens ROAD_WEAR_MIN ausgetreten ist (C16) - nicht schon
    /// beim ersten Schritt.
    /// </summary>
    public void Trample(int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null || tile.Type == TileType.Water)
            return;
        tile.Wear = Math.Min(1f, tile.Wear + WEAR_PER_STEP);
        // Ab der Feudalzeit wird die Kachel Weg auf der erreichten Stufe,
        // sobald sie mindestens ROAD_WEAR_MIN ausgetreten ist — ein einzelner
        // Schritt macht keine Straße (C16).
        if (tile.Wear >= ROAD_WEAR_MIN - 0.001f)
            tile.RoadLevel = Math.Max(tile.RoadLevel, CurrentRoadLevel);
    }

    /// <summary>
    /// Ohne Verkehr vergehen Tritte, aus denen noch kein Pfad wurde (G15): man sieht
    /// sie nicht, und nur wer oft denselben Weg geht, tritt einen Pfad aus. Ein
    /// Trampelpfad (Tile.IsPath) bleibt dagegen für immer - Gras und Pfade ändern
    /// sich nicht mehr, wenn sie einmal da sind.
    /// VERTRAG: step = dt / WEAR_REGROW_SECONDS; jede Kachel mit Wear &gt; 0 und
    /// !tile.IsPath bekommt Wear = Math.Max(0f, Wear - step); Kacheln mit IsPath
    /// bleiben unverändert.
    /// </summary>
    public void RegrowGrass(float dt)
    {
        float step = dt / WEAR_REGROW_SECONDS;
        // Tritte, aus denen noch kein Pfad wurde, vergehen wieder; ein Pfad bleibt für immer (G15).
        foreach (var tile in tiles)
        {
            if (tile.Wear > 0f && !tile.IsPath)
                tile.Wear = Math.Max(0f, tile.Wear - step);
        }
    }

    /// <summary>
    /// Lässt geerntete Farm-Kacheln nachwachsen: jede Kachel mit <c>Farm ==
    /// true</c> und leerem Vorrat zählt <see cref="Tile.FarmRegrow"/> herunter;
    /// bei Null füllt sich die Kachel wieder auf 175. Eine gerade geerntete
    /// Kachel (Regrow = 0, Vorrat 0) startet den Countdown hier.
    /// </summary>
    public void RegrowCrop(float dt)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Width && y < Height; y++)
            {
                var t = GetTile(x, y);
                if (t == null || !t.Farm) continue;
                bool full = t.ResourceType == AoE.Core.Entities.Resource.Food && t.ResourceAmount > 0;
                if (full) continue;
                // Kachel geerntet, noch nicht gestartet: Countdown starten
                if (t.FarmRegrow <= 0f) t.FarmRegrow = FARM_REGROW_SECONDS;
                t.FarmRegrow -= dt;
                if (t.FarmRegrow <= 0f)
                {
                    t.ResourceType = AoE.Core.Entities.Resource.Food;
                    // Die Kachel wächst bis zu ihrem eigenen Vorrat nach (mit Pferdekummet größer), sonst Standard
                    t.ResourceAmount = t.FarmFood > 0 ? t.FarmFood : FARM_FOOD;
                    t.FarmRegrow = 0f;   // voll, Regrow zurückgesetzt (wird beim nächsten Erntetakt neu gesetzt)
                }
            }
        }
    }

    // -----------------------------------------------------------------
    // Wege (C12): Trampelpfade rücken bei jedem Zeitaufstieg auf
    // -----------------------------------------------------------------
    // Ein Trampelpfad entsteht durch Tritte (Wear). Bei einem Aufstieg wird
    // jeder vorhandene Pfad (Wear > 0) oder jede frühere Stufe zum neuen
    // Niveau hochgezogen; neu ausgetretene Pfade entstehen direkt auf der
    // Stufe, die bis dahin erreicht war. Die Stufen: Feudal 1 (Pflasterstraße
    // aus Stein und Zement), Ritter 2 (Erde, Schlamm und Kies), Imperial 3
    // (verdichteter Schotter). Auf Stufe ≥ 1 laufen Einheiten 20 % schneller
    // als auf der Wiese (Spielbildschirm).

    /// <summary>
    /// Weg-Stufe, die mit einem Zeitalter einhergeht: Dunkel 0, Feudal 1,
    /// Ritter 2, Imperial 3.
    /// </summary>
    public static int LevelOfAge(Age age) => age switch
    {
        Age.Feudal => 1,
        Age.Castle => 2,
        Age.Imperial => 3,
        _ => 0
    };

    /// <summary>
    /// Die bislang erreichte Weg-Stufe (Dunkelzeit 0). Der Bildschirm setzt
    /// sie bei jedem Zeitaufstieg auf das neue Niveau.
    /// </summary>
    public int CurrentRoadLevel { get; set; }

    /// <summary>
    /// So ausgetreten muss eine Kachel sein, damit aus dem Trampelpfad ein Weg wird (C16): sechs
    /// Tritte (WEAR_PER_STEP 0,05). Ein einzelner Schritt quer über die Wiese macht keine Straße -
    /// vorher wurde jede einmal betretene Kachel Weg, und um das Dorf entstanden Flächen statt Wege.
    /// Vergleiche mit ROAD_WEAR_MIN - 0.001f: sechs Schritte zu 0,05f ergeben in float nicht genau 0,3.
    /// </summary>
    public const float ROAD_WEAR_MIN = 0.3f;

    /// <summary>
    /// Zeitaufstieg: alle vorhandenen Trampelpfade (Wear mindestens ROAD_WEAR_MIN, C16)
    /// und alle früheren Wege steigen auf <paramref name="level"/> auf,
    /// falls sie darunter liegen — es fällt nichts ab. VERTRAG: außerhalb der
    /// Karte nichts; <paramref name="level"/> 0..3.
    /// </summary>
    public void UpgradeRoads(int level)
    {
        if (level <= 0)
            return;
        CurrentRoadLevel = Math.Max(CurrentRoadLevel, level);
        foreach (var tile in tiles)
        {
            if ((tile.Wear >= ROAD_WEAR_MIN - 0.001f || tile.RoadLevel > 0) && tile.RoadLevel < level)
                tile.RoadLevel = level;
        }
    }

    // -----------------------------------------------------------------
    // Wege von Gebäuden (C16, C16c): ab der Feudalzeit führt von jedem fertigen
    // Gebäude ein Weg bis an das Stadtzentrum seines Besitzers
    // -----------------------------------------------------------------

    /// <summary>
    /// Kosten einer Kachel, die für den Weg eines Gebäudes neu Weg werden müsste (C16c); eine
    /// Kachel, die schon Weg ist, kostet 1. Ein vorhandener Weg wird deshalb mitbenutzt, solange
    /// er höchstens dreimal so lang ist wie die neuen Kacheln, die er erspart - so bündeln sich
    /// die Wege zu Straßen ins Dorfzentrum, statt dicht nebeneinander zu laufen.
    /// </summary>
    public const int ROAD_NEW_TILE_COST = 3;

    /// <summary>
    /// Ob auf der Kachel (x, y) ein Weg angelegt werden darf (C16): Wiese oder Sand,
    /// begehbar, ohne Gebäude, ohne Rohstoff und kein Feld.
    /// VERTRAG: true genau dann, wenn GetTile(x, y) nicht null ist, ihr Type
    /// TileType.Grassland oder TileType.Sand ist, IsWalkable(x, y) gilt, ihr
    /// ResourceType null ist und Farm false - sonst false (auch außerhalb der Karte).
    /// </summary>
    public bool CanCarryRoad(int x, int y)
    {
        var tile = GetTile(x, y);
        if (tile == null)
            return false;
        if (tile.Type != TileType.Grassland && tile.Type != TileType.Sand)
            return false;
        if (!IsWalkable(x, y))
            return false;
        if (tile.ResourceType != null)
            return false;
        if (tile.Farm)
            return false;
        return true;
    }

    /// <summary>
    /// Legt einen Weg von der Vorderseite des Gebäudes <paramref name="building"/> bis an ein
    /// Stadtzentrum seines Besitzers (C16c) und gibt zurück, wie viele Kacheln dabei Weg geworden
    /// oder aufgestiegen sind. Eine Höchstlänge gibt es nicht: auch ein Lager weit draußen bekommt
    /// seinen Weg, solange einer möglich ist. Vorhandene Wege und Trampelpfade werden mitbenutzt,
    /// wo sie sich lohnen (ROAD_NEW_TILE_COST); ein Weg, der nicht zum Stadtzentrum führt, ist
    /// aber kein Ende mehr. Neue Kacheln laufen schräg auf das Stadtzentrum zu statt im Winkel.
    /// VERTRAG:
    /// - level &lt;= 0 (Dunkle Zeit) oder building.Core.BuildingType ==
    ///   AoE.Core.Entities.BuildingType.TownCenter: nichts ändern, Rückgabe 0.
    /// - Zentren: alle ANDEREN Gebäude aus Buildings mit demselben OwnerId wie building und
    ///   Core.BuildingType == AoE.Core.Entities.BuildingType.TownCenter. Keins: nichts ändern,
    ///   Rückgabe 0.
    /// - Ring eines Gebäudes g: alle Kacheln mit x von g.X - 1 bis g.X + g.Width und y von
    ///   g.Y - 1 bis g.Y + g.Height, ohne die Grundfläche (x von g.X bis g.X + g.Width - 1 und
    ///   zugleich y von g.Y bis g.Y + g.Height - 1).
    /// - Startkacheln: die Reihe direkt unter der Grundfläche (y = building.Y + building.Height,
    ///   x von building.X bis building.X + building.Width - 1, aufsteigend), soweit CanCarryRoad.
    ///   Ist dort keine frei, alle Kacheln aus dem Ring von building (x außen aufsteigend, darin
    ///   y aufsteigend), soweit CanCarryRoad. Gibt es auch dort keine: nichts ändern, Rückgabe 0.
    /// - Kosten(x, y): 1, wenn die Kachel RoadLevel &gt;= 1 hat, sonst ROAD_NEW_TILE_COST.
    /// - Abstand dist(x, y), kürzeste Kosten zum Ring eines Zentrums (Dijkstra mit
    ///   PriorityQueue, dist als int[Width, Height], unerreicht int.MaxValue): jede Kachel aus dem
    ///   Ring eines Zentrums mit CanCarryRoad hat dist = Kosten(x, y); jede andere Kachel mit
    ///   CanCarryRoad hat dist = das kleinste dist ihrer vier Nachbarn + Kosten(x, y); Kacheln ohne
    ///   CanCarryRoad bleiben unerreicht. Die Suche darf enden, sobald eine Kachel aus der
    ///   Warteschlange kommt, deren dist größer ist als das einer schon herausgenommenen
    ///   Startkachel. Ist keine Startkachel erreicht: nichts ändern, Rückgabe 0.
    /// - Nähe(x, y): Quadrat des Abstands von (x + 0.5f, y + 0.5f) zur Mitte
    ///   (X + Width / 2f, Y + Height / 2f) des Zentrums, dessen Mitte der Mitte von building am
    ///   nächsten liegt (bei Gleichstand das erste in Buildings).
    /// - Erste Kachel des Wegs: unter den Startkacheln mit dem kleinsten dist die mit der kleinsten
    ///   Nähe, bei Gleichstand die erste in der Reihenfolge der Startkacheln.
    /// - Weg: von der ersten Kachel aus, solange dist(aktuell) &gt; Kosten(aktuell), weiter zu dem
    ///   Nachbarn (rechts x + 1, links x - 1, unten y + 1, oben y - 1) mit CanCarryRoad und
    ///   dist == dist(aktuell) - Kosten(aktuell); gibt es mehrere, der mit der kleinsten Nähe, bei
    ///   Gleichstand der erste in dieser Reihenfolge. Die Kachel mit dist == Kosten liegt am Ring
    ///   eines Zentrums und ist die letzte. Erst den ganzen Weg bestimmen, dann ändern: Kosten und
    ///   dist gelten für den Stand vor der ersten Änderung.
    /// - Jede Kachel des Wegs bekommt RoadLevel = Math.Max(RoadLevel, level). Rückgabe: die Zahl
    ///   der Kacheln, deren RoadLevel sich dabei geändert hat.
    /// </summary>
    public int ConnectBuilding(Building building, int level)
    {
        // (a) Dunkle Zeit oder das Gebäude selbst ein Stadtzentrum: nichts tun.
        if (level <= 0 || building.Core.BuildingType == AoE.Core.Entities.BuildingType.TownCenter)
            return 0;

        // Zentren des Besitzers (ohne das Gebäude selbst).
        var centers = Buildings
            .Where(g => g != building && g.OwnerId == building.OwnerId
                && g.Core.BuildingType == AoE.Core.Entities.BuildingType.TownCenter)
            .ToList();
        if (centers.Count == 0)
            return 0;

        // (b) Ring eines Gebäudes: der Rahmen um die Grundfläche.
        bool InRing(Building g, int x, int y)
        {
            bool inX = x >= g.X - 1 && x <= g.X + g.Width;
            bool inY = y >= g.Y - 1 && y <= g.Y + g.Height;
            bool inFootprint = x >= g.X && x <= g.X + g.Width - 1
                && y >= g.Y && y <= g.Y + g.Height - 1;
            return inX && inY && !inFootprint;
        }

        // (c) Startkacheln: erst die Reihe unter der Grundfläche, sonst der Ring.
        var starts = new List<(int X, int Y)>();
        for (int x = building.X; x < building.X + building.Width; x++)
        {
            int y = building.Y + building.Height;
            if (CanCarryRoad(x, y))
                starts.Add((x, y));
        }
        if (starts.Count == 0)
        {
            for (int x = building.X - 1; x <= building.X + building.Width; x++)
            {
                for (int y = building.Y - 1; y <= building.Y + building.Height; y++)
                {
                    if (!InRing(building, x, y)) continue;
                    if (CanCarryRoad(x, y))
                        starts.Add((x, y));
                }
            }
        }
        if (starts.Count == 0)
            return 0;

        // (d) Kosten einer Kachel: vorhandener Weg ist billig, neue Kachel teuer.
        int Cost(int x, int y)
        {
            var tile = GetTile(x, y);
            return tile != null && tile.RoadLevel >= 1 ? 1 : ROAD_NEW_TILE_COST;
        }

        // (e) Dijkstra: kürzeste Kosten vom Ring eines Zentrums aus.
        var dist = new int[Width, Height];
        for (int x = 0; x < Width; x++)
            for (int y = 0; y < Height; y++)
                dist[x, y] = int.MaxValue;

        var queue = new PriorityQueue<(int X, int Y), int>();
        foreach (var center in centers)
        {
            for (int x = center.X - 1; x <= center.X + center.Width; x++)
            {
                for (int y = center.Y - 1; y <= center.Y + center.Height; y++)
                {
                    if (!InRing(center, x, y)) continue;
                    if (!CanCarryRoad(x, y)) continue;
                    if (dist[x, y] != int.MaxValue) continue;
                    dist[x, y] = Cost(x, y);
                    queue.Enqueue((x, y), dist[x, y]);
                }
            }
        }

        var startSet = new HashSet<(int X, int Y)>(starts);
        int best = int.MaxValue;
        while (queue.TryDequeue(out var p, out int d))
        {
            // Veralteter Eintrag: überspringen.
            if (d != dist[p.X, p.Y])
                continue;
            // Besser als jede erreichte Startkachel: abbrechen.
            if (d > best)
                break;
            if (startSet.Contains(p))
                best = d;
            // Vier Nachbarn.
            foreach (var (nx, ny) in new[] { (p.X + 1, p.Y), (p.X - 1, p.Y), (p.X, p.Y + 1), (p.X, p.Y - 1) })
            {
                if (!CanCarryRoad(nx, ny)) continue;
                int nd = d + Cost(nx, ny);
                if (nd < dist[nx, ny])
                {
                    dist[nx, ny] = nd;
                    queue.Enqueue((nx, ny), nd);
                }
            }
        }

        // (f) Keine Startkachel erreicht: nichts ändern.
        if (best == int.MaxValue)
            return 0;

        // (g) Das Zentrum, dessen Mitte der Mitte von building am nächsten liegt.
        var target = centers
            .OrderBy(c => (c.X + c.Width / 2f - (building.X + building.Width / 2f)) * (c.X + c.Width / 2f - (building.X + building.Width / 2f))
                        + (c.Y + c.Height / 2f - (building.Y + building.Height / 2f)) * (c.Y + c.Height / 2f - (building.Y + building.Height / 2f)))
            .First();

        // Nähe einer Kachel zur Mitte des Zentrums (quadratischer Abstand).
        float Near(int x, int y)
        {
            float dx = x + 0.5f - (target.X + target.Width / 2f);
            float dy = y + 0.5f - (target.Y + target.Height / 2f);
            return dx * dx + dy * dy;
        }

        // (h) Den Weg bestimmen: von der besten Startkachel aus dem dist-Gradienten folgen.
        var path = new List<(int X, int Y)>();
        var current = starts.Where(s => dist[s.X, s.Y] == best).OrderBy(s => Near(s.X, s.Y)).First();
        path.Add(current);
        while (dist[current.X, current.Y] > Cost(current.X, current.Y))
        {
            int rest = dist[current.X, current.Y] - Cost(current.X, current.Y);
            var next = new[] { (current.X + 1, current.Y), (current.X - 1, current.Y), (current.X, current.Y + 1), (current.X, current.Y - 1) }
                .Where(n => CanCarryRoad(n.Item1, n.Item2) && dist[n.Item1, n.Item2] == rest)
                .OrderBy(n => Near(n.Item1, n.Item2))
                .First();
            current = next;
            path.Add(current);
        }

        // (i) Erst jetzt ändern: jede Kachel des Wegs auf mindestens level heben.
        int changed = 0;
        foreach (var (x, y) in path)
        {
            var tile = GetTile(x, y);
            if (tile == null) continue;
            int neu = Math.Max(tile.RoadLevel, level);
            if (neu != tile.RoadLevel)
            {
                tile.RoadLevel = neu;
                changed++;
            }
        }
        return changed;
    }

    // -----------------------------------------------------------------
    // Schaf- und Reh-Wanderung (Wild)
    // -----------------------------------------------------------------
    // Schafe (FoodSource.Sheep, 100 Nahrung) und Rehe (FoodSource.Deer,
    // 150 Nahrung) sind Nahrungskacheln, die über die Wiese wandern. Jede
    // Tierart hat ihre eigene Koordinatenliste, jedes Tier seinen eigenen
    // Takt (WildAnimal auf der Kachel); beide Arten teilen
    // sich die Reservierung _claimed — ist eine Kachel reserviert (ein
    // Dorfbewohner erntet bzw. jagt gerade), bleibt das Tier stehen, damit
    // der Dorfbewohner es nicht im Stich lässt.
    private readonly List<(int x, int y)> _sheepCoords = new List<(int, int)>();
    private readonly List<(int x, int y)> _deerCoords = new List<(int, int)>();
    private readonly List<(int x, int y)> _rabbitCoords = new List<(int, int)>();
    private readonly List<(int x, int y)> _boarCoords = new List<(int, int)>();
    private readonly HashSet<(int x, int y)> _claimed = new HashSet<(int, int)>();
    private readonly Random _wildRng = new Random();

    /// <summary>Sekunden, die ein Tier für einen Schritt auf die Nachbarkachel braucht.</summary>
    public const float WILD_STEP_SECONDS = 1.0f;

    /// <summary>
    /// Ein frisch aufgestelltes Tier: zufälliges Aussehen und Blickrichtung, der
    /// erste Schritt zu einer zufälligen Zeit im Takt - sonst zöge die ganze
    /// Herde im Gleichschritt los.
    /// </summary>
    private WildAnimal NewAnimal(float maxSeconds) => new WildAnimal
    {
        Look = _wildRng.Next(int.MaxValue),
        FacingLeft = _wildRng.Next(2) == 0,
        WanderTimer = _RandomRange(_wildRng, 0f, maxSeconds),
    };

    /// <summary>Anzahl der Schafe auf dieser Karte (auch reservierte).</summary>
    public int SheepCount => _sheepCoords.Count;

    /// <summary>Anzahl der Rehe auf dieser Karte (auch reservierte).</summary>
    public int DeerCount => _deerCoords.Count;

    /// <summary>Anzahl der Kaninchen und der Wildschweine auf dieser Karte.</summary>
    public int RabbitCount => _rabbitCoords.Count;
    public int BoarCount => _boarCoords.Count;

    /// <summary>Ob <c>(x, y)</c> eine reservierte Wild-Kachel ist.</summary>
    public bool IsClaimed(int x, int y) => _claimed.Contains((x, y));

    /// <summary>
    /// Synchronisiert die Reservierungen mit der Realität: <paramref name="aktiv"/>
    /// sind die Schaf- und Reh-Kacheln, die gerade ein Dorfbewohner erntet bzw.
    /// jagt (Phase Gathering). Genau die bleiben stehen; alle anderen
    /// Reservierungen verfallen, damit die freilaufenden Tiere wieder wandern
    /// dürfen.
    /// </summary>
    public void SyncSheepClaims(HashSet<(int x, int y)> aktiv)
    {
        foreach (var p in _claimed)
            if (!aktiv.Contains(p))
                _claimed.Remove(p);
        foreach (var p in aktiv)
            _claimed.Add(p);
    }

    /// <summary>
    /// Schlachtet das Schaf bzw. Reh auf (x, y): ein Dorfbewohner hat angefangen,
    /// es abzubauen. Es bleibt sofort stehen, auch mitten im Schritt, und wandert
    /// nie mehr weiter - auch nicht, wenn der Dorfbewohner abliefert oder
    /// abgezogen wird.
    /// </summary>
    public void Slaughter(int x, int y)
    {
        var t = GetTile(x, y);
        if (t == null || !t.Food.IsWild()) return;
        t.Animal ??= new WildAnimal();
        t.Animal.Slaughtered = true;
        t.Animal.Glide = 0f;
    }

    /// <summary>
    /// Taktet die Schaf-Wanderung. Aufruf: jedes Frame mit
    /// <c>gameTime.ElapsedGameTime.TotalSeconds</c>.
    /// </summary>
    public void UpdateSheep(float dt)
    {
        UpdateWild(_sheepCoords, dt, Settings.SheepWanderSecondsMin, Settings.SheepWanderSecondsMax,
                   Settings.SheepWanderRadius, FoodSource.Sheep);
    }

    /// <summary>
    /// Taktet die Reh-Wanderung — eigene Herde, eigener Takt.
    /// </summary>
    public void UpdateDeer(float dt)
    {
        UpdateWild(_deerCoords, dt, Settings.DeerWanderSecondsMin, Settings.DeerWanderSecondsMax,
                   Settings.DeerWanderRadius, FoodSource.Deer);
    }

    /// <summary>Taktet das Hoppeln der Kaninchen - flink, kurzer Takt.</summary>
    public void UpdateRabbits(float dt)
    {
        UpdateWild(_rabbitCoords, dt, Settings.RabbitWanderSecondsMin, Settings.RabbitWanderSecondsMax,
                   Settings.RabbitWanderRadius, FoodSource.Rabbit);
    }

    /// <summary>Taktet die Wanderung der Wildschweine - gemächlich, langer Takt.</summary>
    public void UpdateBoars(float dt)
    {
        UpdateWild(_boarCoords, dt, Settings.BoarWanderSecondsMin, Settings.BoarWanderSecondsMax,
                   Settings.BoarWanderRadius, FoodSource.Boar);
    }

    /// <summary>
    /// Der gemeinsame Wandertakt: jedes freie Tier hat seinen eigenen Zähler
    /// und macht, wenn er abgelaufen und der letzte Schritt beendet ist, einen
    /// Schritt auf eine freie Nachbarwiese; danach wartet es wieder min bis max
    /// Sekunden. Reservierte Tiere (ein Dorfbewohner erntet bzw. jagt sie
    /// gerade) bleiben stehen; ein laufender Schritt endet trotzdem.
    /// </summary>
    private void UpdateWild(List<(int x, int y)> coords, float dt, float min, float max, int radius,
                            FoodSource source)
    {
        coords.RemoveAll(c => GetTile(c.x, c.y)?.Food != source);
        for (int i = 0; i < coords.Count; i++)
        {
            var (sx, sy) = coords[i];
            var t = GetTile(sx, sy);
            t.Animal ??= NewAnimal(max);
            var tier = t.Animal;
            tier.Glide = Math.Max(0f, tier.Glide - dt);
            if (tier.Slaughtered || _claimed.Contains((sx, sy)) || t.ResourceAmount <= 0)
                continue;   // geschlachtet, reserviert bzw. leer: bleibt stehen
            tier.WanderTimer -= dt;
            if (tier.WanderTimer > 0f || tier.Glide > 0f)
                continue;
            tier.WanderTimer = _RandomRange(_wildRng, min, max);
            if (WanderStep(t, radius, source) is { } ziel)
                coords[i] = ziel;
        }
    }

    /// <summary>
    /// Ein Schritt eines Tiers auf eine freie Wiesen-Kachel im Umkreis radius:
    /// Nahrung und Tier ziehen um, die alte Kachel wird Wiese. Das Tier blickt
    /// in Schrittrichtung und merkt sich, woher es kam (FromX, FromY, Glide).
    /// Gibt die neue Kachel zurück, oder null, wenn kein Ziel frei war.
    /// VERTRAG (zusätzlich): die ersten acht Versuche verwerfen Schritte ohne
    /// Seitenanteil (dx == 0) - das Tier zeigt sich nur von der Seite und ginge sonst
    /// seitwärts gewandt nach oben oder unten; erst danach sind sie erlaubt.
    /// </summary>
    private (int x, int y)? WanderStep(Tile t, int radius, FoodSource source)
    {
        for (int attempt = 0; attempt < 12; attempt++)
        {
            int dx = _wildRng.Next(-radius, radius + 1);
            int dy = _wildRng.Next(-radius, radius + 1);
            if (dx == 0 && dy == 0) continue;
            // Die ersten acht Versuche verwerfen Schritte ohne Seitenanteil
            // (dx == 0): das Tier zeigt sich nur von der Seite und ginge sonst
            // seitwärts gewandt nach oben oder unten; erst danach sind sie erlaubt.
            if (dx == 0 && attempt < 8) continue;
            int nx = t.X + dx, ny = t.Y + dy;
            var target = GetTile(nx, ny);
            if (target == null) continue;
            if (target.Type != TileType.Grassland) continue;
            if (!string.IsNullOrEmpty(target.Building)) continue;
            if (target.Farm) continue;
            if (target.ResourceType != null && target.ResourceAmount > 0) continue;
            if (target.Food != FoodSource.None) continue;   // kein anderes Tier dort

            var tier = t.Animal;
            tier.FromX = -dx;
            tier.FromY = -dy;
            tier.Glide = WILD_STEP_SECONDS;
            if (dx != 0) tier.FacingLeft = dx < 0;
            target.Food = source;
            target.ResourceType = Resource.Food;
            target.ResourceAmount = t.ResourceAmount;
            target.Animal = tier;
            t.Food = FoodSource.None;
            t.ResourceType = null;
            t.ResourceAmount = 0;
            t.Animal = null;
            return (nx, ny);
        }
        return null;
    }

    /// <summary>
    /// Entfernt ein Wild endgültig — es wurde leer gesammelt bzw. gejagt.
    /// Wird von <see cref="ClearResource"/> aufgerufen, wenn die Kachel leer
    /// ist und Schaf oder Reh trägt.
    /// </summary>
    public void RemoveSheep(int x, int y)
    {
        _sheepCoords.Remove((x, y));
        _deerCoords.Remove((x, y));
        _rabbitCoords.Remove((x, y));
        _boarCoords.Remove((x, y));
    }

    /// <summary>Zufallszahl in [min, max) mit gegebenem Generator.</summary>
    internal static float _RandomRange(Random rng, float min, float max)
        => min + (float)(rng.NextDouble() * (max - min));
    
    public Vector2 WorldToGrid(Vector2 worldPosition)
    {
        return new Vector2(
            (int)(worldPosition.X / TileSize),
            (int)(worldPosition.Y / TileSize)
        );
    }
    
    public Vector2 GridToWorld(Vector2 gridPosition)
    {
        return new Vector2(
            gridPosition.X * TileSize + TileSize / 2,
            gridPosition.Y * TileSize + TileSize / 2
        );
    }
    
    // --- Navigation ------------------------------------------------------
    // Die Wegfindung liegt in AoE.Core (A* mit Manhattan-Heuristik). Hier
    // wird nur die Begehbarkeit der Spielkarte in ein MapGrid gespiegelt.

    private MapGrid _navGrid;
    private bool _navDirty = true;

    /// <summary>
    /// Navigationsgitter für die Kernbibliothek. Wird nur neu aufgebaut,
    /// wenn sich die Begehbarkeit geändert hat — bei 100×100 Kacheln lohnt
    /// es sich, das nicht bei jedem Rechtsklick zu tun.
    /// </summary>
    private MapGrid NavGrid
    {
        get
        {
            _navGrid ??= new MapGrid(Width, Height);
            if (_navDirty)
            {
                // Nur die Begehbarkeit auffrischen. Das Gitter selbst bleibt
                // bestehen, weil daran die Sichtbarkeitszustände und die
                // angemeldeten Einheiten hängen.
                for (int x = 0; x < Width; x++)
                {
                    for (int y = 0; y < Height; y++)
                    {
                        var navTile = _navGrid.GetTile(x, y);
                        if (navTile != null)
                        {
                            // Wasser und jede besetzte (Gebäude-)Kachel sind
                            // für die Wegsuche gesperrt. Die reine Begehbar-
                            // keits-Prüfung <see cref="IsWalkable"/> bleibt
                            // für Steh- und Arbeitbarkeits-Entscheidungen
                            // (Fisch vom Ufer, Bau am Rand) — die Wegsuche
                            // selbst braucht das härtere Kriterium.
                            bool besetzt = GetTile(x, y)?.Building != null;
                            navTile.IsPassable = IsWalkable(x, y) && !besetzt;
                        }
                    }
                }
                _navDirty = false;
            }
            return _navGrid;
        }
    }

    /// <summary>
    /// Meldet, dass sich die Begehbarkeit geändert hat (etwa durch ein
    /// neu gesetztes Gebäude). Der nächste Wegfindungsaufruf baut neu auf.
    /// </summary>
    public void InvalidateNavigation() => _navDirty = true;

    /// <summary>
    /// Sucht einen begehbaren Weg von <paramref name="start"/> nach
    /// <paramref name="end"/> und gibt ihn als Weltkoordinaten zurück.
    /// Ist das Ziel nicht erreichbar, kommt eine leere Liste zurück —
    /// die Einheit bleibt dann stehen, statt in ein Hindernis zu laufen.
    /// </summary>
    public List<Vector2> FindPath(Vector2 start, Vector2 end)
    {
        var path = new List<Vector2>();

        var gridStart = WorldToGrid(start);
        var gridEnd = WorldToGrid(end);

        if (!IsWalkable((int)gridEnd.X, (int)gridEnd.Y))
            return path;

        var steps = CorePathfinding.FindPath(
            NavGrid,
            new CorePosition((int)gridStart.X, (int)gridStart.Y),
            new CorePosition((int)gridEnd.X, (int)gridEnd.Y));

        if (steps == null || steps.Count == 0)
            return path;

        // Die erste Kachel ist die, auf der die Einheit bereits steht.
        for (int i = 1; i < steps.Count; i++)
            path.Add(GridToWorld(new Vector2(steps[i].X, steps[i].Y)));

        // Letzten Wegpunkt auf das genaue Ziel legen, damit die Einheit
        // nicht nur bis zur Kachelmitte läuft.
        if (path.Count > 0)
            path[path.Count - 1] = end;

        return path;
    }
    
    private int RandomInt(int min, int max)
    {
        if (max <= min) return min;
        return _random.Next(min, max);
    }
    
    // --- Sichtbarkeit ----------------------------------------------------
    // Der Nebel des Krieges kommt aus AoE.Core.Map.VisibilitySystem. Das
    // liefert die drei Zustände der Spezifikation: unerforscht, erforscht
    // (letzter bekannter Stand) und aktuell sichtbar.

    private CoreVisibility _visibility;
    private readonly List<UnitEntity> _registeredUnits = new List<UnitEntity>();

    /// <summary>
    /// Rechnet die Sicht eines Spielers neu. Die Einheiten werden dafür an
    /// der Karte angemeldet und ihre Kachelposition nachgezogen — die
    /// Sichtrechnung arbeitet auf Gitter-, nicht auf Weltkoordinaten.
    /// </summary>
    public void UpdateFogOfWarForPlayer(int playerId, List<Unit> units)
    {
        var grid = NavGrid;
        _visibility ??= new CoreVisibility(grid);

        foreach (var stale in _registeredUnits)
            grid.RemoveUnit(stale);
        _registeredUnits.Clear();

        foreach (var unit in units)
        {
            if (unit?.Core == null)
                continue;

            var cell = WorldToGrid(unit.Position);
            unit.Core.Position = new CorePosition((int)cell.X, (int)cell.Y);
            unit.Core.OwnerId = unit.OwnerId;

            grid.AddUnit(unit.Core);
            _registeredUnits.Add(unit.Core);
        }

        // Baustellen sehen noch nichts. AoE.Core kennt die Baustelle selbst nicht,
        // nur ob das Gebäude im Bau ist - das wird vor jeder Sichtrechnung abgeglichen
        foreach (var building in Buildings)
            building.Core.IsUnderConstruction = !building.IsComplete;

        _visibility.UpdateVisibility(playerId);
    }

    /// <summary>
    /// Was der Spieler schon von der Karte kennt, für einen Spielstand.
    /// VERTRAG: eine Zeichenkette der Länge Width · Height; das Zeichen an Index y · Width + x ist
    /// '1', wenn NavGrid.GetTile(x, y).GetVisibility(playerId) nicht TileVisibility.Unexplored
    /// ist (erkundet oder gerade sichtbar), sonst '0'. Unabhängig von <see cref="TestNoFog"/>.
    /// Ändert nichts.
    /// </summary>
    public string ExploredMask(int playerId)
    {
        var mask = new char[Width * Height];
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // '1' erkundet oder gerade sichtbar, '0' unerforscht.
                mask[y * Width + x] = NavGrid.GetTile(x, y).GetVisibility(playerId) != TileVisibility.Unexplored
                    ? '1'
                    : '0';
            }
        }
        return new string(mask);
    }

    /// <summary>
    /// Übernimmt eine Maske aus <see cref="ExploredMask"/> (Spielstand laden).
    /// VERTRAG: für jede Kachel setzt NavGrid.GetTile(x, y).SetVisibility(playerId, …) den
    /// Zustand TileVisibility.Explored, wenn das Zeichen an Index y · Width + x '1' ist, sonst
    /// TileVisibility.Unexplored. Fehlt das Zeichen (Maske zu kurz oder null), gilt '0'. Was
    /// gerade sichtbar ist, rechnet der nächste <see cref="UpdateFogOfWarForPlayer"/>.
    /// </summary>
    public void RestoreExplored(int playerId, string mask)
    {
        for (int x = 0; x < Width; x++)
        {
            for (int y = 0; y < Height; y++)
            {
                // Fehlt das Zeichen (Maske zu kurz oder null), gilt '0'.
                int index = y * Width + x;
                bool explored = mask != null && index < mask.Length && mask[index] == '1';
                NavGrid.GetTile(x, y).SetVisibility(playerId,
                    explored ? TileVisibility.Explored : TileVisibility.Unexplored);
            }
        }
    }

    /// <summary>Kachel ist gerade einsehbar.</summary>
    public bool IsTileVisible(int x, int y, int playerId = 0)
        => _testNoFog || NavGrid.GetTile(x, y)?.GetVisibility(playerId) == TileVisibility.Visible;

    /// <summary>
    /// Kachel wurde schon einmal gesehen. Schließt den Zustand
    /// „erforscht, aber veraltet" mit ein — der wird abgedunkelt gezeichnet.
    /// </summary>
    public bool IsTileExplored(int x, int y, int playerId = 0)
        => _testNoFog || (NavGrid.GetTile(x, y)?.GetVisibility(playerId)
            ?? TileVisibility.Unexplored) != TileVisibility.Unexplored;

    /// <summary>
    /// Die Erkundungs-Front des Spielers <paramref name="playerId"/>:
    /// je Hauptrichtung (nord, nordost, ost, …) eine Kachel direkt am Rand
    /// des bereits Erkundeten, aber noch außerhalb davon. Die KI schickt
    /// Einheiten dorthin — jeder Schritt über eine unerkundete Kachel deckt
    /// mehr auf.
    ///
    /// Rückgabe: bis zu 8 Kacheln (je Richtung). Leere Liste, wenn der
    /// Spieler noch nichts gesehen hat oder jede Richtung schon am
    /// Kartenrand ist.
    /// </summary>
    public IReadOnlyList<(int X, int Y)> FrontierTargets(int playerId, (int X, int Y) home)
    {
        if (_testNoFog) return Array.Empty<(int, int)>();
        var targets = new List<(int X, int Y)>();
        foreach (var (dx, dy) in new[] { (0,-1), (1,-1), (1,0), (1,1),
                                         (0,1), (-1,1), (-1,0), (-1,-1) })
        {
            // Schritt für Schritt weg von home, solange die Kachel noch
            // erkundet ist. Die letzte erkundete Kachel ist der Rand;
            // das Ziel ist die Kachel direkt dahinter.
            int sx = home.X + dx, sy = home.Y + dy;
            (int X, int Y) frontier = (home.X, home.Y);
            while (true)
            {
                if (sx < 0 || sy < 0 || sx >= Width || sy >= Height) break;
                if (!IsTileExplored(sx, sy, playerId)) break;
                frontier = (sx, sy);
                sx += dx; sy += dy;
            }
            int tx = frontier.X + dx, ty = frontier.Y + dy;
            if (tx < 0 || ty < 0 || tx >= Width || ty >= Height) continue;
            if (tx == home.X && ty == home.Y) continue;
            if (!IsTileExplored(tx, ty, playerId)) targets.Add((tx, ty));
        }
        return targets;
    }

    /// <summary>
    /// Test-Modus (CLI --test): der Nebel des Krieges ist vollständig
    /// aufgehoben — alle Kacheln gelten als sichtbar. Dadurch bleiben auch
    /// fremde Einheiten sichtbar, und die Minimap zeigt die ganze Karte mitsamt
    /// den gegnerischen Einheiten und Gebäuden. Standard: aus.
    /// </summary>
    public static bool TestNoFog = false;
    private static bool _testNoFog => TestNoFog;
}

/// <summary>
/// Represents a building in the game
/// </summary>
public class Building
{
    public int X { get; set; }
    public int Y { get; set; }
    public string Type { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int OwnerId { get; set; }

    /// <summary>
    /// Das zugehörige Gebäude aus AoE.Core, wie <see cref="Unit.Core"/> bei
    /// Einheiten. Lebenspunkte, Sichtweite und Abgabestelle kommen von dort —
    /// vorher führte das Spiel eigene Werte (pauschal 1000 Lebenspunkte).
    /// </summary>
    public BuildingEntity Core { get; }

    /// <summary>
    /// Stabile Identität der Baustelle. Die KI-Brücke nutzt sie, um eine
    /// Einheiten-ID einer Baustelle-ID zuzuordnen.
    /// </summary>
    public Guid Id { get; } = Guid.NewGuid();

    public int Health
    {
        get => Core.CurrentHp;
        set => Core.CurrentHp = value;
    }
    public int MaxHealth => Core.Stats.HitPoints;

    /// <summary>
    /// Ausbildungs-Warteschlange aus AoE.Core. Bisher bildet nur das
    /// Stadtzentrum aus, und zwar Dorfbewohner (Taste Q).
    /// </summary>
    public AoE.Core.Economy.TrainingQueue<UnitType> Training { get; } = new();

    /// <summary>
    /// Die Forschung in diesem Gebäude (P2), aus AoE.Core. Ein Gebäude forscht höchstens
    /// eine zur Zeit; solange es forscht, bildet es nicht aus.
    /// </summary>
    public AoE.Core.Economy.ResearchSlot Research { get; } = new();

    /// <summary>
    /// Die Baustelle, solange das Gebäude nicht fertig ist; null bei fertigen
    /// Gebäuden. Eine Baustelle sperrt ihre Kacheln schon, zählt aber weder
    /// als Wohnraum noch als Abgabestelle.
    /// </summary>
    public AoE.Core.Economy.Construction Construction { get; set; }

    /// <summary>
    /// Der Reparatur-Auftrag, solange ein beschädigtes Gebäude wieder
    /// hergestellt wird; null bei unverletzten Gebäuden und während der
    /// Reparatur fertig ist. Kostet und dauert anteilig zum fehlenden
    /// Schadensvolumen (AOE-Kern-Repair).
    /// </summary>
    public AoE.Core.Economy.Repair Repair { get; set; }

    /// <summary>Fertig gebaut: keine Baustelle mehr oder die Baustelle ist abgeschlossen.</summary>
    public bool IsComplete => Construction == null || Construction.IsComplete;

    public Building(BuildingEntity core)
    {
        Core = core;
    }
}
