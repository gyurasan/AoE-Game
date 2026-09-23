using AoE.Core.Combat;
using AoE.Core.Entities;
using Xunit;

namespace AoE.Tests;

/// <summary>
/// Prüft das Konter-Dreieck über den tatsächlich zugefügten Schaden.
///
/// Die bestehenden Tests prüfen einzelne Angriffsboni. Diese hier prüfen, was
/// daraus im Kampf wird — also genau den Pfad, den das Spiel seit B2 verwendet
/// (<c>Unit.DamageAgainst</c> ruft <see cref="DamageCalculator.CalculateDamage"/>).
///
/// Bewusst als Vergleiche formuliert und nicht auf feste Zahlen: die Aussage
/// „Speerkämpfer sind gut gegen Reiter" soll auch nach einer Balance-Änderung
/// noch gelten, ohne dass der Test dann fälschlich rot wird.
/// </summary>
public class CounterTriangleTests
{
    private static readonly Position Origin = new(0, 0);

    [Fact]
    public void Spearman_TrifftKavallerieHaerterAlsBogenschuetzen()
    {
        var spearman = new SpearMan(0, Origin);

        int gegenReiter = DamageCalculator.CalculateDamage(spearman, new Knight(1, Origin));
        int gegenSchuetzen = DamageCalculator.CalculateDamage(spearman, new Archer(1, Origin));

        Assert.True(
            gegenReiter > gegenSchuetzen,
            $"Speerkämpfer sollen Reiter kontern: {gegenReiter} gegen Reiter, "
            + $"{gegenSchuetzen} gegen Bogenschützen.");
    }

    [Fact]
    public void Knight_TrifftBogenschuetzenHaerterAlsSpeerkaempfer()
    {
        var knight = new Knight(0, Origin);

        int gegenSchuetzen = DamageCalculator.CalculateDamage(knight, new Archer(1, Origin));
        int gegenSpeer = DamageCalculator.CalculateDamage(knight, new SpearMan(1, Origin));

        Assert.True(
            gegenSchuetzen > gegenSpeer,
            $"Reiter sollen Bogenschützen kontern: {gegenSchuetzen} gegen Schützen, "
            + $"{gegenSpeer} gegen Speerkämpfer.");
    }

    [Fact]
    public void Ram_TrifftGebaeudeHaerterAlsInfanterie()
    {
        var ram = new Ram(0, Origin);

        int gegenGebaeude = ram.Stats.GetTotalAttack(UnitClass.Building);
        int gegenInfanterie = ram.Stats.GetTotalAttack(UnitClass.Infantry);

        Assert.True(
            gegenGebaeude > gegenInfanterie,
            $"Rammböcke sind Belagerungsgerät: {gegenGebaeude} gegen Gebäude, "
            + $"{gegenInfanterie} gegen Infanterie.");
    }

    [Fact]
    public void Schaden_IstImmerMindestensEins()
    {
        // Der Dorfbewohner hat praktisch keinen Angriff, der Ritter Rüstung.
        // Die Formel garantiert trotzdem einen Treffer — sonst wäre eine
        // Einheit gegen bestimmte Gegner unsterblich.
        var villager = new Villager(0, Origin);
        int schaden = DamageCalculator.CalculateDamage(villager, new Knight(1, Origin));

        Assert.True(schaden >= 1, $"Mindestschaden verletzt: {schaden}");
    }

    [Fact]
    public void ToteEinheitenRichtenKeinenSchadenAn()
    {
        var spearman = new SpearMan(0, Origin);
        spearman.CurrentHp = 0;

        int schaden = DamageCalculator.CalculateDamage(spearman, new Knight(1, Origin));

        Assert.Equal(0, schaden);
    }
}
