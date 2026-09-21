using DevTools.Testing;
using RealisticOrbitalTrade.Core;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// The mass, shuttle-plan and value figures the trade dialog and the accept gates read off a
// TradeDeal. Runs in a loaded game because Thing.GetStatValue needs one.
[TestFixture(TestType.Playing)]
internal sealed class TradeDealMassAndValueTests
{
    private static ITrader? _previousTrader;
    private static TradeAgreement? _previousAgreement;

    [SetUp]
    public static void SetUp()
    {
        _previousTrader = TradeSession.trader;
        _previousAgreement = TradeShipData.tradeAgreementForQuest;
        TradeSession.trader = new FakeTrader();
        TradeShipData.tradeAgreementForQuest = null;
    }

    [TearDown]
    public static void TearDown()
    {
        TradeSession.trader = _previousTrader;
        TradeShipData.tradeAgreementForQuest = _previousAgreement;
    }

    private static TradeDeal NewDeal()
    {
        var wasGiftMode = TradeSession.giftMode;
        TradeSession.giftMode = true;
        var deal = new TradeDeal();
        TradeSession.giftMode = wasGiftMode;
        deal.AllTradeables.Clear();
        return deal;
    }

    private static Thing NewThing(ThingDef def, int stackCount)
    {
        var thing = ThingMaker.MakeThing(def);
        thing.stackCount = stackCount;
        return thing;
    }

