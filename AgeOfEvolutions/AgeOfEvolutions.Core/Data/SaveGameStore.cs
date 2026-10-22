using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Text.Json.Serialization;
using AoE.Core.Economy;

namespace AgeOfEvolutions.Core.Data;

/// <summary>
/// Liest und schreibt Spielstände als JSON-Dateien. Alle Plätze liegen neben den
/// Einstellungen in %APPDATA%\AgeOfEvolutions\: der erste heißt spielstand.json,
/// jeder weitere spielstand-2.json, spielstand-3.json, ... (siehe <see cref="ListSaves"/>
/// und <see cref="NextSavePath"/>). Ein Store liest oder schreibt genau eine Datei;
/// die Platz-Verwaltung ist statisch.
/// </summary>
public sealed class SaveGameStore
{
    /// <summary>Dateiname des ersten Spielstands (der älteste Platz).</summary>
    public const string FILE_NAME = "spielstand.json";

    /// <summary>
    /// VERTRAG: <see cref="FilePath"/> ist <paramref name="filePath"/>, oder - wenn null oder leer -
    /// Path.Combine(<see cref="SavesFolder"/>, FILE_NAME), derselbe Ordner wie die Einstellungen.
    /// </summary>
    public SaveGameStore(string filePath = null)
    {
        // Ohne eigenen Pfad: derselbe Ordner wie die Einstellungen.
        FilePath = string.IsNullOrEmpty(filePath) ? Path.Combine(SavesFolder, FILE_NAME) : filePath;
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

    // --- die Plätze ------------------------------------------------------------

    /// <summary>
    /// Der Ordner, in dem alle Spielstände liegen (neben den Einstellungen).
    /// VERTRAG: ist die Umgebungsvariable AOE_SPEICHER_ORDNER nicht leer, ihr Wert - wie
    /// AOE_SPIELSTAND_DATEI eine Öffnung nur für Prüfungen; sonst
    /// Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
    /// "AgeOfEvolutions"), also %APPDATA%\AgeOfEvolutions.
    /// </summary>
    public static string SavesFolder =>
        Environment.GetEnvironmentVariable("AOE_SPEICHER_ORDNER") is { Length: > 0 } ordner
            ? ordner
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "AgeOfEvolutions");

    /// <summary>
    /// Liest nur den Kopf eines Spielstands - für die Anzeige, ohne die Welt aufzubauen.
    /// VERTRAG: fehlt die Datei, FileNotFoundException. Sonst die Datei als JSON lesen
    /// (JsonDocument); ist sie kein JSON-Objekt, eine InvalidDataException mit deutscher
    /// Meldung (JsonException als InnerException). Die Werte:
    /// - SavedAt aus "SavedAt"; fehlt es oder ist keine Zeit, eine InvalidDataException
    ///   (ein Stand ohne Speicherzeit ist in der Liste wertlos).
    /// - GameSeconds aus "GameSeconds"; fehlt es (wird bei 0 weggespart), 0.
    /// - Age aus "Ages"."Current" eines Spielers; fehlt "Current" (Dark ist der
    ///   Standardwert und wird weggespart), Age.Dark. Der Spieler ist der Eintrag in
    ///   "Players" mit "Id" 0 - fehlt dort ein "Id", gilt 0 (auch 0 ist Standardwert
    ///   und wird weggespart); es gibt keinen Eintrag mit Id 0, den ersten.
    ///   "Players" fehlt oder ist leer: InvalidDataException mit deutscher Meldung.
    /// Rückgabe: neuer <see cref="SaveSlot"/> mit <paramref name="path"/>, SavedAt,
    /// GameSeconds, Age.
    /// </summary>
    public static SaveSlot ReadHeader(string path)
    {
        // Vorher prüfen: ReadText allein würfe bei fehlendem Ordner eine DirectoryNotFoundException.
        if (!File.Exists(path))
            throw new FileNotFoundException("Die Spielstand-Datei wurde nicht gefunden.", path);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Der Spielstand ist kein gültiges JSON.", ex);
        }

        using (doc)
        {
            var root = doc.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Der Spielstand ist kein gültiges JSON.");

            // Ohne Speicherzeit ist der Stand in der Liste wertlos.
            if (!root.TryGetProperty("SavedAt", out var savedAt)
                || savedAt.ValueKind != JsonValueKind.String || !savedAt.TryGetDateTime(out var zeit))
                throw new InvalidDataException("Der Spielstand hat keine Speicherzeit.");

            // GameSeconds 0 wird als Standardwert weggespart.
            double gameSeconds = root.TryGetProperty("GameSeconds", out var gs) && gs.TryGetDouble(out var sekunden)
                ? sekunden : 0;

            if (!root.TryGetProperty("Players", out var players) || players.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("Der Spielstand hat keine Spieler.");
            var eintraege = players.EnumerateArray().ToList();
            if (eintraege.Count == 0)
                throw new InvalidDataException("Der Spielstand hat keine Spieler.");

            // Der Mensch (Id 0) - auch dann, wenn die 0 als Standardwert fehlt; keinen solchen: der erste.
            var spieler = default(JsonElement);
            foreach (var eintrag in eintraege)
            {
                if (!eintrag.TryGetProperty("Id", out var id) || (id.TryGetInt32(out var nr) && nr == 0))
                {
                    spieler = eintrag;
                    break;
                }
            }
            if (spieler.ValueKind == JsonValueKind.Undefined)
                spieler = eintraege[0];

            // Ages.Current "Dark" wird als Standardwert weggespart.
            var age = Age.Dark;
            if (spieler.TryGetProperty("Ages", out var ages) && ages.ValueKind == JsonValueKind.Object
                && ages.TryGetProperty("Current", out var current) && current.ValueKind == JsonValueKind.String
                && !Enum.TryParse(current.GetString(), false, out age))
                throw new InvalidDataException($"Das Zeitalter „{current.GetString()}“ gibt es nicht.");

            return new SaveSlot(path, zeit, gameSeconds, age);
        }
    }

