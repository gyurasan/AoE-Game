namespace AgeOfEmpiresClone.Core.Data;

/// <summary>
/// Represents the four resource types in Age of Empires
/// Food, Wood, Gold, Stone
/// </summary>
public struct Resource
{
    public string Name { get; set; }
    public string IconPath { get; set; }
    public string Description { get; set; }
    
    // Resource quantities
    public int Amount { get; set; }
    
    // Resource type enums
    public enum Type { Food, Wood, Gold, Stone }
    public Type ResourceType { get; set; }
    
    // Source locations (for gathering)
    public enum SourceType { BerryBush, Sheep, Farm, Fish, Tree, GoldMine, StoneQuarry, WildBoar, Deer }
    public SourceType Source { get; set; }
    
    // Conversion rates for deliveries
    public int Capacity { get; set; } = 10; // Max items per trip
    public int HarvestAmount { get; set; } = 1; // Per harvest action
    
    // Constructors
    public Resource(Type type) : this()
    {
        ResourceType = type;
        SetupResourceDetails();
    }
    
    private void SetupResourceDetails()
    {
        switch (ResourceType)
        {
            case Type.Food:
                Name = "Nahrung";
                IconPath = "Sprites/resource_food";
                Description = "Benötigt für Dorfbewohner und Militär";
                Source = SourceType.BerryBush;
                Capacity = 10;
                break;
            case Type.Wood:
                Name = "Holz";
                IconPath = "Sprites/resource_wood";
                Description = "Benötigt für Gebäude und Bogenschützen";
                Source = SourceType.Tree;
                Capacity = 10;
                break;
            case Type.Gold:
                Name = "Gold";
                IconPath = "Sprites/resource_gold";
                Description = "Benötigt für Elite-Einheiten";
                Source = SourceType.GoldMine;
                Capacity = 10;
                break;
            case Type.Stone:
                Name = "Stein";
                IconPath = "Sprites/resource_stone";
                Description = "Benötigt für Mauern undTürme";
                Source = SourceType.StoneQuarry;
                Capacity = 10;
                break;
        }
    }
    
    // Resource collection rates per action
    public int GetGatherRate()
    {
        return HarvestAmount;
    }
    
    // Delivery rates (percentage returned to storage)
    public int GetDeliveryRate()
    {
        // Villagers return 70% of collected resources to storage
        return (int)(Capacity * 0.7f);
    }
}
