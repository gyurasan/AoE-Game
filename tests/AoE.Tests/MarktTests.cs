using AoE.Core.Economy;
using AoE.Core.Entities;
using Xunit;
using DataMap = AgeOfEvolutions.Core.Data.TileMap;
using DataPlayer = AgeOfEvolutions.Core.Data.Player;
using MapSettings = AgeOfEvolutions.Core.Data.MapSettings;
using MapSize = AgeOfEvolutions.Core.Data.MapSize;
using SaveGameMapper = AgeOfEvolutions.Core.Data.SaveGameMapper;
using SaveGameStore = AgeOfEvolutions.Core.Data.SaveGameStore;

namespace AoE.Tests;

/// <summary>
/// Kurse am Markt (F8): Nahrung, Holz und Stein gegen Gold, in Losen zu 100, zu einem Kurs, der
/// sich je Handel in dieselbe Richtung verschlechtert und ohne Handel langsam zurückkehrt -
/// die Notlösung, wenn Gold oder Stein ausgeht (Aufgabe F8 in der Aufgabenliste).
/// </summary>
public class MarktTests
{
    private static ResourcePool Kasse(int nahrung = 0, int holz = 0, int gold = 0, int stein = 0)
    {
        return new ResourcePool(nahrung, holz, gold, stein);
    }

