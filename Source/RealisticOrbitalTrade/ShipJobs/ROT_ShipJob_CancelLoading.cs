using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.ShipJobs;

public class ShipJob_CancelLoad : ShipJob
{
    private bool done;

    protected override bool ShouldEnd => done;
    public override bool Interruptible => false;

    public override bool TryStart()
    {
        if (!transportShip.ShipExistsAndIsSpawned)
        {
            return false;
        }
        return base.TryStart();
    }

#if v1_6
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
            CompTradeShuttle compTradeShuttle =
                transportShip.shipThing.TryGetComp<CompTradeShuttle>();
            compTradeShuttle.cancelled = true;
            compTradeShuttle.ShuttleAutoLoad = false;
            transportShip.TransporterComp.leftToLoad?.Clear();

            // End any jobs currently involved in loading the transport ship
            foreach (var humanLike in transportShip.shipThing.Map.mapPawns.GetAllHumanLike())
            {
                if (
                    humanLike.CurJobDef == RimWorld.JobDefOf.HaulToTransporter
                    && humanLike.CurJob.targetB == transportShip.shipThing
                )
                {
                    humanLike.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                }
            }

            // We only need to do this once
            done = true;
        }
    }
}

internal static class MapPawnsGetAllHumanLike
{
#if v1_4
    private static List<Pawn> humanlikePawnsResult = new List<Pawn>();
#endif

    public static List<Pawn> GetAllHumanLike(this MapPawns mapPawns)
    {
#if v1_4
        humanlikePawnsResult.Clear();
        List<Pawn> allPawns = mapPawns.AllPawns;
        for (int i = 0; i < allPawns.Count; i++)
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
