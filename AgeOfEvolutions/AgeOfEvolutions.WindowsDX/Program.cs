using System;
using AgeOfEvolutions.Core;
using AgeOfEvolutions.Core.Diagnostics;

internal class Program
{
    /// <summary>
    /// The main entry point for the application. 
    /// This creates an instance of your game and calls the Run() method 
    /// </summary>
    /// <param name="args">Command-line arguments passed to the application.</param>
    private static void Main(string[] args)
    {
        using var game = new AgeOfEvolutionsGame();
        try
        {
            game.Run();
        }
        catch (Exception ex)
        {
            // Ein Fehler im Spiel-Loop läuft bis hier heraus. Ohne diese Stelle
            // wäre er im Fenster verloren - jetzt steht er mit Stacktrace in
            // logs/spiel-<Datum>.log neben der Exe.
            GameLog.Error(ex, "Spiel-Loop");
            Environment.ExitCode = 1;
        }
    }
}