    private static FakeTradeable NewTradeable(
        Transactor side,
        int countToTransfer,
        params Thing[] things
    )
    {
        FakeTradeable tradeable = new() { BuyPrice = 2f, SellPrice = 1f };
        foreach (var thing in things)
        {
            tradeable.AddThing(thing, side);
        }
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    private static float UnitMass(ThingDef def) => def.GetStatValueAbstract(StatDefOf.Mass);

    private static void ExpectMass(float expected, float actual) =>
        Expect.IsTrue(
            Math.Abs(expected - actual) < 0.001f,
            $"Expected mass {expected}, got {actual}"
        );

    [Test]
    public static void OutgoingMassSpansMultipleColonyStacks()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(
            NewTradeable(Transactor.Colony, -30, NewThing(steel, 20), NewThing(steel, 20))
        );

        ExpectMass(
            30 * UnitMass(steel),
            Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal)
        );
    }

    [Test]
    public static void OutgoingMassIgnoresBoughtAndUntouchedRows()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Trader, 10, NewThing(steel, 20)));
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, 0, NewThing(steel, 20)));

        ExpectMass(0f, Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal));
    }

    [Test]
    public static void OutgoingMassOverrideReplacesTheRowsCount()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        var row = NewTradeable(Transactor.Colony, -30, NewThing(steel, 50));
        deal.AllTradeables.Add(row);

        ExpectMass(
            5 * UnitMass(steel),
            Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal, row, -5)
        );
        ExpectMass(0f, Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal, row, 0));
    }

    [Test]
    public static void OutgoingMassOverrideCanTurnAnUntouchedRowIntoASale()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        var row = NewTradeable(Transactor.Colony, 0, NewThing(steel, 50));
        deal.AllTradeables.Add(row);

        ExpectMass(
            7 * UnitMass(steel),
            Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal, row, -7)
        );
    }

    [Test]
    public static void IncomingMassSpansMultipleTraderStacks()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(
            NewTradeable(Transactor.Trader, 30, NewThing(steel, 20), NewThing(steel, 20))
        );

        ExpectMass(
            30 * UnitMass(steel),
            Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(deal)
        );
    }

    [Test]
    public static void IncomingMassIgnoresSoldRows()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, -10, NewThing(steel, 20)));

        ExpectMass(0f, Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(deal));
    }

    [Test]
    public static void IncomingMassOverrideReplacesTheRowsCount()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        var row = NewTradeable(Transactor.Trader, 30, NewThing(steel, 50));
        deal.AllTradeables.Add(row);

        ExpectMass(
            4 * UnitMass(steel),
            Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(deal, row, 4)
        );
    }

    private static void ExpectOneShuttleWithNoCargo(List<List<(int lineId, int count)>> plan)
    {
        Expect.AreEqual(1, plan.Count);
        Expect.IsEmpty(plan[0]);
    }

    [Test]
    public static void EmptyDealPlansOneShuttleWithNoCargo()
    {
        var plan = Rimworld_Dialog_Trade_DoWindowContents.PlanOutgoingShuttles(NewDeal());

        Expect.IsNotNull(plan);
        ExpectOneShuttleWithNoCargo(plan!);
    }

    [Test]
    public static void SaleWithinOneShuttleIsPlannedAsOneShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, -10, NewThing(steel, 20)));

        Expect.AreEqual(
            1,
            Rimworld_Dialog_Trade_DoWindowContents.PlanOutgoingShuttles(deal)!.Count
        );
    }

    [Test]
    public static void SaleHeavierThanAShuttleIsPlannedAcrossSeveral()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var capacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        var count = (int)(capacity / UnitMass(steel) * 2.5f);
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, -count, NewThing(steel, count)));

        Expect.AreEqual(
            3,
            Rimworld_Dialog_Trade_DoWindowContents.PlanOutgoingShuttles(deal)!.Count
        );
    }

    [Test]
    public static void SoldRowsWithNothingToSellAreLeftOutOfThePlan()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Trader, 10, NewThing(steel, 20)));

        ExpectOneShuttleWithNoCargo(
            Rimworld_Dialog_Trade_DoWindowContents.PlanOutgoingShuttles(deal)!
        );
    }

    [Test]
    public static void ShuttleLimitIsUnsetWithoutAnAgreement()
    {
        Expect.IsFalse(Rimworld_Dialog_Trade_DoWindowContents.OutgoingShuttleLimit.HasValue);
        Expect.AreEqual(
            float.PositiveInfinity,
            Rimworld_Dialog_Trade_DoWindowContents.OutgoingMassCapacity
        );
    }

    [Test]
    public static void ShuttleLimitIsUnsetForANewTrade()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement();

        Expect.IsFalse(Rimworld_Dialog_Trade_DoWindowContents.OutgoingShuttleLimit.HasValue);
    }

    [Test]
    public static void ShuttleLimitForARenegotiationIsTheShuttlesAlreadySent()
    {
        TradeShipData.tradeAgreementForQuest = new TradeAgreement
        {
            tradeDeliveryMethod = new TradeDeliveryMethod_TransportShip(null!),
        };

        Expect.AreEqual(0, Rimworld_Dialog_Trade_DoWindowContents.OutgoingShuttleLimit);
        Expect.AreEqual(0f, Rimworld_Dialog_Trade_DoWindowContents.OutgoingMassCapacity);
    }

    [Test]
    public static void CombinedValueCountsSalesAndPurchasesAsAbsoluteValues()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var deal = NewDeal();
        // Bought at BuyPrice 2, sold at SellPrice 1.
        deal.AllTradeables.Add(NewTradeable(Transactor.Trader, 10, NewThing(steel, 20)));
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, -30, NewThing(steel, 50)));

        Expect.AreEqual(
            50f,
            Rimworld_Dialog_Trade_DoWindowContents.CalculateCombinedTradeValue(deal)
        );
    }

    [Test]
    public static void CombinedValueSkipsCurrencyAndUntouchedRows()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var silver = RimWorld.ThingDefOf.Silver;
        var deal = NewDeal();
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, -30, NewThing(steel, 50)));
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, 0, NewThing(steel, 50)));
        deal.AllTradeables.Add(NewTradeable(Transactor.Colony, 500, NewThing(silver, 500)));

        Expect.AreEqual(
            30f,
            Rimworld_Dialog_Trade_DoWindowContents.CalculateCombinedTradeValue(deal)
        );
    }
}
