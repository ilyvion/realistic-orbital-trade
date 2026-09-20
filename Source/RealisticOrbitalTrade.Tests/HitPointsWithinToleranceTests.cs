using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class HitPointsWithinToleranceTests
{
    [Test]
    public static void ExactMatchIsAlwaysWithinTolerance() =>
        Expect.IsTrue(ThingDefCountWithRequirements.HitPointsWithinTolerance(50, 50, 100, 0));

    // Regression guard: prior to this, item matching required exact hit points, so even
    // deterioration far too small to matter (1 hit point out of a 500-max item) could get a
    // trade stuck.
    [Test]
    public static void SmallDriftOnLargeItemIsWithinFlatFloorWhenPercentIsZero() =>
        Expect.IsTrue(ThingDefCountWithRequirements.HitPointsWithinTolerance(499, 500, 500, 0));

    [Test]
    public static void DriftBeyondFlatFloorFailsWhenPercentIsZero() =>
        Expect.IsFalse(ThingDefCountWithRequirements.HitPointsWithinTolerance(494, 500, 500, 0));

    [Test]
    public static void PercentToleranceScalesWithMaxHitPoints()
    {
        // 2% of 500 is 10, larger than the 5hp floor.
        Expect.IsTrue(ThingDefCountWithRequirements.HitPointsWithinTolerance(490, 500, 500, 2));
        Expect.IsFalse(ThingDefCountWithRequirements.HitPointsWithinTolerance(489, 500, 500, 2));
    }

    [Test]
    public static void FlatFloorAppliesWhenPercentToleranceIsSmaller()
    {
        // 2% of 50 is 1, smaller than the 5hp floor, so the floor should win.
        Expect.IsTrue(ThingDefCountWithRequirements.HitPointsWithinTolerance(45, 50, 50, 2));
        Expect.IsFalse(ThingDefCountWithRequirements.HitPointsWithinTolerance(44, 50, 50, 2));
    }
}
