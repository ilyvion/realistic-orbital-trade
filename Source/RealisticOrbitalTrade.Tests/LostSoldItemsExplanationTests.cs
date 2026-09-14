using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for Utils.ExplainLostSoldItemsIfNeeded's pure decision logic - which
// translation key to show (BuildLostSoldItemsBulletList/LostSoldItemsExplanationKey), split out
// from the effectful shell (TradeDeal.UpdateCurrencyCount, Find.WindowStack.Add) that can't run
// outside a real game.
[TestSuite]
internal static class LostSoldItemsExplanationTests
{
    private static int _nextThingId;

    private static Tradeable NewCurrencyTradeable(int countToTransfer)
    {
        Thing thing = new()
        {
            def = RimWorld.ThingDefOf.Silver,
            stackCount = Math.Max(1, Math.Abs(countToTransfer)),
            thingIDNumber = _nextThingId++,
        };
        Tradeable tradeable = new();
        tradeable.AddThing(thing, countToTransfer <= 0 ? Transactor.Colony : Transactor.Trader);
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    [Test]
    public static void BulletListFormatsEachRecordsLabelAsADashedLine()
    {
        List<TradeAgreement.LostTradeItemRecord> records =
        [
            new() { pawnLabel = "Rex" },
            new() { pawnLabel = "Muffalo" },
        ];

        var result = Utils.BuildLostSoldItemsBulletList(records);

        Assert.That(result).Is.EqualTo("- Rex\n- Muffalo");
    }

    [Test]
    public static void ExplanationKeyIsNoCurrencyChangeWhenNoCurrencyTradeableWasFound()
    {
        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToTrader, null, 0)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsNoCurrencyChangeText");
    }

    [Test]
    public static void ExplanationKeyIsNoCurrencyChangeWhenTheAmountIsZero()
    {
        var currency = NewCurrencyTradeable(0);

        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToTrader, currency, 0)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsNoCurrencyChangeText");
    }

    [Test]
    public static void ExplanationKeyIsIncreasedOwedWhenMoreSilverNowFlowsToTheTrader()
    {
        var currency = NewCurrencyTradeable(-69);

        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToTrader, currency, 69)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsIncreasedOwedText");
    }

    [Test]
    public static void ExplanationKeyIsDecreasedOwedWhenLessSilverNowFlowsToTheTrader()
    {
        var currency = NewCurrencyTradeable(69);

        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToPlayer, currency, 69)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsDecreasedOwedText");
    }

    // Regression case: the trader was already sending the colony silver (ToPlayer), and losing a
    // sold item shrinks that amount rather than growing it - a negative ToPlayer amount still
    // means the colony ends up owing more, not less, even though ToTrader/a positive amount is
    // the more common "owes more" shape.
    [Test]
    public static void ExplanationKeyIsIncreasedOwedWhenLessSilverNowFlowsFromTheTrader()
    {
        var currency = NewCurrencyTradeable(69);

        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToPlayer, currency, -16)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsIncreasedOwedText");
    }

    [Test]
    public static void ExplanationKeyIsDecreasedOwedWhenLessSilverNowFlowsToTheTraderNegativeAmount()
    {
        var currency = NewCurrencyTradeable(-69);

        var key = Utils.LostSoldItemsExplanationKey(
            (TradeAgreement.TransferDirection.ToTrader, currency, -16)
        );

        Assert
            .That(key)
            .Is.EqualTo("RealisticOrbitalTrade.RenegotiationLostItemsDecreasedOwedText");
    }
}
