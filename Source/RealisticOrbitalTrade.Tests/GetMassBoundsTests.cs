using RealisticOrbitalTrade.Patch;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_TradeUI_DrawTradeableRow.GetMassBounds/ComputeMassBounds:
// the branching that decides *whether* a row gets a mass-based stopping point at all -
// which direction, gated on the live trade agreement, on drop-pod delivery, and on which side of
// the deal the row is currently on. The currency-rebalancing arithmetic underneath it is covered
// directly by FindMaxBuyCountTests/FindMaxSellCountTests; every scenario here keeps the deal free
// of any silver tradeable so this logic never needs real trade pricing to run.
//
// GetMassBounds' own two early-return gates are exercised directly; everything past them goes
// through ComputeMassBounds with a hand-picked unitMass instead, since RimTest Redux runs before
// any game/map is loaded and Thing.GetStatValue needs Find.TickManager to resolve.
[TestSuite]
internal static class GetMassBoundsTests
{
    // TransferableUtility.TradeableMatching and TradeDeal's own construction dereference
    // TradeSession.trader - see FakeTrader.
    [BeforeEach]
    public static void SetUp()
    {
        TradeSession.trader = new FakeTrader();
        // TradeDeal's constructor synthesizes a placeholder currency Tradeable via
        // ThingMaker.MakeThing when not in gift mode, which needs Find.UniqueIDsManager -
        // unavailable outside a loaded game/map. Building under gift mode skips that call
        // entirely; this logic never reads giftMode, so it has no bearing on what's under test.
        TradeSession.giftMode = true;
        TradeSession.deal = new TradeDeal();
        TradeSession.giftMode = false;
        TradeSession.deal.AllTradeables.Clear();
    }

    [AfterEach]
    public static void TearDown()
    {
        TradeSession.trader = null;
        TradeSession.deal = null;
        TradeShipData.tradeAgreementForQuest = null;
    }

    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
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
    public static void ReturnsNullBoundsWhenNoTradeAgreementForQuest()
    {
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, -10);

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.GetMassBounds(trad);

        Assert.That(massMin.HasValue).Is.False();
        Assert.That(massMax.HasValue).Is.False();
    }

    [Test]
    public static void ReturnsNullBoundsWhenTradeableHasNoThing()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.GetMassBounds(new Tradeable());

        Assert.That(massMin.HasValue).Is.False();
        Assert.That(massMax.HasValue).Is.False();
    }

    [Test]
    public static void ZeroOrNegativeUnitMassReturnsNullBounds()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, -10);

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.ComputeMassBounds(
            trad,
            unitMass: 0f
        );

        Assert.That(massMin.HasValue).Is.False();
        Assert.That(massMax.HasValue).Is.False();
    }

    [Test]
    public static void SellingRowGetsAMassMinBoundedByColonyStock()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, -10);

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.ComputeMassBounds(
            trad,
            unitMass: 1f
        );

        // 10kg is nowhere near the shuttle's mass capacity, so the bound is just the full colony
        // stock, negated.
        Assert.That(massMin!.Value).Is.EqualTo(-10);
        Assert.That(massMax.HasValue).Is.False();
    }

    [Test]
    public static void BuyingRowGetsAMassMaxBoundedByTraderStock()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, 15);

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.ComputeMassBounds(
            trad,
            unitMass: 1f
        );

        Assert.That(massMax!.Value).Is.EqualTo(15);
        Assert.That(massMin.HasValue).Is.False();
    }

    [Test]
    public static void DropPodDeliveryLeavesMassMaxUnconstrained()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement { deliverItemsViaDropPod = true };
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, 15);

        var (_, massMax) = Rimworld_TradeUI_DrawTradeableRow.ComputeMassBounds(trad, unitMass: 1f);

        Assert.That(massMax.HasValue).Is.False();
    }

    [Test]
    public static void UndecidedRowWithStockOnBothSidesGetsBothBounds()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();
        // ForceTo(0) puts the row in the "undecided" state, where it isn't committed to selling or
        // buying - a row that's actively selling never gets constrained by the (unrelated) incoming
        // shuttle's remaining capacity, and vice versa, but an undecided one can go either way.
        var trad = NewTradeable(RimWorld.ThingDefOf.Steel, 0);
        trad.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 5), Transactor.Trader);

        var (massMin, massMax) = Rimworld_TradeUI_DrawTradeableRow.ComputeMassBounds(
            trad,
            unitMass: 1f
        );

        Assert.That(massMin!.Value).Is.EqualTo(-1);
        Assert.That(massMax!.Value).Is.EqualTo(5);
    }
}