    [Fact]
    public void ZuBeginnKosten100Stueck130GoldUndBringen70()
    {
        var markt = new MarketPrices();
        foreach (var gut in MarketPrices.Goods)
        {
            Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(gut));
            Assert.Equal(130, markt.BuyCost(gut));
            Assert.Equal(70, markt.SellGain(gut));
        }
        Assert.Equal(new[] { Resource.Food, Resource.Wood, Resource.Stone }, MarketPrices.Goods);
    }

    [Theory]
    [InlineData(Resource.Food, true)]
    [InlineData(Resource.Wood, true)]
    [InlineData(Resource.Stone, true)]
    [InlineData(Resource.Gold, false)]
    [InlineData(Resource.Population, false)]
    public void GehandeltWerdenNahrungHolzUndSteinGegenGold(Resource r, bool gut)
    {
        Assert.Equal(gut, MarketPrices.IsGood(r));
        var markt = new MarketPrices();
        var kasse = Kasse(500, 500, 500, 500);
        Assert.Equal(gut, markt.TryBuy(r, kasse));
        Assert.Equal(gut, markt.TrySell(r, kasse));
        if (!gut)
            Assert.Equal(0f, markt.PriceOf(r));
    }

    [Fact]
    public void KaufenZahltGoldBringtHundertUndMachtTeurer()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(holz: 20, gold: 300);

        Assert.True(markt.TryBuy(Resource.Wood, kasse));

        Assert.Equal(120, kasse[Resource.Wood]);
        Assert.Equal(170, kasse[Resource.Gold]);
        Assert.Equal(MarketPrices.START_PRICE + MarketPrices.PRICE_STEP, markt.PriceOf(Resource.Wood));
        Assert.Equal(134, markt.BuyCost(Resource.Wood));
        // Die anderen Güter bleiben, wie sie sind
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Food));
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Stone));
    }

    [Fact]
    public void VerkaufenBringtGoldUndMachtBilliger()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(stein: 250, gold: 0);

        Assert.True(markt.TrySell(Resource.Stone, kasse));

        Assert.Equal(150, kasse[Resource.Stone]);
        Assert.Equal(70, kasse[Resource.Gold]);
        Assert.Equal(MarketPrices.START_PRICE - MarketPrices.PRICE_STEP, markt.PriceOf(Resource.Stone));
        Assert.Equal(68, markt.SellGain(Resource.Stone));
    }

    [Fact]
    public void OhneGenugGoldOderWareAendertSichNichts()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(nahrung: 99, holz: 500, gold: 129, stein: 0);

        Assert.False(markt.TryBuy(Resource.Food, kasse));     // 130 Gold nötig
        Assert.False(markt.TrySell(Resource.Food, kasse));    // 100 Nahrung nötig
        Assert.False(markt.TrySell(Resource.Stone, kasse));

        Assert.Equal(99, kasse[Resource.Food]);
        Assert.Equal(129, kasse[Resource.Gold]);
        Assert.Equal(0, kasse[Resource.Stone]);
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Food));
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Stone));
    }

    [Fact]
    public void JederHandelInDieselbeRichtungWirdUnguenstiger()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(gold: 100000, nahrung: 100000);
        int vorher = markt.BuyCost(Resource.Food);
        for (int i = 0; i < 10; i++)
        {
            markt.TryBuy(Resource.Food, kasse);
            Assert.True(markt.BuyCost(Resource.Food) > vorher);
            vorher = markt.BuyCost(Resource.Food);
        }

        var zweiter = new MarketPrices();
        int erloes = zweiter.SellGain(Resource.Food);
        for (int i = 0; i < 10; i++)
        {
            zweiter.TrySell(Resource.Food, kasse);
            Assert.True(zweiter.SellGain(Resource.Food) < erloes);
            erloes = zweiter.SellGain(Resource.Food);
        }
    }

    [Fact]
    public void KaufenUndGleichWiederVerkaufenKostetGold()
    {
        // Der Aufschlag hält den Markt als Notlösung: hin und zurück bleibt Gold liegen
        var markt = new MarketPrices();
        var kasse = Kasse(gold: 1000);
        markt.TryBuy(Resource.Wood, kasse);
        markt.TrySell(Resource.Wood, kasse);
        Assert.Equal(0, kasse[Resource.Wood]);
        Assert.True(kasse[Resource.Gold] < 1000 - 50, $"nur {1000 - kasse[Resource.Gold]} Gold verloren");
    }

    [Fact]
    public void DerKursBleibtZwischenUntergrenzeUndObergrenze()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(stein: 1_000_000, gold: 10_000_000);
        markt.Trade(Resource.Stone, buy: false, 200, kasse);
        Assert.Equal(MarketPrices.MIN_PRICE, markt.PriceOf(Resource.Stone));
        Assert.True(markt.SellGain(Resource.Stone) > 0);

        markt.Trade(Resource.Stone, buy: true, 2000, kasse);
        Assert.Equal(MarketPrices.MAX_PRICE, markt.PriceOf(Resource.Stone));
    }

    [Fact]
    public void MehrereLoseHandelnJedesZumDannGueltigenKursBisEsNichtMehrReicht()
    {
        var markt = new MarketPrices();
        // 130 + 134 + 138 = 402: das vierte Los (142) geht nicht mehr
        var kasse = Kasse(gold: 410);
        Assert.Equal(3, markt.Trade(Resource.Food, buy: true, 5, kasse));
        Assert.Equal(300, kasse[Resource.Food]);
        Assert.Equal(8, kasse[Resource.Gold]);

        var verkauf = Kasse(holz: 250);
        Assert.Equal(2, new MarketPrices().Trade(Resource.Wood, buy: false, 5, verkauf));
        Assert.Equal(50, verkauf[Resource.Wood]);
        Assert.Equal(70 + 68, verkauf[Resource.Gold]);
    }

    [Fact]
    public void OhneHandelKehrtDerKursLangsamZurueck()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(gold: 10000, stein: 10000);
        markt.Trade(Resource.Food, buy: true, 2, kasse);    // 106
        markt.TrySell(Resource.Stone, kasse);                // 97

        markt.Update(10f);   // 1 je 10 s
        Assert.Equal(105f, markt.PriceOf(Resource.Food), 3);
        Assert.Equal(98f, markt.PriceOf(Resource.Stone), 3);

        // Ein gehandeltes Los ist nach 30 s erholt - und es geht nicht darüber hinaus
        markt.Update(30f);
        Assert.Equal(102f, markt.PriceOf(Resource.Food), 3);
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Stone));
        markt.Update(1000f);
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Food));
        Assert.Equal(MarketPrices.START_PRICE, markt.PriceOf(Resource.Wood));
    }

    [Fact]
    public void EinKursGiltFuerAlleSpieler()
    {
        // Wie in AoE II: wer kauft, macht das Gut auch für den anderen teurer
        var markt = new MarketPrices();
        var ich = Kasse(gold: 1000);
        var gegner = Kasse(gold: 1000);
        markt.TryBuy(Resource.Stone, ich);
        Assert.True(markt.TryBuy(Resource.Stone, gegner));
        Assert.Equal(1000 - 134, gegner[Resource.Gold]);
    }

    [Fact]
    public void KurseLassenSichSpeichernUndWiederherstellen()
    {
        var markt = new MarketPrices();
        var kasse = Kasse(gold: 10000, holz: 10000);
        markt.Trade(Resource.Wood, buy: false, 4, kasse);
        markt.TryBuy(Resource.Food, kasse);
        markt.Update(2.5f);

        var kopie = new MarketPrices();
        kopie.Restore(markt.ToState());
        foreach (var gut in MarketPrices.Goods)
            Assert.Equal(markt.PriceOf(gut), kopie.PriceOf(gut));

        // Unsinnige Werte landen in den Grenzen; null ändert nichts
        kopie.Restore(new MarketPricesState(-5f, 5000f, 50f));
        Assert.Equal(MarketPrices.MIN_PRICE, kopie.PriceOf(Resource.Food));
        Assert.Equal(MarketPrices.MAX_PRICE, kopie.PriceOf(Resource.Wood));
        kopie.Restore(null!);
        Assert.Equal(50f, kopie.PriceOf(Resource.Stone));
    }

    // --- Spielstand ------------------------------------------------------------

    private static DataMap Karte() => new(40, 30, 32, MapSettings.ForSize(MapSize.Test));

    [Fact]
    public void SpielstandBehaeltDieKurse()
    {
        var karte = Karte();
        var p1 = new DataPlayer(0, "Spieler", "Briten");
        var p2 = new DataPlayer(1, "Gegner", "Azteken");
        p1.Resources.Add(Resource.Gold, 1000);
        karte.Market.Trade(Resource.Stone, buy: true, 3, p1.Resources);
        karte.Market.TrySell(Resource.Food, p1.Resources);
        karte.Market.Update(4f);

        string json = SaveGameStore.Serialize(SaveGameMapper.Capture(karte, p1, p2, MapSize.Test));
        var (geladen, g1, g2) = SaveGameMapper.Restore(SaveGameStore.Deserialize(json));

        foreach (var gut in MarketPrices.Goods)
            Assert.Equal(karte.Market.PriceOf(gut), geladen.Market.PriceOf(gut));
        // Ringschluss: noch einmal gespeichert, Zeichen für Zeichen gleich
        Assert.Equal(json, SaveGameStore.Serialize(SaveGameMapper.Capture(geladen, g1, g2, MapSize.Test)));
    }

    [Fact]
    public void AelterSpielstandOhneKurseBeginntMitDenStartkursen()
    {
        var karte = Karte();
        var p1 = new DataPlayer(0, "Spieler", "Briten");
        var p2 = new DataPlayer(1, "Gegner", "Azteken");
        p1.Resources.Add(Resource.Gold, 500);
        Assert.True(karte.Market.TryBuy(Resource.Wood, p1.Resources));
        var save = SaveGameMapper.Capture(karte, p1, p2, MapSize.Test);
        save.Market = null!;

        string json = SaveGameStore.Serialize(save);
        Assert.DoesNotContain("\"Market\"", json);
        var (geladen, _, _) = SaveGameMapper.Restore(SaveGameStore.Deserialize(json));

        foreach (var gut in MarketPrices.Goods)
            Assert.Equal(MarketPrices.START_PRICE, geladen.Market.PriceOf(gut));
    }
}
