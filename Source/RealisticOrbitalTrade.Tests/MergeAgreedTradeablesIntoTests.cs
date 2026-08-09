using RealisticOrbitalTrade.Core;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the double-counting bug caught during review of full renegotiation: seeding
// a reopened trade dialog's live TradeDeal (Patch/Rimworld_TradeDeal_AddAllTradeables.cs) merges
// the agreed deal on top of tradeables vanilla's own scan already populated. An already-sold item
// still sitting untouched in a stockpile is found by *both* - blindly re-adding its physical Thing
// would duplicate it in Tradeable.thingsColony and double-count its stack in mass/count
// calculations.
[TestSuite]
internal static class MergeAgreedTradeablesIntoTests
{
    // TransferableUtility.TradeableMatching dereferences TradeSession.trader - see FakeTrader.
    [BeforeEach]
    public static void SetUpTradeSession() => TradeSession.trader = new FakeTrader();

    [AfterEach]
    public static void TearDownTradeSession() => TradeSession.trader = null;

    private static int _nextThingId;

    private static Pawn NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static TradeAgreement NewTradeAgreement()
    {
        var tradeAgreement = new TradeAgreement();
        tradeAgreement.thingsSoldToPlayer = new ThingOwner<Thing>(tradeAgreement);
        tradeAgreement.tradeDeliveryMethod = new TradeDeliveryMethod_DropPod(tradeAgreement);
        return tradeAgreement;
    }

    [Test]
    public static void MergeDoesNotDuplicateAPhysicalThingTheLiveScanAlreadyFound()
    {
        var tradeAgreement = NewTradeAgreement();
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 15);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 10));

        // Simulate vanilla's own AddAllTradeables scan already having found this exact physical
        // Thing as ordinary sellable stock (still untouched in a stockpile, not yet hauled) before
        // the merge runs.
        Tradeable existing = new();
        existing.AddThing(thing, Transactor.Colony);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        // The matched plain Tradeable gets upgraded (list entry replaced) to a capped one - see
        // MergeAppliesACapFloorWhenAMatchedThingsStackHasAlreadyShrunk - so assertions run against
        // the current tradeables[0], not the now-orphaned `existing` reference.
        Assert.ThatCollection(tradeables).Has.Count(1);
        Assert.ThatCollection(tradeables[0].thingsColony).Has.Count(1);
        Assert.That(tradeables[0].CountToTransfer).Is.EqualTo(-10);
    }

    [Test]
    public static void MergeInjectsAnItemTheLiveScanDidNotFind()
    {
        // E.g. already loaded onto the shuttle, so it's no longer reachable as ordinary sellable
        // colony stock.
        var tradeAgreement = NewTradeAgreement();
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 10);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 10));

        List<Tradeable> tradeables = [];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Assert.ThatCollection(tradeables).Has.Count(1);
        Assert.That(tradeables[0].CountToTransfer).Is.EqualTo(-10);
    }

    // Regression guard for the bug reported live: renegotiating a partially-hauled sale (some of
    // the stack already split off and loaded onto the shuttle, or mid-haul in a pawn's hands)
    // showed a maximum sellable amount lower than what was actually already sold, because
    // CountHeldBy(Colony) only summed physically-reachable Things, undercounting the portion no
    // longer reachable by vanilla's scan.
    [Test]
    public static void MergeAppliesACapFloorWhenAMatchedThingsStackHasAlreadyShrunk()
    {
        var tradeAgreement = NewTradeAgreement();
        // Originally a 430-count stack was sold in full (ThingCountClass.Count clamps to the
        // thing's stackCount at construction, so it must be recorded while the stack is still
        // whole). By the time renegotiation reopens, 55 of it has already been split off and
        // hauled away - shrinking the physical remainder directly, bypassing that clamp, exactly
        // like a real hauling job's Thing.SplitOff does - leaving only 375 still on the original
        // Thing reference that thingsSoldToTrader still points at.
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 430);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 430));
        thing.stackCount = 375;

        // Vanilla's own scan finds the shrunken remainder stack as ordinary sellable colony stock.
        Tradeable existing = new();
        existing.AddThing(thing, Transactor.Colony);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Assert.ThatCollection(tradeables).Has.Count(1);
        Assert.That(tradeables[0].CountToTransfer).Is.EqualTo(-430);
        Assert.That(tradeables[0].CountHeldBy(Transactor.Colony)).Is.EqualTo(430);
    }

    // Regression guard for the bug reported live: a colony can hold more of an already-sold def
    // than was originally committed (e.g. more was gathered since, or some is already loaded onto
    // the shuttle plus more sits untouched in storage) - CountHeldBy(Colony) must reflect that
    // true, higher total rather than plateauing at the originally agreed amount. This is the pure
    // matching/summing logic that total is built from (see
    // TradeAgreement.CountOfMatchingThingsInFlightToTrader, which feeds it the trader shuttle's
    // cargo and whatever's mid-haul toward it - both untestable here without a live Map/TransportShip).
    [Test]
    public static void SumMatchingStackCountsOnlySumsThingsMatchingTheTradeable()
    {
        Tradeable tradeable = new();
        tradeable.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 10), Transactor.Colony);

        var matching = NewThing(RimWorld.ThingDefOf.Steel, 20);
        var nonMatching = NewThing(RimWorld.ThingDefOf.WoodLog, 300);

        var result = TradeAgreement.SumMatchingStackCounts(tradeable, [matching, nonMatching]);

        Assert.That(result).Is.EqualTo(20);
    }

    [Test]
    public static void SumMatchingStackCountsIsZeroForNoThings()
    {
        Tradeable tradeable = new();
        tradeable.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 10), Transactor.Colony);

        var result = TradeAgreement.SumMatchingStackCounts(tradeable, []);

        Assert.That(result).Is.EqualTo(0);
    }
}
