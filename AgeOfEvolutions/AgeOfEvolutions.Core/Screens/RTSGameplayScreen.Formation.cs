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
//
// Aufstellung wählbar (Nutzer 2026-11-15: „…soll man verschiedene Formationen auswählen
// können, für Verteidigung und für Angriff“): _selectedFormation steht auf
// AoE.Core.Ai.FormationLayout (Angriff = Standard wie bisher, Verteidigung = Schusslinie
// vorn, Schild dahinter); die Leiste bietet beides als Taste an (RTSGameplayScreen.LayoutButtons),
// FormArmyMove marschiert in der gewählten Aufstellung.
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
    /// Die gewählte Aufstellung der nächsten Aufstellungs-Marsche (Standard
    /// <see cref="AoE.Core.Ai.FormationLayout.Angriff"/> = wie bisher;
    /// <see cref="SetSelectedFormation"/> oder die Leistentaste schaltet um).
    /// Bewusst kein Spielstand: die Auswahl und Aufstellung sind Spieler-Zustand
    /// wie <c>placing</c> — ein geladenes Spiel marschiert wie ein neues.
    /// </summary>
    private AoE.Core.Ai.FormationLayout _selectedFormation = AoE.Core.Ai.FormationLayout.Angriff;

    /// <summary>
    /// Die Aufstellung der nächsten Aufstellungs-Marsche wählen (Leistentaste
    /// „Angriff"/„Verteidigung" oder Befehl). Die Wahl bleibt, bis eine andere
    /// Formation gewählt wird — der nächste Laufbefehl der Soldaten marschiert
    /// danach in ihr.
    /// </summary>
    internal void SetSelectedFormation(AoE.Core.Ai.FormationLayout layout)
        => _selectedFormation = layout;

    /// <summary>
    /// Der Aufstellungs-Marsch für <paramref name="marchers"/> (zwei oder mehr Soldaten
    /// eines Besitzers, keine Dorfbewohner, keine Schiffe) um <paramref name="gridPos"/>.
    /// VERTRAG:
    /// - Blickrichtung: vom Schwerpunkt (Mittelpunkt der Einheiten) zum Klickpunkt
    ///   (Klick auf der Kachel-Mitte); liegt der Klick im Schwerpunkt, (1, 0).
    /// - Anker: <see cref="AoECoreAi.Formation.AnchorForCenter"/> auf der Klick-Kachel,
    ///   damit die MITTE der Aufstellung auf dem Klick liegt (SlotsByPosition).
    /// - Plätze: <see cref="AoECoreAi.Formation.SlotsByPosition"/> in der gewählten
    ///   Aufstellung <see cref="_selectedFormation"/> — Angriff (Standard): Nahkämpfer vorn,
    ///   Schützen und Belagerung dahinter, Reiter an den Flanken; Verteidigung: Schusslinie
    ///   vorn, Schild dahinter, Flanken gleich. Eine Kachel frei zwischen den Nachbarn;
    ///   jeder bekommt den Platz, der zu seiner Lage passt (keine Kreuzungen).
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
            ((float)facing.X, (float)facing.Y), snapshots, Walkable, _selectedFormation);

        // Jede Einheit behält den Platz, den SlotsByPosition ihr gibt (Schlüssel von slots
        // sind die Ids aus idOf, 1..N — keine Indizes). Einen Ersatzplatz für einen
        // „unerreichbaren“ Platz gibt es bewusst nicht: FindPathKnown liefert nur dann
        // einen leeren Weg, wenn die Einheit schon auf der besten Kachel steht — dann
        // bleibt sie stehen (unten: ohne Weg untätig); liegt der Platz hinter Wasser,
        // führt der Weg bis zur nächstliegenden Kachel. Der frühere Ersatzzweig lief
        // deshalb genau dann, wenn ein Soldat schon richtig stand: erst las er slots[0]
        // (Game-Crash 2026-11-15, KeyNotFoundException), nach der ersten Korrektur
        // schickte er ihn auf den Platz des Nachbarn — zwei Soldaten auf einer Kachel.
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
