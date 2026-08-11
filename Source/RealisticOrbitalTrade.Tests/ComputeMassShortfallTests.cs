using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: takeThing's actual pickup (mass-capped for real) used to come in under
// CommitToInventory's own up-front mass estimate without the difference being tracked anywhere -
// the shortfall stayed physically sitting in the source stack but the trip's bookkeeping already
// counted it as taken, so it was silently left behind on the map for an entirely separate future
// hauling trip to come back for.
[TestSuite]
internal static class ComputeMassShortfallTests
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

        Assert.That(shortfall).Is.EqualTo(1);
    }

    [Test]
    public static void PickupMatchingPlanHasNoShortfall()
    {
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 10,
            stackCountBeforeSplit: 75,
            countPickedUp: 10
        );

        Assert.That(shortfall).Is.EqualTo(0);
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

        Assert.That(shortfall).Is.EqualTo(0);
    }

    [Test]
    public static void ShortfallIsBoundedByJobCountNotStackSize()
    {
        var shortfall = JobDriver_HaulMultipleToTransporter.ComputeMassShortfall(
            jobCount: 5,
            stackCountBeforeSplit: 75,
            countPickedUp: 3
        );

        Assert.That(shortfall).Is.EqualTo(2);
    }
}
