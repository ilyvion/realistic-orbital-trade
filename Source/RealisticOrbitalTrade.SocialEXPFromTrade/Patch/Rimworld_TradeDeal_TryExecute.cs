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

    private static void Prefix() => PendingTradeAgreement = TradeShipData.tradeAgreementForQuest;
}
