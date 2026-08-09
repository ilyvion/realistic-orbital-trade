using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: ComputeCommittedCounts (JobDriver_HaulMultipleToTransporter) used to look up a
// thing's transferable via `transferable.things.Contains(thing)`, but Thing.SplitOff always hands
// back a brand-new Thing (with its own thingIDNumber) whenever it splits off part of a stack, so a
// thing already picked up for a haul trip is no longer the same Thing the transferable's own list
// knows about. That silently dropped every already-in-flight pickup out of the "committed" tally,
// so another pawn computing its own remainingNeeded saw stock that was really already spoken for
// as still up for grabs and over-hauled it - caught live via Player.log showing committed counts
// go blank for a pawn that was still mid-haul.
[TestSuite]
internal static class ComputeCommittedCountsTests
{
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static TransferableOneWay NewTransferable(Thing thing, int countToTransfer)
    {
        TransferableOneWay transferable = new();
        transferable.things.Add(thing);
        transferable.ForceTo(countToTransfer);
        return transferable;
    }

    [Test]
    public static void FindCommittedTransferableMatchesAnAlreadyPickedUpSplitPortion()
    {
        // Thing.SplitOff itself needs a loaded game (it mints the split portion's ID via
        // Find.UniqueIDsManager) - mirror what it produces instead of calling it directly: the
        // original stack shrunk by the split amount, and a separate Thing (distinct
        // thingIDNumber, same as a real SplitOff's freshly minted instance) standing in for the
        // picked-up portion, absent from transferable.things.
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 37);
        var transferable = NewTransferable(stack, 430);
        List<TransferableOneWay> leftToLoad = [transferable];

        var pickedUp = NewThing(RimWorld.ThingDefOf.Steel, 38);

        var found = JobDriver_HaulMultipleToTransporter.FindCommittedTransferable(
            pickedUp,
            leftToLoad
        );

        Assert.That(ReferenceEquals(found, transferable)).Is.True();
    }

    [Test]
    public static void FindCommittedTransferableStillMatchesAnUnsplitThing()
    {
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var transferable = NewTransferable(stack, 430);
        List<TransferableOneWay> leftToLoad = [transferable];

        var found = JobDriver_HaulMultipleToTransporter.FindCommittedTransferable(
            stack,
            leftToLoad
        );

        Assert.That(ReferenceEquals(found, transferable)).Is.True();
    }

    [Test]
    public static void FindCommittedTransferableReturnsNullForAnUnrelatedThing()
    {
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        List<TransferableOneWay> leftToLoad = [NewTransferable(stack, 430)];

        var unrelated = NewThing(RimWorld.ThingDefOf.WoodLog, 50);

        var found = JobDriver_HaulMultipleToTransporter.FindCommittedTransferable(
            unrelated,
            leftToLoad
        );

        Assert.That(found == null).Is.True();
    }

    [Test]
    public static void CommittedCountForTargetUsesJobCountBeforePickup()
    {
        var targetThing = NewThing(RimWorld.ThingDefOf.Steel, 75);

        var result = JobDriver_HaulMultipleToTransporter.CommittedCountForTarget(
            targetThing,
            carriedThing: null,
            jobCount: 38
        );

        Assert.That(result).Is.EqualTo(38);
    }

    // Regression guard: once a hand-carry group's second (or later) pickup has merged into the
    // pawn's already-carried stack (QueueHandCarryGroup), job.count only ever reflects the most
    // recently queued entry's own amount, not the merged total actually in hand - understating
    // what's really committed unless the carried thing's own stackCount (which does reflect every
    // merge) is used instead.
    [Test]
    public static void CommittedCountForTargetUsesCarriedStackCountAfterHandCarryMerge()
    {
        var carriedStack = NewThing(RimWorld.ThingDefOf.Steel, 75);

        var result = JobDriver_HaulMultipleToTransporter.CommittedCountForTarget(
            carriedStack,
            carriedThing: carriedStack,
            jobCount: 38
        );

        Assert.That(result).Is.EqualTo(75);
    }
}
