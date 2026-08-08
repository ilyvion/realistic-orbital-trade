using RimWorld.Planet;
using RimWorld.QuestGen;

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
            WarnIfAnyStillReserved();

            var things = tradeAgreement!.tradeShip.things;
            var soldPrisoners = tradeAgreement.tradeShip.soldPrisoners;

            // Give the things the player sold to the trader
            foreach (var thing in toTraderTransportShip!.TransporterComp.innerContainer.ToList())
            {
                thing.PreTraded(
                    TradeAction.PlayerSells,
                    tradeAgreement.negotiator,
                    tradeAgreement.tradeShip
                );

                var pawn = thing as Pawn;
                if (pawn != null && pawn.RaceProps.Humanlike)
                {
                    soldPrisoners.Add(pawn);
                }
                if (!things.TryAddOrTransfer(thing))
                {
                    RealisticOrbitalTradeMod.Instance.LogWarning(
                        $"Failed transferring {thing.Label} to orbital trader {tradeAgreement.tradeShip.TraderName} in QuestPart_TransferItemsToTrader"
                    );
                    thing.Destroy();
                }
                else if (pawn != null && pawn.IsWorldPawn())
                {
                    // ShipJob_FlyAway.TryStart fires this quest signal synchronously via
                    // SendLaunchedSignals, then immediately sweeps any thing still left in the
                    // shuttle's transporter into a leaving skyfaller that passes stray pawns to
                    // World Pawns. If this pawn was concurrently swept there before this transfer
                    // ran, it's now deep-owned by tradeShip.things as well, so it must be dropped
                    // from World Pawns to avoid a duplicate loadID registration on save/load.
                    Find.WorldPawns.RemovePawn(pawn);
                }
            }
        }
    }

    // The shuttle is taking off with whatever made it into the transporter's inner container - by
    // this point every sold item should either be loaded or gone from
    // tradeAgreement.reservedTraderThings (see JobDriver_HaulMultipleToTransporter/
    // Rimworld_JobDriver_HaulToTransporter_Notify_Starting, which shrink it as hauling jobs claim
    // real reservations). Anything still sitting untouched on the map here means a sold item never
    // got claimed by a hauling job - a bug in the mod's own logic, not something the player did.
    private void WarnIfAnyStillReserved()
    {
        var stuck = tradeAgreement!.StillReservedSpawnedThings().ToList();
        if (stuck.Count == 0)
        {
            return;
        }

        RealisticOrbitalTradeMod.Instance.LogWarning(
            "Trade shuttle departed with sold item(s) still marked as reserved-for-protection but "
                + "never hauled: "
                + string.Join(", ", stuck.Select(t => t.LabelCap))
                + ". This is a bug in the mod's hauling logic, please report it with a full log."
        );
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
    public static QuestPart_TransferItemsToTrader TransferItemsToTrader(
        this Quest quest,
        TradeAgreement tradeAgreement,
        TransportShip toTraderTransportShip,
        string? inSignal = null
    )
    {
        QuestPart_TransferItemsToTrader questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeAgreement = tradeAgreement,
            toTraderTransportShip = toTraderTransportShip,
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
