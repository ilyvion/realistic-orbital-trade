using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: takeThing's actual pickup (mass-capped for real) used to come in under
// CommitToInventory's own up-front mass estimate without the difference being tracked anywhere -
// the shortfall stayed physically sitting in the source stack but the trip's bookkeeping already
// counted it as taken, so it was silently left behind on the map for an entirely separate future
// hauling trip to come back for.
[TestFixture(TestType.MainMenu)]
internal sealed class ComputeMassShortfallTests
{
    [Test]
    public static void PickupShortOfPlanIsAShortfall()
    {
        // Mirrors the reported bug: planned to take 3 into inventory, mass only allowed 2.
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 3,
            stackCountBeforeSplit: 30,
            countPickedUp: 2
        );

        Expect.AreEqual(1, shortfall);
    }

    [Test]
    public static void PickupMatchingPlanHasNoShortfall()
    {
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 10,
            stackCountBeforeSplit: 75,
            countPickedUp: 10
        );

        Expect.AreEqual(0, shortfall);
    }

    // The stack itself only had 4 units to give in the first place (physical stock, not mass, was
    // the limit) - there's nothing left behind in it to carry forward.
    [Test]
    public static void StackSmallerThanJobCountHasNoShortfall()
    {
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 10,
            stackCountBeforeSplit: 4,
            countPickedUp: 4
        );

        Expect.AreEqual(0, shortfall);
    }

    [Test]
    public static void ShortfallIsBoundedByJobCountNotStackSize()
    {
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 5,
            stackCountBeforeSplit: 75,
            countPickedUp: 3
        );

        Expect.AreEqual(2, shortfall);
    }
}
