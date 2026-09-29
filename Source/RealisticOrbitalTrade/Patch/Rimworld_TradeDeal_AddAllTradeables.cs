namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Seeds a reopened trade dialog's live <see cref="TradeDeal"/> with the current state of an
/// in-progress trade agreement during renegotiation, on top of whatever vanilla's own
/// <c>AddAllTradeables</c> already found sellable/buyable. The merge is a no-op for a brand-new
/// trade, since <see cref="TradeAgreement.tradeDeliveryMethod"/> stays <see langword="null"/> until
/// a deal has already gone through <see cref="Quests.QuestNode_Root_TradeShipTransportShip"/> -
/// exactly when renegotiation becomes possible. Either way, colony stock committed to any other
/// active trade agreement is then taken out of the deal, so it can't be sold twice.
/// </summary>
[HarmonyPatch(typeof(TradeDeal), "AddAllTradeables")]
internal static class Rimworld_TradeDeal_AddAllTradeables
{
    internal static void Postfix(TradeDeal __instance)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement?.tradeDeliveryMethod != null)
        {
            // TradeSession.trader is only guaranteed assigned once this postfix runs (it's set up
            // as part of the same TradeSession.SetupWith call that resets the deal and triggers
            // AddAllTradeables), so this is the earliest point AllTradeables can safely be
            // computed - see Utils.RenegotiationOriginalTradeables.
            if (
                Utils.RenegotiatingAgreement == tradeAgreement
                && Utils.RenegotiationOriginalTradeables == null
            )
            {
                Utils.RenegotiationOriginalTradeables = tradeAgreement.AllTradeables;
            }

            tradeAgreement.MergeAgreedTradeablesInto(__instance.AllTradeables);
        }

        Tradeable_Withheld.WithholdCommittedStock(
            __instance.AllTradeables,
            tradeAgreement?.tradeShip == TradeSession.trader ? tradeAgreement : null
        );
    }
}
