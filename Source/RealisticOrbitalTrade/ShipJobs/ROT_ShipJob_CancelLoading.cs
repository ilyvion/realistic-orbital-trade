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

            // End any jobs currently involved in loading the transport ship - both vanilla's
            // own single-item hauling and this mod's bulk-haul job (JobDefOf.ROT_HaulMultipleToTransporter,
            // the default loading path per Settings._multiItemShuttleLoading) target the shuttle
            // thing itself via targetB, so both are matched the same way.
            var soldDefs = SoldDefs(compTradeShuttle.tradeAgreement);
            foreach (var humanLike in transportShip.shipThing.Map.mapPawns.GetAllHumanLike())
            {
                if (IsLoadingCancelledShip(humanLike.CurJob, transportShip.shipThing))
                {
                    humanLike.jobs.EndCurrentJob(JobCondition.Incompletable, true, true);
                    if (soldDefs != null)
                    {
                        DropSoldItems(humanLike, soldDefs);
                    }
                }
            }

            // We only need to do this once
            done = true;
        }
    }

    // Loading jobs from other mods (Bulk Load For Transporters) stash sold items in the pawn's
    // hands and inventory and don't hand them back when they're ended. Anything of a sold kind the
    // pawn still holds goes back on the ground so the goods stay in the colony.
    internal static HashSet<ThingDef>? SoldDefs(TradeAgreement? tradeAgreement) =>
        tradeAgreement?.thingsSoldToTrader.Select(t => t.thing?.def).OfType<ThingDef>().ToHashSet();

    internal static void DropSoldItems(Pawn pawn, HashSet<ThingDef> soldDefs)
    {
        if (pawn.carryTracker.CarriedThing is { } carried && soldDefs.Contains(carried.def))
        {
            _ = pawn.carryTracker.TryDropCarriedThing(
                pawn.PositionHeld,
                ThingPlaceMode.Near,
                out _
            );
        }

        foreach (var thing in pawn.inventory.innerContainer.ToList())
        {
            if (soldDefs.Contains(thing.def))
            {
                _ = pawn.inventory.innerContainer.TryDrop(
                    thing,
                    pawn.PositionHeld,
                    pawn.MapHeld,
                    ThingPlaceMode.Near,
                    thing.stackCount,
                    out _
                );
            }
        }
    }

    // Split out from Tick/TickInterval so the matching condition is unit-testable without a live
    // map/pawn - see IsLoadingCancelledShipTests.
    internal static bool IsLoadingCancelledShip(Job? curJob, Thing shipThing) =>
        curJob != null
        && (
            curJob.def == RimWorld.JobDefOf.HaulToTransporter
            || curJob.def == JobDefOf.ROT_HaulMultipleToTransporter
            || curJob.def == JobDefOf.LoadTransportersInBulk_Load
        )
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
