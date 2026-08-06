namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(Thing), nameof(Thing.Destroy))]
internal static class Rimworld_Thing_Destroy
{
    private static void Postfix(Thing __instance)
    {
        if (
            __instance == Rimworld_Thing_TryAbsorbStack.AbsorbedThing
            && Rimworld_Thing_TryAbsorbStack.AbsorbingThing is { } survivor
        )
        {
            TradeAgreement.Notify_ThingMerged(__instance, survivor);
            return;
        }

        TradeAgreement.Notify_ThingDestroyed(__instance);
    }
}
