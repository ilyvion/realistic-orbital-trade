using System.Reflection;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method(
            "DynamicTradeInterface.UserInterface.Window_DynamicTrade:PostClose"
        );
    }

    private static void Postfix()
    {
        TradeShipData.EndTradeAgreementIfExists();
    }
}
