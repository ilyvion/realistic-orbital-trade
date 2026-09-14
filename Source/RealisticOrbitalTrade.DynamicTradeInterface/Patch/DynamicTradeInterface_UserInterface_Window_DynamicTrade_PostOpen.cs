using DynamicTradeInterface.UserInterface;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// Window_DynamicTrade doesn't override PostOpen itself, so this actually targets the base Window
// method and must guard on __instance to stay scoped to DTI's trade window - mirrors
// Rimworld_Dialog_Trade_Close/DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose,
// which do the same for Close/PostClose.
[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostOpen
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.PostOpen));

    // Mirrors Rimworld_Dialog_Trade_PostOpen, which does the same right after vanilla's own trade
    // dialog opens - DTI's trade window replaces Dialog_Trade entirely, so that patch never runs
    // while DTI is in use.
    internal static void Postfix(Window __instance)
    {
        if (__instance is not Window_DynamicTrade)
        {
            return;
        }

        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement?.tradeDeliveryMethod == null)
        {
            return;
        }

        Utils.ExplainLostSoldItemsIfNeeded(tradeAgreement, TradeSession.deal);
    }
}
