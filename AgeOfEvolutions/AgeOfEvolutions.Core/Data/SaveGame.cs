using System;
using System.Collections.Generic;
using AoE.Core.Economy;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Ein Spielstand: alles, was ein laufendes Spiel ausmacht, als reine Daten für
/// System.Text.Json (siehe <see cref="SaveGameStore"/>). Die Welt füllt
/// <see cref="SaveGameMapper.Capture"/>, Kamera, Spielzeit und KI der Spielbildschirm
/// (RTSGameplayScreen.CaptureSave).
/// </summary>
/// <remarks>
/// Keine Eigenschaft hat einen Startwert außer den Listen: der Speicher lässt
/// Standardwerte (0, false, null) weg, und beim Lesen müssen sie wieder genau diese
/// Standardwerte ergeben. Querverweise (Baustelle, Angriffsziel) stehen als Index in
/// <see cref="Buildings"/> bzw. <see cref="Units"/>.
/// </remarks>
public sealed class SaveGame
{
    /// <summary>Fassung des Formats; ein Spielstand mit anderer Fassung wird nicht geladen.</summary>
    public const int CURRENT_VERSION = 1;

    public int Version { get; set; }

    /// <summary>Wann gespeichert wurde (Ortszeit), für die Anzeige.</summary>
    public DateTime SavedAt { get; set; }

    public MapSize MapSize { get; set; }
    public int Width { get; set; }
    public int Height { get; set; }
    public int TileSize { get; set; }

    // --- vom Spielbildschirm ---------------------------------------------------

    /// <summary>Spielzeit in Sekunden (RTSGameplayScreen._gameSeconds).</summary>
    public double GameSeconds { get; set; }

    /// <summary>Takt der Animationen (Wind, Wasser), Sekunden seit Spielbeginn.</summary>
    public float AnimationTime { get; set; }

    /// <summary>Der Weltpunkt in der Mitte des Spielfelds zwischen den Leisten.</summary>
    public float CameraCenterX { get; set; }
    public float CameraCenterY { get; set; }

    /// <summary>Zoom geteilt durch den Fensterfaktor (ZoomScale); 0 heißt: Startzoom.</summary>
    public float RelativeZoom { get; set; }

    /// <summary>KI-Stufe wie in den Einstellungen: 0 Leicht, 1 Normal, 2 Schwer, 3 Extrem.</summary>
    public int AiDifficulty { get; set; }
    public bool AiEnabled { get; set; }
    public bool BothSidesAi { get; set; }

    // --- die Welt --------------------------------------------------------------

    /// <summary>Genau zwei: Spieler 0 (Mensch), dann Spieler 1.</summary>
    public List<PlayerSave> Players { get; set; } = new();

    /// <summary>Jede Kachel der Karte, Index y · Width + x.</summary>
    public List<TileSave> Tiles { get; set; } = new();

    /// <summary>Die Gebäude in der Reihenfolge von TileMap.Buildings.</summary>
    public List<BuildingSave> Buildings { get; set; } = new();

    /// <summary>Die Einheiten in der Reihenfolge von TileMap.Units.</summary>
    public List<UnitSave> Units { get; set; } = new();
}

/// <summary>Ein Spieler: Rohstoffe, Zeitalter, Forschungen und was er von der Karte kennt.</summary>
public sealed class PlayerSave
{
    public int Id { get; set; }
    public string Name { get; set; }
    public string Civilization { get; set; }

    public int Food { get; set; }
    public int Wood { get; set; }
    public int Gold { get; set; }
    public int Stone { get; set; }

    public AgeProgressState Ages { get; set; }
    public TechProgressState Techs { get; set; }

    /// <summary>
    /// Eine Ziffer je Kachel, Index y · Width + x: '1' schon erkundet (auch gerade sichtbar),
    /// '0' unerforscht.
    /// </summary>
    public string Explored { get; set; }
}

