using System;
using System.Collections.Generic;
using AoE.Core.Combat;

namespace AoE.Core.Entities;

/// <summary>
/// Position auf dem Spielfeld als X/Y-Koordinaten
/// </summary>
public record struct Position(int X, int Y)
{
    /// <summary>
    /// Entfernung zur Quadratwurzel (für Performance)
    /// </summary>
    public double DistanceSquared(Position other) => 
        (X - other.X) * (X - other.X) + (Y - other.Y) * (Y - other.Y);
    
    /// <summary>
    /// Euclidean distance
    /// </summary>
    public double Distance(Position other) => Math.Sqrt(DistanceSquared(other));
    
    public override string ToString() => $"({X}, {Y})";
}

/// <summary>
/// Einheitenausstattung mit Angriffs- und Rüstungswerten pro Klasse
/// </summary>
public class UnitStats
{
    private readonly Dictionary<UnitClass, int> _attackValues = new();
    private readonly Dictionary<UnitClass, int> _armorValues = new();
    
    /// <summary>
    /// Basis-HP der Einheit
    /// </summary>
    public int HitPoints { get; set; }
    
    /// <summary>
    /// Basis-Angriff
    /// </summary>
    public int BaseAttack { get; set; }
    
    /// <summary>
    /// Basis-Rüstung
    /// </summary>
    public int BaseArmor { get; set; }
    
    /// <summary>
    /// Angriffsreichweite (0 = Nahkampf)
    /// </summary>
    public int Range { get; set; }
    
    /// <summary>
    /// Geschwindigkeit (Units pro Sekunde)
    /// </summary>
    public float Speed { get; set; } = 1.0f;
    
    /// <summary>
    /// Sichtweite (in Tiles)
    /// </summary>
    public int VisionRange { get; set; } = 3;
    
    /// <summary>
    /// Erstellt einen neuen Stats-Block mit Standardwerten
    /// </summary>
    public UnitStats()
    {
        // Standardwerte für Nah- und Fernkampf
        _attackValues[UnitClass.Infantry] = 0;
        _attackValues[UnitClass.Archer] = 0;
        _attackValues[UnitClass.Cavalry] = 0;
        _attackValues[UnitClass.SiegeWeapon] = 0;
        _attackValues[UnitClass.Building] = 0;
        
        _armorValues[UnitClass.Infantry] = 0;
        _armorValues[UnitClass.Archer] = 0;
        _armorValues[UnitClass.SiegeWeapon] = 0;
    }
    
    /// <summary>
    /// Fügt einen Angriffswert gegen eine Klasse hinzu
    /// </summary>
    public void AddAttackBonus(UnitClass targetClass, int bonus)
    {
        _attackValues[targetClass] = bonus;
    }
    
    /// <summary>
    /// Fügt einen Rüstungswert gegen eine Klasse hinzu
    /// </summary>
    public void AddArmorBonus(UnitClass sourceClass, int bonus)
    {
        _armorValues[sourceClass] = bonus;
    }
    
    /// <summary>
    /// Berechnet den Gesamtangriff gegen eine Einheit
    /// </summary>
    public int GetTotalAttack(UnitClass targetClass)
    {
        int total = BaseAttack;
        
        if (_attackValues.TryGetValue(targetClass, out int bonus))
            total += bonus;
        else if (_attackValues.TryGetValue(UnitClass.Infantry, out bonus))
            total += bonus;
        
        return total;
    }
    
    /// <summary>
    /// Berechnet die Gesamtrüstung gegen eine Schadensart
    /// </summary>
    public int GetTotalArmor(UnitClass sourceClass, bool isRanged)
    {
        int armor = BaseArmor;
        
        if (isRanged && _armorValues.TryGetValue(UnitClass.Archer, out int archerArmor))
            armor += archerArmor;
        
        if (_armorValues.TryGetValue(sourceClass, out int classArmor))
            armor += classArmor;
        
        return armor;
    }
}

/// <summary>
/// Basis-Klasse für alle Einheiten im Spiel
/// </summary>
public abstract class UnitEntity
{
    private static int _nextId = 1;
    
    /// <summary>
    /// Eindeutige ID der Einheit
    /// </summary>
    public int Id { get; } = _nextId++;
    
    /// <summary>
    /// Typ der Einheit
    /// </summary>
    public abstract string Type { get; }
    
    /// <summary>
    /// Aktuelle Position
    /// </summary>
    public Position Position { get; set; }
    
    /// <summary>
    /// Aktuelle HP
    /// </summary>
    public int CurrentHp { get; set; }
    
    /// <summary>
    /// Status (Wachen, Sammeln, Bewegen, etc.)
    /// </summary>
    public UnitState State { get; set; } = UnitState.Idle;
    
    /// <summary>
    /// Spielerbesitzer
    /// </summary>
    public int OwnerId { get; set; }
    
    /// <summary>
    /// Statistiken der Einheit
    /// </summary>
    protected UnitStats _stats;
    
    /// <summary>
    /// Konvertiert Einheit lebendig ist
    /// </summary>
    public bool IsAlive => CurrentHp > 0;
    
    /// <summary>
    /// Statistiken (öffentlich)
    /// </summary>
    public UnitStats Stats => _stats;
    
    /// <summary>
    /// Zielposition für Bewegung
    /// </summary>
    public Position? TargetPosition { get; set; }
    
    public UnitEntity(int ownerId, Position position, UnitStats stats)
    {
        OwnerId = ownerId;
        Position = position;
        _stats = stats;
        CurrentHp = stats.HitPoints;
    }
    
    /// <summary>
    /// Nimmt Schaden und gibt true zurück, wenn die Einheit getötet wurde
    /// </summary>
    public virtual bool TakeDamage(int damage)
    {
        CurrentHp = Math.Max(0, CurrentHp - damage);
        return CurrentHp == 0;
    }
    
    /// <summary>
    /// Heilt die Einheit (maximal zur Maximal-Hp)
    /// </summary>
    public virtual void Heal(int amount)
    {
        CurrentHp = Math.Min(_stats.HitPoints, CurrentHp + amount);
    }
}

/// <summary>
/// Status einer Einheit
/// </summary>
public enum UnitState
{
    Idle,
    Gathering,
    Moving,
    Attacking,
    Building,
    Researching,
    Training,
    Healing,
    Converting,
    Guarding
}
