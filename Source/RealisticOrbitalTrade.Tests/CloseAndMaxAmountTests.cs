using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Rimworld_TradeUI_DrawTradeableRow.ClampMaxAmount is what DTI's "<<"/">>" buttons use to stop at
// the mass limit; Rimworld_Dialog_Trade_Close.ApplyClose is the trade-state effect shared by the
// vanilla and DTI trade windows when they close.
[TestFixture(TestType.MainMenu)]
internal sealed class CloseAndMaxAmountTests
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

    [Test]
    public static void BlockAndConfirmChangesNothing()
    {
        var agreement = (TradeAgreement)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(TradeAgreement)
            );
        TradeShipData.tradeAgreementForQuest = agreement;
        try
        {
            Rimworld_Dialog_Trade_Close.ApplyClose(
                Rimworld_Dialog_Trade_Close.CloseAction.BlockAndConfirm
            );

            Expect.IsTrue(ReferenceEquals(agreement, TradeShipData.tradeAgreementForQuest));
        }
        finally
        {
            TradeShipData.tradeAgreementForQuest = null;
        }
    }

    [Test]
    public static void NoOpDropsRenegotiationAndTheSessionAgreement()
    {
        var agreement = (TradeAgreement)
            System.Runtime.CompilerServices.RuntimeHelpers.GetUninitializedObject(
                typeof(TradeAgreement)
            );
        TradeShipData.tradeAgreementForQuest = agreement;
        try
        {
            Rimworld_Dialog_Trade_Close.ApplyClose(Rimworld_Dialog_Trade_Close.CloseAction.NoOp);

            Expect.IsNull(TradeShipData.tradeAgreementForQuest);
            Expect.IsNull(Utils.RenegotiatingAgreement);
        }
        finally
        {
            TradeShipData.tradeAgreementForQuest = null;
        }
    }
}
