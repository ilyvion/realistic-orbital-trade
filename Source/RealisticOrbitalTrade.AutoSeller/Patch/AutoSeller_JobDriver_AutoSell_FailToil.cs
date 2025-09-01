namespace RealisticOrbitalTrade.AutoSeller.Patch;

[HarmonyPatch]
internal static class AutoSeller_JobDriver_AutoSell_FailToil
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method("RWAutoSell.AI.JobDriver_AutoSell:FailToil");

    internal static void Postfix(ref bool __result)
    {
        // Don't trade if we're blacklisted
        if (RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Blacklisted)
        {
            RealisticOrbitalTradeMod.Dev(() =>
                "Failing auto-sell pawn job because player is blacklisted"
            );
            __result = true;
        }
    }
}