    /// <summary>
    /// Alle Spielstände im Ordner, der neueste zuerst.
    /// VERTRAG: Ordner = <paramref name="folder"/> ?? <see cref="SavesFolder"/>. Gibt es
    /// den Ordner nicht, eine leere Liste. Gezählt werden nur die Dateien (keine
    /// Ordner, keine ".tmp") spielstand.json und spielstand-&lt;Zahl&gt;.json - Zahl eine
    /// oder mehr Ziffern, Groß-/Kleinschreibung egal; alles andere im Ordner wird
    /// ignoriert. Zu jeder Datei <see cref="ReadHeader"/>; jede Ausnahme dabei
    /// (kaputter Spielstand) überspringt nur diese Datei. Rückgabe sortiert: SavedAt
    /// absteigend, bei gleicher Zeit der Dateiname aufsteigend.
    /// </summary>
    public static IReadOnlyList<SaveSlot> ListSaves(string folder = null)
    {
        folder ??= SavesFolder;
        if (!Directory.Exists(folder))
            return Array.Empty<SaveSlot>();

        var plaetze = new List<SaveSlot>();
        foreach (var datei in Directory.EnumerateFiles(folder))
        {
            if (!SLOT_NAME.IsMatch(Path.GetFileName(datei)))
                continue;
            try
            {
                plaetze.Add(ReadHeader(datei));
            }
            catch (Exception)
            {
                // Kaputter Spielstand: nur diese Datei überspringen, die Liste bleibt gültig.
            }
        }

        return plaetze
            .OrderByDescending(p => p.SavedAt)
            .ThenBy(p => Path.GetFileName(p.FilePath), StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>spielstand.json und spielstand-&lt;Zahl&gt;.json, sonst nichts.</summary>
    private static readonly Regex SLOT_NAME =
        new(@"^spielstand(-\d+)?\.json$", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    /// <summary>
    /// Der Pfad für einen neuen Spielstand: der freie Platz mit der kleinsten Nummer.
    /// VERTRAG: Ordner = <paramref name="folder"/> ?? <see cref="SavesFolder"/>. Liegt
    /// dort kein spielstand.json, dessen Pfad. Sonst der Pfad von spielstand-&lt;n&gt;.json
    /// für das kleinste n ab 2, dessen Datei fehlt. Die Methode legt nichts an
    /// (<see cref="Save"/> legt den Ordner an).
    /// </summary>
    public static string NextSavePath(string folder = null)
    {
        folder ??= SavesFolder;
        var erster = Path.Combine(folder, FILE_NAME);
        if (!File.Exists(erster))
            return erster;

        // Die kleinste freie Nummer ab 2 - eine gelöschte Mitte wird wieder benutzt.
        for (int n = 2; ; n++)
        {
            var pfad = Path.Combine(folder, $"spielstand-{n}.json");
            if (!File.Exists(pfad))
                return pfad;
        }
    }
}

/// <summary>
/// Ein Spielstand-Platz, wie die Menüs ihn anzeigen: Pfad der Datei und die paar Werte
/// aus dem Kopf (siehe <see cref="SaveGameStore.ReadHeader"/>) - die Welt selbst ist nicht geladen.
/// </summary>
/// <param name="FilePath">Vollständiger Pfad der Spielstand-Datei.</param>
/// <param name="SavedAt">Wann gespeichert wurde (Ortszeit).</param>
/// <param name="GameSeconds">Spielzeit in Sekunden.</param>
/// <param name="Age">Zeitalter des Spielers (Id 0) beim Speichern.</param>
public sealed record SaveSlot(string FilePath, DateTime SavedAt, double GameSeconds, Age Age)
{
    /// <summary>
    /// Anzeige für die Menüeinträge, z. B. „22.10.2026 21:14 · Ritterzeit · 37 min“.
    /// VERTRAG: <c>{SavedAt:dd.MM.yyyy HH:mm} · {AgeRules.NameOf(Age)} · {n} min</c> mit
    /// n = GameSeconds durch 60, auf ganze Minuten gerundet. Nur Zeichen, die die
    /// Menüschrift hat (Ziffern, Punkte, Doppelpunkt, Leerzeichen, das Mittelhochpunkt ·).
    /// </summary>
    public string Label =>
        $"{SavedAt:dd.MM.yyyy HH:mm} · {AgeRules.NameOf(Age)} · {GameSeconds / 60:0} min";
}
