using DevTools.Testing;
using RealisticOrbitalTrade.Tests;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Tests;

// Regression suite: SocialExpGrantTracker.ExposeData() read its four persisted lists via
// Scribe_Collections.Look and then, in its PostLoadInit pass, indexed into them without checking
// for null. Verse's Scribe_Collections.Look sets a list to null (rather than leaving its
// pre-existing empty-list default) when the save being loaded has no matching XML node for it -
// which is exactly what happens the first time a save made before this integration persisted
// anything is loaded with the integration active (e.g. the integration mod, or Social Experience
// from Trade itself, gets added to an existing save that already has an in-flight ROT trade).
// That crashed every such load with a NullReferenceException. These tests drive a real Scribe
// save/load cycle (via ilyvion.Laboratory's CustomStream Scribe helpers, so no on-disk save file
// is needed) to guard against that regressing.
[TestFixture(TestType.MainMenu)]
internal sealed class SocialExpGrantTrackerScribeTests
{
    private sealed class EmptyHarness : IExposable
    {
        public void ExposeData() { }
    }

    private sealed class SocialExpGrantTrackerHarness : IExposable
    {
        public void ExposeData() => SocialExpGrantTracker.ExposeData();
    }

    [Test]
    public static void LoadingASaveWithNoPersistedPendingDataDoesNotThrow()
    {
        SocialExpGrantTracker.Reset();

        ScribeRoundTrip.Run(new EmptyHarness(), new SocialExpGrantTrackerHarness());

        SocialExpGrantTracker.Reset();
    }
}
