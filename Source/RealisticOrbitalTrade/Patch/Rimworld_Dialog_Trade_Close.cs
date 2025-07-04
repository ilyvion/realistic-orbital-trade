
namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.Close))]
internal static class Rimworld_Dialog_Trade_Close
{
    private static void Postfix()
    {
        TradeShipData.EndTradeAgreementIfExists();
    }
}
