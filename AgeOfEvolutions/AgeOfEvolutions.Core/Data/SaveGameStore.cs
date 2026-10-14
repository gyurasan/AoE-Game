using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Liest und schreibt den Spielstand als JSON-Datei. Ein Platz: neben den Einstellungen in
/// %APPDATA%\AgeOfEvolutions\spielstand.json; ein neuer Spielstand ersetzt den alten.
/// </summary>
public sealed class SaveGameStore
{
    /// <summary>Dateiname des Spielstands.</summary>
    public const string FILE_NAME = "spielstand.json";

    /// <summary>
    /// VERTRAG: <see cref="FilePath"/> ist <paramref name="filePath"/>, oder - wenn null oder leer -
    /// Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    /// "AgeOfEvolutions", FILE_NAME), derselbe Ordner wie die Einstellungen.
    /// </summary>
    public SaveGameStore(string filePath = null)
    {
        // Ohne eigenen Pfad: derselbe Ordner wie die Einstellungen.
        FilePath = string.IsNullOrEmpty(filePath)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgeOfEvolutions", FILE_NAME)
            : filePath;
    }

    /// <summary>Vollständiger Pfad der Spielstand-Datei.</summary>
    public string FilePath { get; }

    /// <summary>Ob es einen Spielstand gibt (die Datei existiert).</summary>
    public bool Exists => File.Exists(FilePath);

    /// <summary>
    /// Schreibt <paramref name="game"/> nach <see cref="FilePath"/>.
    /// VERTRAG: legt den Ordner an, falls er fehlt; schreibt <see cref="Serialize"/>(game) erst in
    /// FilePath + ".tmp" und ersetzt dann FilePath damit (File.Move mit overwrite: true) - ein
    /// Absturz mitten im Schreiben lässt den alten Spielstand heil. Danach gibt es keine
    /// ".tmp"-Datei mehr.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="game"/> ist null.</exception>
    public void Save(SaveGame game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));

        // Ordner anlegen, falls er fehlt (nur wenn es einen Ordner gibt).
        string dir = Path.GetDirectoryName(FilePath);
        if (!string.IsNullOrEmpty(dir))
            Directory.CreateDirectory(dir);

        // Erst in eine temporäre Datei schreiben und dann ersetzen:
        // ein Absturz mitten im Schreiben lässt den alten Spielstand heil.
        string tmp = FilePath + ".tmp";
        File.WriteAllText(tmp, Serialize(game));
        File.Move(tmp, FilePath, overwrite: true);
    }

    /// <summary>
    /// Liest den Spielstand aus <see cref="FilePath"/>.
    /// VERTRAG: Fehlt die Datei - oder schon ihr Ordner -, wirft die Methode eine
    /// FileNotFoundException (vorher mit File.Exists prüfen; File.ReadAllText allein würfe bei
    /// fehlendem Ordner eine DirectoryNotFoundException). Sonst
    /// <see cref="Deserialize"/>(File.ReadAllText(FilePath)).
    /// </summary>
    public SaveGame Load()
    {
        // Vorher prüfen: File.ReadAllText allein würfe bei fehlendem Ordner
        // eine DirectoryNotFoundException.
        if (!File.Exists(FilePath))
            throw new FileNotFoundException("Die Spielstand-Datei wurde nicht gefunden.", FilePath);
        return Deserialize(File.ReadAllText(FilePath));
    }

    /// <summary>
    /// Der Spielstand als JSON.
    /// VERTRAG: JsonSerializer.Serialize mit <see cref="Options"/>.
    /// </summary>
    /// <exception cref="ArgumentNullException"><paramref name="game"/> ist null.</exception>
    public static string Serialize(SaveGame game)
    {
        if (game == null)
            throw new ArgumentNullException(nameof(game));
        return JsonSerializer.Serialize(game, Options);
    }

    /// <summary>
    /// Ein Spielstand aus JSON.
    /// VERTRAG: JsonSerializer.Deserialize&lt;SaveGame&gt; mit <see cref="Options"/>. Ist der Text
    /// kein gültiges JSON für einen Spielstand (JsonException), null, leer oder ergibt er null,
    /// wirft die Methode eine InvalidDataException mit deutscher Meldung (bei JsonException als
    /// InnerException).
    /// </summary>
    public static SaveGame Deserialize(string json)
    {
        if (string.IsNullOrEmpty(json))
            throw new InvalidDataException("Der Spielstand ist leer oder fehlt.");

        SaveGame game;
        try
        {
            game = JsonSerializer.Deserialize<SaveGame>(json, Options);
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Der Spielstand ist kein gültiges JSON.", ex);
        }

        if (game == null)
            throw new InvalidDataException("Der Spielstand ergibt kein gültiges Ergebnis.");
        return game;
    }

    /// <summary>
    /// Die JSON-Einstellungen: Aufzählungen als Namen (JsonStringEnumConverter) - robust, wenn
    /// eine Aufzählung später neue Werte bekommt; Standardwerte werden weggelassen
    /// (DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault), das hält große Karten
    /// klein; nicht eingerückt.
    /// </summary>
    public static readonly JsonSerializerOptions Options = new()
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        WriteIndented = false,
    };
}
