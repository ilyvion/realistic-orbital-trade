using RimWorld.Planet;
using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_ReturnBoughtItemsToTradeShip : QuestPart
{
    public string? inSignal;

    public TradeAgreement? tradeAgreement;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            // Give the things the player bought back to the trader
            var things = tradeAgreement!.tradeShip.things;
            foreach (var thing in tradeAgreement.tradeDeliveryMethod!.Things.ToList())
            {
                if (!things.TryAddOrTransfer(thing))
                {
                    RealisticOrbitalTradeMod.Instance.LogWarning(
                        $"Failed returning {thing.Label} to orbital trader {tradeAgreement.tradeShip.TraderName} in QuestPart_ReturnBoughtItemsToTradeShip"
                    );
                    thing.Destroy();
                }
                else if (thing is Pawn pawn && pawn.IsWorldPawn())
                {
                    // PreTrade already passed this pawn to World Pawns before it was ever handed
                    // to the player (see QuestNode_Root_TradeShipTransportShip.PreTrade), so
                    // undo that here now that it's deep-owned by tradeShip.things again, to avoid
                    // a duplicate loadID registration on save/load.
                    Find.WorldPawns.RemovePawn(pawn);
                }
            }
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
    }
}

internal static class QuestGen_ReturnBoughtItemsToTradeShip
{
    public static QuestPart_ReturnBoughtItemsToTradeShip ReturnBoughtItemsToTradeShip(
        this Quest quest,
        TradeAgreement tradeAgreement,
        string? inSignal = null
    )
    {
        QuestPart_ReturnBoughtItemsToTradeShip questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeAgreement = tradeAgreement,
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
