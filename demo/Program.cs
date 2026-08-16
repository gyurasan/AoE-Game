using AoE.Core.Entities;
using AoE.Core.Combat;
using AoE.Core.Economy;

// Demo: Age of Empires II Core Game Logic
Console.WriteLine("=== Age of Empires II - Core Logic Demo ===\n");

// 1. Ressourcen-System testen
Console.WriteLine("1. Ressourcen-System:");
var resources = new ResourcePool();
Console.WriteLine($"  Anfang: Food={resources[Resource.Food]}, Wood={resources[Resource.Wood]}, Gold={resources[Resource.Gold]}, Stone={resources[Resource.Stone]}");

resources.Add(Resource.Food, 100);
resources.Add(Resource.Wood, 50);
Console.WriteLine($"  Nach Sammeln: Food={resources[Resource.Food]}, Wood={resources[Resource.Wood]}");

if (resources.Remove(Resource.Gold, 50))
    Console.WriteLine("  Gold erfolgreich abgezogen (50)");
else
    Console.WriteLine("  Gold-Auszahlung gescheitert (nicht genug)");

// 2. Einheiten erstellen und bewegen
Console.WriteLine("\n2. Einheiten-System:");
var villager = new Villager(0, new Position(5, 5));
var archer = new Archer(1, new Position(10, 10));
var knight = new Knight(0, new Position(15, 15));

Console.WriteLine($"  Villager @ {villager.Position}, HP={villager.CurrentHp}");
Console.WriteLine($"  Archer @ {archer.Position}, Range={archer.Stats.Range}");
Console.WriteLine($"  Knight @ {knight.Position}, Attack={knight.Stats.BaseAttack}");

// 3. Kampf-System testen
Console.WriteLine("\n3. Kampf-System (Konter-Beispiel):");

// Speer vs Ritter (Speer hat Bonus gegen Kavallerie)
var spearman = new SpearMan(0, new Position(0, 0));
var ram = new Ram(0, new Position(5, 5));

int damage1 = DamageCalculator.CalculateDamage(spearman, ram);
Console.WriteLine($"  Speer vs Rammböcker: {damage1} Schaden");

// Ritter vs Bogenschütze (Ritter hat Bonus gegen Bogenschützen)
int damage2 = DamageCalculator.CalculateDamage(knight, archer);
Console.WriteLine($"  Ritter vs Bogenschütze: {damage2} Schaden");

// 4. Dorfbewohner-Loop demo
Console.WriteLine("\n4. Dorfbewohner-Verhalten:");
Console.WriteLine($"  Villager Status: {villager.State}");
Console.WriteLine($"  Villager Sammelgeschwindigkeit: {villager.Stats.Speed}");
Console.WriteLine($"  Villager Sichtweite: {villager.Stats.VisionRange}");

// 5. Einheiten-Größe testen
Console.WriteLine("\n5. Einheiten-Größe (Angriff/Rüstung):");
Console.WriteLine($"  Speer: BaseAttack={spearman.Stats.BaseAttack}, HP={spearman.Stats.HitPoints}");
Console.WriteLine($"  Ram: BaseAttack={ram.Stats.BaseAttack}, HP={ram.Stats.HitPoints}, BuildingBonus={ram.Stats.GetTotalAttack(UnitClass.Building)}");
Console.WriteLine($"  Knight: BaseAttack={knight.Stats.BaseAttack}, ArcherBonus={knight.Stats.GetTotalAttack(UnitClass.Archer)}");

Console.WriteLine("\n=== Demo beendet ===");
Console.WriteLine("Press Enter to exit...");
Console.ReadLine();