using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AgeOfEvolutions.Core.Diagnostics;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Das Fehler-Log schreibt in den Ordner "logs" neben der laufenden Assembly
/// (Anhangmodus, ein File je Kalendertag), nimmt Fehler von mehreren Threads
/// gleichzeitig an und verliert dabei keine Zeile.
/// </summary>
public class GameLogTests
{
    [Fact]
    public void Error_SchreibtMeldungUndStacktraceInLogsOrdner()
    {
        // Erst werfen: eine nie geworfene Exception hat in .NET noch keinen
        // Stacktrace (wird erst beim Throw erfasst).
        Exception ex = null;
        try { throw new InvalidOperationException("Prüf-Fehler 42"); }
        catch (Exception e) { ex = e; }
        GameLog.Error(ex, "Test-Kontext");

        var datei = NeueLogdatei();
        var text = File.ReadAllText(datei);
        Assert.Contains("ERROR", text);
        Assert.Contains("Test-Kontext: System.InvalidOperationException: Prüf-Fehler 42", text);
        Assert.Contains("at ", text); // der Stacktrace gehört dazu
    }

    [Fact]
    public void Warn_SchreibtMeldungInLogsOrdner()
    {
        GameLog.Warn("Asset fehlt: Boden/prüfung");

        var text = File.ReadAllText(NeueLogdatei());
        Assert.Contains("WARN", text);
        Assert.Contains("Asset fehlt: Boden/prüfung", text);
    }

    [Fact]
    public void ParallelesSchreiben_verliertKeineZeile()
    {
        const int anzahl = 50;
        Parallel.For(0, anzahl, i => GameLog.Info("Parallel-" + i.ToString("D3")));

        var text = File.ReadAllText(NeueLogdatei());
        for (var i = 0; i < anzahl; i++)
            Assert.Contains("Parallel-" + i.ToString("D3"), text);
    }

    /// <summary>Frischeste Logdatei im Ordner "logs" der laufenden Test-Assembly.</summary>
    private static string NeueLogdatei()
    {
        var ordner = Path.Combine(AppContext.BaseDirectory, "logs");
        return Directory.EnumerateFiles(ordner, "spiel-*.log")
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .First();
    }
}
