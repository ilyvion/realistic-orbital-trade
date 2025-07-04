// private bool RWAutoSell.AI.JobDriver_AutoSell.FailToil()
using System.Reflection;

namespace RealisticOrbitalTrade.AutoSeller.Patch;

[HarmonyPatch]
internal static class AutoSeller_JobDriver_AutoSell_FailToil
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method("RWAutoSell.AI.JobDriver_AutoSell:FailToil");
    }

    private static void Postfix(ref bool __result)
    {
        // Don't trade if we're blacklisted
        if (RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Blacklisted)
        {
            RealisticOrbitalTradeMod.Dev(() => "Failing auto-sell pawn job because player is blacklisted");
            __result = true;
        }
    }
}
