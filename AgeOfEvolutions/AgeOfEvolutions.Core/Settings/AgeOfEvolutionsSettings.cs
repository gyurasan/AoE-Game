using System.ComponentModel;
using System.Runtime.CompilerServices;
using AgeOfEvolutions.Core.Effects;

namespace AgeOfEvolutions.Core.Settings;

/// <summary>
/// Represents the game settings, including display, language, and particle effects.
/// This class implements <see cref="INotifyPropertyChanged"/> to notify subscribers
/// when a property value changes, enabling data binding and UI updates.
/// </summary>
/// <remarks>
/// Die zehn Spiel-Optionen gelten für jedes neue Spiel (das Menü wird im
/// Hauptmenü gewählt, der Spielbildschirm übernimmt sie beim Bildaufbau).
/// Alle sind Ints — System.Text.Json serialisiert sie verlustfrei, und eine
/// alte settings.json ohne diese Felder liefert einfach die Werte hier als
/// Default.
/// </remarks>
public class AgeOfEvolutionsSettings : INotifyPropertyChanged
{
    private bool fullScreen;
    private int language = 2; // Default to English for now
    private ParticleEffectType particleEffect;
    private int mapSize;
    private int soundVolume = 60;
    private int aiDifficulty = 1;
    private int startingCapital = 1;
    private int populationCap = 200;
    private int unitSpeed = 100;
    private int gatherSpeed = 100;
    private int productionSpeed = 100;
    private int meleeStopTiles = 50;
    private int formationSpacingTiles = 50;
    private int cameraPanSpeed = 800;

    /// <summary>
    /// Gets or sets whether the game is in full-screen mode.
    /// </summary>
    /// <value>
    /// <c>true</c> if the game is in full-screen mode; otherwise, <c>false</c>.
    /// </value>
    public bool FullScreen
    {
        get => fullScreen;
        set
        {
            if (fullScreen != value)
            {
                fullScreen = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Die im Hauptmenü gewählte Kartengröße: 0 Standard, 1 Groß, 2 Maximal.
    /// </summary>
    public int MapSize
    {
        get => mapSize;
        set
        {
            if (mapSize != value)
            {
                mapSize = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the language setting for the game.
    /// </summary>
    /// <value>
    /// An integer representing the selected language. The value corresponds to a language
    /// option in the game's localization system.
    /// </value>
    public int Language
    {
        get => language;
        set
        {
            if (language != value)
            {
                language = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Gets or sets the type of particle effect used in the game.
    /// </summary>
    /// <value>
    /// A <see cref="ParticleEffectType"/> value representing the current particle effect.
    /// </value>
    public ParticleEffectType ParticleEffect
    {
        get => particleEffect;
        set
        {
            if (particleEffect != value)
            {
                particleEffect = value;
                OnPropertyChanged();
            }
        }
    }

    // Add more settings as needed

    // --- Die zehn wichtigsten Spiel-Optionen -------------------------------
    // Jede Option ist eine rohe Zahl in der Einheit der Klammer; die erlaubten
    // Wertschritte stehen in GameplayOptions (das Menü blätter sie durcheinander).
    // Alle Defaults sind EXAKT die bisher hartkodierten Werte im Code, damit ein
    // Spiel ohne Einstellungen sich genauso verhält wie vorher.

    /// <summary>Lautstärke der Weltgeräusche in Prozent (0–100).</summary>
    public int SoundVolume
    {
        get => soundVolume;
        set
        {
            if (soundVolume != value)
            {
                soundVolume = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>KI-Schwierigkeit: 0 Leicht, 1 Normal, 2 Schwer, 3 Extrem.</summary>
    public int AiDifficulty
    {
        get => aiDifficulty;
        set
        {
            if (aiDifficulty != value)
            {
                aiDifficulty = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Startkapital: 0 Arm, 1 Normal, 2 Reich.</summary>
    public int StartingCapital
    {
        get => startingCapital;
        set
        {
            if (startingCapital != value)
            {
                startingCapital = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Bevölkerungs-Obergrenze (Plätze), als feste Zahl.</summary>
    public int PopulationCap
    {
        get => populationCap;
        set
        {
            if (populationCap != value)
            {
                populationCap = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Einheiten-Tempo in Prozent (100 = bisheriges Tempo).</summary>
    public int UnitSpeed
    {
        get => unitSpeed;
        set
        {
            if (unitSpeed != value)
            {
                unitSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Sammel-Tempo der Dorfbewohner in Prozent (100 = bisheriges Tempo).</summary>
    public int GatherSpeed
    {
        get => gatherSpeed;
        set
        {
            if (gatherSpeed != value)
            {
                gatherSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Produktions-Tempo (Bauen, Ausbilden, Forschen, Zeitalter) in Prozent
    /// (100 = bisheriges Tempo).
    /// </summary>
    public int ProductionSpeed
    {
        get => productionSpeed;
        set
        {
            if (productionSpeed != value)
            {
                productionSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Angriffshalt-Abstand für Nahkämpfer in Hundertstel-Kacheln
    /// (50 = 0,5 Kachel, der bisherige Wert).
    /// </summary>
    public int MeleeStopTiles
    {
        get => meleeStopTiles;
        set
        {
            if (meleeStopTiles != value)
            {
                meleeStopTiles = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Verbands-Abstand (Standplatz-Verteilung, Schlachtabstand) in
    /// Hundertstel-Kacheln (50 = 0,5 Kachel, der bisherige Wert).
    /// </summary>
    public int FormationSpacingTiles
    {
        get => formationSpacingTiles;
        set
        {
            if (formationSpacingTiles != value)
            {
                formationSpacingTiles = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>Kamera-Verschiebung in Bildschirm-Pixeln pro Sekunde.</summary>
    public int CameraPanSpeed
    {
        get => cameraPanSpeed;
        set
        {
            if (cameraPanSpeed != value)
            {
                cameraPanSpeed = value;
                OnPropertyChanged();
            }
        }
    }

    /// <summary>
    /// Event triggered when a property value changes.
    /// </summary>
    public event PropertyChangedEventHandler PropertyChanged;

    /// <summary>
    /// Raises the <see cref="PropertyChanged"/> event to notify subscribers that a property value has changed.
    /// </summary>
    /// <param name="propertyName">
    /// The name of the property that changed. If not provided, the name of the calling member is used.
    /// </param>
    protected void OnPropertyChanged([CallerMemberName] string propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}