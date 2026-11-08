using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Screens;

// Flucht (Nutzer 2026-11-08: „Dorfbewohner hören auf zu Arbeiten nach einem Angriff.
// Dorfbewohner sollten flüchten nicht angreifen da sie keine Krieger sind. Ist der
// Angriff vorbei müssen sie wieder die Arbeit aufnehmen wo sie vorher dran waren und
// das selbständig.“)
//
// Ein getroffener Dorfbewohner wehrt sich nicht mehr (Soldaten verteidigen weiter —
// der Gegenwehr-Hook in HandleAttack), sondern flieht: Er merkt sich seinen
// Sammelauftrag bzw. seine Baustelle (Unit.ResumeJob / Unit.ResumeSite, nur bei der
// ersten Flucht), läuft FLEE_TARGET_TILES Kacheln in den Fächer um die Richtung
// weg vom Angreifer (FleePoint) und flieht so oft erneut, wie der Angreifer
// bedroht ist. Ist der Angreifer tot oder fort — oder es war FLEE_CALM_SECONDS
// ruhig am Fluchtpunkt — nimmt der Dorfbewohner seine frühere Arbeit selbst wieder
// auf (ResumeWork). Ein neuer Befehl (IssueCommand, AttackUnit, AttackBuilding)
// beendet die Flucht; auf Befehl angreifende Dorfbewohner kämpfen weiter.
public partial class RTSGameplayScreen
{
    /// <summary>So weit (Kacheln) weg vom Angreifer läuft ein fliehender Dorfbewohner.</summary>
    internal const float FLEE_TARGET_TILES = 6f;

    /// <summary>Bedroht, wenn der Angreifer so nah (Kacheln) am Fliehenden ist.</summary>
    internal const float FLEE_NEAR_TILES = 2f;

    /// <summary>Bedroht, wenn der Angreifer auf ihn aus ist und so nah (Kacheln).</summary>
    internal const float FLEE_AIMED_TILES = 7f;

    /// <summary>Ruhe am Fluchtpunkt in Sekunden, nach der die frühere Arbeit weitergeht.</summary>
    internal const float FLEE_CALM_SECONDS = 2.5f;

    /// <summary>Halbweite des Flucht-Fächers um die Richtung weg vom Angreifer (Grad).</summary>
    internal const float FLEE_FAN_DEGREES = 150f;

    /// <summary>Flucht-Zustand je fliehendem Dorfbewohner (nur in diesem Bild, kein Spielstand).</summary>
    private sealed class FleeState
    {
        public Unit From;       // der Angreifer, vor dem er flieht
        public float Calm;      // Sekunden Ruhe am Fluchtpunkt
        public Vector2 Point;   // der aktuelle Fluchtpunkt (Weltkoordinaten)
    }

    private readonly Dictionary<Unit, FleeState> _flee = new();

    /// <summary>Ob <paramref name="unit"/> gerade flieht.</summary>
    internal bool IsFleeing(Unit unit) => _flee.ContainsKey(unit);

