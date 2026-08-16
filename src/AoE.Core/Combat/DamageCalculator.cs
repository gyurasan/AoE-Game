using System;
using System.Collections.Generic;
using System.Linq;
using AoE.Core.Entities;

namespace AoE.Core.Combat;

/// <summary>
/// Schadens-Arten im Spiel
/// </summary>
public enum DamageType
{
    /// <summary>
    /// Nahkampf
    /// </summary>
    Physical,
    
    /// <summary>
    /// Fernkampf (Pfeile, etc.)
    /// </summary>
    Ranged,
    
    /// <summary>
    /// Explosion (Trebuchet, Mangonel)
    /// </summary>
    Explosion,
    
    /// <summary>
    /// Magie/Speciell
    /// </summary>
    Special
}

/// <summary>
/// Schadens-Informationen
/// </summary>
public sealed class DamageInfo
{
    /// <summary>
    /// Menge des Schadens
    /// </summary>
    public int Amount { get; set; }
    
    /// <summary>
    /// Art des Schadens
    /// </summary>
    public DamageType Type { get; set; }
    
    /// <summary>
    /// Quelle des Schadens
    /// </summary>
    public UnitClass SourceClass { get; set; }
    
    public DamageInfo(int amount, DamageType type, UnitClass sourceClass)
    {
        Amount = amount;
        Type = type;
        SourceClass = sourceClass;
    }
}

/// <summary>
/// Kämpfer-Interface für alle combat-fähigen Einheiten
/// </summary>
public interface ICombatant
{
    UnitStats Stats { get; }
    UnitClass UnitClass { get; }
    Position Position { get; }
    int CurrentHp { get; }
    bool IsAlive { get; }
    
    /// <summary>
    /// Fügt Schaden zu und gibt true zurück, wenn das Ziel getötet wurde
    /// </summary>
    bool TakeDamage(int damage);
    
    /// <summary>
    /// Berechnet den Schaden gegen ein Ziel
    /// </summary>
    int CalculateDamage(ICombatant target);
    
    /// <summary>
    /// Prüft, ob das Ziel in Reichweite ist
    /// </summary>
    bool InRange(ICombatant target, double distanceSquared);
}

/// <summary>
/// Schadens-Manager für das Konter-System
/// Implementiert die Formel aus der Spezifikation
/// </summary>
public static class DamageCalculator
{
    /// <summary>
    /// Hilfsmethode zum Ermitteln der UnitClass aus dem Typnamen
    /// </summary>
    private static UnitClass GetUnitClassByType(string type)
    {
        return type switch
        {
            "Villager" => UnitClass.Villager,
            "Scout" => UnitClass.Cavalry,
            "Militia" => UnitClass.Infantry,
            "Spearman" => UnitClass.Spearman,
            "Archer" => UnitClass.Archer,
            "Skirmisher" => UnitClass.Skirmisher,
            "Knight" => UnitClass.Cavalry,
            "CamelRider" => UnitClass.Camel,
            "Ram" => UnitClass.SiegeWeapon,
            "Mangonel" => UnitClass.SiegeWeapon,
            "Trebuchet" => UnitClass.SiegeWeapon,
            "Monk" => UnitClass.Monk,
            "Building" => UnitClass.Building,
            _ => UnitClass.Infantry
        };
    }
    
