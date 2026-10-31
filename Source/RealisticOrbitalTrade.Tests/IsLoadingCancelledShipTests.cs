using DevTools.Testing;
using RealisticOrbitalTrade.ShipJobs;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// ShipJob_CancelLoad ends every pawn running this mod's hauling job
// (JobDefOf.ROT_HaulMultipleToTransporter) toward the cancelled shuttle, so none keeps walking
// cargo to it.
[TestFixture(TestType.MainMenu)]
internal sealed class IsLoadingCancelledShipTests
{
    private static int _nextThingId;

    private static Thing NewShipThing() =>
        new() { def = RimWorld.ThingDefOf.Steel, thingIDNumber = _nextThingId++ };

    [Test]
    public static void TrueForBulkHaulJobTargetingTheShip()
    {
        var shipThing = NewShipThing();
        var job = new Job { def = JobDefOf.ROT_HaulMultipleToTransporter, targetB = shipThing };

        Expect.IsTrue(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing));
    }

    [Test]
    public static void FalseWhenPawnHasNoCurrentJob()
    {
        var shipThing = NewShipThing();

        Expect.IsFalse(ShipJob_CancelLoad.IsLoadingCancelledShip(null, shipThing));
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

        Expect.IsFalse(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing));
    }

    [Test]
    public static void FalseForAnUnrelatedJobDef()
    {
        var shipThing = NewShipThing();
        var job = new Job { def = RimWorld.JobDefOf.Wait, targetB = shipThing };

        Expect.IsFalse(ShipJob_CancelLoad.IsLoadingCancelledShip(job, shipThing));
    }
}
