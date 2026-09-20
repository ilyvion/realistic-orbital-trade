using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay:
// the trade-value-difference/color/flash selection factored out of DrawTradeValueThresholdRow and
// its DynamicTradeInterface mirror so both UIs stay in sync instead of copy-pasting this
// logic.
[TestFixture(TestType.MainMenu)]
internal sealed class TradeValueThresholdRowDisplayTests
{
    [TearDown]
    public static void ResetFlashTime() =>
        Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime = -100f;

    [Test]
    public static void DifferenceIsRoundedCombinedValueMinusThreshold()
    {
        var (tradeValueDifference, _, _) =
            Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay(
                combinedTradeValue: 149.6f,
                minimumTradeThreshold: 100
            );

        Expect.AreEqual(50, tradeValueDifference);
    }

    [Test]
    public static void MeetingTheThresholdIsGreen()
    {
        var (_, valueColor, _) =
            Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay(
                combinedTradeValue: 100f,
                minimumTradeThreshold: 100
            );

        Expect.IsTrue(valueColor == Color.green);
    }

    [Test]
    public static void BelowTheThresholdIsYellow()
    {
        var (_, valueColor, _) =
            Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay(
                combinedTradeValue: 99f,
                minimumTradeThreshold: 100
            );

        Expect.IsTrue(valueColor == Color.yellow);
    }

    [Test]
    public static void FlashReflectsTheSharedTradeValueFlashTimeField()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime = Time.time;

        var (_, _, flash) = Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay(
            combinedTradeValue: 100f,
            minimumTradeThreshold: 100
        );

        Expect.IsTrue(flash);
    }

    [Test]
    public static void FlashExpiresAfterOneSecond()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime = Time.time - 10f;

        var (_, _, flash) = Rimworld_Dialog_Trade_DoWindowContents.GetTradeValueThresholdRowDisplay(
            combinedTradeValue: 100f,
            minimumTradeThreshold: 100
        );

        Expect.IsFalse(flash);
    }
}
