namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Vanilla's <c>CompShuttle.CheckAutoload</c> rebuilds the transporter's <c>leftToLoad</c> from
/// scratch every autoload tick, clamping each entry's <c>CountToTransfer</c> to the matching
/// stock it can see: <c>AllSendableItems</c> plus <see
/// cref="TransporterUtility.ThingsBeingHauledTo"/>. The latter only recognizes pawns running
/// vanilla's own <see cref="JobDriver_HaulToTransporter"/>, so cargo already split off colony
/// stacks and riding with this mod's bulk hauling job (in hand or in inventory) is invisible to
/// the rebuild. Each rebuilt entry then starts out short by everything in flight, and the
/// deposits that follow drain it toward zero anyway - leaving <c>CountToTransfer</c> swinging
/// wildly between autoload ticks while haulers under-pick against it. Append the bulk job's
/// in-flight cargo so the rebuild sees the same picture it already gets from vanilla haulers.
/// </summary>
[HarmonyPatch(typeof(TransporterUtility), nameof(TransporterUtility.ThingsBeingHauledTo))]
internal static class Rimworld_TransporterUtility_ThingsBeingHauledTo
{
    internal static void Postfix(
        ref IEnumerable<Thing> __result,
        List<CompTransporter> transporters,
        Map map
    ) =>
        __result = __result.Concat(
            transporters.SelectMany(transporter =>
                Utils.BulkHauledThingsTowardTransporter(transporter, map)
            )
        );
}
