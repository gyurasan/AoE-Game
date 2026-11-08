using System;
using System.Collections.Generic;
using System.Linq;
using AgeOfEvolutions.Core.AI;
using AgeOfEvolutions.Core.Data;
using Microsoft.Xna.Framework;
using AoECoreAi = AoE.Core.Ai;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Screens;

// Aufstellungs-Marsch (Nutzer 2026-11-08: „Wählt man zum Beispiel Bogenschützen aus und es ist
// mehr als einer ausgewählt müssten sie immer sich in Formation bewegen und sich hinstellen. Der
// Zielpunkt ist nur der Zentrum der Aufstellung. Das Gleich gilt für alle Arten von Soldaten.“)
//
// Ein Laufbefehl für zwei oder mehr Soldaten (alles außer Dorfbewohnern und Schiffen) stellt
// sie wie die KI-Armee auf: Nahkämpfer vorn, Bogenschützen und Belagerung dahinter, Reiter an
// den Flanken, eine Kachel frei zwischen den Nachbarn, Blick vom Schwerpunkt zum Klick
// (AoE.Core.Ai.Formation: AnchorForCenter + SlotsByPosition). Ein einzelner Soldat läuft
// weiter genau auf den Punkt, Dorfbewohner verteilen sich wie bisher. Gleichschritt:
// der wirklich gelaufene Weg je Einheit (PulledLength) misst, wie weit jede laufen muss;
// die Schnellen werden gebremst (MarchPace, mindestens MARCH_MIN_FACTOR), damit alle zugleich
// ankommen. Ein Klick auf Baum, Stein oder Gold ist für Soldaten ein Laufbefehl wie jeder
// andere (IssueCommand) — nur Dorfbewohner ernten.
public partial class RTSGameplayScreen
{
    /// <summary>
    /// So langsam (Anteil des vollen Tempos) wird ein Marschierer mindestens geschickt —
    /// schneller als 1/4 der Laufzeit des langsamsten zu bremsen, wäre Unsinn statt Gleichschritt.
    /// </summary>
    internal const float MARCH_MIN_FACTOR = 0.25f;

    /// <summary>Marsch-Faktor je Einheit des letzten Aufstellungs-Marsches (Gleichschritt, B3).</summary>
    private readonly Dictionary<Unit, float> _marchFactor = new();

