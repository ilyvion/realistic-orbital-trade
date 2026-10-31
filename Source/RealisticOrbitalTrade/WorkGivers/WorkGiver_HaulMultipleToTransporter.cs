using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

/// <summary>
/// Only fires for trade shuttles (transporters carrying a <see cref="CompTradeShuttle"/>), so
/// vanilla transport-pod launches and caravan packing are unaffected. It is the only source of
/// loading jobs for trade shuttles; see
/// <see cref="Patch.Rimworld_LoadTransportersJobUtility_HasJobOnTransporter"/>.
/// </summary>
[HotSwappable]
internal class WorkGiver_HaulMultipleToTransporter : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest =>
        ThingRequest.ForGroup(ThingRequestGroup.Transporter);

    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

    public override bool ShouldSkip(Pawn pawn, bool forced = false)
    {
        var shouldSkip = base.ShouldSkip(pawn, forced);
        DebugLog($"Should skip: {shouldSkip}", pawn);
        return shouldSkip;
    }

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var transporter = t.TryGetComp<CompTransporter>();
        if (
            transporter == null
            || t.TryGetComp<CompTradeShuttle>() == null
            || !transporter.AnythingLeftToLoad
            || !pawn.health.capacities.CapableOf(PawnCapacityDefOf.Manipulation)
            || !pawn.CanReach(transporter.parent, PathEndMode.Touch, pawn.NormalMaxDanger())
        )
        {
            DebugLog(
                $"Transporter is null: {transporter == null}, comp is null: {t.TryGetComp<CompTradeShuttle>() == null}, nothing left to load: {transporter?.AnythingLeftToLoad != true}",
                pawn
            );
            return false;
        }

        // Exempts the scan below from Patch.Rimworld_ReservationManager_CanReserve's protection
        // of not-yet-hauled sold items - see TradeHaulReservationContext.
        // JobDriver_HaulMultipleToTransporter.Notify_Starting wraps its own FindThingToLoad call
        // the same way once the job actually starts. Not routed through
        // LoadTransportersJobUtility.HasJobOnTransporter, which finds no work on trade shuttles
        // (see Patch.Rimworld_LoadTransportersJobUtility_HasJobOnTransporter).
        using var haulReservationScope = TradeHaulReservationContext.Enter(pawn, transporter);

        // FindThingToLoad only checks whether some leftToLoad entry still has CountToTransfer > 0
        // and a reachable matching thing exists - it has no idea another pawn has already
        // committed (queued, carried, or in hand - see
        // JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts) enough to cover the rest.
        // CompTradeShuttle.CheckAutoload only reconciles CountToTransfer against that every ~120
        // ticks, so without this check the WorkGiver keeps handing this pawn a job
        // Notify_Starting is guaranteed to immediately reject, spamming a
        // create-job/bail-out loop every tick until the next autoload tick catches up.
        var thingCount = LoadTransportersJobUtility.FindThingToLoad(pawn, transporter);
        if (thingCount.Thing == null)
        {
            DebugLog($"Thing is null", pawn);
            return false;
        }
        var primaryTransferable = transporter.leftToLoad?.Find(tr =>
            tr.things.Contains(thingCount.Thing)
        );
        if (primaryTransferable == null)
        {
            DebugLog($"primaryTransferable is null", pawn);
            return true;
        }
        var committed = JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts(
            transporter,
            pawn
        );
        var v = StillNeedsPickup(primaryTransferable, committed);
        if (!v)
        {
            DebugLog(
                $"primaryTransferable.CountToTransfer: {primaryTransferable.CountToTransfer}, committed.GetValueOrDefault(primaryTransferable): {committed.GetValueOrDefault(primaryTransferable)}",
                pawn
            );
        }
        return v;
    }

    // Split out from HasJobOnThing so the commitment-floor arithmetic (identical to
    // JobDriver_HaulMultipleToTransporter.Notify_Starting's own clamp) can be unit tested without
    // a live pawn/map.
    internal static bool StillNeedsPickup(
        TransferableOneWay primaryTransferable,
        Dictionary<TransferableOneWay, int> committed
    ) => primaryTransferable.CountToTransfer - committed.GetValueOrDefault(primaryTransferable) > 0;

    public override Job? JobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var transporter = t.TryGetComp<CompTransporter>();
        if (transporter == null || t.TryGetComp<CompTradeShuttle>() == null)
        {
            return null;
        }

        var job = JobMaker.MakeJob(
            JobDefOf.ROT_HaulMultipleToTransporter,
            LocalTargetInfo.Invalid,
            t
        );
        job.ignoreForbidden = true;
        return job;
    }

    // Verbose per-decision tracing of the pickup/hauling logic below, gated on the
    // dev-messages toggle so it's opt-in and doesn't spam normal play.
    private static void DebugLog(string message, Pawn? pawn = null)
    {
        if (Settings._printDevMessages)
        {
            if (pawn != null)
            {
                Log.Message($"[ROT-Haul WG] {pawn.LabelShort}: {message}");
            }
            else
            {
                Log.Message($"[ROT-Haul WG] {message}");
            }
        }
    }
}
