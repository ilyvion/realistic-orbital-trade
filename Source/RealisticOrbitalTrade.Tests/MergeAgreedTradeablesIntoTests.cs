using DevTools.Testing;
using RealisticOrbitalTrade.Core;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the double-counting bug caught during review of full renegotiation: seeding
// a reopened trade dialog's live TradeDeal (Patch/Rimworld_TradeDeal_AddAllTradeables.cs) merges
// the agreed deal on top of tradeables vanilla's own scan already populated. An already-sold item
// still sitting untouched in a stockpile is found by *both* - blindly re-adding its physical Thing
// would duplicate it in Tradeable.thingsColony and double-count its stack in mass/count
// calculations.
[TestFixture(TestType.MainMenu)]
internal sealed class MergeAgreedTradeablesIntoTests
{
    // TradeAgreement.MatchingTradeable dereferences TradeSession.trader - see FakeTrader.
    [SetUp]
    public static void SetUpTradeSession() => TradeSession.trader = new FakeTrader();

    [TearDown]
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
        Expect.AreEqual(1, tradeables.Count);
        Expect.AreEqual(1, tradeables[0].thingsColony.Count);
        Expect.AreEqual(-10, tradeables[0].CountToTransfer);
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

        Expect.AreEqual(1, tradeables.Count);
        Expect.AreEqual(-10, tradeables[0].CountToTransfer);
    }

    // Regression guard: renegotiating a partially-hauled sale (some of
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

        Expect.AreEqual(1, tradeables.Count);
        Expect.AreEqual(-430, tradeables[0].CountToTransfer);
        Expect.AreEqual(430, tradeables[0].CountHeldBy(Transactor.Colony));
    }

    // Regression guard: silver was excluded from the Tradeable_Capped
    // treatment every other sold def gets (both here and in ComputeRenegotiationChanges's
    // matching), so silver already loaded onto the shuttle or mid-haul toward it vanished from
    // CountHeldBy(Colony) entirely instead of being added back like every other sold good -
    // making the renegotiation dialog show only whatever silver was still sitting in storage as
    // the colony's total available amount.
    [Test]
    public static void MergeAppliesACapFloorForSilverToo()
    {
        var tradeAgreement = NewTradeAgreement();
        var thing = NewThing(RimWorld.ThingDefOf.Silver, 430);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 430));
        thing.stackCount = 375;

        Tradeable existing = new();
        existing.AddThing(thing, Transactor.Colony);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Expect.AreEqual(1, tradeables.Count);
        Expect.AreEqual(-430, tradeables[0].CountToTransfer);
        Expect.AreEqual(430, tradeables[0].CountHeldBy(Transactor.Colony));
    }

    // Regression guard: a colony can hold more of an already-sold def
    // than was originally committed (e.g. more was gathered since, or some is already loaded onto
    // the shuttle plus more sits untouched in storage) - CountHeldBy(Colony) must reflect that
    // true, higher total rather than plateauing at the originally agreed amount. This is the pure
    // matching/summing logic that total is built from (see
    // TradeAgreement.CountOfMatchingThingsInFlightToTrader, which feeds it the trader shuttle's
    // cargo and whatever's mid-haul toward it - both untestable here without a live Map/TransportShip).
    // Regression guard: when nothing is left reachable in colony
    // stockpiles (everything sold is already loaded/hauled), vanilla's own AddAllTradeables scan
    // finds no IsCurrency tradeable and seeds a zero-stack dummy on the trader side instead (see
    // TradeDeal.AddAllTradeables's final block). The merge must still find and upgrade *that*
    // entry for a committed silver payment, the same way it upgrades a colony-stock-backed one -
    // otherwise the live TradeDeal ends up with two Silver-def tradeables (vanilla's untouched
    // dummy plus a second one this merge creates from scratch), and whichever one
    // Dialog_Trade/Window_DynamicTrade's own currency lookup happens to find first is not
    // necessarily the one carrying the real CountToTransfer, silently hiding the currency row's
    // true value from the player.
    [Test]
    public static void MergeUpgradesVanillasOwnCurrencyDummyWhenNoColonySilverIsReachable()
    {
        var tradeAgreement = NewTradeAgreement();
        var thing = NewThing(RimWorld.ThingDefOf.Silver, 299);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 299));

        var dummy = NewThing(RimWorld.ThingDefOf.Silver, 0);
        Tradeable existing = new();
        existing.AddThing(dummy, Transactor.Trader);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Expect.AreEqual(1, tradeables.Count);
        Expect.IsTrue(tradeables[0].IsCurrency);
        Expect.AreEqual(-299, tradeables[0].CountToTransfer);
    }

    // Regression guard: TradeUI.DrawTradeableRow shows the trader's own
    // held count (and its price) via Tradeable.CountHeldBy(Transactor.Trader), skipping that whole
    // column when the count is 0. The upgraded (Tradeable_Capped) entry must still report the
    // trader's real holdings there instead of always reporting 0 - otherwise, for silver
    // specifically (whose thingsTrader always carries the trader's funds), the trader's silver
    // amount silently disappears from the currency row the moment any silver gets sold/loaded.
    [Test]
    public static void MergeDoesNotZeroOutTheTradersOwnHeldCount()
    {
        var tradeAgreement = NewTradeAgreement();
        var soldThing = NewThing(RimWorld.ThingDefOf.Silver, 299);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(soldThing, 299));

        var traderSilver = NewThing(RimWorld.ThingDefOf.Silver, 5000);
        Tradeable existing = new();
        existing.AddThing(traderSilver, Transactor.Trader);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Expect.AreEqual(5000, tradeables[0].CountHeldBy(Transactor.Trader));
    }

    [Test]
    public static void SumMatchingStackCountsOnlySumsThingsMatchingTheTradeable()
    {
        Tradeable tradeable = new();
        tradeable.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 10), Transactor.Colony);

        var matching = NewThing(RimWorld.ThingDefOf.Steel, 20);
        var nonMatching = NewThing(RimWorld.ThingDefOf.WoodLog, 300);

        var result = TradeAgreement.SumMatchingStackCounts(tradeable, [matching, nonMatching]);

        Expect.AreEqual(20, result);
    }

    [Test]
    public static void SumMatchingStackCountsIsZeroForNoThings()
    {
        Tradeable tradeable = new();
        tradeable.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 10), Transactor.Colony);

        var result = TradeAgreement.SumMatchingStackCounts(tradeable, []);

        Expect.AreEqual(0, result);
    }
}
