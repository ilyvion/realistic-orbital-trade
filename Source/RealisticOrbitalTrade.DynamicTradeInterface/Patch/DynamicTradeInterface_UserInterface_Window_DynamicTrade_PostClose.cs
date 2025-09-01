namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method("DynamicTradeInterface.UserInterface.Window_DynamicTrade:PostClose");

    internal static void Postfix() => TradeShipData.EndTradeAgreementIfExists();
}
