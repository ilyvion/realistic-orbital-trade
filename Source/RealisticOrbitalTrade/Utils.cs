using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

internal static class Utils
{
    public static Quest? AmendsQuest => RealisticOrbitalTradeGameComponent.Current.AmendsQuest;

    public static bool IsMakingAmends => AmendsQuest != null;

    /// <summary>
    /// The agreement currently being renegotiated (a real trade dialog reopened over an existing
    /// deal), or <see langword="null"/> outside of that flow. Set by
    /// <see cref="JobDriver_RenegotiateTrade"/> before reopening comms; read by
    /// <see cref="Patch.Rimworld_TradeShip_TryOpenComms"/> (to reuse the existing agreement
    /// instead of starting a new one) and by the close/accept patches (to know an in-progress
    /// trade shouldn't be discarded/replayed the way a fresh one would).
    /// </summary>
    public static TradeAgreement? RenegotiatingAgreement { get; private set; }

    /// <summary>
    /// Snapshot of <see cref="TradeAgreement.AllTradeables"/> taken the moment the reopened trade
    /// dialog's <see cref="TradeDeal"/> is first built, before the player could edit anything - the
    /// "original" side of the diff <see cref="TradeAgreement.ApplyRenegotiation"/> applies against.
    /// Captured by <see cref="Patch.Rimworld_TradeDeal_AddAllTradeables"/> rather than
    /// <see cref="BeginRenegotiation"/> itself, since <see cref="TradeAgreement.AllTradeables"/>
    /// dereferences <c>TradeSession.trader</c> (via <see cref="TradeAgreement.MatchingTradeable"/>),
    /// which isn't assigned until comms are actually opened.
    /// </summary>
    public static List<Tradeable>? RenegotiationOriginalTradeables { get; internal set; }

    /// <summary>
    /// Set once <see cref="ExplainLostSoldItemsIfNeeded"/> has shown its explanation dialog for
    /// the renegotiation currently in progress, so a second <c>AddAllTradeables</c> call doesn't
    /// pop it again.
    /// </summary>
    private static bool RenegotiationLossExplanationShown { get; set; }

    /// <summary>
    /// Set alongside <see cref="RenegotiatingAgreement"/> when the reopened trade dialog was
    /// triggered automatically because the trader is leaving with the shuttle only partially
    /// loaded (see <see cref="TryBeginDepartureRenegotiation"/>), rather than by the player
    /// choosing to renegotiate. Already-loaded/boarded sold goods can't be edited in this mode -
    /// see <see cref="Patch.Rimworld_TradeDeal_TryExecute"/> - and closing without accepting warns
    /// the player it will fail the trade instead of silently doing nothing - see
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/>.
    /// </summary>
    public static bool RenegotiatingBecauseDeparting { get; private set; }

    /// <summary>
    /// Set once the player has confirmed, via the warning shown by
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/>, that closing a departure-triggered
    /// renegotiation without accepting should actually fail the trade.
    /// </summary>
    public static bool DepartureRenegotiationCancelConfirmed { get; internal set; }

    /// <summary>
    /// The <see cref="Quests.QuestPart_TryBeginDepartureRenegotiation.outSignalFailed"/> tag for
    /// the renegotiation currently in progress, set alongside <see cref="RenegotiatingBecauseDeparting"/>.
    /// Declining a departure-triggered renegotiation - see
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/> - fires this instead of duplicating the
    /// quest's own dump-and-fail logic, so there's exactly one place that logic lives regardless
    /// of which reason caused the renegotiation to not go through.
    /// </summary>
    public static string? DepartureRenegotiationFailedSignal { get; private set; }

    internal static void BeginRenegotiation(TradeAgreement tradeAgreement)
    {
        RenegotiatingAgreement = tradeAgreement;
        RenegotiationOriginalTradeables = null;
        RenegotiationLossExplanationShown = false;
        RenegotiatingBecauseDeparting = false;
        DepartureRenegotiationCancelConfirmed = false;
        DepartureRenegotiationFailedSignal = null;
    }

