using System.Linq;
using RealisticOrbitalTrade.Comps;
using RimWorld;
using RimWorld.Planet;
using RimWorld.QuestGen;
using Verse;

namespace RealisticOrbitalTrade.Quests;

public class QuestNode_Root_TradeShipTransportShip : QuestNode
{
    protected override void RunInt()
    {
        var quest = QuestGen.quest;
        var slate = QuestGen.slate;

        var tradeAgreement = slate.Get<TradeAgreement>("tradeAgreement")!;

        var questTag = QuestGenUtility.HardcodedTargetQuestTagWithQuestID("ROT_TradeShipTransportShip");
        var cancelTradeSignal = QuestGenUtility.HardcodedSignalWithQuestID("CancelTrade");

        (var toTraderShuttle, var toTraderTransportShip) = SetupToTraderTransportShip(quest, slate, tradeAgreement, questTag,
            out var signalToTraderShuttleSentSatisfied, out var signalToTraderShuttleKilled);

        (var toPlayerShuttle, var toPlayerTransportShip) = SetupToPlayerTransportShip(quest, slate, tradeAgreement, questTag,
            signalToTraderShuttleSentSatisfied, out var signaltoPlayerShuttleSentSatisfied, out var signalToPlayerShuttleKilled);

        quest.CancelTrade(toTraderShuttle, outSignalCancelled: cancelTradeSignal);
        quest.Signal(cancelTradeSignal, () =>
        {
            quest.CancelTransportShip(toTraderTransportShip);
            quest.ReturnBoughtItemsToTradeShip(tradeAgreement, toPlayerTransportShip);
            quest.Letter(LetterDefOf.NeutralEvent, text: "[cancelledTradeLetterText]", label: "[cancelledTradeLetterLabel]");
            quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
            quest.End(QuestEndOutcome.Fail, signalListenMode: QuestPart.SignalListenMode.OngoingOnly);
        });

        // What to do if the player fails to load the shuttle on time
        var ticksUntilShuttleDeparture = QuestUtils.CheckTradeShipRequiresGraceTime(quest, slate, tradeAgreement.tradeShip);
        quest.ShuttleLeaveDelay(toTraderShuttle, ticksUntilShuttleDeparture, null, Gen.YieldSingle(signalToTraderShuttleSentSatisfied), null, () =>
        {
            quest.CancelTransportShip(toTraderTransportShip);
            quest.Letter(LetterDefOf.NeutralEvent, text: "[expiredTradeLetterText]", label: "[expiredTradeLetterLabel]");
            quest.ReturnBoughtItemsToTradeShip(tradeAgreement, toPlayerTransportShip);
            quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
            quest.End(QuestEndOutcome.Fail, signalListenMode: QuestPart.SignalListenMode.OngoingOnly);
        });

        // What to do if the player destroys the first shuttle
        quest.ReturnBoughtItemsToTradeShip(tradeAgreement, toPlayerTransportShip, signalToTraderShuttleKilled);

        // What to do if the player destroys either shuttle
        quest.AnySignal(new[] { signalToTraderShuttleKilled, signalToPlayerShuttleKilled }, () =>
        {
            quest.Letter(LetterDefOf.NegativeEvent, signalListenMode: QuestPart.SignalListenMode.OngoingOnly, text: "[tradeShuttleKilledLetterText]", label: "[tradeShuttleKilledLetterLabel]");
            quest.BlacklistPlayerFactionInOrbitalTrade();
            quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
            quest.End(QuestEndOutcome.Fail, signalListenMode: QuestPart.SignalListenMode.OngoingOnly);
        });

        // What to do when the player has loaded the shuttle appropriately
        quest.Signal(signalToTraderShuttleSentSatisfied, () =>
        {
            quest.TransferItemsToTrader(tradeAgreement, toTraderTransportShip);
            quest.ExtendTradeShipDepartureIfVeryShort(tradeAgreement.tradeShip);
        });

        // What to do when the trader has dropped off the items to the player
        quest.Signal(signaltoPlayerShuttleSentSatisfied, () =>
        {
            // Put in a small delay so the quest finishes slightly later than immediately after the
            // shuttle starts taking off
            quest.Delay(500, () =>
            {
                quest.Letter(LetterDefOf.PositiveEvent, text: "[tradeSuccessLetterText]", label: "[tradeSuccessLetterLabel]");
                quest.EndActiveTradeShipTradeAgreement(tradeAgreement);
                quest.End(QuestEndOutcome.Success, signalListenMode: QuestPart.SignalListenMode.OngoingOnly);
            }).debugLabel = "toPlayerTransportShip departure delay";
        });
    }

