using System;
using System.Linq;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;
using UnityEngine;
using Verse;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
[HotSwappable]
internal static class Rimworld_TradeDeal_TryExecute
{
    private static bool Prefix(TradeDeal __instance, ref bool actuallyTraded, ref bool __result)
    {
        if (!Settings._useMinimumTradeThreshold)
        {
            return true;
        }

        var tradeAgreementForQuest = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreementForQuest == null)
        {
            return true;
        }

        var combinedTradeValue = 0f;
        foreach (var item in __instance.AllTradeables.Where(t => t.CountToTransfer != 0))
        {
            if (item.IsCurrency)
            {
                continue;
            }
            combinedTradeValue += Math.Abs(item.CurTotalCurrencyCostForDestination);
        }

        int minimumTradeThreshold = tradeAgreementForQuest.tradeShip.GetData().minimumTradeThreshold;
        if (combinedTradeValue < minimumTradeThreshold)
        {
            Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime = Time.time;
            Messages.Message(
                "RealisticOrbitalTrade.TradeLessThanMinimumTradeThreshold".Translate(
                    combinedTradeValue.ToStringMoney(),
                    ((float)minimumTradeThreshold).ToStringMoney()),
                MessageTypeDefOf.RejectInput,
                historical: false);

            actuallyTraded = false;
            __result = false;
            return false;
        }

        return true;
    }
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