    /// <summary>
    /// Der Aufstellungs-Marsch für <paramref name="marchers"/> (zwei oder mehr Soldaten
    /// eines Besitzers, keine Dorfbewohner, keine Schiffe) um <paramref name="gridPos"/>.
    /// VERTRAG:
    /// - Blickrichtung: vom Schwerpunkt (Mittelpunkt der Einheiten) zum Klickpunkt
    ///   (Klick auf der Kachel-Mitte); liegt der Klick im Schwerpunkt, (1, 0).
    /// - Anker: <see cref="AoECoreAi.Formation.AnchorForCenter"/> auf der Klick-Kachel,
    ///   damit die MITTE der Aufstellung auf dem Klick liegt (SlotsByPosition).
    /// - Plätze: <see cref="AoECoreAi.Formation.SlotsByPosition"/> — Nahkämpfer vorn,
    ///   Schützen und Belagerung dahinter, Reiter an den Flanken, eine Kachel frei
    ///   zwischen den Nachbarn; jeder bekommt den Platz, der zu seiner Lage passt
    ///   (keine Kreuzungen).
    /// - Jede Einheit: Auftrag, Baustelle und Ziele fallen, sie läuft auf ihre
    ///   Kachel (FindPathKnown — nur mit dem Wissen des Besitzers, wie jeder
    ///   Laufbefehl), ohne Weg untätig.
    /// - Gleichschritt: der gelaufene Weg je Einheit (Start bis Wegende, gezogen
    ///   wie StepAlongPath) wird gemessen; der langsamste bestimmt die Marschzeit,
    ///   jeder andere bekommt den Faktor Zeit * Tempo / Weg, begrenzt auf
    ///   MARCH_MIN_FACTOR bis 1. Wer nicht laufen muss, hat keinen Faktor.
    /// </summary>
    internal void FormArmyMove(List<Unit> marchers, Vector2 gridPos)
    {
        if (marchers.Count < 2)
            return;

        var center = GridToWorld(gridPos);
        var centroid = marchers.Aggregate(Vector2.Zero, (s, u) => s + u.Position) / marchers.Count;
        var facing = center - centroid;
        float facingLen = facing.Length();
        facing = facingLen > 0.5f ? facing / facingLen : new Vector2(1f, 0f);

        // Snapshots in der Reihenfolge der Einheiten; die Identität bindet die
        // Platz-Zuordnung zurück (SlotsByPosition sortiert intern nach Lage)
        var snapshots = new List<AoECoreAi.UnitSnapshot>(marchers.Count);
        var idOf = new Dictionary<Unit, int>(marchers.Count);
        int nextId = 0;
        foreach (var u in marchers)
        {
            nextId++;
            idOf[u] = nextId;
            var at = tileMap.WorldToGrid(u.Position);
            snapshots.Add(new AoECoreAi.UnitSnapshot
            {
                Id = nextId,
                Owner = u.OwnerId,
                Kind = AiBridge.KindOf(u.Type),
                X = (int)at.X,
                Y = (int)at.Y,
                State = AoECoreAi.UnitStateKind.Moving,
            });
        }

        bool Walkable(int x, int y)
            => x >= 0 && y >= 0 && x < tileMap.Width && y < tileMap.Height
               && tileMap.IsWalkable(x, y) && string.IsNullOrEmpty(tileMap.GetTile(x, y).Building);

        var anchor = AoECoreAi.Formation.AnchorForCenter(
            ((int)gridPos.X, (int)gridPos.Y), ((float)facing.X, (float)facing.Y), snapshots);
        var slots = AoECoreAi.Formation.SlotsByPosition(anchor,
            ((float)facing.X, (float)facing.Y), snapshots, Walkable);

        // Ein Slot wird nur zugeteilt, wenn die Einheit ihn auch erreichen kann:
        // sonst bleibt sie am gemeinsamen Wegpunkt stehen und die Formation
        // bricht zusammen (z. B. wenn ein Slot auf Wasser fällt).
        var belegt = new HashSet<int>();
        foreach (var u in marchers)
        {
            int bevorzugt = idOf[u];
            bool erreichbar = tileMap.FindPathKnown(u.OwnerId, u.Position,
                tileMap.GridToWorld(new Vector2(slots[bevorzugt].X, slots[bevorzugt].Y))).Count > 0;
            if (!erreichbar)
            {
                for (int s = 0; s < slots.Count; s++)
                {
                    if (s == bevorzugt || belegt.Contains(s))
                        continue;
                    if (tileMap.FindPathKnown(u.OwnerId, u.Position,
                        tileMap.GridToWorld(new Vector2(slots[s].X, slots[s].Y))).Count > 0)
                    {
                        idOf[u] = s;
                        break;
                    }
                }
            }
            belegt.Add(idOf[u]);
        }

        var distances = new Dictionary<Unit, float>(marchers.Count);
        float slowest = 0f;
        foreach (var u in marchers)
        {
            u.Job = null;
            u.BuildSite = null;
            u.AttackTarget = null;
            u.UnitTarget = null;
            u.PreyTarget = null;
            u.AutoTarget = false;
            u.BoardShip = null;

            var (sx, sy) = slots[idOf[u]];
            u.TargetPosition = tileMap.GridToWorld(new Vector2(sx, sy));
            u.Path = tileMap.FindPathKnown(u.OwnerId, u.Position, u.TargetPosition);
            u.State = u.Path.Count > 0 ? UnitState.Moving : UnitState.Idle;

            // Der wirklich zu laufende Weg, gezogen wie StepAlongPath
            float dist = 0f;
            if (u.Path.Count > 0)
            {
                dist = Vector2.Distance(u.Position, u.Path[0]);
                for (int i = 1; i < u.Path.Count; i++)
                    dist += Vector2.Distance(u.Path[i - 1], u.Path[i]);
            }
            distances[u] = dist;
            if (dist > 0f && u.MovementSpeed > 0f)
                slowest = Math.Max(slowest, dist / u.MovementSpeed);
        }

        foreach (var u in marchers)
        {
            float dist = distances[u];
            if (dist <= 0f || u.MovementSpeed <= 0f)
            {
                _marchFactor.Remove(u);
                continue;
            }
            float factor = slowest * u.MovementSpeed / dist;
            _marchFactor[u] = Math.Clamp(factor, MARCH_MIN_FACTOR, 1f);
        }
    }

    /// <summary>
    /// Das Tempo einer Marsch-Einheit: das gewünschte Tempo, begrenzt auf ihren
    /// Marsch-Faktor (Gleichschritt) — <see cref="StepAlongPath"/> wendet es an.
    /// Kein Faktor: das Tempo bleibt, wie es ist.
    /// </summary>
    internal float MarchPace(Unit unit, float pace)
        => _marchFactor.TryGetValue(unit, out float factor) ? Math.Min(pace, factor) : pace;
}
