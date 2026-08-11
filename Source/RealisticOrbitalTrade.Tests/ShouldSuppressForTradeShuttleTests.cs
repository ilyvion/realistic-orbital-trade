using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Patch;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: nothing kept vanilla's WorkGiver_LoadTransporters from also being offered a
// trade shuttle transporter alongside WorkGiver_HaulMultipleToTransporter. That's normally
// harmless, but a third-party mod patching LoadTransportersJobUtility with its own competing
// bulk-loading system (e.g. Bulk Load For Transporters) is invisible to this mod's own
// reservation/commitment tracking - its planner can get permanently blocked and keep reissuing a
// short expiring Wait job forever instead of giving up, which looks like colonists stuck standing
// still doing nothing (see TODO.md's "Occasional trade shuttle loading bug"). Suppressing
// vanilla's WorkGiver for trade shuttles whenever this mod's own multi-item loading is enabled
// avoids that class of conflict entirely.
[TestSuite]
internal static class ShouldSuppressForTradeShuttleTests
{
    [Test]
    public static void SuppressesWhenEnabledAndThingIsATradeShuttle()
    {
        var result = Rimworld_WorkGiver_LoadTransporters_Shared.ShouldSuppressForTradeShuttle(
            multiItemShuttleLoading: true,
            tradeShuttleComp: new CompTradeShuttle()
        );

        Assert.That(result).Is.True();
    }

    [Test]
    public static void DoesNotSuppressWhenDisabledEvenForATradeShuttle()
    {
        var result = Rimworld_WorkGiver_LoadTransporters_Shared.ShouldSuppressForTradeShuttle(
            multiItemShuttleLoading: false,
            tradeShuttleComp: new CompTradeShuttle()
        );

        Assert.That(result).Is.False();
    }

    [Test]
    public static void DoesNotSuppressWhenEnabledButThingIsNotATradeShuttle()
    {
        var result = Rimworld_WorkGiver_LoadTransporters_Shared.ShouldSuppressForTradeShuttle(
            multiItemShuttleLoading: true,
            tradeShuttleComp: null
        );

        Assert.That(result).Is.False();
    }
}
