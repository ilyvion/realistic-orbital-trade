using RealisticOrbitalTrade.Comps;
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

        // Renegotiation reopens the real trade dialog over an *existing* deal, so it can't let
        // original TryExecute/ResolveTrade run: those always transfer a tradeable's absolute
        // CountToTransfer from current availability, with no concept of "part of this was already
        // delivered" - see TradeAgreement.ApplyRenegotiation for the delta-based replacement.
        if (tradeAgreementForQuest.tradeDeliveryMethod != null)
        {
            var newTradeables = __instance.AllTradeables;
            if (TradeAgreement.IsFullCancellation(newTradeables))
            {
                var toTraderCompTradeShuttle =
                    tradeAgreementForQuest.toTraderTransportShip?.shipThing.TryGetComp<CompTradeShuttle>();
                Find.WindowStack.Add(
                    Dialog_MessageBox.CreateConfirmation(
                        "RealisticOrbitalTrade.DidYouMeanToCancel".Translate(),
                        () =>
                        {
                            if (toTraderCompTradeShuttle?.cancelTradeAction != null)
                            {
                                toTraderCompTradeShuttle.cancelTradeAction.Invoke();
                            }
                            else
                            {
                                RealisticOrbitalTradeMod.Instance.LogError(
                                    "Can't cancel trade; cancelTradeAction is null!"
                                );
                            }
                        }
                    )
                );
                actuallyTraded = false;
                __result = false;
                return false;
            }

            var originalTradeables =
                Utils.RenegotiationOriginalTradeables ?? tradeAgreementForQuest.AllTradeables;
            actuallyTraded = tradeAgreementForQuest.ApplyRenegotiation(
                originalTradeables,
                newTradeables
            );
            __result = true;
            return false;
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
        if (!__result)
        {
            return;
        }

        // Renegotiation reuses the trade agreement/shuttle that's already in flight - no new
        // quest to generate, just clean up the renegotiation session.
        if (tradeAgreementForQuest.tradeDeliveryMethod != null)
        {
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();
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
