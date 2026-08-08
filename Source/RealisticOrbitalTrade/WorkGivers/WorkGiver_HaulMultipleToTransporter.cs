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
        return transporter != null
            && t.TryGetComp<CompTradeShuttle>() != null
            && LoadTransportersJobUtility.HasJobOnTransporter(pawn, transporter);
    }

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
