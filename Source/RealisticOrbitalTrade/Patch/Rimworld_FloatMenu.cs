using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Labels and disables float menu options (pick up, equip, wear, ...) targeting a thing a trade
/// agreement has reserved for hauling, mirroring vanilla's own "(Reserved by X)" treatment of
/// things reserved by another pawn's job. Vanilla's decoration can't do this by itself because our
/// reservation isn't backed by a real per-pawn <see cref="ReservationManager"/> entry (see
/// <see cref="Rimworld_ReservationManager_CanReserve"/>), so it never finds a reserver to name and
/// leaves the option enabled - which would otherwise let the player queue a job that
/// <see cref="Rimworld_Pawn_JobTracker_TryTakeOrderedJob"/> then has to reject anyway.
/// </summary>
[HarmonyPatch(typeof(FloatMenuUtility), nameof(FloatMenuUtility.DecoratePrioritizedTask))]
internal static class Rimworld_FloatMenuUtility_DecoratePrioritizedTask
{
    private static void Postfix(FloatMenuOption __result, Pawn pawn, LocalTargetInfo target)
    {
        if (__result.Disabled || !target.HasThing)
        {
            return;
        }

        if (
            !TradeHaulReservationContext.IsBlocked(
                pawn,
                target.Thing,
                RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements
            )
        )
        {
            return;
        }

        __result.Label += ": " + "RealisticOrbitalTrade.ReservedForTradeSuffix".Translate();
        __result.Disabled = true;
    }
}

/// <summary>
/// Rejects a player-ordered job outright when one of its targets is reserved by a trade agreement
/// and the ordering pawn isn't entitled to it, instead of letting it fail deep inside
/// <see cref="Job.TryMakePreToilReservations"/> - which logs a reservation error and a follow-up
/// warning and leaves the pawn silently reverting to whatever it was doing before. This is the
/// backstop for RimWorld.FloatMenuOptionProvider_PickUpItem and other providers that don't call
/// <see cref="FloatMenuUtility.DecoratePrioritizedTask"/> to gate their options.
/// </summary>
[HarmonyPatch(typeof(Pawn_JobTracker), nameof(Pawn_JobTracker.TryTakeOrderedJob))]
internal static class Rimworld_Pawn_JobTracker_TryTakeOrderedJob
{
    private static readonly TargetIndex[] TargetIndicesToCheck =
    [
        TargetIndex.A,
        TargetIndex.B,
        TargetIndex.C,
    ];

    private static bool Prefix(Job job, Pawn ___pawn, ref bool __result)
    {
        var blockedThing = FindBlockedTarget(___pawn, job);
        if (blockedThing == null)
        {
            return true;
        }

        Messages.Message(
            "RealisticOrbitalTrade.ReservedForTradeMessage".Translate(
                blockedThing.LabelShort,
                blockedThing
            ),
            blockedThing,
            MessageTypeDefOf.RejectInput,
            historical: false
        );
        __result = false;
        return false;
    }

    internal static Thing? FindBlockedTarget(Pawn pawn, Job job)
    {
        var tradeAgreements = RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements;
        foreach (var index in TargetIndicesToCheck)
        {
            var thing = job.GetTarget(index).Thing;
            if (
                thing != null
                && TradeHaulReservationContext.IsBlocked(pawn, thing, tradeAgreements)
            )
            {
                return thing;
            }
        }
        return null;
    }
}
