using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Makes <see cref="WorkGiver_HaulMultipleToTransporter"/> the only source of loading jobs for
/// trade shuttles: vanilla's <see cref="WorkGiver_LoadTransporters"/> and
/// <see cref="JobGiver_LoadTransporters"/>, and other mods' loading systems that patch these
/// methods, find no work on one. The prefixes run first, so returning <see langword="false"/>
/// also skips other mods' prefixes.
/// </summary>
[HarmonyPatch(
    typeof(LoadTransportersJobUtility),
    nameof(LoadTransportersJobUtility.HasJobOnTransporter)
)]
internal static class Rimworld_LoadTransportersJobUtility_HasJobOnTransporter
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(CompTransporter transporter, ref bool __result)
    {
        if (!Rimworld_LoadTransportersJobUtility_Shared.IsTradeShuttle(transporter))
        {
            return true;
        }
        __result = false;
        return false;
    }
}

[HarmonyPatch(
    typeof(LoadTransportersJobUtility),
    nameof(LoadTransportersJobUtility.JobOnTransporter)
)]
internal static class Rimworld_LoadTransportersJobUtility_JobOnTransporter
{
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(CompTransporter transporter, ref Job? __result)
    {
        if (!Rimworld_LoadTransportersJobUtility_Shared.IsTradeShuttle(transporter))
        {
            return true;
        }
        __result = null;
        return false;
    }
}

internal static class Rimworld_LoadTransportersJobUtility_Shared
{
    internal static bool IsTradeShuttle(CompTransporter? transporter) =>
        transporter?.parent?.TryGetComp<CompTradeShuttle>() != null;
}