    private static (Thing shuttle, TransportShip transportShip) SetupToTraderTransportShip(
        Quest quest,
        Slate slate,
        TradeAgreement tradeAgreement,
        string questTag,
        out string signalToTraderShuttleSentSatisfied,
        out string signalToTraderShuttleKilled)
    {
        var shuttle = QuestUtils.SetupShuttle("toTraderShuttle", slate, questTag, out signalToTraderShuttleSentSatisfied, out signalToTraderShuttleKilled);
        var compTradeShuttle = shuttle.TryGetComp<CompTradeShuttle>();
        compTradeShuttle.isToTrader = true;
        compTradeShuttle.tradeAgreement = tradeAgreement;
        var compShuttle = shuttle.TryGetComp<CompShuttle>();
        compShuttle.requiredPawns = tradeAgreement.pawnsSoldToTrader;
        foreach (var thingCount in tradeAgreement.thingsSoldToTrader)
        {
            var thing = thingCount.thing;
            bool healthAffectsPrice = thing.def.healthAffectsPrice;
            bool hasQuality = QualityUtility.TryGetQuality(thing, out var quality);
            if (healthAffectsPrice || hasQuality)
            {
                compTradeShuttle.requiredSpecificItems.Add(new ThingDefCountWithRequirements
                {
                    def = thing.def,
                    count = thingCount.Count,
                    healthAffectsPrice = healthAffectsPrice,
                    hitPoints = thing.HitPoints,
                    hasQuality = hasQuality,
                    quality = quality
                });
            }
            else
            {
                compShuttle.requiredItems.Add(new ThingDefCount(thingCount.thing.def, thingCount.Count));
            }
        }

        var transportShip = quest.GenerateTransportShip(TransportShipDefOf.Ship_Shuttle, Enumerable.Empty<Thing>(), shuttle).transportShip;
        slate.Set("toTraderTransportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        var map = tradeAgreement.tradeShip.Map;
        IntVec3 landingSpot = QuestUtils.FindLandingSpot(map);

        quest.Letter(LetterDefOf.PositiveEvent, text: "[tradeAcceptedLetterText]", label: "[tradeAcceptedLetterLabel]");
        quest.AddShipJob_Arrive(transportShip, map.Parent, null, landingSpot, ShipJobStartMode.Queue);
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
        out string signalToPlayerShuttleKilled)
    {
        var shuttle = QuestUtils.SetupShuttle("toPlayerShuttle", slate, questTag, out signalToPlayerShuttleSentSatisfied, out signalToPlayerShuttleKilled);

        foreach (var thing in tradeAgreement.thingsSoldToPlayer)
        {
            if (thing is Pawn pawn)
            {
                if (!pawn.IsWorldPawn())
                {
                    Find.WorldPawns.PassToWorld(pawn);
                }
                pawn.PreTraded(TradeAction.PlayerBuys, tradeAgreement.negotiator, tradeAgreement.tradeShip);
                pawn.psychicEntropy?.SetInitialPsyfocusLevel();
            }
        }

        var transportShip = quest.GenerateTransportShip(TransportShipDefOf.Ship_Shuttle, tradeAgreement.thingsSoldToPlayer, shuttle).transportShip;
        slate.Set("toPlayerTransportShip", transportShip);
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        var map = tradeAgreement.tradeShip.Map;
        IntVec3 landingSpot = QuestUtils.FindLandingSpot(map);

        // Put in a small delay so the shuttles don't come back to back
        quest.Delay(500, () =>
        {
            quest.AddShipJob_Arrive(transportShip, map.Parent, null, landingSpot, ShipJobStartMode.Queue);
            quest.AddShipJob_Unload(transportShip);
            quest.AddShipJob_WaitTime(transportShip, 0, true);
        }, signalToTraderShuttleSentSatisfied).debugLabel = "toPlayerTransportShip arrival delay";

        return (shuttle, transportShip);
    }

    protected override bool TestRunInt(Slate slate)
    {
        var tradeAgreement = slate.Get<TradeAgreement>("tradeAgreement")!;

        return tradeAgreement.tradeShip.GetData().ticksUntilCommsClosed != 0;
    }
}
