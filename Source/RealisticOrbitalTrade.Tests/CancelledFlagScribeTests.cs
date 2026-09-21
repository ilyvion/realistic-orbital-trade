using DevTools.Testing;
using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.ShipJobs;

namespace RealisticOrbitalTrade.Tests;

// Regression suite: `CompTradeShuttle.cancelled` and `ShipJob_CancelLoad.done` were
// never scribed, so saving/loading mid-cancellation reset both flags to false, causing CompTick
// to re-initiate loading and reviving the renegotiation UI on a dead trade. These tests drive a
// real Scribe save/load cycle (via ilyvion.Laboratory's CustomStream Scribe helpers, so no
// on-disk save file is needed) and assert the flags survive the round trip.
[TestFixture(TestType.MainMenu)]
internal sealed class CancelledFlagScribeTests
{
    private sealed class CompTradeShuttleHarness : IExposable
    {
        public readonly CompTradeShuttle Comp = new();

        public void ExposeData() => Comp.PostExposeData();
    }

    [Test]
    public static void CompTradeShuttleCancelledSurvivesSaveLoadWhenTrue()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.cancelled = true;
        var loadHarness = new CompTradeShuttleHarness();

        ScribeRoundTrip.Run(saveHarness, loadHarness);

        Expect.IsTrue(loadHarness.Comp.cancelled);
    }

    [Test]
    public static void CompTradeShuttleCancelledSurvivesSaveLoadWhenFalse()
    {
        var saveHarness = new CompTradeShuttleHarness();
        saveHarness.Comp.cancelled = false;
        var loadHarness = new CompTradeShuttleHarness();

        ScribeRoundTrip.Run(saveHarness, loadHarness);

        Expect.IsFalse(loadHarness.Comp.cancelled);
    }

    [Test]
    public static void ShipJobCancelLoadDoneSurvivesSaveLoadWhenTrue()
    {
        var saveJob = new ShipJob_CancelLoad { done = true };
        var loadJob = new ShipJob_CancelLoad();

        ScribeRoundTrip.Run(saveJob, loadJob);

        Expect.IsTrue(loadJob.done);
    }
}
