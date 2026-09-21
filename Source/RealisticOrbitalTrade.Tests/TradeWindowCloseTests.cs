using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Rimworld_Dialog_Trade_Close.ApplyClose is the trade-state effect shared by the vanilla and DTI
// trade windows when they close.
[TestFixture(TestType.Playing)]
internal sealed class TradeWindowCloseTests
{
    [Test]
    public static void BlockAndConfirmChangesNothing()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var agreement = new TradeAgreement(scene.TradeShip, scene.SpawnPawn(), false, false);
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
        using var scene = new TradeScene(Find.CurrentMap);
        var agreement = new TradeAgreement(scene.TradeShip, scene.SpawnPawn(), false, false);
        TradeShipData.tradeAgreementForQuest = agreement;
        Utils.BeginRenegotiation(agreement);
        try
        {
            Rimworld_Dialog_Trade_Close.ApplyClose(Rimworld_Dialog_Trade_Close.CloseAction.NoOp);

            Expect.IsNull(TradeShipData.tradeAgreementForQuest);
            Expect.IsNull(Utils.RenegotiatingAgreement);
        }
        finally
        {
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();
        }
    }
}