    internal static void EndRenegotiation()
    {
        RenegotiatingAgreement = null;
        RenegotiationOriginalTradeables = null;
        RenegotiationLossExplanationShown = false;
        RenegotiatingBecauseDeparting = false;
        DepartureRenegotiationCancelConfirmed = false;
        DepartureRenegotiationFailedSignal = null;
    }

    /// <summary>
    /// Shows a one-time explanation dialog, right as a renegotiation's trade dialog opens, when
    /// the renegotiation was triggered by one or more sold-to-trader things/pawns having been
    /// lost before delivery (see <see cref="TradeAgreement.lostSoldItems"/>). Lists what was lost
    /// and how much the deal's silver was automatically adjusted to compensate - neither is
    /// otherwise obvious from the trade dialog alone, since the adjustment already happens
    /// silently via <see cref="TradeDeal.UpdateCurrencyCount"/>.
    /// </summary>
    internal static void ExplainLostSoldItemsIfNeeded(
        TradeAgreement tradeAgreement,
        TradeDeal tradeDeal
    )
    {
        if (
            RenegotiationLossExplanationShown
            || RenegotiatingAgreement != tradeAgreement
            || tradeAgreement.lostSoldItems.Count == 0
            || RenegotiationOriginalTradeables == null
        )
        {
            return;
        }
        RenegotiationLossExplanationShown = true;

        // Forces the same auto-balance vanilla's Dialog_Trade.DoWindowContents runs every frame
        // (TradeDeal.UpdateCurrencyCount) to happen now, so the compensating silver change can be
        // read off immediately instead of waiting a frame for the dialog to draw itself once.
        tradeDeal.UpdateCurrencyCount();

        var currencyChange = TradeAgreement
            .ComputeRenegotiationChanges(RenegotiationOriginalTradeables, tradeDeal.AllTradeables)
            .FirstOrDefault(change => change.tradeable.IsCurrency);

        var text = LostSoldItemsExplanationKey(currencyChange)
            .Translate(
                BuildLostSoldItemsBulletList(tradeAgreement.lostSoldItems),
                tradeAgreement.tradeShip.TraderName,
                Math.Abs(currencyChange.amount)
            );

        Find.WindowStack.Add(
            new Dialog_MessageBox(
                text,
                title: "RealisticOrbitalTrade.RenegotiationLostItemsExplanationTitle".Translate()
            )
        );
    }

    internal static string BuildLostSoldItemsBulletList(
        IEnumerable<TradeAgreement.LostTradeItemRecord> lostSoldItems
    ) => string.Join("\n", lostSoldItems.Select(record => "- " + record.Label));

    /// <summary>
    /// Picks which translation key <see cref="ExplainLostSoldItemsIfNeeded"/> shows, based on
    /// which way (if any) <paramref name="currencyChange"/> - the currency entry from
    /// <see cref="TradeAgreement.ComputeRenegotiationChanges"/>, or its default if none was found
    /// - moved the deal's silver to compensate for the loss.
    /// </summary>
    internal static string LostSoldItemsExplanationKey(
        (
            TradeAgreement.TransferDirection direction,
            Tradeable? tradeable,
            int amount
        ) currencyChange
    ) =>
        currencyChange.tradeable == null || currencyChange.amount == 0
            ? "RealisticOrbitalTrade.RenegotiationLostItemsNoCurrencyChangeText"
        // ComputeRenegotiationChanges' amount is negated relative to raw CountToTransfer for a
        // ToTrader change but not for a ToPlayer one (see its own "negative: colony gets less;
        // positive: trader gets less" comment), so whether the amount owed to the trader went up
        // or down flips along with direction rather than following the amount's sign alone.
        : (
            currencyChange.direction == TradeAgreement.TransferDirection.ToTrader
                ? currencyChange.amount
                : -currencyChange.amount
        ) > 0
            ? "RealisticOrbitalTrade.RenegotiationLostItemsIncreasedOwedText"
        : "RealisticOrbitalTrade.RenegotiationLostItemsDecreasedOwedText";

