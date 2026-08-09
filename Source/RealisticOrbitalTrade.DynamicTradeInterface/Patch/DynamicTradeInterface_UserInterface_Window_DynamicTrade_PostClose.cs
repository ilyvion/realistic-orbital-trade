using DynamicTradeInterface.UserInterface;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.PostClose));

    internal static void Postfix()
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
