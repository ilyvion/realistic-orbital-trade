using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_TradeUI_DrawTradeableRow.FindMaxBuyCount: the
// buy-side half of GetMassBounds' silver-rebalancing mass search, previously untested despite being
// exactly the kind of branchy, edge-case-prone pure logic the project otherwise covers well.
[TestFixture(TestType.MainMenu)]
internal sealed class FindMaxBuyCountTests
{
    [Test]
    public static void NoCurrencyBoundsPurelyByThisRowsOwnMass()
    {
        // 1kg/unit, 10kg of other incoming cargo already committed, 20kg capacity -> 10 more units fit.
        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxBuyCount(
            new FakeTradeable(),
            null,
            unitMass: 1f,
            unitMassCurrency: 0f,
            otherCost: 0f,
            otherIncomingMass: 10f,
            massCapacity: 20f,
            trueMax: 100
        );

        Expect.AreEqual(10, result);
    }

    // otherCost=-100 means the deal (before this row) already owes the colony 100 silver back;
    // buying c units at 2 silver/unit chips away at that until the balance flips and the colony
    // stops being owed anything. This row's own mass (1kg/unit) is heavier per value than the
    // silver it earns back (0.02kg/unit), so total incoming mass stays non-decreasing in c:
    // mass(c) = c + 0.02*(100 - 2c) = 0.96c + 2 while c < 50, capped at 45kg -> c=44 (44.24kg),
    // since c=45 would be 45.2kg.
    [Test]
    public static void SilverOwedBackToColonyAddsToIncomingMass()
    {
        var row = new FakeTradeable { BuyPrice = 2f };
        var currency = new FakeTradeable();

        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxBuyCount(
            row,
            currency,
            unitMass: 1f,
            unitMassCurrency: 0.02f,
            otherCost: -100f,
            otherIncomingMass: 0f,
            massCapacity: 45f,
            trueMax: 100
        );

        Expect.AreEqual(44, result);

        // Ignoring the rebalancing entirely (no currency tradeable) would have allowed one more
        // unit, since the 44th unit alone fits in 45kg without the extra 0.24kg the owed silver adds.
        var withoutCurrency = Rimworld_TradeUI_DrawTradeableRow.FindMaxBuyCount(
            row,
            null,
            unitMass: 1f,
            unitMassCurrency: 0.02f,
            otherCost: -100f,
            otherIncomingMass: 0f,
            massCapacity: 45f,
            trueMax: 100
        );
        Expect.AreEqual(45, withoutCurrency);
    }

    // When the deal is already net-buying (the colony owes the trader silver, not the other way
    // around), that owed silver leaves via the *outgoing* shuttle, not this one - so no matter how
    // heavy unitMassCurrency is, it must never be added to this row's incoming mass.
    [Test]
    public static void SilverOwedByColonyIsNotAddedToIncomingMass()
    {
        var row = new FakeTradeable { BuyPrice = 1f };
        var currency = new FakeTradeable();

        var result = Rimworld_TradeUI_DrawTradeableRow.FindMaxBuyCount(
            row,
            currency,
            unitMass: 1f,
            unitMassCurrency: 5f,
            otherCost: 10f,
            otherIncomingMass: 0f,
            massCapacity: 20f,
            trueMax: 100
        );

        Expect.AreEqual(20, result);
    }
}
