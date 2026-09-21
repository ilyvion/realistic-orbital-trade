using DevTools.Testing;
using RealisticOrbitalTrade.Dialogs;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SettingsAndIdTests
{
    [Test]
    public static void ZeroRollReturnsBaseline() =>
        Expect.AreEqual(250, Settings.ApplyDeviation(250, 0));

    [Test]
    public static void MinusHundredPercentGivesZero() =>
        Expect.AreEqual(0, Settings.ApplyDeviation(250, -100));

    [Test]
    public static void RollBelowMinusHundredClampsToZero() =>
        Expect.AreEqual(0, Settings.ApplyDeviation(250, -150));

    [Test]
    public static void DeviationTruncatesTowardZero()
    {
        Expect.AreEqual(332, Settings.ApplyDeviation(250, 33));
        Expect.AreEqual(168, Settings.ApplyDeviation(250, -33));
    }

    [Test]
    public static void ZeroBaselineStaysZero() =>
        Expect.AreEqual(0, Settings.ApplyDeviation(0, 40));

    [Test]
    public static void TradeIdsAdvanceByOne() =>
        Expect.AreEqual(6, RealisticOrbitalTradeGameComponent.AdvanceTradeId(5));

    [Test]
    public static void TradeIdWrapsToZeroBeforeMaxValue()
    {
        Expect.AreEqual(
            int.MaxValue - 1,
            RealisticOrbitalTradeGameComponent.AdvanceTradeId(int.MaxValue - 2)
        );
        Expect.AreEqual(0, RealisticOrbitalTradeGameComponent.AdvanceTradeId(int.MaxValue - 1));
    }

    [Test]
    public static void GetNextTradeIdHandsOutSequentialIds()
    {
        var component = new RealisticOrbitalTradeGameComponent(null!) { _nextTradeID = 10 };
        Expect.AreEqual(10, component.GetNextTradeId());
        Expect.AreEqual(11, component.GetNextTradeId());
        Expect.AreEqual(12, component._nextTradeID);
    }

    [Test]
    public static void PartsCostFloorsAtHalf() =>
        Expect.AreEqual(0.5f, Dialog_PayBlacklistRemovalFee.ComputePartsCost(0f, 1f, 0f, 0f));

    [Test]
    public static void PriceImprovementOfOneFloorsCost() =>
        Expect.AreEqual(0.5f, Dialog_PayBlacklistRemovalFee.ComputePartsCost(100f, 1f, 0f, 1f));

    [Test]
    public static void PartsCostRoundsOnlyAboveThreshold()
    {
        // 1.4 * 71 = 99.4 -> not rounded; 1.4 * 71.5 = 100.1 -> rounded to 100.
        Expect.IsTrue(
            Mathf.Abs(99.4f - Dialog_PayBlacklistRemovalFee.ComputePartsCost(71f, 1f, 0f, 0f))
                < 0.001f
        );
        Expect.AreEqual(100f, Dialog_PayBlacklistRemovalFee.ComputePartsCost(71.5f, 1f, 0f, 0f));
    }

    [Test]
    public static void PartsCostAppliesMultiplierLossAndImprovement()
    {
        // 10 * 1.4 * 2 * 1.5 * (1 - 0.5) = 21
        var cost = Dialog_PayBlacklistRemovalFee.ComputePartsCost(10f, 2f, 0.5f, 0.5f);
        Expect.IsTrue(Mathf.Abs(21f - cost) < 0.001f);
    }
}
