using System;
using AoE.Core.Entities;

namespace AoE.Core.Economy;

/// <summary>
/// Die Kurse am Markt (F8): Nahrung, Holz und Stein lassen sich in Losen zu
/// <see cref="LOT"/> gegen Gold kaufen und verkaufen - die Notlösung, wenn eine
/// Ressource ausgeht. Wie in AoE II gilt ein Kurs für alle Spieler; wer kauft, macht
/// das Gut für alle teurer, wer verkauft, billiger.
/// <list type="bullet">
///   <item>Der Kurs eines Guts ist der Goldwert von 100 Einheiten, anfangs
///         <see cref="START_PRICE"/>.</item>
///   <item>Kaufen kostet den Kurs plus <see cref="COMMISSION"/> (anfangs 130 Gold), Verkaufen
///         bringt den Kurs minus <see cref="COMMISSION"/> (anfangs 70 Gold), beides auf ganzes
///         Gold gerundet.</item>
///   <item>Jedes gekaufte Los hebt den Kurs um <see cref="PRICE_STEP"/>, jedes verkaufte senkt
///         ihn darum - der Handel in dieselbe Richtung wird immer ungünstiger. Der Kurs bleibt
///         zwischen <see cref="MIN_PRICE"/> und <see cref="MAX_PRICE"/>.</item>
///   <item>Ohne Handel kehrt jeder Kurs langsam zu <see cref="START_PRICE"/> zurück,
///         <see cref="RECOVERY_PER_SECOND"/> je Sekunde (<see cref="Update"/>).</item>
/// </list>
/// Reine Logik ohne MonoGame; den Markt als Gebäude prüft das Spiel.
/// </summary>
public sealed class MarketPrices
{
    /// <summary>So viele Einheiten wechseln je Handel.</summary>
    public const int LOT = 100;

    /// <summary>Kurs zu Spielbeginn: Goldwert von 100 Einheiten.</summary>
    public const float START_PRICE = 100f;

    /// <summary>Tiefster und höchster Kurs.</summary>
    public const float MIN_PRICE = 20f;
    public const float MAX_PRICE = 1000f;

    /// <summary>So weit verschiebt ein gehandeltes Los den Kurs.</summary>
    public const float PRICE_STEP = 3f;

    /// <summary>Aufschlag beim Kauf und Abschlag beim Verkauf, Anteil am Kurs.</summary>
    public const float COMMISSION = 0.3f;

    /// <summary>So weit kehrt ein Kurs je Sekunde zu START_PRICE zurück: ein Los in 30 s.</summary>
    public const float RECOVERY_PER_SECOND = 0.1f;

    /// <summary>Die Güter des Markts in der Reihenfolge der Tasten; Gold ist das Zahlungsmittel.</summary>
    public static readonly Resource[] Goods = { Resource.Food, Resource.Wood, Resource.Stone };

    private float _food = START_PRICE, _wood = START_PRICE, _stone = START_PRICE;

    /// <summary>Ob <paramref name="resource"/> am Markt gehandelt wird: Nahrung, Holz, Stein.</summary>
    public static bool IsGood(Resource resource)
        => resource is Resource.Food or Resource.Wood or Resource.Stone;

    /// <summary>Der Kurs des Guts (Goldwert von 100 Einheiten); 0 für alles, was kein Gut ist.</summary>
    public float PriceOf(Resource good) => good switch
    {
        Resource.Food => _food,
        Resource.Wood => _wood,
        Resource.Stone => _stone,
        _ => 0f,
    };

    /// <summary>Was ein Los des Guts gerade kostet: Kurs · (1 + COMMISSION), auf ganzes Gold gerundet.</summary>
    public int BuyCost(Resource good)
        => (int)Math.Round(PriceOf(good) * (1.0 + COMMISSION), MidpointRounding.AwayFromZero);

    /// <summary>Was ein Los des Guts gerade einbringt: Kurs · (1 - COMMISSION), auf ganzes Gold gerundet.</summary>
    public int SellGain(Resource good)
        => (int)Math.Round(PriceOf(good) * (1.0 - COMMISSION), MidpointRounding.AwayFromZero);

