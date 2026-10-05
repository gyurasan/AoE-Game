using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using AgeOfEvolutions.Core.Inputs;
using AgeOfEvolutions.Screens;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Resource = AoE.Core.Entities.Resource;
using UnitState = AoE.Core.Entities.UnitState;
using CorePosition = AoE.Core.Entities.Position;
using CoreVillager = AoE.Core.Entities.Villager;
using GatherJob = AoE.Core.Economy.GatherJob;
using GatherPhase = AoE.Core.Economy.GatherPhase;
using BuildingType = AoE.Core.Entities.BuildingType;
using Population = AoE.Core.Economy.Population;
using BuildingRules = AoE.Core.Economy.BuildingRules;
using Construction = AoE.Core.Economy.Construction;
using Age = AoE.Core.Economy.Age;
using AgeRules = AoE.Core.Economy.AgeRules;

namespace AgeOfEvolutions.Core.Screens;

/// <summary>
/// Main RTS gameplay screen with tilemap, units, and resource management
/// </summary>
public class RTSGameplayScreen : GameScreen
{
    private GraphicsDevice graphicsDevice;
    private SpriteBatch spriteBatch;
    private Texture2D tileTexture;

    // Camera
    private Vector2 cameraPosition = Vector2.Zero;
    private float cameraZoom = 1.0f;

    private const float MIN_ZOOM = 0.5f;
    // Maximalzoom: 1× zeigt ca. 17 Kacheln über die Fensterhöhe, 2× acht, 4×
    // noch vier — damit kommt man nah an einzelne Figuren und Gebäude heran.
    private const float MAX_ZOOM = 4.0f;

    // Bezugshöhe der Zoomgrenzen. Bis 1080 px Fensterhöhe gelten MIN_ZOOM und
    // MAX_ZOOM wie angegeben, darüber wachsen beide und der Startzoom mit. So
    // zeigt der nächste Zoom immer rund 17 Kacheln über die Fensterhöhe - auch
    // in der DX-Fassung, die mit DPI-Überschreibung in physischen Pixeln
    // zeichnet (2707 px hoch). Mit festen Grenzen kam man dort nur halb so nah.
    private const float ZOOM_REFERENCE_HEIGHT = 1080f;

    /// <summary>Faktor für Zoomgrenzen und Startzoom: 1 bis 1080 px Fensterhöhe, darüber proportional.</summary>
    private float ZoomScale => Math.Max(1f, screenBounds.Height / ZOOM_REFERENCE_HEIGHT);
    private float MinZoom => MIN_ZOOM * ZoomScale;
    private float MaxZoom => MAX_ZOOM * ZoomScale;

    // ZoomScale, auf die cameraZoom zuletzt abgestimmt wurde (SyncZoomToWindow)
    private float _appliedZoomScale = 1f;

    // Ein Schritt je Rastung des Mausrads. Multiplikativ, damit sich das
    // Zoomen über den ganzen Bereich gleich schnell anfühlt — additiv wäre
    // es nah an MIN_ZOOM viel grober als nah an MAX_ZOOM.
    private const float ZOOM_STEP = 1.12f;

    // Kamera-Schwenken: Geschwindigkeit in Bildschirmpixeln pro Sekunde und
    // der Randstreifen am Fensterrand, der das Kantenscrollen auslöst.
    private const float CAMERA_PAN_SPEED = 800f;   // Bildschirmpixel pro Sekunde
    private const int EDGE_SCROLL_MARGIN = 8;       // Pixel am Fensterrand

    // ScrollWheelValue zählt seit Programmstart hoch. Ohne diesen Startwert
    // ergäbe der erste Frame eine riesige Differenz und würde sofort auf
    // Maximalzoom springen.
    private int previousScrollWheel;
    private bool scrollInitialised;
    private Rectangle screenBounds;

    // Game state
    internal TileMap tileMap;
    internal Data.Player player1;
    internal Data.Player player2;
    internal List<Unit> units;

    // Selection
    private List<Unit> selectedUnits = new List<Unit>();
    private Vector2? selectionStart = null;
    private Rectangle selectionRectangle = Rectangle.Empty;

    // Linke Maustaste: gedrückt halten und ziehen verschiebt die Karte; erst
    // ab DRAG_THRESHOLD Pixeln gilt es als Ziehen, darunter als Klick.
    private Vector2? dragStart;
    private Vector2 dragCameraStart;
    private bool isDragging;
    private bool hudPressed;   // links über der Leiste gedrückt: Taste oder Minimap beim Loslassen
    private const float DRAG_THRESHOLD = 6f;

    // Rendering (prozedural generierte AoE1-artige Texturen)
    private Texture2D px;                     // 1x1-Pixel für FillRect/DrawLine
    private Dictionary<TileType, Texture2D> tileTex = new();
    private Dictionary<UnitType, Texture2D> unitTex = new();
    // Wasser: mehrere Varianten zu je WATER_FRAMES Animationsbildern [Variante, Frame]
    private const int WATER_VARIANTS = 3;
    private const int WATER_FRAMES = 4;
    private Texture2D[,] waterTex = new Texture2D[0, 0];
    private const int CROWN_VARIANTS = 3;
    private const int CROWN_SIZE = 24;   // Durchmesser einer Krone in Welteinheiten
    private Texture2D[] crownTex = Array.Empty<Texture2D>();

    // Lage der drei Kronen je Waldkachel in Welteinheiten (Kachel = 32)
    private static readonly (int X, int Y)[] CrownSpots = { (9, 9), (23, 12), (14, 24) };
    private int waterAnimationFrameCounter;
    private float waterAnimTimer;
    private List<Unit> drawList = new();

    // Sammeln: die Karte aus Sicht der Sammelaufträge (GatherJob, AoE.Core)
    internal TileMapGatherWorld gatherWorld;

    // Maus im vorigen Frame. Ein Rechtsklick ist ein Befehl pro Klick, nicht
    // einer pro Frame, in dem die Taste unten ist.
    private MouseState previousMouse;
    private KeyboardState previousKeyboard;
    private int idleCycleIndex;

    // Nebel des Krieges: die Sicht wird nicht jeden Frame neu gerechnet –
    // Einheiten bewegen sich langsam, viermal pro Sekunde genügt
    private float fogTimer;
    private const float FOG_INTERVAL = 0.25f;

    // ------------------------------------------------------------------
    // KI-Steuerung (Spieler 1). Der AI-Agent tickt im Update-Loop — alle
    // Befehle laufen damit im Spiel-Faden. Externe Anweisungen (REST-API,
    // Skript) steuern über EnqueueOrder dieselbe Schlange, die auch die
    // eingebaute KI benutzt.
    // ------------------------------------------------------------------
    private AgeOfEvolutions.Core.AI.AiAgent _agent;
    private readonly Queue<System.Action> _orderQueue = new();

    /// <summary>
    /// Eine Handlung in den Spiel-Faden einreihen. Thread-sicher — wird im
    /// nächsten <see cref="Update"/>-Frame ausgeführt, wo sie dieselbe
    /// owner-generic Schnittstelle nimmt wie der Mensch. So steuert die
    /// eingebaute KI und eine externe REST-KI über denselben Mechanismus.
    /// </summary>
    public void EnqueueOrder(System.Action order)
    {
        if (order == null) return;
        lock (_orderQueue) _orderQueue.Enqueue(order);
    }

    /// <summary>
    /// Die eingebaute KI einschalten (oder aus — <c>ai = null</c>). Standard:
    /// <c>EconomyAi</c> als Gegner (Spieler 1). Der Takt ist 0,5 s.
    /// Läuft die Methode zweimal, ersetzt die zweite den ersten Agenten.
    /// </summary>
    public void ActivateAi(AoE.Core.Ai.IAi ai, int owner = 1, float tickInterval = 0.5f)
    {
        if (ai == null)
        {
            _agent = null;
            return;
        }
        _agent = new AgeOfEvolutions.Core.AI.AiAgent(this, ai, owner, tickInterval);
    }

    /// <summary>Für Tests/Reflexion: der aktuelle Agent, oder null.</summary>
    public AgeOfEvolutions.Core.AI.AiAgent ActiveAgent => _agent;

    /// <summary>
    /// Alle wartenden Orders in Ausführung ausführen. Läuft nur im
    /// Update-Frame — von anderen Fäden nur über <see cref="EnqueueOrder"/>
    /// einreichen, nicht direkt hier rufen.
    /// </summary>
    private void DrainOrders()
    {
        List<System.Action> snapshot;
        lock (_orderQueue)
        {
            if (_orderQueue.Count == 0) return;
            snapshot = new List<System.Action>(_orderQueue);
            while (_orderQueue.TryDequeue(out _)) { }
        }

        foreach (var order in snapshot)
        {
            try
            {
                order();
            }
            catch (System.Exception ex)
            {
                // Ein Fehler in einer einzelnen Order darf den Spiel-Faden
                // nicht abreißen; die nächste Order läuft weiter.
                System.Diagnostics.Debug.WriteLine($"[AI-Order] {ex.GetType().Name}: {ex.Message}");
            }
        }
    }

    // Höhe der oberen und unteren Leiste aus DrawUI — Klicks dort gelten
    // nicht der Karte. Die untere Leiste trägt Befehlstasten, Einheiteninfo
    // und die Minimap rechts unten
    private const int HUD_TOP_HEIGHT = 34;
    private const int HUD_BOTTOM_HEIGHT = 200;

    // Ausbildung im Stadtzentrum, Werte laut Spezifikation (Kapitel
    // „Der Dorfbewohner-Loop"): 25 Nahrung, rund 25 Sekunden
    private static readonly Dictionary<Resource, int> VillagerCost = new() { [Resource.Food] = 25 };
    private const float VILLAGER_TRAIN_SECONDS = 25f;

    // Kurzer Hinweis oben in der Mitte, etwa „Nicht genug Nahrung"
    private string hudMessage;
    private float hudMessageTimer;
    private const float HUD_MESSAGE_SECONDS = 2.5f;

    // Rot für die Bevölkerung am Limit und den Hinweis dazu
    private static readonly Color LimitColor = new Color(235, 80, 60);

    private static readonly Color[] IsoCol = // Index: (int)TileType
    {
        new Color(104, 148, 68),    // Grasland
        new Color(92, 140, 186),    // Wasser
        new Color(52, 98, 44),      // Wald
        new Color(146, 132, 110),   // Stein
        new Color(212, 190, 82),    // Gold
        new Color(196, 176, 124),   // Sand
        new Color(224, 228, 234),   // Schnee
        new Color(150, 150, 156),   // Fels
        new Color(122, 104, 86),    // Mauer
        new Color(122, 104, 86),    // Basis
    };
    private static readonly Color IsoSheep = new Color(216, 148, 178);  // Herde auf der Minimap
    private static readonly Color IsoFarm  = new Color(96, 132, 58);    // Saatfeld
    private static readonly Color IsoBuilding = new Color(200, 186, 160); // Gebäudekachel

    // Befehlstasten in der unteren Leiste: Lage wird erst gezeichnet einmal
    // berechnet (LayoutButtons) und bleibt so im Update-Loop stabil — nur der
    // Hintergrund wird neu gezogen, der Zustand nicht.
    private struct CommandButton
    {
        public Rectangle Rect;
        public string Label;     // kurze Kennung (Q, H, M, F, B, G, .)
        public string Name;      // vollständiger Name, unten auf der Taste
        public string Hint;      // Kosten/Status-Zeile im Tooltip/Bereich
        public BuildingType? Type; // gesetzt, gold, solange dieses Gebäude platziert wird
            public Action Action;
        public Texture2D Icon;     // Symbol aus Content/Icons, oder null
    }

    // Symbol je Befehlstaste - erzeugt mit Qwen-Image (tools/bilder/bilder.json,
    // Gruppe icons), verkleinert auf 128 px in Content/Icons
    private static readonly (string Label, string Asset)[] ButtonIcons =
    {
        ("Q", "Icons/dorfbewohner"), ("A", "Icons/zeitalter"), ("H", "Icons/haus"),
        ("M", "Icons/muehle"), ("F", "Icons/holzfaellerlager"), ("B", "Icons/bergbaulager"),
        ("G", "Icons/farm"), ("T", "Icons/wachturm"), (".", "Icons/untaetig"),
    };
    private readonly Dictionary<string, Texture2D> _buttonIcons = new();

    // Gebäude als Sprites - erzeugt mit Qwen-Image (tools/bilder/bilder.json,
    // Gruppe gebaeude), freigestellt und je Spieler eingefärbt: _blau für
    // Spieler 0, _rot für Spieler 1. Schlüssel ist der Gebäudename der Karte.
    // Jedes Zeitalter hat einen eigenen Satz unter Gebaeude/<ordner>/ (Gruppen
    // gebaeude_dunkel bis gebaeude_imperial); gezeichnet wird der des Zeitalters,
    // in dem der Besitzer gerade ist. Fehlt dort ein Bild, gilt das des
    // Zeitalters davor, zuletzt das zeitlose direkt unter Gebaeude/.
    private static readonly (string Type, string Asset)[] BuildingSprites =
    {
        ("Stadtzentrum", "Gebaeude/stadtzentrum"), ("Haus", "Gebaeude/haus"),
        ("Mühle", "Gebaeude/muehle"), ("Holzfällerlager", "Gebaeude/holzfaellerlager"),
        ("Bergbaulager", "Gebaeude/bergbaulager"), ("Wachturm", "Gebaeude/wachturm"),
    };
    private static readonly string[] AgeFolders = { "dunkel", "feudal", "ritter", "imperial" };   // Index = Age
    private readonly Dictionary<(string Type, Age Age, int Owner), Texture2D> _buildingSprites = new();

    // Windrad der Mühle: muehle_ohne_* ist die Mühle ohne gemalte Flügel, je
    // Zeitalter eine, Gebaeude/muehle_fluegel das Flügelkreuz von vorn für alle.
    // MillHubs hält je Mühlenbild die Nabe als Anteil am Bild (am Bild gemessen)
    // und den Durchmesser des Kreuzes als Anteil an seiner Breite; die Drehung in
    // Bogenmaß je Sekunde
    private static readonly Dictionary<string, (Vector2 Hub, float Size)> MillHubs = new()
    {
        ["Gebaeude/muehle_ohne"] = (new(0.566f, 0.396f), 1.0f),
        ["Gebaeude/dunkel/muehle_ohne"] = (new(0.227f, 0.45f), 1.0f),
        ["Gebaeude/feudal/muehle_ohne"] = (new(0.498f, 0.593f), 1.0f),
        ["Gebaeude/ritter/muehle_ohne"] = (new(0.494f, 0.554f), 1.0f),
        ["Gebaeude/imperial/muehle_ohne"] = (new(0.492f, 0.368f), 1.0f),
    };
    private const float MILL_SAIL_SPEED = 0.6f;
    private readonly Dictionary<(Age Age, int Owner), Texture2D> _millBare = new();
    private readonly Dictionary<Texture2D, (Vector2 Hub, float Size)> _millHubs = new();
    private Texture2D _millSails;

    // Fahnen auf den Gebäuden: das Fahnentuch je Gebäudebild (Mast links), am Bild
    // gemessen und um die Kontur erweitert - jedes Zeitalter malt es woanders.
    // DrawWavingFlag lässt es wehen: Ausschlag als Anteil an der Tuchhöhe, Welle in
    // Bogenmaß je Sekunde
    private static readonly Dictionary<string, Rectangle> FlagCloth = new()
    {
        ["Gebaeude/haus"] = new(64, 12, 48, 36),
        ["Gebaeude/muehle"] = new(168, 5, 37, 21),
        ["Gebaeude/muehle_ohne"] = new(168, 5, 37, 21),
        ["Gebaeude/wachturm"] = new(131, 12, 50, 40),
        ["Gebaeude/bergbaulager"] = new(236, 130, 20, 22),
        ["Gebaeude/holzfaellerlager"] = new(208, 190, 21, 18),
        ["Gebaeude/dunkel/haus"] = new(78, 13, 41, 38),
        ["Gebaeude/dunkel/muehle_ohne"] = new(166, 1, 44, 28),
        ["Gebaeude/dunkel/holzfaellerlager"] = new(11, 127, 27, 24),
        ["Gebaeude/dunkel/bergbaulager"] = new(207, 22, 49, 35),
        ["Gebaeude/feudal/haus"] = new(73, 26, 27, 20),
        ["Gebaeude/feudal/muehle_ohne"] = new(130, 4, 60, 29),
        ["Gebaeude/feudal/holzfaellerlager"] = new(237, 122, 19, 20),
        ["Gebaeude/feudal/bergbaulager"] = new(214, 16, 42, 36),
        ["Gebaeude/feudal/wachturm"] = new(130, 7, 45, 28),
        ["Gebaeude/ritter/haus"] = new(66, 26, 29, 23),
        ["Gebaeude/ritter/muehle_ohne"] = new(131, 4, 51, 27),
        ["Gebaeude/ritter/bergbaulager"] = new(236, 124, 20, 22),
        ["Gebaeude/ritter/wachturm"] = new(130, 8, 54, 28),
        ["Gebaeude/imperial/haus"] = new(76, 11, 35, 30),
        ["Gebaeude/imperial/muehle_ohne"] = new(131, 9, 49, 35),
        ["Gebaeude/imperial/holzfaellerlager"] = new(92, 9, 29, 19),
        ["Gebaeude/imperial/bergbaulager"] = new(223, 122, 33, 22),
        ["Gebaeude/imperial/wachturm"] = new(131, 7, 56, 37),
    };
    private readonly Dictionary<Texture2D, Rectangle> _flagCloths = new();
    private const float FLAG_WAVE = 0.18f;
    private const float FLAG_SPEED = 5f;

    // Dorfbewohner als Sprite (tools/bilder, Gruppe einheiten), je Spieler blau
    // oder rot; die Bewegung kommt aus dem Code, siehe DrawVillager. Ab der
    // Feudalzeit trägt er die Kleidung seines Zeitalters (Einheiten/<ordner>/,
    // dieselbe Figur umgekleidet, die Faust an derselben Stelle); fehlt ein Bild,
    // die des Zeitalters davor. Schlüssel ist Zeitalter und Spieler
    private readonly Dictionary<(Age Age, int Owner), Texture2D> _villagerSprites = new();
    // Laufbilder (dorfbewohner_lauf1, _lauf2 im Ordner des Standbilds), deckungsgleich
    // mit dem Standbild zugeschnitten: Schritt, Stand, Gegenschritt, Stand
    private readonly Dictionary<(Age Age, int Owner), Texture2D[]> _villagerWalk = new();
    private Texture2D _shadowTex;          // weicher Schatten unter den Füßen

    // Werkzeuge der Dorfbewohner (tools/bilder, Gruppe werkzeuge): waagerecht,
    // Griff links, Kopf rechts, Schneide unten. DrawTool dreht sie um die Faust.
    // Winkel in Bogenmaß für eine nach rechts blickende Figur: 0 zeigt nach vorn,
    // negativ nach oben. Länge als Anteil an der Figurenhöhe, Rate in Schlägen
    // je Sekunde.
    private enum Tool { Hoe, Axe, Pickaxe, Hammer, Sickle, Rod }
    private readonly record struct ToolStyle(string Asset, float Length, float Raised, float Strike, float Rate);
    private static readonly Dictionary<Tool, ToolStyle> ToolStyles = new()
    {
        [Tool.Hoe] = new("Werkzeuge/hacke", 0.75f, -2.3f, 0.75f, 0.9f),
        [Tool.Axe] = new("Werkzeuge/axt", 0.6f, -2.4f, 0.7f, 1.0f),
        [Tool.Pickaxe] = new("Werkzeuge/spitzhacke", 0.6f, -2.4f, 0.75f, 0.9f),
        [Tool.Hammer] = new("Werkzeuge/hammer", 0.42f, -1.9f, 0.35f, 1.6f),
        [Tool.Sickle] = new("Werkzeuge/sichel", 0.4f, -1.1f, 0.9f, 1.3f),
        [Tool.Rod] = new("Werkzeuge/angel", 0.95f, -0.75f, -0.55f, 0.4f),
    };
    private const float TOOL_REST = -2.7f;   // auf der Schulter, der Kopf schräg hinten oben
    // Faust im Dorfbewohner-Sprite als Anteil an Breite und Höhe - am Bild gemessen
    private static readonly Vector2 VillagerFist = new(0.89f, 0.31f);
    private readonly Dictionary<Tool, Texture2D> _toolSprites = new();
    private readonly Dictionary<Tool, Vector2> _toolGrips = new();   // Griffpunkt im Werkzeugbild

    // Boden, Wald und Rohstoffe aus tools/bilder (Gruppen boden, baeume und
    // rohstoffe): große kachelbare Bilder für Gras, Sand und Wasser, freigestellte
    // Bäume mit Stamm, Stein- und Goldhaufen.
    // Fehlt ein Bild, zeichnet der Code diesen Teil wie bisher selbst.
    private static readonly string[] TreeAssets =
        { "Baeume/laubbaum", "Baeume/laubbaum2", "Baeume/nadelbaum", "Baeume/nadelbaum2", "Baeume/buschbaum" };
    private static readonly string[] StoneAssets = { "Rohstoffe/stein", "Rohstoffe/stein2" };
    private static readonly string[] GoldAssets = { "Rohstoffe/gold", "Rohstoffe/gold2" };
    // Schafe und Rehe aus tools/bilder (Gruppe tiere), nach rechts blickend. Ihre
    // Breite als Anteil an der Kachel - ein Dorfbewohner ist 24 von 32 Welteinheiten hoch
    private static readonly string[] SheepAssets = { "Tiere/schaf", "Tiere/schaf2" };
    private static readonly string[] DeerAssets = { "Tiere/reh", "Tiere/reh2" };
    private static readonly string[] RabbitAssets = { "Tiere/kaninchen", "Tiere/kaninchen2" };
    private static readonly string[] BoarAssets = { "Tiere/wildschwein", "Tiere/wildschwein2" };
    private const float SHEEP_WIDTH = 0.8f;
    private const float DEER_WIDTH = 0.95f;
    private const float RABBIT_WIDTH = 0.45f;
    private const float BOAR_WIDTH = 0.85f;
    private const int WALK_CYCLES_PER_STEP = 2;   // Doppelschritte der Beine je Kachelschritt
    private Texture2D _grassTex;
    private Texture2D _sandTex;
    private Texture2D _waterGroundTex;    // Wasserbild; waterTex sind die gezeichneten Wasserkacheln
    private Texture2D _soilTex;           // abgeerntetes Feld (Felder/acker), ein Bild je Feld
    private Texture2D _wheatTex;          // dasselbe Feld mit reifem Weizen (Felder/weizen)
    private Effect _wheatEffect;          // Weizen im Wind (Effects/Weizen); fehlt er, steht der Weizen still.
    private Texture2D[] _treeSprites = Array.Empty<Texture2D>();
    private Texture2D[] _stoneSprites = Array.Empty<Texture2D>();
    private Texture2D[] _goldSprites = Array.Empty<Texture2D>();
    private Texture2D[] _sheepSprites = Array.Empty<Texture2D>();
    private Texture2D[] _deerSprites = Array.Empty<Texture2D>();
    private Texture2D[] _rabbitSprites = Array.Empty<Texture2D>();
    private Texture2D[] _boarSprites = Array.Empty<Texture2D>();
    private Texture2D[] _deerFallback;    // das gezeichnete Reh, wenn Tiere/reh* fehlen
    // Laufbilder je Standbild (Tiere/<name>_lauf1, _lauf2), deckungsgleich mit ihm
    // zugeschnitten: Schritt, Stand, Gegenschritt, Stand
    private readonly Dictionary<Texture2D, Texture2D[]> _animalWalk = new();
    // Fleisch eines geschlachteten Tiers (Tiere/fleisch_schaf, Tiere/fleisch_reh)
    private Texture2D _sheepMeat;
    private Texture2D _deerMeat;
    private Texture2D _rabbitMeat;
    private Texture2D _boarMeat;
    private const int GROUND_TEXELS = 4;   // Bildpixel der Bodenbilder je Welteinheit

    // Boden aus einem Guss (Effects/Boden): Gras, Sand und Wasser gehen weich
    // ineinander über, das Wasser hat Tiefe, Wellen und Schaum, viel begangene
    // Kacheln zeigen Trampelpfade. Fehlt der Effekt oder ein Bodenbild, zeichnet
    // DrawGround den Boden wie bisher Kachel für Kachel.
    private Effect _groundEffect;
    private Texture2D _groundControl;     // Steuerbild: Sand, Wasser, Tiefe (BuildGroundControl)
    private Texture2D _groundLive;        // je Kachel: ausgetreten, Wald - ändert sich im Spiel
    private Color[] _groundLiveData;
    private float _groundLiveTimer;
    private Texture2D _groundNoise;
    private const int GROUND_CONTROL_TEXELS = 4;       // Texel des Steuerbilds je Kachel und Richtung
    private const float GROUND_SMOOTHING = 0.45f;      // Glättung der Kachelgrenzen, in Kacheln
    private const float GROUND_LIVE_INTERVAL = 0.25f;  // so oft übernimmt das Lebendbild die Karte

    // Bewegung je Einheit, aus der Lage zwischen zwei Updates abgeleitet - die
    // Einheit selbst speichert weder Blickrichtung noch Tempo
    private struct UnitMotion
    {
        public Vector2 Last;       // Lage im letzten Update
        public bool FacingLeft;    // zuletzt nach links gelaufen
        public float MovingFor;    // gilt noch so viele Sekunden als in Bewegung
        public float Walked;       // gelaufene Strecke in Welteinheiten - Takt der Laufbilder
    }
    private readonly Dictionary<Unit, UnitMotion> _unitMotion = new();
    private float animationTime;   // Sekunden seit Spielbeginn, Takt der Animation
    private List<CommandButton> _buttons = new();
    private int _buttonsLayoutHeight;    // Fensterhöhe, für die _buttons angelegt sind
    private bool _buttonsLayoutBuilders; // ob dabei ein Dorfbewohner ausgewählt war
    private Age _buttonsLayoutAge;       // und welches Zeitalter galt
    private Rectangle _minimapRect;      // das gezeichnete Minimap-Feld (Klickfläche)

    // Abstand der Minimap zur Unterkante der Leiste; der 2-px-Rahmen liegt darin.
    // Seitenlänge 80 % mehr als die frühere 192-px-Minimap, siehe MinimapRect.
    private const int MINIMAP_MARGIN = 4;
    private const int MINIMAP_SIZE = 346;

    // Bauen (C5): Dorfbewohner wählen, Taste drücken, Bauplatz anklicken.
    // Kosten, Bauzeit und Größe kommen aus AoE.Core (BuildingRules); der Name
    // ist zugleich der Gebäudetyp der Karte (TileMap.AddBuilding).
    private static readonly (Keys Key, BuildingType Type, string Name)[] BuildMenu =
    {
        (Keys.H, BuildingType.House, "Haus"),
        (Keys.M, BuildingType.Mill, "Mühle"),
        (Keys.F, BuildingType.LumberCamp, "Holzfällerlager"),
        (Keys.B, BuildingType.MiningCamp, "Bergbaulager"),
        (Keys.G, BuildingType.Farm, "Farm"),
        (Keys.T, BuildingType.Tower, "Wachturm"),   // ab der Feudalzeit
    };
    private BuildingType? placing;   // was gerade gesetzt wird, oder null
    private Vector2 mouseGridCell;

    private readonly MapSize _mapSize;   // Kartengröße aus dem Hauptmenü

