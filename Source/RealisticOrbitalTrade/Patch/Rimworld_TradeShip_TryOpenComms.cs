using HarmonyLib;
using RimWorld;
using Verse;
using RealisticOrbitalTrade.Dialogs;

namespace RealisticOrbitalTrade.Patch;

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
        else if (Find.QuestManager.QuestsListForReading.Any(q => q.root == QuestScriptDefOf.ROT_TradeShipMakeAmends && !q.Historical))
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
        else if (tradeShipExtra.activeTradeAgreement != null)
        {
            Messages.Message("RealisticOrbitalTrade.NotAnsweringForActiveTrade".Translate(__instance.TraderName), MessageTypeDefOf.NeutralEvent, historical: false);
            return false;
        }

        tradeShipExtra.activeTradeAgreement = TradeShipData.tradeAgreementForQuest = RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(__instance, negotiator);
        return true;
    }
}
