using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Core;
using RimWorld.Planet;
using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

[HotSwappable]
internal class QuestNode_Root_TradeShipTransportShip : QuestNode
{
    protected override void RunInt()
    {
        var quest = QuestGen.quest;
        var slate = QuestGen.slate;

        var tradeAgreement = slate.Get<TradeAgreement>("tradeAgreement");

        var questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID(
            "ROT_TradeShipTransportShip"
        );
        var cancelTradeSignal = QuestGenUtility.HardcodedSignalWithQuestID("CancelTrade");

        (var toTraderShuttle, var toTraderTransportShip) = SetupToTraderTransportShip(
            quest,
            slate,
            tradeAgreement,
            questTag,
            out var signalToTraderShuttleSentSatisfied,
            out var signalToTraderShuttleKilled
        );

        tradeAgreement.toTraderTransportShip = toTraderTransportShip;

        string? signalToPlayerItemsDelivered = null;
        string? signalToPlayerShuttleKilled = null;
        if (!tradeAgreement.deliverItemsViaDropPod)
        {
            var (toPlayerShuttle, toPlayerTransportShip) = SetupToPlayerTransportShip(
                quest,
                slate,
                tradeAgreement,
                questTag,
                signalToTraderShuttleSentSatisfied,
                out signalToPlayerItemsDelivered,
                out signalToPlayerShuttleKilled
            );
            tradeAgreement.tradeDeliveryMethod = new TradeDeliveryMethod_TransportShip(
                toPlayerTransportShip
            );
        }
        else
        {
            tradeAgreement.tradeDeliveryMethod = new TradeDeliveryMethod_DropPod(tradeAgreement);

            signalToPlayerItemsDelivered = QuestGenUtility.HardcodedSignalWithQuestID(
                $"SentSatisfied"
            );
            PreTrade(tradeAgreement);
            quest
                .Delay(
                    500,
                    () =>
                    {
                        _ = quest.TradeDropPods(tradeAgreement);
                        quest.SignalPass(null, null, signalToPlayerItemsDelivered);
                    },
                    signalToTraderShuttleSentSatisfied
                )
                .debugLabel = "toPlayerDropPods delay";
        }

        _ = quest.CancelTrade(toTraderShuttle, outSignalCancelled: cancelTradeSignal);
        quest.Signal(
            cancelTradeSignal,
            () =>
            {
                quest.CancelTransportShip(toTraderTransportShip);
                _ = quest.ReturnBoughtItemsToTradeShip(tradeAgreement);
                _ = quest.Letter(
                    LetterDefOf.NeutralEvent,
                    text: "[cancelledTradeLetterText]",
                    label: "[cancelledTradeLetterLabel]"
                );
                _ = quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do if the player fails to load the shuttle on time
        var ticksUntilShuttleDeparture = !tradeAgreement.tradePausesDepartureTimer
            ? QuestUtils.CheckTradeShipRequiresGraceTime(quest, slate, tradeAgreement.tradeShip)
            : tradeAgreement.tradeShip.ticksUntilDeparture;
        var shuttleDepartureExpiredSignal = QuestGenUtility.HardcodedSignalWithQuestID(
            "ShuttleDepartureExpired"
        );
        _ = quest.TradeShuttleLeaveDelay(
            toTraderShuttle,
            tradeAgreement.tradePausesDepartureTimer,
            tradeAgreement.tradeShip.TraderName,
            ticksUntilShuttleDeparture,
            inSignalsDisable: Gen.YieldSingle(signalToTraderShuttleSentSatisfied),
            outSignalComplete: shuttleDepartureExpiredSignal
        );

        // Rather than dumping everything and failing outright, offer to renegotiate the trade
        // down to whatever's already loaded, so the player isn't punished for a
        // partially-completed haul - see QuestPart_TryBeginDepartureRenegotiation. Falls through
        // to the dump-and-fail behavior below (via departureRenegotiationFailedSignal) if there's
        // nothing loaded to salvage, or no negotiator available to renegotiate.
        var departureRenegotiationFailedSignal = QuestGenUtility.HardcodedSignalWithQuestID(
            "DepartureRenegotiationFailed"
        );
        _ = quest.TryBeginDepartureRenegotiation(
            tradeAgreement,
            departureRenegotiationFailedSignal,
            inSignal: shuttleDepartureExpiredSignal
        );

        quest.Signal(
            departureRenegotiationFailedSignal,
            () =>
            {
                quest.CancelTransportShip(toTraderTransportShip);
                _ = quest.ReturnBoughtItemsToTradeShip(tradeAgreement);
                _ = quest.Letter(
                    LetterDefOf.NeutralEvent,
                    text: "[expiredTradeLetterText]",
                    label: "[expiredTradeLetterLabel]"
                );
                _ = quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do if the player destroys the first shuttle
        _ = quest.ReturnBoughtItemsToTradeShip(tradeAgreement, signalToTraderShuttleKilled);

        // What to do if the player destroys either shuttle
        quest.AnySignal(
            signalToPlayerShuttleKilled != null
                ? [signalToTraderShuttleKilled, signalToPlayerShuttleKilled]
                : [signalToTraderShuttleKilled],
            () =>
            {
                _ = quest.Letter(
                    LetterDefOf.NegativeEvent,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly,
                    text: "[tradeShuttleKilledLetterText]",
                    label: "[tradeShuttleKilledLetterLabel]"
                );
                _ = quest.BlacklistPlayerFactionInOrbitalTrade();
                _ = quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do if the player becomes blacklisted externally (i.e. destroyed another trader's shuttle)
        _ = quest.Blacklisted(
            inSignalDisable: signalToTraderShuttleSentSatisfied,
            complete: () =>
            {
                quest.CancelTransportShip(toTraderTransportShip);
                _ = quest.ReturnBoughtItemsToTradeShip(tradeAgreement);
                _ = quest.Letter(
                    LetterDefOf.NegativeEvent,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly,
                    text: "[blacklistedLetterText]",
                    label: "[blacklistedLetterLabel]"
                );
                _ = quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                _ = quest.End(
                    QuestEndOutcome.Fail,
                    signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                );
            }
        );

        // What to do when the player has loaded the shuttle appropriately
        quest.Signal(
            signalToTraderShuttleSentSatisfied,
            () =>
            {
                _ = quest.TransferItemsToTrader(tradeAgreement, toTraderTransportShip);
                _ = quest.ExtendTradeShipDepartureIfVeryShort(tradeAgreement.tradeShip);
            }
        );

        // What to do when the trader has dropped off the items to the player
        quest.Signal(
            signalToPlayerItemsDelivered,
            () =>
            {
                // Put in a small delay so the quest finishes slightly later than immediately after the
                // shuttle starts taking off
                quest
                    .Delay(
                        500,
                        () =>
                        {
                            _ = quest.Letter(
                                LetterDefOf.PositiveEvent,
                                text: "[tradeSuccessLetterText]",
                                label: "[tradeSuccessLetterLabel]"
                            );
                            _ = quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                            _ = quest.End(
                                QuestEndOutcome.Success,
                                signalListenMode: QuestPart.SignalListenMode.OngoingOnly
                            );
                        }
                    )
                    .debugLabel = "toPlayerItemsDelivered delay";
            }
        );
    }

    private static (Thing shuttle, TransportShip transportShip) SetupToTraderTransportShip(
        Quest quest,
        Slate slate,
        TradeAgreement tradeAgreement,
        string questTag,
        out string signalToTraderShuttleSentSatisfied,
        out string signalToTraderShuttleKilled
    )
    {
        var shuttle = QuestUtils.SetupShuttle(
            "toTraderShuttle",
            slate,
            questTag,
            out signalToTraderShuttleSentSatisfied,
            out signalToTraderShuttleKilled
        );
        var compTradeShuttle = shuttle.TryGetComp<CompTradeShuttle>();
        compTradeShuttle.isToTrader = true;
        compTradeShuttle.tradeAgreement = tradeAgreement;
        var compShuttle = shuttle.TryGetComp<CompShuttle>();
        compShuttle.requiredPawns = tradeAgreement.pawnsSoldToTrader;
        foreach (var thingCount in tradeAgreement.thingsSoldToTrader)
        {
            Utils.AddThingToLoadToShuttle(thingCount, compTradeShuttle, compShuttle);
        }

        var transportShip = quest
            .GenerateTransportShip(TransportShipDefOf.Ship_Shuttle, [], shuttle)
            .transportShip;
        slate.Set("toTraderTransportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        var map = tradeAgreement.tradeShip.Map;
        var landingSpot = QuestUtils.FindLandingSpot(map);

        _ = quest.Letter(
            LetterDefOf.PositiveEvent,
            text: "[tradeAcceptedLetterText]",
            label: "[tradeAcceptedLetterLabel]"
        );
        _ = quest.AddShipJob_Arrive(
            transportShip,
            map.Parent,
            null,
            landingSpot,
            ShipJobStartMode.Queue
        );
        quest.AddShipJob_WaitForever(transportShip, true, false).showGizmos = false;

        return (shuttle, transportShip);
    }

    private static (Thing shuttle, TransportShip transportShip) SetupToPlayerTransportShip(
        Quest quest,
        Slate slate,
        TradeAgreement tradeAgreement,
        string questTag,
        string signalToTraderShuttleSentSatisfied,
        out string signalToPlayerShuttleSentSatisfied,
        out string signalToPlayerShuttleKilled
    )
    {
        var shuttle = QuestUtils.SetupShuttle(
            "toPlayerShuttle",
            slate,
            questTag,
            out signalToPlayerShuttleSentSatisfied,
            out signalToPlayerShuttleKilled
        );

        PreTrade(tradeAgreement);

        var transportShip = quest
            .GenerateTransportShip(
                TransportShipDefOf.Ship_Shuttle,
                tradeAgreement.thingsSoldToPlayer,
                shuttle
            )
            .transportShip;
        slate.Set("toPlayerTransportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        var map = tradeAgreement.tradeShip.Map;

        // Put in a small delay so the shuttles don't come back to back
        quest
            .Delay(
                500,
                () =>
                {
                    _ = quest.AddShipJob_Arrive_FindLandingSpot_JIT(
                        transportShip,
                        map.Parent,
                        null,
                        ShipJobStartMode.Queue
                    );
                    _ = quest.AddShipJob_Unload(transportShip);
                    _ = quest.AddShipJob_WaitTime(transportShip, 0, true);
                },
                signalToTraderShuttleSentSatisfied
            )
            .debugLabel = "toPlayerTransportShip arrival delay";

        return (shuttle, transportShip);
    }

    private static void PreTrade(TradeAgreement tradeAgreement)
    {
        foreach (var thing in tradeAgreement.thingsSoldToPlayer)
        {
            if (thing is Pawn pawn)
            {
                if (!pawn.IsWorldPawn())
                {
                    Find.WorldPawns.PassToWorld(pawn);
                }
                pawn.PreTraded(
                    TradeAction.PlayerBuys,
                    tradeAgreement.negotiator,
                    tradeAgreement.tradeShip
                );
                pawn.psychicEntropy?.SetInitialPsyfocusLevel();
            }
        }
    }

    /// <inheritdoc/>
    protected override bool TestRunInt(Slate slate)
    {
        if (slate == null)
        {
            throw new ArgumentNullException(nameof(slate));
        }
        var tradeAgreement = slate.Get<TradeAgreement>("tradeAgreement");

        return tradeAgreement.tradeShip.GetData().ticksUntilCommsClosed != 0;
    }
}
