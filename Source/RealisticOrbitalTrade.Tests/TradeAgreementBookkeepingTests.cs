using DevTools.Testing;
using RealisticOrbitalTrade.Core;

namespace RealisticOrbitalTrade.Tests;

// Sold-record and required-cargo bookkeeping on TradeAgreement that needs no map: re-pointing
// records when stacks merge, and the distribution of sold goods over shuttles.
[TestFixture(TestType.MainMenu)]
internal sealed class TradeAgreementBookkeepingTests
{
    // ThingCountClass.Count clamps to thing.stackCount, and Thing.Equals dereferences def and
    // compares thingIDNumber, so each bare thing needs all three set.
    private static int _nextThingId;

    private static Pawn NewThing(int stackCount = 100) =>
        new()
        {
            stackCount = stackCount,
            def = RimWorld.ThingDefOf.Steel,
            thingIDNumber = _nextThingId++,
        };

    // Notify_OwnThingMerged

    [Test]
    public static void MergingAnUnreservedThingRepointsOnlyTheSoldRecord()
    {
        var absorbed = NewThing();
        var survivor = NewThing();
        var other = NewThing();
        var agreement = new TradeAgreement();
        agreement.thingsSoldToTrader.Add(new(absorbed, 10));
        agreement.thingsSoldToTrader.Add(new(other, 5));

        agreement.Notify_OwnThingMerged(absorbed, survivor);

        Expect.AreEqual(survivor, agreement.thingsSoldToTrader[0].thing);
        Expect.AreEqual(other, agreement.thingsSoldToTrader[1].thing);
        Expect.IsEmpty(agreement.reservedTraderThings);
    }

    [Test]
    public static void MergingRepointsEverySoldRecordForTheAbsorbedThing()
    {
        var absorbed = NewThing();
        var survivor = NewThing();
        var agreement = new TradeAgreement();
        agreement.thingsSoldToTrader.Add(new(absorbed, 10));
        agreement.thingsSoldToTrader.Add(new(absorbed, 5));

        agreement.Notify_OwnThingMerged(absorbed, survivor);

        Expect.IsTrue(agreement.thingsSoldToTrader.All(t => t.thing == survivor));
    }

    [Test]
    public static void MergingAddsBothReservationsTogether()
    {
        var absorbed = NewThing();
        var survivor = NewThing();
        var agreement = new TradeAgreement();
        agreement.AddReservation(absorbed, 20);
        agreement.AddReservation(survivor, 30);

        agreement.Notify_OwnThingMerged(absorbed, survivor);

        Expect.AreEqual(1, agreement.reservedTraderThings.Count);
        Expect.AreEqual(50, agreement.ReservedCountFor(survivor));
        Expect.AreEqual(0, agreement.ReservedCountFor(absorbed));
    }

    [Test]
    public static void MergedReservationsAreClampedToTheSurvivingStack()
    {
        var absorbed = NewThing();
        var survivor = NewThing(stackCount: 40);
        var agreement = new TradeAgreement();
        agreement.AddReservation(absorbed, 30);
        agreement.AddReservation(survivor, 30);

        agreement.Notify_OwnThingMerged(absorbed, survivor);

        Expect.AreEqual(40, agreement.ReservedCountFor(survivor));
    }

    [Test]
    public static void MergingIntoAnUnreservedSurvivorMovesTheReservation()
    {
        var absorbed = NewThing();
        var survivor = NewThing();
        var agreement = new TradeAgreement();
        agreement.AddReservation(absorbed, 20);

        agreement.Notify_OwnThingMerged(absorbed, survivor);

        Expect.AreEqual(1, agreement.reservedTraderThings.Count);
        Expect.AreEqual(20, agreement.ReservedCountFor(survivor));
        Expect.AreEqual(0, agreement.ReservedCountFor(absorbed));
    }

