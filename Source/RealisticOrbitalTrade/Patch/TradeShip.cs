
using System.Collections.Generic;
using HarmonyLib;
using RealisticOrbitalTrade.Dialogs;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ExposeData))]
internal static class Rimworld_TradeShip_ExposeData
{
    private static void Postfix(TradeShip __instance)
    {
        var extra = __instance.GetData();
        Scribe_Deep.Look(ref extra, "realisticOrbitalTradeData", new object[] { __instance, false });
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
internal static class Rimworld_TradeShip_GiveSoldThingToPlayer
{
    private static bool Prefix(TradeShip __instance, Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.WarningOnce("Trade ship did not have a trade agreement arranged. Mod incompatibility?", Constants.MissingTradeAgreementKey);
            return true;
        }

        Thing thing = toGive.SplitOff(countToGive);
        if (thing is Pawn item)
        {
            Traverse.Create(__instance).Field("soldPrisoners").GetValue<List<Pawn>>().Remove(item);
        }
        RealisticOrbitalTradeMod.Dev($"Adding {thing.Label} to list of things to give to player on successful trade");
        tradeAgreement.thingsSoldToPlayer.TryAddOrTransfer(thing);

        return false;
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToTrader))]
internal static class Rimworld_TradeShip_GiveSoldThingToTrader
{
    private static bool Prefix(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.WarningOnce("Trade ship did not have a trade agreement arranged. Mod incompatibility?", Constants.MissingTradeAgreementKey);
            return true;
        }

        RealisticOrbitalTradeMod.Dev($"Adding {toGive.LabelCapNoCount} x{countToGive} to list of things player needs to load onto shuttle");
        if (toGive is Pawn pawn)
        {
            tradeAgreement.pawnsSoldToTrader.Add(pawn);
        }
        else
        {
            tradeAgreement.thingsSoldToTrader.Add(new(toGive, countToGive));
        }

        return false;
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.PassingShipTick))]
internal static class Rimworld_TradeShip_PassingShipTick
{
    private static void Prefix(TradeShip __instance, out int __state)
    {
        __state = __instance.ticksUntilDeparture;
    }

    private static void Postfix(TradeShip __instance, int __state)
    {
        var tradeShipExtra = __instance.GetData();
        var activeTradeAgreement = tradeShipExtra.activeTradeAgreement;
        if ((activeTradeAgreement != null && activeTradeAgreement.tradePausesDepartureTimer) || Utils.IsMakingAmends)
        {
            __instance.ticksUntilDeparture = __state;
        }
        else
        {
            if (tradeShipExtra.ticksUntilCommsClosed > 0)
            {
                tradeShipExtra.ticksUntilCommsClosed--;
            }
            else if (tradeShipExtra.ticksUntilCommsClosed == 0 && activeTradeAgreement == null && __instance.ticksUntilDeparture > Constants.AdditionalTicksAfterDeparture)
            {
                __instance.ticksUntilDeparture = Constants.AdditionalTicksAfterDeparture;
            }
        }
    }
}

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.TryOpenComms))]
internal static class Rimworld_TradeShip_TryOpenComms
{
    private static bool Prefix(TradeShip __instance, Pawn negotiator)
    {
        TradeShipData tradeShipExtra = __instance.GetData();
        if (tradeShipExtra.ticksUntilCommsClosed == 0)
        {
            Messages.Message("RealisticOrbitalTrade.NotAnsweringForGraceTime".Translate(__instance.TraderName), MessageTypeDefOf.NeutralEvent, historical: false);
            return false;
        }
        else if (Utils.IsMakingAmends)
        {
            Messages.Message("RealisticOrbitalTrade.NotAnsweringForActiveAmendment".Translate(__instance.TraderName), MessageTypeDefOf.NeutralEvent, historical: false);
            return false;
        }
        else if (RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Blacklisted)
        {
            Find.WindowStack.Add(new Dialog_PayBlacklistRemovalFee(__instance, negotiator));

            float level = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Talking);
            float level2 = negotiator.health.capacities.GetLevel(PawnCapacityDefOf.Hearing);
            if (level < 0.95f || level2 < 0.95f)
            {
                TaggedString text = (!(level < 0.95f)) ? "NegotiatorHearingImpaired".Translate(negotiator.LabelShort, negotiator) : "NegotiatorTalkingImpaired".Translate(negotiator.LabelShort, negotiator);
                text += "\n\n" + "NegotiatorCapacityImpaired".Translate();
                Find.WindowStack.Add(new Dialog_MessageBox(text));
            }
            return false;
        }
        else if (tradeShipExtra.HasActiveTradeAgreement)
        {
            Messages.Message("RealisticOrbitalTrade.NotAnsweringForActiveTrade".Translate(__instance.TraderName), MessageTypeDefOf.NeutralEvent, historical: false);
            return false;
        }

        tradeShipExtra.activeTradeAgreement = TradeShipData.tradeAgreementForQuest = RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(__instance, negotiator);
        return true;
    }
}
