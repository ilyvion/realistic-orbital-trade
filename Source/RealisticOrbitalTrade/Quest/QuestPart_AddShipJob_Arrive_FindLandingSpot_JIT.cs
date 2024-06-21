using RimWorld.Planet;
using RimWorld.QuestGen;

using RWShipJobDefOf = RimWorld.ShipJobDefOf;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_AddShipJob_Arrive_FindLandingSpot_JIT : QuestPart_AddShipJob_Arrive
{
    public override ShipJob GetShipJob()
    {
        var job = (ShipJob_Arrive)base.GetShipJob();
        job.cell = QuestUtils.FindLandingSpot(mapParent.Map);
        return job;
    }
}


internal static class QuestGen_AddShipJob_Arrive_FindLandingSpot_JIT
{
    public static QuestPart_AddShipJob_Arrive_FindLandingSpot_JIT AddShipJob_Arrive_FindLandingSpot_JIT(
        this Quest quest,
        TransportShip transportShip,
        MapParent mapParent,
        Pawn? mapOfPawn = null,
        ShipJobStartMode startMode = ShipJobStartMode.Queue,
        Faction? factionForArrival = null,
        string? inSignal = null
    )
    {
        QuestPart_AddShipJob_Arrive_FindLandingSpot_JIT questPart_AddShipJob_Arrive_FindLandingSpot_JIT = new QuestPart_AddShipJob_Arrive_FindLandingSpot_JIT
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            shipJobStartMode = startMode,
            transportShip = transportShip,
            shipJobDef = RWShipJobDefOf.Arrive,
            cell = IntVec3.Invalid,
            mapParent = mapParent,
            mapOfPawn = mapOfPawn,
            factionForArrival = factionForArrival
        };
        quest.AddPart(questPart_AddShipJob_Arrive_FindLandingSpot_JIT);
        return questPart_AddShipJob_Arrive_FindLandingSpot_JIT;
    }
}
