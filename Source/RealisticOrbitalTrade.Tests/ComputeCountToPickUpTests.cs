using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: countToPickUp used to be clamped only against job.count and the pawn's mass
// capacity, never against the target thing's actual stackCount. When a queued pickup amount
// exceeded what was really left in the stack (e.g. several pawns converging on the same shrinking
// stack), Thing.SplitOff(count > stackCount) doesn't clamp either - it logs an error and
// despawns/detaches the *original* thing instead of returning a valid partial split, which left
// the haul job's toil chain unable to make progress and jumping between toils forever until the
// native call stack overflowed.
[TestFixture(TestType.MainMenu)]
internal sealed class ComputeCountToPickUpTests
{
    [Test]
    public static void StackCountBelowJobAndMassCapClampsToStackCount()
    {
        var countToPickUp = JobDriver_HaulMultipleToTransporter.ComputeCountToPickUp(
            jobCount: 24,
            massCappedCount: 20,
            stackCount: 4
        );

        Expect.AreEqual(4, countToPickUp);
    }

    [Test]
    public static void MassCapBelowJobAndStackCountClampsToMassCap()
    {
        var countToPickUp = JobDriver_HaulMultipleToTransporter.ComputeCountToPickUp(
            jobCount: 24,
            massCappedCount: 10,
            stackCount: 75
        );

        Expect.AreEqual(10, countToPickUp);
    }

    [Test]
    public static void JobCountBelowMassCapAndStackCountClampsToJobCount()
    {
        var countToPickUp = JobDriver_HaulMultipleToTransporter.ComputeCountToPickUp(
            jobCount: 5,
            massCappedCount: 20,
            stackCount: 75
        );

        Expect.AreEqual(5, countToPickUp);
    }

    [Test]
    public static void AllThreeEqualPassesThroughUnchanged()
    {
        var countToPickUp = JobDriver_HaulMultipleToTransporter.ComputeCountToPickUp(
            jobCount: 10,
            massCappedCount: 10,
            stackCount: 10
        );

        Expect.AreEqual(10, countToPickUp);
    }
}