    /// <summary>
    /// Called when the trade shuttle's departure timer expires with the shuttle only partially
    /// loaded. Locks in whatever's already physically loaded as the new sold-to-trader amount
    /// (see <see cref="TradeAgreement.ApplyRenegotiation"/>), then reopens the trade dialog so the
    /// player can adjust what they're buying to match, instead of dumping everything and failing
    /// the trade outright. Returns <see langword="false"/> without doing anything if there's
    /// nothing loaded to salvage, or no negotiator available to run the renegotiation - the caller
    /// is expected to fall back to failing the trade outright in that case.
    /// </summary>
    internal static bool TryBeginDepartureRenegotiation(
        TradeAgreement tradeAgreement,
        string outSignalFailed
    )
    {
        if (!tradeAgreement.NegotiatorAvailable)
        {
            RealisticOrbitalTradeMod.Instance.LogDevMessage(
                "TryBeginDepartureRenegotiation failing because there is no negotiator available"
            );
            return false;
        }

        // Tradeable.TraderWillTrade (hit while merging more than one already-committed
        // tradeable, including inside ApplyRenegotiation's own diffing) reads the static
        // TradeSession.trader, which is only populated while an actual trade dialog is open -
        // null here, since this runs from a quest signal with no dialog open yet, and would
        // otherwise NRE. Setting it to the correct trader for the duration of every tradeable
        // computation below is also semantically correct, not just crash avoidance: it's the
        // same trader TryOpenComms is about to open a real session with anyway.
        var previousTrader = TradeSession.trader;
        TradeSession.trader = tradeAgreement.tradeShip;
        try
        {
            var loadedOnlyTradeables = tradeAgreement.LoadedOnlyTradeables();
            if (TradeAgreement.IsFullCancellation(loadedOnlyTradeables))
            {
                RealisticOrbitalTradeMod.Instance.LogDevMessage(
                    "TryBeginDepartureRenegotiation failing because this is a full cancellation"
                );
                return false;
            }

            _ = tradeAgreement.ApplyRenegotiation(
                tradeAgreement.AllTradeables,
                loadedOnlyTradeables
            );
        }
        finally
        {
            TradeSession.trader = previousTrader;
        }

        BeginRenegotiation(tradeAgreement);
        RenegotiatingBecauseDeparting = true;
        DepartureRenegotiationFailedSignal = outSignalFailed;

        // A letter sent the same tick the dialog opens can't actually be read before the
        // renegotiation happens - shown as a blocking dialog instead so the player sees the
        // explanation before comms open, not buried behind it.
        Find.WindowStack.Add(
            new Dialog_MessageBox(
                "RealisticOrbitalTrade.DepartureRenegotiationExplanationText".Translate(
                    tradeAgreement.tradeShip.TraderName
                ),
                title: "RealisticOrbitalTrade.DepartureRenegotiationExplanationTitle".Translate(),
                buttonAAction: () =>
                    tradeAgreement.tradeShip.TryOpenComms(tradeAgreement.negotiator)
            )
        );
        return true;
    }

    /// <summary>
    /// True while <see cref="JobDriver_BuyMoreTime"/> is handing the payment silver to the trade
    /// ship. That transfer runs outside any open trade dialog, so
    /// <see cref="TradeShipData.tradeAgreementForQuest"/> is legitimately unset at that point;
    /// <see cref="Patch.Rimworld_TradeShip_GiveSoldThingToTrader"/> checks this flag to avoid
    /// logging its missing-agreement warning for that expected case.
    /// </summary>
    public static bool IsBuyingMoreTime { get; internal set; }

    /// <summary>
    /// Returns <paramref name="cache"/> unless <paramref name="isDone"/> says it's done, in which
    /// case the cache is cleared (set to <see langword="null"/>) first. Used to lazily invalidate
    /// a cached reference to something that starts "live" and eventually finishes, without
    /// re-deriving it on every read.
    /// </summary>
    internal static T? GetLiveOrClear<T>(ref T? cache, Func<T, bool> isDone)
        where T : class
    {
        if (cache != null && isDone(cache))
        {
            cache = null;
        }
        return cache;
    }

