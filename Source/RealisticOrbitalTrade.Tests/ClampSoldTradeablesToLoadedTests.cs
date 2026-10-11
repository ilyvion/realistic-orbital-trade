using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for TradeAgreement.ClampSoldTradeablesToLoaded, the core of the
// "trader leaves before loading finished" auto-renegotiation (issue #7): when the trade shuttle's
// departure timer expires with cargo only partially loaded, whatever's already loaded gets locked
// in as the new sold-to-trader amount instead of dumping everything and failing the trade outright.
[TestFixture(TestType.MainMenu)]
internal sealed class ClampSoldTradeablesToLoadedTests
{
    // TradeAgreement.MatchingTradeable dereferences TradeSession.trader - see FakeTrader.
    [SetUp]
    public static void SetUpTradeSession() => TradeSession.trader = new FakeTrader();

    [TearDown]
    public static void TearDownTradeSession() => TradeSession.trader = null;

    // Thing.Equals/List<Thing>.Contains dereferences def and compares thingIDNumber - see
    // ReservedTraderThingsTests for why every test thing needs a real def and a distinct id.
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static Pawn NewPawn() =>
        new() { def = RimWorld.ThingDefOf.Human, thingIDNumber = _nextThingId++ };

    private static Tradeable NewSoldTradeable(Thing thing, int countToTransfer)
    {
        Tradeable tradeable = new();
        tradeable.AddThing(thing, Transactor.Colony);
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    [Test]
    public static void FullyLoadedSoldItemIsUnchanged()
    {
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 10);
        var tradeable = NewSoldTradeable(thing, -10);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], [thing]);

        Expect.AreEqual(-10, tradeable.CountToTransfer);
    }

    [Test]
    public static void PartiallyLoadedSoldItemIsReducedToWhatsLoaded()
    {
        var soldThing = NewThing(RimWorld.ThingDefOf.Steel, 10);
        var tradeable = NewSoldTradeable(soldThing, -10);
        var loadedThing = NewThing(RimWorld.ThingDefOf.Steel, 4);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], [loadedThing]);

        Expect.AreEqual(-4, tradeable.CountToTransfer);
    }

    [Test]
    public static void UnloadedSoldItemIsDroppedFromTheDeal()
    {
        var soldThing = NewThing(RimWorld.ThingDefOf.Steel, 10);
        var tradeable = NewSoldTradeable(soldThing, -10);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], []);

        Expect.AreEqual(0, tradeable.CountToTransfer);
    }

    [Test]
    public static void LoadedPawnIsUnchanged()
    {
        var pawn = NewPawn();
        var tradeable = NewSoldTradeable(pawn, -1);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], [pawn]);

        Expect.AreEqual(-1, tradeable.CountToTransfer);
    }

    [Test]
    public static void UnboardedPawnIsDroppedFromTheDeal()
    {
        var pawn = NewPawn();
        var tradeable = NewSoldTradeable(pawn, -1);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], []);

        Expect.AreEqual(0, tradeable.CountToTransfer);
    }

    [Test]
    public static void BoughtItemIsUntouchedRegardlessOfWhatsLoaded()
    {
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 5);
        Tradeable tradeable = new();
        tradeable.AddThing(thing, Transactor.Trader);
        tradeable.ForceTo(5);

        TradeAgreement.ClampSoldTradeablesToLoaded([tradeable], []);

        Expect.AreEqual(5, tradeable.CountToTransfer);
    }
}
