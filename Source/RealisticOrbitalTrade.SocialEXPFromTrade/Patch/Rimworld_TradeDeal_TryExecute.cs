namespace RealisticOrbitalTrade.SocialEXPFromTrade.Patch;

[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
internal static class Rimworld_TradeDeal_TryExecute
{
    /// <summary>
    /// Read by <see cref="SocialEXPFromTrade_Patch1_Postfix"/> once Social Experience from
    /// Trade's own postfix on this same method runs. Captured here, in a prefix, so it's set
    /// before every postfix on this method runs, including ROT's own postfix which nulls
    /// <see cref="TradeShipData.tradeAgreementForQuest"/> - a prefix always runs before all
    /// postfixes, so this needs no <c>[HarmonyBefore]</c>/<c>[HarmonyAfter]</c> coupling to
    /// ROT's own patch ordering.
    /// </summary>
    internal static TradeAgreement? PendingTradeAgreement;

    /// <summary>
    /// The deal being executed when it renegotiates <see cref="PendingTradeAgreement"/>, otherwise
    /// <see langword="null"/>. Captured here for the same reason, since ROT's postfix gives a new
    /// trade's agreement the <see cref="TradeAgreement.tradeDeliveryMethod"/> that marks a
    /// renegotiation.
    /// </summary>
    internal static TradeDeal? PendingRenegotiatedDeal;

    private static void Prefix(TradeDeal __instance)
    {
        PendingTradeAgreement = TradeShipData.tradeAgreementForQuest;
        PendingRenegotiatedDeal =
            PendingTradeAgreement?.tradeDeliveryMethod != null ? __instance : null;
    }
}