    public static void AddThingToLoadToShuttle(
        ThingCountClass thingCount,
        CompTradeShuttle compTradeShuttle,
        CompShuttle compShuttle
    )
    {
        if (
            thingCount.thing.HasRequirements(
                out var healthAffectsPrice,
                out var qualityCategory,
                out var hasInnerThing
            )
        )
        {
            var thing = thingCount.thing.GetInnerIfMinified();
            var requiredThing = thing is GeneSetHolderBase ? thing : null;

            // A requiredThing-tracked entry (genepack/xenogerm) needs its own row per exact
            // instance - Matches() keys those off reference identity, so merging would collapse
            // distinct genepacks into one row. Every other entry only distinguishes by
            // def/stuff/quality/health, and several sold items can share all of those (e.g.
            // several identical-quality tuques) - without merging, each becomes its own
            // count-1 entry that all match the same physical stock, and CheckAutoload's
            // UpdateToLoadList (which corrects a matching "To load" row to whichever single
            // entry it's currently processing, rather than summing across entries sharing that
            // row) then caps the row at 1 instead of the true combined total, forcing a separate
            // hauling trip per entry instead of one trip for the lot.
            var existingIndex =
                requiredThing == null
                    ? compTradeShuttle.requiredSpecificItems.FindIndex(t =>
                        t.requiredThing == null
                        && t.def == thing.def
                        && t.stuffDef == thing.Stuff
                        && t.isInnerThing == hasInnerThing
                        && t.healthAffectsPrice == healthAffectsPrice
                        && t.hitPoints == thing.HitPoints
                        && t.maxHitPoints == thing.MaxHitPoints
                        && t.hasQuality == qualityCategory.HasValue
                        && t.quality == (qualityCategory ?? QualityCategory.Normal)
                    )
                    : -1;
            if (existingIndex >= 0)
            {
                compTradeShuttle.requiredSpecificItems[existingIndex] = compTradeShuttle
                    .requiredSpecificItems[existingIndex]
                    .WithCount(
                        compTradeShuttle.requiredSpecificItems[existingIndex].count
                            + thingCount.Count
                    );
            }
            else
            {
                compTradeShuttle.requiredSpecificItems.Add(
                    new ThingDefCountWithRequirements
                    {
                        def = thing.def,
                        stuffDef = thing.Stuff,
                        isInnerThing = hasInnerThing,
                        count = thingCount.Count,
                        healthAffectsPrice = healthAffectsPrice,
                        hitPoints = thing.HitPoints,
                        maxHitPoints = thing.MaxHitPoints,
                        hasQuality = qualityCategory.HasValue,
                        quality = qualityCategory ?? QualityCategory.Normal,
                        requiredThing = requiredThing,
                    }
                );
            }
        }
        else
        {
            // Vanilla's CompShuttle.CompInspectStringExtra treats each requiredItems entry as
            // the sole/total requirement for its ThingDef (comparing innerContainer's total
            // stack count of that def against the entry's Count), so multiple entries sharing a
            // def must be merged into one - otherwise loading enough to satisfy just one entry
            // makes it (and every other entry for that same def) vanish from "Required" even
            // though the combined requirement isn't met yet.
            var existingIndex = compShuttle.requiredItems.FindIndex(t =>
                t.ThingDef == thingCount.thing.def
            );
            if (existingIndex >= 0)
            {
                compShuttle.requiredItems[existingIndex] = compShuttle
                    .requiredItems[existingIndex]
                    .WithCount(compShuttle.requiredItems[existingIndex].Count + thingCount.Count);
            }
            else
            {
                compShuttle.requiredItems.Add(
                    new ThingDefCount(thingCount.thing.def, thingCount.Count)
                );
            }
        }
    }

    /// <summary>
    /// Symmetric inverse of the plain (non-requirements) branch of
    /// <see cref="AddThingToLoadToShuttle"/>: reduces the entry matching <paramref name="def"/> by
    /// <paramref name="leftToTakeOff"/>, removing it entirely if fully consumed. Mirrors
    /// <see cref="ThingDefCountWithRequirements.TryReduceRequiredCount"/>, which covers the
    /// requirements-based list.
    /// </summary>
    public static bool TryReduceRequiredItemsCount(
        List<ThingDefCount> requiredItems,
        ThingDef def,
        ref int leftToTakeOff
    )
    {
        var thingDefCount = requiredItems.FirstOrDefault(t => t.ThingDef == def);
        if (thingDefCount == null)
        {
            return false;
        }
        if (thingDefCount.Count > leftToTakeOff)
        {
            var index = requiredItems.IndexOf(thingDefCount);
            requiredItems[index] = thingDefCount.WithCount(thingDefCount.Count - leftToTakeOff);
            leftToTakeOff = 0;
        }
        else
        {
            leftToTakeOff -= thingDefCount.Count;
            _ = requiredItems.Remove(thingDefCount);
        }
        return true;
    }

