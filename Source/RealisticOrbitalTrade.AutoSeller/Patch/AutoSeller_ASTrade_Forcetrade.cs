using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.AutoSeller.Patch;

[HarmonyPatch]
internal static class AutoSeller_ASTrade_Forcetrade
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method("RWAutoSell.ASTrade:Forcetrade");

    internal static void Postfix(bool actuallyTraded)
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

            _ = QuestUtility.GenerateQuestAndMakeAvailable(
                QuestScriptDefOf.ROT_TradeShipTransportShip,
                slate
            );
        }
        else
        {
            RealisticOrbitalTradeGameComponent.Current.EndTradeAgreement(tradeAgreementForQuest);
        }
        TradeShipData.tradeAgreementForQuest = null;
    }
}
