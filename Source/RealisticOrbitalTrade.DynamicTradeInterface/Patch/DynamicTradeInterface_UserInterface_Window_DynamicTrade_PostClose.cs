using DynamicTradeInterface.UserInterface;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.PostClose));

    internal static void Postfix() => TradeShipData.EndTradeAgreementIfExists();
}