    /// <summary>
    /// Fully rebuilds the shuttles' required-cargo lists (<c>requiredSpecificItems</c>/
    /// <c>requiredItems</c>) from <paramref name="tradeAgreement"/>'s current
    /// <c>thingsSoldToTrader</c>, discarding whatever was there before. Used after
    /// <c>thingsSoldToTrader</c> was pruned of entries whose thing no longer resolves (e.g. a
    /// sold item destroyed and saved before that pruning existed), since at that point there's no
    /// way to identify which specific required-cargo entry to reduce.
    /// </summary>
    internal static void RebuildRequiredCargo(TradeAgreement tradeAgreement)
    {
        foreach (var ship in tradeAgreement.ToTraderTransportShips)
        {
            ship.shipThing.TryGetComp<CompTradeShuttle>().requiredSpecificItems.Clear();
            ship.ShuttleComp.requiredItems.Clear();
        }

        var plan = tradeAgreement.PlanShuttleLoadsOrSingleShuttle(
            Patch.Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity
        );
        tradeAgreement.AssignRequiredCargo(plan);
    }

    public static IEnumerable<Pawn> PawnsWithHaulToTransporterJob(
        CompTransporter compTransporter,
        Map map
    )
    {
        var pawns = map.mapPawns.AllPawnsSpawned;
        foreach (var pawn in pawns)
        {
            if (
                pawn.jobs?.curDriver is JobDriver_HaulMultipleToTransporter bulkDriver
                && compTransporter == bulkDriver.Transporter
            )
            {
                yield return pawn;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="pawn"/>'s hauling job has anything matching
    /// <paramref name="matches"/> in its hands, its queue of pickups, or (for this mod's bulk
    /// hauling) its inventory awaiting delivery.
    /// </summary>
    internal static bool IsHauling(Pawn pawn, Func<Thing, bool> matches)
    {
        var job = pawn.CurJob;
        return (pawn.carryTracker.CarriedThing is { } carried && matches(carried))
            || (job.targetA.Thing is { } target && matches(target))
            || job.GetTargetQueue(TargetIndex.A).Any(t => t.Thing is { } queued && matches(queued))
            || (
                pawn.jobs.curDriver is JobDriver_HaulMultipleToTransporter bulkDriver
                && bulkDriver.CarriedThings.Any(matches)
            );
    }

    /// <summary>
    /// Ends every hauling job toward <paramref name="compTransporter"/> that is hauling something
    /// matching <paramref name="matches"/>.
    /// </summary>
    internal static void EndHaulJobsFor(
        CompTransporter compTransporter,
        Map map,
        Func<Thing, bool> matches
    )
    {
        foreach (
            var pawn in PawnsWithHaulToTransporterJob(compTransporter, map)
                .Where(p => IsHauling(p, matches))
                .ToList()
        )
        {
            pawn.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
        }
    }

    /// <summary>
    /// Everything currently physically split off a colony stack and in a pawn's hands or
    /// inventory en route to <paramref name="compTransporter"/> by this mod's bulk hauling job -
    /// already gone from the map's reachable stockpiles, but not yet inside the transporter
    /// either, so neither vanilla's own colony scan nor a straight read of the transporter's
    /// contents will find it.
    /// </summary>
    public static IEnumerable<Thing> ThingsCarriedTowardTransporter(
        CompTransporter compTransporter,
        Map map
    )
    {
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (
                pawn.jobs.curDriver is JobDriver_HaulMultipleToTransporter bulkDriver
                && bulkDriver.Transporter == compTransporter
            )
            {
                foreach (
                    var thing in JobDriver_HaulMultipleToTransporter.InFlightCargo(
                        pawn.carryTracker.CarriedThing,
                        bulkDriver.CarriedThings
                    )
                )
                {
                    yield return thing;
                }
            }
        }
    }
}
