using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

/// <summary>
/// Only fires for trade shuttles (transporters carrying a <see cref="CompTradeShuttle"/>), so
/// vanilla transport-pod launches and caravan packing are unaffected. When
/// <see cref="Settings._multiItemShuttleLoading"/> is off, this work giver skips entirely and
/// vanilla's own <see cref="WorkGiver_LoadTransporters"/> handles shuttle loading as
/// usual.
/// </summary>
[HotSwappable]
internal class WorkGiver_HaulMultipleToTransporter : WorkGiver_Scanner
{
    public override ThingRequest PotentialWorkThingRequest =>
        ThingRequest.ForGroup(ThingRequestGroup.Transporter);

    public override PathEndMode PathEndMode => PathEndMode.Touch;

    public override Danger MaxPathDanger(Pawn pawn) => Danger.Deadly;

    public override bool ShouldSkip(Pawn pawn, bool forced = false) =>
        base.ShouldSkip(pawn, forced) || !Settings._multiItemShuttleLoading;

    public override bool HasJobOnThing(Pawn pawn, Thing t, bool forced = false)
    {
        var transporter = t.TryGetComp<CompTransporter>();
        if (
            transporter == null
            || t.TryGetComp<CompTradeShuttle>() == null
            || !LoadTransportersJobUtility.HasJobOnTransporter(pawn, transporter)
        )
        {
            return false;
        }

        // HasJobOnTransporter only checks whether some leftToLoad entry still has
        // CountToTransfer > 0 and a reachable matching thing exists - it has no idea another pawn
        // has already committed (queued, carried, or in hand - see
        // JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts) enough to cover the rest.
        // CompTradeShuttle.CheckAutoload only reconciles CountToTransfer against that every ~120
        // ticks, so without this check the WorkGiver keeps handing this pawn a job
        // Notify_Starting is guaranteed to immediately reject, spamming a
        // create-job/bail-out loop every tick until the next autoload tick catches up.
        var thingCount = LoadTransportersJobUtility.FindThingToLoad(pawn, transporter);
        if (thingCount.Thing == null)
        {
            return false;
        }
        var primaryTransferable = transporter.leftToLoad?.Find(tr =>
            tr.things.Contains(thingCount.Thing)
        );
        if (primaryTransferable == null)
        {
            return true;
        }
        var committed = JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts(
            transporter,
            pawn
        );
        return StillNeedsPickup(primaryTransferable, committed);
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
}
