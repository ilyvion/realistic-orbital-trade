using DevTools.Testing;
using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Quests;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class BuyMoreTimeTests
{
    private sealed class CompTradeShuttleHarness : IExposable
    {
        public readonly CompTradeShuttle Comp = new();

        public void ExposeData() => Comp.PostExposeData();
    }

    [Test]
    public static void TimesBoughtMoreTimeSurvivesSaveLoad()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.timesBoughtMoreTime = 3;
        var loadHarness = new CompTradeShuttleHarness();

        ScribeRoundTrip.Run(saveHarness, loadHarness);

        Expect.AreEqual(3, loadHarness.Comp.timesBoughtMoreTime);
    }

    [Test]
    public static void NextBuyMoreTimeCostGrowsWithPurchaseCount()
    {
        var originalBaseCost = Settings._buyMoreTimeBaseSilverCost.Value;
        var originalGrowthFactor = Settings._buyMoreTimeCostGrowthFactor;
        try
        {
            Settings._buyMoreTimeBaseSilverCost.Value = 200;
            Settings._buyMoreTimeCostGrowthFactor = 2f;

            CompTradeShuttle comp = new() { timesBoughtMoreTime = 0 };
            Expect.AreEqual(200, comp.NextBuyMoreTimeCost);

            comp.timesBoughtMoreTime = 1;
            Expect.AreEqual(400, comp.NextBuyMoreTimeCost);

            comp.timesBoughtMoreTime = 2;
            Expect.AreEqual(800, comp.NextBuyMoreTimeCost);
        }
        finally
        {
            Settings._buyMoreTimeBaseSilverCost.Value = originalBaseCost;
            Settings._buyMoreTimeCostGrowthFactor = originalGrowthFactor;
        }
    }

    // Regression guard: buying more time used to only extend the shuttle's own leave-delay quest
    // timer, leaving the underlying TradeShip's ticksUntilDeparture on its original schedule. The
    // trade ship would then despawn out from under a shuttle still waiting on it, leaving a
    // dangling tradeShip reference that failed to deep-save ("PassingShip_0 is referenced ... but
    // is not deep-saved").
    [Test]
    public static void ExtendDepartureTimeExtendsBothShuttleDelayAndTradeShip()
    {
        var tradeShip = new TradeShip { ticksUntilDeparture = 500 };
        var delayTicks = 500;

        QuestPart_TradeShuttleLeaveDelay.ExtendDepartureTime(ref delayTicks, tradeShip, 1000);

        Expect.AreEqual(1500, delayTicks);
        Expect.AreEqual(1500, tradeShip.ticksUntilDeparture);
    }
}
