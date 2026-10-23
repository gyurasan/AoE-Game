using System;
using System.Collections.Generic;
using AgeOfEvolutions.Core.Data;
using AoE.Core.Combat;
using Microsoft.Xna.Framework;
using UnitState = AoE.Core.Entities.UnitState;

namespace AgeOfEvolutions.Core.Screens;

// F1 — Gebäude schießen: Wachturm, Stadtzentrum und Burg verteidigen sich selbst.
// Werte und Takt stehen in AoE.Core (BuildingEntity-Erzeugung, BuildingCombat),
// die Schadensformel ist die allgemeine (Fernkampf gegen Fernkampf-Rüstung).
// Befiederte Pfeile hebt Angriff und Reichweite der schießenden Gebäude um je 1
// (TechEffects.Apply), Maurerkunst wirkt auf sie wie auf jedes Gebäude.
public partial class RTSGameplayScreen
{
    /// <summary>
    /// Prüfschalter (wie <see cref="Data.TileMap.TestNoFog"/>): false lässt alle
    /// Gebäude schweigend stehen. <c>tools/spielablauf</c> baut jede Welt damit -
    /// die Szenarien vieler Gruppen stehen neben dem Start-Stadtzentrum, und das
    /// schießt seit F1. Die Gruppe <c>beschuss</c> schaltet den Beschuss für sich
    /// wieder an; das Spiel selbst ändert hier nichts (immer true).
    /// </summary>
    public static bool TestBombard = true;

    /// <summary>
    /// Ein Schuss je schießendem Gebäude und Takt (F1). VERTRAG: je Gebäude b aus
    /// tileMap.Buildings mit BuildingCombat.Shoots(b.Core) und b.IsComplete:
    /// 1. Ziel = die nächste lebende Feindeinheit (OwnerId ungleich b.OwnerId) innerhalb
    ///    der Reichweite b.Core.Stats.Range (Kacheln, euklidisch von der Gebäudemitte zur
    ///    Figur) UND auf einer für den Besitzer sichtbaren Kachel (IsTileVisible). Kein
    ///    Ziel: BombardTimer läuft bis höchstens BuildingCombat.BOMBARD_SECONDS nach —
    ///    ein wartender Turm schießt, sobald ein Feind in Reichweite steht.
    /// 2. Mit Ziel: BombardTimer += dt. Überquert er BOMBARD_SECONDS minus Flugzeit,
    ///    bekommt _arrows einen Pfeil von BombardMuzzle(b) auf die Brust des Ziels
    ///    (genau wie bei einer Figur: Zielposition + (0, -10)), Flight so, dass er beim
    ///    Schlag ankommt; Fletched nach der Forschung des Gebäudebesitzers.
    /// 3. Erreicht BombardTimer BOMBARD_SECONDS: Bauernregel wie beim Soldaten —
    ///    Schaden = BuildingCombat.DamageToUnit auf das Ziel (mindestens 1),
    ///    RegisterDamageTaken, Klang "bogen" an der Gebäudemitte, Timer um eine Takt-
    ///    länge nach vorn. Trifft es das Ziel tödlich, wird es Dead; eingesammelt wird
    ///    es vom Aufräumer am Ende von UpdateUnits.
    /// Unsichtbare, tote und eigene Einheiten treffen nie; unvollendete Gebäude und
    /// solche ohne eigenen Angriff schießen nie.
    /// </summary>
    internal void UpdateBombard(float dt)
    {
        if (!TestBombard)
            return;
        foreach (Building b in tileMap.Buildings)
        {
            if (!BuildingCombat.Shoots(b.Core) || !b.IsComplete)
                continue;

            Unit target = BombardTarget(b);
            if (target == null)
            {
                // kein Ziel: der Takt bleibt geladen, aber nie über eine Taktlänge
                b.BombardTimer = Math.Min(BombardReload, b.BombardTimer + dt);
                continue;
            }

            // Schlagtakt; der Pfeil startet so früh, dass er genau beim Schlag ankommt
            Vector2 from = BombardMuzzle(b);
            Vector2 to = target.Position + new Vector2(0, -10f);
            float flight = ArrowFlight(Vector2.Distance(from, to));
            float launch = BombardReload - flight;
            float before = b.BombardTimer;
            b.BombardTimer += dt;
            if (before < launch && b.BombardTimer >= launch)
                _arrows.Add(new Arrow
                {
                    From = from,
                    To = to,
                    Flight = MathF.Max(1f / 60f, BombardReload - b.BombardTimer),
                    Fletched = (b.OwnerId == 0 ? player1 : player2).Techs
                        .IsResearched(AoE.Core.Economy.Tech.Fletching),
                });

            if (b.BombardTimer >= BombardReload)
            {
                b.BombardTimer -= BombardReload;
                target.Health = Math.Max(0, target.Health - BuildingCombat.DamageToUnit(b.Core, target.Core));
                RegisterDamageTaken(target.OwnerId);
                EmitSound("bogen", BuildingCenter(b));
                if (target.Health == 0)
                    target.State = UnitState.Dead;
            }
        }
    }

