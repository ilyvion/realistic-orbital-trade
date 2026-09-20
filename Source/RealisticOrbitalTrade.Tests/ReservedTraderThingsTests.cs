using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the reserved-trader-things bookkeeping backing the "traded items get eaten
// before they can be hauled" fix (see TradeAgreement.reservedTraderThings and
// Patch/Rimworld_ReservationManager.cs). AddReservation/ReduceReservation/RestoreReservation must
// only ever track/release the exact amount asked for, clamped to what's actually still tracked -
// getting this wrong either leaves a stale reservation blocking a stack forever (never hits 0) or
// under-protects a partially-hauled stack's remainder.
[TestFixture(TestType.MainMenu)]
internal sealed class ReservedTraderThingsTests
{
    // ThingCountClass.Count silently clamps to thing.stackCount (see Verse.ThingCountClass), so
    // every test thing needs a stack big enough for the counts it exercises - a bare new Pawn()
    // defaults to a stack of 1, which would otherwise silently clamp every count used here down to
    // 1 and make these tests pass or fail for the wrong reason. def/thingIDNumber are set because
    // Thing.Equals (used by List<Thing>.Contains, e.g. TransferableOneWay.things) dereferences def
    // and compares thingIDNumber - a bare new Pawn() leaves def null (NRE) and every instance's
    // thingIDNumber at the same default, which would make unrelated test things compare equal.
    private static int _nextThingId;

    private static Pawn NewThing(int stackCount = 100) =>
        new()
        {
            stackCount = stackCount,
            def = RimWorld.ThingDefOf.Steel,
            thingIDNumber = _nextThingId++,
        };

    [Test]
    public static void ReservedCountForIsZeroForAnUntrackedThing()
    {
        var tradeAgreement = new TradeAgreement();

        Expect.AreEqual(0, tradeAgreement.ReservedCountFor(NewThing()));
    }

    [Test]
    public static void AddReservationTracksTheGivenCount()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();

        tradeAgreement.AddReservation(thing, 5);

