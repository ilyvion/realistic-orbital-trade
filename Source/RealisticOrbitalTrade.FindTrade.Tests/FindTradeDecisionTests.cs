using DevTools.Testing;
using RealisticOrbitalTrade.FindTrade.Patch;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.FindTrade.Tests;

// The decisions the Find / Trade patches make about a barter deal, without a game or ticking.
// Needs Find / Trade active; see TestPlans/FindTrade.xml.
[TestFixture(TestType.MainMenu)]
[LoadIfModsActive("memegoddess.findandtrade")]
internal sealed class FindTradeDecisionTests
{
    private const float Capacity = 10f;
    private const int Threshold = 600;

    private static bool IsTooHeavy(
        float[] colony,
        float[]? trader = null,
        bool isCurrency = false,
        bool boughtByShuttle = true
    ) =>
        FindTrade_TradeDealProcessor_DoTradeDeal.IsTooHeavyForShuttle(
            isCurrency,
            colony,
            trader ?? [],
            Capacity,
            boughtByShuttle
        );

    [Test]
    public static void CurrencyIsNeverTooHeavy() =>
        Expect.IsFalse(IsTooHeavy([float.MaxValue], [float.MaxValue], isCurrency: true));

    [Test]
    public static void ColonyUnitMassAtCapacityIsKept() => Expect.IsFalse(IsTooHeavy([Capacity]));

    [Test]
    public static void ColonyUnitMassAboveCapacityIsRemoved() =>
        Expect.IsTrue(IsTooHeavy([Capacity + 0.01f]));

    [Test]
    public static void ColonyUnitMassAboveCapacityIsRemovedWhenBoughtByDropPod() =>
        Expect.IsTrue(IsTooHeavy([Capacity + 0.01f], boughtByShuttle: false));

    [Test]
    public static void TraderUnitMassAtCapacityIsKept() =>
        Expect.IsFalse(IsTooHeavy([], [Capacity]));

    [Test]
    public static void TraderUnitMassAboveCapacityIsRemovedWhenBoughtByShuttle() =>
        Expect.IsTrue(IsTooHeavy([], [Capacity + 0.01f]));

    [Test]
    public static void TraderUnitMassAboveCapacityIsKeptWhenBoughtByDropPod() =>
        Expect.IsFalse(IsTooHeavy([], [Capacity + 0.01f], boughtByShuttle: false));

    [Test]
    public static void OneHeavyColonyThingAmongLightOnesIsRemoved() =>
        Expect.IsTrue(IsTooHeavy([1f, 2f, Capacity + 1f, 3f]));

    [Test]
    public static void OneHeavyTraderThingAmongLightOnesIsRemoved() =>
        Expect.IsTrue(IsTooHeavy([1f], [1f, Capacity + 1f, 2f]));

    [Test]
    public static void LightThingsAreKept() => Expect.IsFalse(IsTooHeavy([1f, Capacity], [2f]));

    [Test]
    public static void NoThingsAreKept() => Expect.IsFalse(IsTooHeavy([], []));

    private static bool IsBelowThreshold(float combinedTradeValue, int threshold = Threshold) =>
        FindTrade_TradeDealProcessor_DoTradeDeal.IsBelowMinimumTradeThreshold(
            combinedTradeValue,
            threshold
        );

    [Test]
    public static void EmptyDealIsNotBelowThreshold() => Expect.IsFalse(IsBelowThreshold(0f));

    [Test]
    public static void ValueJustBelowThresholdIsBelowThreshold() =>
        Expect.IsTrue(IsBelowThreshold(Threshold - 0.01f));

    [Test]
    public static void ValueAtThresholdIsNotBelowThreshold() =>
        Expect.IsFalse(IsBelowThreshold(Threshold));

    [Test]
    public static void ValueAboveThresholdIsNotBelowThreshold() =>
        Expect.IsFalse(IsBelowThreshold(Threshold + 0.01f));

    [Test]
    public static void NoValueIsBelowZeroThreshold() =>
        Expect.IsFalse(IsBelowThreshold(0.01f, threshold: 0));

    [Test]
    public static void EveryCommsOutcomeHidesTheShipAsExpected()
    {
        var expected = new Dictionary<
            Rimworld_TradeShip_TryOpenComms.CommsOutcome,
            FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility
        >
        {
            [Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement] =
                FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility.Trade,
            [Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForGraceTime] =
                FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility.HideForStay,
            [Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForActiveAmendment] =
                FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility.HideForThisPass,
            [Rimworld_TradeShip_TryOpenComms.CommsOutcome.Blacklisted] =
                FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility.HideForThisPass,
            [Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForActiveTrade] =
                FindTrade_TradeDealProcessor_DoTradeShips.ShipVisibility.HideForThisPass,
        };

        foreach (var outcome in Enum.GetValues<Rimworld_TradeShip_TryOpenComms.CommsOutcome>())
        {
            Expect.IsTrue(expected.TryGetValue(outcome, out var visibility), $"{outcome} mapped");
            Expect.AreEqual(
                visibility,
                FindTrade_TradeDealProcessor_DoTradeShips.ClassifyShip(outcome),
                $"{outcome}"
            );
        }
    }
}