    /// <summary>
    /// Eingebaute KI als Gegner (Spieler 1). Standardmäßig an — so hat ein
    /// Solo-Spiel automatisch einen spielenden Gegenspieler. Auf
    /// <c>false</c>, wenn man zweispielerig/hotseat spielen will.
    /// </summary>
    public bool AiEnabled { get; set; } = true;

    public RTSGameplayScreen(MapSize mapSize = MapSize.Standard)
    {
        _mapSize = mapSize;
        TransitionOnTime = TimeSpan.FromSeconds(1.5);
        TransitionOffTime = TimeSpan.FromSeconds(0.5);
    }

    /// <summary>
    /// The world is built here rather than in the constructor, because the
    /// GraphicsDevice is only reachable once the ScreenManager has adopted us.
    /// </summary>
    public override void LoadContent()
    {
        base.LoadContent();

        graphicsDevice = ScreenManager.GraphicsDevice;
        screenBounds = new Rectangle(0, 0, graphicsDevice.PresentationParameters.BackBufferWidth,
                                          graphicsDevice.PresentationParameters.BackBufferHeight);

        // Initialize game
        int side = MapSizes.Side(_mapSize);
        tileMap = new TileMap(side, side, 32, MapSettings.ForSize(_mapSize));
        gatherWorld = new TileMapGatherWorld(tileMap);
        player1 = new Data.Player(0, "Player 1", "Briten");
        player2 = new Data.Player(1, "Player 2", "Azteken");

        // Add units from tilemap
        units = tileMap.Units;

        // Assign units to players
        foreach (var unit in units)
        {
            if (unit.OwnerId == 0) player1.AddUnit(unit);
            else player2.AddUnit(unit);
        }

        // Bevölkerungsgrenze gleich aus den Startgebäuden rechnen
        UpdatePopulationLimits();

        // Eingebaute KI als Gegner aktivieren — Solo-Spiel bekommt automatisch
        // einen spielenden Gegenspieler (Owner 1). Die KI tickt im Update-Loop
        // (0,5 s) und benutzt dieselben owner-generic Screen-Methoden wie der
        // Mensch. Externe Steuerungen (REST, Netzwerk) steuern über
        // EnqueueOrder die denselben Pfad nehmen.
        if (AiEnabled)
        {
            ActivateAi(new AoE.Core.Ai.EconomyAi(), owner: 1);
            System.Diagnostics.Debug.WriteLine("[AI] Eingebaute Wirtschaft-KI als Gegner (Owner 1) aktiv.");
        }

        // Create placeholder textures
        tileTexture = CreateTexture(graphicsDevice, 32, 32, Color.Green);
        BuildAoETextures();

        // Symbole der Befehlstasten; fehlt eines, zeigt die Taste wie bisher
        // ihren Namen
        foreach (var (label, asset) in ButtonIcons)
        {
            try
            {
                _buttonIcons[label] = ScreenManager.Game.Content.Load<Texture2D>(asset);
            }
            catch (Microsoft.Xna.Framework.Content.ContentLoadException)
            {
            }
        }

        // Dorfbewohner-Sprites je Zeitalter und Spielerfarbe und ihr Schatten. Die
        // Dunkle Zeit trägt die Grundkleidung unter Einheiten/, spätere Zeitalter ihre
        // eigene, sonst die des Zeitalters davor; fehlt das Sprite ganz, zeichnet
        // DrawUnits die Figur wie bisher selbst
        foreach (var (owner, colour) in new[] { (0, "_blau"), (1, "_rot") })
        {
            var stand = LoadOptional("Einheiten/dorfbewohner" + colour);
            var walk = VillagerWalkCycle(stand, "Einheiten/", colour);
            for (int age = 0; age < AgeFolders.Length; age++)
            {
                string folder = "Einheiten/" + AgeFolders[age] + "/";
                if (LoadOptional(folder + "dorfbewohner" + colour) is { } dressed)
                {
                    stand = dressed;
                    walk = VillagerWalkCycle(dressed, folder, colour);
                }
                if (stand != null)
                    _villagerSprites[((Age)age, owner)] = stand;
                if (walk != null)
                    _villagerWalk[((Age)age, owner)] = walk;
            }
        }
        _shadowTex = BuildShadowTexture(graphicsDevice);
        foreach (var (tool, style) in ToolStyles)
        {
            if (LoadOptional(style.Asset) is { } tex)
            {
                _toolSprites[tool] = tex;
                _toolGrips[tool] = ToolGrip(tex);
            }
        }

        _grassTex = LoadOptional("Boden/gras");
        _sandTex = LoadOptional("Boden/sand");
        _soilTex = LoadOptional("Felder/acker");
        _wheatTex = LoadOptional("Felder/weizen");
        _waterGroundTex = LoadOptional("Boden/wasser");
        try
        {
            _groundEffect = ScreenManager.Game.Content.Load<Effect>("Effects/Boden");
        }
        catch (Microsoft.Xna.Framework.Content.ContentLoadException)
        {
        }
        try
        {
            _wheatEffect = ScreenManager.Game.Content.Load<Effect>("Effects/Weizen");
        }
        catch (Microsoft.Xna.Framework.Content.ContentLoadException)
        {
        }
        if (_groundEffect != null && _grassTex != null && _sandTex != null)
        {
            _groundNoise = BuildNoiseTexture(graphicsDevice);
            _groundControl = BuildGroundControl(graphicsDevice);
            _groundLive = new Texture2D(graphicsDevice, tileMap.Width, tileMap.Height);
            _groundLiveData = new Color[tileMap.Width * tileMap.Height];
            RefreshGroundLive();
        }
        _treeSprites = TreeAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _stoneSprites = StoneAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _goldSprites = GoldAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _sheepSprites = SheepAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _deerSprites = DeerAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _rabbitSprites = RabbitAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _boarSprites = BoarAssets.Select(LoadOptional).Where(t => t != null).ToArray();
        _sheepMeat = LoadOptional("Tiere/fleisch_schaf");
        _deerMeat = LoadOptional("Tiere/fleisch_reh");
        _rabbitMeat = LoadOptional("Tiere/fleisch_kaninchen");
        _boarMeat = LoadOptional("Tiere/fleisch_wildschwein");
        // Laufbilder nur, wenn beide da sind - sonst gleitet das Tier wie bisher
        foreach (var asset in SheepAssets.Concat(DeerAssets).Concat(RabbitAssets).Concat(BoarAssets))
        {
            if (LoadOptional(asset) is { } stand && LoadOptional(asset + "_lauf1") is { } lauf1
                && LoadOptional(asset + "_lauf2") is { } lauf2)
                _animalWalk[stand] = new[] { lauf1, stand, lauf2, stand };
        }

        // Gebäude-Sprites je Zeitalter und Spielerfarbe. Fehlt das Bild eines
        // Zeitalters, gilt das des Zeitalters davor, zuletzt das zeitlose unter
        // Gebaeude/; fehlt auch das, zeichnet DrawBuilding das Gebäude wie bisher
        // selbst
        foreach (var (owner, colour) in new[] { (0, "_blau"), (1, "_rot") })
        {
            foreach (var (type, asset) in BuildingSprites)
            {
                var sprite = LoadBuildingSprite(asset, colour);
                for (int age = 0; age < AgeFolders.Length; age++)
                {
                    sprite = LoadBuildingSprite(AgeAsset(asset, age), colour) ?? sprite;
                    if (sprite != null)
                        _buildingSprites[(type, (Age)age, owner)] = sprite;
                }
            }
            var bare = LoadBuildingSprite("Gebaeude/muehle_ohne", colour);
            for (int age = 0; age < AgeFolders.Length; age++)
            {
                bare = LoadBuildingSprite(AgeAsset("Gebaeude/muehle_ohne", age), colour) ?? bare;
                if (bare != null)
                    _millBare[((Age)age, owner)] = bare;
            }
        }
        _millSails = LoadOptional("Gebaeude/muehle_fluegel");

        // Start the camera on player 1's town center instead of the map corner.
        // Startzoom wie die Zoomgrenzen an der Fensterhöhe ausgerichtet; die
        // Bildmitte liegt in Weltkoordinaten bei bildschirm / (2 * zoom).
        cameraZoom = ZoomScale;
        _appliedZoomScale = cameraZoom;
        var start = tileMap.GridToWorld(new Vector2(3, 3));
        cameraPosition = new Vector2(screenBounds.Width / (2f * cameraZoom),
                                     screenBounds.Height / (2f * cameraZoom)) - start;
        ClampCamera();

        // Sicht gleich zu Beginn rechnen, sonst wäre der erste Frame schwarz
        tileMap.UpdateFogOfWarForPlayer(0, units);
    }

    public override void UnloadContent()
    {
        tileTexture?.Dispose();
        tileTexture = null;
        px?.Dispose(); px = null;
        foreach (var t in tileTex.Values) t?.Dispose();
        tileTex.Clear();
        foreach (var t in waterTex) t?.Dispose();
        waterTex = new Texture2D[0, 0];
        foreach (var t in crownTex) t?.Dispose();
        crownTex = Array.Empty<Texture2D>();
        foreach (var t in unitTex.Values) t?.Dispose();
        unitTex.Clear();
        _groundControl?.Dispose(); _groundControl = null;
        _groundLive?.Dispose(); _groundLive = null;
        _groundNoise?.Dispose(); _groundNoise = null;
    }

    // Fester Seed: die prozeduralen Texturen sollen bei jedem Start gleich aussehen.
    private readonly Random _texRng = new Random(1337);

    private Texture2D CreateTexture(GraphicsDevice device, int width, int height, Color color)
    {
        var texture = new Texture2D(device, width, height);
        var colors = new Color[width * height];
        for (int i = 0; i < colors.Length; i++)
            colors[i] = color;
        texture.SetData(colors);
        return texture;
    }