        Expect.AreEqual(5, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void AddReservationAccumulatesForTheSameThing()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();

        tradeAgreement.AddReservation(thing, 5);
        tradeAgreement.AddReservation(thing, 3);

        Expect.AreEqual(8, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void ReduceReservationReturnsTheAmountActuallyReleased()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();
        tradeAgreement.AddReservation(thing, 10);

        var released = tradeAgreement.ReduceReservation(thing, 4);

        Expect.AreEqual(4, released);
        Expect.AreEqual(6, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void ReduceReservationClampsToWhatsStillTracked()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();
        tradeAgreement.AddReservation(thing, 4);

        var released = tradeAgreement.ReduceReservation(thing, 100);

        Expect.AreEqual(4, released);
        Expect.AreEqual(0, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void ReduceReservationOnAnUntrackedThingReleasesNothing()
    {
        var tradeAgreement = new TradeAgreement();

        var released = tradeAgreement.ReduceReservation(NewThing(), 5);

        Expect.AreEqual(0, released);
    }

    [Test]
    public static void ReducingTheFullAmountRemovesTheEntryEntirely()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();
        tradeAgreement.AddReservation(thing, 4);

        _ = tradeAgreement.ReduceReservation(thing, 4);

        Expect.IsEmpty(tradeAgreement.reservedTraderThings);
    }

    [Test]
    public static void RestoreReservationReAddsAPreviouslyReleasedAmount()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();
        tradeAgreement.AddReservation(thing, 10);
        var released = tradeAgreement.ReduceReservation(thing, 4);

        tradeAgreement.RestoreReservation(thing, released);

        Expect.AreEqual(10, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void RestoreReservationOfZeroIsANoOp()
    {
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();

        tradeAgreement.RestoreReservation(thing, 0);

        Expect.AreEqual(0, tradeAgreement.ReservedCountFor(thing));
    }

    [Test]
    public static void PartiallyReducingAStackLeavesTheRemainderProtected()
    {
        // Guards the "unreserve an equal amount" requirement: hauling only part of a sold stack
        // must keep the rest reserved-for-protection rather than dropping the whole entry.
        var tradeAgreement = new TradeAgreement();
        var thing = NewThing();
        tradeAgreement.AddReservation(thing, 100);

        _ = tradeAgreement.ReduceReservation(thing, 30);

        Expect.AreEqual(70, tradeAgreement.ReservedCountFor(thing));
    }

    private static TransferableOneWay NewTransferable(int countToTransfer, params Thing[] things)
    {
        TransferableOneWay transferable = new();
        foreach (var thing in things)
        {
            transferable.things.Add(thing);
        }
        transferable.ForceTo(countToTransfer);
        return transferable;
    }

    [Test]
    public static void ReconcileReservationLeavesAStackAloneWhenNothingIsInExcess()
    {
        // "Still needed" (not CountToTransfer - see TradeAgreement.ReconcileReservation for why)
        // matching what's reserved is the ordinary "nothing to trim yet" case.
        var tradeAgreement = new TradeAgreement();
        var stack = NewThing();
        tradeAgreement.AddReservation(stack, 55);
        var transferable = NewTransferable(55, stack);

        tradeAgreement.ReconcileReservation(transferable, 55);

        Expect.AreEqual(55, tradeAgreement.ReservedCountFor(stack));
    }

    [Test]
    public static void ReconcileReservationReleasesSurplusLeftOnAnUntouchedStackOnceAnotherStackCoveredTheRest()
    {
        // Reproduces the real-world bug: a 75-unit stack has its full amount reserved at sale
        // time, but the hauling sweep only ever claims 55 of it directly (the rest of the need
        // came from a *different*, closer stack that was never tracked by AddReservation). Once
        // every pawn's claims add up to covering the whole transferable (stillNeeded reaches 0),
        // the remaining 20 reserved-but-untouched units on the original stack must be released -
        // it's not needed anymore, but nothing else would ever reduce its reservation since it
        // was never actually hauled.
        var tradeAgreement = new TradeAgreement();
        var stack = NewThing();
        tradeAgreement.AddReservation(stack, 75);
        var transferable = NewTransferable(0, stack);

        tradeAgreement.ReconcileReservation(transferable, 0);

        Expect.AreEqual(0, tradeAgreement.ReservedCountFor(stack));
        Expect.IsEmpty(tradeAgreement.reservedTraderThings);
    }

    [Test]
    public static void ReconcileReservationTrimsExcessAcrossMultipleStacksOfTheSameTransferable()
    {
        var tradeAgreement = new TradeAgreement();
        var stackA = NewThing();
        var stackB = NewThing();
        tradeAgreement.AddReservation(stackA, 30);
        tradeAgreement.AddReservation(stackB, 20);
        var transferable = NewTransferable(10, stackA, stackB);

        tradeAgreement.ReconcileReservation(transferable, 10);

        Expect.AreEqual(
            10,
            tradeAgreement.ReservedCountFor(stackA) + tradeAgreement.ReservedCountFor(stackB)
        );
    }

    [Test]
    public static void ReconcileReservationIgnoresANullTransferable()
    {
        var tradeAgreement = new TradeAgreement();
        var stack = NewThing();
        tradeAgreement.AddReservation(stack, 30);

        tradeAgreement.ReconcileReservation(null, 0);

        Expect.AreEqual(30, tradeAgreement.ReservedCountFor(stack));
    }

    [Test]
    public static void ReconcileReservationCanReleaseAcrossMultipleCallsAsStillNeededDrops()
    {
        // Mirrors the real driver: ReserveForHaul always calls ReduceReservation for the exact
        // amount it just claimed *before* ReconcileReservation ever runs, so by the time Reconcile
        // sees a stack's entry, that stack's own claimed portion is already gone from it - only a
        // stack nobody has claimed from yet (like stackB here) still carries its full original
        // amount. This reproduces two ReserveForHaul calls across one trip (a 42-unit pickup from
        // stackA, then a 13-unit pickup from stackB), each followed by its own reconcile using the
        // progressively-lower stillNeeded left for the whole transferable.
        var tradeAgreement = new TradeAgreement();
        var stackA = NewThing();
        var stackB = NewThing();
        tradeAgreement.AddReservation(stackA, 42);
        tradeAgreement.AddReservation(stackB, 33);
        var transferable = NewTransferable(55, stackA, stackB);

        _ = tradeAgreement.ReduceReservation(stackA, 42);
        tradeAgreement.ReconcileReservation(transferable, 13);
        Expect.AreEqual(13, tradeAgreement.ReservedCountFor(stackB));

        _ = tradeAgreement.ReduceReservation(stackB, 13);
        tradeAgreement.ReconcileReservation(transferable, 0);
        Expect.AreEqual(
            0,
            tradeAgreement.ReservedCountFor(stackA) + tradeAgreement.ReservedCountFor(stackB)
        );
    }
}
