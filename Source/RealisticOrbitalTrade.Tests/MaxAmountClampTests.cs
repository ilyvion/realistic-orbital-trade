using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount is what DTI's "<<"/">>" buttons use to stop at
// the mass limit.
[TestFixture(TestType.MainMenu)]
internal sealed class MaxAmountClampTests
{
    [Test]
    public static void BuyIsCappedAtMassMax() =>
        Expect.AreEqual(
            7,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(20, TradeAction.PlayerBuys, -3, 7)
        );

    [Test]
    public static void BuyBelowMassMaxIsKept() =>
        Expect.AreEqual(
            5,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(5, TradeAction.PlayerBuys, -3, 7)
        );

    [Test]
    public static void SellIsCappedAtNegatedMassMin() =>
        Expect.AreEqual(
            3,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(20, TradeAction.PlayerSells, -3, 7)
        );

    [Test]
    public static void SellBelowNegatedMassMinIsKept() =>
        Expect.AreEqual(
            2,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(2, TradeAction.PlayerSells, -3, 7)
        );

    [Test]
    public static void NullBoundsLeaveResult()
    {
        Expect.AreEqual(
            20,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(20, TradeAction.PlayerBuys, -3, null)
        );
        Expect.AreEqual(
            20,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(20, TradeAction.PlayerSells, null, 7)
        );
    }

    [Test]
    public static void OtherActionsAreUnchanged() =>
        Expect.AreEqual(
            20,
            Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount(20, TradeAction.None, -3, 7)
        );
}
