using Microsoft.Xna.Framework;
using Resource = AoE.Core.Entities.Resource;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Tile types for the game world
/// </summary>
public enum TileType
{
    Grassland,      // Normal terrain, building allowed
    Water,          // Cannot walk or build
    Forest,         // Contains trees (wood resource)
    Mountain,       // Contains stone (stone resource)
    GoldMine,       // Contains gold
    Sand,           // Coastal terrain
    Snow,           // Frozen terrain
    Rock,           // Rocky terrain
    Wall,           // Defensive structure
    Base            // Starting position
}

/// <summary>
/// Art der Nahrungsquelle auf einer Kachel – bestimmt, wie sie gezeichnet wird
/// </summary>
public enum FoodSource { None, Sheep, Berries, Fish, Farm, Deer, Rabbit, Boar }

/// <summary>Hilfen zu den Nahrungsquellen.</summary>
public static class FoodSources
{
    /// <summary>Ob die Quelle ein Tier ist, das wandert, gejagt und geschlachtet wird.</summary>
    public static bool IsWild(this FoodSource food)
        => food is FoodSource.Sheep or FoodSource.Deer or FoodSource.Rabbit or FoodSource.Boar;
}

/// <summary>
/// Represents a single tile on the game map
/// </summary>
public class Tile
{
    public int X { get; set; }
    public int Y { get; set; }
    
    public TileType Type { get; set; }

    /// <summary>
    /// Der Boden der Kachel, wie die Kartenerzeugung ihn angelegt hat (G15). Er bleibt, wenn
    /// Bäume gefällt oder Stein und Gold abgebaut sind - die Wiese ändert sich dadurch nicht:
    /// wo Wald stand, bleibt Waldboden, und hohes Gras wächst nur, wo von Anfang an Wiese war.
    /// Der Konstruktor setzt ihn auf den Typ, TileMap nach der Erzeugung auf den fertigen Typ.
    /// </summary>
    public TileType GroundType { get; set; }

    public bool Walkable { get; set; }
    public bool Buildable { get; set; }
    
    // Resource information (if tile contains resources)
    public Resource? ResourceType { get; set; }
    public int ResourceAmount { get; set; }
    public FoodSource Food { get; set; }

    /// <summary>
    /// Das Schaf oder Reh auf dieser Kachel (Wandertakt, Blickrichtung,
    /// Aussehen); null, wenn keines hier steht. Wandert das Tier, zieht dieses
    /// Objekt mit ihm auf die neue Kachel.
    /// </summary>
    public WildAnimal Animal { get; set; }

    /// <summary>
    /// Wie ausgetreten der Boden ist, von 0 (unberührt) bis 1. Jede Figur, die die
    /// Kachel betritt, tritt sie weiter aus (TileMap.Trample). Ab TileMap.ROAD_WEAR_MIN
    /// ist ein Trampelpfad entstanden (<see cref="IsPath"/>), der für immer bleibt;
    /// Tritte darunter sieht man nicht, und ohne Verkehr vergehen sie wieder
    /// (TileMap.RegrowGrass, G15).
    /// </summary>
    public float Wear { get; set; }

    /// <summary>
    /// Ob hier ein Trampelpfad entstanden ist (G15): ab TileMap.ROAD_WEAR_MIN, also sechs
    /// Tritten. Ein Pfad bleibt für immer und sieht immer gleich aus - mehr Tritte ändern
    /// ihn nicht, und er wächst nicht wieder zu.
    /// </summary>
    public bool IsPath => Wear >= TileMap.ROAD_WEAR_MIN - 0.001f;
    
    /// <summary>
    /// Ob die Kachel Teil eines angelegten Feldes ist (Farm). Jede Kachel eines
    /// Feldes ist eine unabhängige 175-Nahrungsquelle (AoE II: jede Zeile wächst
    /// und wird erneut geerntet). Nach der Ernte zählt <see cref="FarmRegrow"/>
    /// herunter; bei Null füllt sie sich wieder auf 175.
    /// </summary>
    public bool Farm { get; set; }

    /// <summary>
    /// Verbleibende Sekunden, bis die geerntete Farm-Kachel wieder 175 Nahrung
    /// trägt. Nur relevant, wenn <see cref="Farm"/> ist und der Vorrat leer ist;
    /// 0 während die Kachel noch Nahrung trägt.
    /// </summary>
    public float FarmRegrow { get; set; }

    /// <summary>
    /// Vorrat der vollen Feldkachel: <see cref="TileMap.FARM_FOOD"/>, mit Pferdekummet mehr
    /// (P2). Setzt <see cref="TileMap.PlantCrop"/>; nach der Ernte wächst die Kachel wieder
    /// bis hierher nach. 0, solange die Kachel kein Feld ist.
    /// </summary>
    public int FarmFood { get; set; }

