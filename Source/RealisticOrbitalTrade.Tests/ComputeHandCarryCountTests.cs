using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class ComputeHandCarryCountTests
{
    [Test]
    public static void NoShortfallCarriesJobCountAsPlanned()
    {
        var countToCarry = JobDriver_HaulMultipleToTransporter.ComputeHandCarryCount(
            jobCount: 27,
            carriedOverShortfall: 0,
            stackCount: 28
        );

        Assert.That(countToCarry).Is.EqualTo(27);
    }

    // Mirrors the reported bug: an earlier inventory pickup from this same stack came in 1 short
    // of its plan, leaving a 28-unit stack behind a 27-unit hand-carry plan - the topper absorbs
    // the shortfall so nothing is left on the map.
    [Test]
    public static void ShortfallIsAddedOnTopOfJobCount()
    {
        var countToCarry = JobDriver_HaulMultipleToTransporter.ComputeHandCarryCount(
            jobCount: 27,
            carriedOverShortfall: 1,
            stackCount: 28
        );

        Assert.That(countToCarry).Is.EqualTo(28);
    }

    // Carrying the shortfall forward must never exceed what's physically in the stack, same as
    // every other pickup in this job.
    [Test]
    public static void ResultIsStillClampedToStackCount()
    {
        var countToCarry = JobDriver_HaulMultipleToTransporter.ComputeHandCarryCount(
            jobCount: 17,
            carriedOverShortfall: 5,
            stackCount: 20
        );

        Assert.That(countToCarry).Is.EqualTo(20);
    }

    // A demand-limited topper (only part of the stack was ever needed) still only tops up by the
    // actual shortfall, not by however much extra stock happens to be sitting in the stack -
    // otherwise this would over-haul beyond what the trade agreement actually calls for.
    [Test]
    public static void DemandLimitedTopperOnlyGainsTheActualShortfall()
    {
        var countToCarry = JobDriver_HaulMultipleToTransporter.ComputeHandCarryCount(
            jobCount: 17,
            carriedOverShortfall: 1,
            stackCount: 28
        );

        Assert.That(countToCarry).Is.EqualTo(18);
    }
}
