using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestNode_Root_TradeShipMakeAmends : QuestNode
{
    protected override void RunInt()
    {
        var quest = QuestGen.quest;
        var slate = QuestGen.slate;

        var tradeShip = slate.Get<TradeShip>("tradeShip")!;
        var thingsToReturn = slate.Get<List<ThingDefCount>>("thingsToReturn");
        var tradePausesDepartureTimer = slate.Get<bool>("tradePausesDepartureTimer");

        var questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID(
            "ROT_TradeShipMakeAmends"
        );
        var cancelAmendmentSignal = QuestGenUtility.HardcodedSignalWithQuestID("CancelAmendment");

        var shuttle = QuestUtils.SetupShuttle(
            "shuttle",
            slate,
            questTag,
            out var signalShuttleSentSatisfied,
            out var signalShuttleKilled,
            true
        );
        var compShuttle = shuttle.TryGetComp<CompShuttle>();
        compShuttle.requiredItems = thingsToReturn;

        var transportShip = quest
            .GenerateTransportShip(TransportShipDefOf.Ship_Shuttle, [], shuttle)
            .transportShip;
        slate.Set("transportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        var map = tradeShip.Map;
        var landingSpot = QuestUtils.FindLandingSpot(map);

        _ = quest.Letter(
            LetterDefOf.PositiveEvent,
            text: "[introLetterText]",
            label: "[introLetterLabel]"
        );
        _ = quest.AddShipJob_Arrive(
            transportShip,
            map.Parent,
            null,
            landingSpot,
            ShipJobStartMode.Queue
        );
        _ = quest.AddShipJob_WaitForever(transportShip, true, true);

        _ = quest.CancelAmendment(shuttle, outSignalCancelled: cancelAmendmentSignal);
        quest.Signal(
            cancelAmendmentSignal,
            () =>
            {
                quest.CancelTransportShip(transportShip);
                _ = quest.Letter(
                    LetterDefOf.NeutralEvent,
                    text: "[cancelledLetterText]",
                    label: "[cancelledLetterLabel]"
                );
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do if the player fails to load the shuttle on time
        var ticksUntilShuttleDeparture = !tradePausesDepartureTimer
            ? QuestUtils.CheckTradeShipRequiresGraceTime(quest, slate, tradeShip)
            : tradeShip.ticksUntilDeparture;
        _ = quest.TradeShuttleLeaveDelay(
            shuttle,
            tradePausesDepartureTimer,
            tradeShip.TraderName,
            ticksUntilShuttleDeparture,
            inSignalsDisable: Gen.YieldSingle(signalShuttleSentSatisfied),
            complete: () =>
            {
                quest.CancelTransportShip(transportShip);
                _ = quest.Letter(
                    LetterDefOf.NeutralEvent,
                    text: "[expiredOfferLetterText]",
                    label: "[expiredOfferLetterLabel]"
                );
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do if the player destroys the shuttle
        quest.Signal(
            signalShuttleKilled,
            () =>
            {
                _ = quest.Letter(
                    LetterDefOf.NegativeEvent,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly,
                    text: "[shuttleKilledLetterText]",
                    label: "[shuttleKilledLetterLabel]"
                );
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do when the player has loaded the shuttle appropriately
        quest.Signal(
            signalShuttleSentSatisfied,
            () =>
            {
                // Put in a small delay so the quest finishes slightly later than immediately after the
                // shuttle starts taking off
                _ = quest.Delay(
                    500,
                    () =>
                    {
                        _ = quest.ExtendTradeShipDepartureIfVeryShort(tradeShip);
                        _ = quest.Letter(
                            LetterDefOf.PositiveEvent,
                            text: "[successLetterText]",
                            label: "[successLetterLabel]"
                        );
                        _ = quest.ForgivePlayerFactionInOrbitalTrade();
                        _ = quest.End(
                            QuestEndOutcome.Success,
                            signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                        );
                    }
                );
            }
        );
    }

    protected override bool TestRunInt(Slate slate) => true;
}
