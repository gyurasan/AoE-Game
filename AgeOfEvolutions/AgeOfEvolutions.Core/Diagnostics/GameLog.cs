using System;
using System.IO;
using System.Text;

namespace AgeOfEvolutions.Core.Diagnostics;

/// <summary>
/// Fehler-Log des Spiels: schreibt Meldungen in eine Datei im Ordner
/// <c>logs</c> neben der Exe (je Kalendertag eine Datei, Anhangmodus).
/// Thread-sicher - Spiel-Loop, API-Server und die Fangnetze für unbehaltete
/// Ausnahmen schreiben parallel. Das Loggen darf niemals das Spiel stürzen:
/// jeder Fehler beim Schreiben wird geschluckt.
/// </summary>
public static class GameLog
{
    // Pro Meldung wird kurz geöffnet, geschrieben, geschlossen - danach ist
    // die Datei wieder für alle Programme lesbar. (Ein dauerhaft offenes
    // Handle hat hier paralleles Lesen blockiert, s. GameLogTests; bei der
    // Frequenz eines Fehler-Logs ist der offene StreamWriter überflüssig.)
    private static readonly object _lock = new();
    private static readonly UTF8Encoding _utf8 = new(false);

    /// <summary>Eine Info-Meldung (z. B. Sitzungsstart/-ende).</summary>
    public static void Info(string message) => Write("INFO ", message);

    /// <summary>Eine Warnung: das Spiel läuft weiter, aber etwas fehlt (z. B. ein Bild).</summary>
    public static void Warn(string message) => Write("WARN ", message);

    /// <summary>Eine Fehlermeldung ohne Exception.</summary>
    public static void Error(string message) => Write("ERROR", message);

    /// <summary>
    /// Eine Exception mit vollständigem Stacktrace in die Logdatei.
    /// <paramref name="kontext"/> benennt optional den Ort, an dem der
    /// Fehler auftrat (z. B. "Spiel-Loop").
    /// </summary>
    public static void Error(Exception ex, string? kontext = null)
    {
        var text = string.IsNullOrEmpty(kontext)
            ? ex.ToString()
            : kontext + ": " + ex.ToString();
        Write("ERROR", text);
    }

    private static void Write(string level, string text)
    {
        try
        {
            var zeile = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + level
                + " [Thread " + Environment.CurrentManagedThreadId + "] " + Einrücken(text);

            lock (_lock)
            {
                var datei = Logdatei();
                var ordner = Path.GetDirectoryName(datei)!;
                Directory.CreateDirectory(ordner);
                // Share-ReadWrite, damit auch während des kurzen Schreibfensters
                // die Datei mitgelesen werden kann.
                using var s = File.Open(datei, FileMode.Append, FileAccess.Write,
                    FileShare.ReadWrite);
                s.Write(_utf8.GetBytes(zeile + Environment.NewLine));
            }

            // Fehler zusätzlich in die Konsole, damit sie in einer
            // "dotnet run"-Sichtbarkeit sofort auffallen.
            if (level == "ERROR")
                Console.Error.WriteLine(zeile);
        }
        catch
        {
            // Endstation: ein Problem beim Loggen darf weder das Spiel
            // stürzen noch die Ausbreitung des echten Fehlers stoppen.
        }
    }

    /// <summary><c>logs/spiel-jjjj-mm-tt.log</c> neben der Exe.</summary>
    private static string Logdatei() =>
        Path.Combine(AppContext.BaseDirectory, "logs",
            "spiel-" + DateTime.Now.ToString("yyyy-MM-dd") + ".log");

    /// <summary>
    /// Nachträge (Stacktrace-Zeilen) eingerücken, damit ein Eintrag ein
    /// zusammenhängender Block in der Datei bleibt.
    /// </summary>
    private static string Einrücken(string text)
    {
        var zeilen = text.Replace("\r\n", "\n").Split('\n');
        if (zeilen.Length <= 1)
            return text;
        var b = new StringBuilder();
        for (var i = 0; i < zeilen.Length; i++)
        {
            if (i > 0)
                b.Append("    ");
            b.Append(zeilen[i]);
            if (i + 1 < zeilen.Length)
                b.Append('\n');
        }
        return b.ToString();
    }
}
