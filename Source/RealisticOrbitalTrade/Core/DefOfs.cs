namespace RealisticOrbitalTrade;

#pragma warning disable CS8618,CS0649
[DefOf]
internal static class QuestScriptDefOf
{
    public static QuestScriptDef ROT_TradeShipTransportShip;
    public static QuestScriptDef ROT_TradeShipMakeAmends;

    static QuestScriptDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(QuestScriptDefOf));
    }
}

[DefOf]
internal static class ThingDefOf
{
    public static ThingDef ROT_TradeShuttle;
    public static ThingDef ROT_ExplosiveRiggedTradeShuttle;
    public static ThingDef ROT_ExplosiveRiggedAmendmentTradeShuttle;

    static ThingDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ThingDefOf));
    }
}

[DefOf]
internal static class ShipJobDefOf
{
    public static ShipJobDef ROT_CancelLoad;

    static ShipJobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(ShipJobDefOf));
    }
}

[DefOf]
internal static class JobDefOf
{
    public static JobDef ROT_RenegotiateTrade;
    public static JobDef ROT_RegisterAsNegotiator;
    public static JobDef ROT_BuyMoreTime;
    public static JobDef ROT_HaulMultipleToTransporter;

    [MayRequire(Settings.BulkLoadForTransportersPackageId)]
    public static JobDef? LoadTransportersInBulk_Load;

    static JobDefOf()
    {
        DefOfHelper.EnsureInitializedInCtor(typeof(JobDefOf));
    }
}
#pragma warning restore CS8618,CS0649
