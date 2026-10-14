namespace AoE.Core.Economy;

/// <summary>
/// Startkapital je Spieler — die Menü-Option „Startkapital
/// (Age of Evolutions)". <c>Normal</c> sind die bisherigen festen Werte,
/// <c>Arm</c> halb so viel, <c>Reich</c> das Doppelte: dieselbe Spielwelt,
/// anderes Anfangsguthaben.
/// </summary>
public enum StartingCapital
{
    Arm = 0,
    Normal = 1,
    Reich = 2,
}

/// <summary>
/// Das Anfangsguthaben (Nahrung, Holz, Gold, Stein) je Stufe — reiner Nachschlag,
/// ohne Spiel-Logik. <c>Normal</c> hält exakt die bisherigen
/// <see cref="ResourcePool"/>-Startwerte.
/// </summary>
public static class StartingCapitalRules
{
    /// <summary>Die Startwerte einer Stufe als (Nahrung, Holz, Gold, Stein).</summary>
    public static (int Food, int Wood, int Gold, int Stone) Values(StartingCapital capital)
        => capital switch
        {
            StartingCapital.Arm => (100, 100, 50, 100),
            StartingCapital.Reich => (400, 400, 200, 400),
            _ => (ResourcePool.START_FOOD, ResourcePool.START_WOOD,
                  ResourcePool.START_GOLD, ResourcePool.START_STONE),
        };
}
