using RealisticOrbitalTrade.Patch;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_TradeUI_DrawTradeableRow.FindMaxSellCount: the
// sell-side mirror of FindMaxBuyCount - see FindMaxBuyCountTests for the shared rationale.
[TestSuite]
internal static class FindMaxSellCountTests
{
    [Test]
    public static void NoCurrencyBoundsPurelyByThisRowsOwnMass()
    {
        // 1kg/unit, 10kg of other outgoing cargo already committed, 20kg capacity -> 10 more units fit.
        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxSellCount(
            new FakeTradeable(),
            null,
            unitMass: 1f,
            unitMassCurrency: 0f,
            otherCost: 0f,
            otherOutgoingMass: 10f,
            massCapacity: 20f,
            trueMax: 100
        );

        Assert.That(result).Is.EqualTo(10);
    }

    // otherCost=100 means the deal (before this row) already has the colony owing the trader 100
    // silver; selling s units at 2 silver/unit chips away at that debt until the balance flips.
    // While the debt remains, that silver has to leave via this same outgoing shuttle. This row's
    // own mass (1kg/unit) is heavier per value than the silver it pays off (0.02kg/unit), so total
    // outgoing mass stays non-decreasing in s: mass(s) = s + 0.02*(100 - 2s) = 0.96s + 2 while
    // s < 50, capped at 45kg -> s=44 (44.24kg), since s=45 would be 45.2kg.
    [Test]
    public static void SilverOwedByColonyAddsToOutgoingMass()
    {
        var row = new FakeTradeable { SellPrice = 2f };
        var currency = new FakeTradeable();

        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxSellCount(
            row,
            currency,
            unitMass: 1f,
            unitMassCurrency: 0.02f,
            otherCost: 100f,
            otherOutgoingMass: 0f,
            massCapacity: 45f,
            trueMax: 100
        );

        Assert.That(result).Is.EqualTo(44);

        // Ignoring the rebalancing entirely (no currency tradeable) would have allowed one more
        // unit, since the 44th unit alone fits in 45kg without the extra 0.24kg the owed silver adds.
        var withoutCurrency = Rimworld_TradeUI_DrawTradeableRow.FindMaxSellCount(
            row,
            null,
            unitMass: 1f,
            unitMassCurrency: 0.02f,
            otherCost: 100f,
            otherOutgoingMass: 0f,
            massCapacity: 45f,
            trueMax: 100
        );
        Assert.That(withoutCurrency).Is.EqualTo(45);
    }

    // When the deal is already net-selling (the trader owes the colony silver, not the other way
    // around), that owed silver arrives via the *incoming* shuttle, not this one - so no matter how
    // heavy unitMassCurrency is, it must never be added to this row's outgoing mass.
    [Test]
    public static void SilverOwedToColonyIsNotAddedToOutgoingMass()
    {
        var row = new FakeTradeable { SellPrice = 1f };
        var currency = new FakeTradeable();

        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxSellCount(
            row,
            currency,
            unitMass: 1f,
            unitMassCurrency: 5f,
            otherCost: -10f,
            otherOutgoingMass: 0f,
            massCapacity: 20f,
            trueMax: 100
        );

        Assert.That(result).Is.EqualTo(20);
    }
}
