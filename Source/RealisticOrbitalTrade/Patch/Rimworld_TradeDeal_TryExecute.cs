using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
[HotSwappable]
internal static class Rimworld_TradeDeal_TryExecute
{
    private static bool Prefix(TradeDeal __instance, ref bool actuallyTraded, ref bool __result)
    {
        var tradeAgreementForQuest = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreementForQuest == null)
        {
            return true;
        }

        if (Settings._useMinimumTradeThreshold)
        {
            var combinedTradeValue = 0f;
            foreach (var item in __instance.AllTradeables.Where(t => t.CountToTransfer != 0))
            {
                if (item.IsCurrency)
                {
                    continue;
                }
                combinedTradeValue += Math.Abs(item.CurTotalCurrencyCostForDestination);
            }

            var minimumTradeThreshold = tradeAgreementForQuest
                .tradeShip.GetData()
                .minimumTradeThreshold;
            if (combinedTradeValue < minimumTradeThreshold)
            {
                Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime = Time.time;
                Messages.Message(
                    "RealisticOrbitalTrade.TradeLessThanMinimumTradeThreshold".Translate(
                        combinedTradeValue.ToStringMoney(),
                        ((float)minimumTradeThreshold).ToStringMoney()
                    ),
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );

                actuallyTraded = false;
                __result = false;
                return false;
            }
        }

        // The shuttle that will haul what the colony sells has a real, fixed mass capacity
        // (CompProperties_Transporter.massCapacity on the Shuttle ThingDef) - it's not a balance
        // preference like the minimum trade threshold, so this is enforced unconditionally.
        var outgoingMass = Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(__instance);
        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        if (outgoingMass > massCapacity)
        {
            Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = Time.time;
            Messages.Message(
                "RealisticOrbitalTrade.TradeExceedsShuttleMassCapacity".Translate(
                    outgoingMass.ToStringEnsureThreshold(massCapacity, 1),
                    massCapacity.ToString("F0", CultureInfo.CurrentCulture)
                ),
                MessageTypeDefOf.RejectInput,
                historical: false
            );

            actuallyTraded = false;
            __result = false;
            return false;
        }

        // Items bought from the trader also arrive by shuttle, and are subject to the same mass
        // capacity - unless the player has opted to receive them via drop pods instead, which
        // aren't mass-limited.
        if (!tradeAgreementForQuest.deliverItemsViaDropPod)
        {
            var incomingMass = Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(
                __instance
            );
            if (incomingMass > massCapacity)
            {
                Rimworld_Dialog_Trade_DoWindowContents.lastIncomingMassFlashTime = Time.time;
                Messages.Message(
                    "RealisticOrbitalTrade.TradeExceedsIncomingShuttleMassCapacity".Translate(
                        incomingMass.ToStringEnsureThreshold(massCapacity, 1),
                        massCapacity.ToString("F0", CultureInfo.CurrentCulture)
                    ),
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );

                actuallyTraded = false;
                __result = false;
                return false;
            }
        }

        return true;
    }

    private static void Postfix(bool __result, bool actuallyTraded)
    {
        var tradeAgreementForQuest = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreementForQuest == null)
        {
            RealisticOrbitalTradeMod.Instance.LogDevMessage(
                "Trade agreement for quest is null in TradeDeal.TryExecute postfix."
            );
            return;
        }

        RealisticOrbitalTradeMod.Instance.LogDevMessage(
            $"TradeDeal.TryExecute postfix: __result={__result}, actuallyTraded={actuallyTraded}, tradeAgreementForQuest={tradeAgreementForQuest}"
        );
        if (__result)
        {
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
                RealisticOrbitalTradeGameComponent.Current.EndTradeAgreement(
                    tradeAgreementForQuest
                );
            }
            TradeShipData.tradeAgreementForQuest = null;
        }
    }
}