    [Test]
    public static void MergingAnUnrelatedThingLeavesReservationsAlone()
    {
        var reserved = NewThing();
        var agreement = new TradeAgreement();
        agreement.AddReservation(reserved, 20);

        agreement.Notify_OwnThingMerged(NewThing(), NewThing());

        Expect.AreEqual(20, agreement.ReservedCountFor(reserved));
    }

    // BuildLines

    [Test]
    public static void LinesForThingsUseTheirIndexAsId()
    {
        var lines = TradeAgreement.BuildLines([(2f, 5, false), (3f, 1, false)], []);

        Expect.AreEqual(2, lines.Count);
        Expect.AreEqual(new ShuttleLoadPlanner.Line(0, 2f, 5), lines[0]);
        Expect.AreEqual(new ShuttleLoadPlanner.Line(1, 3f, 1), lines[1]);
    }

    [Test]
    public static void LostThingsAndPawnsGetNoLineButKeepTheirIndices()
    {
        var lines = TradeAgreement.BuildLines(
            [(2f, 5, true), (3f, 1, false)],
            [(60f, true), (70f, false)]
        );

        Expect.AreEqual(2, lines.Count);
        Expect.AreEqual(1, lines[0].Id);
        Expect.AreEqual(TradeAgreement.PawnLineIdBase + 1, lines[1].Id);
    }

    [Test]
    public static void PawnLineIdsDoNotCollideWithThingLineIds()
    {
        var lines = TradeAgreement.BuildLines([(1f, 1, false)], [(60f, false)]);

        Expect.AreEqual(2, lines.Select(l => l.Id).Distinct().Count());
        Expect.AreEqual(
            new ShuttleLoadPlanner.Line(TradeAgreement.PawnLineIdBase, 60f, 1),
            lines[1]
        );
    }

    [Test]
    public static void NothingSoldGivesNoLines() =>
        Expect.IsEmpty(TradeAgreement.BuildLines([], []));

    // DistributeByFreeMass

    [Test]
    public static void ExistingShuttlesAreFilledBeforeTheLastTakesTheRest()
    {
        var shares = TradeAgreement.DistributeByFreeMass([10f, 10f], 3f, 5);

        Expect.AreEqual("3,2", string.Join(",", shares));
    }

    [Test]
    public static void NegativeFreeMassTakesNothing()
    {
        var shares = TradeAgreement.DistributeByFreeMass([-5f, 10f], 3f, 4);

        Expect.AreEqual("0,4", string.Join(",", shares));
    }

    [Test]
    public static void MoreThanAllTheRoomLandsOnTheLastShuttle()
    {
        var shares = TradeAgreement.DistributeByFreeMass([6f, 6f], 3f, 10);

        Expect.AreEqual("2,8", string.Join(",", shares));
    }

    [Test]
    public static void ASingleShuttleTakesEverything()
    {
        var shares = TradeAgreement.DistributeByFreeMass([1f], 3f, 7);

        Expect.AreEqual("7", string.Join(",", shares));
    }

    [Test]
    public static void AWeightlessUnitAllGoesOnTheFirstShuttle()
    {
        var shares = TradeAgreement.DistributeByFreeMass([0f, 10f], 0f, 5);

        Expect.AreEqual("5,0", string.Join(",", shares));
    }

    [Test]
    public static void ZeroCountGivesZeroShares()
    {
        var shares = TradeAgreement.DistributeByFreeMass([10f, 10f], 3f, 0);

        Expect.AreEqual("0,0", string.Join(",", shares));
    }

    [Test]
    public static void AnExactMultipleOfFreeMassFitsDespiteFloatRounding()
    {
        var shares = TradeAgreement.DistributeByFreeMass([0.3f * 3f, 10f], 0.3f, 4);

        Expect.AreEqual("3,1", string.Join(",", shares));
    }

    // ReduceRequiredItemsAcross

