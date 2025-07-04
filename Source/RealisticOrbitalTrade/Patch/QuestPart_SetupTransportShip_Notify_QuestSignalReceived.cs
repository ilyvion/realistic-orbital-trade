// This bug we're correcting is fixed in 1.5, so we only patch for 1.4
#if v1_4
using RimWorld;
using RimWorld.QuestGen;
using Verse;

[HarmonyPatch(typeof(QuestPart_SetupTransportShip), nameof(QuestPart_SetupTransportShip.Notify_QuestSignalReceived))]
internal static class QuestPart_SetupTransportShip_Notify_QuestSignalReceived
{
    private static void Postfix(QuestPart_SetupTransportShip __instance, Signal signal)
    {
        if (!(signal.tag == __instance.inSignal))
        {
            return;
        }
        if (!__instance.pawns.NullOrEmpty())
        {
            __instance.pawns.Clear();
        }
        if (!__instance.items.NullOrEmpty())
        {
            __instance.items.Clear();
        }
    }
}
#endif
