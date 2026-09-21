#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE0062 // Make local function 'static'

using RealisticOrbitalTrade.Dialogs;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ExposeData))]
internal static class Rimworld_TradeShip_ExposeData
{
    private static void Postfix(TradeShip __instance)
    {
        var extra = __instance.GetData();
        Scribe_Deep.Look(ref extra, "realisticOrbitalTradeData", [__instance, false]);
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
internal static class Rimworld_TradeShip_GiveSoldThingToPlayer
{
    private static bool _hasWarnedAboutMissingTradeAgreement;

    private static bool Prefix(
        TradeShip __instance,
        Thing toGive,
        int countToGive,
        Pawn playerNegotiator
    )
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.Instance.LogWarningOnce(
                "Trade ship did not have a trade agreement arranged. Mod incompatibility?",
                ref _hasWarnedAboutMissingTradeAgreement
            );
            return true;
        }

        var thing = toGive.SplitOff(countToGive);
        if (thing is Pawn item)
        {
            _ = __instance.soldPrisoners.Remove(item);
        }
        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Adding {thing.Label} to list of things to give to player on successful trade"
        );
        _ = tradeAgreement.thingsSoldToPlayer.TryAddOrTransfer(thing);

        return false;
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToTrader))]
internal static class Rimworld_TradeShip_GiveSoldThingToTrader
{
    private static bool _hasWarnedAboutMissingTradeAgreement;

    private static bool Prefix(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            if (!Utils.IsBuyingMoreTime)
            {
                RealisticOrbitalTradeMod.Instance.LogWarningOnce(
                    "Trade ship did not have a trade agreement arranged. Mod incompatibility?",
                    ref _hasWarnedAboutMissingTradeAgreement
                );
            }
            return true;
        }

        RealisticOrbitalTradeMod.Instance.LogDevMessage(() =>
            $"Adding {toGive.LabelCapNoCount} x{countToGive} to list of things player needs to load onto shuttle"
        );
        if (toGive is Pawn pawn)
        {
            tradeAgreement.pawnsSoldToTrader.Add(pawn);
        }
        else
        {
            tradeAgreement.thingsSoldToTrader.Add(new(toGive, countToGive));
            tradeAgreement.AddReservation(toGive, countToGive);
        }

        return false;
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.PassingShipTick))]
internal static class Rimworld_TradeShip_PassingShipTick
{
    private static void Prefix(TradeShip __instance, out int __state) =>
        __state = __instance.ticksUntilDeparture;

    private static void Postfix(TradeShip __instance, int __state)
    {
        var tradeShipExtra = __instance.GetData();
        var activeTradeAgreement = tradeShipExtra.activeTradeAgreement;
        (__instance.ticksUntilDeparture, tradeShipExtra.ticksUntilCommsClosed) =
            AdvanceTradeShipTimers(
                __state,
                __instance.ticksUntilDeparture,
                tradeShipExtra.ticksUntilCommsClosed,
                activeTradeAgreement != null && activeTradeAgreement.tradePausesDepartureTimer,
                activeTradeAgreement != null,
                Utils.IsMakingAmends,
                Constants.AdditionalTicksAfterDeparture
            );
    }

    internal static (int ticksUntilDeparture, int ticksUntilCommsClosed) AdvanceTradeShipTimers(
        int departureBefore,
        int departureAfterVanillaTick,
        int commsClosed,
        bool activeAgreementPauses,
        bool hasActiveAgreement,
        bool makingAmends,
        int additionalTicks
    )
    {
        if (activeAgreementPauses || makingAmends)
        {
            return (departureBefore, commsClosed);
        }

        if (commsClosed > 0)
        {
            return (departureAfterVanillaTick, commsClosed - 1);
        }

        if (commsClosed == 0 && !hasActiveAgreement && departureAfterVanillaTick > additionalTicks)
        {
            return (additionalTicks, commsClosed);
        }

        return (departureAfterVanillaTick, commsClosed);
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.TryOpenComms))]
internal static class Rimworld_TradeShip_TryOpenComms
{
    private static bool Prefix(TradeShip __instance, Pawn negotiator)
    {
        var tradeShipExtra = __instance.GetData();

        // Renegotiation reopens comms on a shuttle whose deal is already active - the usual
        // grace-time/amends/blacklist/already-trading gates below either don't apply (this isn't
        // a *new* trade) or would incorrectly block reopening the very trade they're guarding.
        if (Utils.RenegotiatingAgreement != null)
        {
            tradeShipExtra.activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
                Utils.RenegotiatingAgreement;
            return true;
        }

        switch (
            DecideComms(
                tradeShipExtra.ticksUntilCommsClosed,
                Utils.IsMakingAmends,
                RealisticOrbitalTradeGameComponent.Current.Standing,
                tradeShipExtra.HasActiveTradeAgreement
            )
        )
        {
            case CommsOutcome.StartAgreement:
                break;
            case CommsOutcome.NotAnsweringForGraceTime:
                Messages.Message(
                    "RealisticOrbitalTrade.NotAnsweringForGraceTime".Translate(
                        __instance.TraderName
                    ),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false
                );
                return false;
            case CommsOutcome.NotAnsweringForActiveAmendment:
                Messages.Message(
                    "RealisticOrbitalTrade.NotAnsweringForActiveAmendment".Translate(
                        __instance.TraderName
                    ),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false
                );
                return false;
            case CommsOutcome.Blacklisted:
                Find.WindowStack.Add(new Dialog_PayBlacklistRemovalFee(__instance, negotiator));

                var level = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Talking);
                var level2 = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
                if (level < 0.95f || level2 < 0.95f)
                {
                    var text =
                        (!(level < 0.95f))
                            ? "NegotiatorHearingImpaired".Translate(
                                negotiator.LabelShort,
                                negotiator
                            )
                            : "NegotiatorTalkingImpaired".Translate(
                                negotiator.LabelShort,
                                negotiator
                            );
                    text += "\n\n" + "NegotiatorCapacityImpaired".Translate();
                    Find.WindowStack.Add(new Dialog_MessageBox(text));
                }
                return false;
            case CommsOutcome.NotAnsweringForActiveTrade:
                Messages.Message(
                    "RealisticOrbitalTrade.NotAnsweringForActiveTrade".Translate(
                        __instance.TraderName
                    ),
                    MessageTypeDefOf.NeutralEvent,
                    historical: false
                );
                return false;
            default:
                break;
        }

        tradeShipExtra.activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
            RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(__instance, negotiator);
        return true;
    }

    internal enum CommsOutcome
    {
        StartAgreement,
        NotAnsweringForGraceTime,
        NotAnsweringForActiveAmendment,
        Blacklisted,
        NotAnsweringForActiveTrade,
    }

    internal static CommsOutcome DecideComms(
        int commsClosed,
        bool makingAmends,
        Standing standing,
        bool hasActiveAgreement
    ) =>
        commsClosed == 0 ? CommsOutcome.NotAnsweringForGraceTime
        : makingAmends ? CommsOutcome.NotAnsweringForActiveAmendment
        : standing == Standing.Blacklisted ? CommsOutcome.Blacklisted
        : hasActiveAgreement ? CommsOutcome.NotAnsweringForActiveTrade
        : CommsOutcome.StartAgreement;
}
