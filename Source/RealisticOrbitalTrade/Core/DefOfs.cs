
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade;

#pragma warning disable CS8618,CS0649
[DefOf]
internal static class QuestScriptDefOf
{
    public static QuestScriptDef ROT_TradeShipTransportShip;
    public static QuestScriptDef ROT_TradeShipMakeAmends;
}

[DefOf]
internal static class ThingDefOf
{
    public static ThingDef ROT_TradeShuttle;
    public static ThingDef ROT_ExplosiveRiggedTradeShuttle;
    public static ThingDef ROT_ExplosiveRiggedAmendmentTradeShuttle;
}
[DefOf]
internal static class ShipJobDefOf
{
    public static ShipJobDef ROT_CancelLoad;
}
#pragma warning restore CS8618,CS0649
