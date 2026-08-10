using RealisticOrbitalTrade.ShipJobs;
using RimTestRedux;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: ShipJob_CancelLoad used to only end pawns running vanilla's own
// JobDefOf.HaulToTransporter, so a pawn mid-haul via this mod's own bulk-haul job
// (JobDefOf.ROT_HaulMultipleToTransporter - the default loading path per
// Settings._multiItemShuttleLoading) kept walking cargo to a shuttle whose trade had already been
// cancelled.
[TestSuite]
internal static class IsLoadingCancelledShipTests
{
    private static int _nextThingId;

    private static Thing NewShipThing() =>
        new() { def = RimWorld.ThingDefOf.Steel, thingIDNumber = _nextThingId++ };

    [Test]
    public static void TrueForVanillaHaulJobTargetingTheShip()
    {
        var shipThing = NewShipThing();
        var job = new Job { def = RimWorld.JobDefOf.HaulToTransporter, targetB = shipThing };

        Assert.That(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing)).Is.True();
    }

    [Test]
    public static void TrueForBulkHaulJobTargetingTheShip()
    {
        var shipThing = NewShipThing();
        var job = new Job { def = JobDefOf.ROT_HaulMultipleToTransporter, targetB = shipThing };

        Assert.That(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing)).Is.True();
    }

    [Test]
    public static void FalseWhenPawnHasNoCurrentJob()
    {
        var shipThing = NewShipThing();

        Assert.That(ShipJob_CancelLoad.IsLoadingCancelledShip(null, shipThing)).Is.False();
    }

    [Test]
    public static void FalseWhenHaulJobTargetsADifferentShip()
    {
        var shipThing = NewShipThing();
        var otherShipThing = NewShipThing();
        var job = new Job
        {
            def = JobDefOf.ROT_HaulMultipleToTransporter,
            targetB = otherShipThing,
        };

        Assert.That(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing)).Is.False();
    }

    [Test]
    public static void FalseForAnUnrelatedJobDef()
    {
        var shipThing = NewShipThing();
        var job = new Job { def = RimWorld.JobDefOf.Wait, targetB = shipThing };

        Assert.That(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing)).Is.False();
    }
}
