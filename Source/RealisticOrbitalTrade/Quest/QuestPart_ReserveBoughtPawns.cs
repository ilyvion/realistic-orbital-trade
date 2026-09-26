namespace RealisticOrbitalTrade.Quests;

/// <summary>
/// Marks pawns bought from the trader as reserved by the quest while they wait for delivery, so
/// the World Pawns cleanup doesn't discard them (it only keeps unspawned animals that a quest
/// reserves).
/// </summary>
internal class QuestPart_ReserveBoughtPawns : QuestPart
{
    public TradeAgreement? tradeAgreement;

    public override bool QuestPartReserves(Pawn p) =>
        p != null && tradeAgreement?.tradeDeliveryMethod?.Things.Contains(p) == true;

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
    }
}

internal static class QuestGen_ReserveBoughtPawns
{
    public static QuestPart_ReserveBoughtPawns ReserveBoughtPawns(
        this Quest quest,
        TradeAgreement tradeAgreement
    )
    {
        var part = new QuestPart_ReserveBoughtPawns { tradeAgreement = tradeAgreement };
        quest.AddPart(part);
        return part;
    }
}
