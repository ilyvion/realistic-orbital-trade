using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class HitPointsWithinToleranceTests
{
    [Test]
    public static void ExactMatchIsAlwaysWithinTolerance() =>
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(50, 50, 100, 0))
            .Is.True();

    // Regression guard: prior to this, item matching required exact hit points, so even
    // deterioration far too small to matter (1 hit point out of a 500-max item) could get a
    // trade stuck.
    [Test]
    public static void SmallDriftOnLargeItemIsWithinFlatFloorWhenPercentIsZero() =>
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(499, 500, 500, 0))
            .Is.True();

    [Test]
    public static void DriftBeyondFlatFloorFailsWhenPercentIsZero() =>
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(494, 500, 500, 0))
            .Is.False();

    [Test]
    public static void PercentToleranceScalesWithMaxHitPoints()
    {
        // 2% of 500 is 10, larger than the 5hp floor.
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(490, 500, 500, 2))
            .Is.True();
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(489, 500, 500, 2))
            .Is.False();
    }

    [Test]
    public static void FlatFloorAppliesWhenPercentToleranceIsSmaller()
    {
        // 2% of 50 is 1, smaller than the 5hp floor, so the floor should win.
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(45, 50, 50, 2))
            .Is.True();
        Assert
            .That(ThingDefCountWithRequirements.HitPointsWithinTolerance(44, 50, 50, 2))
            .Is.False();
    }
}
