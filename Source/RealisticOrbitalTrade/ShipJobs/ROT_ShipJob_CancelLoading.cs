using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.ShipJobs;

internal class ShipJob_CancelLoad : ShipJob
{
    internal bool done;

    protected override bool ShouldEnd => done;
    public override bool Interruptible => false;

    public override bool TryStart() => transportShip.ShipExistsAndIsSpawned && base.TryStart();

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref done, "done");
    }

#if v1_6_OR_GREATER
    public override void TickInterval(int delta)
    {
        base.TickInterval(delta);
#else
    public override void Tick()
    {
        base.Tick();
#endif
        if (!done)
        {
            // Stop autoloading and remove any things from leftToLoad
            var compTradeShuttle = transportShip.shipThing.TryGetComp<CompTradeShuttle>();
            compTradeShuttle.cancelled = true;
            compTradeShuttle.ShuttleAutoLoad = false;
            transportShip.TransporterComp.leftToLoad?.Clear();

            // End any jobs currently loading the transport ship.
            foreach (var humanLike in transportShip.shipThing.Map.mapPawns.GetAllHumanLike())
            {
                if (IsLoadingCancelledShip(humanLike.CurJob, transportShip.shipThing))
                {
                    humanLike.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                }
            }

            // We only need to do this once
            done = true;
        }
    }

    // Split out from Tick/TickInterval so the matching condition is unit-testable without a live
    // map/pawn - see IsLoadingCancelledShipTests.
    internal static bool IsLoadingCancelledShip(Job? curJob, Thing shipThing) =>
        curJob != null
        && curJob.def == JobDefOf.ROT_HaulMultipleToTransporter
        && curJob.targetB == shipThing;
}

internal static class MapPawnsGetAllHumanLike
{
#if v1_4
    private static readonly List<Pawn> humanlikePawnsResult = [];
#endif

    public static List<Pawn> GetAllHumanLike(this MapPawns mapPawns)
    {
#if v1_4
        humanlikePawnsResult.Clear();
        var allPawns = mapPawns.AllPawns;
        for (var i = 0; i < allPawns.Count; i++)
        {
            if (allPawns[i].RaceProps.Humanlike)
            {
                humanlikePawnsResult.Add(allPawns[i]);
            }
        }
        return humanlikePawnsResult;
#else
        return mapPawns.AllHumanlike;
#endif
    }
}