    /// <summary>
    /// Berechnet den Schaden, den Angreifer auf Verteidiger verursacht
    /// Formel: schaden = max(0, Angriff - Rüstung) für jede passende Klasse
    /// </summary>
    public static int CalculateDamage(UnitEntity attacker, UnitEntity defender)
    {
        if (attacker.CurrentHp <= 0 || defender.CurrentHp <= 0)
            return 0;
        
        // Bestimme, ob Fern- oder Nahkampf
        bool isRanged = attacker.Stats.Range > 0;
        DamageType damageType = isRanged ? DamageType.Ranged : DamageType.Physical;
        
        // Grundangriffswert
        int baseAttack = attacker.Stats.BaseAttack;
        
        // Schaden berechnen
        int totalDamage = 0;
        
        // 1. Nahkampf-Schaden (gegen alle Einheiten)
        // physicalBonus ist bereits BaseAttack + Bonus, keinen BaseAttack doppelt zählen
        int physicalBonus = attacker.Stats.GetTotalAttack(UnitClass.Infantry);
        int physicalArmor = defender.Stats.GetTotalArmor(UnitClass.Infantry, isRanged);
        totalDamage += Math.Max(0, physicalBonus - physicalArmor);
        
        // 2. Spezifische Bonus-Schläge basierend auf der Klasse des Verteidigers
        UnitClass defenderClass = GetUnitClassByType(defender.Type);
        
        switch (defenderClass)
        {
            case UnitClass.Cavalry:
            case UnitClass.Camel:
                int cavalryBonus = attacker.Stats.GetTotalAttack(defenderClass);
                int cavalryArmor = defender.Stats.GetTotalArmor(defenderClass, isRanged);
                totalDamage += Math.Max(0, cavalryBonus - cavalryArmor);
                break;
            
            case UnitClass.Archer:
                int archerBonus = attacker.Stats.GetTotalAttack(defenderClass);
                int archerArmor = defender.Stats.GetTotalArmor(defenderClass, isRanged);
                totalDamage += Math.Max(0, archerBonus - archerArmor);
                break;
            
            case UnitClass.Building:
                int buildingBonus = attacker.Stats.GetTotalAttack(UnitClass.Building);
                int buildingArmor = defender.Stats.GetTotalArmor(UnitClass.Building, isRanged);
                totalDamage += Math.Max(0, buildingBonus - buildingArmor);
                break;
            
            case UnitClass.SiegeWeapon:
                int siegeBonus = attacker.Stats.GetTotalAttack(UnitClass.SiegeWeapon);
                int siegeArmor = defender.Stats.GetTotalArmor(UnitClass.SiegeWeapon, isRanged);
                totalDamage += Math.Max(0, siegeBonus - siegeArmor);
                break;
        }
        
        // Mindestens 1 Schaden muss immer durchgehen
        return Math.Max(1, totalDamage);
    }
    
    /// <summary>
    /// Berechnet den Schaden, den ein Angriff verursacht (Vereinfachte Schnittstelle)
    /// </summary>
    public static int CalculateDamage(
        int baseAttack,
        int attackBonus,
        int targetArmor,
        bool isRanged = false)
    {
        // Nahkampf-Angriff - Rüstung abziehen
        int damage = baseAttack + attackBonus - targetArmor;
        return Math.Max(1, damage);
    }
}

/// <summary>
/// Kampf-System für das Spiel
/// </summary>
public sealed class CombatSystem
{
    /// <summary>
    /// Fügt einer Einheit Schaden zu
    /// </summary>
    public static bool ApplyDamage(UnitEntity target, int damage)
    {
        return target.TakeDamage(damage);
    }
    
    /// <summary>
    /// Prüft, ob zwei Einheiten in Reichweite sind
    /// </summary>
    public static bool InRange(ICombatant attacker, ICombatant target)
    {
        double distanceSquared = attacker.Position.DistanceSquared(target.Position);
        return InRange(attacker, target, distanceSquared);
    }
    
    /// <summary>
    /// Prüft, ob zwei Einheiten in Reichweite sind (mit vorkalkulierter Distanz)
    /// </summary>
    public static bool InRange(ICombatant attacker, ICombatant target, double distanceSquared)
    {
        int range = attacker.Stats.Range;
        if (range == 0)
        {
            // Nahkampf: muss direkt angrenzen (1 Tile)
            return distanceSquared <= 1.0;
        }
        else
        {
            // Fernkampf: nach Range
            return distanceSquared <= range * range;
        }
    }
}
