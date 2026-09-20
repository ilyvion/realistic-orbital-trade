using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class CalculateCountToTakeTests
{
    [Test]
    public static void LimitedByStackCount()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 10,
            stillNeededCount: 1000,
            remainingMassCapacity: 1000f,
            unitMass: 0.1f
        );

        Expect.AreEqual(10, result);
    }

    [Test]
    public static void LimitedByStillNeededCount()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 75,
            stillNeededCount: 5,
            remainingMassCapacity: 1000f,
            unitMass: 0.1f
        );

        Expect.AreEqual(5, result);
    }

    [Test]
    public static void LimitedByMassCapacity()
    {
        // 20 mass / 2 mass-per-unit = 10 units fit, even though the stack and requirement allow more.
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 75,
            stillNeededCount: 75,
            remainingMassCapacity: 20f,
            unitMass: 2f
        );

        Expect.AreEqual(10, result);
    }

    [Test]
    public static void ZeroMassCapacityLeftTakesNothing()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 75,
            stillNeededCount: 75,
            remainingMassCapacity: 0f,
            unitMass: 2f
        );

        Expect.AreEqual(0, result);
    }

    // Regression guard: a zero-mass item (unitMass == 0) must not divide-by-zero or otherwise
    // block pickup - it should be bounded only by stack/needed count.
    [Test]
    public static void MasslessItemIsNotLimitedByMassCapacity()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 40,
            stillNeededCount: 30,
            remainingMassCapacity: 0f,
            unitMass: 0f
        );

        Expect.AreEqual(30, result);
    }

    // A single unit that only just barely fits (mass caps it down from a larger want) isn't worth
    // taking - the live in-game mass check at actual pickup time can disagree with this up-front
    // estimate right at the edge, wasting the trip on nothing.
    [Test]
    public static void MassEdgeCaseTakingOnlyOneOfSeveralWantedIsSkipped()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 75,
            stillNeededCount: 75,
            remainingMassCapacity: 0.3f,
            unitMass: 0.3f
        );

        Expect.AreEqual(0, result);
    }

    // If only a single unit was ever wanted (not because mass forced it down from more), mass
    // being tight isn't a reason to skip it - there's nothing being left behind either way.
    [Test]
    public static void MassEdgeCaseTakingTheOnlyOneWantedIsNotSkipped()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 1,
            stillNeededCount: 1,
            remainingMassCapacity: 0.3f,
            unitMass: 0.3f
        );

        Expect.AreEqual(1, result);
    }

    // Room for two or more of the item means it's worth grabbing what fits, edge case or not.
    [Test]
    public static void MassEdgeCaseWithRoomForTwoOrMoreTakesWhatFits()
    {
        var result = JobDriver_HaulMultipleToTransporter.CalculateCountToTake(
            stackCount: 75,
            stillNeededCount: 75,
            remainingMassCapacity: 0.6f,
            unitMass: 0.3f
        );

        Expect.AreEqual(2, result);
    }
}
