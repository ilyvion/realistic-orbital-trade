namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.Close))]
internal static class Rimworld_Dialog_Trade_Close
{
    private static void Postfix()
    {
        // Closing without accepting is a no-op for a renegotiation - unlike a fresh trade, the
        // agreement is already a real, in-progress trade (possibly with cargo already loaded), not
        // something safe to discard.
        if (Utils.RenegotiatingAgreement != null)
        {
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();
            return;
        }

        TradeShipData.EndTradeAgreementIfExists();
    }
}
