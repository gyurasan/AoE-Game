using AgeOfEvolutions.Core.Data;
using AoE.Core.Economy;
using Xunit;
using Resource = AoE.Core.Entities.Resource;

namespace AoE.Tests;

/// <summary>Bevölkerungsplätze je Einheitentyp (F15): Rammbock und Katapult
/// belegen 2, der Tribok 3, sonst eins — und die Ausbildungswarteschlange
/// lässt ein Gerät nur ausbilden, wenn sein Bedarf noch durch die Grenze passt.</summary>
public class PopulationSlotsTests
{
    [Theory]
    [InlineData(UnitType.Ram, 2)]
    [InlineData(UnitType.Catapult, 2)]
    [InlineData(UnitType.Trebuchet, 3)]
    [InlineData(UnitType.Villager, 1)]
    [InlineData(UnitType.Archer, 1)]
    public void Bevoelkerungsplaetze_je_Spieltyp(UnitType typ, int erwartet)
        => Assert.Equal(erwartet, CoreUnits.PopulationSlots(typ));

    [Fact]
    public void Tribok_bildet_nicht_aus_wenn_nur_zwei_Plaetze_frei_sind()
    {
        var queue = new TrainingQueue<UnitType>();
        var pool = new ResourcePool();   // 200 Holz Startkapital reichen für die Kosten von 1
        Assert.True(queue.Enqueue(UnitType.Trebuchet, new() { [Resource.Wood] = 1 }, 1f, pool));

        // 198 von 200 belegt: nur 2 frei, der Tribok braucht 3 — er steht still.
        Assert.False(queue.Update(2f, 198, 200, t => CoreUnits.PopulationSlots(t), out _));
        // 199 von 200: erst recht nicht.
        Assert.False(queue.Update(2f, 199, 200, t => CoreUnits.PopulationSlots(t), out _));
        // 197 von 200: genau 3 frei, 197 + 3 <= 200 — er bildet sich aus.
        Assert.True(queue.Update(2f, 197, 200, t => CoreUnits.PopulationSlots(t), out var fertig));
        Assert.Equal(UnitType.Trebuchet, fertig);
    }

    [Fact]
    public void Alte_Ueberladung_zaehlt_weiter_je_Einheit_eins()
    {
        var queue = new TrainingQueue<UnitType>();
        var pool = new ResourcePool();
        Assert.True(queue.Enqueue(UnitType.Trebuchet, new() { [Resource.Wood] = 1 }, 1f, pool));
        // Ohne Platzbedarf-Funktion gilt wie bisher: ein Platz, 199 passt in 200.
        Assert.True(queue.Update(2f, 199, 200, out var fertig));
        Assert.Equal(UnitType.Trebuchet, fertig);
    }
}