    /// <summary>
    /// Kauft ein Los des Guts: zieht <see cref="BuyCost"/> Gold von <paramref name="pool"/> ab,
    /// schreibt <see cref="LOT"/> Einheiten gut und hebt den Kurs um PRICE_STEP (höchstens auf
    /// MAX_PRICE). false ohne jede Änderung, wenn es kein Gut ist oder das Gold nicht reicht.
    /// </summary>
    public bool TryBuy(Resource good, ResourcePool pool)
    {
        if (!IsGood(good) || pool == null)
            return false;
        if (!pool.Remove(Resource.Gold, BuyCost(good)))
            return false;
        pool.Add(good, LOT);
        Shift(good, PRICE_STEP);
        return true;
    }

    /// <summary>
    /// Verkauft ein Los des Guts: zieht <see cref="LOT"/> Einheiten von <paramref name="pool"/> ab,
    /// schreibt <see cref="SellGain"/> Gold gut und senkt den Kurs um PRICE_STEP (höchstens auf
    /// MIN_PRICE). false ohne jede Änderung, wenn es kein Gut ist oder keine LOT Einheiten da sind.
    /// </summary>
    public bool TrySell(Resource good, ResourcePool pool)
    {
        if (!IsGood(good) || pool == null)
            return false;
        int gain = SellGain(good);
        if (!pool.Remove(good, LOT))
            return false;
        pool.Add(Resource.Gold, gain);
        Shift(good, -PRICE_STEP);
        return true;
    }

    /// <summary>
    /// Bis zu <paramref name="lots"/> Lose nacheinander kaufen (<paramref name="buy"/>) oder
    /// verkaufen, jedes zum dann gültigen Kurs; hört beim ersten auf, das nicht geht.
    /// Rückgabe: die Zahl der gehandelten Lose.
    /// </summary>
    public int Trade(Resource good, bool buy, int lots, ResourcePool pool)
    {
        int done = 0;
        while (done < lots && (buy ? TryBuy(good, pool) : TrySell(good, pool)))
            done++;
        return done;
    }

    /// <summary>
    /// Lässt jeden Kurs <paramref name="dt"/> Sekunden lang zu START_PRICE zurückkehren,
    /// RECOVERY_PER_SECOND je Sekunde, ohne darüber hinaus.
    /// </summary>
    public void Update(float dt)
    {
        if (dt <= 0f)
            return;
        float step = RECOVERY_PER_SECOND * dt;
        _food = Toward(_food, step);
        _wood = Toward(_wood, step);
        _stone = Toward(_stone, step);
    }

    /// <summary>Die Kurse für den Spielstand.</summary>
    public MarketPricesState ToState() => new(_food, _wood, _stone);

    /// <summary>Setzt die Kurse aus dem Spielstand, begrenzt auf MIN_PRICE bis MAX_PRICE.</summary>
    public void Restore(MarketPricesState state)
    {
        if (state == null)
            return;
        _food = Clamp(state.Food);
        _wood = Clamp(state.Wood);
        _stone = Clamp(state.Stone);
    }

    private void Shift(Resource good, float delta)
    {
        switch (good)
        {
            case Resource.Food: _food = Clamp(_food + delta); break;
            case Resource.Wood: _wood = Clamp(_wood + delta); break;
            case Resource.Stone: _stone = Clamp(_stone + delta); break;
        }
    }

    private static float Toward(float price, float step)
        => price > START_PRICE ? Math.Max(START_PRICE, price - step)
         : price < START_PRICE ? Math.Min(START_PRICE, price + step)
         : price;

    private static float Clamp(float price) => Math.Clamp(price, MIN_PRICE, MAX_PRICE);
}

/// <summary>Gespeicherte Kurse des Markts (<see cref="MarketPrices"/>, Spielstand).</summary>
public sealed record MarketPricesState(float Food, float Wood, float Stone);
