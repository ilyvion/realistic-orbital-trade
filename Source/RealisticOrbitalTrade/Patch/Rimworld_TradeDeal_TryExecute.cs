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

        // What the colony sells is divided across as many shuttles as it takes, but each shuttle
        // has a real, fixed mass capacity (CompProperties_Transporter.massCapacity on the Shuttle
        // ThingDef) - it's not a balance preference like the minimum trade threshold, so this is
        // enforced unconditionally. A renegotiation can't send more shuttles than were sent.
        var outgoingMass = Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(__instance);
        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        var outgoingPlan = Rimworld_Dialog_Trade_DoWindowContents.PlanOutgoingShuttles(__instance);
        var shuttleLimit = Rimworld_Dialog_Trade_DoWindowContents.OutgoingShuttleLimit;
        if (
            Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                outgoingPlan,
                shuttleLimit
            )
        )
        {
            Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = Time.time;
            Messages.Message(
                shuttleLimit is { } limit
                    ? "RealisticOrbitalTrade.TradeExceedsShuttleMassCapacity".Translate(
                        outgoingMass.ToStringEnsureThreshold(limit * massCapacity, 1),
                        (limit * massCapacity).ToString("F0", CultureInfo.CurrentCulture)
                    )
                    : "RealisticOrbitalTrade.TradeContainsItemTooHeavyForShuttle".Translate(
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
            // Vanilla's Dialog_Trade recalculates the silver tradeable every frame
            // (TradeDeal.UpdateCurrencyCount, called from Dialog_Trade.DoWindowContents) so it's
            // always current by the time Accept is clicked - but DTI's Window_DynamicTrade only
            // does this inside its own throttled refresh, not unconditionally, so the silver
            // tradeable can still be showing a stale (often untouched-since-original-trade) amount
            // the moment Accept is clicked there. Doing it here directly, before diffing below,
            // means the comparison always sees what the deal actually needs regardless of which
            // trade UI is open - without it, a renegotiation the player left untouched (silver
            // tradeable still at its pre-renegotiation value) shows no diff from
            // RenegotiationOriginalTradeables at all, and the ToTrader-increase check further down
            // never has anything to catch.
            __instance.UpdateCurrencyCount();

            // Vanilla's own TradeDeal.TryExecute refuses to run at all once the colony can't
            // cover what's owed (CurrencyTradeable.CountPostDealFor(Colony) < 0) - but that check
            // lives inside the very method body this branch replaces wholesale (see the big
            // comment above), so without repeating it here a renegotiation the player can't
            // afford sails straight into ApplyRenegotiation and tries to pull more silver off the
            // colony than it actually has.
            if (
                __instance.CurrencyTradeable == null
                || __instance.CurrencyTradeable.CountPostDealFor(Transactor.Colony) < 0
            )
            {
                // Setting the static directly (rather than Dialog_Trade.FlashSilver(), an instance
                // method) flashes the currency row under DTI too - DTI's own Window_DynamicTrade.
                // DrawCurrencyRow reads this exact same vanilla field, and WindowOfType<Dialog_Trade>
                // finds nothing to call FlashSilver() on when DTI, not the vanilla dialog, is open.
                Dialog_Trade.lastCurrencyFlashTime = Time.time;
                Messages.Message(
                    "MessageColonyCannotAfford".Translate(),
                    MessageTypeDefOf.RejectInput,
                    historical: false
                );

                actuallyTraded = false;
                __result = false;
                return false;
            }

            var newTradeables = __instance.AllTradeables;
            var originalTradeables =
                Utils.RenegotiationOriginalTradeables ?? tradeAgreementForQuest.AllTradeables;

            // A departure-triggered renegotiation only offers the player a chance to adjust what
            // they're buying to match what's already loaded - they can't sell anything further at
            // this stage (see Utils.TryBeginDepartureRenegotiation). This includes silver: paying
            // more silver toward the trader still means physically loading more of it onto the
            // shuttle, which is exactly as impossible post-departure as loading more of any other
            // good - the player can only balance the deal by buying less, not by paying more.
            // ComputeRenegotiationChanges tags a tradeable ToTrader for any change to the sold
            // side, including reducing it (e.g. handing over less of the loaded silver than the
            // full amount) - amount > 0 is what actually distinguishes a genuine increase from a
            // harmless reduction; see its "negative number: colony gets less" comment.
            if (Utils.RenegotiatingBecauseDeparting)
            {
                var allChanges = TradeAgreement.ComputeRenegotiationChanges(
                    originalTradeables,
                    newTradeables
                );
                RealisticOrbitalTradeMod.Instance.LogDevMessage(
                    "Departure renegotiation accept check: "
                        + $"RenegotiationOriginalTradeables is {(Utils.RenegotiationOriginalTradeables == null ? "null (using tradeAgreementForQuest.AllTradeables fallback)" : "set")}; "
                        + $"originalTradeables=[{string.Join(", ", originalTradeables.Select(t => $"{t.ThingDef?.defName}:IsCurrency={t.IsCurrency}:HasAnyThing={t.HasAnyThing}:CountToTransfer={t.CountToTransfer}"))}]; "
                        + $"newTradeables=[{string.Join(", ", newTradeables.Select(t => $"{t.ThingDef?.defName}:IsCurrency={t.IsCurrency}:HasAnyThing={t.HasAnyThing}:CountToTransfer={t.CountToTransfer}"))}]; "
                        + $"changes=[{string.Join(", ", allChanges.Select(c => $"{c.direction}:{c.tradeable.ThingDef?.defName}:{c.amount}"))}]"
                );

                var (_, loadedGoodsTradeable, blockingAmount) = allChanges.FirstOrDefault(change =>
                    change.direction == TradeAgreement.TransferDirection.ToTrader
                    && change.amount > 0
                );
                if (loadedGoodsTradeable != null)
                {
                    var originalCount = originalTradeables
                        .FirstOrDefault(t => t.ThingDef == loadedGoodsTradeable.ThingDef)
                        ?.CountToTransfer;
                    RealisticOrbitalTradeMod.Instance.LogDevMessage(
                        $"Blocking departure renegotiation accept: {loadedGoodsTradeable.LabelCap} "
                            + $"blocked by {blockingAmount} (original CountToTransfer="
                            + $"{originalCount}, new CountToTransfer="
                            + $"{loadedGoodsTradeable.CountToTransfer})"
                    );

                    // Silver's own row never shows this ceiling on its own (see MaxPayableSilver) -
                    // flash it here the same way the affordability check above does, so the player
                    // has some visual cue pointing at which row is actually blocking Accept.
                    if (loadedGoodsTradeable.IsCurrency)
                    {
                        Dialog_Trade.lastCurrencyFlashTime = Time.time;
                    }

                    Messages.Message(
                        "RealisticOrbitalTrade.CantChangeLoadedGoods".Translate(
                            loadedGoodsTradeable.LabelCap,
                            Math.Abs(originalCount ?? 0)
                        ),
                        MessageTypeDefOf.RejectInput,
                        historical: false
                    );

                    actuallyTraded = false;
                    __result = false;
                    return false;
                }
            }

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
