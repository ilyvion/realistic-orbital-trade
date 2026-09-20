using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for TradeAgreement.PreferUnloadedThingCount, extracted from the "take things off
// the soldToTrader list" step of ApplyRenegotiation's reduce-a-sold-amount branch. A sold def can
// span several ThingCountClass stacks - some already loaded into the shuttle, some still sitting on
// the map. Reducing the wrong one (an already-loaded stack instead of an unloaded one) leaves the
// untouched stack's reservation dangling forever, since nothing else would ever release it - this
// is what caused sold items to remain incorrectly marked "reserved for trade" after a departure
// renegotiation locked in less than what had originally been sold (issue #7 follow-up).
[TestFixture(TestType.MainMenu)]
internal sealed class PreferUnloadedThingCountTests
{
    // Every check below asserts a bool for whether the result is (or isn't) a particular
    // candidate.
    private static int _nextThingId;

    private static Thing NewThing(int stackCount = 10) =>
        new()
        {
            stackCount = stackCount,
            def = RimWorld.ThingDefOf.Steel,
            thingIDNumber = _nextThingId++,
        };

    private static ThingCountClass NewThingCount(Thing thing, int count) => new(thing, count);

    [Test]
    public static void PicksTheOnlyCandidateRegardlessOfLoadedState()
    {
        var thing = NewThing();
        var thingCount = NewThingCount(thing, 5);

        var result = TradeAgreement.PreferUnloadedThingCount([thingCount], _ => true);

        Expect.IsTrue(ReferenceEquals(result, thingCount));
    }

    [Test]
    public static void PrefersTheUnloadedStackOverTheLoadedOne()
    {
        var loadedThing = NewThing();
        var unloadedThing = NewThing();
        var loaded = NewThingCount(loadedThing, 5);
        var unloaded = NewThingCount(unloadedThing, 5);

        var result = TradeAgreement.PreferUnloadedThingCount(
            [loaded, unloaded],
            t => t == loadedThing
        );

        Expect.IsTrue(ReferenceEquals(result, unloaded));
    }

    [Test]
    public static void PrefersTheUnloadedStackRegardlessOfCandidateOrder()
    {
        var loadedThing = NewThing();
        var unloadedThing = NewThing();
        var loaded = NewThingCount(loadedThing, 5);
        var unloaded = NewThingCount(unloadedThing, 5);

        var result = TradeAgreement.PreferUnloadedThingCount(
            [unloaded, loaded],
            t => t == loadedThing
        );

        Expect.IsTrue(ReferenceEquals(result, unloaded));
    }

    [Test]
    public static void FallsBackToALoadedStackWhenNothingElseIsUnloaded()
    {
        var thing = NewThing();
        var loaded = NewThingCount(thing, 5);

        var result = TradeAgreement.PreferUnloadedThingCount([loaded], _ => true);

        Expect.IsTrue(ReferenceEquals(result, loaded));
    }

    [Test]
    public static void ReturnsNullForNoCandidates()
    {
        var result = TradeAgreement.PreferUnloadedThingCount([], _ => false);

        Expect.IsTrue(result == null);
    }
}
