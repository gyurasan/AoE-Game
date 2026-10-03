namespace AoE.Core.Entities;

/// <summary>
/// Die fünf Hauptressourcen (nach Age of Empires)
/// </summary>
public enum Resource
{
    /// <summary>
    /// Nahrung - unbegrenzt via Farmen verfügbar
    /// </summary>
    Food,
    
    /// <summary>
    /// Holz - praktisch unbegrenzt verfügbar
    /// </summary>
    Wood,
    
    /// <summary>
    /// Gold - endlich (Minen)
    /// </summary>
    Gold,
    
    /// <summary>
    /// Stein - endlich (Steinbrüche)
    /// </summary>
    Stone,
    
    /// <summary>
    /// Population - Bevölkerungsplätze
    /// </summary>
    Population
}
