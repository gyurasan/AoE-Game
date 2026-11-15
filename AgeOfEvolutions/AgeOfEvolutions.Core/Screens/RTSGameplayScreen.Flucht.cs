using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;
using GatherJob = AoE.Core.Economy.GatherJob;

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
//
// Raubtiere (Nutzer 2026-11-13: „Raubtiere können übrigens auch Flucht auslösen“):
// beißt ein Bär, Wolf oder Wildschwein einen Dorfbewohner, flieht er ebenso
// (StartFleeAnimal, ausgelöst in UpdatePredators) - nur nicht, wer das Tier jagt
// oder ein Wildschwein selbst gereizt hat. Das Tier folgt nicht (Bär, Wolf) oder nur
// seinem Reizer (Wildschwein); nach der Ruhe geht die Arbeit weiter. Liegt die alte
// Quelle in der Gefahrenzone eines Raubtiers (TileMap.InPredatorDanger), sucht er
// sich die nächste gleiche Quelle außerhalb - sonst liefe er immer wieder in den Biss.
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

    /// <summary>Flucht-Zustand je fliehendem Dorfbewohner (im Spielstand über CaptureFlee/RestoreFlee).</summary>
    private sealed class FleeState
    {
        public Unit From;       // der Angreifer, vor dem er flieht (null bei einem Tier)
        public WildAnimal Animal; // das Tier, vor dem er flieht (null bei einer Einheit)
        public float Calm;      // Sekunden Ruhe am Fluchtpunkt
        public Vector2 Point;   // der aktuelle Fluchtpunkt (Weltkoordinaten)
    }

    private readonly Dictionary<Unit, FleeState> _flee = new();

    /// <summary>
    /// Trägt den Fluchtzustand in einen frisch erfassten Spielstand ein (CaptureSave) - sonst
    /// stünde ein Dorfbewohner, der beim Speichern flieht, geladen am Fluchtpunkt untätig herum,
    /// statt seine Arbeit wieder aufzunehmen.
    /// VERTRAG: für jedes (unit, state) aus _flee: i = tileMap.Units.IndexOf(unit); liegt i nicht
    /// in 0 .. save.Units.Count - 1, weiter. us = save.Units[i]: us.Fleeing = true; us.FleeFrom =
    /// Index von state.From in tileMap.Units (null, wenn state.From null ist oder nicht in der
    /// Liste steht); steht state.Animal (nicht null) laut tileMap.LocatePredator(state.Animal)
    /// auf einer Kachel (lage): us.FleeAnimalX = lage.X, us.FleeAnimalY = lage.Y, sonst beide null;
    /// us.FleePointX/FleePointY = state.Point.X/Y; us.FleeCalm = state.Calm.
    /// </summary>
    private void CaptureFlee(SaveGame save)
    {
        foreach (var (unit, state) in _flee)
        {
            int i = tileMap.Units.IndexOf(unit);
            if (i < 0 || i >= save.Units.Count)
                continue;
            var us = save.Units[i];
            us.Fleeing = true;
            us.FleeFrom = state.From != null ? tileMap.Units.IndexOf(state.From) : null;
            if (state.Animal != null && tileMap.LocatePredator(state.Animal) is { } lage)
            {
                us.FleeAnimalX = lage.X;
                us.FleeAnimalY = lage.Y;
            }
            else
            {
                us.FleeAnimalX = null;
                us.FleeAnimalY = null;
            }
            us.FleePointX = state.Point.X;
            us.FleePointY = state.Point.Y;
            us.FleeCalm = state.Calm;
        }
    }

    /// <summary>
    /// Stellt den Fluchtzustand aus einem Spielstand wieder her (RestoreWorld, nach
    /// SaveGameMapper.Restore - die ersten Einheiten von save.Units sind dann tileMap.Units in
    /// gleicher Reihenfolge, Passagiere stehen hinten und fliehen nie).
    /// VERTRAG: _flee.Clear(). Für jedes i von 0 bis Math.Min(save.Units.Count,
    /// tileMap.Units.Count) - 1 mit save.Units[i].Fleeing: us = save.Units[i]; state = new
    /// FleeState { Calm = us.FleeCalm, Point = new Vector2(us.FleePointX, us.FleePointY) };
    /// state.From = tileMap.Units[us.FleeFrom], wenn us.FleeFrom gesetzt ist und in 0 ..
    /// tileMap.Units.Count - 1 liegt, sonst null; state.Animal = tileMap.GetTile(us.FleeAnimalX,
    /// us.FleeAnimalY)?.Animal, wenn beide gesetzt sind, sonst null; _flee[tileMap.Units[i]] = state.
    /// (Ohne From und Animal nimmt UpdateFlee im nächsten Bild die Arbeit wieder auf.)
    /// </summary>
    private void RestoreFlee(SaveGame save)
    {
        _flee.Clear();
        for (int i = 0; i < Math.Min(save.Units.Count, tileMap.Units.Count); i++)
        {
            var us = save.Units[i];
            if (!us.Fleeing)
                continue;
            var state = new FleeState
            {
                Calm = us.FleeCalm,
                Point = new Vector2(us.FleePointX, us.FleePointY),
            };
            state.From = us.FleeFrom is { } from && from >= 0 && from < tileMap.Units.Count
                ? tileMap.Units[from]
                : null;
            state.Animal = us.FleeAnimalX is { } ax && us.FleeAnimalY is { } ay
                ? tileMap.GetTile(ax, ay)?.Animal
                : null;
            _flee[tileMap.Units[i]] = state;
        }
    }

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
        state.Animal = null;
        state.Calm = 0f;
        state.Point = FleePoint(unit, attacker);
        unit.TargetPosition = state.Point;
        unit.Path = tileMap.FindPath(unit.Position, state.Point);
        unit.State = unit.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
    }

    /// <summary>
    /// Schickt <paramref name="unit"/> vor dem Tier <paramref name="animal"/> in die
    /// Flucht, das auf <paramref name="from"/> (Weltkoordinaten, Mitte seiner Kachel)
    /// steht - oder erneuert die Flucht. VERTRAG wie StartFlee (Arbeit nur bei der
    /// ersten Flucht merken, alle Aufträge und Ziele fallen), aber state.From = null,
    /// state.Animal = animal und der Fluchtpunkt aus FleePointFrom(unit, from).
    /// </summary>
    internal void StartFleeAnimal(Unit unit, WildAnimal animal, Vector2 from)
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
        state.From = null;
        state.Animal = animal;
        state.Calm = 0f;
        state.Point = FleePointFrom(unit, from);
        unit.TargetPosition = state.Point;
        unit.Path = tileMap.FindPath(unit.Position, state.Point);
        unit.State = unit.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;
    }

    /// <summary>
    /// Der Fluchtpunkt von <paramref name="unit"/> vor <paramref name="attacker"/>:
    /// FleePointFrom(unit, attacker.Position).
    /// </summary>
    internal Vector2 FleePoint(Unit unit, Unit attacker) => FleePointFrom(unit, attacker.Position);

    /// <summary>
    /// Der Fluchtpunkt von <paramref name="unit"/> vor einer Gefahr auf <paramref name="from"/>:
    /// VERTRAG:
    /// - Fächer: um die Richtung weg von der Gefahr (unit.Position − from,
    ///   normalisiert; liegt der Angreifer auf der Stelle, (1, 0)) die Winkel
    ///   baseAngle ± FLEE_FAN_DEGREES, in 31 gleich großen Schritten (i = −15…15).
    /// - Je Winkel der Punkt unit.Position + Richtung * FLEE_TARGET_TILES * TileSize;
    ///   er muss selbst in der Karte liegen (X und Y nicht negativ - WorldToGrid schneidet
    ///   Richtung null ab, -27 wäre sonst Kachel 0), seine Kachel innerhalb der Karte,
    ///   begehbar und ohne Gebäude sein.
    /// - Von den Kandidaten der mit dem größten Abstand zu from; er muss
    ///   erreichbar sein (FindPath nicht leer). Die erste freie Kachel im
    ///   Abstands-Ranking, die einen Weg hergibt, gewinnt (deren Mittelpunkt,
    ///   nicht der Idealpunkt).
    /// - Findet sich kein erreichbarer Punkt, bleibt die Einheit stehen
    ///   (ihr jetziger Ort ist der Fluchtpunkt).
    /// </summary>
    internal Vector2 FleePointFrom(Unit unit, Vector2 from)
    {
        float ts = tileMap.TileSize;
        var away = unit.Position - from;
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
            // Links/oberhalb der Karte: (int) schneidet Richtung null ab, also vorher aussortieren
            if (point.X < 0f || point.Y < 0f)
                continue;
            var cell = tileMap.WorldToGrid(point);
            int cx = (int)cell.X, cy = (int)cell.Y;
            if (cx < 0 || cy < 0 || cx >= tileMap.Width || cy >= tileMap.Height)
                continue;
            if (!tileMap.IsWalkable(cx, cy) || !string.IsNullOrEmpty(tileMap.GetTile(cx, cy).Building))
                continue;
            candidates.Add((point, Vector2.Distance(point, from)));
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
    ///   Liegt dessen Quelle (job.Source) in der Gefahrenzone eines Raubtiers
    ///   (tileMap.InPredatorDanger), wird stattdessen
    ///   gatherWorld.FindNearestSource(job.Source, job.Resource, GatherJob.SEARCH_RADIUS)
    ///   gesucht (sie überspringt die Gefahrenzone): gefunden → der Job wird
    ///   new GatherJob(unit.OwnerId, job.Resource, gefunden, job.Carrying), dann
    ///   FollowJob; nichts gefunden → er bleibt untätig (wie ohne Auftrag).
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
            if (tileMap.InPredatorDanger(job.Source.X, job.Source.Y))
            {
                // Zurück in den Biss? Lieber die nächste gleiche Quelle außerhalb
                if (gatherWorld.FindNearestSource(job.Source, job.Resource, GatherJob.SEARCH_RADIUS) is not { } safe)
                    return;
                job = new GatherJob(unit.OwnerId, job.Resource, safe, job.Carrying);
            }
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
    /// - Flieht er vor einem Tier (state.Animal): ist es erlegt (Slaughtered),
    ///   ohne Lebenspunkte oder steht auf keiner Kachel mehr (tileMap.LocatePredator
    ///   null): ResumeWork. Seine Lage ist die Mitte seiner Kachel
    ///   (tileMap.GridToWorld); unsichtbar für den Besitzer: ResumeWork. Bedroht,
    ///   wenn es binnen FLEE_NEAR_TILES steht, oder - Wildschwein, das ihn verfolgt
    ///   (animal.ProvokedBy == unit) - binnen FLEE_AIMED_TILES: neuer Fluchtpunkt
    ///   per FleePointFrom wie unten. Sonst Ruhe wie unten.
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

        float ts = tileMap.TileSize;
        Vector2 danger;
        bool aimed;
        if (state.Animal is { } animal)
        {
            if (animal.Slaughtered || animal.Health <= 0 || tileMap.LocatePredator(animal) is not { } lage)
            {
                ResumeWork(unit);
                return;
            }
            if (!tileMap.IsTileVisible(lage.X, lage.Y, unit.OwnerId))
            {
                ResumeWork(unit);
                return;
            }
            danger = tileMap.GridToWorld(new Vector2(lage.X, lage.Y));
            aimed = ReferenceEquals(animal.ProvokedBy, unit);
        }
        else
        {
            var from = state.From;
            if (from == null || from.State == UnitState.Dead || !units.Contains(from))
            {
                ResumeWork(unit);
                return;
            }
            var cell = tileMap.WorldToGrid(from.Position);
            if (!tileMap.IsTileVisible((int)cell.X, (int)cell.Y, unit.OwnerId))
            {
                ResumeWork(unit);
                return;
            }
            danger = from.Position;
            aimed = from.UnitTarget == unit;
        }

        float dist = Vector2.Distance(unit.Position, danger);
        if (dist <= FLEE_NEAR_TILES * ts || (aimed && dist <= FLEE_AIMED_TILES * ts))
        {
            // Bedroht: weiter weg — Ruhe vergessen
            state.Calm = 0f;
            state.Point = FleePointFrom(unit, danger);
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
