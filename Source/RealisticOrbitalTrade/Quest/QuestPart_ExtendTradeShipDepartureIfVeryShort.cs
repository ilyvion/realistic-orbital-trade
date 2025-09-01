using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_ExtendTradeShipDepartureIfVeryShort : QuestPart
{
    private string? inSignal;

    private TradeShip? tradeShip;

    public QuestPart_ExtendTradeShipDepartureIfVeryShort() { }

    public QuestPart_ExtendTradeShipDepartureIfVeryShort(string? inSignal, TradeShip tradeShip)
    {
        this.inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal");
        this.tradeShip = tradeShip;
    }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            // If the trade ship was leaving in less than an hour, make it an hour.
            if (tradeShip!.ticksUntilDeparture < 2500)
            {
                tradeShip.ticksUntilDeparture = 2500;
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_References.Look(ref tradeShip, "tradeShip");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeShip = null;
    }
}

internal static class QuestGen_ExtendTradeShipDepartureIfVeryShort
{
    public static QuestPart_ExtendTradeShipDepartureIfVeryShort ExtendTradeShipDepartureIfVeryShort(
        this Quest quest,
        TradeShip tradeShip,
        string? inSignal = null
    )
    {
        QuestPart_ExtendTradeShipDepartureIfVeryShort questPart = new(
            inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeShip
        );
        quest.AddPart(questPart);
        return questPart;
    }
}
