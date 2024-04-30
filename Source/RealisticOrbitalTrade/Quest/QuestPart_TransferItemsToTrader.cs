using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_TransferItemsToTrader : QuestPart
{
    public string? inSignal;

    public TradeAgreement? tradeAgreement;

    public TransportShip? toTraderTransportShip;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            var things = Traverse.Create(tradeAgreement!.tradeShip).Field<ThingOwner>("things").Value;
            var soldPrisoners = Traverse.Create(tradeAgreement.tradeShip).Field("soldPrisoners").GetValue<List<Pawn>>();

            // Give the things the player sold to the trader
            foreach (var thing in toTraderTransportShip!.TransporterComp.innerContainer.ToList())
            {
                thing.PreTraded(TradeAction.PlayerSells, tradeAgreement.negotiator, tradeAgreement.tradeShip);

                if (thing is Pawn pawn && pawn.RaceProps.Humanlike)
                {
                    soldPrisoners.Add(pawn);
                }
                if (!things.TryAddOrTransfer(thing))
                {
                    RealisticOrbitalTradeMod.Warning($"Failed transferring {thing.Label} to orbital trader {tradeAgreement.tradeShip.TraderName} in QuestPart_TransferItemsToTrader");
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
        Scribe_References.Look(ref toTraderTransportShip, "toTraderTransportShip");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
        toTraderTransportShip = null;
    }
}

internal static class QuestGen_TransferItemsToTrader
{
    public static QuestPart_TransferItemsToTrader TransferItemsToTrader(this Quest quest, TradeAgreement tradeAgreement, TransportShip toTraderTransportShip, string? inSignal = null)
    {
        QuestPart_TransferItemsToTrader questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeAgreement = tradeAgreement,
            toTraderTransportShip = toTraderTransportShip
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
