namespace AoE.Core.Entities;

/// <summary>
/// Einheitenklassen für das Konter-System
/// </summary>
public enum UnitClass
{
    /// <summary>
    /// Infanterie - Schwertkämpfer, Miliz
    /// </summary>
    Infantry,
    
    /// <summary>
    /// Speerkämpfer/Pikeniere - besonders gegen Kavallerie
    /// </summary>
    Spearman,
    
    /// <summary>
    /// Bogenschützen
    /// </summary>
    Archer,
    
    /// <summary>
    /// Skirmisher - besonders gegen Bogenschützen
    /// </summary>
    Skirmisher,
    
    /// <summary>
    /// Kavallerie - Ritter
    /// </summary>
    Cavalry,
    
    /// <summary>
    /// Kamelreiter - gegen Kavallerie
    /// </summary>
    Camel,
    
    /// <summary>
    /// Belagerungswaffen allgemein
    /// </summary>
    SiegeWeapon,
    
    /// <summary>
    /// Gebäude
    /// </summary>
    Building,
    
    /// <summary>
    /// Dorfbewohner
    /// </summary>
    Villager,
    
    /// <summary>
    /// Mönch
    /// </summary>
    Monk
}