    [Test]
    public static void ReductionSpillsOntoTheNextShuttleWhenTheFirstIsExhausted()
    {
        var def = RimWorld.ThingDefOf.Steel;
        List<ThingDefCount> first = [new(def, 4)];
        List<ThingDefCount> second = [new(def, 10)];
        var left = 6;

        var reduced = TradeAgreement.ReduceRequiredItemsAcross([first, second], def, ref left);

        Expect.IsTrue(reduced);
        Expect.AreEqual(0, left);
        Expect.IsEmpty(first);
        Expect.AreEqual(8, second[0].Count);
    }

    [Test]
    public static void ReductionStartsWithTheFirstShuttleGiven()
    {
        var def = RimWorld.ThingDefOf.Steel;
        List<ThingDefCount> first = [new(def, 10)];
        List<ThingDefCount> second = [new(def, 10)];
        var left = 3;

        _ = TradeAgreement.ReduceRequiredItemsAcross([first, second], def, ref left);

        Expect.AreEqual(7, first[0].Count);
        Expect.AreEqual(10, second[0].Count);
    }

    [Test]
    public static void ReductionWithNoMatchingEntryReportsNothingAndKeepsTheAmountLeft()
    {
        List<ThingDefCount> requiredItems = [new(RimWorld.ThingDefOf.Steel, 10)];
        var left = 5;

        var reduced = TradeAgreement.ReduceRequiredItemsAcross(
            [requiredItems],
            RimWorld.ThingDefOf.Gold,
            ref left
        );

        Expect.IsFalse(reduced);
        Expect.AreEqual(5, left);
        Expect.AreEqual(10, requiredItems[0].Count);
    }

    // RemoveUnresolvedSoldEntries / Notify_OwnThingDestroyed without a shuttle

    [Test]
    public static void NothingUnresolvedMeansNothingIsLost()
    {
        var agreement = new TradeAgreement();
        agreement.thingsSoldToTrader.Add(new(NewThing(), 5));
        agreement.pawnsSoldToTrader.Add(NewThing());

        Expect.IsFalse(agreement.RemoveUnresolvedSoldEntries());
        Expect.AreEqual(1, agreement.thingsSoldToTrader.Count);
        Expect.AreEqual(1, agreement.pawnsSoldToTrader.Count);
    }

    [Test]
    public static void AnUnresolvedSoldThingIsDropped()
    {
        var agreement = new TradeAgreement();
        agreement.thingsSoldToTrader.Add(new(null, 5));

        Expect.IsTrue(agreement.RemoveUnresolvedSoldEntries());
        Expect.IsEmpty(agreement.thingsSoldToTrader);
    }

    [Test]
    public static void AnUnresolvedSoldPawnIsDropped()
    {
        var agreement = new TradeAgreement();
        agreement.pawnsSoldToTrader.Add(null!);

        Expect.IsTrue(agreement.RemoveUnresolvedSoldEntries());
        Expect.IsEmpty(agreement.pawnsSoldToTrader);
    }

    [Test]
    public static void AnUnresolvedReservationIsDroppedWithoutCountingAsALoss()
    {
        var agreement = new TradeAgreement();
        agreement.reservedTraderThings.Add(new(null, 5));
        var kept = NewThing();
        agreement.AddReservation(kept, 3);

        Expect.IsFalse(agreement.RemoveUnresolvedSoldEntries());
        Expect.AreEqual(1, agreement.reservedTraderThings.Count);
        Expect.AreEqual(3, agreement.ReservedCountFor(kept));
    }

    [Test]
    public static void DestroyingASoldThingDoesNothingWithoutAToTraderShuttle()
    {
        var sold = NewThing();
        var agreement = new TradeAgreement();
        agreement.thingsSoldToTrader.Add(new(sold, 5));

        agreement.Notify_OwnThingDestroyed(sold);

        Expect.AreEqual(1, agreement.thingsSoldToTrader.Count);
        Expect.IsFalse(agreement.RenegotiationRequired);
        Expect.IsEmpty(agreement.lostSoldItems);
    }
}