    /// <summary>
    /// Schickt <paramref name="unit"/> vor <paramref name="attacker"/> in die Flucht —
    /// oder erneuert die Flucht, wenn er schon flieht (neuer Angreifer, neuer Punkt).
    /// VERTRAG:
    /// - Nur bei der ersten Flucht (unit noch nicht in _flee) merkt er sich die Arbeit:
    ///   Sammelauftrag in ResumeJob, Baustelle in ResumeSite. Danach bleibt, was
    ///   gemerkt ist, bis ResumeWork es aufbraucht oder StopFlee es verwirft.
    /// - Auftrag, Baustelle, alle Ziele und das Einsteigen fallen (Job null,
    ///   BuildSite null, UnitTarget/AttackTarget/PreyTarget/BoardShip null).
    /// - Der Fluchtpunkt kommt aus FleePoint(unit, attacker); die Einheit läuft
    ///   dorthin (FindPath), sonst bleibt sie untätig stehen.
    /// </summary>
    internal void StartFlee(Unit unit, Unit attacker)
    {
        if (!_flee.ContainsKey(unit))
        {
            // Die Arbeit nur einmal merken — späteren Flucht-Zyklen gehören
            // die gemerkten Aufträge, nicht die (leeren) aktuellen Felder
            if (unit.Job != null)
                unit.ResumeJob = unit.Job;
            if (unit.BuildSite != null)
                unit.ResumeSite = unit.BuildSite;
        }
        unit.Job = null;
        unit.BuildSite = null;
        unit.UnitTarget = null;
        unit.AttackTarget = null;
        unit.PreyTarget = null;
        unit.AutoTarget = false;
        unit.BoardShip = null;

        FleeState state;
        if (!_flee.TryGetValue(unit, out state))
        {
            state = new FleeState();
            _flee[unit] = state;
        }
        state.From = attacker;
        state.Calm = 0f;
        state.Point = FleePoint(unit, attacker);
        unit.TargetPosition = state.Point;
        unit.Path = tileMap.FindPath(unit.Position, state.Point);
        unit.State = unit.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
    }

    /// <summary>
    /// Der Fluchtpunkt von <paramref name="unit"/> vor <paramref name="attacker"/>:
    /// VERTRAG:
    /// - Fächer: um die Richtung weg vom Angreifer (unit.Position − attacker.Position,
    ///   normalisiert; liegt der Angreifer auf der Stelle, (1, 0)) die Winkel
    ///   baseAngle ± FLEE_FAN_DEGREES, in 31 gleich großen Schritten (i = −15…15).
    /// - Je Winkel der Punkt unit.Position + Richtung * FLEE_TARGET_TILES * TileSize;
    ///   seine Kachel muss innerhalb der Karte, begehbar und ohne Gebäude sein.
    /// - Von den Kandidaten der mit dem größten Abstand zum Angreifer; er muss
    ///   erreichbar sein (FindPath nicht leer). Die erste freie Kachel im
    ///   Abstands-Ranking, die einen Weg hergibt, gewinnt (deren Mittelpunkt,
    ///   nicht der Idealpunkt).
    /// - Findet sich kein erreichbarer Punkt, bleibt die Einheit stehen
    ///   (ihr jetziger Ort ist der Fluchtpunkt).
    /// </summary>
    internal Vector2 FleePoint(Unit unit, Unit attacker)
    {
        float ts = tileMap.TileSize;
        var away = unit.Position - attacker.Position;
        if (away.Length() < 1f)
            away = new Vector2(1f, 0f);
        away /= away.Length();
        float baseAngle = (float)Math.Atan2(away.Y, away.X);
        float fan = FLEE_FAN_DEGREES * (float)Math.PI / 180f;

        var candidates = new List<(Vector2 Point, float Distance)>();
        for (int i = -15; i <= 15; i++)
        {
            float angle = baseAngle + fan * i / 15f;
            var point = unit.Position + new Vector2(MathF.Cos(angle), MathF.Sin(angle)) * FLEE_TARGET_TILES * ts;
            var cell = tileMap.WorldToGrid(point);
            int cx = (int)cell.X, cy = (int)cell.Y;
            if (cx < 0 || cy < 0 || cx >= tileMap.Width || cy >= tileMap.Height)
                continue;
            if (!tileMap.IsWalkable(cx, cy) || !string.IsNullOrEmpty(tileMap.GetTile(cx, cy).Building))
                continue;
            candidates.Add((point, Vector2.Distance(point, attacker.Position)));
        }
        candidates.Sort((a, b) => b.Distance.CompareTo(a.Distance));
        foreach (var (point, _) in candidates)
            if (tileMap.FindPath(unit.Position, point).Count > 0)
                return point;
        return unit.Position;
    }

