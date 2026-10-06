using AoE.Core.Entities;

namespace AoE.Core.Combat;

/// <summary>
/// Schaden an Gebäuden: eigene Einheiten greifen auf Befehl ein fremdes Gebäude an,
/// seine Stärke sinkt Schlag um Schlag, bei 0 ist es zerstört. Bewusst schlicht und
/// nach AoE II: ein Schlag alle <see cref="RELOAD_SECONDS"/>, Nahkampf nach Angriff
/// gegen Rüstung, Pfeile prallen an Holz und Stein fast ganz ab. Der allgemeine
/// <see cref="DamageCalculator"/> zählt bei Gebäuden den Grundangriff doppelt - deshalb
/// diese eigene Regel.
/// </summary>
public static class BuildingCombat
{
    /// <summary>Sekunden zwischen zwei Schlägen: Dorfbewohner, Miliz, Bogenschütze und
    /// Späher schlagen in AoE II alle 2 s zu.</summary>
    public const float RELOAD_SECONDS = 2f;

    /// <summary>Zusätzliche Rüstung eines Gebäudes gegen Fernkampf: Pfeile prallen ab.</summary>
    public const int PIERCE_ARMOR = 6;

    /// <summary>
    /// Schaden eines Schlags von <paramref name="attacker"/> an <paramref name="target"/>.
    /// VERTRAG:
    /// - 0, wenn der Angreifer keinen Angriff hat (Stats.BaseAttack 0) oder das Gebäude
    ///   schon zerstört ist (CurrentHp 0 oder weniger).
    /// - Nahkampf (Stats.Range 0): attacker.Stats.GetTotalAttack(UnitClass.Building) -
    ///   target.Stats.BaseArmor, mindestens 1. GetTotalAttack zählt einen Bonus gegen
    ///   Gebäude schon mit.
    /// - Fernkampf (Stats.Range größer 0): dasselbe gegen target.Stats.BaseArmor +
    ///   PIERCE_ARMOR, ebenfalls mindestens 1.
    /// </summary>
    public static int DamagePerHit(UnitEntity attacker, BuildingEntity target)
    {
        // Kein Angriff oder Gebäude schon zerstört: kein Schaden.
        if (attacker.Stats.BaseAttack <= 0 || target.CurrentHp <= 0)
            return 0;

        // Nahkampf greift gegen die Grundrüstung an, Fernkampf zusätzlich gegen PIERCE_ARMOR.
        int ruestung = target.Stats.BaseArmor + (attacker.Stats.Range > 0 ? PIERCE_ARMOR : 0);
        return Math.Max(1, attacker.Stats.GetTotalAttack(UnitClass.Building) - ruestung);
    }

    /// <summary>
    /// Ein Schlag: zieht <see cref="DamagePerHit"/> von target.CurrentHp ab, nie unter 0.
    /// Rückgabe: true, wenn das Gebäude damit zerstört ist (CurrentHp 0) - auch, wenn es
    /// das vorher schon war.
    /// </summary>
    public static bool Hit(UnitEntity attacker, BuildingEntity target)
    {
        // Einen Schlag abziehen, aber nie unter 0 gehen.
        target.CurrentHp = Math.Max(0, target.CurrentHp - DamagePerHit(attacker, target));
        return target.CurrentHp <= 0;
    }
}
