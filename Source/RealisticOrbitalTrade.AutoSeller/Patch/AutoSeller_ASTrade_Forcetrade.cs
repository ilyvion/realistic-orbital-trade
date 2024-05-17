using System.Reflection;
using HarmonyLib;
using RimWorld;
using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.AutoSeller.Patch;

[HarmonyPatch]
internal static class AutoSeller_ASTrade_Forcetrade
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method("RWAutoSell.ASTrade:Forcetrade");
    }

    private static void Postfix(bool actuallyTraded)
    {
        var tradeAgreementForQuest = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreementForQuest == null)
        {
            return;
        }

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
