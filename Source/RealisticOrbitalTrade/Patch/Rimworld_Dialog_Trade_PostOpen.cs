namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Explains a lost-sold-items silver adjustment right after a renegotiation's trade dialog opens,
/// the same way vanilla's own <see cref="Dialog_Trade.PostOpen"/> warns about an impaired
/// negotiator - added here rather than in <see cref="Rimworld_TradeDeal_AddAllTradeables"/> so the
/// message box lands on top of the trade dialog instead of underneath it, which is already added
/// to <see cref="WindowStack"/> by the time <c>PostOpen</c> runs. DTI's trade window replaces
/// <see cref="Dialog_Trade"/> entirely, so this never runs while it's in use - see
/// <c>DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostOpen</c> for its counterpart.
/// </summary>
[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.PostOpen))]
internal static class Rimworld_Dialog_Trade_PostOpen
{
    internal static void Postfix()
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement?.tradeDeliveryMethod == null)
        {
            return;
        }

        Utils.ExplainLostSoldItemsIfNeeded(tradeAgreement, TradeSession.deal);
    }
}
