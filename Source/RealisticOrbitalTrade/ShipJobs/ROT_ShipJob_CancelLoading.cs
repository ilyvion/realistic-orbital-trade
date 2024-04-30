using RealisticOrbitalTrade.Comps;
using RimWorld;
using Verse;
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

    public override void Tick()
    {
        base.Tick();
        if (!done)
        {
            // Stop autoloading and remove any things from leftToLoad
            CompTradeShuttle compTradeShuttle = transportShip.shipThing.TryGetComp<CompTradeShuttle>();
            compTradeShuttle.cancelled = true;
            compTradeShuttle.ShuttleAutoLoad = false;
            transportShip.TransporterComp.leftToLoad.Clear();

            // End any jobs currently involved in loading the transport ship
            foreach (var humanLike in transportShip.shipThing.Map.mapPawns.AllHumanlike)
            {
                if (humanLike.CurJobDef == JobDefOf.HaulToTransporter && humanLike.CurJob.targetB == transportShip.shipThing)
                {
                    humanLike.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                }
            }

            // We only need to do this once
            done = true;
        }
    }
}
