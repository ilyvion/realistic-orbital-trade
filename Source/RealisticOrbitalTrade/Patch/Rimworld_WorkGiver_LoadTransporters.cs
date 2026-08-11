using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// When <see cref="Settings._multiItemShuttleLoading"/> is on, <see
/// cref="WorkGiver_HaulMultipleToTransporter"/> is meant to be the sole authority for loading
/// trade shuttles - but nothing stopped vanilla's own <see cref="WorkGiver_LoadTransporters"/>
/// from also being offered the same transporter. That's normally harmless (this mod's own
/// commitment tracking already accounts for vanilla's <see cref="JobDriver_HaulToTransporter"/>
/// running alongside it), but a third-party mod that itself patches <see
/// cref="LoadTransportersJobUtility.HasJobOnTransporter"/>/<see
/// cref="LoadTransportersJobUtility.JobOnTransporter"/> to install its own competing bulk-loading
/// system (e.g. Bulk Load For Transporters) is invisible to that tracking. Its planner can then
/// end up permanently blocked by reservations this mod holds for the trade, and - since its own
/// "is there work?" check disagrees with its own "can I actually plan it?" check - it keeps
/// reissuing a short expiring Job it can never complete instead of ever giving up, which reads to
/// the player as colonists getting stuck standing still. Suppressing vanilla's WorkGiver for trade
/// shuttles outright avoids that class of conflict regardless of which mod is behind it.
/// </summary>
[HarmonyPatch(typeof(WorkGiver_LoadTransporters), nameof(WorkGiver_LoadTransporters.HasJobOnThing))]
internal static class Rimworld_WorkGiver_LoadTransporters_HasJobOnThing
{
    private static bool Prefix(Thing t, ref bool __result)
    {
        if (!Rimworld_WorkGiver_LoadTransporters_Shared.ShouldSuppressForTradeShuttle(t))
        {
            return true;
        }
        __result = false;
        return false;
    }
}

[HarmonyPatch(typeof(WorkGiver_LoadTransporters), nameof(WorkGiver_LoadTransporters.JobOnThing))]
internal static class Rimworld_WorkGiver_LoadTransporters_JobOnThing
{
    private static bool Prefix(Thing t, ref Job __result)
    {
        if (!Rimworld_WorkGiver_LoadTransporters_Shared.ShouldSuppressForTradeShuttle(t))
        {
            return true;
        }
        __result = null!;
        return false;
    }
}

internal static class Rimworld_WorkGiver_LoadTransporters_Shared
{
    internal static bool ShouldSuppressForTradeShuttle(Thing t) =>
        ShouldSuppressForTradeShuttle(
            Settings._multiItemShuttleLoading,
            t.TryGetComp<CompTradeShuttle>()
        );

    // Split out from the Thing-taking overload above so the decision can be unit tested without a
    // live spawned Thing/comp hierarchy.
    internal static bool ShouldSuppressForTradeShuttle(
        bool multiItemShuttleLoading,
        CompTradeShuttle? tradeShuttleComp
    ) => multiItemShuttleLoading && tradeShuttleComp != null;
}
