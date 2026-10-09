using System;
using AoE.Core.Ai;
using AgeOfEvolutions.Core.Screens;

namespace AgeOfEvolutions.Core.AI;

/// <summary>
/// Der KI-Agent für einen Spieler. Hält:
///  • die Brücke zwischen <see cref="RTSGameplayScreen"/> und
///    <see cref="IAi"/> (AoE.Core.Ai),
///  • die konkrete KI-Instanz,
///  • den Takt (Standard: 1 Takt alle 0,5 s — eine wirtschaftliche KI
///    braucht keine Bildrate von 60 Hz).
///
/// <see cref="Tick"/> wird von <c>RTSGameplayScreen.Update()</c> pro Frame
/// aufgerufen. Die Aktionen, die die KI auf <c>IWorldActions</c> nimmt, laufen
/// IM SPIEL-FADEN — <see cref="AiBridge"/> ruft die owner-generic
/// Screen-Methoden direkt.
///
/// Externe Steuerung (REST-API, Netzwerk-KI) ruft <b>nicht</b> über den
/// Agenten: die Bridge ist für den synchronen Spiel-Faden gebaut. Für andere
/// Fäden steht <see cref="RTSGameplayScreen.EnqueueOrder"/> bereit.
/// </summary>
public sealed class AiAgent
{
    public AiAgent(RTSGameplayScreen screen, IAi ai, int owner = 1, float tickInterval = 0.5f)
    {
        _bridge = new AiBridge(screen) { Owner = owner };
        _ai = ai ?? throw new ArgumentNullException(nameof(ai));
        _tickInterval = Math.Max(0.05f, tickInterval);
    }

    public int Owner { get; }
    public IAi Ai => _ai;
    public AiBridge Bridge => _bridge;

    /// <summary>Optionales Debug-Log — wird auf den AI-Kontext und die
    /// Bridge weitergereicht (Standard: aus; im Spiel leise).</summary>
    public Action<string>? Log { get; set; }

    private readonly AiBridge _bridge;
    private readonly IAi _ai;
    private readonly float _tickInterval;
    private float _elapsed = 0f;
    private readonly AiContext _ctx = new(1);

    /// <summary>
    /// Ein Spielframe (im Update aufgerufen). Sammelt die vergangene Zeit und
    /// ruft die KI auf, wenn ihr Takt fällig ist.
    /// </summary>
    public void Tick(float dt)
    {
        _elapsed += dt;
        while (_elapsed >= _tickInterval)
        {
            _elapsed -= _tickInterval;
            if (Log is { } log)
            {
                _ctx.Log = log;
                _bridge.DebugLog = log;
            }
            _ai.Tick(_bridge, _bridge, _tickInterval, _ctx);
        }
    }
}
