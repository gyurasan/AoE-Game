namespace AoE.Core.Ai;

/// <summary>
/// Das KI-Interface — eine Implementierung liest den Zustand der Welt über
/// <see cref="IWorldState"/>, trifft Entscheidungen und sendet Befehle über
/// <see cref="IWorldActions"/>. Das ist die eigentliche „API", die ein
/// KI-Spieler verwendet: <see cref="EconomyAi"/> ist die eingebaute Regel-AI,
/// eine externe AI (LLM, Skript, Netzwerk) kann dasselbe Interface implementieren.
/// </summary>
public interface IAi
{
    /// <summary>
    /// Ein KI-Takt. <paramref name="dt"/> ist die vergangene Zeit seit dem
    /// letzten Takt in Sekunden. Die KI darf pro Takt so viele Aktionen
    /// ausführen, wie sie will — das Spiel führt sie sofort aus (die
    /// Update-Loops des Spiels treiben die Ausführung weiter).
    ///
    /// <paramref name="ctx"/> hält den Kontext der Takt-Runde — Log-Ausgaben,
    /// Takt-Zähler, Zufallswerte.
    /// </summary>
    void Tick(IWorldState state, IWorldActions actions, float dt, AiContext ctx);
}

/// <summary>Kontext eines KI-Takts. Die implementierte KI hält hier ihren
/// internen Zustand (zähler, letzte Befehle, Zufall).</summary>
public sealed class AiContext
{
    /// <summary>Gesamtzahl der Takt-Runden, seit die KI existiert.</summary>
    public int Tick { get; internal set; }

    /// <summary>Gesamtzeit in Sekunden, seit dem ersten Takt.</summary>
    public double Time { get; internal set; }

    /// <summary>Zufallsgenerator der KI (deterministischer Seed über <see cref="Seed"/>,
    /// wiederholbar in Tests).</summary>
    public Random Rng { get; }

    /// <summary>Seed, mit dem der Zufallsgenerator gestartet wurde.</summary>
    public int Seed { get; }

    /// <summary>Log-Ausgabe der KI — die KI schreibt hier ihre Entscheidungen
    /// (für Debugging und Tests).</summary>
    public Action<string> Log { get; }

    public AiContext(int seed = 1)
    {
        Seed = seed;
        Rng = new Random(seed);
        Log = _ => { };
        Tick = 0;
        Time = 0;
    }

    internal void Advance(float dt)
    {
        Tick++;
        Time += dt;
    }
}
