using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: the standalone hand-carry branch of Notify_Starting (pawn, or no batchable
// transferable) used to queue its pickup into targetQueueA/countQueue unconditionally, only
// consulting ReserveForHaul's result afterwards to gate reservation-bookkeeping reconciliation -
// never whether to queue at all. That let a candidate get queued even when reservation failed,
// opening a race window where a live animal/prisoner could be double-claimed. This must instead
// mirror CommitToInventory/QueueHandCarryGroup, which both drop their candidate outright on a
// failed reservation.
[TestFixture(TestType.MainMenu)]
internal sealed class TryQueueStandaloneHandCarryTests
{
    private static int _nextThingId;

    private static Thing NewThing(int stackCount = 100) =>
        new()
        {
            stackCount = stackCount,
            def = RimWorld.ThingDefOf.Steel,
            thingIDNumber = _nextThingId++,
        };

    [Test]
    public static void FailedReservationQueuesNothing()
    {
        var thing = NewThing();
        List<LocalTargetInfo> targetQueue = [];
        List<int> countQueue = [];

        var queued = JobDriver_HaulMultipleToTransporter.TryQueueStandaloneHandCarry(
            thing,
            5,
            (_, _) => false,
            targetQueue,
            countQueue
        );

        Expect.AreEqual(false, queued);
        Expect.IsEmpty(targetQueue);
        Expect.IsEmpty(countQueue);
    }

    [Test]
    public static void SuccessfulReservationQueuesTheThingAndCount()
    {
        var thing = NewThing();
        List<LocalTargetInfo> targetQueue = [];
        List<int> countQueue = [];

        var queued = JobDriver_HaulMultipleToTransporter.TryQueueStandaloneHandCarry(
            thing,
            5,
            (_, _) => true,
            targetQueue,
            countQueue
        );

        Expect.AreEqual(true, queued);
        Expect.AreEqual(1, targetQueue.Count);
        Expect.AreEqual(thing.thingIDNumber, targetQueue[0].Thing?.thingIDNumber);
        Expect.AreEqual(1, countQueue.Count);
        Expect.AreEqual(5, countQueue[0]);
    }

    [Test]
    public static void ReservationIsAskedForTheExactThingAndCount()
    {
        var thing = NewThing();
        Thing? reservedThing = null;
        var reservedCount = -1;

        _ = JobDriver_HaulMultipleToTransporter.TryQueueStandaloneHandCarry(
            thing,
            7,
            (t, c) =>
            {
                reservedThing = t;
                reservedCount = c;
                return true;
            },
            [],
            []
        );

        Expect.AreEqual(thing.thingIDNumber, reservedThing?.thingIDNumber);
        Expect.AreEqual(7, reservedCount);
    }
}
