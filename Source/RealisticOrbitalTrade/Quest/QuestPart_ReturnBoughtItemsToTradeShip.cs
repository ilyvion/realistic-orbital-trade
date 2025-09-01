using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_ReturnBoughtItemsToTradeShip : QuestPart
{
    public string? inSignal;

    public TradeAgreement? tradeAgreement;
    public TransportShip? toPlayerTransportShip;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            // Give the things the player bought back to the trader
            var things = Traverse
                .Create(tradeAgreement!.tradeShip)
                .Field<ThingOwner>("things")
                .Value;
            foreach (var thing in toPlayerTransportShip!.TransporterComp.innerContainer.ToList())
            {
                if (!things.TryAddOrTransfer(thing))
                {
                    RealisticOrbitalTradeMod.Instance.LogWarning(
                        $"Failed returning {thing.Label} to orbital trader {tradeAgreement.tradeShip.TraderName} in QuestPart_ReturnBoughtItemsToTradeShip"
                    );
                    thing.Destroy();
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
        toPlayerTransportShip = null;
    }
}

internal static class QuestGen_ReturnBoughtItemsToTradeShip
{
    public static QuestPart_ReturnBoughtItemsToTradeShip ReturnBoughtItemsToTradeShip(
        this Quest quest,
        TradeAgreement tradeAgreement,
        TransportShip toPlayerTransportShip,
        string? inSignal = null
    )
    {
        QuestPart_ReturnBoughtItemsToTradeShip questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeAgreement = tradeAgreement,
            toPlayerTransportShip = toPlayerTransportShip,
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
