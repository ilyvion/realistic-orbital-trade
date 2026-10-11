using DevTools.Testing;
using RealisticOrbitalTrade.Core;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for renegotiation alongside a mod (GTGTraderCore) that patches
// TransferableUtility.TradeableMatching to return tradeables it cached from earlier calls, which
// need not be in the list being searched. The deal and the original agreement are separate lists
// built in the same trade session, so matching through that method mixed them up: renegotiating
// an untouched deal took sold goods off the sale and tried to sell more than the colony had.
// Each test patches TradeableMatching to always return a tradeable from no list at all.
[TestFixture(TestType.MainMenu)]
internal sealed class MatchingTradeableTests
{
    private const string HarmonyId = "RealisticOrbitalTrade.Tests.MatchingTradeable";

    private static readonly Harmony _harmony = new(HarmonyId);

    private static Tradeable? _foreignTradeable;

    private static int _nextThingId;

    [SetUp]
    public static void SetUp()
    {
        // TradeAgreement.MatchingTradeable dereferences TradeSession.trader - see FakeTrader.
        TradeSession.trader = new FakeTrader();

        _foreignTradeable = new Tradeable();
        _foreignTradeable.AddThing(NewThing(RimWorld.ThingDefOf.Steel, 1), Transactor.Colony);
        _ = _harmony.Patch(
            AccessTools.Method(
                typeof(TransferableUtility),
                nameof(TransferableUtility.TradeableMatching)
            ),
            prefix: new HarmonyMethod(
                typeof(MatchingTradeableTests),
                nameof(ReturnForeignTradeable)
            )
        );
    }

    [TearDown]
    public static void TearDown()
    {
        _harmony.UnpatchAll(HarmonyId);
        _foreignTradeable = null;
        TradeSession.trader = null;
    }

    private static bool ReturnForeignTradeable(ref Tradeable? __result)
    {
        __result = _foreignTradeable;
        return false;
    }

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

    private static Tradeable NewTradeable(ThingDef def, int countToTransfer)
    {
        Tradeable tradeable = new();
        tradeable.AddThing(NewThing(def, Math.Abs(countToTransfer)), Transactor.Colony);
        tradeable.ForceTo(countToTransfer);
        return tradeable;
    }

    [Test]
    public static void MatchingTradeableReturnsTheMatchingEntryOfTheList()
    {
        var steel = NewTradeable(RimWorld.ThingDefOf.Steel, 0);
        var wood = NewTradeable(RimWorld.ThingDefOf.WoodLog, 0);

        var match = TradeAgreement.MatchingTradeable(
            NewThing(RimWorld.ThingDefOf.Steel, 5),
            [wood, steel]
        );

        Expect.IsTrue(ReferenceEquals(steel, match));
    }

    [Test]
    public static void MatchingTradeableIsNullWhenNoEntryOfTheListMatches()
    {
        var wood = NewTradeable(RimWorld.ThingDefOf.WoodLog, 0);

        var match = TradeAgreement.MatchingTradeable(
            NewThing(RimWorld.ThingDefOf.Steel, 5),
            [wood]
        );

        Expect.IsNull(match);
    }

    [Test]
    public static void MergeRecordsTheSaleOnTheListsOwnTradeable()
    {
        var tradeAgreement = NewTradeAgreement();
        var thing = NewThing(RimWorld.ThingDefOf.Steel, 10);
        tradeAgreement.thingsSoldToTrader.Add(new ThingCountClass(thing, 10));
        var existing = NewTradeable(RimWorld.ThingDefOf.Steel, 0);
        List<Tradeable> tradeables = [existing];

        tradeAgreement.MergeAgreedTradeablesInto(tradeables);

        Expect.AreEqual(1, tradeables.Count);
        Expect.AreEqual(-10, tradeables[0].CountToTransfer);
        Expect.AreEqual(0, _foreignTradeable!.CountToTransfer);
    }

    [Test]
    public static void RenegotiationOfAnUnchangedDealProducesNoChanges()
    {
        var tradeAgreement = NewTradeAgreement();
        tradeAgreement.thingsSoldToTrader.Add(
            new ThingCountClass(NewThing(RimWorld.ThingDefOf.Steel, 10), 10)
        );
        List<Tradeable> original = [];
        tradeAgreement.MergeAgreedTradeablesInto(original);
        List<Tradeable> deal = [];
        tradeAgreement.MergeAgreedTradeablesInto(deal);

        var changes = TradeAgreement.ComputeRenegotiationChanges(original, deal);

        Expect.IsEmpty(changes);
    }

    [Test]
    public static void SumMatchingStackCountsCountsThingsMatchingTheTradeable()
    {
        var tradeable = NewTradeable(RimWorld.ThingDefOf.Steel, 0);

        var result = TradeAgreement.SumMatchingStackCounts(
            tradeable,
            [NewThing(RimWorld.ThingDefOf.Steel, 20)]
        );

        Expect.AreEqual(20, result);
    }
}
