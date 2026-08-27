using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: CompTradeShuttle.CheckAutoload runs every ~120 ticks while autoload is on,
// and recomputes "still needed" as the *full* remaining requirement each time, not a delta of
// newly discovered stock. Routing that through vanilla's own CompTransporter.AddToTheToLoadList
// merged into an existing to-load entry via an additive AdjustBy, so the same "still needed"
// figure kept getting added on top of itself every autoload tick - inflating CountToTransfer far
// past what was actually still owed, capped only by how much matching stock physically existed.
// Caught live via Player.log: a pawn queued exactly the correct remaining steel for a hand-carry
// trip, but by the time that trip finished delivering it, a fresh autoload tick had already
// pushed the requirement a few units higher again, sending the pawn back for more of the very
// same stack.
[TestSuite]
internal static class UpdateToLoadListTests
{
    private static int _nextThingId;

    private static readonly Dictionary<TransferableOneWay, int> NoCommitments = [];

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
    public static void UpdateToLoadListSetsCountRatherThanAddingOnRepeatedCalls()
    {
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var existing = NewTransferable(stack, 51);
        List<TransferableOneWay> leftToLoad = [existing];

        // Same candidate, same "still needed" figure - as a second autoload tick would report
        // right after the first, before anything's actually changed.
        var repeatCandidate = new TransferableOneWay();
        repeatCandidate.things.Add(stack);

        CompTradeShuttle.UpdateToLoadList(leftToLoad, repeatCandidate, 51, NoCommitments);

        Assert.That(existing.CountToTransfer).Is.EqualTo(51);
    }

    [Test]
    public static void UpdateToLoadListCorrectsDownWhenLessIsNowNeeded()
    {
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var existing = NewTransferable(stack, 51);
        List<TransferableOneWay> leftToLoad = [existing];

        var candidate = new TransferableOneWay();
        candidate.things.Add(stack);

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 4, NoCommitments);

