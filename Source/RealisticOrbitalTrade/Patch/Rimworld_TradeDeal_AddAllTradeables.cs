namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Seeds a reopened trade dialog's live <see cref="TradeDeal"/> with the current state of an
/// in-progress trade agreement during renegotiation, on top of whatever vanilla's own
/// <c>AddAllTradeables</c> already found sellable/buyable. A no-op for a brand-new trade, since
/// <see cref="TradeAgreement.tradeDeliveryMethod"/> stays <see langword="null"/> until a deal has
/// already gone through <see cref="Quests.QuestNode_Root_TradeShipTransportShip"/> - exactly when
/// renegotiation becomes possible.
/// </summary>
[HarmonyPatch(typeof(TradeDeal), "AddAllTradeables")]
internal static class Rimworld_TradeDeal_AddAllTradeables
{
    internal static void Postfix(TradeDeal __instance)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement?.tradeDeliveryMethod == null)
        {
            return;
        }

        // TradeSession.trader is only guaranteed assigned once this postfix runs (it's set up as
        // part of the same TradeSession.SetupWith call that resets the deal and triggers
        // AddAllTradeables), so this is the earliest point AllTradeables can safely be computed -
        // see Utils.RenegotiationOriginalTradeables.
        if (
            Utils.RenegotiatingAgreement == tradeAgreement
            && Utils.RenegotiationOriginalTradeables == null
        )
        {
            Utils.RenegotiationOriginalTradeables = tradeAgreement.AllTradeables;
        }

        tradeAgreement.MergeAgreedTradeablesInto(__instance.AllTradeables);
    }
}