/// <summary>Eine Kachel mit allem, was sich im Spiel an ihr ändert. Das Gebäude steht in den Gebäuden.</summary>
public sealed class TileSave
{
    public TileType Type { get; set; }
    public bool Walkable { get; set; }
    public bool Buildable { get; set; }
    public Resource? ResourceType { get; set; }
    public int ResourceAmount { get; set; }
    public FoodSource Food { get; set; }

    /// <summary>Das Tier auf der Kachel, oder null.</summary>
    public AnimalSave Animal { get; set; }

    public float Wear { get; set; }
    public bool Farm { get; set; }
    public float FarmRegrow { get; set; }
    public int FarmFood { get; set; }
    public int FarmCol { get; set; }
    public int FarmRow { get; set; }
    public int FarmSize { get; set; }

    /// <summary>
    /// Wegqualität der Kachel (C12): 0 = keine Wege, sonst Stufe 1–3 —
    /// Trampelpfade rücken bei jedem Zeitaufstieg auf, see Tile.RoadLevel.
    /// </summary>
    public int RoadLevel { get; set; }
}

/// <summary>Ein Tier (<see cref="WildAnimal"/>) mit Takt, Blick und Schritt.</summary>
public sealed class AnimalSave
{
    public int Look { get; set; }
    public bool FacingLeft { get; set; }
    public float WanderTimer { get; set; }
    public int FromX { get; set; }
    public int FromY { get; set; }
    public float Glide { get; set; }
    public bool Slaughtered { get; set; }
}

/// <summary>Ein Gebäude: Lage, Besitzer, Lebenspunkte, Baustelle, Ausbildung und Forschung.</summary>
public sealed class BuildingSave
{
    /// <summary>Linke obere Kachel der Grundfläche.</summary>
    public int X { get; set; }
    public int Y { get; set; }

    /// <summary>Gebäudetyp, wie TileMap.AddBuilding ihn nimmt ("Stadtzentrum", "Haus", …).</summary>
    public string Type { get; set; }

    /// <summary>Seitenlänge der quadratischen Grundfläche in Kacheln.</summary>
    public int Size { get; set; }

    public int OwnerId { get; set; }
    public int Health { get; set; }

    /// <summary>Die Baustelle; null bei fertigen Gebäuden.</summary>
    public ConstructionState Construction { get; set; }

    /// <summary>Die Reparatur; null, wenn das Gebäude nicht repariert wird.</summary>
    public RepairState Repair { get; set; }

    public TrainingQueueState<UnitType> Training { get; set; }
    public ResearchSlotState Research { get; set; }
}

/// <summary>Ein Punkt in Weltkoordinaten (Wegpunkte).</summary>
public readonly record struct PointSave(float X, float Y);

/// <summary>Eine Einheit: Lage, Weg, Zustand, Auftrag und Ziele.</summary>
public sealed class UnitSave
{
    public Guid Id { get; set; }
    public UnitType Type { get; set; }
    public int OwnerId { get; set; }

    /// <summary>Lage und Ziel in Weltkoordinaten.</summary>
    public float X { get; set; }
    public float Y { get; set; }
    public float TargetX { get; set; }
    public float TargetY { get; set; }

    public List<PointSave> Path { get; set; } = new();
    public float Pace { get; set; }
    public UnitState State { get; set; }
    public int Health { get; set; }
    public float AttackTimer { get; set; }

    /// <summary>Der Sammelauftrag, oder null.</summary>
    public GatherJobState Job { get; set; }

    /// <summary>Index der Baustelle in <see cref="SaveGame.Buildings"/>, oder null.</summary>
    public int? BuildSite { get; set; }

    /// <summary>Index des angegriffenen Gebäudes in <see cref="SaveGame.Buildings"/>, oder null.</summary>
    public int? AttackTarget { get; set; }

    /// <summary>Index der angegriffenen Einheit in <see cref="SaveGame.Units"/>, oder null.</summary>
    public int? UnitTarget { get; set; }
}