    /// <summary>
    /// Lage der Kachel in ihrem Feld: Spalte und Zeile ab der linken oberen
    /// Ecke, 0 bis <see cref="FarmSize"/> - 1. Der Spielbildschirm schneidet
    /// daraus ihren Teil des Feldbilds, das so über das ganze Feld reicht.
    /// </summary>
    public int FarmCol { get; set; }
    public int FarmRow { get; set; }

    /// <summary>Seitenlänge des Felds in Kacheln; 0, solange die Kachel kein Feld ist.</summary>
    public int FarmSize { get; set; }

    /// <summary>
    /// Wegqualität dieser Kachel (C12 neu): 0 = keine Wege (nur Trampelpfad,
    /// falls <see cref="Wear"/> &gt; 0), 1 = Pflasterstraße aus Stein und Zement
    /// (Feudalzeit), 2 = Weg aus Erde, Schlamm und Kies (Ritterzeit), 3 = verdichteter
    /// Schotter (Imperialzeit). Bei jedem Zeitaufstieg rücken die vorhandenen
    /// Trampelpfade auf (TileMap.UpgradeRoads); neu ausgetretene Pfade entstehen direkt
    /// auf der aktuellen Stufe, und ab der Feudalzeit bekommt jedes fertige Gebäude
    /// einen Weg bis ans Stadtzentrum (TileMap.ConnectBuilding, C16, C16c).
    /// Auf Stufen 1 und höher laufen Einheiten 20 % schneller als auf der Wiese.
    /// </summary>
    public int RoadLevel { get; set; }

    // Building on this tile
    public string Building { get; set; }
    
    // Fog of war visibility
    public bool Visible { get; set; }
    public bool Explored { get; set; }
    
    // Tile properties
    public bool IsWater => Type == TileType.Water;
    public bool IsForest => Type == TileType.Forest;
    public bool IsMountain => Type == TileType.Mountain;
    public bool IsGoldMine => Type == TileType.GoldMine;
    
    // Constructor
    public Tile(int x, int y, TileType type)
    {
        X = x;
        Y = y;
        Type = type;
        GroundType = type;
        SetupTileProperties();
    }
    
    private void SetupTileProperties()
    {
        // Default walkable and buildable states
        Walkable = Type != TileType.Water;
        Buildable = Type == TileType.Grassland || Type == TileType.Sand;
        
        // Determine resource type if applicable
        switch (Type)
        {
            case TileType.Forest:
                ResourceType = Resource.Wood;
                ResourceAmount = 100; // 100 trees per forest
                break;
            case TileType.Mountain:
                ResourceType = Resource.Stone;
                ResourceAmount = 80;
                break;
            case TileType.GoldMine:
                ResourceType = Resource.Gold;
                ResourceAmount = 60;
                break;
        }
    }
}

/// <summary>
/// Ein Schaf oder Reh, das mit seiner Kachel wandert: eigener Wandertakt,
/// Blickrichtung und Aussehen, damit das Tier bei jedem Schritt dasselbe
/// bleibt. Die Nahrung trägt weiter die Kachel (Food, ResourceAmount).
/// </summary>
public class WildAnimal
{
    /// <summary>Bildvariante und Versatz in der Kachel, fest ab dem Aufstellen.</summary>
    public int Look { get; set; }

    /// <summary>Blickt nach links; sonst nach rechts, wie die Sprites gemalt sind.</summary>
    public bool FacingLeft { get; set; }

    /// <summary>Sekunden bis zum nächsten Wanderschritt.</summary>
    public float WanderTimer { get; set; }

    /// <summary>
    /// Woher der letzte Schritt kam, in Kacheln relativ zur jetzigen Kachel,
    /// und wie viele Sekunden das Tier noch unterwegs ist. Solange
    /// <see cref="Glide"/> über 0 liegt, zeichnet der Spielbildschirm es
    /// zwischen alter und neuer Kachel und es beginnt keinen neuen Schritt.
    /// </summary>
    public int FromX { get; set; }
    public int FromY { get; set; }
    public float Glide { get; set; }

    /// <summary>
    /// Geschlachtet: ein Dorfbewohner hat angefangen, das Tier abzubauen. Ab da
    /// liegt es als Fleisch an seiner Stelle und wandert nie mehr weiter.
    /// </summary>
    public bool Slaughtered { get; set; }
}

/// <summary>
/// Helper class for tilemap operations
/// </summary>
public static class TileMapHelper
{
    // Default map dimensions
    public const int DefaultWidth = 64;
    public const int DefaultHeight = 64;
    public const int TileSize = 32; // pixels per tile
    
    // Get tile type from resource type
    public static TileType GetTileTypeFromResource(Resource resource)
    {
        return resource switch
        {
            Resource.Wood => TileType.Forest,
            Resource.Stone => TileType.Mountain,
            Resource.Gold => TileType.GoldMine,
            _ => TileType.Grassland
        };
    }
    
    // Check if a tile can be built on
    public static bool CanBuildOn(TileType type)
    {
        return type == TileType.Grassland || type == TileType.Sand;
    }
}
