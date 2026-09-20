using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class BinarySearchMaxFeasibleTests
{
    [Test]
    public static void ReturnsZeroWhenEvenZeroIsOverCapacity()
    {
        var result = Rimworld_TradeUI_DrawTradeableRow.BinarySearchMaxFeasible(
            _ => 100f,
            massCapacity: 50f,
            trueMax: 10
        );

        Expect.AreEqual(0, result);
    }

    [Test]
    public static void ReturnsTrueMaxWhenItFitsWithinCapacity()
    {
        var result = Rimworld_TradeUI_DrawTradeableRow.BinarySearchMaxFeasible(
            x => x * 2f,
            massCapacity: 100f,
            trueMax: 10
        );

        Expect.AreEqual(10, result);
    }

    [Test]
    public static void FindsExactBoundaryForMonotonicallyIncreasingMass()
    {
        // Each unit costs 3 mass, capacity 20 -> largest count with count*3 <= 20 is 6 (18 <= 20 < 21).
        var result = Rimworld_TradeUI_DrawTradeableRow.BinarySearchMaxFeasible(
            x => x * 3f,
            massCapacity: 20f,
            trueMax: 100
        );

        Expect.AreEqual(6, result);
    }

    [Test]
    public static void ReturnsZeroWhenTrueMaxIsZero()
    {
        var result = Rimworld_TradeUI_DrawTradeableRow.BinarySearchMaxFeasible(
            _ => 0f,
            massCapacity: 100f,
            trueMax: 0
        );

        Expect.AreEqual(0, result);
    }

    // Regression guard: mirrors the silver-rebalancing mass function, which is not strictly
    // monotonic (it can dip once the currency tradeable's contribution flips sign). The search
    // still must only ever return a feasible x, even though it isn't guaranteed to find the
    // largest one when massAt isn't monotonic.
    [Test]
    public static void NonMonotonicMassAtStillReturnsAFeasibleResult()
    {
        static float MassAt(int x)
        {
            return x == 5 ? 200f : x * 1f;
        }

        var result = Rimworld_TradeUI_DrawTradeableRow.BinarySearchMaxFeasible(
            MassAt,
            massCapacity: 50f,
            trueMax: 10
        );

        Expect.LessThanOrEqualTo(MassAt(result), 50f);
    }
}
