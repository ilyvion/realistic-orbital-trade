using DevTools.Testing;
using RealisticOrbitalTrade.FindTrade.Patch;

namespace RealisticOrbitalTrade.FindTrade.Tests;

// The decisions the Find / Trade patches make about a barter deal, without a game or ticking.
// Needs Find / Trade active; see TestPlans/FindTrade.xml.
[TestFixture(TestType.MainMenu)]
[LoadIfModsActive("memegoddess.findandtrade")]
internal sealed class FindTradeDecisionTests
{
    private const float Capacity = 10f;

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
}
