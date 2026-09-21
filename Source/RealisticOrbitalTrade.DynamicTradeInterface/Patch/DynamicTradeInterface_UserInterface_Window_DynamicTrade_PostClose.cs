using DynamicTradeInterface.UserInterface;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// Window_DynamicTrade doesn't override OnCloseRequest/PostClose itself, so both patches below
// actually target the base Window method and must guard on __instance to stay scoped to DTI's
// trade window - mirrors Rimworld_Dialog_Trade_Close, which patches Dialog_Trade.Close directly
// instead, since vanilla's trade dialog does override that one.
[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_OnCloseRequest
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.OnCloseRequest));

    // See Rimworld_Dialog_Trade_Close.
    internal static bool Prefix(Window __instance, ref bool __result)
    {
        if (
            __instance is not Window_DynamicTrade
            || Rimworld_Dialog_Trade_Close.CurrentAction()
                != Rimworld_Dialog_Trade_Close.CloseAction.BlockAndConfirm
        )
        {
            return true;
        }

        Rimworld_Dialog_Trade_Close.ShowCancelConfirmation(__instance);
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.PostClose));

    internal static void Postfix(Window __instance)
    {
        if (__instance is not Window_DynamicTrade)
        {
            return;
        }

        Rimworld_Dialog_Trade_Close.ApplyClose(Rimworld_Dialog_Trade_Close.CurrentAction());
    }
}
