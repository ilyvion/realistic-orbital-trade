using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
internal static class Rimworld_TradeDeal_TryExecute
{
    private static void Postfix(bool __result, bool actuallyTraded)
    {
        var tradeAgreementForQuest = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreementForQuest == null)
        {
            return;
        }


        if (__result)
        {
            if (actuallyTraded)
            {
                Slate slate = new();
                slate.Set("tradeAgreement", tradeAgreementForQuest);
                slate.Set("traderName", tradeAgreementForQuest.tradeShip.TraderName);

                QuestUtility.GenerateQuestAndMakeAvailable(QuestScriptDefOf.ROT_TradeShipTransportShip, slate);
            }
            else
            {
                RealisticOrbitalTradeGameComponent.Current.EndTradeAgreement(tradeAgreementForQuest);
            }
            TradeShipData.tradeAgreementForQuest = null;
        }
    }
}