    public override void Update(GameTime gameTime, bool otherScreenHasFocus, bool coveredByOtherScreen)
    {
        base.Update(gameTime, otherScreenHasFocus, coveredByOtherScreen);

        if (!IsActive)
            return;

        // Keep the HUD and selection maths in sync with window resizes.
        screenBounds = new Rectangle(0, 0,
            graphicsDevice.PresentationParameters.BackBufferWidth,
            graphicsDevice.PresentationParameters.BackBufferHeight);

        SyncZoomToWindow();
        HandleRtsInput(gameTime);
        UpdateUnits(gameTime);
        UpdateUnitMotion((float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdatePopulationLimits();
        UpdateTraining((float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateAges((float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateConstruction((float)gameTime.ElapsedGameTime.TotalSeconds);
        tileMap.RegrowCrop((float)gameTime.ElapsedGameTime.TotalSeconds);
        tileMap.RegrowGrass((float)gameTime.ElapsedGameTime.TotalSeconds);
        UpdateSheepClaims();    // Reservierung = der einzige Dorfbewohner, der gerade erntet
        tileMap.UpdateSheep((float)gameTime.ElapsedGameTime.TotalSeconds);
        tileMap.UpdateDeer((float)gameTime.ElapsedGameTime.TotalSeconds);
        tileMap.UpdateRabbits((float)gameTime.ElapsedGameTime.TotalSeconds);
        tileMap.UpdateBoars((float)gameTime.ElapsedGameTime.TotalSeconds);

        if (hudMessageTimer > 0f)
            hudMessageTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;

        // Nebel des Krieges: Sicht von Spieler 0 im Takt FOG_INTERVAL neu rechnen
        fogTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (fogTimer <= 0f)
        {
            fogTimer = FOG_INTERVAL;
            tileMap.UpdateFogOfWarForPlayer(0, units);
            // Der KI-Gegner (Spieler 1) braucht seine eigene Sicht — seine
            // Einheiten und Gebäude decken seine Startzone auf. Ohne den
            // zweiten Aufruf wäre seine ganze Karte im Nebel und er könnte
            // nie gezielt sammeln.
            tileMap.UpdateFogOfWarForPlayer(1, units);
        }

        // Befehle aus der Queue (eingebaute KI, REST-API, Skripte) im
        // Spiel-Faden ausführen. Jede Order läuft als geschlossener Block —
        // der menschliche und der KI-Eingabe pfaden teilen sich denselben
        // owner-generic Kernel.
        DrainOrders();

        // Die eingebaute KI tickt auf ihrem eigenen Rhythmus. Sie liest den
        // gerade aktuellen Zustand und meldet ihre Handlungen als Orders —
        // dieselbe Queue, die auch ein externer Spieler füttert.
        if (_agent != null)
        {
            _agent.Tick((float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        UpdateResources(gameTime);

        // Trampelpfade und gefällter Wald: das Lebendbild des Bodens nachziehen
        _groundLiveTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
        if (_groundLive != null && _groundLiveTimer <= 0f)
        {
            _groundLiveTimer = GROUND_LIVE_INTERVAL;
            RefreshGroundLive();
        }

        // Wasser-Animation: alle ~400 ms ein Frame weiter (2,5 fps-Loop)
        waterAnimTimer += (float)gameTime.ElapsedGameTime.TotalMilliseconds;
        if (waterTex.Length > 0 && waterAnimTimer > 400f)
        {
            waterAnimTimer = 0f;
            waterAnimationFrameCounter = (waterAnimationFrameCounter + 1) % WATER_FRAMES;
        }
    }

    // ====================================================================
    // AoE1-artiges Rendering: prozedurale Pixel-Art-Texturen
    // ====================================================================

    // --- Kleine Pixel-Helfer -------------------------------------------

    private class TextureBuilder
    {
        public readonly int W, H;
        public Color[] P;
        public TextureBuilder(int w, int h) { W = w; H = h; P = new Color[w * h]; }
        public TextureBuilder Set(int x, int y, Color c)
        {
            if (x >= 0 && y >= 0 && x < W && y < H) P[y * W + x] = c;
            return this;
        }
        public void FillRect(int x, int y, int w, int h, Color c)
        {
            for (int i = x; i < x + w; i++)
                for (int j = y; j < y + h; j++) Set(i, j, c);
        }
        public void FillCircle(int cx, int cy, int r, Color c)
        {
            for (int j = -r; j <= r; j++)
                for (int i = -r; i <= r; i++)
                    if (i * i + j * j <= r * r) Set(cx + i, cy + j, c);
        }
        public void OutlineRect(int x, int y, int w, int h, Color c)
        {
            FillRect(x, y, w, 1, c);
            FillRect(x, y + h - 1, w, 1, c);
            FillRect(x, y, 1, h, c);
            FillRect(x + w - 1, y, 1, h, c);
        }
        public void Noise(Color baseC, int amount, Random rng, float density = 1f)
        {
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                {
                    if (rng.NextDouble() > density) continue;
                    int d = rng.Next(-amount, amount + 1);
                    var c = P[y * W + x];
                    Set(x, y, new Color(
                        (byte)Math.Clamp(c.R + d, 0, 255),
                        (byte)Math.Clamp(c.G + d, 0, 255),
                        (byte)Math.Clamp(c.B + d, 0, 255)));
                }
        }
        public Texture2D Build(GraphicsDevice gd)
        {
            var t = new Texture2D(gd, W, H);
            t.SetData(P);
            return t;
        }
    }

    /// <summary>Lädt ein Bild aus dem Content-Verzeichnis, null wenn es fehlt.</summary>
    private Texture2D LoadOptional(string asset)
    {
        try
        {
            return ScreenManager.Game.Content.Load<Texture2D>(asset);
        }
        catch (Microsoft.Xna.Framework.Content.ContentLoadException)
        {
            return null;
        }
    }

    /// <summary>
    /// Lädt das Gebäudebild asset + colour, falls es das gibt, und merkt sich dazu
    /// sein Fahnentuch (FlagCloth) und bei der Mühle die Nabe (MillHubs). Beide
    /// gehören zum Bild, nicht zum Gebäudetyp - jedes Zeitalter malt sie woanders.
    /// </summary>
    private Texture2D LoadBuildingSprite(string asset, string colour)
    {
        var sprite = LoadOptional(asset + colour);
        if (sprite == null)
            return null;
        if (FlagCloth.TryGetValue(asset, out var cloth))
            _flagCloths[sprite] = cloth;
        if (MillHubs.TryGetValue(asset, out var mill))
            _millHubs[sprite] = mill;
        return sprite;
    }

    /// <summary>
    /// Die Laufbilder zum Dorfbewohner-Standbild stand aus seinem Ordner folder:
    /// Schritt, Stand, Gegenschritt, Stand - oder null, wenn eines fehlt; dann
    /// gleitet die Figur wie bisher. Laufbilder eines anderen Zeitalters passen
    /// nicht zur Kleidung und werden deshalb nicht genommen.
    /// </summary>
    private Texture2D[] VillagerWalkCycle(Texture2D stand, string folder, string colour)
        => stand != null && LoadOptional(folder + "dorfbewohner_lauf1" + colour) is { } lauf1
           && LoadOptional(folder + "dorfbewohner_lauf2" + colour) is { } lauf2
            ? new[] { lauf1, stand, lauf2, stand }
            : null;

    /// <summary>Das Bild asset im Ordner des Zeitalters age: aus Gebaeude/haus wird Gebaeude/feudal/haus.</summary>
    private static string AgeAsset(string asset, int age)
        => asset.Replace("Gebaeude/", "Gebaeude/" + AgeFolders[age] + "/");

    /// <summary>
    /// Erzeugt prozedurale Pixel-Art-Texturen für alle Kacheltypen + Wasserframes.
    /// Ruft sich in LoadContent, nachdem graphicsDevice verfügbar ist.
    /// </summary>
    private void BuildAoETextures()
    {
        var gd = graphicsDevice;
        px = new Texture2D(gd, 1, 1);
        px.SetData(new[] { Color.White });

        tileTex[TileType.Grassland] = BuildGrassTexture(gd);
        tileTex[TileType.Sand] = BuildSandTexture(gd);
        tileTex[TileType.Rock] = BuildRockTexture(gd);
        // Wald: dunkler Boden als Kachel, die Baumkronen zeichnet DrawCrowns als
        // eigene Figuren darüber – nur so dürfen sie über Kachelgrenzen ragen
        tileTex[TileType.Forest] = BuildForestFloorTexture(gd);
        crownTex = new Texture2D[CROWN_VARIANTS];
        for (int v = 0; v < CROWN_VARIANTS; v++)
            crownTex[v] = BuildCrownTexture(gd, v);
        tileTex[TileType.Mountain] = BuildMountainTexture(gd);
        tileTex[TileType.GoldMine] = BuildGoldTexture(gd);
        tileTex[TileType.Snow] = BuildSandTexture(gd); // Snow ≈ Sand-Optik

        // Wasser in mehreren Varianten zu je vier Frames (Animation)
        waterTex = new Texture2D[WATER_VARIANTS, WATER_FRAMES];
        for (int v = 0; v < WATER_VARIANTS; v++)
            for (int f = 0; f < WATER_FRAMES; f++)
                waterTex[v, f] = BuildWaterTexture(gd, f, v);
        tileTex[TileType.Water] = waterTex[0, 0];
        tileTex[TileType.Base] = BuildGrassTexture(gd); // Startzone = Gras
        tileTex[TileType.Wall] = BuildRockTexture(gd);

        // Einheiten-Sprites (24x24). Basisfarbe blau; Spieler 2 wird beim Zeichnen rot getönt.
        var unitBase = new Color(70, 110, 190);
        unitTex[UnitType.Villager] = BuildVillagerTexture(gd, unitBase);
        unitTex[UnitType.SpearMan] = BuildSpearManTexture(gd, unitBase);
        unitTex[UnitType.Archer] = BuildArcherTexture(gd, unitBase);
        unitTex[UnitType.Cavalry] = BuildCavalryTexture(gd, unitBase);
    }

    private Texture2D BuildGrassTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(76, 141, 48));
        b.Noise(new Color(76, 141, 48), 10, _texRng, 0.6f);
        // Einzelne helle Grashalme
        for (int i = 0; i < 14; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(100, 170, 60));
            if (_texRng.NextDouble() < 0.4) b.Set(x, y - 1, new Color(90, 155, 52));
        }
        // Einzelne dunklere Flecken (Erde)
        for (int i = 0; i < 5; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(60, 110, 40));
            b.Set(x + 1, y, new Color(65, 118, 44));
        }
        return b.Build(gd);
    }

    private Texture2D BuildSandTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(210, 190, 130));
        b.Noise(new Color(210, 190, 130), 8, _texRng, 0.7f);
        // Kleine Kieselsteine
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(180, 160, 110));
        }
        return b.Build(gd);
    }

    private Texture2D BuildRockTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(130, 130, 125));
        b.Noise(new Color(130, 130, 125), 15, _texRng, 0.5f);
        // Felsbrocken
        for (int i = 0; i < 4; i++)
        {
            int x = _texRng.Next(28) + 2, y = _texRng.Next(28) + 2;
            b.FillCircle(x, y, _texRng.Next(2, 4), new Color(100, 100, 95));
            b.FillCircle(x - 1, y - 1, 1, new Color(150, 150, 140));
        }
        return b.Build(gd);
    }

    /// <summary>
    /// Wasser mit sanftem Kräuseln aus zwei überlagerten Wellen und kurzen
    /// Glanzlichtern, die je Variante woanders liegen. Der Grund ist in allen
    /// Frames gleich, damit nichts flimmert; beide Wellen schließen an allen
    /// Kachelrändern nahtlos an und laufen je Frame eine Viertelperiode weiter.
    /// </summary>
    private Texture2D BuildWaterTexture(GraphicsDevice gd, int frame, int variant)
    {
        var b = new TextureBuilder(32, 32);
        var deep = new Color(46, 96, 158);
        b.FillRect(0, 0, 32, 32, deep);
        b.Noise(deep, 4, new Random(99), 0.5f);

        var band = new Color(56, 110, 172);
        for (int y = 0; y < 32; y++)
        {
            for (int x = 0; x < 32; x++)
            {
                double a = 2 * Math.PI * (x + 2 * y) / 32.0 + frame * Math.PI / 2;
                double c = 2 * Math.PI * (3 * x - y) / 32.0 - frame * Math.PI / 2;
                if (Math.Sin(a) + 0.7 * Math.Sin(c) > 1.1)
                    b.Set(x, y, band);
            }
        }

        var glint = new Color(150, 195, 235);
        var rng = new Random(7 + variant * 13);
        for (int i = 0; i < 4; i++)
        {
            int gx = (rng.Next(32) + frame * 2) % 32, gy = rng.Next(32);
            b.FillRect(gx, gy, 2 + rng.Next(2), 1, glint);
        }
        return b.Build(gd);
    }

    /// <summary>
    /// Waldboden: dunkel und leicht verrauscht. Die Bäume stehen nicht in der
    /// Kacheltextur – eine Textur kann nicht über ihren Rand hinaus zeichnen,
    /// und abgeschnittene Kronen zeigten das Kachelraster.
    /// </summary>
    private Texture2D BuildForestFloorTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(38, 72, 30));
        b.Noise(new Color(38, 72, 30), 8, new Random(4711), 0.7f);
        return b.Build(gd);
    }

    /// <summary>
    /// Eine Baumkrone mit durchsichtigem Grund: Schatten nach rechts unten,
    /// Blattbüschel am Rand und Licht von links oben.
    /// </summary>
    private Texture2D BuildCrownTexture(GraphicsDevice gd, int variant)
    {
        var rng = new Random(815 + variant * 31);
        var b = new TextureBuilder(CROWN_SIZE, CROWN_SIZE);
        int m = CROWN_SIZE / 2;
        b.FillCircle(m + 2, m + 3, 10, new Color(24, 56, 22));
        b.FillCircle(m, m, 10, new Color(40, 98, 34));
        for (int i = 0; i < 6; i++)
            b.FillCircle(m + rng.Next(-7, 8), m + rng.Next(-7, 8), rng.Next(3, 5), new Color(50, 114, 40));
        b.FillCircle(m - 3, m - 4, 4, new Color(78, 140, 52));
        b.Set(m - 5, m - 6, new Color(110, 170, 70));
        return b.Build(gd);
    }

    private Texture2D BuildMountainTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        b.FillRect(0, 0, 32, 32, new Color(130, 125, 120));
        b.Noise(new Color(130, 125, 120), 12, _texRng, 0.6f);

        // Bergkamm: diagonale Felsen
        for (int i = 0; i < 6; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            int sz = _texRng.Next(2, 5);
            b.FillRect(x, y, sz, sz, new Color(110, 105, 100));
            b.Set(x + 1, y, new Color(140, 135, 128)); // Lichtkante
            b.Set(x, y + sz - 1, new Color(80, 78, 72)); // Schattenkante
        }
        // Steinbruch-Spuren (dunkle Punkte)
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.Set(x, y, new Color(95, 92, 88));
        }
        return b.Build(gd);
    }

    private Texture2D BuildGoldTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Brauner Untergrund
        b.FillRect(0, 0, 32, 32, new Color(140, 110, 70));
        b.Noise(new Color(140, 110, 70), 10, _texRng, 0.5f);

        // Goldklumpen
        for (int i = 0; i < 8; i++)
        {
            int x = _texRng.Next(28) + 2, y = _texRng.Next(28) + 2;
            int sz = _texRng.Next(1, 3);
            b.FillRect(x, y, sz, sz, new Color(255, 200, 50));
            if (sz > 1) b.Set(x, y, new Color(255, 230, 120)); // Glanz
        }
        // Fels um die Adern
        for (int i = 0; i < 5; i++)
        {
            int x = _texRng.Next(32), y = _texRng.Next(32);
            b.FillCircle(x, y, _texRng.Next(1, 3), new Color(100, 80, 55));
        }
        return b.Build(gd);
    }

    private Texture2D BuildBerryTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Durchsichtiger Grund: darunter liegt das Gras der Karte

        // Büsch: grüner Haufen mit roten Beeren
        int cx = 16, cy = 18;
        b.FillCircle(cx, cy, 8, new Color(50, 110, 40));
        b.FillCircle(cx - 2, cy - 2, 5, new Color(60, 125, 48));
        b.FillCircle(cx + 3, cy + 2, 4, new Color(45, 100, 36));

        // Beeren (kleine rote Punkte)
        for (int i = 0; i < 10; i++)
        {
            int angle = _texRng.Next(360);
            int dist = _texRng.Next(0, 6);
            int x = cx + (int)(Math.Cos(angle * Math.PI / 180) * dist);
            int y = cy + (int)(Math.Sin(angle * Math.PI / 180) * dist);
            b.Set(x, y, new Color(200, 40, 40));
            b.Set(x + 1, y, new Color(180, 30, 30));
        }
        // Glanzpunkt
        b.Set(cx - 1, cy - 3, new Color(255, 100, 100));
        return b.Build(gd);
    }

    private Texture2D BuildSheepTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        var wolle = new Color(238, 236, 228);
        var wolleUnten = new Color(212, 210, 200);
        var dunkel = new Color(92, 78, 68);
        var bein = new Color(70, 58, 52);
        var schnauze = new Color(76, 62, 54);

        // Weicher Schatten auf dem Gras (zweireihig, in der Mitte breiter)
        for (int x = 9; x <= 23; x++)
            b.Set(x, 25, new Color(56, 106, 38, 110));
        for (int x = 11; x <= 21; x++)
            b.Set(x, 26, new Color(56, 106, 38, 60));

        // Vier schmale Beine
        foreach (var lx in new[] { 11, 13, 19, 21 })
            for (int y = 19; y <= 24; y++)
                b.Set(lx, y, bein);

        // Wollkörper aus überlappenden Kreisen - fluffig statt kastig
        b.FillCircle(13, 15, 4, wolle);
        b.FillCircle(17, 14, 5, wolle);
        b.FillCircle(21, 15, 4, wolle);
        b.FillCircle(15, 17, 3, wolle);
        b.FillCircle(19, 17, 3, wolle);
        b.FillCircle(17, 17, 4, wolle);

        // Unterkante etwas dunkler, damit die Wolle Tiefe bekommt
        for (int x = 10; x <= 24; x++) b.Set(x, 19, wolleUnten);
        for (int x = 13; x <= 21; x++) b.Set(x, 20, wolleUnten);

        // Kopf nach rechts, Schnauze schräg nach unten, Ohr und Auge
        b.FillCircle(25, 16, 3, dunkel);
        b.FillRect(26, 17, 3, 2, schnauze);
        b.Set(24, 13, dunkel);            // Ohr
        b.Set(25, 15, new Color(35, 30, 28)); // Auge
        return b.Build(gd);
    }

    private Texture2D BuildDeerTexture(GraphicsDevice gd)
    {
        // Rothirsch: rostbrauner Körper, helle Färbung auf dem Bauch, ein
        // Paar geweihte Antler, schlankes Profil mit Schwanz.
        var b = new TextureBuilder(32, 32);
        var felle = new Color(150, 99, 62);
        var felleHell = new Color(188, 146, 105);
        var bein = new Color(92, 62, 40);
        var antler = new Color(122, 92, 58);
        var weisse = new Color(230, 220, 205);

        // Weicher Schatten auf dem Gras (zweireihig, in der Mitte breiter)
        for (int x = 8; x <= 24; x++)
            b.Set(x, 25, new Color(56, 106, 38, 110));
        for (int x = 10; x <= 22; x++)
            b.Set(x, 26, new Color(56, 106, 38, 60));

        // Vier lange, schlanke Beine
        foreach (var lx in new[] { 10, 12, 19, 21 })
            for (int y = 19; y <= 24; y++)
                b.Set(lx, y, bein);

        // Körper (dünner als ein Schaf, in der Mitte leicht gesenkt)
        b.FillCircle(12, 14, 4, felle);
        b.FillCircle(16, 13, 5, felle);
        b.FillCircle(20, 14, 4, felle);
        b.FillCircle(14, 16, 3, felle);
        b.FillCircle(18, 16, 3, felle);

        // Helle Färbung auf Bauch und Hinterbein (Rothirsch)
        b.FillCircle(16, 17, 3, felleHell);
        b.FillCircle(20, 16, 2, felleHell);

        // Kopf schräg nach rechts unten; Schnauze etwas abgemager
        b.FillCircle(25, 15, 3, felle);
        b.FillRect(27, 16, 2, 2, new Color(88, 56, 36));   // dunkle Schnauze
        b.Set(26, 14, new Color(30, 22, 18));               // Auge

        // Geweihte Antler: ein Paar mit zwei Asteln (einfach, aber erkennbar)
        // Stange rechts
        for (int y = 8; y <= 14; y++)
            b.Set(24, y, antler);
        // Astel oben rechts
        b.Set(25, 10, antler);
        b.Set(26, 9, antler);
        b.Set(27, 9, antler);
        // Astel oben links
        b.Set(23, 10, antler);
        // Spitze
        b.Set(24, 7, antler);

        // Schwanz: kleiner weißer Fleck (Kennzeichen der Rothirsche)
        b.Set(23, 17, weisse);
        b.Set(24, 17, weisse);
        b.Set(23, 18, weisse);
        return b.Build(gd);
    }

    private Texture2D BuildFishTexture(GraphicsDevice gd)
    {
        // Durchsichtiger Grund: der Schwarm liegt über dem animierten Wasser.
        // Nur deckende Farben – halbdurchsichtige hellen bei vormultipliziertem
        // Alpha auf.
        var b = new TextureBuilder(32, 32);
        var body = new Color(175, 195, 205);
        var back = new Color(85, 105, 125);
        var eye = new Color(20, 20, 30);

        // Drei kleine Fische, schräg versetzt
        foreach (var (fx, fy) in new[] { (6, 9), (17, 15), (8, 21) })
        {
            b.FillRect(fx, fy, 7, 3, body);       // Körper
            b.FillRect(fx + 1, fy, 5, 1, back);   // Rücken
            b.Set(fx + 5, fy + 1, eye);           // Auge
            b.Set(fx - 1, fy, body);              // Schwanzflosse oben
            b.Set(fx - 1, fy + 2, body);          // Schwanzflosse unten
        }

        // Kringel an der Oberfläche über dem Schwarm
        for (int x = 20; x < 27; x++)
            b.Set(x, 6, new Color(200, 225, 240));
        return b.Build(gd);
    }

    /// <summary>
    /// Getreidefeld: ein dunklerer Erdfleck mit mehreren senkrechten
    /// Getreidehalmen in Goldgelb. Wird bei voller Ernte hell, bei leerer
    /// Kachel (RegrowCrop) dunkel — die Geometrie bleibt, nur die Farbe wechselt.
    /// </summary>
    private Texture2D BuildFarmTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 32);
        // Gras/Erde-Grund (dunkler als Wiese, heller als ein gepflügtes Feld)
        b.FillRect(0, 0, 32, 32, new Color(110, 85, 50));
        b.Noise(new Color(110, 85, 50), 6, _texRng, 0.3f);
        // Getreidehalme: 4–5 senkrechte, mit Ähren an der Spitze
        var stalk = new Color(170, 145, 65);
        var head = new Color(210, 185, 85);
        int[] xs = { 6, 11, 16, 21, 27 };
        foreach (int sx in xs)
        {
            b.FillRect(sx, 12, 2, 16, stalk);
            b.FillRect(sx - 1, 10, 4, 3, head);
            b.FillRect(sx, 9, 2, 2, new Color(230, 215, 115));
        }
        // Erdfurchen zwischen den Halmen
        for (int x = 2; x < 30; x++)
            if ((x + 1) % 5 == 0) b.Set(x, 26, new Color(70, 55, 30));
        return b.Build(gd);
    }

    /// <summary>
    /// Screen-Eingaben: ESC öffnet das Pausenmenü.
    /// </summary>
    public override void HandleInput(GameTime gameTime, InputState inputState)
    {
        if (inputState.IsPauseGame(ControllingPlayer))
        {
            // Nicht ExitScreen(): LoadingScreen.Load hat beim Spielstart alle
            // vorherigen Screens beendet, das Hauptmenü existiert also nicht
            // mehr. Ein Schließen hinterließe einen leeren Stack — schwarzes
            // Bild ohne Rückweg. Das Pausenmenü bietet Fortsetzen und einen
            // sauberen Rückweg ins Hauptmenü.
            ScreenManager.AddScreen(new PauseScreen(), ControllingPlayer);
        }
    }
    private Texture2D BuildVillagerTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
        // Figur (10x16 Pixel): horizontal zentriert (x=7..17)
        int x = 7, y = 4;

        // Hautfarbe
        Color skinColor = new Color(225, 190, 150);
        Color outlineColor = new Color(40, 30, 25);

        // Kopf (eine kleine Halbkugel)
        b.FillCircle(x + 4, y + 1, 3, skinColor);
        b.OutlineRect(x + 1, y, 8, 3, outlineColor);

        // Körper (Kappe und Tunika)
        // Kappe (braun)
        b.FillRect(x, y + 4, 10, 2, new Color(139, 69, 19));
        b.OutlineRect(x, y + 4, 10, 2, outlineColor);

        // Tunika in Spielerfarbe
        b.FillRect(x + 1, y + 6, 8, 10, playerColor);
        b.OutlineRect(x + 1, y + 6, 8, 10, outlineColor);

        // Arme (klein, ausgerichtet mit Körper)
        b.FillRect(x - 1, y + 8, 2, 4, playerColor);
        b.FillRect(x + 9, y + 8, 2, 4, playerColor);
        b.OutlineRect(x - 1, y + 8, 2, 4, outlineColor);
        b.OutlineRect(x + 9, y + 8, 2, 4, outlineColor);

        // eine kleine Axt in der rechten Hand
        b.FillRect(x + 10, y + 10, 1, 3, new Color(150, 150, 150)); // Stock
        b.FillRect(x + 11, y + 10, 1, 2, new Color(100, 100, 100)); // Axtkopf

        // Beine (in der Mitte)
        b.FillRect(x + 3, y + 16, 2, 2, skinColor);
        b.FillRect(x + 6, y + 16, 2, 2, skinColor);

        // Schatten unter dem Fuß
        b.FillCircle(x + 4, y + 18, 3, new Color(0, 0, 0, 70));
        b.FillCircle(x + 7, y + 18, 3, new Color(0, 0, 0, 70));

        return b.Build(gd);
    }

    private Texture2D BuildSpearManTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);
        // Figur (10x16 Pixel): horizontal zentriert (x=7..17)
        int x = 7, y = 4;

        // Hautfarbe
        Color skinColor = new Color(225, 190, 150);
        Color outlineColor = new Color(40, 30, 25);

        // Helm (Grau)
        b.FillRect(x + 2, y, 6, 2, new Color(150, 150, 160));
        b.OutlineRect(x + 2, y, 6, 2, outlineColor);
        b.FillCircle(x + 4, y + 1, 3, new Color(150, 150, 160));

        // Körper
        b.FillRect(x + 1, y + 4, 8, 10, playerColor); // Tunika in Spielerfarbe
        b.OutlineRect(x + 1, y + 4, 8, 10, outlineColor);

        // Arme (ausgerichtet mit Körper)
        b.FillRect(x - 1, y + 6, 2, 4, playerColor);
        b.FillRect(x + 9, y + 6, 2, 4, playerColor);
        b.OutlineRect(x - 1, y + 6, 2, 4, outlineColor);
        b.OutlineRect(x + 9, y + 6, 2, 4, outlineColor);

        // Speer auf der rechten Seite (1px breit, von 2 bis 21)
        b.FillRect(x + 10, y + 2, 1, 19, new Color(150, 150, 150)); // Speer-Schaft
        b.FillRect(x + 10, y + 1, 1, 1, new Color(200, 200, 200)); // Spitze

        // Beine (in der Mitte)
        b.FillRect(x + 3, y + 14, 2, 2, skinColor);
        b.FillRect(x + 6, y + 14, 2, 2, skinColor);

        // Schatten unter dem Fuß
        b.FillCircle(x + 4, y + 16, 3, new Color(0, 0, 0, 70));
        b.FillCircle(x + 7, y + 16, 3, new Color(0, 0, 0, 70));

        return b.Build(gd);
    }


    /// <summary>
    /// Schwenkt die Kamera per Pfeiltasten und Kantenscrollen (Mauszeiger am
    /// Fensterrand). Die Richtung wird in Bildschirmkoordinaten bestimmt und
    /// bildratenunabhängig über dt umgesetzt; die Division durch cameraZoom
    /// hält die Schwenkgeschwindigkeit auf dem Bildschirm zoomunabhängig.
    /// </summary>
    private void PanCamera(KeyboardState keyboard, MouseState mouse, float dt)
    {
        var dir = Vector2.Zero;

        // Pfeiltasten: Richtung, in die der Ausschnitt wandern soll
        if (keyboard.IsKeyDown(Keys.Left))
            dir.X -= 1;
        if (keyboard.IsKeyDown(Keys.Right))
            dir.X += 1;
        if (keyboard.IsKeyDown(Keys.Up))
            dir.Y -= 1;
        if (keyboard.IsKeyDown(Keys.Down))
            dir.Y += 1;

        // Kantenscrollen: nur wenn der Zeiger im Fenster liegt und keine
        // Auswahl aufgezogen wird oder die Karte gezogen
        bool mouseInWindow = mouse.X >= 0 && mouse.X < screenBounds.Width
                          && mouse.Y >= 0 && mouse.Y < screenBounds.Height;
        if (mouseInWindow && !selectionStart.HasValue && !dragStart.HasValue)
        {
            if (mouse.X < EDGE_SCROLL_MARGIN)
                dir.X -= 1;
            if (mouse.X >= screenBounds.Width - EDGE_SCROLL_MARGIN)
                dir.X += 1;
            if (mouse.Y < EDGE_SCROLL_MARGIN)
                dir.Y -= 1;
            if (mouse.Y >= screenBounds.Height - EDGE_SCROLL_MARGIN)
                dir.Y += 1;
        }

        if (dir != Vector2.Zero)
        {
            dir.Normalize(); // diagonal nicht schneller
            cameraPosition -= dir * CAMERA_PAN_SPEED * dt / cameraZoom;
        }
    }

    /// <summary>
    /// Eingabe eines Bildes. Ohne Angabe liest die Methode Tastatur und Maus
    /// selbst; tools/spielablauf übergibt beide, um Klicks nachzustellen.
    /// </summary>
    private void HandleRtsInput(GameTime gameTime, KeyboardState? keyboardInput = null,
                                MouseState? mouseInput = null)
    {
        var keyboard = keyboardInput ?? Keyboard.GetState();
        var mouse = mouseInput ?? Mouse.GetState();

        // Kamera schwenken: Pfeiltasten und Bildrand
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
        PanCamera(keyboard, mouse, dt);

        // Zoom
        if (keyboard.IsKeyDown(Keys.OemPlus))
            cameraZoom = Math.Min(cameraZoom + 0.1f, MaxZoom);
        if (keyboard.IsKeyDown(Keys.OemMinus))
            cameraZoom = Math.Max(cameraZoom - 0.1f, MinZoom);

        // Zoom mit dem Mausrad — auf den Cursor zu, nicht auf die Bildmitte.
        // Der Weltpunkt unter dem Zeiger bleibt dabei an Ort und Stelle, so
        // wie man es von Karten kennt.
        if (!scrollInitialised)
        {
            previousScrollWheel = mouse.ScrollWheelValue;
            scrollInitialised = true;
        }

        int wheelDelta = mouse.ScrollWheelValue - previousScrollWheel;
        previousScrollWheel = mouse.ScrollWheelValue;

        if (wheelDelta != 0)
            ZoomAt(new Vector2(mouse.X, mouse.Y), wheelDelta > 0 ? ZOOM_STEP : 1f / ZOOM_STEP);

        // Nach einer Änderung der Fenstergröße können die Grenzen enger sein
        cameraZoom = MathHelper.Clamp(cameraZoom, MinZoom, MaxZoom);

        // Egal ob per Tastatur geschwenkt, per Tastatur oder Mausrad gezoomt:
        // danach darf der Ausschnitt nicht über den Kartenrand hinausragen.
        ClampCamera();

        if (keyboard.IsKeyDown(Keys.OemPeriod) && previousKeyboard.IsKeyUp(Keys.OemPeriod))
            SelectNextIdleVillager();

        // Q: Dorfbewohner im Stadtzentrum ausbilden, einmal je Tastendruck
        if (keyboard.IsKeyDown(Keys.Q) && previousKeyboard.IsKeyUp(Keys.Q))
            TrainVillager();

        // A: Aufstieg ins nächste Zeitalter, ebenfalls im Stadtzentrum
        if (keyboard.IsKeyDown(Keys.A) && previousKeyboard.IsKeyUp(Keys.A))
            AdvanceAge();

        // Baumenü: H Haus, M Mühle, F Holzfällerlager, B Bergbaulager, G Farm,
        // ab der Feudalzeit T Wachturm.
        // Dieselbe Taste noch einmal schaltet den Setzmodus aus; identisch
        // erledigen die quadratischen Befehlstasten in der unteren Leiste.
        foreach (var entry in BuildMenu)
        {
            if (!keyboard.IsKeyDown(entry.Key) || !previousKeyboard.IsKeyUp(entry.Key))
                continue;
            TogglePlacing(entry.Type);
        }

        // Maus: rechts markieren (Klick oder Rahmen), links gedrückt halten und
        // ziehen verschiebt die Karte, ein kurzer Linksklick ist ein Befehl.
        // ScreenToWorld ist die Umkehrung von WorldToScreen (pos / zoom - kamera).
        var mousePos = new Vector2(mouse.X, mouse.Y);
        var mouseGridPos = WorldToGrid(ScreenToWorld(mousePos));
        mouseGridCell = mouseGridPos;

        // Beim Setzen eines Gebäudes bricht ein Rechtsklick ab, statt zu markieren
        if (placing != null && mouse.RightButton == ButtonState.Pressed
            && previousMouse.RightButton == ButtonState.Released)
        {
            placing = null;
            previousMouse = mouse;
            previousKeyboard = keyboard;
            return;
        }

        // Rechts: markieren
        if (mouse.RightButton == ButtonState.Pressed)
        {
            if (!selectionStart.HasValue && previousMouse.RightButton == ButtonState.Released
                && !IsOverHud(mouse.Position))
            {
                selectionStart = mousePos;
            }

            if (selectionStart.HasValue)
            {
                var minX = Math.Min(selectionStart.Value.X, mousePos.X);
                var minY = Math.Min(selectionStart.Value.Y, mousePos.Y);
                var width = Math.Abs(mousePos.X - selectionStart.Value.X);
                var height = Math.Abs(mousePos.Y - selectionStart.Value.Y);
                selectionRectangle = new Rectangle((int)minX, (int)minY, (int)width, (int)height);
            }
        }
        else if (selectionStart.HasValue)
        {
            if (selectionRectangle.Width > 5 && selectionRectangle.Height > 5)
            {
                // Rahmen: alle eigenen Einheiten darin
                var worldSelection = new Rectangle(
                    (int)(selectionRectangle.X / cameraZoom - cameraPosition.X),
                    (int)(selectionRectangle.Y / cameraZoom - cameraPosition.Y),
                    (int)(selectionRectangle.Width / cameraZoom),
                    (int)(selectionRectangle.Height / cameraZoom));
                SelectUnitsInRectangle(worldSelection);
            }
            else
            {
                // Einzelklick: die Einheit darunter – oder niemand
                SelectSingleUnit(ScreenToWorld(mousePos));
            }

            selectionStart = null;
            selectionRectangle = Rectangle.Empty;
        }

        // Links: gedrückt halten und ziehen verschiebt die Karte. Über der Leiste
        // beginnt kein Ziehen - dort merkt sich hudPressed den Druck, damit das
        // Loslassen die Taste oder die Minimap auslöst.
        if (mouse.LeftButton == ButtonState.Pressed)
        {
            if (!dragStart.HasValue && !hudPressed && previousMouse.LeftButton == ButtonState.Released)
            {
                if (IsOverHud(mouse.Position))
                {
                    hudPressed = true;
                }
                else
                {
                    dragStart = mousePos;
                    dragCameraStart = cameraPosition;
                    isDragging = false;
                }
            }

            if (dragStart.HasValue)
            {
                var offset = mousePos - dragStart.Value;
                if (!isDragging && offset.Length() > DRAG_THRESHOLD)
                    isDragging = true;

                if (isDragging)
                {
                    // Die Karte folgt dem Zeiger: WorldToScreen = (welt + kamera) * zoom
                    cameraPosition = dragCameraStart + offset / cameraZoom;
                    ClampCamera();
                }
            }
        }
        else if (hudPressed)
        {
            // Über der Leiste gedrückt und losgelassen: Taste oder Minimap. Bis
            // 2026-10-03 kam dieser Klick nie an - das Loslassen wurde nur
            // ausgewertet, wenn auf der Karte ein Ziehen begonnen hatte.
            hudPressed = false;
            if (IsOverHud(mouse.Position))
                HandleHudButtons(mouse.Position);
        }
        else if (dragStart.HasValue)
        {
            // Losgelassen ohne zu ziehen: ein Klick auf die Karte
            if (!isDragging && !IsOverHud(mouse.Position))
                LeftClick(mousePos);

            dragStart = null;
            isDragging = false;
        }
        previousMouse = mouse;
        previousKeyboard = keyboard;
    }

    private bool IsOverHud(Point p)
        => p.Y < HUD_TOP_HEIGHT || p.Y >= screenBounds.Height - HUD_BOTTOM_HEIGHT || MinimapPanel().Contains(p);

    /// <summary>
    /// Die Fläche, die eine Einheit in der Welt einnimmt: die Figur steht mit den
    /// Füßen auf ihrer Position und ist 24 Einheiten breit und hoch – so, wie
    /// DrawUnits sie zeichnet. Auswahl per Klick und per Rahmen prüfen dagegen.
    /// </summary>
    private static Rectangle UnitWorldRect(Unit unit)
        => new Rectangle((int)(unit.Position.X - 12), (int)(unit.Position.Y - 24), 24, 24);

    private void SelectUnitsInRectangle(Rectangle selectionRect)
    {
        foreach (var u in units.Where(u => u.OwnerId == 0))
        {
            var unitRect = UnitWorldRect(u);

            if (selectionRect.Intersects(unitRect))
            {
                if (!selectedUnits.Contains(u))
                {
                    selectedUnits.Add(u);
                    u.IsSelected = true;
                }
            }
            else
            {
                u.IsSelected = false;
            }
        }

        // Remove units no longer in list
        selectedUnits.RemoveAll(u => !u.IsSelected);
    }

    private void SelectSingleUnit(Vector2 worldPos)
    {
        // Clear previous selection
        foreach (var unit in selectedUnits)
            unit.IsSelected = false;
        selectedUnits.Clear();

        var u = OwnUnitAt(worldPos);
        if (u != null)
        {
            selectedUnits.Add(u);
            u.IsSelected = true;
        }
    }

    /// <summary>
    /// Die eigene Einheit, deren Figur unter <paramref name="worldPos"/> liegt,
    /// oder null. Überlappen sich zwei, gewinnt die zuletzt gezeichnete, also
    /// die vorderste.
    /// </summary>
    private Unit OwnUnitAt(Vector2 worldPos)
    {
        var point = new Point((int)worldPos.X, (int)worldPos.Y);
        return units.LastOrDefault(u => u.OwnerId == 0 && UnitWorldRect(u).Contains(point));
    }

    /// <summary>
    /// Ein kurzer Linksklick an <paramref name="screenPos"/>. Beim Setzen eines
    /// Gebäudes legt er die Baustelle an. Auf einer eigenen Einheit wählt er sie
    /// aus, statt sie wegzuschicken - vorher zog ein Klick zum Auswählen einen
    /// Bauarbeiter vom Bau ab. Sonst ist er ein Befehl an die Auswahl.
    /// </summary>
    private void LeftClick(Vector2 screenPos)
    {
        var worldPos = ScreenToWorld(screenPos);
        var gridPos = WorldToGrid(worldPos);
        if (placing != null)
            PlaceBuilding(placing.Value, gridPos);
        else if (OwnUnitAt(worldPos) != null)
            SelectSingleUnit(worldPos);
        else
            IssueCommand(gridPos);
    }

    /// <summary>
    /// Alle untätigen Dorfbewohner von Spieler 0: kein Sammelauftrag und
    /// Zustand Idle, in der Reihenfolge der Liste units.
    /// </summary>
    private List<Unit> IdleVillagers()
    {
        return units.Where(u => u.OwnerId == 0
                               && u.Core is CoreVillager
                               && u.State == UnitState.Idle
                               && u.Job == null).ToList();
    }

    /// <summary>
    /// Setz-Modus für ein Gebäude ein- oder ausschalten. Ein Klick auf die
    /// Befehlstaste (oder der Tastendruck) schaltet um; ohne ausgewählten
    /// Dorfbewohner gibt es einen Hinweis. Wird von Taste und Leiste gleichermaßen
    /// aufgerufen.
    /// </summary>
    /// <summary>Ein Dorfbewohner ist ausgewählt - nur der kann bauen.</summary>
    private bool VillagerSelected() => selectedUnits.Any(u => u.Core is CoreVillager);

    private void TogglePlacing(BuildingType type)
    {
        if (placing == type)
        {
            placing = null;
            return;
        }
        if (!AgeRules.IsUnlocked(type, player1.Ages.Current))
            ShowHudMessage($"Erst ab der {AgeRules.NameOf(AgeRules.RequiredAgeOf(type))}");
        else if (VillagerSelected())
            placing = type;
        else
            ShowHudMessage("Erst einen Dorfbewohner auswählen");
    }

    /// <summary>
    /// Nächstes untätigen Dorfbewohner wählen und die Kamera darauf ausrichten —
    /// Taste „." und die Leiste „Untätig" tun dasselbe.
    /// </summary>
    private void SelectNextIdleVillager()
    {
        var idle = IdleVillagers();
        if (idle.Count == 0)
            return;
        idleCycleIndex %= idle.Count;
        var villager = idle[idleCycleIndex];
        idleCycleIndex++;
        foreach (var selected in selectedUnits)
            selected.IsSelected = false;
        selectedUnits.Clear();
        villager.IsSelected = true;
        selectedUnits.Add(villager);
        // WorldToScreen = (welt + cameraPosition) * cameraZoom: so landet er in der Bildmitte
        cameraPosition = new Vector2(screenBounds.Width / (2f * cameraZoom),
                                     screenBounds.Height / (2f * cameraZoom)) - villager.Position;
        ClampCamera();
    }

    /// <summary>
    /// Befehl an die Auswahl (kurzer Linksklick). Dorfbewohner auf eine eigene
    /// Baustelle helfen bauen, auf eine Ressource bekommen sie einen
    /// Sammelauftrag; alles andere ist ein Laufbefehl. Jeder Befehl beendet,
    /// was die Einheit vorher tat.
    /// </summary>
    private void IssueCommand(Vector2 gridPos)
    {
        // Menschliche UI-Ebene: die Auswahl gehört zu Spieler 0, deshalb
        // genau dieselben Einheiten, die gerade ausgewählt sind.
        var sel = new List<Unit>();
        foreach (var u in selectedUnits)
        {
            if (u.OwnerId == 0) sel.Add(u);
        }
        if (sel.Count == 0)
        {
            sel.AddRange(selectedUnits);    // Fallback: keine eigenen Einheiten — alle nehmen
        }
        IssueCommand(0, sel, gridPos);
    }

    /// <summary>
    /// Owner-generic Version — die KI-Schnittstelle. <paramref name="ownerId"/>
    /// ist der Spieler, <paramref name="selectedUnits"/> seine Einheiten.
    /// </summary>
    internal void IssueCommand(int ownerId, List<Unit> selectedUnits, Vector2 gridPos)
    {
        var tile = tileMap.GetTile((int)gridPos.X, (int)gridPos.Y);
        // Nur Erforschtes lässt sich gezielt ernten; ein Klick in den Nebel ist ein Laufbefehl
        bool isSource = tile != null && tile.ResourceType.HasValue && tile.ResourceAmount > 0
                        && string.IsNullOrEmpty(tile.Building)
                        && tileMap.IsTileExplored((int)gridPos.X, (int)gridPos.Y, ownerId);
        // Eine eigene, noch unfertige Baustelle unter dem Klick
        var site = BuildingAt(gridPos);
        if (site != null && (site.OwnerId != ownerId || site.IsComplete))
            site = null;

        foreach (var unit in selectedUnits)
        {
            unit.BuildSite = null;
            if (site != null && unit.Core is CoreVillager)
            {
                AssignBuilder(unit, site);
            }
            else if (isSource && unit.Core is CoreVillager)
            {
                // Gleiche Ressource: die Traglast kommt mit, wie in AoE
                int carrying = unit.Job != null && unit.Job.Resource == tile.ResourceType.Value
                    ? unit.Job.Carrying : 0;
                unit.Job = new GatherJob(unit.OwnerId, tile.ResourceType.Value,
                                         new CorePosition((int)gridPos.X, (int)gridPos.Y), carrying);
                FollowJob(unit);
            }
            else
            {
                unit.Job = null;
                unit.TargetPosition = GridToWorld(gridPos);
                // geplant nur mit dem Wissen des Spielers, damit ein Klick ins Schwarze nicht verrät, was darunter liegt
                unit.Path = tileMap.FindPathKnown(unit.OwnerId, unit.Position, unit.TargetPosition);
                unit.State = UnitState.Moving;
            }
        }
    }

    /// <summary>
    /// Setzt um, was der Sammelauftrag als Nächstes verlangt: zum Ziel
    /// laufen, sammeln oder — ist er erledigt — untätig werden.
    /// </summary>
    internal void FollowJob(Unit unit)
    {
        var job = unit.Job;
        switch (job.Phase)
        {
            case GatherPhase.Gathering:
                unit.State = UnitState.Gathering;
                break;

            case GatherPhase.ToSource:
            case GatherPhase.ToDropOff:
                var stand = StandCell(job.Destination.Value, unit.Position);
                if (stand == null)
                {
                    job.Unreachable();
                    FollowJob(unit);
                    break;
                }

                var cellVector = stand.Value;
                unit.TargetPosition = GridToWorld(cellVector);
                unit.Path = tileMap.FindPath(unit.Position, unit.TargetPosition);

                if (unit.Path.Count > 0)
                {
                    unit.State = job.Phase == GatherPhase.ToDropOff ? UnitState.Returning : UnitState.Moving;
                }
                else if (WorldToGrid(unit.Position) == cellVector)
                {
                    // Kein Weg nötig, die Einheit steht schon auf dem Ziel.
                    ArriveWithJob(unit);
                }
                else
                {
                    job.Unreachable();
                    FollowJob(unit);
                }
                break;

            default:
                unit.Job = null;
                unit.State = UnitState.Idle;
                break;
        }
    }

    /// <summary>
    /// Die Kachel, auf die sich eine Einheit stellt, um an <paramref name="cell"/>
    /// zu arbeiten. Die Figuren zeigen sich nur von der Seite - wer über oder unter
    /// seiner Arbeit steht, schlägt seitlich ins Leere. Deshalb:
    /// VERTRAG:
    /// - Auf Feldern (Tile.Farm) und bei Tieren (Tile.Animal nicht null) die Kachel
    ///   selbst, wenn sie begehbar ist.
    /// - Sonst (Bäume, Stein, Gold, Beeren, Fisch, alles Unbegehbare) eine begehbare
    ///   Nachbarkachel: bevorzugt eine links oder rechts davon (dx ungleich 0, auch
    ///   schräg), davon die, die <paramref name="from"/> am nächsten liegt; nur wenn
    ///   keine solche begehbar ist, die nächste darüber oder darunter. So wird Fisch vom
    ///   Ufer aus gefangen.
    /// - Ist keine Nachbarkachel begehbar, die Kachel selbst, wenn sie begehbar ist,
    ///   sonst null.
    /// </summary>
    private Vector2? StandCell(CorePosition cell, Vector2 from)
    {
        var source = tileMap.GetTile(cell.X, cell.Y);
        bool onTile = source != null && (source.Farm || source.Animal != null);
        if (tileMap.IsWalkable(cell.X, cell.Y) && onTile)
            return new Vector2(cell.X, cell.Y);

        Vector2? best = null;
        float bestDistance = float.MaxValue;
        for (int dx = -1; dx <= 1; dx++)
        {
            for (int dy = -1; dy <= 1; dy++)
            {
                var neighbour = new Vector2(cell.X + dx, cell.Y + dy);
                if ((dx == 0 && dy == 0) || !tileMap.IsWalkable((int)neighbour.X, (int)neighbour.Y))
                    continue;

                float distance = Vector2.DistanceSquared(GridToWorld(neighbour), from);
                // Kacheln darüber oder darunter (dx == 0) zurückstellen: die
                // Figuren zeigen sich nur von der Seite und würden seitlich ins
                // Leere schlagen - sie kommen nur dran, wenn keine seitliche
                // begehbar ist.
                if (dx == 0)
                    distance += 1e9f;
                if (distance < bestDistance)
                {
                    best = neighbour;
                    bestDistance = distance;
                }
            }
        }
        if (best == null && tileMap.IsWalkable(cell.X, cell.Y))
            return new Vector2(cell.X, cell.Y);
        return best;
    }

    /// <summary>
    /// Die Einheit hat das Ziel ihres Sammelauftrags erreicht. An der
    /// Abgabestelle wird die volle Traglast gutgeschrieben — die frühere
    /// Kürzung auf 70 % kennt AoE nicht.
    /// </summary>
    private void ArriveWithJob(Unit unit)
    {
        int delivered = unit.Job.Arrive(gatherWorld);
        if (delivered > 0)
            PlayerOf(unit).Resources.Add(unit.Job.Resource, delivered);
        FollowJob(unit);
    }

    internal Data.Player PlayerOf(Unit unit) => unit.OwnerId == 0 ? player1 : player2;

    /// <summary>Das Zeitalter des Spielers owner - danach richtet sich, wie seine Gebäude aussehen.</summary>
    private Age AgeOf(int owner) => (owner == 0 ? player1 : player2).Ages.Current;

    /// <summary>Das Stadtzentrum eines Spielers, oder null.</summary>
    internal Building TownCenterOf(int ownerId)
        => tileMap.Buildings.FirstOrDefault(b => b.OwnerId == ownerId
                                               && b.Core.BuildingType == BuildingType.TownCenter);

    /// <summary>
    /// Bevölkerungsgrenze beider Spieler aus ihren fertigen Gebäuden:
    /// Stadtzentrum und Haus geben je 5 Plätze, höchstens 200 (AoE.Core,
    /// Population.Capacity). Eine Baustelle zählt noch nicht.
    /// </summary>
    private void UpdatePopulationLimits()
    {
        foreach (var player in new[] { player1, player2 })
        {
            player.PopulationLimit = Population.Capacity(
                tileMap.Buildings.Where(b => b.OwnerId == player.Id && b.IsComplete)
                                 .Select(b => b.Core.BuildingType));
        }
    }

    /// <summary>
    /// Reiht im Stadtzentrum von Spieler 0 einen Dorfbewohner ein. Bezahlt
    /// wird sofort; ist die Warteschlange voll oder reicht die Nahrung nicht,
    /// erscheint ein kurzer Hinweis.
    /// </summary>
    private void TrainVillager()
    {
        var townCenter = TownCenterOf(0);
        if (townCenter == null)
            return;

        if (townCenter.Training.Count >= AoE.Core.Economy.TrainingQueue<UnitType>.MAX_LENGTH)
            ShowHudMessage("Warteschlange voll");
        else if (!townCenter.Training.Enqueue(UnitType.Villager, VillagerCost, VILLAGER_TRAIN_SECONDS, player1.Resources))
            ShowHudMessage($"Nicht genug Nahrung ({VillagerCost[Resource.Food]})");
    }

    /// <summary>
    /// Owner-generic Version der Ausbildung — die KI-Schnittstelle. Der
    /// menschliche Pfad (ohne Parameter) ruft <c>TrainVillager(0)</c>.
    /// </summary>
    internal bool TrainVillager(int ownerId)
    {
        var owner = ownerId == 0 ? player1 : player2;
        var townCenter = TownCenterOf(ownerId);
        if (townCenter == null) return false;
        if (townCenter.Training.Count >= AoE.Core.Economy.TrainingQueue<UnitType>.MAX_LENGTH)
            return false;
        return townCenter.Training.Enqueue(UnitType.Villager, VillagerCost,
                                           VILLAGER_TRAIN_SECONDS, owner.Resources);
    }

    /// <summary>
    /// Owner-generic Version des Aufstiegs — die KI-Schnittstelle.
    /// </summary>
    internal bool AdvanceAge(int ownerId)
    {
        var owner = ownerId == 0 ? player1 : player2;
        var ages = owner.Ages;
        if (TownCenterOf(ownerId) == null) return false;
        if (ages.Target is { } target) return false;
        if (AgeRules.Next(ages.Current) is not { } next) return false;
        return ages.TryStart(owner.Resources);
    }

    /// <summary>
    /// Startet im Stadtzentrum von Spieler 0 den Aufstieg ins nächste Zeitalter
    /// (Taste A). Kosten und Dauer kommen aus AoE.Core (AgeRules), bezahlt wird
    /// sofort. Läuft schon ein Aufstieg, ist die Imperialzeit erreicht oder
    /// reichen die Rohstoffe nicht, erscheint ein kurzer Hinweis.
    /// </summary>
    private void AdvanceAge()
    {
        var ages = player1.Ages;
        if (TownCenterOf(0) == null)
            return;

        if (ages.Target is { } target)
            ShowHudMessage($"Aufstieg in die {AgeRules.NameOf(target)} läuft ({(int)(ages.Progress * 100)} %)");
        else if (AgeRules.Next(ages.Current) is not { } next)
            ShowHudMessage("Letztes Zeitalter erreicht");
        else if (!ages.TryStart(player1.Resources))
            ShowHudMessage($"{AgeRules.NameOf(next)} braucht {CostText(AgeRules.CostOf(next))}");
    }

    /// <summary>
    /// Aufstieg beider Spieler fortschreiben. Ist er fertig, gilt das neue
    /// Zeitalter, und die obere Leiste meldet es für Spieler 0.
    /// </summary>
    private void UpdateAges(float dt)
    {
        if (player1.Ages.Update(dt))
            ShowHudMessage($"{AgeRules.NameOf(player1.Ages.Current)} erreicht");
        player2.Ages.Update(dt);
    }

    private void ShowHudMessage(string text)
    {
        hudMessage = text;
        hudMessageTimer = HUD_MESSAGE_SECONDS;
    }

    /// <summary>
    /// Bildet in allen Gebäuden aus. Ist die Bevölkerungsgrenze erreicht,
    /// steht die Ausbildung still (TrainingQueue.IsBlocked). Ein fertiger
    /// Dorfbewohner erscheint auf der nächsten freien Kachel am Gebäude.
    /// </summary>
    private void UpdateTraining(float dt)
    {
        foreach (var building in tileMap.Buildings)
        {
            var player = building.OwnerId == 0 ? player1 : player2;
            // Während des Aufstiegs forscht das Stadtzentrum und bildet nicht aus
            if (player.Ages.IsResearching && building.Core.BuildingType == BuildingType.TownCenter)
                continue;
            if (!building.Training.Update(dt, player.PopulationCount, player.PopulationLimit, out _))
                continue;

            // Bisher bildet nur das Stadtzentrum aus, und zwar Dorfbewohner
            var cell = SpawnCell(building);
            if (cell == null)
                continue;   // rundum zugestellt - kommt praktisch nicht vor
            var villager = tileMap.AddVillager((int)cell.Value.X, (int)cell.Value.Y, building.OwnerId);
            player.AddUnit(villager);
        }
    }

    /// <summary>
    /// Nächste freie Kachel rund um ein Gebäude: Ring um Ring nach außen,
    /// höchstens 8 Kacheln weit, jeweils zeilenweise von oben links. Frei
    /// heißt begehbar und ohne Einheit darauf. null, wenn keine frei ist.
    /// </summary>
    private Vector2? SpawnCell(Building building)
    {
        for (int r = 1; r <= 8; r++)
        {
            int left = building.X - r, right = building.X + building.Width - 1 + r;
            int top = building.Y - r, bottom = building.Y + building.Height - 1 + r;
            for (int y = top; y <= bottom; y++)
            {
                for (int x = left; x <= right; x++)
                {
                    // Nur der Rand des Rings, das Innere ist schon geprüft
                    if (x != left && x != right && y != top && y != bottom)
                        continue;
                    var cell = new Vector2(x, y);
                    if (tileMap.IsWalkable(x, y) && !units.Any(u => WorldToGrid(u.Position) == cell))
                        return cell;
                }
            }
        }
        return null;
    }

    /// <summary>
    /// Ob ein Gebäude dieses Typs mit der linken oberen Ecke auf
    /// <paramref name="cell"/> Platz hat: freie, bebaubare Fläche
    /// (TileMap.CanPlaceBuilding), schon einmal gesehen und ohne Einheit darauf.
    /// </summary>
    private bool CanPlace(BuildingType type, Vector2 cell) => CanPlace(0, type, cell);

    /// <summary>
    /// Owner-generic Version — die KI-Schnittstelle. Die menschliche UI-Version
    /// (oben) delegiert hier mit <c>ownerId = 0</c>.
    /// </summary>
    internal bool CanPlace(int ownerId, BuildingType type, Vector2 cell)
    {
        int x = (int)cell.X, y = (int)cell.Y, size = BuildingRules.SizeOf(type);
        if (!tileMap.CanPlaceBuilding(x, y, size))
            return false;
        for (int bx = x; bx < x + size; bx++)
            for (int by = y; by < y + size; by++)
                if (!tileMap.IsTileExplored(bx, by, ownerId))
                    return false;
        return !units.Any(u =>
        {
            if (u.OwnerId != ownerId) return false;
            var c = WorldToGrid(u.Position);
            return c.X >= x && c.X < x + size && c.Y >= y && c.Y < y + size;
        });
    }

    /// <summary>
    /// Legt für Spieler 0 eine Baustelle mit der linken oberen Ecke auf
    /// <paramref name="cell"/> an und bezahlt sie; die ausgewählten
    /// Dorfbewohner gehen bauen. Passt das Gebäude nicht hin oder reichen die
    /// Rohstoffe nicht, bleibt der Setzmodus an und ein Hinweis erscheint.
    /// </summary>
    private void PlaceBuilding(BuildingType type, Vector2 cell)
    {
        if (!PlaceBuilding(0, type, cell,
                           selectedUnits.Where(u => u.OwnerId == 0).ToList()))
            ShowHudMessage("Hier kann nicht gebaut werden");
    }

    /// <summary>
    /// Owner-generic Baustelle anlegen, bezahlen und die übergebenen
    /// Dorfbewohner schicken — die KI-Schnittstelle. Die menschliche UI-Version
    /// delegiert mit <c>ownerId = 0</c> und der aktuellen Auswahl. Rückgabe
    /// false, wenn der Platz nicht passt oder die Rohstoffe fehlen.
    /// </summary>
    internal bool PlaceBuilding(int ownerId, BuildingType type, Vector2 cell, List<Unit> builders)
    {
        if (!CanPlace(ownerId, type, cell))
            return false;
        var owner = ownerId == 0 ? player1 : player2;
        var cost = BuildingRules.CostOf(type);
        if (!owner.Resources.PayCost(cost))
            return false;
        if (ownerId == 0)
            placing = null;   // Setzmodus nur für die menschliche Spielweise

        var villagers = builders.Where(u => u.Core is CoreVillager).ToList();

        // Farm: kein Gebäude, das man baut — ein 3×3-Feld, das man anlegt und
        // das wächst. Die angegebenen Dorfbewohner werden sofort dazu geschickt.
        if (type == BuildingType.Farm)
        {
            tileMap.PlantCrop((int)cell.X, (int)cell.Y, BuildingRules.SizeOf(type));
            HarvestFarm((int)cell.X, (int)cell.Y, villagers);
            return true;
        }

        if (!string.IsNullOrEmpty(BuildingNameOrNull(type)))
        {
            var site = tileMap.AddBuilding((int)cell.X, (int)cell.Y, BuildingName(type), ownerId, BuildingRules.SizeOf(type));
            site.Construction = new Construction(BuildingRules.BuildSecondsOf(type));
            foreach (var unit in villagers)
                AssignBuilder(unit, site);
            return true;
        }
        return false;
    }

    /// <summary>
    /// Name eines Gebäudetyps aus dem Baumenü, oder null, falls das Gebäude
    /// (noch) nicht im Menü ist — dann kann die KI es nicht bauen.
    /// </summary>
    private static string BuildingNameOrNull(BuildingType type)
    {
        foreach (var entry in BuildMenu)
            if (entry.Type == type) return entry.Name;
        return null;
    }

    /// <summary>
    /// Schickt die ausgewählten Dorfbewohner an das frisch angelegte Feld: sie
    /// ernten die Getreidekachel in der Mitte (garantiert 175 voller Vorrat) und
    /// nehmen danach — über den üblichen Sammelauftrag — die nächste volle Kachel,
    /// bis das Feld leer ist. Wie bei jeder anderen Nahrungsquelle bleibt die von
    /// ihnen mitgebrachte Traglast erhalten.
    /// </summary>
    private void HarvestFarm(int x, int y, List<Unit> villagers)
    {
        var source = new CorePosition(x + 1, y + 1);   // Mitte des 3×3-Fields, garantiert Getreide
        foreach (var v in villagers)
        {
            if (v.Job != null && v.Job.Resource == Resource.Food)
                v.Job = new GatherJob(v.OwnerId, Resource.Food, source, v.Job.Carrying);
            else
                v.Job = new GatherJob(v.OwnerId, Resource.Food, source);
            FollowJob(v);
        }
    }

    /// <summary>Name eines Gebäudetyps aus dem Baumenü, etwa „Mühle".</summary>
    private static string BuildingName(BuildingType type) => BuildMenu.First(e => e.Type == type).Name;

    /// <summary>Kosten als Text, etwa „275 Holz, 100 Stein".</summary>
    private static string CostText(Dictionary<Resource, int> cost)
        => string.Join(", ", cost.Select(c => $"{c.Value} {ResourceName(c.Key)}"));

    /// <summary>Das Gebäude, das die Kachel bedeckt, oder null.</summary>
    private Building BuildingAt(Vector2 cell)
        => tileMap.Buildings.FirstOrDefault(b => cell.X >= b.X && cell.X < b.X + b.Width
                                               && cell.Y >= b.Y && cell.Y < b.Y + b.Height);

    /// <summary>
    /// Schickt einen Dorfbewohner an eine Baustelle: ein laufender
    /// Sammelauftrag endet, er läuft auf eine Kachel am Rand und baut dort
    /// (UpdateConstruction). Ist kein Rand erreichbar, wird er untätig.
    /// </summary>
    internal void AssignBuilder(Unit unit, Building site)
    {
        unit.Job = null;
        unit.BuildSite = site;
        var stand = SiteStandCell(site, unit);
        if (stand != null)
        {
            unit.TargetPosition = GridToWorld(stand.Value);
            unit.Path = tileMap.FindPath(unit.Position, unit.TargetPosition);
            if (unit.Path.Count > 0)
            {
                unit.State = UnitState.Moving;
                return;
            }
            if (WorldToGrid(unit.Position) == stand.Value)
            {
                unit.State = UnitState.Building;   // steht schon dort
                return;
            }
        }
        unit.BuildSite = null;
        unit.State = UnitState.Idle;
    }

    /// <summary>
    /// Begehbare Kachel direkt am Rand einer Baustelle, die dem Dorfbewohner
    /// am nächsten liegt. Kacheln, auf die schon ein anderer Bauarbeiter
    /// derselben Baustelle zuläuft, kommen nur dran, wenn keine andere frei
    /// ist - so verteilen sich mehrere um das Gebäude. null, wenn keine
    /// Randkachel begehbar ist.
    /// VERTRAG (zusätzlich): Kacheln links und rechts der Baustelle (Spalten
    /// site.X - 1 und site.X + site.Width) gehen allen anderen vor - die Figuren
    /// zeigen sich nur von der Seite und hämmern sonst ins Leere; erst wenn dort
    /// keine begehbar ist, kommen die Reihen darüber und darunter dran.
    /// </summary>
    private Vector2? SiteStandCell(Building site, Unit unit)
    {
        Vector2? best = null, bestFree = null;
        float bestDistance = float.MaxValue, bestFreeDistance = float.MaxValue;
        for (int x = site.X - 1; x <= site.X + site.Width; x++)
        {
            for (int y = site.Y - 1; y <= site.Y + site.Height; y++)
            {
                bool onRing = x == site.X - 1 || x == site.X + site.Width
                           || y == site.Y - 1 || y == site.Y + site.Height;
                if (!onRing || !tileMap.IsWalkable(x, y))
                    continue;

                var cell = new Vector2(x, y);
                float distance = Vector2.DistanceSquared(GridToWorld(cell), unit.Position);
                // Kacheln, die nicht links oder rechts der Baustelle liegen,
                // zurückstellen: die Figuren zeigen sich nur von der Seite und
                // hämmern sonst seitlich ins Leere - erst wenn dort keine
                // begehbar ist, kommen die Reihen darüber und darunter dran.
                if (x != site.X - 1 && x != site.X + site.Width)
                    distance += 1e9f;
                if (distance < bestDistance)
                {
                    best = cell;
                    bestDistance = distance;
                }
                bool taken = units.Any(u => u != unit && u.BuildSite == site
                                            && WorldToGrid(u.TargetPosition) == cell);
                if (!taken && distance < bestFreeDistance)
                {
                    bestFree = cell;
                    bestFreeDistance = distance;
                }
            }
        }
        return bestFree ?? best;
    }

    /// <summary>
    /// Baut an allen Baustellen. Es zählen nur Dorfbewohner, die dort schon
    /// arbeiten (Zustand Building) - wer noch hinläuft, baut noch nicht. Ist
    /// ein Gebäude fertig, verliert es seine Baustelle, und
    /// FinishConstruction schickt die Erbauer weiter.
    /// </summary>
    private void UpdateConstruction(float dt)
    {
        foreach (var site in tileMap.Buildings.Where(b => !b.IsComplete).ToList())
        {
            int builders = units.Count(u => u.BuildSite == site && u.State == UnitState.Building);
            if (site.Construction.Update(dt, builders))
            {
                site.Construction = null;
                FinishConstruction(site);
            }
        }
    }

    /// <summary>
    /// Ein Gebäude ist fertig: seine Erbauer hören auf, auch die, die noch
    /// hinlaufen. Wer ein Lager gebaut hat, sammelt gleich die passende
    /// Ressource in der Nähe - Holzfällerlager Holz, Bergbaulager das nähere
    /// von Gold und Stein, Mühle Nahrung. Wer nichts zu sammeln hat, baut an der
    /// nächsten unfertigen eigenen Baustelle weiter (NearestUnfinishedSite) - so
    /// werden liegengebliebene Baustellen nebenbei fertig. Sonst werden die
    /// Erbauer untätig.
    /// </summary>
    private void FinishConstruction(Building site)
    {
        var center = new CorePosition(site.X + site.Width / 2, site.Y + site.Height / 2);
        CorePosition? source = null;
        var resource = Resource.Food;
        foreach (var candidate in ResourcesFor(site.Core.BuildingType))
        {
            var found = gatherWorld.FindNearestSource(center, candidate, GatherJob.SEARCH_RADIUS);
            if (found != null && (source == null
                                  || found.Value.DistanceSquared(center) < source.Value.DistanceSquared(center)))
            {
                source = found;
                resource = candidate;
            }
        }

        var nextSite = source == null ? NearestUnfinishedSite(site) : null;
        foreach (var unit in units.Where(u => u.BuildSite == site).ToList())
        {
            unit.BuildSite = null;
            unit.Path.Clear();
            if (source != null)
            {
                unit.Job = new GatherJob(unit.OwnerId, resource, source.Value);
                FollowJob(unit);
            }
            else if (nextSite != null)
            {
                AssignBuilder(unit, nextSite);
            }
            else
            {
                unit.State = UnitState.Idle;
            }
        }
    }

    /// <summary>
    /// So weit (in Kacheln, Mitte zu Mitte) suchen Erbauer nach der nächsten
    /// unfertigen Baustelle - derselbe Umkreis wie beim Sammeln.
    /// </summary>
    private const int SITE_SEARCH_RADIUS = GatherJob.SEARCH_RADIUS;

    /// <summary>
    /// Die unfertige Baustelle desselben Spielers, deren Mitte der Mitte von
    /// <paramref name="from"/> am nächsten liegt, höchstens SITE_SEARCH_RADIUS
    /// Kacheln entfernt, oder null.
    /// </summary>
    private Building NearestUnfinishedSite(Building from)
    {
        var center = new Vector2(from.X + from.Width / 2f, from.Y + from.Height / 2f);
        Building best = null;
        float bestDistance = SITE_SEARCH_RADIUS;
        foreach (var b in tileMap.Buildings)
        {
            if (b == from || b.OwnerId != from.OwnerId || b.IsComplete)
                continue;
            float distance = Vector2.Distance(center, new Vector2(b.X + b.Width / 2f, b.Y + b.Height / 2f));
            if (distance <= bestDistance)
            {
                best = b;
                bestDistance = distance;
            }
        }
        return best;
    }

    /// <summary>Was die Erbauer eines Lagers danach sammeln; leer bei anderen Gebäuden.</summary>
    private static Resource[] ResourcesFor(BuildingType type) => type switch
    {
        BuildingType.LumberCamp => new[] { Resource.Wood },
        BuildingType.MiningCamp => new[] { Resource.Gold, Resource.Stone },
        BuildingType.Mill => new[] { Resource.Food },
        _ => Array.Empty<Resource>()
    };

    private void UpdateUnits(GameTime gameTime)
    {
        var dt = (float)gameTime.ElapsedGameTime.TotalSeconds;

        foreach (var unit in units.Where(u => u.OwnerId == 0))
        {
            switch (unit.State)
            {
                case UnitState.Moving:
                case UnitState.Returning:
                    if (StepAlongPath(unit, dt))
                    {
                        if (unit.Job != null)
                            ArriveWithJob(unit);
                        else if (unit.BuildSite != null && !unit.BuildSite.IsComplete)
                            unit.State = UnitState.Building;   // am Bauplatz angekommen
                        else
                        {
                            unit.BuildSite = null;
                            unit.State = UnitState.Idle;
                        }
                    }
                    break;

                case UnitState.Building:
                    // Gebaut wird je Baustelle in UpdateConstruction; hier nur aufräumen
                    if (unit.BuildSite == null || unit.BuildSite.IsComplete)
                    {
                        unit.BuildSite = null;
                        unit.State = UnitState.Idle;
                    }
                    break;

                case UnitState.Gathering:
                    if (unit.Job == null)
                    {
                        unit.State = UnitState.Idle;
                        break;
                    }
                    unit.Job.Update(dt, gatherWorld);
                    if (unit.Job.Phase != GatherPhase.Gathering)
                        FollowJob(unit);
                    break;
            }
        }
    }

    /// <summary>
    /// Bewegt die Einheit ein Stück auf ihrem Weg. Gibt true zurück, sobald
    /// der Weg abgelaufen ist.
    /// </summary>
    /// <summary>
    /// Synchronisiert die Wild-Reservierungen mit der Realität: ein Schaf
    /// bzw. Reh bleibt reserviert, nur solange ein eigener Dorfbewohner es
    /// gerade erntet bzw. jagt (Phase <c>Gathering</c>); sonst wird es frei,
    /// damit es wieder wandern kann.
    /// </summary>
    private void UpdateSheepClaims()
    {
        // Schafe und Rehe, die gerade ein eigener Dorfbewohner erntet bzw.
        // jagt — das ist die einzige Reservierung, die zählen soll. Ein Tier
        // gehört für die ganze Zeit zu einem Auftrag, in dem es die Quelle
        // ist (ToSource hinlaufen, Gathering sammeln, ToDropOff abliefern) —
        // nicht nur, wenn der Dörfler gerade am Tier steht.
        var aktiv = new HashSet<(int x, int y)>();
        foreach (var u in units)
        {
            var job = u.Job;
            if (job == null) continue;
            if (job.Phase == GatherPhase.Done) continue;
            if (job.Resource != Resource.Food) continue;
            var food = tileMap.GetTile(job.Source.X, job.Source.Y)?.Food;
            if (food is not { } wild || !wild.IsWild()) continue;
            aktiv.Add((job.Source.X, job.Source.Y));
            // Wer schon daran sammelt, hat das Tier geschlachtet: ab jetzt Fleisch
            if (job.Phase == GatherPhase.Gathering)
                tileMap.Slaughter(job.Source.X, job.Source.Y);
        }
        tileMap.SyncSheepClaims(aktiv);
    }

    private bool StepAlongPath(Unit unit, float dt)
    {
        if (unit.Path.Count == 0)
        {
            unit.Pace = 0f;
            return true;
        }

        // über freies Land geradeaus statt im Zickzack der Kachelmitten
        // ein freier Laufbefehl, kein Sammel- oder Bauweg
        bool free = unit.Job == null && unit.BuildSite == null;
        while (unit.Path.Count >= 2 && tileMap.IsSegmentWalkable(unit.Position, unit.Path[1], 6f, free ? unit.OwnerId : null))
            unit.Path.RemoveAt(0);

        var nextPosition = unit.Path[0];
        var direction = nextPosition - unit.Position;
        var distance = direction.Length();

        if (distance < 2)
        {
            unit.Path.RemoveAt(0);
            return false;
        }

        direction.Normalize();
        // anfahren, vor dem Ziel abbremsen
        float remaining = distance;
        for (int i = 1; i < unit.Path.Count; i++)
            remaining += Vector2.Distance(unit.Path[i - 1], unit.Path[i]);
        unit.Pace = Gait.Approach(unit.Pace, Gait.ArrivalSpeed(remaining), dt);
        var before = tileMap.WorldToGrid(unit.Position);
        var next = unit.Position + direction * unit.MovementSpeed * 40 * unit.Pace * dt;
        var nextCell = tileMap.WorldToGrid(next);
        // dort ist etwas, das die Einheit erst jetzt sieht: nicht betreten, neuen Weg suchen
        if (nextCell != before && !tileMap.IsWalkable((int)nextCell.X, (int)nextCell.Y))
        {
            unit.Path = free ? tileMap.FindPathKnown(unit.OwnerId, unit.Position, unit.TargetPosition)
                             : tileMap.FindPath(unit.Position, unit.TargetPosition);
            return false;
        }
        unit.Position = next;
        // Wer eine Kachel betritt, tritt sie weiter aus - so entstehen Trampelpfade
        var after = tileMap.WorldToGrid(unit.Position);
        if (after != before)
            tileMap.Trample((int)after.X, (int)after.Y);
        return false;
    }

    private void UpdateResources(GameTime gameTime)
    {
        // No natural resource regeneration — resources are finite.
    }

    public override void Draw(GameTime gameTime)
    {
        spriteBatch = ScreenManager.SpriteBatch;

        // Clear background — must happen outside of Begin/End.
        graphicsDevice.Clear(new Color(22, 79, 45)); // Grass color

        // Deliberately drawn without ScreenManager.GlobalTransformation: this screen
        // renders in raw backbuffer space so that mouse coordinates and drawn pixels
        // line up for unit selection.
        // Der Boden kommt zuerst: Gras, Sand und Wasser aus dem Bodenshader, dann in
        // einem eigenen Durchgang mit wiederholender Abtastung die Felder - siehe
        // DrawGround. Ohne Shader zeichnet DrawGround alles Kachel für Kachel
        if (GroundShaded)
            DrawGroundShaded(spriteBatch);
        spriteBatch.Begin(samplerState: SamplerState.LinearWrap);
        DrawGround(spriteBatch);
        spriteBatch.End();
        // Der Weizen im Wind über dem Acker, den DrawGround gezeichnet hat.
        if (_wheatEffect != null && _soilTex != null && _wheatTex != null)
            DrawWheat(spriteBatch);

        spriteBatch.Begin();

        // Draw tilemap
        DrawTileMap(spriteBatch);

        // Draw units
        DrawUnits(spriteBatch);

        // Nebel über Karte und Einheiten, unter Auswahlrahmen und Leisten
        DrawFog(spriteBatch);

        // Beim Setzen eines Gebäudes: Grundfläche unter dem Mauszeiger, grün wenn frei
        if (placing != null)
        {
            int size = BuildingRules.SizeOf(placing.Value);
            var ghost = TileScreenRect((int)mouseGridCell.X, (int)mouseGridCell.Y, size, size);
            var ghostColor = CanPlace(placing.Value, mouseGridCell) ? new Color(60, 200, 60) : new Color(220, 50, 40);
            spriteBatch.Draw(px, ghost, ghostColor * 0.4f);
        }

        // Draw selection rectangle
        if (selectionRectangle.Width > 0 && selectionRectangle.Height > 0)
        {
            spriteBatch.Draw(tileTexture, selectionRectangle, new Color(255, 255, 255, 100));
        }

        // Draw UI
        DrawUI(spriteBatch);

        spriteBatch.End();

        // Fade the screen in/out while transitioning.
        if (TransitionPosition > 0)
            ScreenManager.FadeBackBufferToBlack(1f - TransitionAlpha);
    }

    /// <summary>
    /// Bodenkacheln: Gras, Sand und Wasser aus den großen Bodenbildern, sonst die
    /// gezeichneten Kacheltexturen, am Wasser die Uferlinie. Unter Bäumen, Stein
    /// und Gold liegt Gras, wenn sie als Sprites geladen sind - DrawTrees und
    /// DrawPile stellen sie in DrawTileMap darauf; der dunkle Waldboden zeigte
    /// neben einzelnen Bäumen sein Kachelquadrat. Draw ruft das in einem eigenen
    /// SpriteBatch mit SamplerState.LinearWrap auf: die Bodenbilder sind
    /// kachelbar, und das treibende Wasser greift über den Bildrand hinaus.
    /// </summary>
    private void DrawGround(SpriteBatch spriteBatch)
    {
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null) continue;
                // Gras, Sand und Wasser hat DrawGroundShaded schon gezeichnet
                if (GroundShaded && !tile.Farm) continue;

                var rect = TileScreenRect(x, y);
                if (!rect.Intersects(screenBounds)) continue;

                bool grassUnder = tile.Type == TileType.Grassland || PileSprites(tile.Type).Length > 0
                                  || (tile.Type == TileType.Forest && _treeSprites.Length > 0);
                if (tile.Type == TileType.Water && _waterGroundTex != null)
                    DrawWater(spriteBatch, x, y, rect);
                else if (tile.Farm && _soilTex != null && _wheatTex != null)
                    DrawField(spriteBatch, tile, rect);
                else if (tile.Type == TileType.Sand && _sandTex != null)
                    DrawGroundImage(spriteBatch, _sandTex, x, y, rect);
                else if (grassUnder && _grassTex != null)
                    DrawGroundImage(spriteBatch, _grassTex, x, y, rect);
                else if (GetTileTexture(tile) is { } tex)
                    spriteBatch.Draw(tex, rect, Color.White);
                if (tile.Type == TileType.Water)
                    DrawShore(spriteBatch, x, y, rect);
            }
        }
    }

    /// <summary>Ob der Boden aus dem Bodenshader kommt (Effects/Boden geladen).</summary>
    private bool GroundShaded => _groundControl != null;

    /// <summary>
    /// Gras, Sand und Wasser in einem Zug mit dem Bodenshader: das Steuerbild über
    /// die ganze Karte gelegt, der Shader rechnet daraus jeden Bildpunkt. Felder
    /// legt DrawGround danach darüber.
    /// </summary>
    private void DrawGroundShaded(SpriteBatch spriteBatch)
    {
        var p = _groundEffect.Parameters;
        p["MapTiles"]?.SetValue(new Vector2(tileMap.Width, tileMap.Height));
        p["Time"]?.SetValue(animationTime);
        p["DetailScale"]?.SetValue(tileMap.TileSize * GROUND_TEXELS / (float)_grassTex.Width);
        p["LiveTexture"]?.SetValue(_groundLive);
        p["GrassTexture"]?.SetValue(_grassTex);
        p["SandTexture"]?.SetValue(_sandTex);
        p["NoiseTexture"]?.SetValue(_groundNoise);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque, SamplerState.LinearClamp,
                          null, null, _groundEffect);
        spriteBatch.Draw(_groundControl, TileScreenRect(0, 0, tileMap.Width, tileMap.Height), Color.White);
        spriteBatch.End();
    }

    /// <summary>
    /// Das Steuerbild des Bodenshaders: je Kachel GROUND_CONTROL_TEXELS² Texel, rot
    /// Sand, grün Wasser, blau die Wassertiefe. Jeder Texel mittelt die Kacheln um
    /// sich mit einer Glockenkurve (GROUND_SMOOTHING): auf einer Kachelgrenze steht
    /// so genau die Hälfte, und der Shader zieht dort, mit Rauschen verschoben,
    /// eine weiche, unregelmäßige Grenze statt einer Kachelkante. Die Tiefe wächst
    /// mit dem Abstand zum Land, voll ab vier Kacheln. Sand und Wasser ändern sich
    /// im Spiel nicht - das Bild entsteht einmal je Karte.
    /// </summary>
    private Texture2D BuildGroundControl(GraphicsDevice gd)
    {
        int w = tileMap.Width, h = tileMap.Height, r = GROUND_CONTROL_TEXELS;
        var fields = new float[3, w, h];   // Sand, Wasser, Tiefe je Kachel
        var dist = new int[w, h];          // Abstand zum Land in Schritten, auch schräg
        var queue = new Queue<(int x, int y)>();
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                var type = tileMap.GetTile(x, y).Type;
                fields[0, x, y] = type == TileType.Sand ? 1f : 0f;
                fields[1, x, y] = type == TileType.Water ? 1f : 0f;
                dist[x, y] = type == TileType.Water ? int.MaxValue : 0;
                if (type != TileType.Water)
                    queue.Enqueue((x, y));
            }
        }
        while (queue.Count > 0)
        {
            var (x, y) = queue.Dequeue();
            for (int dx = -1; dx <= 1; dx++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    int nx = x + dx, ny = y + dy;
                    if (nx < 0 || ny < 0 || nx >= w || ny >= h || dist[nx, ny] <= dist[x, y] + 1)
                        continue;
                    dist[nx, ny] = dist[x, y] + 1;
                    queue.Enqueue((nx, ny));
                }
            }
        }
        for (int x = 0; x < w; x++)
        {
            for (int y = 0; y < h; y++)
            {
                fields[2, x, y] = fields[1, x, y] > 0f ? Math.Clamp((dist[x, y] - 1) / 3f, 0f, 1f) : 0f;
                // Stein und Gold am Wasser liegen auf dem Strand - der Kartenbau macht
                // nur Wiese am Ufer zu Sand, und der Haufen stand auf einem Grasfleck
                var type = tileMap.GetTile(x, y).Type;
                if (type is TileType.Mountain or TileType.GoldMine && dist[x, y] == 0 && NextToWater(x, y))
                    fields[0, x, y] = 1f;
            }
        }

        // Gewichte je Lage im Kachelinneren und Nachbarkachel (-2 bis 2)
        var weight = new float[r, 5];
        for (int s = 0; s < r; s++)
        {
            for (int k = 0; k < 5; k++)
            {
                float d = k - 2 + 0.5f - (s + 0.5f) / r;
                weight[s, k] = MathF.Exp(-d * d / (2f * GROUND_SMOOTHING * GROUND_SMOOTHING));
            }
        }
        var data = new Color[w * r * h * r];
        var sum = new float[3];
        for (int ty = 0; ty < h * r; ty++)
        {
            int cy = ty / r, sy = ty % r;
            for (int tx = 0; tx < w * r; tx++)
            {
                int cx = tx / r, sx = tx % r;
                float total = 0f;
                sum[0] = sum[1] = sum[2] = 0f;
                for (int j = 0; j < 5; j++)
                {
                    int ny = Math.Clamp(cy + j - 2, 0, h - 1);
                    for (int i = 0; i < 5; i++)
                    {
                        int nx = Math.Clamp(cx + i - 2, 0, w - 1);
                        float k = weight[sx, i] * weight[sy, j];
                        total += k;
                        for (int f = 0; f < 3; f++)
                            sum[f] += k * fields[f, nx, ny];
                    }
                }
                data[ty * w * r + tx] = new Color(sum[0] / total, sum[1] / total, sum[2] / total, 1f);
            }
        }
        var tex = new Texture2D(gd, w * r, h * r);
        tex.SetData(data);
        return tex;
    }

    /// <summary>Ob eine der acht Nachbarkacheln von (x, y) Wasser ist.</summary>
    private bool NextToWater(int x, int y)
    {
        for (int dx = -1; dx <= 1; dx++)
            for (int dy = -1; dy <= 1; dy++)
                if ((dx != 0 || dy != 0) && tileMap.GetTile(x + dx, y + dy)?.Type == TileType.Water)
                    return true;
        return false;
    }

    /// <summary>
    /// Kachelbares Rauschen für den Bodenshader, 256² Texel, in jedem Farbkanal ein
    /// eigenes: Wertrauschen in vier Oktaven ab einem 8×8-Gitter, auf gleichmäßig
    /// verteilte Werte von 0 bis 1 gebracht - so trifft eine Schwelle im Shader einen
    /// vorhersehbaren Anteil der Fläche. Mit Mipmaps, damit es beim Herauszoomen
    /// nicht flimmert; fester Seed, derselbe Boden bei jedem Start.
    /// </summary>
    private static Texture2D BuildNoiseTexture(GraphicsDevice gd)
    {
        const int size = 256;
        var rng = new Random(4711);
        var level = new Vector4[size * size];
        for (int c = 0; c < 4; c++)
        {
            var v = new float[size * size];
            float amp = 1f;
            for (int cells = 8; cells <= 64; cells *= 2, amp *= 0.5f)
            {
                var grid = new float[cells * cells];
                for (int i = 0; i < grid.Length; i++)
                    grid[i] = (float)rng.NextDouble();
                float cell = size / (float)cells;
                for (int y = 0; y < size; y++)
                {
                    int y0 = (int)(y / cell), y1 = (y0 + 1) % cells;
                    float fy = MathHelper.SmoothStep(0f, 1f, y / cell - y0);
                    for (int x = 0; x < size; x++)
                    {
                        int x0 = (int)(x / cell), x1 = (x0 + 1) % cells;
                        float fx = MathHelper.SmoothStep(0f, 1f, x / cell - x0);
                        float top = MathHelper.Lerp(grid[y0 * cells + x0], grid[y0 * cells + x1], fx);
                        float bottom = MathHelper.Lerp(grid[y1 * cells + x0], grid[y1 * cells + x1], fx);
                        v[y * size + x] += amp * MathHelper.Lerp(top, bottom, fy);
                    }
                }
            }
            // Rang statt Wert: gleichmäßig verteilt
            var order = Enumerable.Range(0, v.Length).OrderBy(i => v[i]).ToArray();
            for (int i = 0; i < order.Length; i++)
            {
                float rank = i / (float)(order.Length - 1);
                ref var texel = ref level[order[i]];
                if (c == 0) texel.X = rank;
                else if (c == 1) texel.Y = rank;
                else if (c == 2) texel.Z = rank;
                else texel.W = rank;
            }
        }
        var tex = new Texture2D(gd, size, size, true, SurfaceFormat.Color);
        for (int lv = 0, s = size; lv < tex.LevelCount; lv++, s /= 2)
        {
            tex.SetData(lv, null, level.Select(t => new Color(t)).ToArray(), 0, s * s);
            if (s == 1)
                break;
            // nächste Stufe: je 2×2 Texel gemittelt
            int n = s / 2;
            var next = new Vector4[n * n];
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    next[y * n + x] = (level[2 * y * s + 2 * x] + level[2 * y * s + 2 * x + 1]
                                       + level[(2 * y + 1) * s + 2 * x] + level[(2 * y + 1) * s + 2 * x + 1]) / 4f;
                }
            }
            level = next;
        }
        return tex;
    }

    /// <summary>
    /// Überträgt, was sich am Boden im Spiel ändert, ins Lebendbild des
    /// Bodenshaders: rot wie ausgetreten die Kachel ist (Tile.Wear), grün Wald -
    /// gefällter Wald wird wieder Wiese -, blau ein Fischschwarm, solange er
    /// Nahrung trägt.
    /// </summary>
    private void RefreshGroundLive()
    {
        int w = tileMap.Width;
        for (int y = 0; y < tileMap.Height; y++)
        {
            for (int x = 0; x < w; x++)
            {
                var tile = tileMap.GetTile(x, y);
                bool fish = tile.Food == FoodSource.Fish && tile.ResourceAmount > 0;
                _groundLiveData[y * w + x] = new Color(tile.Wear, tile.Type == TileType.Forest ? 1f : 0f,
                                                       fish ? 1f : 0f, 1f);
            }
        }
        _groundLive.SetData(_groundLiveData);
    }

    private void DrawTileMap(SpriteBatch spriteBatch)
    {
        // Den Boden hat DrawGround schon gezeichnet, hier kommt alles darauf.

        // Nahrungskacheln (Schafe, Beeren, Fische) und Getreidefelder als Objekte.
        // Farm-Kacheln bleiben sichtbar, auch wenn ihr Vorschot geerntet ist: sie
        // wachsen nach und werden mit zunehmendem Fortschritt heller gezeichnet.
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null) continue;
                bool isFarm = tile.Farm;
                bool isFood = tile.ResourceType == Resource.Food;
                if (!isFarm && !isFood) continue;

                var rect = TileScreenRect(x, y);
                if (!rect.Intersects(screenBounds)) continue;

                // Fische schwimmen im Wasser, das der erste Durchlauf schon
                // gezeichnet hat — Gras-Grund nur unter Schaf und Beerenbusch.
                // Eine Farm hat ihren eigenen Erdfleck; darunter nur ein neutraler
                // dunkler Grund, damit die Halme absetzen.
                bool isFish = tile.Food == FoodSource.Fish;
                bool isSheep = tile.Food == FoodSource.Sheep;
                if (isFarm && _soilTex != null && _wheatTex != null)
                    continue;
                // Schafe und Rehe mit Bild stehen in der Zeilenschicht (DrawAnimal)
                if (AnimalSprites(tile.Food).Length > 0)
                    continue;
                if (isFarm)
                    spriteBatch.Draw(px, rect, new Color(70, 55, 30));
                else if (GroundShaded)
                {
                    // Der Grund kommt schon aus dem Bodenshader - ein Grasquadrat
                    // darüber stünde als Flicken in der Wiese
                }
                else if (!isFish && _grassTex != null)
                    DrawGroundImage(spriteBatch, _grassTex, x, y, rect);
                else if (!isFish && tileTex.TryGetValue(TileType.Grassland, out var grass))
                    spriteBatch.Draw(grass, rect, Color.White);

                // Fischschwärme zieht der Bodenshader unter der Wasseroberfläche
                if (isFish && GroundShaded)
                    continue;

                var objTex = isFarm ? BuildFarmTextureCached()
                           : isFish ? BuildFishTextureCached()
                           : isSheep ? BuildSheepTextureCached()
                           : BuildBerryTextureCached();

                // Wachstumsanzeige: Farm-Kachel mit Vorrat zeigt helles Weizenfeld;
                // geerntete (Vorrat 0) dunkle Erde — die Nachwachst-Zeit (100 s)
                // ist sichtbar an der Dunkelheit; ein heller werdendes Feld
                // bedeutet wachsendes Getreide.
                Color tint = Color.White;
                if (isFarm)
                {
                    bool full = tile.ResourceType == Resource.Food && tile.ResourceAmount > 0;
                    tint = full ? new Color(255, 240, 180) : new Color(80, 60, 30);
                }
                if (objTex != null)
                    spriteBatch.Draw(objTex, rect, tint);
            }
        }

        // Baumkronen, Stein- und Goldhaufen, Schafe und Rehe als eigene Figuren, zeilenweise von
        // oben: tiefere überdecken höhere, auch über Kachelgrenzen hinweg. Tiere sind
        // oben: tiefere überdecken höhere, auch über Kachelgrenzen hinweg. Tiere sind
        // „beseelt" wie spätere Gegnereinheiten: in nicht mehr sichtbarem Dunst (erforscht,
        // aber aus der Sicht) sind lebende Tiere unsichtbar — der Dunst bleibt für Terrain,
        // Bäume und Ressourcen. Einzige Ausnahme: erlegtes Fleisch ist abgeerntete
        // Ressource und bleibt sichtbar, solange die Kachel bekannt ist, so wie die
        // Beerenbüsche und Bäume im Dunst.
        for (int y = 0; y < tileMap.Height; y++)
        {
            for (int x = 0; x < tileMap.Width; x++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null) continue;
                if (tile.Type == TileType.Forest)
                    DrawCrowns(spriteBatch, x, y);
                else if (PileSprites(tile.Type).Length > 0)
                    DrawPile(spriteBatch, x, y, PileSprites(tile.Type));
                else if (AnimalSprites(tile.Food) is { Length: > 0 } animals
                         && (tileMap.IsTileVisible(x, y, 0)
                             || (tile.Animal is { Slaughtered: true } && tileMap.IsTileExplored(x, y, 0))))
                    DrawAnimal(spriteBatch, tile, x, y, animals);
            }
        }

        // Gebäude (Stadtzentrum = 4×4 Kacheln)
        foreach (var b in tileMap.Buildings)
        {
            DrawBuilding(spriteBatch, b);
        }
    }

    /// <summary>
    /// Bodenkachel aus einem großen kachelbaren Bodenbild (Gras, Sand, Wasser):
    /// die Kachel (x, y) zeigt ihren eigenen Ausschnitt, GROUND_TEXELS Bildpixel
    /// je Welteinheit - die KI-Bilder sind grob gepixelt, bei einem Bildpixel je
    /// Welteinheit wurden die Halme riesig. Die Bilder wiederholen sich alle acht
    /// Kacheln. Gespiegelt statt wiederholt zeigte das Gras an jeder Spiegelkante
    /// einen Streifen, wo die Halmspitzen aufeinanderstießen. drift verschiebt den
    /// Ausschnitt um so viele Bildpixel; über den Bildrand hinaus geht das nur im
    /// Bodendurchgang mit wiederholender Abtastung (DrawGround).
    /// </summary>
    private void DrawGroundImage(SpriteBatch spriteBatch, Texture2D image, int x, int y, Rectangle rect,
                                 Vector2 drift = default, float alpha = 1f)
    {
        int ts = tileMap.TileSize * GROUND_TEXELS, size = image.Width;
        var source = new Rectangle((x * ts + (int)drift.X) % size, (y * ts + (int)drift.Y) % size, ts, ts);
        spriteBatch.Draw(image, rect, source, Color.White * alpha);
    }

    /// <summary>
    /// Wasser aus dem Wasserbild in zwei Lagen, die langsam in verschiedene
    /// Richtungen treiben: die Kräusel wandern und überlagern sich wie auf einem
    /// See im Wind. Beide Lagen hängen nur an animationTime, nicht an der Kachel -
    /// über Kachelgrenzen hinweg bleibt das Bild so lückenlos.
    /// </summary>
    private void DrawWater(SpriteBatch spriteBatch, int x, int y, Rectangle rect)
    {
        int size = _waterGroundTex.Width;
        float t = animationTime;
        DrawGroundImage(spriteBatch, _waterGroundTex, x, y, rect, new Vector2(t * 6f, t * 2f));
        DrawGroundImage(spriteBatch, _waterGroundTex, x, y, rect,
                        new Vector2(size / 2 + t * 2f, size / 3 + t * 5f), 0.4f);
    }

    /// <summary>Die Haufen-Sprites für Stein- und Goldkacheln, sonst keine.</summary>
    private Texture2D[] PileSprites(TileType type) => type switch
    {
        TileType.Mountain => _stoneSprites,
        TileType.GoldMine => _goldSprites,
        _ => Array.Empty<Texture2D>(),
    };

    /// <summary>
    /// Eine Feldkachel zeigt ihren Teil des Feldbilds: Felder/weizen und
    /// Felder/acker zeigen jedes das ganze Feld samt Zaun, mit denselben Reihen -
    /// das Ackerbild ist aus dem Weizenbild ausgebessert. Zuerst der abgeerntete
    /// Acker, darüber der Weizen mit Deckkraft und Ton aus WheatLook; jede Kachel
    /// wird für sich geerntet und wächst für sich nach.
    /// </summary>
    private void DrawField(SpriteBatch spriteBatch, Data.Tile tile, Rectangle rect)
    {
        spriteBatch.Draw(_soilTex, rect, FieldPart(tile, _soilTex.Width, _soilTex.Height), Color.White);
        var (growth, tint) = WheatLook(tile);
        // Sonst zeichnet DrawWheat den Weizen im Wind.
        if (growth > 0f && _wheatEffect == null)
            spriteBatch.Draw(_wheatTex, rect, FieldPart(tile, _wheatTex.Width, _wheatTex.Height), tint * growth);
    }

    /// <summary>
    /// Zeichnet den Weizen aller Feldkacheln mit dem Effekt Effects/Weizen, Kachel für Kachel,
    /// weil PartRect und TileOrigin je Kachel gelten.
    /// </summary>
    private void DrawWheat(SpriteBatch spriteBatch)
    {
        _wheatEffect.Parameters["Time"]?.SetValue(animationTime);
        spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.LinearClamp, null, null, _wheatEffect);
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var tile = tileMap.GetTile(x, y);
                if (tile == null || !tile.Farm)
                    continue;
                var rect = TileScreenRect(x, y);
                if (!rect.Intersects(screenBounds))
                    continue;
                var (growth, tint) = WheatLook(tile);
                if (growth <= 0f)
                    continue;
                var part = FieldPart(tile, _wheatTex.Width, _wheatTex.Height);
                _wheatEffect.Parameters["PartRect"]?.SetValue(new Vector4(part.X / (float)_wheatTex.Width, part.Y / (float)_wheatTex.Height, part.Width / (float)_wheatTex.Width, part.Height / (float)_wheatTex.Height));
                _wheatEffect.Parameters["TileOrigin"]?.SetValue(new Vector2(x, y));
                spriteBatch.Draw(_wheatTex, rect, part, tint * growth);
            }
        }
        spriteBatch.End();
    }

    // Die Feldbilder zeigen außerhalb des Holzzauns noch einen schmalen Grasrand,
    // der neben dem dunkleren Gras des Bodenshaders als heller Rahmen wirkt.
    private const float FIELD_MARGIN_NEAR = 8f / 384f;   // Grasrand links und oben, Anteil an der Bildgröße
    private const float FIELD_MARGIN_FAR = 3f / 384f;    // Grasrand rechts und unten

    /// <summary>
    /// Der Ausschnitt einer Feldkachel aus einem Bild, das das ganze Feld zeigt -
    /// ohne den Grasrand außerhalb des Zauns;
    /// Spalte FarmCol und Zeile FarmRow von FarmSize × FarmSize gleichen Teilen.
    /// </summary>
    private Rectangle FieldPart(Data.Tile tile, int imageWidth, int imageHeight)
    {
        int size = Math.Max(1, tile.FarmSize);
        int left = (int)MathF.Round(imageWidth * FIELD_MARGIN_NEAR), top = (int)MathF.Round(imageHeight * FIELD_MARGIN_NEAR);
        int right = imageWidth - (int)MathF.Round(imageWidth * FIELD_MARGIN_FAR), bottom = imageHeight - (int)MathF.Round(imageHeight * FIELD_MARGIN_FAR);
        int w = (right - left) / size, h = (bottom - top) / size;
        return new Rectangle(left + tile.FarmCol * w, top + tile.FarmRow * h, w, h);
    }

    /// <summary>
    /// Die Weizendeckkraft und der Weizenton einer Feldkachel. Das stehende
    /// Getreide folgt dem Vorrat: mit jeder geernteten Einheit wird es lichter,
    /// bleibt aber sichtbar, bis die Kachel leer ist. Nach der Ernte wächst es
    /// nach - erst dünn und grün, zuletzt dicht und im Ton des Feldbilds -,
    /// sichtbar über FarmRegrow, bis bei FARM_FOOD wieder voller Weizen steht.
    /// </summary>
    private (float Growth, Color Tint) WheatLook(Data.Tile tile)
    {
        if (tile.ResourceType == Resource.Food && tile.ResourceAmount > 0)
            return (MathHelper.Clamp(0.3f + 0.7f * tile.ResourceAmount / TileMap.FARM_FOOD, 0f, 1f), Color.White);
        float nach = 1f - MathHelper.Clamp(tile.FarmRegrow / TileMap.FARM_REGROW_SECONDS, 0f, 1f);
        if (nach <= 0f) return (0f, Color.White);
        return (nach, Color.Lerp(new Color(150, 200, 90), Color.White, nach));
    }

    /// <summary>
    /// Ein Stein- oder Goldhaufen je Kachel, anderthalb bis gut eindreiviertel
    /// Kachelbreiten groß und gegeneinander versetzt - Nachbarhaufen überlappen
    /// so zu einem Steinbruch oder einer Goldader statt in Reihen zu stehen.
    /// Bild, Größe und Versatz kommen fest aus dem Ortshash der Kachel, sonst
    /// zappelten die Haufen.
    /// </summary>
    private void DrawPile(SpriteBatch spriteBatch, int x, int y, Texture2D[] sprites)
    {
        var tileRect = TileScreenRect(x, y);
        int hash = unchecked(x * 73856093 ^ y * 19349663) & 0x7FFFFFFF;
        var tex = sprites[(hash >> 16) % sprites.Length];
        int width = tileRect.Width * (150 + (hash >> 20) % 26) / 100;
        int height = width * tex.Height / tex.Width;
        int jx = ((hash >> 4) % 11 - 5) * tileRect.Width / 32;
        int jy = ((hash >> 8) % 7 - 3) * tileRect.Width / 32;
        var target = new Rectangle(tileRect.Center.X - width / 2 + jx,
                                   tileRect.Bottom - tileRect.Height / 6 - height + jy, width, height);
        if (target.Intersects(screenBounds))
            spriteBatch.Draw(tex, target, Color.White);
    }

    /// <summary>
    /// Die Bilder für Schafe, Rehe, Kaninchen und Wildschweine: die freigestellten Sprites aus Tiere/,
    /// für Rehe ohne Bild das gezeichnete Reh (BuildDeerTexture). Leer heißt:
    /// das Schaf zeichnet DrawTileMap wie bisher als Kachelbild.
    /// </summary>
    private Texture2D[] AnimalSprites(FoodSource food) => food switch
    {
        FoodSource.Sheep => _sheepSprites,
        FoodSource.Deer => _deerSprites.Length > 0 ? _deerSprites
                         : _deerFallback ??= new[] { BuildDeerTextureCached() },
        FoodSource.Rabbit => _rabbitSprites,
        FoodSource.Boar => _boarSprites,
        _ => Array.Empty<Texture2D>(),
    };

    /// <summary>
    /// Ein Schaf oder Reh als Sprite in der Zeilenschicht, wie Bäume und Haufen:
    /// es steht mit den Hufen im unteren Teil seiner Kachel, Bild und Versatz
    /// kommen aus WildAnimal.Look und bleiben beim Wandern gleich. Es blickt in
    /// Laufrichtung (die Bilder sind nach rechts gemalt und werden gespiegelt)
    /// und läuft jeden Schritt sichtbar von der alten zur neuen Kachel, weich
    /// angefahren und abgebremst (Gait.Ease): es
    /// setzt dabei die Beine (Laufbilder im Wechsel, WalkPhase) und wippt
    /// leicht. Ein weicher Schatten verankert es auf dem Gras. Ein
    /// geschlachtetes Tier (WildAnimal.Slaughtered) zeigt nur noch sein Fleisch.
    /// </summary>
    private void DrawAnimal(SpriteBatch spriteBatch, Data.Tile tile, int x, int y, Texture2D[] sprites)
    {
        var tier = tile.Animal ?? new Data.WildAnimal();
        var tileRect = TileScreenRect(x, y);
        // Geschlachtet: nur noch das Fleisch, ruhig an seiner Stelle
        var meat = tile.Food switch
        {
            FoodSource.Deer => _deerMeat,
            FoodSource.Rabbit => _rabbitMeat,
            FoodSource.Boar => _boarMeat,
            _ => _sheepMeat,
        };
        bool butchered = tier.Slaughtered && meat != null;
        var tex = butchered ? meat : sprites[tier.Look % sprites.Length];
        int phase = butchered ? -1 : WalkPhase(tier.Glide);
        if (phase >= 0 && _animalWalk.TryGetValue(tex, out var walk))
            tex = walk[phase];
        int width = (int)(tileRect.Width * tile.Food switch
        {
            FoodSource.Deer => DEER_WIDTH,
            FoodSource.Rabbit => RABBIT_WIDTH,
            FoodSource.Boar => BOAR_WIDTH,
            _ => SHEEP_WIDTH,
        });
        int height = width * tex.Height / tex.Width;

        // Versatz in der Kachel aus dem Aussehen, dazu der Rest des Schritts
        int jx = ((tier.Look >> 8) % 9 - 4) * tileRect.Width / 32;
        int jy = ((tier.Look >> 12) % 7 - 3) * tileRect.Width / 32;
        float rest = MathHelper.Clamp(tier.Glide / TileMap.WILD_STEP_SECONDS, 0f, 1f);
        // Weich angefahren und abgebremst: left ist der Teil des Schritts, der noch fehlt.
        float progress = 1f - rest;
        float left = 1f - Gait.Ease(progress);
        var foot = new Vector2(tileRect.Center.X + jx + tier.FromX * left * tileRect.Width,
                               tileRect.Bottom - tileRect.Height / 4 + jy + tier.FromY * left * tileRect.Height);
        // Es wippt im Takt der Beine und nur, solange es Fahrt hat.
        float bob = rest > 0f ? MathF.Abs(MathF.Sin(Gait.Ease(progress) * MathF.PI * 2f * WALK_CYCLES_PER_STEP)) * 1.5f * cameraZoom * Gait.EaseSpeed(progress) : 0f;

        var target = new Rectangle((int)(foot.X - width / 2f), (int)(foot.Y - height - bob), width, height);
        if (!target.Intersects(screenBounds))
            return;
        if (_shadowTex != null)
        {
            int shadowWidth = (int)(width * 0.8f), shadowHeight = Math.Max(2, (int)(width * 0.22f));
            spriteBatch.Draw(_shadowTex, new Rectangle((int)foot.X - shadowWidth / 2,
                (int)foot.Y - height / 12 - shadowHeight / 2, shadowWidth, shadowHeight), Color.White);
        }
        spriteBatch.Draw(tex, target, null, Color.White, 0f, Vector2.Zero,
                         tier.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Ein Gebäudebild mit wehender Fahne: alles außer dem Fahnentuch wird wie
    /// gemalt gezeichnet (vier Teile rund um das Tuch), das Tuch in senkrechten
    /// Streifen, die eine Welle hebt und senkt - am Mast fest, zum freien Ende
    /// hin stärker (FlagWave). Alle Teile runden ihre Bildkoordinaten gleich, damit
    /// keine Fugen entstehen; jeder Streifen ist mindestens einen Bildschirmpixel breit.
    /// </summary>
    private void DrawWavingFlag(SpriteBatch spriteBatch, Texture2D sprite, Rectangle area, Rectangle cloth, float phase)
    {
        float scale = area.Width / (float)sprite.Width;
        int X(int sx) => area.X + (int)MathF.Round(sx * scale);
        int Y(int sy) => area.Y + (int)MathF.Round(sy * scale);
        void Part(Rectangle src, int dy)
        {
            if (src.Width <= 0 || src.Height <= 0) return;
            var dest = new Rectangle(X(src.Left), Y(src.Top) + dy, X(src.Right) - X(src.Left), Y(src.Bottom) - Y(src.Top));
            if (dest.Width > 0 && dest.Height > 0)
                spriteBatch.Draw(sprite, dest, src, Color.White);
        }

        Part(new Rectangle(0, 0, sprite.Width, cloth.Top), 0);
        Part(new Rectangle(0, cloth.Bottom, sprite.Width, sprite.Height - cloth.Bottom), 0);
        Part(new Rectangle(0, cloth.Top, cloth.Left, cloth.Height), 0);
        Part(new Rectangle(cloth.Right, cloth.Top, sprite.Width - cloth.Right, cloth.Height), 0);

        int strip = Math.Max(1, (int)MathF.Ceiling(1f / scale));
        float amplitude = cloth.Height * FLAG_WAVE * scale;
        for (int sx = 0; sx < cloth.Width; sx += strip)
        {
            float frei = (sx + strip / 2f) / cloth.Width;
            int dy = (int)MathF.Round(FlagWave(frei, animationTime + phase) * amplitude);
            Part(new Rectangle(cloth.Left + sx, cloth.Top, Math.Min(strip, cloth.Width - sx), cloth.Height), dy);
        }
    }

    /// <summary>
    /// Wie weit das Fahnentuch an der Stelle frei (0 am Mast, 1 am freien Ende) zur
    /// Zeit time ausschlägt, als Anteil an FLAG_WAVE Tuchhöhen: eine Welle läuft vom
    /// Mast zum Ende, am Mast steht das Tuch fest, am Ende schlägt es am weitesten aus.
    /// </summary>
    private float FlagWave(float frei, float time)
        => MathF.Sin(time * FLAG_SPEED - frei * MathF.PI * 2f) * frei;

    /// <summary>
    /// Das Flügelkreuz der Mühle dreht sich vor dem Mühlenbild um die Nabe
    /// (mill.Hub, am Bild gemessen), mill.Size Mühlenbreiten groß.
    /// </summary>
    private void DrawMillSails(SpriteBatch spriteBatch, Rectangle area, float phase, (Vector2 Hub, float Size) mill)
    {
        var hub = new Vector2(area.X + area.Width * mill.Hub.X, area.Y + area.Height * mill.Hub.Y);
        float scale = area.Width * mill.Size / _millSails.Width;
        var origin = new Vector2(_millSails.Width / 2f, _millSails.Height / 2f);
        spriteBatch.Draw(_millSails, hub, null, Color.White, MillSailAngle(animationTime + phase), origin, scale,
                         SpriteEffects.None, 0f);
    }

    /// <summary>Drehwinkel des Windrads zur Zeit time: MILL_SAIL_SPEED Bogenmaß je Sekunde, 0 bis 2π.</summary>
    private float MillSailAngle(float time) => (time * MILL_SAIL_SPEED) % MathHelper.TwoPi;

    /// <summary>
    /// Welches Laufbild ein Tier zeigt, das noch glide Sekunden unterwegs ist:
    /// 0 bis 3 für Schritt, Stand, Gegenschritt, Stand, WALK_CYCLES_PER_STEP
    /// Durchgänge je Kachelschritt; -1, solange es steht.
    /// </summary>
    private int WalkPhase(float glide)
    {
        if (glide <= 0f) return -1;
        // Die Beine folgen der weich gegangenen Strecke, sonst rutschten sie beim Anfahren und Abbremsen.
        float gelaufen = Gait.Ease(1f - MathHelper.Clamp(glide / TileMap.WILD_STEP_SECONDS, 0f, 1f));
        return Math.Min((int)(gelaufen * WALK_CYCLES_PER_STEP * 4), WALK_CYCLES_PER_STEP * 4 - 1) % 4;
    }

    /// <summary>
    /// Drei Bäume je Waldkachel - als Sprite mit Stamm, wenn geladen, sonst als
    /// gezeichnete Krone. Lage und Variante kommen fest aus dem Ortshash der
    /// Kachel – sonst zappelten die Bäume von Frame zu Frame.
    /// </summary>
    private void DrawCrowns(SpriteBatch spriteBatch, int x, int y)
    {
        int crownSize = (int)(CROWN_SIZE * cameraZoom);
        var tileRect = TileScreenRect(x, y);
        // Kronen ragen bis zu einer halben Krone über die Kachel hinaus, Bäume
        // mit Stamm bis zu drei Kronen nach oben
        var reach = new Rectangle(tileRect.X - crownSize, tileRect.Y - 3 * crownSize,
                                  tileRect.Width + 2 * crownSize, tileRect.Height + 4 * crownSize);
        if (!reach.Intersects(screenBounds))
            return;

        if (_treeSprites.Length > 0)
        {
            DrawTrees(spriteBatch, x, y, crownSize);
            return;
        }

        int hash = unchecked(x * 73856093 ^ y * 19349663) & 0x7FFFFFFF;
        for (int k = 0; k < CrownSpots.Length; k++)
        {
            int jx = (hash >> (k * 4)) % 7 - 3;
            int jy = (hash >> (k * 4 + 2)) % 7 - 3;
            var tex = crownTex[(hash >> k) % crownTex.Length];
            var center = WorldToScreen(new Vector2(x * tileMap.TileSize + CrownSpots[k].X + jx,
                                                   y * tileMap.TileSize + CrownSpots[k].Y + jy));
            spriteBatch.Draw(tex, new Rectangle((int)center.X - crownSize / 2, (int)center.Y - crownSize / 2,
                                                crownSize, crownSize), Color.White);
        }
    }

    /// <summary>
    /// Bäume als Sprites an den Stellen der Kronen: anderthalb Kronen breit,
    /// mit dem Stammfuß eine halbe Krone unter der Kronenmitte. Die Kachel wird
    /// von oben nach unten gezeichnet - tiefer stehende Bäume überdecken höhere.
    /// Jeder Baum wiegt sich im Wind, in Streifen gezeichnet (Wind.SwayStrips).
    /// </summary>
    private void DrawTrees(SpriteBatch spriteBatch, int x, int y, int crownSize)
    {
        int hash = unchecked(x * 73856093 ^ y * 19349663) & 0x7FFFFFFF;
        var spots = new List<(Vector2 Foot, Texture2D Tex, Vector2 Tile, int Look)>();
        for (int k = 0; k < CrownSpots.Length; k++)
        {
            int jx = (hash >> (k * 4)) % 7 - 3;
            int jy = (hash >> (k * 4 + 2)) % 7 - 3;
            var center = WorldToScreen(new Vector2(x * tileMap.TileSize + CrownSpots[k].X + jx,
                                                   y * tileMap.TileSize + CrownSpots[k].Y + jy));
            var tile = new Vector2(x * tileMap.TileSize + CrownSpots[k].X + jx,
                                   y * tileMap.TileSize + CrownSpots[k].Y + jy) / tileMap.TileSize;
            spots.Add((center + new Vector2(0, crownSize / 2f), _treeSprites[(hash >> k) % _treeSprites.Length], tile, hash ^ (k * 7919)));
        }
        foreach (var (foot, tex, tile, look) in spots.OrderBy(s => s.Foot.Y))
        {
            int treeWidth = (int)(crownSize * 1.5f);
            int treeHeight = treeWidth * tex.Height / tex.Width;
            // Der Baum wiegt sich im Wind - der Fuß steht, die Krone schwingt aus (Wind.TreeSway, Wind.SwayStrips).
            var target = new Rectangle((int)foot.X - treeWidth / 2, (int)foot.Y - treeHeight,
                                       treeWidth, treeHeight);
            float sway = Wind.TreeSway(tile, animationTime, look);
            foreach (var strip in Wind.SwayStrips(target, tex.Width, tex.Height, sway))
            {
                spriteBatch.Draw(tex, strip.Position, strip.Source, Color.White, 0f, Vector2.Zero, strip.Scale, SpriteEffects.None, 0f);
            }
        }
    }

    // Gedächtnis-Texturen für wiederkehrende Nahrungsobjekte
    private Texture2D _sheepTex;
    private Texture2D _berryTex;
    private Texture2D _fishTex;
    private Texture2D _farmTex;
    private Texture2D _deerTex;

    private Texture2D BuildDeerTextureCached()
    {
        if (_deerTex == null) _deerTex = BuildDeerTexture(graphicsDevice);
        return _deerTex;
    }
    private Texture2D BuildSheepTextureCached()
    {
        if (_sheepTex == null) _sheepTex = BuildSheepTexture(graphicsDevice);
        return _sheepTex;
    }
    private Texture2D BuildBerryTextureCached()
    {
        if (_berryTex == null) _berryTex = BuildBerryTexture(graphicsDevice);
        return _berryTex;
    }
    private Texture2D BuildFishTextureCached()
    {
        if (_fishTex == null) _fishTex = BuildFishTexture(graphicsDevice);
        return _fishTex;
    }

    private Texture2D BuildFarmTextureCached()
    {
        if (_farmTex == null) _farmTex = BuildFarmTexture(graphicsDevice);
        return _farmTex;
    }

    private Texture2D GetTileTexture(Data.Tile tile)
    {
        // Wasser nutzt den animierten Frame
        if (tile.Type == TileType.Water && waterTex.Length > 0)
        {
            // Variante fest nach Lage, damit sich die Glanzlichter nicht Kachel
            // für Kachel an derselben Stelle wiederholen
            int variant = (unchecked(tile.X * 73856093 ^ tile.Y * 19349663) & 0x7FFFFFFF) % WATER_VARIANTS;
            return waterTex[variant, waterAnimationFrameCounter];
        }

        if (tileTex.TryGetValue(tile.Type, out var tex))
            return tex;

        return tileTex.Values.FirstOrDefault();
    }

    /// <summary>
    /// Ufer: an jeder Kante, hinter der Land liegt, eine hellere
    /// Flachwasserkante mit Schaumlinie. So sieht man, wo das Wasser aufhört,
    /// ohne eigene Uferkacheln.
    /// </summary>
    private void DrawShore(SpriteBatch spriteBatch, int x, int y, Rectangle rect)
    {
        int shallow = Math.Max(2, rect.Width / 6);
        int foam = Math.Max(1, rect.Width / 32);
        // Halbdurchsichtig, damit die Wellen des Wasserbilds durchscheinen - deckend
        // war die Kante neben ihnen ein flaches hellblaues Band
        var shallowColor = new Color(80, 145, 200) * 0.55f;
        var foamColor = new Color(205, 228, 240) * 0.8f;

        if (IsLand(x, y - 1))   // oben
        {
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, shallow), shallowColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, rect.Width, foam), foamColor);
        }
        if (IsLand(x, y + 1))   // unten
        {
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - shallow, rect.Width, shallow), shallowColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Bottom - foam, rect.Width, foam), foamColor);
        }
        if (IsLand(x - 1, y))   // links
        {
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, shallow, rect.Height), shallowColor);
            spriteBatch.Draw(px, new Rectangle(rect.X, rect.Y, foam, rect.Height), foamColor);
        }
        if (IsLand(x + 1, y))   // rechts
        {
            spriteBatch.Draw(px, new Rectangle(rect.Right - shallow, rect.Y, shallow, rect.Height), shallowColor);
            spriteBatch.Draw(px, new Rectangle(rect.Right - foam, rect.Y, foam, rect.Height), foamColor);
        }
    }

    private bool IsLand(int x, int y)
    {
        var tile = tileMap.GetTile(x, y);
        return tile != null && tile.Type != TileType.Water;
    }

    private void DrawBuilding(SpriteBatch spriteBatch, Data.Building b)
    {
        // Nur Gebäude mit bekanntem Typ zeichnen (erstmal: Stadtzentrum)
        Rectangle rect = TileScreenRect(b.X, b.Y, b.Width, b.Height);

        // Sprites ragen nach oben über die Grundfläche hinaus (der Wachturm um
        // fast eine weitere Grundfläche) - der Sichtbereich prüft das mit
        if (rect.Intersects(screenBounds) == false &&
            new Rectangle(rect.X - 32, rect.Y - rect.Height - 32, rect.Width + 64, rect.Height * 2 + 64).Intersects(screenBounds) == false)
            return;

        bool isPlayer1 = b.OwnerId == 0;

        // Baustellen zeigen den Fortschritt, fertige Gebäude ihre eigene Grafik;
        // was hier fehlt, sieht aus wie das Stadtzentrum
        if (!b.IsComplete)
        {
            DrawConstructionSite(spriteBatch, rect, b.Construction.Progress);
            return;
        }

        // Sprite so breit wie die Grundfläche und unten bündig mit ihr - hohe
        // Gebäude wie Turm und Mühle ragen über die Kacheln dahinter hinaus
        // Jedes Zeitalter hat eigene Bilder: gezeichnet wird das des Besitzers
        var age = AgeOf(b.OwnerId);
        if (_buildingSprites.TryGetValue((b.Type, age, b.OwnerId), out var sprite))
        {
            // Die Mühle ohne gemalte Flügel, wenn das Flügelkreuz da ist und ihre
            // Nabe gemessen - es dreht sich davor
            Texture2D bare = null;
            bool windmill = b.Type == "Mühle" && _millSails != null
                            && _millBare.TryGetValue((age, b.OwnerId), out bare) && _millHubs.ContainsKey(bare);
            if (windmill)
                sprite = bare;
            int spriteHeight = rect.Width * sprite.Height / sprite.Width;
            var area = new Rectangle(rect.X, rect.Bottom - spriteHeight, rect.Width, spriteHeight);
            float phase = (b.X * 7 + b.Y * 13) * 0.37f;   // jedes Gebäude im eigenen Takt
            if (_flagCloths.TryGetValue(sprite, out var cloth))
                DrawWavingFlag(spriteBatch, sprite, area, cloth, phase);
            else
                spriteBatch.Draw(sprite, area, Color.White);
            if (windmill)
                DrawMillSails(spriteBatch, area, phase, _millHubs[bare]);
            return;
        }
        switch (b.Core.BuildingType)
        {
            case BuildingType.House:
                DrawHouse(spriteBatch, rect, isPlayer1);
                return;
            case BuildingType.Mill:
                DrawMill(spriteBatch, rect, isPlayer1);
                return;
            case BuildingType.LumberCamp:
                DrawLumberCamp(spriteBatch, rect, isPlayer1);
                return;
            case BuildingType.MiningCamp:
                DrawMiningCamp(spriteBatch, rect, isPlayer1);
                return;
            case BuildingType.Tower:
                DrawTower(spriteBatch, rect, isPlayer1);
                return;
        }

        // Fundament (grün/braun)
        Color foundation = isPlayer1 ? new Color(120, 100, 70) : new Color(140, 90, 70);
        Color roof = isPlayer1 ? new Color(170, 160, 140) : new Color(180, 140, 120);
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);

        // Entworfen für 4 × 4 Kacheln = 128 × 128 Pixel bei Zoom 1. BuildingPart
        // rechnet jede Entwurfsangabe auf die tatsächliche Größe um – vorher
        // wuchs nur die Grundplatte mit, und bei Zoom 2 blieb das Haus ein
        // kleiner Fleck im großen Erdplatz.

        // Grundplatte
        spriteDraw(spriteBatch, px, rect, foundation);
        // Innenfläche (hellere Erde)
        spriteDraw(spriteBatch, px, BuildingPart(rect, 6, 6, 116, 116), new Color(140, 125, 90));

        // Hauptgebäude (Mittelslot): 80×80
        spriteDraw(spriteBatch, px, BuildingPart(rect, 24, 24, 80, 80), wood);
        // Dach (heller, nach oben schmaler)
        spriteDraw(spriteBatch, px, BuildingPart(rect, 30, 30, 68, 40), roof);
        // Dachschrägen
        for (int i = 0; i < 40; i++)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 30 + i / 2, 70 + i, 68 - i, 2),
                       i % 8 < 4 ? new Color(190, 180, 150) : new Color(160, 150, 130));
        // Tür
        spriteDraw(spriteBatch, px, BuildingPart(rect, 58, 80, 12, 24), new Color(60, 45, 30));
        // Fenster
        spriteDraw(spriteBatch, px, BuildingPart(rect, 38, 82, 10, 10), new Color(200, 200, 220));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 80, 82, 10, 10), new Color(200, 200, 220));

        // Ecktürme (kleine Türme in den 4 Ecken), 20 × 20 im Entwurf
        foreach (var (cx, cy) in new[] { (0, 0), (108, 0), (0, 108), (108, 108) })
        {
            spriteDraw(spriteBatch, px, BuildingPart(rect, cx, cy, 20, 20), wood);
            // Kuppel (halbrund, durch Stapeln)
            for (int i = 0; i < 8; i++)
                spriteDraw(spriteBatch, px, BuildingPart(rect, cx + i, cy - i, 20 - i * 2, 2), roof);
        }

        // Pfad von der Tür nach unten. Die frühere Fassung rechnete hier eine
        // negative Höhe aus und zeichnete ihn nie.
        spriteDraw(spriteBatch, px, BuildingPart(rect, 56, 104, 16, 12), new Color(160, 145, 100));
    }

    /// <summary>
    /// Haus, 2 × 2 Kacheln: Fachwerk unter einem Satteldach, am First ein
    /// Wimpel in Spielerfarbe. Entworfen im selben 128er-Raster wie das
    /// Stadtzentrum, BuildingPart rechnet es auf die Größe um.
    /// </summary>
    private void DrawHouse(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1)
    {
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);
        Color plaster = new Color(200, 185, 150);
        Color flag = isPlayer1 ? new Color(70, 110, 210) : new Color(200, 60, 50);

        // Grund
        spriteDraw(spriteBatch, px, BuildingPart(rect, 10, 100, 108, 20), new Color(140, 125, 90));
        // Wände mit Fachwerk
        spriteDraw(spriteBatch, px, BuildingPart(rect, 18, 58, 92, 56), plaster);
        foreach (int bx in new[] { 18, 62, 106 })
            spriteDraw(spriteBatch, px, BuildingPart(rect, bx, 58, 4, 56), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 18, 84, 92, 3), wood);
        // Satteldach in Streifen, nach unten breiter
        for (int i = 0; i < 48; i += 2)
        {
            int half = 6 + (int)(i * 1.2f);
            spriteDraw(spriteBatch, px, BuildingPart(rect, 64 - half, 14 + i, 2 * half, 2),
                       i % 16 < 8 ? new Color(150, 70, 50) : new Color(130, 60, 45));
        }
        // Tür und Fenster
        spriteDraw(spriteBatch, px, BuildingPart(rect, 56, 86, 16, 28), new Color(60, 45, 30));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 28, 72, 14, 12), new Color(200, 200, 220));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 86, 72, 14, 12), new Color(200, 200, 220));
        // Wimpel in Spielerfarbe am First
        spriteDraw(spriteBatch, px, BuildingPart(rect, 63, 2, 3, 14), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 66, 2, 12, 7), flag);
    }

    /// <summary>
    /// Baustelle: ein abgesteckter Bauplatz, auf dem die Mauern mit dem
    /// Fortschritt wachsen, davor ein Gerüst, darüber ein Fortschrittsbalken.
    /// Gilt für jede Größe, BuildingPart rechnet um.
    /// </summary>
    private void DrawConstructionSite(SpriteBatch spriteBatch, Rectangle rect, float progress)
    {
        // Bauplatz mit Rand
        var plot = new Color(165, 145, 105);
        var edge = new Color(135, 115, 80);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 4, 36, 120, 88), plot);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 4, 36, 120, 3), edge);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 4, 121, 120, 3), edge);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 4, 36, 3, 88), edge);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 121, 36, 3, 88), edge);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 6, 96, 116, 26), new Color(150, 130, 95));
        // Mauern wachsen mit dem Fortschritt
        int wall = (int)(56 * progress);
        if (wall > 0)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 18, 114 - wall, 92, wall), new Color(185, 160, 115));
        // Gerüst
        var pole = new Color(115, 90, 55);
        foreach (int bx in new[] { 12, 62, 112 })
            spriteDraw(spriteBatch, px, BuildingPart(rect, bx, 44, 4, 72), pole);
        foreach (int by in new[] { 64, 88 })
            spriteDraw(spriteBatch, px, BuildingPart(rect, 10, by, 108, 3), pole);
        // Fortschrittsbalken
        spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 4, 100, 8), new Color(30, 30, 30));
        int done = (int)(100 * progress);
        if (done > 0)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 4, done, 8), new Color(230, 200, 70));
    }

    /// <summary>
    /// Mühle, 2 × 2 Kacheln: Mauer unter einem Strohdach, davor das
    /// Flügelkreuz, daneben ein Wimpel in Spielerfarbe.
    /// </summary>
    private void DrawMill(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1)
    {
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);
        Color flag = isPlayer1 ? new Color(70, 110, 210) : new Color(200, 60, 50);

        // Grund und Mauer
        spriteDraw(spriteBatch, px, BuildingPart(rect, 10, 100, 108, 20), new Color(140, 125, 90));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 30, 58, 68, 56), new Color(195, 180, 145));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 30, 58, 4, 56), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 94, 58, 4, 56), wood);
        // Strohdach in Streifen, nach unten breiter
        for (int i = 0; i < 26; i += 2)
        {
            int half = 10 + i;
            spriteDraw(spriteBatch, px, BuildingPart(rect, 64 - half, 34 + i, 2 * half, 2),
                       i % 8 < 4 ? new Color(170, 130, 65) : new Color(150, 112, 55));
        }
        // Tür und Fenster
        spriteDraw(spriteBatch, px, BuildingPart(rect, 56, 88, 16, 26), new Color(60, 45, 30));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 40, 72, 10, 10), new Color(200, 200, 220));
        // Flügelkreuz um die Nabe, mit Lattenwerk
        var canvas = new Color(230, 220, 190);
        var lattice = new Color(150, 120, 80);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 60, 2, 8, 56), canvas);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 34, 26, 60, 8), canvas);
        for (int k = 0; k < 56; k += 8)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 60, 4 + k, 8, 2), lattice);
        for (int k = 0; k < 60; k += 8)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 36 + k, 26, 2, 8), lattice);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 59, 25, 10, 10), new Color(90, 65, 40));
        // Wimpel in Spielerfarbe
        spriteDraw(spriteBatch, px, BuildingPart(rect, 104, 40, 3, 18), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 107, 40, 12, 7), flag);
    }

    /// <summary>
    /// Wachturm, 2 × 2 Kacheln: ein Steinschaft mit Fugen, Schießscharte und
    /// Tür, oben eine vorkragende Plattform mit vier Zinnen und ein Wimpel in
    /// Spielerfarbe. Licht von links wie bei den übrigen Gebäuden.
    /// </summary>
    private void DrawTower(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1)
    {
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);
        Color flag = isPlayer1 ? new Color(70, 110, 210) : new Color(200, 60, 50);
        var stone = new Color(150, 148, 140);
        var stoneDark = new Color(118, 116, 110);
        var stoneLight = new Color(175, 172, 162);

        // Grund
        spriteDraw(spriteBatch, px, BuildingPart(rect, 24, 106, 80, 16), new Color(140, 125, 90));
        // Schaft mit Steinfugen, links Licht, rechts Schatten
        spriteDraw(spriteBatch, px, BuildingPart(rect, 40, 36, 48, 80), stone);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 40, 36, 6, 80), stoneLight);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 80, 36, 8, 80), stoneDark);
        for (int fy = 48; fy < 116; fy += 12)
            spriteDraw(spriteBatch, px, BuildingPart(rect, 40, fy, 48, 2), stoneDark);
        // Schießscharte und Tür
        spriteDraw(spriteBatch, px, BuildingPart(rect, 61, 54, 6, 18), new Color(40, 35, 30));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 56, 94, 16, 22), new Color(60, 45, 30));
        // Plattform, vorkragend, mit vier Zinnen
        spriteDraw(spriteBatch, px, BuildingPart(rect, 32, 24, 64, 14), stone);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 32, 36, 64, 3), stoneDark);
        foreach (int zx in new[] { 32, 50, 68, 86 })
            spriteDraw(spriteBatch, px, BuildingPart(rect, zx, 14, 10, 10), stone);
        // Wimpel in Spielerfarbe
        spriteDraw(spriteBatch, px, BuildingPart(rect, 63, 0, 3, 14), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 66, 0, 12, 7), flag);
    }

    /// <summary>
    /// Offener Unterstand der beiden Lager: Grund, Rückwand, Pultdach, zwei
    /// Pfosten und ein Wimpel in Spielerfarbe. Davor legen DrawLumberCamp und
    /// DrawMiningCamp ihren Vorrat.
    /// </summary>
    private void DrawShed(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1,
                          Color wall, Color roof, Color roofTop)
    {
        Color wood = isPlayer1 ? new Color(100, 80, 55) : new Color(110, 75, 55);
        Color flag = isPlayer1 ? new Color(70, 110, 210) : new Color(200, 60, 50);

        spriteDraw(spriteBatch, px, BuildingPart(rect, 6, 100, 116, 22), new Color(140, 125, 90));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 52, 100, 50), wall);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 6, 38, 116, 16), roof);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 6, 38, 116, 3), roofTop);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 14, 54, 6, 50), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 108, 54, 6, 50), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 110, 20, 3, 18), wood);
        spriteDraw(spriteBatch, px, BuildingPart(rect, 113, 20, 12, 7), flag);
    }

    /// <summary>Holzfällerlager: Unterstand mit Holzstapel, daneben Hackklotz und Axt.</summary>
    private void DrawLumberCamp(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1)
    {
        DrawShed(spriteBatch, rect, isPlayer1,
                 new Color(110, 85, 55), new Color(95, 70, 45), new Color(130, 100, 65));
        // Holzstapel von vorn: Stirnseiten als Pyramide, 4-3-2-1
        int[] perRow = { 4, 3, 2, 1 };
        for (int row = 0; row < perRow.Length; row++)
        {
            for (int k = 0; k < perRow[row]; k++)
            {
                int lx = 24 + row * 7 + k * 14, ly = 90 - row * 11;
                spriteDraw(spriteBatch, px, BuildingPart(rect, lx, ly, 13, 12), new Color(125, 82, 45));
                spriteDraw(spriteBatch, px, BuildingPart(rect, lx + 2, ly + 2, 9, 8), new Color(215, 180, 125));
                spriteDraw(spriteBatch, px, BuildingPart(rect, lx + 5, ly + 5, 3, 3), new Color(160, 120, 75));
            }
        }
        // Hackklotz mit Axt
        spriteDraw(spriteBatch, px, BuildingPart(rect, 92, 92, 14, 12), new Color(125, 90, 55));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 92, 90, 14, 3), new Color(190, 150, 100));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 98, 76, 2, 15), new Color(150, 110, 60));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 99, 76, 7, 5), new Color(175, 175, 185));
    }

    /// <summary>
    /// Bergbaulager: Unterstand mit einer Halde Gold und einer Halde Stein,
    /// dazwischen eine Spitzhacke.
    /// </summary>
    private void DrawMiningCamp(SpriteBatch spriteBatch, Rectangle rect, bool isPlayer1)
    {
        DrawShed(spriteBatch, rect, isPlayer1,
                 new Color(105, 92, 75), new Color(90, 85, 85), new Color(130, 125, 125));
        // Zwei Halden, je drei Stufen mit Glanzpunkten: links Gold, rechts Stein
        foreach (var (x0, color, shine) in new[]
                 {
                     (24, new Color(215, 180, 60), new Color(240, 215, 110)),
                     (66, new Color(160, 160, 165), new Color(200, 200, 205)),
                 })
        {
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0, 96, 36, 8), color);
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0 + 4, 90, 28, 6), color);
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0 + 9, 85, 18, 5), color);
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0 + 12, 86, 4, 3), shine);
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0 + 6, 97, 4, 3), shine);
            spriteDraw(spriteBatch, px, BuildingPart(rect, x0 + 22, 92, 4, 3), shine);
        }
        // Spitzhacke
        spriteDraw(spriteBatch, px, BuildingPart(rect, 61, 66, 2, 30), new Color(150, 110, 60));
        spriteDraw(spriteBatch, px, BuildingPart(rect, 54, 64, 16, 3), new Color(175, 175, 185));
    }

    /// <summary>
    /// Rechnet ein Rechteck aus dem 128 × 128-Entwurf eines Gebäudes auf dessen
    /// tatsächliche Bildschirmgröße um. Mindestens 1 Pixel, damit schmale
    /// Streifen beim Herauszoomen nicht verschwinden.
    /// </summary>
    private static Rectangle BuildingPart(Rectangle rect, int x, int y, int width, int height)
    {
        float sx = rect.Width / 128f, sy = rect.Height / 128f;
        return new Rectangle(rect.X + (int)(x * sx), rect.Y + (int)(y * sy),
                             Math.Max(1, (int)(width * sx)), Math.Max(1, (int)(height * sy)));
    }

    private void spriteDraw(SpriteBatch sb, Texture2D tex, Rectangle r, Color c)
    {
        if (tex == null) return;
        // Clipping auf ScreenBounds, um GPU-Load zu sparen
        var clipped = Rectangle.Intersect(r, new Rectangle(0, 0, screenBounds.Width, screenBounds.Height));
        if (clipped.Width <= 0 || clipped.Height <= 0) return;
        // Zeichnen mit Tinting (px-Texture = weißes 1×1-Pixel)
        sb.Draw(tex, clipped, c);
    }

    private void DrawUnits(SpriteBatch spriteBatch)
    {
        var margin = new Rectangle(screenBounds.X - 32, screenBounds.Y - 32, screenBounds.Width + 64, screenBounds.Height + 64);

        foreach (var unit in units)
        {
            // Fremde Einheiten nur dort, wo Spieler 0 gerade hinsieht
            if (unit.OwnerId != 0)
            {
                var cell = tileMap.WorldToGrid(unit.Position);
                if (!tileMap.IsTileVisible((int)cell.X, (int)cell.Y, 0))
                    continue;
            }

            var screenPos = WorldToScreen(unit.Position);
            var size = (int)(24 * cameraZoom);

            // Culling
            if (!margin.Contains((int)screenPos.X, (int)screenPos.Y))
                continue;

            var tex = unitTex.GetValueOrDefault(unit.Type);
            var tint = unit.OwnerId == 0 ? Color.White : new Color(255, 180, 180);

            // Einheit zeichnen - Dorfbewohner als bewegtes Sprite, sofern geladen
            if (unit.Type == UnitType.Villager
                && _villagerSprites.TryGetValue((AgeOf(unit.OwnerId), unit.OwnerId), out var figure))
            {
                DrawVillager(spriteBatch, unit, figure, screenPos, size);
            }
            else if (tex != null)
            {
                spriteBatch.Draw(tex, screenPos - new Vector2(size / 2, size), null, tint, 0f, Vector2.Zero, cameraZoom, SpriteEffects.None, 0f);
            }
            else
            {
                // Fallback: 12x12 Quadrat aus px
                spriteBatch.Draw(px, screenPos - new Vector2(6, 6), null, tint, 0f, Vector2.Zero, 12 * cameraZoom, SpriteEffects.None, 0f);
            }

            // Auswahlrahmen um die ganze Figur: sie steht mit den Füßen auf
            // screenPos und reicht size Pixel nach oben
            if (unit.IsSelected)
            {
                var ringRect = new Rectangle((int)(screenPos.X - size / 2), (int)(screenPos.Y - size), size, size);
                int line = Math.Max(1, (int)cameraZoom);
                var ringColor = new Color(80, 255, 80);
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Y, size, line), null, ringColor); // oben
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Bottom - line, size, line), null, ringColor); // unten
                spriteBatch.Draw(px, new Rectangle(ringRect.X, ringRect.Y, line, size), null, ringColor); // links
                spriteBatch.Draw(px, new Rectangle(ringRect.Right - line, ringRect.Y, line, size), null, ringColor); // rechts
            }

            // Lebensbalken über dem Kopf, mit dem Zoom skaliert
            if (unit.Health < unit.MaxHealth || unit.IsSelected)
            {
                int barHeight = Math.Max(3, (int)(3 * cameraZoom));
                var barRect = new Rectangle((int)(screenPos.X - size / 2), (int)(screenPos.Y - size) - barHeight - 2,
                                            size, barHeight);
                spriteBatch.Draw(px, barRect, null, new Color(20, 20, 20));
                var healthWidth = (int)(barRect.Width * ((float)unit.Health / unit.MaxHealth));
                spriteBatch.Draw(px, new Rectangle(barRect.X, barRect.Y, healthWidth, barHeight), null, Color.Green);
            }
        }
    }

    /// <summary>
    /// Merkt sich je Einheit, ob und wohin sie sich seit dem letzten Update
    /// bewegt hat - daraus wählt DrawVillager Gehen, Arbeiten oder Stehen und die
    /// Blickrichtung. Die kurze Nachlaufzeit verhindert Flackern, wenn eine
    /// Einheit ein einzelnes Bild lang stillsteht.
    /// </summary>
    private void UpdateUnitMotion(float dt)
    {
        animationTime += dt;
        foreach (var unit in units)
        {
            if (!_unitMotion.TryGetValue(unit, out var motion))
                motion.Last = unit.Position;
            var delta = unit.Position - motion.Last;
            bool moved = delta.LengthSquared() > 0.0001f;
            if (moved)
            {
                motion.Walked += delta.Length();
                motion.MovingFor = 0.15f;
            }
            else
            {
                motion.MovingFor = Math.Max(0f, motion.MovingFor - dt);
            }
            // Blickrichtung: die Figuren zeigen sich nur von der Seite, also
            // blickt die Einheit in die Seite, in die sie geht - und bei der
            // Arbeit zu dem, woran sie arbeitet, statt in die zuletzt gehabte
            // Richtung. toward ist der Weg zu dem Punkt, auf den sie zugeht
            // oder an dem sie arbeitet.
            var toward = Vector2.Zero;
            if (unit.Path.Count > 0)
                toward = unit.Path[unit.Path.Count - 1] - unit.Position;
            else if (unit.Job?.Phase == GatherPhase.Gathering)
                toward = GridToWorld(new Vector2(unit.Job.Source.X, unit.Job.Source.Y)) - unit.Position;
            else if (unit.State == UnitState.Building && unit.BuildSite is { } site)
            {
                var center = new Vector2((site.X + site.Width / 2f) * tileMap.TileSize,
                                         (site.Y + site.Height / 2f) * tileMap.TileSize);
                toward = center - unit.Position;
            }
            motion.FacingLeft = Gait.FacingLeft(moved ? delta : Vector2.Zero, toward, motion.FacingLeft);
            motion.Last = unit.Position;
            _unitMotion[unit] = motion;
        }
    }

    /// <summary>
    /// Dorfbewohner als Sprite mit Bewegung aus dem Code: beim Gehen wippt und
    /// pendelt er, beim Sammeln und Bauen holt er im Takt mit dem Werkzeug aus,
    /// im Stehen atmet er. Er blickt in Laufrichtung, steht mit den Füßen auf
    /// screenPos und ist size Pixel groß wie die Auswahl- und Trefferfläche.
    /// Was er trägt, zeigt ein Bündel in der Farbe des Rohstoffs auf dem Rücken.
    /// </summary>
    private void DrawVillager(SpriteBatch spriteBatch, Unit unit, Texture2D figure, Vector2 screenPos, int size)
    {
        var motion = _unitMotion.GetValueOrDefault(unit);
        bool moving = motion.MovingFor > 0f;
        bool working = !moving && (unit.Job?.Phase == GatherPhase.Gathering || unit.State == UnitState.Building);
        // Jede Einheit im eigenen Takt, sonst wippt das ganze Dorf im Gleichschritt
        float t = animationTime + (unit.GetHashCode() & 0xFF) / 40f;
        var tool = ToolFor(unit);
        bool hasTool = _toolSprites.ContainsKey(tool);

        float bob = 0f, tilt = 0f, stretch = 1f;
        if (moving)
        {
            // er wippt im Takt seiner Schritte, nach der gelaufenen Strecke, und neigt sich beim Gehen leicht nach vorn, statt von Seite zu Seite zu kippeln
            bob = Gait.Bob(motion.Walked, Gait.VILLAGER_STRIDE) * 1.2f * cameraZoom;
            tilt = Gait.Lean(unit.Pace);
        }
        else if (working && !hasTool)
        {
            // Ohne Werkzeugbild holt die ganze Figur aus: kräftig nach vorn, verhalten zurück
            float swing = MathF.Sin(t * 7f);
            tilt = (swing > 0f ? swing : swing * 0.3f) * 0.2f;
        }
        else if (!working)
        {
            stretch = 1f + MathF.Sin(t * 2.2f) * 0.02f;
        }

        // Beim Gehen setzt er die Beine, im Takt des Wippens: gespreizt unten, im Stand oben
        if (moving && _villagerWalk.TryGetValue((AgeOf(unit.OwnerId), unit.OwnerId), out var walk))
            figure = walk[VillagerWalkPhase(motion.Walked)];

        // Schatten unter den Füßen - er bleibt am Boden, während die Figur wippt
        if (_shadowTex != null)
        {
            int shadowWidth = (int)(size * 0.7f), shadowHeight = Math.Max(2, (int)(size * 0.18f));
            spriteBatch.Draw(_shadowTex, new Rectangle((int)screenPos.X - shadowWidth / 2,
                (int)screenPos.Y - shadowHeight / 2, shadowWidth, shadowHeight), Color.White);
        }

        // Das Werkzeug liegt hinter der Figur: die Faust umschließt den Stiel, Kopf
        // und Hut verdecken ihn auf der Schulter. Beim Arbeiten holt es aus und
        // schlägt zu, die Figur selbst steht dabei still.
        if (hasTool)
        {
            float angle = working ? SwingAngle(ToolStyles[tool], t) : TOOL_REST;
            DrawTool(spriteBatch, tool, angle, figure, screenPos - new Vector2(0, bob), size, stretch,
                     tilt, motion.FacingLeft);
        }

        // Das Sprite blickt nach rechts; nach links gespiegelt kippt es auch gespiegelt
        float scale = (float)size / figure.Height;
        spriteBatch.Draw(figure, screenPos - new Vector2(0, bob), null, Color.White,
                         motion.FacingLeft ? -tilt : tilt,
                         new Vector2(figure.Width / 2f, figure.Height),
                         new Vector2(scale, scale * stretch),
                         motion.FacingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);

        if (unit.CarryingResource is { } carried)
        {
            var colour = carried switch
            {
                Resource.Wood => new Color(140, 92, 48),
                Resource.Food => new Color(205, 70, 55),
                Resource.Gold => new Color(232, 192, 60),
                _ => new Color(160, 160, 165),   // Stein
            };
            int bundle = Math.Max(3, (int)(size * 0.28f));
            float side = motion.FacingLeft ? 0.22f : -0.22f;   // auf dem Rücken, gegen die Laufrichtung
            int bx = (int)(screenPos.X + side * size) - bundle / 2;
            int by = (int)(screenPos.Y - size * 0.72f - bob) - bundle / 2;
            spriteBatch.Draw(px, new Rectangle(bx - 1, by - 1, bundle + 2, bundle + 2), new Color(40, 28, 16));
            spriteBatch.Draw(px, new Rectangle(bx, by, bundle, bundle), colour);
        }
    }

    /// <summary>
    /// Welches Laufbild ein Dorfbewohner zeigt, der walked Welteinheiten gegangen ist:
    /// 0 bis 3 für Schritt, Stand, Gegenschritt, Stand, je Schrittlänge Gait.VILLAGER_STRIDE
    /// ein Schritt - nach der Strecke, nicht nach der Uhr, damit die Füße nicht rutschen.
    /// </summary>
    private int VillagerWalkPhase(float walked) => Gait.WalkFrame(walked, Gait.VILLAGER_STRIDE);

    /// <summary>
    /// Das Werkzeug zur Arbeit: Hammer am Bau, Axt im Wald, Spitzhacke an Stein
    /// und Gold, Hacke auf dem Feld, Angel am Fischgrund, Sichel an Beeren und
    /// Schafen. Ohne Auftrag trägt der Dorfbewohner die Hacke.
    /// </summary>
    private Tool ToolFor(Unit unit)
    {
        if (unit.State == UnitState.Building)
            return Tool.Hammer;
        if (unit.Job is not { } job)
            return Tool.Hoe;
        return job.Resource switch
        {
            Resource.Wood => Tool.Axe,
            Resource.Stone or Resource.Gold => Tool.Pickaxe,
            _ => tileMap.GetTile(job.Source.X, job.Source.Y)?.Food switch
            {
                FoodSource.Farm => Tool.Hoe,
                FoodSource.Fish => Tool.Rod,
                _ => Tool.Sickle,
            },
        };
    }

    /// <summary>
    /// Winkel des Werkzeugs beim Arbeiten: langsam ausholen (70 % des Takts),
    /// schnell und beschleunigt zuschlagen (30 %) - so liest sich der Hieb.
    /// </summary>
    private static float SwingAngle(ToolStyle style, float t)
    {
        float phase = t * style.Rate % 1f;
        return phase < 0.7f
            ? MathHelper.SmoothStep(style.Strike, style.Raised, phase / 0.7f)
            : MathHelper.Lerp(style.Raised, style.Strike, MathF.Pow((phase - 0.7f) / 0.3f, 2f));
    }

    /// <summary>
    /// Zeichnet das Werkzeug, um die Faust gedreht. angle gilt für eine nach rechts
    /// blickende Figur; die Faust folgt der Figur mit Wippen, Neigen und Spiegeln.
    /// Gespiegelt liegt der Griff im Bild rechts - der Ursprung spiegelt mit.
    /// </summary>
    private void DrawTool(SpriteBatch spriteBatch, Tool tool, float angle, Texture2D figure, Vector2 feet,
                          int size, float stretch, float tilt, bool facingLeft)
    {
        var tex = _toolSprites[tool];
        var grip = _toolGrips[tool];
        float scale = (float)size / figure.Height;
        var fist = new Vector2((VillagerFist.X - 0.5f) * figure.Width * scale,
                               (VillagerFist.Y - 1f) * figure.Height * scale * stretch);
        if (facingLeft)
            fist.X = -fist.X;
        fist = Vector2.Transform(fist, Matrix.CreateRotationZ(facingLeft ? -tilt : tilt));

        float toolScale = size * ToolStyles[tool].Length / tex.Width;
        spriteBatch.Draw(tex, feet + fist, null, Color.White,
                         facingLeft ? -(angle + tilt) : angle + tilt,
                         facingLeft ? new Vector2(tex.Width - grip.X, grip.Y) : grip,
                         toolScale, facingLeft ? SpriteEffects.FlipHorizontally : SpriteEffects.None, 0f);
    }

    /// <summary>
    /// Griffpunkt eines Werkzeugbilds: ein Zwölftel der Breite vom linken
    /// Stielende, in der Mitte der dort deckenden Pixel - die Werkzeuge liegen
    /// waagerecht, aber nicht jeder Stiel genau auf halber Höhe.
    /// </summary>
    private static Vector2 ToolGrip(Texture2D tex)
    {
        var pixels = new Color[tex.Width * tex.Height];
        tex.GetData(pixels);
        int column = tex.Width / 12, sum = 0, count = 0;
        for (int y = 0; y < tex.Height; y++)
        {
            if (pixels[y * tex.Width + column].A > 128)
            {
                sum += y;
                count++;
            }
        }
        return new Vector2(column, count > 0 ? (float)sum / count : tex.Height / 2f);
    }

    /// <summary>Weicher, ovaler Schatten: in der Mitte dunkel, zum Rand durchsichtig.</summary>
    private static Texture2D BuildShadowTexture(GraphicsDevice gd)
    {
        var b = new TextureBuilder(32, 12);
        for (int y = 0; y < b.H; y++)
        {
            for (int x = 0; x < b.W; x++)
            {
                float dx = (x + 0.5f - b.W / 2f) / (b.W / 2f), dy = (y + 0.5f - b.H / 2f) / (b.H / 2f);
                float d = dx * dx + dy * dy;
                if (d < 1f)
                    b.Set(x, y, Color.Black * (0.35f * (1f - d)));
            }
        }
        return b.Build(gd);
    }

    /// <summary>
    /// Nebel des Krieges für Spieler 0: nie Gesehenes schwarz, einmal Gesehenes
    /// abgedunkelt – dort zeigt die Karte den letzten bekannten Stand.
    /// </summary>
    private void DrawFog(SpriteBatch spriteBatch)
    {
        var dimmed = new Color(0, 0, 0, 110);
        for (int x = 0; x < tileMap.Width; x++)
        {
            for (int y = 0; y < tileMap.Height; y++)
            {
                var rect = TileScreenRect(x, y);
                if (!rect.Intersects(screenBounds))
                    continue;

                if (!tileMap.IsTileExplored(x, y, 0))
                    spriteBatch.Draw(px, rect, Color.Black);
                else if (!tileMap.IsTileVisible(x, y, 0))
                    spriteBatch.Draw(px, rect, dimmed);
            }
        }
    }

    private void DrawUI(SpriteBatch spriteBatch)
    {
        if (ScreenManager.Font == null) return;

        // Obere Leiste
        var topBarRect = new Rectangle(0, 0, screenBounds.Width, 34);
        spriteBatch.Draw(px, topBarRect, new Color(94, 76, 48));
        spriteBatch.Draw(px, new Rectangle(0, 32, screenBounds.Width, 2), new Color(52, 40, 24));

        // Ressourcen
        int startX = 10;
        int iconSize = 12;
        int spacing = 10;
        int resourceWidth = 110;

        // Food
        var foodRect = new Rectangle(startX, 6, iconSize, iconSize);
        spriteBatch.Draw(px, foodRect, new Color(190, 70, 55));
        var foodText = player1.Resources[Resource.Food].ToString();
        spriteBatch.DrawString(ScreenManager.Font, foodText, new Vector2(startX + iconSize + spacing, 6), Color.White);

        // Wood
        var woodRect = new Rectangle(startX + resourceWidth, 6, iconSize, iconSize);
        spriteBatch.Draw(px, woodRect, new Color(130, 95, 55));
        var woodText = player1.Resources[Resource.Wood].ToString();
        spriteBatch.DrawString(ScreenManager.Font, woodText, new Vector2(startX + resourceWidth + iconSize + spacing, 6), Color.White);

        // Gold
        var goldRect = new Rectangle(startX + resourceWidth * 2, 6, iconSize, iconSize);
        spriteBatch.Draw(px, goldRect, new Color(215, 180, 60));
        var goldText = player1.Resources[Resource.Gold].ToString();
        spriteBatch.DrawString(ScreenManager.Font, goldText, new Vector2(startX + resourceWidth * 2 + iconSize + spacing, 6), Color.White);

        // Stone
        var stoneRect = new Rectangle(startX + resourceWidth * 3, 6, iconSize, iconSize);
        spriteBatch.Draw(px, stoneRect, new Color(160, 160, 165));
        var stoneText = player1.Resources[Resource.Stone].ToString();
        spriteBatch.DrawString(ScreenManager.Font, stoneText, new Vector2(startX + resourceWidth * 3 + iconSize + spacing, 6), Color.White);

        // Bevölkerung und Zeitalter
        var popText = $"Bev. {player1.PopulationCount}/{player1.PopulationLimit}";
        var ageText = AgeRules.NameOf(player1.Ages.Current);
        var popSize = ScreenManager.Font.MeasureString(popText);
        var ageSize = ScreenManager.Font.MeasureString(ageText);
        var textX = screenBounds.Width - 10 - popSize.X - ageSize.X - 10;
        // Rot, sobald das Limit erreicht ist - dann steht die Produktion
        var popColor = player1.PopulationCount >= player1.PopulationLimit ? LimitColor : Color.White;
        spriteBatch.DrawString(ScreenManager.Font, popText, new Vector2(textX, 6), popColor);
        spriteBatch.DrawString(ScreenManager.Font, ageText, new Vector2(textX + popSize.X + 10, 6), Color.White);

        // Mitte der oberen Leiste: Hinweis oder Ausbildung im Stadtzentrum
        string centerText = null;
        var centerColor = Color.White;
        var townCenter = TownCenterOf(0);
        if (hudMessageTimer > 0f)
        {
            centerText = hudMessage;
            centerColor = Color.Orange;
        }
        else if (player1.Ages.Target is { } ageTarget)
        {
            // Aufstieg im Stadtzentrum - die Ausbildung wartet so lange
            centerText = $"Aufstieg in die {AgeRules.NameOf(ageTarget)} {(int)(player1.Ages.Progress * 100)} %";
            if (townCenter != null && townCenter.Training.Count > 0)
                centerText += $"  (Dorfbewohner warten: {townCenter.Training.Count})";
        }
        else if (townCenter != null && townCenter.Training.Count > 0)
        {
            if (townCenter.Training.IsBlocked)
            {
                centerText = "Bevölkerungslimit erreicht - Haus bauen mit H";
                centerColor = LimitColor;
            }
            else
            {
                centerText = $"Dorfbewohner {(int)(townCenter.Training.Progress * 100)} %";
                if (townCenter.Training.Count > 1)
                    centerText += $"  (+{townCenter.Training.Count - 1})";
            }
        }
        if (centerText != null)
        {
            var centerSize = ScreenManager.Font.MeasureString(centerText);
            spriteBatch.DrawString(ScreenManager.Font, centerText,
                new Vector2((screenBounds.Width - centerSize.X) / 2f, 6), centerColor);
        }

        // Untere Leiste wie in der Spezifikation (Kommandoleiste) und im
        // Referenzbild docs/overview.jpg: Befehlstasten links, Einheiteninfo in
        // der Mitte, Minimap rechts. Klicks hier gelten nie der Karte (IsOverHud).
        int barY = screenBounds.Height - HUD_BOTTOM_HEIGHT;
        var bottomBarRect = new Rectangle(0, barY, screenBounds.Width, HUD_BOTTOM_HEIGHT);
        spriteBatch.Draw(px, bottomBarRect, new Color(94, 76, 48));
        spriteBatch.Draw(px, new Rectangle(0, barY, screenBounds.Width, 2), new Color(140, 115, 75));

        // Die erhöhte Fläche hinter der Minimap, im Ton der Leiste mit heller Kante
        var minimapPanel = MinimapPanel();
        spriteBatch.Draw(px, minimapPanel, new Color(94, 76, 48));
        spriteBatch.Draw(px, new Rectangle(minimapPanel.X, minimapPanel.Y, minimapPanel.Width, 2), new Color(140, 115, 75));
        spriteBatch.Draw(px, new Rectangle(minimapPanel.X, minimapPanel.Y, 2, minimapPanel.Height), new Color(140, 115, 75));

        var minimapRect = MinimapRect();
        DrawMinimap(spriteBatch, minimapRect);

        // Quadratische Befehlstasten — dieselbe Wirkung wie Q / H / M / F / B / G / .
        LayoutButtons();
        var cursor = new Point(Mouse.GetState().X, Mouse.GetState().Y);
        foreach (var b in _buttons)
            DrawCommandButton(spriteBatch, b, cursor);

        // Einheiteninfo in der Mitte, zwischen Tasten und Minimap. Zeigt die
        // Maus auf eine Befehlstaste, steht hier ihr Name mit Kürzel und Kosten -
        // die Tasten selbst tragen nur Symbol und Buchstaben
        var hovered = _buttons.FirstOrDefault(b => b.Rect.Contains(cursor));
        string infoLine;
        if (hovered.Name != null)
            infoLine = $"{hovered.Name} ({hovered.Label}): {hovered.Hint}";
        else if (selectedUnits.Count > 1)
            infoLine = $"{selectedUnits.Count} Einheiten ausgewählt";
        else if (selectedUnits.Count == 1)
        {
            var u = selectedUnits[0];
            infoLine = $"{u.Name}  {u.Health}/{u.MaxHealth} LP";
            if (u.Job != null)
                infoLine += $"   Traglast {u.CarryingAmount}/{GatherJob.CARRY_CAPACITY} {ResourceName(u.Job.Resource)}";
            if (u.BuildSite is { IsComplete: false } site)
                infoLine += $"   baut {site.Type} {(int)(site.Construction.Progress * 100)} %";
        }
        else if (placing != null)
        {
            var entry = BuildMenu.First(e => e.Type == placing.Value);
            infoLine = $"{entry.Name} setzen: Linksklick ({CostText(BuildingRules.CostOf(entry.Type))})   Abbrechen: Rechtsklick";
        }
        else
        {
            infoLine = "Links: bewegen/sammeln/bauen   Rechts: auswählen   Ziehen: Karte   Q/H/M/F/B/G/T: Befehle oben   A: Zeitalter   .: untätig";
        }
        // Im kleinsten Fenster ist der Platz schmaler als der Hilfetext - dann
        // bricht er an den Dreifach-Leerzeichen um, statt unter die Minimap zu laufen
        int infoLeft = _buttons[^1].Rect.Right + 24;
        float infoWidth = minimapRect.Left - 24 - infoLeft;
        var lines = WrapHudText(infoLine, infoWidth);
        for (int i = 0; i < lines.Count; i++)
            spriteBatch.DrawString(ScreenManager.Font, lines[i],
                new Vector2(infoLeft, barY + 18 + i * ScreenManager.Font.LineSpacing), Color.White);
    }

    /// <summary>
    /// Zerlegt einen Leistentext an seinen Dreifach-Leerzeichen in Zeilen von
    /// höchstens <paramref name="maxWidth"/> Pixeln. Ein einzelner zu langer
    /// Abschnitt bleibt eine eigene Zeile.
    /// </summary>
    private List<string> WrapHudText(string text, float maxWidth)
    {
        var lines = new List<string>();
        string line = "";
        foreach (var part in text.Split("   "))
        {
            string candidate = line.Length == 0 ? part : line + "   " + part;
            if (line.Length > 0 && ScreenManager.Font.MeasureString(candidate).X > maxWidth)
            {
                lines.Add(line);
                line = part;
            }
            else
            {
                line = candidate;
            }
        }
        if (line.Length > 0)
            lines.Add(line);
        return lines;
    }

    // Befehlstasten in der unteren Leiste: ein mal anlegen (LayoutButtons),
    // dann nur noch den Zustand zeichnen. Der goldene Rahmen folgt placing.
    private void LayoutButtons()
    {
        // Neu anlegen, wenn sich die Fensterhöhe geändert hat - sonst blieben
        // die Tasten nach dem Vergrößern an der alten Stelle über der Karte -
        // oder die Auswahl: die Bautasten gibt es nur, solange ein Dorfbewohner
        // ausgewählt ist. Q (Stadtzentrum) und „." brauchen keine Auswahl.
        // Ebenso beim Zeitalter: es schaltet Gebäude frei, und A (Aufstieg)
        // gibt es nur, solange ein nächstes Zeitalter folgt.
        bool builders = VillagerSelected();
        var age = player1.Ages.Current;
        if (_buttons.Count > 0 && _buttonsLayoutHeight == screenBounds.Height
            && _buttonsLayoutBuilders == builders && _buttonsLayoutAge == age)
            return;
        _buttons.Clear();
        _buttonsLayoutHeight = screenBounds.Height;
        _buttonsLayoutBuilders = builders;
        _buttonsLayoutAge = age;
        int size = 60, gap = 6;
        int x = 10, y = screenBounds.Height - HUD_BOTTOM_HEIGHT + 14;
        AddButton(ref x, size, gap, y, "Q", "Dorfbewohner", "25 Nahrung, 25 s", TrainVillager);
        if (AgeRules.Next(age) is { } next)
            AddButton(ref x, size, gap, y, "A", "Zeitalter",
                      $"{AgeRules.NameOf(next)}, {CostText(AgeRules.CostOf(next))}", AdvanceAge);
        if (builders)
        {
            foreach (var e in BuildMenu.Where(e => AgeRules.IsUnlocked(e.Type, age)))
                AddButton(ref x, size, gap, y, e.Key.ToString().ToUpperInvariant(),
                          e.Name, CostText(BuildingRules.CostOf(e.Type)), e.Type,
                          () => TogglePlacing(e.Type));
        }
        AddButton(ref x, size, gap, y, ".", "Untätig", "nächster untätiger Dorfbewohner",
                  SelectNextIdleVillager);
    }

    private void AddButton(ref int x, int size, int gap, int y, string label, string name,
                           string hint, Action action)
    {
        AddButton(ref x, size, gap, y, label, name, hint, null, action);
    }

    private void AddButton(ref int x, int size, int gap, int y, string label, string name,
                           string hint, BuildingType? type, Action action)
    {
        // Mit Symbol quadratisch - Name und Kosten nennt die Leiste beim Zeigen.
        // Ohne Symbol (Datei fehlt, tools/spielablauf) so breit wie der Name:
        // „Bergbaulager" ist breiter als eine quadratische Taste
        _buttonIcons.TryGetValue(label, out var icon);
        int width = icon != null
            ? size
            : Math.Max(size, (int)(ScreenManager?.Font?.MeasureString(name).X ?? 0f) + 8);
        _buttons.Add(new CommandButton
        {
            Rect = new Rectangle(x, y, width, size),
            Label = label, Name = name, Hint = hint,
            Type = type, Action = action, Icon = icon,
        });
        x += width + gap;
    }

    private void DrawCommandButton(SpriteBatch sb, CommandButton b, Point cursor)
    {
        var r = b.Rect;
        bool active = b.Type.HasValue && placing == b.Type.Value;
        bool hover = r.Contains(cursor);
        bool pressed = hover && Mouse.GetState().LeftButton == ButtonState.Pressed;
        sb.Draw(px, new Rectangle(r.Left - 1, r.Top - 1, r.Width + 2, r.Height + 2),
                active ? new Color(120, 92, 40) : new Color(38, 28, 16));
        var face = active
            ? new Color(150, 118, 55)
            : pressed ? new Color(96, 76, 46)
            : hover   ? new Color(140, 116, 76)
                      : new Color(118, 94, 58);
        sb.Draw(px, r, face);
        if (b.Icon != null)
        {
            // Symbol bis auf einen schmalen Rand; beim Zeigen heller, gedrückt dunkler
            var inner = new Rectangle(r.Left + 3, r.Top + 3, r.Width - 6, r.Height - 6);
            sb.Draw(b.Icon, inner, pressed ? new Color(170, 170, 170)
                                 : hover || active ? Color.White
                                                   : new Color(222, 222, 222));
            // Tastenbuchstabe oben links, mit Schatten lesbar auf dem Bild
            var at = new Vector2(r.Left + 5, r.Top + 2);
            sb.DrawString(ScreenManager.Font, b.Label, at + new Vector2(1, 1), Color.Black);
            sb.DrawString(ScreenManager.Font, b.Label, at, active ? new Color(255, 228, 120) : Color.White);
            if (active)
            {
                // Setzmodus: goldener Rahmen um die Taste
                var gold = new Color(255, 215, 0);
                sb.Draw(px, new Rectangle(r.Left, r.Top, r.Width, 2), gold);
                sb.Draw(px, new Rectangle(r.Left, r.Bottom - 2, r.Width, 2), gold);
                sb.Draw(px, new Rectangle(r.Left, r.Top, 2, r.Height), gold);
                sb.Draw(px, new Rectangle(r.Right - 2, r.Top, 2, r.Height), gold);
            }
            return;
        }
        var labelSize = ScreenManager.Font.MeasureString(b.Label);
        sb.DrawString(ScreenManager.Font, b.Label,
            new Vector2(r.Left + (r.Width - labelSize.X) / 2f, r.Top + 4),
            active ? new Color(255, 228, 120) : Color.White);
        // Unterkante des Namens auf die Unterkante der Taste - mit festen
        // 13 px ragte die gut 20 px hohe Schrift unten hinaus
        var nameSize = ScreenManager.Font.MeasureString(b.Name);
        sb.DrawString(ScreenManager.Font, b.Name,
            new Vector2(r.Left + (r.Width - nameSize.X) / 2f, r.Bottom - nameSize.Y),
            active ? new Color(240, 210, 140) : new Color(214, 200, 170));
    }

    /// <summary>Klicks auf die Leiste — einmal beim Loslassen, kein Drag.</summary>
    private void HandleHudButtons(Point cursor)
    {
        foreach (var b in _buttons)
            if (b.Rect.Contains(cursor))
            {
                b.Action();
                return;
            }
        if (_minimapRect != Rectangle.Empty && _minimapRect.Contains(cursor))
            CenterCameraOnMinimapPoint(cursor);
    }

    // --- Minimap (C6) -----------------------------------------------------------
    // Dieselbe Ansicht wie die Spielkarte - Draufsicht, Norden oben: der
    // Kartenpunkt (x, y) in Kacheln liegt im Feld r bei
    //     px = r.Left + x * r.Width / mapW ,  py = r.Top + y * r.Height / mapH
    // Oben links die Ecke (0, 0), unten rechts (mapW, mapH).

    /// <summary>
    /// Feld der Minimap rechts unten: MINIMAP_SIZE Pixel bei jeder Kartengröße,
    /// unten bündig in der Leiste und nach oben über sie hinaus - 80 % größer als
    /// die frühere, die in die 200-px-Leiste passen musste. Je Kachel sind das
    /// Bruchteile von Pixeln; DrawMinimap setzt jede Kachel aus ihren Eckpunkten
    /// zusammen, lückenlos. MinimapPanel ist die Fläche dahinter.
    /// </summary>
    private Rectangle MinimapRect()
    {
        int mapMax = Math.Max(tileMap.Width, tileMap.Height);
        int w = MINIMAP_SIZE * tileMap.Width / mapMax, h = MINIMAP_SIZE * tileMap.Height / mapMax;
        return new Rectangle(screenBounds.Width - w - 12, screenBounds.Height - MINIMAP_MARGIN - h, w, h);
    }

    /// <summary>
    /// Die erhöhte Fläche hinter der Minimap bis zum Fensterrand: sieht aus wie die
    /// Leiste und zählt für Klicks zu ihr (IsOverHud), auch wo sie über die Leiste
    /// hinausragt.
    /// </summary>
    private Rectangle MinimapPanel()
    {
        var r = MinimapRect();
        int rand = MINIMAP_MARGIN + 6;
        return new Rectangle(r.Left - rand, r.Top - rand, screenBounds.Width - r.Left + rand,
                             screenBounds.Height - r.Top + rand);
    }

    /// <summary>Kartenpunkt (Kacheln) → Bildschirmkoordinaten innerhalb der Minimap.</summary>
    private Vector2 MinimapPoint(float x, float y, Rectangle r)
        => new Vector2(r.Left + x * r.Width / tileMap.Width,
                       r.Top + y * r.Height / tileMap.Height);

    /// <summary>
    /// Klick in die Minimap: die Kamera mittig über den angeklickten Kartenpunkt.
    /// Umkehrung von MinimapPoint.
    /// </summary>
    private void CenterCameraOnMinimapPoint(Point cursor)
    {
        var r = _minimapRect;
        float gx = MathHelper.Clamp((cursor.X - r.Left) * (float)tileMap.Width / r.Width, 0f, tileMap.Width);
        float gy = MathHelper.Clamp((cursor.Y - r.Top) * (float)tileMap.Height / r.Height, 0f, tileMap.Height);
        var world = new Vector2(gx, gy) * tileMap.TileSize;
        cameraPosition = new Vector2(screenBounds.Width / (2f * cameraZoom),
                                     screenBounds.Height / (2f * cameraZoom)) - world;
        ClampCamera();
    }

    /// <summary>
    /// Zeichnet die Minimap: dunkler Grund, erkundetes Gelände, eigene Einheiten
    /// weiß, fremde rot (nur in Sicht), Kameraausschnitt als weißer Rahmen.
    /// </summary>
    private void DrawMinimap(SpriteBatch sb, Rectangle r)
    {
        sb.Draw(px, new Rectangle(r.Left - 2, r.Top - 2, r.Width + 4, r.Height + 4),
                new Color(30, 22, 12));
        sb.Draw(px, r, new Color(12, 12, 12));

        int mapW = tileMap.Width, mapH = tileMap.Height;
        for (int y = 0; y < mapH; y++)
        {
            for (int x = 0; x < mapW; x++)
            {
                if (!tileMap.IsTileExplored(x, y, 0))
                    continue;
                var tile = tileMap.GetTile(x, y);
                Color c = string.IsNullOrEmpty(tile.Building) ? IsoCol[(int)tile.Type] : IsoBuilding;
                if (tile.Food == FoodSource.Sheep && tile.ResourceAmount > 0)
                    c = IsoSheep;
                else if (tile.Farm && tile.ResourceAmount > 0)
                    c = IsoFarm;
                if (!tileMap.IsTileVisible(x, y, 0))
                    c = new Color(c.R * 96 / 255, c.G * 96 / 255, c.B * 96 / 255);
                // Aus den Eckpunkten der Kachel - schließt lückenlos an die Nachbarn an
                var a = MinimapPoint(x, y, r);
                var b = MinimapPoint(x + 1, y + 1, r);
                sb.Draw(px, new Rectangle((int)a.X, (int)a.Y, (int)b.X - (int)a.X, (int)b.Y - (int)a.Y), c);
            }
        }

        // Einheiten - Position ist die Mitte der Figur in Weltpixeln. Fremde
        // nur in Sicht, sonst verriete die Minimap, was der Nebel verbirgt.
        float ts = tileMap.TileSize;
        foreach (var u in units)
        {
            float ux = u.Position.X / ts, uy = u.Position.Y / ts;
            if (u.OwnerId != 0 && !tileMap.IsTileVisible((int)ux, (int)uy, 0))
                continue;
            var p = MinimapPoint(ux, uy, r);
            sb.Draw(px, new Rectangle((int)p.X - 1, (int)p.Y - 1, 3, 3),
                    u.OwnerId == 0 ? Color.White : new Color(235, 70, 60));
        }

        // Kameraausschnitt: der Kartenbereich zwischen den beiden Leisten
        float gx0 = MathHelper.Clamp(-cameraPosition.X / ts, 0f, mapW);
        float gx1 = MathHelper.Clamp((-cameraPosition.X + screenBounds.Width / cameraZoom) / ts, 0f, mapW);
        float gy0 = MathHelper.Clamp((-cameraPosition.Y + HUD_TOP_HEIGHT / cameraZoom) / ts, 0f, mapH);
        float gy1 = MathHelper.Clamp((-cameraPosition.Y + (screenBounds.Height - HUD_BOTTOM_HEIGHT) / cameraZoom) / ts, 0f, mapH);
        var topLeft = MinimapPoint(gx0, gy0, r);
        var bottomRight = MinimapPoint(gx1, gy1, r);
        DrawMinimapFrame(sb, new Rectangle((int)topLeft.X, (int)topLeft.Y,
                                           (int)bottomRight.X - (int)topLeft.X,
                                           (int)bottomRight.Y - (int)topLeft.Y), Color.White);

        _minimapRect = r;
    }

    /// <summary>Rahmen aus vier 1-px-Linien, innen am Rechteck entlang.</summary>
    private void DrawMinimapFrame(SpriteBatch sb, Rectangle f, Color color)
    {
        sb.Draw(px, new Rectangle(f.Left, f.Top, f.Width, 1), color);
        sb.Draw(px, new Rectangle(f.Left, f.Bottom - 1, f.Width, 1), color);
        sb.Draw(px, new Rectangle(f.Left, f.Top, 1, f.Height), color);
        sb.Draw(px, new Rectangle(f.Right - 1, f.Top, 1, f.Height), color);
    }

    // --- Ende Minimap ------------------------------------------------------------


        private static string ResourceName(Resource resource) => resource switch
    {
        Resource.Food => "Nahrung",
        Resource.Wood => "Holz",
        Resource.Gold => "Gold",
        Resource.Stone => "Stein",
        _ => resource.ToString()
    };

    private Texture2D BuildArcherTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);

        // Körper
        int bodyX = 7, bodyY = 6, bodyW = 10, bodyH = 14;
        b.FillRect(bodyX, bodyY, bodyW, bodyH, new Color(225, 190, 150)); // Haut
        b.OutlineRect(bodyX, bodyY, bodyW, bodyH, new Color(40, 30, 25)); // Umriss

        // Tunika über dem Unterkörper (Haut bleibt nur als Kopf/Arme sichtbar)
        b.FillRect(bodyX + 1, bodyY + 4, bodyW - 2, bodyH - 5, playerColor);

        // Kapuze
        b.FillRect(bodyX - 1, bodyY - 2, bodyW + 2, 3, playerColor);

        // Bogen (vertikaler Halbbogen links)
        int bowX = 2, bowY = 6;
        b.FillCircle(bowX, bowY + 5, 4, new Color(120, 80, 40)); // Bogen
        b.FillRect(bowX - 1, bowY + 3, 1, 5, new Color(220, 210, 190)); // Sehne

        // Schatten
        b.FillRect(6, 21, 12, 2, new Color(0, 0, 0, 70));

        return b.Build(gd);
    }

    private Texture2D BuildCavalryTexture(GraphicsDevice gd, Color playerColor)
    {
        var b = new TextureBuilder(24, 24);

        // Pferd
        int horseX = 3, horseY = 8, horseW = 16, horseH = 8;
        b.FillRect(horseX, horseY, horseW, horseH, new Color(110, 75, 50)); // Pferdekörper
        b.OutlineRect(horseX, horseY, horseW, horseH, new Color(40, 30, 25)); // Umriss

        // Beine
        b.FillRect(horseX + 1, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 5, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 9, horseY + 7, 2, 2, new Color(110, 75, 50));
        b.FillRect(horseX + 13, horseY + 7, 2, 2, new Color(110, 75, 50));

        // Kopf
        b.FillRect(horseX + 16, horseY + 4, 3, 3, new Color(110, 75, 50));

        // Schweif
        b.FillRect(horseX - 1, horseY + 6, 2, 1, new Color(110, 75, 50));

        // Reiter
        int riderX = 9, riderY = 4;
        b.FillRect(riderX, riderY, 6, 4, playerColor); // Oberkörper
        b.FillRect(riderX + 2, riderY - 1, 2, 2, new Color(150, 150, 160)); // Helm

        // Schatten
        b.FillRect(4, 21, 16, 2, new Color(0, 0, 0, 70));

        return b.Build(gd);
    }

    private Vector2 WorldToScreen(Vector2 worldPos)
    {
        return new Vector2(
            (worldPos.X + cameraPosition.X) * cameraZoom,
            (worldPos.Y + cameraPosition.Y) * cameraZoom);
    }

    /// <summary>
    /// Bildschirmrechteck eines Kachelbereichs, aus den Weltecken berechnet – so schließen benachbarte Kacheln bei jedem Zoom lückenlos aneinander und liegen genau dort, wo Maus und Einheiten sie erwarten.
    /// </summary>
    private Rectangle TileScreenRect(int x, int y, int width = 1, int height = 1)
    {
        var topLeft = WorldToScreen(new Vector2(x * tileMap.TileSize, y * tileMap.TileSize));
        var bottomRight = WorldToScreen(new Vector2((x + width) * tileMap.TileSize,
                                                    (y + height) * tileMap.TileSize));
        int left = (int)MathF.Floor(topLeft.X);
        int top = (int)MathF.Floor(topLeft.Y);
        return new Rectangle(left, top,
                             (int)MathF.Floor(bottomRight.X) - left,
                             (int)MathF.Floor(bottomRight.Y) - top);
    }

    /// <summary>
    /// Hält den sichtbaren Ausschnitt innerhalb der Karte.
    ///
    /// Aus <c>WorldToScreen(w) = (w + cameraPosition) * cameraZoom</c> folgt,
    /// dass der sichtbare Weltbereich bei <c>-cameraPosition</c> beginnt und
    /// <c>screenBounds / cameraZoom</c> breit ist. Daraus ergeben sich die
    /// Grenzen unten.
    ///
    /// Ist die Karte kleiner als das Fenster — bei 64×32 = 2048 Pixeln und
    /// Zoom 1 der Normalfall —, wird sie stattdessen zentriert. Ohne das
    /// klebte die Karte in einer Ecke und der Rest blieb leere Fläche.
    /// </summary>
    private void ClampCamera()
    {
        float mapWidth = tileMap.Width * tileMap.TileSize;
        float mapHeight = tileMap.Height * tileMap.TileSize;

        float viewWidth = screenBounds.Width / cameraZoom;
        float viewHeight = screenBounds.Height / cameraZoom;

        cameraPosition.X = viewWidth >= mapWidth
            ? (viewWidth - mapWidth) / 2f
            : MathHelper.Clamp(cameraPosition.X, viewWidth - mapWidth, 0f);

        cameraPosition.Y = viewHeight >= mapHeight
            ? (viewHeight - mapHeight) / 2f
            : MathHelper.Clamp(cameraPosition.Y, viewHeight - mapHeight, 0f);
    }

    /// <summary>
    /// Hält den sichtbaren Ausschnitt gleich groß, wenn sich die Fensterhöhe
    /// ändert: der Zoom wächst und schrumpft im Verhältnis von ZoomScale. Das
    /// zählt schon beim Start - DesktopGL gibt dem Fenster seine Größe mitunter
    /// erst nach LoadContent, dann begann das Spiel mit Zoom 1 statt 1,25.
    /// </summary>
    private void SyncZoomToWindow()
    {
        float scale = ZoomScale;
        if (scale == _appliedZoomScale)
            return;
        cameraZoom *= scale / _appliedZoomScale;
        _appliedZoomScale = scale;
    }

    /// <summary>
    /// Zoomt um <paramref name="factor"/> auf den Bildschirmpunkt
    /// <paramref name="cursor"/> zu, innerhalb von MinZoom und MaxZoom. Der
    /// Weltpunkt unter dem Zeiger bleibt dabei an Ort und Stelle.
    /// </summary>
    private void ZoomAt(Vector2 cursor, float factor)
    {
        var worldUnderCursor = ScreenToWorld(cursor);
        float newZoom = MathHelper.Clamp(cameraZoom * factor, MinZoom, MaxZoom);
        if (newZoom == cameraZoom)
            return;
        cameraZoom = newZoom;

        // Kamera so nachführen, dass derselbe Weltpunkt wieder unter dem
        // Cursor liegt. Umkehrung von ScreenToWorld:
        //   welt = bildschirm / zoom - kamera
        cameraPosition = cursor / cameraZoom - worldUnderCursor;
    }

    private Vector2 ScreenToWorld(Vector2 screenPos)
    {
        return new Vector2(
            screenPos.X / cameraZoom - cameraPosition.X,
            screenPos.Y / cameraZoom - cameraPosition.Y);
    }

    private Vector2 WorldToGrid(Vector2 worldPos)
    {
        return tileMap.WorldToGrid(worldPos);
    }

    private Vector2 GridToWorld(Vector2 gridPos)
    {
        return tileMap.GridToWorld(gridPos);
    }

    private Color GetTileColor(AgeOfEvolutions.Core.Data.Tile tile)
    {
        return tile.Type switch
        {
            TileType.Water => Color.Blue,
            TileType.Forest => new Color(34, 139, 34),
            TileType.Mountain => new Color(105, 105, 105),
            TileType.GoldMine => Color.Gold,
            TileType.Base => Color.Peru,
            _ => new Color(22, 79, 45) // Grass
        };
    }
}