        Assert.That(existing.CountToTransfer).Is.EqualTo(4);
    }

    // Regression guard: without this floor, a freshly recomputed (and legitimately lower) count
    // could talk CountToTransfer down below what a pawn has already split off the stockpile and
    // is actively carrying toward the transporter - desyncing the entry from a delivery that's
    // already in motion, which either strands the difference or makes the requirement look
    // already satisfied while that pawn is still mid-haul.
    [Test]
    public static void UpdateToLoadListNeverDropsCountBelowWhatsAlreadyCommitted()
    {
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var existing = NewTransferable(stack, 75);
        List<TransferableOneWay> leftToLoad = [existing];

        var candidate = new TransferableOneWay();
        candidate.things.Add(stack);

        Dictionary<TransferableOneWay, int> committed = new() { [existing] = 51 };

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 24, committed);

        Assert.That(existing.CountToTransfer).Is.EqualTo(51);
    }

    // Regression guard: the old lookup only checked the candidate's AnyThing (its first thing)
    // against existing entries via vanilla's strict TransferableMatching. CheckAutoload builds its
    // candidate's things list from whatever this mod's own (hit-point-drift-tolerant)
    // requiredItem.Matches accepted, so a thing that's already sitting in an existing entry's
    // things list (and thus already attributed commitments via ComputeCommittedCounts's
    // reference-first FindCommittedTransferable) can end up anywhere in that list, not
    // necessarily first. Checking AnyThing alone missed it, splitting the requirement across a
    // stray second entry that never got the commitment floor applied to it - producing the
    // "committed exceeds CountToTransfer" split observed live.
    [Test]
    public static void UpdateToLoadListMatchesAnExistingEntryEvenWhenItsNotTheCandidatesFirstThing()
    {
        var existingThing = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var existing = NewTransferable(existingThing, 66);
        List<TransferableOneWay> leftToLoad = [existing];

        var unrelatedThing = NewThing(RimWorld.ThingDefOf.WoodLog, 15);
        var candidate = new TransferableOneWay();
        candidate.things.Add(unrelatedThing);
        candidate.things.Add(existingThing);

        Dictionary<TransferableOneWay, int> committed = new() { [existing] = 75 };

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 66, committed);

        Assert.That(leftToLoad.Count).Is.EqualTo(1);
        Assert.That(existing.CountToTransfer).Is.EqualTo(75);
    }

    [Test]
    public static void UpdateToLoadListMergesNewlyFoundStacksIntoTheExistingEntry()
    {
        var firstStack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var existing = NewTransferable(firstStack, 51);
        List<TransferableOneWay> leftToLoad = [existing];

        var secondStack = NewThing(RimWorld.ThingDefOf.Steel, 20);
        var candidate = new TransferableOneWay();
        candidate.things.Add(firstStack);
        candidate.things.Add(secondStack);

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 55, NoCommitments);

        Assert.ThatCollection(existing.things).Contain(firstStack);
        Assert.ThatCollection(existing.things).Contain(secondStack);
        Assert.That(existing.things.Count).Is.EqualTo(2);
    }

    // Regression guard: FindExistingToLoadEntry used to defer to vanilla's
    // TransferableMatchingDesperate, whose third tier matches by ThingDef alone once neither
    // reference-containment nor TransferAsOne (stuff/quality/hit-points aware) finds a match. Two
    // required items sharing a def but differing in stuff (e.g. a cloth tuque and a muskox wool
    // tuque, both required by the same trade) would hit that fallback and silently fold into a
    // single leftToLoad row - the row that lost only ever showed up in "To load" again once the
    // one that won was fully hauled and its requirement dropped to zero, forcing one hauling trip
    // per distinct item instead of batching them together.
    [Test]
    public static void UpdateToLoadListDoesNotMergeDifferentStuffIntoTheSameRow()
    {
        var apparelDef = RimWorld.ThingDefOf.Apparel_Tuque;
        var clothTuque = NewThing(apparelDef, 1);
        clothTuque.SetStuffDirect(RimWorld.ThingDefOf.Cloth);
        var existing = NewTransferable(clothTuque, 1);
        List<TransferableOneWay> leftToLoad = [existing];

        var steelTuque = NewThing(apparelDef, 1);
        steelTuque.SetStuffDirect(RimWorld.ThingDefOf.Steel);
        var candidate = new TransferableOneWay();
        candidate.things.Add(steelTuque);

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 1, NoCommitments);

        Assert.That(leftToLoad.Count).Is.EqualTo(2);
        Assert.That(existing.CountToTransfer).Is.EqualTo(1);
        Assert.That(leftToLoad[1].CountToTransfer).Is.EqualTo(1);
    }

    [Test]
    public static void UpdateToLoadListAddsANewEntryWhenNothingMatchesYet()
    {
        List<TransferableOneWay> leftToLoad = [];
        var stack = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var candidate = new TransferableOneWay();
        candidate.things.Add(stack);

        CompTradeShuttle.UpdateToLoadList(leftToLoad, candidate, 51, NoCommitments);

        Assert.That(leftToLoad.Count).Is.EqualTo(1);
        Assert.That(leftToLoad[0].CountToTransfer).Is.EqualTo(51);
    }

    [Test]
    public static void UpdateSpecificToLoadEntrySetsCountRatherThanAddingOnRepeatedCalls()
    {
        var requiredThing = NewThing(RimWorld.ThingDefOf.Steel, 1);
        var existing = NewTransferable(requiredThing, 1);
        List<TransferableOneWay> leftToLoad = [existing];

        var candidate = new TransferableOneWay();
        candidate.things.Add(requiredThing);

        CompTradeShuttle.UpdateSpecificToLoadEntry(
            leftToLoad,
            requiredThing,
            candidate,
            1,
            NoCommitments
        );

        Assert.That(existing.CountToTransfer).Is.EqualTo(1);
        // The freshly-built candidate for this cycle must not have become a second row for the
        // same requiredThing.
        Assert.That(leftToLoad.Count).Is.EqualTo(1);
    }

    [Test]
    public static void UpdateSpecificToLoadEntryAddsANewRowWhenNothingMatchesYet()
    {
        List<TransferableOneWay> leftToLoad = [];
        var requiredThing = NewThing(RimWorld.ThingDefOf.Steel, 1);
        var candidate = new TransferableOneWay();
        candidate.things.Add(requiredThing);

        CompTradeShuttle.UpdateSpecificToLoadEntry(
            leftToLoad,
            requiredThing,
            candidate,
            1,
            NoCommitments
        );

        Assert.That(leftToLoad.Count).Is.EqualTo(1);
        Assert.That(leftToLoad[0].CountToTransfer).Is.EqualTo(1);
    }
}
