using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for the diff at the heart of full renegotiation
// (TradeAgreement.ComputeRenegotiationChanges): comparing the deal as originally agreed against
// its edited state must produce the right delta for both changes to an already-agreed tradeable
// (previously the only case renegotiation supported) *and* brand-new tradeables that only exist
// on the "new" side - additions the old Dialog_RenegotiateTrade could never produce, since it only
// ever showed tradeables already present in the agreed deal.
[TestFixture(TestType.MainMenu)]
internal sealed class ComputeRenegotiationChangesTests
{
    // TradeAgreement.MatchingTradeable dereferences TradeSession.trader - see FakeTrader.
    [SetUp]
    public static void SetUpTradeSession() => TradeSession.trader = new FakeTrader();

    [TearDown]
    public static void TearDownTradeSession() => TradeSession.trader = null;

    // Thing.Equals/List<Thing>.Contains dereferences def and compares thingIDNumber - see
    // ReservedTraderThingsTests for why every test thing needs a real def and a distinct id.
    private static int _nextThingId;

    private static Pawn NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static Tradeable NewTradeable(ThingDef def, int countToTransfer)
    {
        var thing = NewThing(def, Math.Max(1, Math.Abs(countToTransfer)));
        Tradeable tradeable = new();
        tradeable.AddThing(thing, countToTransfer <= 0 ? Transactor.Colony : Transactor.Trader);
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    [Test]
    public static void UnchangedTradeableProducesNoChanges()
    {
        var original = NewTradeable(RimWorld.ThingDefOf.Steel, -10);
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, -10);

        var changes = TradeAgreement.ComputeRenegotiationChanges([original], [updated]);

        Expect.IsEmpty(changes);
    }

    [Test]
    public static void ReducingAnAlreadySoldItemProducesAToTraderRemoval()
    {
        var original = NewTradeable(RimWorld.ThingDefOf.Steel, -10);
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, -5);

        var changes = TradeAgreement.ComputeRenegotiationChanges([original], [updated]);

