using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Makes things sold to a trader but not yet hauled to the shuttle unavailable for every other
/// colony job (eating, crafting, wearing, ...), closing the window between a deal being struck
/// and a hauler actually picking the item up during which vanilla would otherwise let any pawn
/// consume it. See <see cref="TradeHaulReservationContext"/> for how a hauling pawn's own
/// candidate scan/reservation is told apart from everyone else's.
/// </summary>
[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
internal static class Rimworld_ReservationManager_CanReserve
{
    private static void Postfix(Pawn claimant, LocalTargetInfo target, ref bool __result)
    {
        if (!__result || !target.HasThing)
        {
            return;
        }

        if (
            TradeHaulReservationContext.IsBlocked(
                claimant,
                target.Thing,
                RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements
            )
        )
        {
            __result = false;
        }
    }
}

/// <summary>
/// Wraps vanilla's own <see cref="JobDriver_HaulToTransporter.Notify_Starting"/>, for a vanilla
/// hauling job that targets a trade shuttle, in a <see cref="TradeHaulReservationContext"/>
/// scope, and - once it's done - moves whatever it
/// actually reserved from the trade's reserved-for-protection list to the real reservation it just
/// took out, mirroring what <see cref="JobDriver_HaulMultipleToTransporter"/> does for itself.
/// </summary>
[HarmonyPatch(
    typeof(JobDriver_HaulToTransporter),
    nameof(JobDriver_HaulToTransporter.Notify_Starting)
)]
internal static class Rimworld_JobDriver_HaulToTransporter_Notify_Starting
{
    private static void Prefix(JobDriver __instance, out IDisposable? __state)
    {
        var transporter = __instance
            .job.GetTarget(TargetIndex.B)
            .Thing?.TryGetComp<CompTransporter>();
        __state =
            transporter != null
                ? TradeHaulReservationContext.Enter(__instance.pawn, transporter)
                : null;
    }

    private static void Postfix(JobDriver __instance, IDisposable? __state)
    {
        __state?.Dispose();

        var thing = __instance.job.GetTarget(TargetIndex.A).Thing;
        var transporter = __instance
            .job.GetTarget(TargetIndex.B)
            .Thing?.TryGetComp<CompTransporter>();
        var tradeAgreement = transporter?.parent.TryGetComp<CompTradeShuttle>()?.tradeAgreement;
        if (thing == null || tradeAgreement == null)
        {
            return;
        }

        // Notify_Starting doesn't check pawn.Reserve's return value itself, so a failed
        // reservation there (rare - the whole point of the scope above is to make it succeed)
        // must be told apart here from a genuine success before touching our own bookkeeping.
        if (
            __instance.pawn.Map.reservationManager.ReservedBy(
                thing,
                __instance.pawn,
                __instance.job
            )
        )
        {
            _ = tradeAgreement.ReduceReservation(thing, __instance.job.count);

            // See TradeAgreement.ReconcileReservation - vanilla's driver only ever hauls one
            // stack per job, but the same surplus can still accrue across several separate trips
            // each grabbing a different stack of the same resource. CountToTransfer itself can't
            // be used as the target here - it lags behind every in-flight reservation - so this
            // mirrors JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts to work out what
            // every pawn (this one included) still intends to claim from here on.
            var transferable = transporter!.leftToLoad?.Find(t => t.things.Contains(thing));
            if (transferable != null)
            {
                var committedByOthers = JobDriver_HaulMultipleToTransporter
                    .ComputeCommittedCounts(transporter, __instance.pawn)
                    .GetValueOrDefault(transferable);
                var stillNeeded = Math.Max(
                    0,
                    transferable.CountToTransfer - committedByOthers - __instance.job.count
                );
                tradeAgreement.ReconcileReservation(transferable, stillNeeded);
            }
        }
    }
}