    /// <summary>
    /// Die Flucht ist vorbei: der Dorfbewohner nimmt seine frühere Arbeit selbst
    /// wieder auf. VERTRAG:
    /// - Baustelle zuerst: ResumeSite ist noch unfertig und steht auf der Karte —
    ///   AssignBuilder(unit, site) schickt ihn zurück an die Wand.
    /// - Sonst der gemerkte Sammelauftrag: ResumeJob wird der Job und FollowJob
    ///   führt ihn zur Quelle (erschöpft ist die Quelle, findet er selbst weiter).
    /// - Sonst bleibt er untätig. In allen Fällen verschwinden der Flucht-Zustand
    ///   und die gemerkten Aufträge.
    /// </summary>
    internal void ResumeWork(Unit unit)
    {
        if (unit.ResumeSite is { } site && !site.IsComplete && tileMap.Buildings.Contains(site))
        {
            unit.ResumeSite = null;
            unit.ResumeJob = null;
            _flee.Remove(unit);
            AssignBuilder(unit, site);
            return;
        }
        if (unit.ResumeJob is { } job)
        {
            unit.ResumeJob = null;
            unit.ResumeSite = null;
            _flee.Remove(unit);
            unit.Job = job;
            FollowJob(unit);
            return;
        }
        _flee.Remove(unit);
    }

    /// <summary>
    /// Beendet die Flucht von <paramref name="unit"/> (neuer Befehl, Einsteigen
    /// oder Angriffsbefehl) und verwirft die gemerkten Aufträge — sie gehören zur
    /// unterbrochenen Arbeit, die der neue Befehl ersetzt.
    /// </summary>
    internal void StopFlee(Unit unit)
    {
        if (_flee.Remove(unit))
        {
            unit.ResumeJob = null;
            unit.ResumeSite = null;
        }
    }

    /// <summary>
    /// Die Flucht pro Einheit, einmal je Bild vor der Zustandsmaschine
    /// (UpdateUnits). VERTRAG:
    /// - Der Angreifer ist tot, entfernt von der Karte oder nicht mehr sichtbar
    ///   für den Besitzer: ResumeWork.
    /// - Bedroht (Angreifer binnen FLEE_NEAR_TILES, oder auf ihn aus und binnen
    ///   FLEE_AIMED_TILES): Ruhe-Zähler auf null, neuer Fluchtpunkt (FleePoint),
    ///   Weg dorthin.
    /// - Sonst: ruht er (untätig oder ohne Weg und höchstens eine Kachel vom
    ///   Fluchtpunkt), zählt die Ruhe weiter — nach FLEE_CALM_SECONDS ResumeWork.
    ///   Bewegung verzögert den Ruhe-Zähler (auf null).
    /// </summary>
    internal void UpdateFlee(Unit unit, float dt)
    {
        if (!_flee.TryGetValue(unit, out var state))
            return;

        var from = state.From;
        if (from == null || from.State == UnitState.Dead || !units.Contains(from))
        {
            ResumeWork(unit);
            return;
        }

        float ts = tileMap.TileSize;
        var cell = tileMap.WorldToGrid(from.Position);
        if (!tileMap.IsTileVisible((int)cell.X, (int)cell.Y, unit.OwnerId))
        {
            ResumeWork(unit);
            return;
        }

        float dist = Vector2.Distance(unit.Position, from.Position);
        bool aimed = from.UnitTarget == unit;
        if (dist <= FLEE_NEAR_TILES * ts || (aimed && dist <= FLEE_AIMED_TILES * ts))
        {
            // Bedroht: weiter weg — Ruhe vergessen
            state.Calm = 0f;
            state.Point = FleePoint(unit, from);
            unit.TargetPosition = state.Point;
            unit.Path = tileMap.FindPath(unit.Position, state.Point);
            unit.State = unit.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
            return;
        }

        bool settled = (unit.State == UnitState.Idle || unit.Path.Count == 0)
                       && Vector2.Distance(unit.Position, state.Point) <= ts;
        state.Calm = settled ? state.Calm + dt : 0f;
        if (state.Calm >= FLEE_CALM_SECONDS)
            ResumeWork(unit);
    }
}
