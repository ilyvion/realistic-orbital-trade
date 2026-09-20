using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: WorkGiver_HaulMultipleToTransporter.HasJobOnThing used to only check vanilla's
// LoadTransportersJobUtility.HasJobOnTransporter, which has no idea another pawn already committed
// enough hauling to cover what's left - CompTradeShuttle.CheckAutoload only reconciles
// CountToTransfer against that every ~120 ticks. That let the WorkGiver keep handing a pawn a job
// Notify_Starting was guaranteed to immediately reject, spamming a create-job/bail-out loop every
// tick. Caught live via Player.log: dozens of repeated "committed counts from other pawns ->
// wood=75" / "CountToTransfer=66 alreadyCommitted=75 -> primaryCount clamped to 0" / "bailing out"
// lines for the same pawn in a row.
[TestFixture(TestType.MainMenu)]
internal sealed class StillNeedsPickupTests
{
    private static int _nextThingId;

    private static TransferableOneWay NewTransferable(int countToTransfer)
    {
        TransferableOneWay transferable = new();
        transferable.things.Add(
            new Thing
            {
                def = RimWorld.ThingDefOf.WoodLog,
                stackCount = countToTransfer,
                thingIDNumber = _nextThingId++,
            }
        );
        transferable.ForceTo(countToTransfer);
        return transferable;
    }

    [Test]
    public static void StillNeedsPickupIsFalseWhenAnotherPawnAlreadyCommittedTheFullAmount()
    {
        var transferable = NewTransferable(66);
        Dictionary<TransferableOneWay, int> committed = new() { [transferable] = 75 };

        Expect.IsFalse(
            WorkGiver_HaulMultipleToTransporter.StillNeedsPickup(transferable, committed)
        );
    }

    [Test]
    public static void StillNeedsPickupIsTrueWhenCommitmentsDontCoverTheFullAmount()
    {
        var transferable = NewTransferable(66);
        Dictionary<TransferableOneWay, int> committed = new() { [transferable] = 40 };

        Expect.IsTrue(
            WorkGiver_HaulMultipleToTransporter.StillNeedsPickup(transferable, committed)
        );
    }

    [Test]
    public static void StillNeedsPickupIsTrueWithNoCommitments()
    {
        var transferable = NewTransferable(66);
        Dictionary<TransferableOneWay, int> committed = [];

        Expect.IsTrue(
            WorkGiver_HaulMultipleToTransporter.StillNeedsPickup(transferable, committed)
        );
    }
}