    /// <summary>Der Schusstakt in Sekunden (F1) — eine Konstante, damit
    /// Prüfungen dieselbe Zahl sehen wie das Spiel.</summary>
    internal static float BombardReload => BuildingCombat.BOMBARD_SECONDS;

    /// <summary>
    /// Die Gebäudemitte in Weltpixeln — Ort des Klangs und Bezug der Reichweite (F1).
    /// VERTRAG: Vector2((b.X + b.Width / 2f) * tileMap.TileSize,
    /// (b.Y + b.Height / 2f) * tileMap.TileSize).
    /// </summary>
    internal Vector2 BuildingCenter(Building b)
        => new((b.X + b.Width / 2f) * tileMap.TileSize, (b.Y + b.Height / 2f) * tileMap.TileSize);

    /// <summary>
    /// Wo der Pfeil das Gebäude verlässt (F1): über der Mitte, damit er über die Mauer-
    /// zinnen bzw. den Turmhut steigt. VERTRAG: BuildingCenter(b) + Vector2(0, -tileMap.TileSize).
    /// </summary>
    internal Vector2 BombardMuzzle(Building b) => BuildingCenter(b) + new Vector2(0, -tileMap.TileSize);

    /// <summary>
    /// Das nächste sichtbare Opfer des Gebäudes (F1) oder null. VERTRAG: über alle
    /// Einheiten mit OwnerId ungleich b.OwnerId und State ungleich Dead, deren Kachel
    /// (tileMap.WorldToGrid) für b.OwnerId sichtbar ist (IsTileVisible) und deren
    /// Abstand zur Gebäudemitte höchstens b.Core.Stats.Range Kacheln plus eine halbe
    /// Kachel beträgt (Weltpixel, plus 0,25 px Gleitkomma-Rand wie InAttackRange);
    /// Rückgabe die mit dem kleinsten Abstand, bei Gleichstand die zuerst Gelistete.
    /// </summary>
    internal Unit BombardTarget(Building b)
    {
        float reichweite = (b.Core.Stats.Range + 0.5f) * tileMap.TileSize + 0.25f;
        Vector2 mitte = BuildingCenter(b);
        Unit best = null;
        float bestDist = float.MaxValue;
        foreach (Unit u in units)
        {
            if (u.OwnerId == b.OwnerId || u.State == UnitState.Dead)
                continue;
            Vector2 kachel = tileMap.WorldToGrid(u.Position);
            if (!tileMap.IsTileVisible((int)kachel.X, (int)kachel.Y, b.OwnerId))
                continue;
            float d = Vector2.Distance(mitte, u.Position);
            if (d <= reichweite && d < bestDist)
            {
                bestDist = d;
                best = u;
            }
        }
        return best;
    }
}