        Expect.AreEqual(1, changes.Count);
        var (direction, tradeable, amount) = changes[0];
        Expect.AreEqual(TradeAgreement.TransferDirection.ToTrader, direction);
        Expect.IsTrue(ReferenceEquals(tradeable, updated));
        Expect.AreEqual(-5, amount);
    }

    [Test]
    public static void IncreasingAnAlreadySoldItemProducesAToTraderAddition()
    {
        // The case that never used to be reachable: an already-agreed sale growing rather than
        // shrinking (e.g. selling 5 more steel on top of the 10 already sold).
        var original = NewTradeable(RimWorld.ThingDefOf.Steel, -10);
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, -15);

        var changes = TradeAgreement.ComputeRenegotiationChanges([original], [updated]);

        Expect.AreEqual(1, changes.Count);
        var (direction, tradeable, amount) = changes[0];
        Expect.AreEqual(TradeAgreement.TransferDirection.ToTrader, direction);
        Expect.IsTrue(ReferenceEquals(tradeable, updated));
        Expect.AreEqual(5, amount);
    }

    [Test]
    public static void BrandNewSaleWithNoOriginalCounterpartProducesAToTraderAddition()
    {
        // The headline new capability: an item that was never part of the agreed deal at all.
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, -8);

        var changes = TradeAgreement.ComputeRenegotiationChanges([], [updated]);

        Expect.AreEqual(1, changes.Count);
        var (direction, tradeable, amount) = changes[0];
        Expect.AreEqual(TradeAgreement.TransferDirection.ToTrader, direction);
        Expect.IsTrue(ReferenceEquals(tradeable, updated));
        Expect.AreEqual(8, amount);
    }

    [Test]
    public static void BrandNewPurchaseWithNoOriginalCounterpartProducesAToPlayerAddition()
    {
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, 6);

        var changes = TradeAgreement.ComputeRenegotiationChanges([], [updated]);

        Expect.AreEqual(1, changes.Count);
        var (direction, tradeable, amount) = changes[0];
        Expect.AreEqual(TradeAgreement.TransferDirection.ToPlayer, direction);
        Expect.IsTrue(ReferenceEquals(tradeable, updated));
        Expect.AreEqual(6, amount);
    }

    [Test]
    public static void UntouchedBrandNewTradeableProducesNoChange()
    {
        // A candidate item merged in from the trader's currently-sellable/buyable stock (see
        // TradeAgreement.MergeAgreedTradeablesInto / the AddAllTradeables postfix) that the player
        // never actually touched during renegotiation must not be treated as an addition.
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, 0);

        var changes = TradeAgreement.ComputeRenegotiationChanges([], [updated]);

        Expect.IsEmpty(changes);
    }

    [Test]
    public static void MixedReduceOneAddAnotherProducesBothChanges()
    {
        var originalSteel = NewTradeable(RimWorld.ThingDefOf.Steel, -10);
        var updatedSteel = NewTradeable(RimWorld.ThingDefOf.Steel, -4);
        var newSilver = NewTradeable(RimWorld.ThingDefOf.Silver, -50);

        var changes = TradeAgreement.ComputeRenegotiationChanges(
            [originalSteel],
            [updatedSteel, newSilver]
        );

        Expect.AreEqual(2, changes.Count);
        var (steelDirection, _, steelAmount) = changes.Single(c => c.tradeable == updatedSteel);
        Expect.AreEqual(TradeAgreement.TransferDirection.ToTrader, steelDirection);
        Expect.AreEqual(-6, steelAmount);
        var (silverDirection, _, silverAmount) = changes.Single(c => c.tradeable == newSilver);
        Expect.AreEqual(TradeAgreement.TransferDirection.ToTrader, silverDirection);
        Expect.AreEqual(50, silverAmount);
    }

    [Test]
    public static void SwitchingSidesProducesBothAReductionAndAnIncrease()
    {
        // Previously sold (negative), now being bought instead (positive) - both halves of the
        // swing need to be applied.
        var original = NewTradeable(RimWorld.ThingDefOf.Steel, -10);
        var updated = NewTradeable(RimWorld.ThingDefOf.Steel, 5);

        var changes = TradeAgreement.ComputeRenegotiationChanges([original], [updated]);

        Expect.AreEqual(2, changes.Count);
        // ToTrader's amount is the original (already-negative) CountToTransfer itself, not its
        // magnitude - a negative ToTrader amount means "undo this much of the previous sale" (see
        // the "hard case" removal branch in ApplyRenegotiation), which is exactly what fully
        // reversing a sale that switched to a purchase means.
        var (_, _, toTraderAmount) = changes.Single(c =>
            c.direction == TradeAgreement.TransferDirection.ToTrader
        );
        Expect.AreEqual(-10, toTraderAmount);
        var (_, _, toPlayerAmount) = changes.Single(c =>
            c.direction == TradeAgreement.TransferDirection.ToPlayer
        );
        Expect.AreEqual(5, toPlayerAmount);
    }

    [Test]
    public static void IsFullCancellationIsTrueWhenEverythingIsZero()
    {
        var tradeables = new List<Tradeable>
        {
            NewTradeable(RimWorld.ThingDefOf.Steel, 0),
            NewTradeable(RimWorld.ThingDefOf.Silver, 0),
        };

        Expect.IsTrue(TradeAgreement.IsFullCancellation(tradeables));
    }

    [Test]
    public static void IsFullCancellationIsFalseWhenSomethingRemains()
    {
        var tradeables = new List<Tradeable>
        {
            NewTradeable(RimWorld.ThingDefOf.Steel, -3),
            NewTradeable(RimWorld.ThingDefOf.Silver, 0),
        };

        Expect.IsFalse(TradeAgreement.IsFullCancellation(tradeables));
    }
}
