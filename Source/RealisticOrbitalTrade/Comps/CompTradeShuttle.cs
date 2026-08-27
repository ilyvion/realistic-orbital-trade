using System.Text;
using RimWorld.Planet;
using Verse.AI;

namespace RealisticOrbitalTrade.Comps;

[HotSwappable]
[StaticConstructorOnStartup]
internal class CompTradeShuttle : ThingComp
{
    private static readonly Material RenegotiationRequiredOverlayMat = MaterialPool.MatFrom(
        "UI/Overlays/ROT_TradeAttention",
        ShaderDatabase.MetaOverlay
    );
    private static readonly Material MissingRequiredItemsOverlayMat = MaterialPool.MatFrom(
        "UI/Overlays/ROT_TradeIssue",
        ShaderDatabase.MetaOverlay
    );

    internal bool cancelled;
    internal bool isToTrader;
    internal TradeAgreement? tradeAgreement;
    internal List<ThingDefCountWithRequirements> requiredSpecificItems = [];

    // Recomputed each time CheckAutoload runs (whenever loading is actively in progress), rather
    // than tracked as saved state - a stale value left over from before a save/load is corrected
    // within a few seconds by the next tick, same as everything else CheckAutoload derives.
    internal bool missingRequiredItems;

    // Same recomputation caveat as missingRequiredItems. True when a requirement's remaining
    // colony stock (excluding anything already split off and in flight to this transporter, which
    // keeps being delivered regardless of forbidding - see JobDriver_HaulMultipleToTransporter's
    // ignoreForbidden) is only enough to cover it once forbidden items are counted in - i.e. the
    // player has enough of the item, but has forbidden so much of it that colonists won't actually
    // haul what's left. Distinct from missingRequiredItems, which fires when the item can't be
    // found at all regardless of forbidden status.
    internal bool requiredItemsForbidden;

    internal Action<int>? extendDepartureTimeAction;
    internal int timesBoughtMoreTime;

    internal int NextBuyMoreTimeCost =>
        Mathf.RoundToInt(
            Settings._buyMoreTimeBaseSilverCost
                * Mathf.Pow(Settings._buyMoreTimeCostGrowthFactor, timesBoughtMoreTime)
        );

    private CompShuttle Shuttle
    {
        get
        {
            field ??= parent.GetComp<CompShuttle>();
            return field;
        }
    }
    internal bool ShuttleAutoLoad
    {
        get => Shuttle.autoload;
        set => Shuttle.autoload = value;
    }

    internal Action? cancelTradeAction;

    private CompTransporter Transporter
    {
        get
        {
            field ??= parent.GetComp<CompTransporter>();
            return field;
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (isToTrader && parent.IsHashIntervalTick(120))
        {
            if (!parent.Spawned || cancelled)
            {
                return;
            }

            NotifyIfNegotiatorUnavailable();

            ShuttleAutoLoad = true;
            if (!Transporter.LoadingInProgressOrReadyToLaunch)
            {
                _ = TransporterUtility.InitiateLoading(Gen.YieldSingle(Transporter));
            }
        }
    }

    public override void PostDraw()
    {
        base.PostDraw();
        if (!parent.Spawned || cancelled)
        {
            return;
        }
        if (tradeAgreement is { RenegotiationRequired: true })
        {
            DrawAttentionOverlay(RenegotiationRequiredOverlayMat);
        }
        else if (missingRequiredItems || requiredItemsForbidden)
        {
            DrawAttentionOverlay(MissingRequiredItemsOverlayMat);
        }
    }

    private const float AttentionOverlaySizeFraction = 3f / 5f;

    private void DrawAttentionOverlay(Material material)
    {
        var drawPos = parent.DrawPos;
        drawPos.y = AltitudeLayer.MetaOverlays.AltitudeFor() + 0.21951221f;

        var iconSize = parent.Graphic.drawSize.y * AttentionOverlaySizeFraction;
        var mesh = MeshPool.GridPlane(new Vector2(iconSize, iconSize));

        var pulsePhase = (Time.realtimeSinceStartup + (397f * (parent.thingIDNumber % 571))) * 4f;
        var pulse = ((float)Math.Sin(pulsePhase) + 1f) * 0.5f;
        pulse = 0.3f + (pulse * 0.7f);

        var fadedMaterial = FadedMaterialPool.FadedVersionOf(material, pulse);
        Graphics.DrawMesh(
            mesh,
            Matrix4x4.TRS(drawPos, Quaternion.identity, Vector3.one),
            fadedMaterial,
            0
        );
    }

    private void NotifyIfNegotiatorUnavailable()
    {
        if (tradeAgreement is not { ShouldSendNegotiatorUnavailableLetter: true })
        {
            return;
        }

        tradeAgreement.NegotiatorUnavailableLetterSent = true;
        Find.LetterStack.ReceiveLetter(
            "RealisticOrbitalTrade.NegotiatorUnavailableLetterLabel".Translate(),
            "RealisticOrbitalTrade.NegotiatorUnavailableLetterText".Translate(
                tradeAgreement.negotiator.Named("NEGOTIATOR"),
                tradeAgreement.tradeShip.TraderName
            ),
            LetterDefOf.NegativeEvent,
            parent
        );
    }

    private static readonly List<string> tmpRequiredLabels = [];

    public override string? CompInspectStringExtra()
    {
        if (cancelled)
        {
            return null;
        }

        StringBuilder stringBuilder = new();

        if (tradeAgreement is { NegotiatorAvailable: false })
        {
#if v1_6_OR_GREATER
            _ =
#endif
            stringBuilder.AppendInNewLine(
                "RealisticOrbitalTrade.NegotiatorUnavailableInspectString".Translate()
            );
        }

        if (tradeAgreement is { RenegotiationRequired: true })
        {
#if v1_6_OR_GREATER
            _ =
#endif
            stringBuilder.AppendInNewLine(
                "RealisticOrbitalTrade.RenegotiationRequiredInspectString".Translate()
            );
        }
        else if (missingRequiredItems)
        {
#if v1_6_OR_GREATER
            _ =
#endif
            stringBuilder.AppendInNewLine(
                "RealisticOrbitalTrade.RequiredItemsMissingInspectString".Translate()
            );
        }
        else if (requiredItemsForbidden)
        {
#if v1_6_OR_GREATER
            _ =
#endif
            stringBuilder.AppendInNewLine(
                "RealisticOrbitalTrade.RequiredItemsForbiddenInspectString".Translate()
            );
        }

        CalculateRemainingRequiredItems();

        tmpRequiredLabels.Clear();
        foreach (var item in tmpRequiredSpecificItems.Where(i => i.count > 0))
        {
            tmpRequiredLabels.Add(item.Label);
        }
        if (tmpRequiredLabels.Any())
        {
#if v1_6_OR_GREATER
            _ = stringBuilder.AppendInNewLine(
#else
            stringBuilder.AppendInNewLine(
#endif
                "RealisticOrbitalTrade.RequiredThings".Translate()
                    + ": "
                    + tmpRequiredLabels.ToCommaList().CapitalizeFirst()
            );
        }
        return stringBuilder.ToString();
    }

    /// <summary>
    /// The requiredSpecificItems entries still owed, each reduced by whatever's already loaded -
    /// what "RequiredThingsLabel" and the inspect string should display, rather than the full
    /// original amounts in <see cref="requiredSpecificItems"/>.
    /// </summary>
    internal IEnumerable<ThingDefCountWithRequirements> RemainingRequiredItems()
    {
        CalculateRemainingRequiredItems();
        return tmpRequiredSpecificItems.Where(t => t.count > 0);
    }

    private void CalculateRemainingRequiredItems()
    {
        tmpRequiredSpecificItems.Clear();
        tmpRequiredSpecificItems.AddRange(requiredSpecificItems);

        var innerContainer = Transporter.innerContainer;
        foreach (var storedItem in innerContainer)
        {
            if (storedItem is Pawn)
            {
                continue;
            }
            var stackCount = storedItem.stackCount;
            for (var i = 0; i < tmpRequiredSpecificItems.Count; i++)
            {
                var requiredSpecificItem = tmpRequiredSpecificItems[i];
                if (requiredSpecificItem.Matches(storedItem))
                {
                    var additionalItemsNeeded = Mathf.Min(requiredSpecificItem.count, stackCount);
                    if (additionalItemsNeeded > 0)
                    {
                        tmpRequiredSpecificItems[i] = tmpRequiredSpecificItems[i]
                            .WithCount(tmpRequiredSpecificItems[i].count - additionalItemsNeeded);
                        stackCount -= additionalItemsNeeded;
                    }
                }
            }
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref isToTrader, "isToTrader");
        Scribe_Values.Look(ref cancelled, "cancelled");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_Collections.Look(ref requiredSpecificItems, "requiredSpecificItems", LookMode.Deep);
        Scribe_Values.Look(ref timesBoughtMoreTime, "timesBoughtMoreTime");
    }

    internal bool IsRequired(Thing thing)
    {
        if (!isToTrader)
        {
            return false;
        }
        foreach (var item in requiredSpecificItems)
        {
            if (item.Matches(thing))
            {
                return true;
            }
        }
        return false;
    }

    private static readonly List<ThingDefCountWithRequirements> tmpRequiredSpecificItems = [];
    private static readonly List<Thing> tmpAllSendableItems = [];
    private static readonly List<Thing> tmpColonySendableItems = [];
    private static readonly List<Thing> tmpCarriedSendableItems = [];
    private static readonly List<ThingDefCount> tmpRequiredGenericItems = [];

    private static bool IsForbiddenToPlayer(Thing thing) => thing.IsForbidden(Faction.OfPlayer);

    internal void CheckAutoload()
    {
        if (!isToTrader)
        {
            return;
        }

        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                "tradeAgreement is null in CompTradeShuttle. This is a bug, autoloading impossible."
            );
            return;
        }

        if (
            !ShuttleAutoLoad
            || !Transporter.LoadingInProgressOrReadyToLaunch
            || !parent.Spawned
            || cancelled
        )
        {
            return;
        }

        CalculateRemainingRequiredItems();

        tmpColonySendableItems.Clear();
        tmpColonySendableItems.AddRange(tradeAgreement.ColonyThingsTraderWillingToBuy());

        // Vanilla's TransporterUtility.ThingsBeingHauledTo only recognizes pawns running its own
        // JobDriver_HaulToTransporter - a pawn running this mod's own
        // JobDriver_HaulMultipleToTransporter never matches that check, so everything it's
        // currently carrying (hand-carried or still queued in its inventory via CarriedThings)
        // would otherwise vanish from tmpAllSendableItems for the whole trip. That understated
        // numberOfMatches below, clamping `count` under the true remaining requirement while a
        // haul was still in flight - see Utils.ThingsCarriedTowardTransporter, the same helper
        // TradeAgreement.CountOfMatchingThingsInFlightToTrader already uses for this reason.
        tmpCarriedSendableItems.Clear();
        foreach (var transporter in Shuttle.TransportersInGroup)
        {
            tmpCarriedSendableItems.AddRange(
                Utils.ThingsCarriedTowardTransporter(transporter, parent.Map)
            );
        }

        tmpAllSendableItems.Clear();
        tmpAllSendableItems.AddRange(tmpColonySendableItems);
        tmpAllSendableItems.AddRange(tmpCarriedSendableItems);

        // What's already been split off and promised to this transporter by an in-flight haul
        // (this mod's own bulk driver or vanilla's single-item one) - excluding: null since
        // nothing here should be excluded from this global tally, unlike the hauling job's own
        // use of this method, which excludes the pawn currently making its own hauling decision.
        // Below, this floors each existing entry's corrected count so a still-running autoload
        // tick can never talk a requirement back down below an amount a pawn has already reserved
        // and physically split off the stockpile for - doing so would desync the entry from the
        // real delivery already in motion and either strand the difference or, worse, make the
        // requirement look already met while that pawn is still carrying the shortfall.
        var committed = JobDriver_HaulMultipleToTransporter.ComputeCommittedCounts(
            Transporter,
            excluding: null!
        );

        // A requirement not fully covered by tmpAllSendableItems (colony stock plus everything
        // already en route to this transporter - the same two sources renegotiation-triggering
        // item loss and the loading below already treat as the full picture of what's available)
        // means the shortfall genuinely can't be found anywhere right now (consumed, worn, hidden
        // away, etc.), as opposed to merely not yet picked up for hauling.
        //
        // requiredSpecificItems only covers this mod's own per-instance-tracked requirements
        // (genepacks/xenogerms via requiredThing, apparel via quality+hitpoints) - Shuttle's own
        // plain ThingDef-counted requiredItems (e.g. wood, steel, meals) is vanilla's own list and
        // needs the same "can it be found anywhere" check, or a shortfall in it never gets flagged.
        tmpRequiredGenericItems.Clear();
        tmpRequiredGenericItems.AddRange(
            CalculateRemainingGenericItems(Shuttle.requiredItems, Transporter.innerContainer)
        );
        missingRequiredItems =
            AnyRequiredItemMissing(tmpRequiredSpecificItems, tmpAllSendableItems)
            || AnyGenericRequiredItemMissing(tmpRequiredGenericItems, tmpAllSendableItems);
        requiredItemsForbidden =
            AnyRequiredItemForbidden(
                tmpRequiredSpecificItems,
                tmpColonySendableItems,
                tmpCarriedSendableItems,
                IsForbiddenToPlayer
            )
            || AnyGenericRequiredItemForbidden(
                tmpRequiredGenericItems,
                tmpColonySendableItems,
                tmpCarriedSendableItems,
                IsForbiddenToPlayer
            );

        foreach (var requiredItem in tmpRequiredSpecificItems)
        {
            if (requiredItem.count <= 0)
            {
                continue;
            }

            var numberOfMatches = 0;
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    numberOfMatches += sendableItem.stackCount;
                }
            }
            if (numberOfMatches <= 0)
            {
                continue;
            }
            TransferableOneWay transferableOneWay = new();
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    transferableOneWay.things.Add(sendableItem);
                }
            }
            var count = Mathf.Min(requiredItem.count, numberOfMatches);
            if (requiredItem.requiredThing != null)
            {
                // A specific-item requirement keeps its own row rather than merging with
                // same-def rows the way UpdateToLoadList's stackability match does (see there) -
                // requiredThing already narrowed transferableOneWay to this exact instance, so
                // its existing row (if any) has to be found by that instance's identity instead.
                UpdateSpecificToLoadEntry(
                    Transporter.leftToLoad ??= [],
                    requiredItem.requiredThing,
                    transferableOneWay,
                    count,
                    committed
                );
            }
            else
            {
                UpdateToLoadList(
                    Transporter.leftToLoad ??= [],
                    transferableOneWay,
                    count,
                    committed
                );
            }
        }
    }

    /// <summary>
    /// Whether any of <paramref name="remainingRequiredItems"/> (already reduced to what's still
    /// owed) needs more than <paramref name="sendableItems"/> can cover in total - i.e. the
    /// shortfall can't be found anywhere the game currently knows to look.
    /// </summary>
    internal static bool AnyRequiredItemMissing(
        IEnumerable<ThingDefCountWithRequirements> remainingRequiredItems,
        IEnumerable<Thing> sendableItems
    )
    {
        foreach (var requiredItem in remainingRequiredItems)
        {
            if (requiredItem.count <= 0)
            {
                continue;
            }

            var numberOfMatches = 0;
            foreach (var sendableItem in sendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    numberOfMatches += sendableItem.stackCount;
                }
            }
            if (numberOfMatches < requiredItem.count)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Same check as <see cref="AnyRequiredItemMissing"/>, for the plain ThingDef-counted
    /// requirements vanilla's own <c>CompShuttle.requiredItems</c> tracks (wood, steel, meals,
    /// etc.), matched by def alone rather than <see cref="ThingDefCountWithRequirements.Matches"/>.
    /// </summary>
    internal static bool AnyGenericRequiredItemMissing(
        IEnumerable<ThingDefCount> remainingRequiredItems,
        IEnumerable<Thing> sendableItems
    )
    {
        foreach (var requiredItem in remainingRequiredItems)
        {
            if (requiredItem.Count <= 0)
            {
                continue;
            }

            var numberOfMatches = 0;
            foreach (var sendableItem in sendableItems)
            {
                if (sendableItem.def == requiredItem.ThingDef)
                {
                    numberOfMatches += sendableItem.stackCount;
                }
            }
            if (numberOfMatches < requiredItem.Count)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether any of <paramref name="remainingRequiredItems"/> has enough matching stock across
    /// <paramref name="colonyItems"/> plus <paramref name="inFlightItems"/> to cover it in total,
    /// but not enough once forbidden items in <paramref name="colonyItems"/> are excluded -
    /// i.e. the item exists in sufficient quantity, but too much of what's left on the map is
    /// forbidden for a colonist to ever pick up. <paramref name="inFlightItems"/> (already split
    /// off and physically being hauled toward the transporter) always counts toward the
    /// requirement regardless of forbidden status, since <see
    /// cref="JobDriver_HaulMultipleToTransporter"/> hauls with <c>job.ignoreForbidden</c> set and
    /// keeps delivering it even if it's forbidden after being picked up - the same behavior
    /// vanilla's own single-item <c>JobDriver_HaulToTransporter</c> uses for shuttle loading.
    /// <paramref name="isForbidden"/> is injected (rather than calling <c>Thing.IsForbidden</c>
    /// directly) so this can be unit tested without a live map.
    /// </summary>
    internal static bool AnyRequiredItemForbidden(
        IEnumerable<ThingDefCountWithRequirements> remainingRequiredItems,
        IEnumerable<Thing> colonyItems,
        IEnumerable<Thing> inFlightItems,
        Func<Thing, bool> isForbidden
    )
    {
        foreach (var requiredItem in remainingRequiredItems)
        {
            if (requiredItem.count <= 0)
            {
                continue;
            }

            var inFlightMatches = 0;
            foreach (var inFlightItem in inFlightItems)
            {
                if (requiredItem.Matches(inFlightItem))
                {
                    inFlightMatches += inFlightItem.stackCount;
                }
            }

            var stillNeeded = requiredItem.count - inFlightMatches;
            if (stillNeeded <= 0)
            {
                continue;
            }

            var totalColonyMatches = 0;
            var availableColonyMatches = 0;
            foreach (var colonyItem in colonyItems)
            {
                if (!requiredItem.Matches(colonyItem))
                {
                    continue;
                }
                totalColonyMatches += colonyItem.stackCount;
                if (!isForbidden(colonyItem))
                {
                    availableColonyMatches += colonyItem.stackCount;
                }
            }

            if (totalColonyMatches >= stillNeeded && availableColonyMatches < stillNeeded)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Same check as <see cref="AnyRequiredItemForbidden"/>, for the plain ThingDef-counted
    /// requirements vanilla's own <c>CompShuttle.requiredItems</c> tracks, matched by def alone.
    /// </summary>
    internal static bool AnyGenericRequiredItemForbidden(
        IEnumerable<ThingDefCount> remainingRequiredItems,
        IEnumerable<Thing> colonyItems,
        IEnumerable<Thing> inFlightItems,
        Func<Thing, bool> isForbidden
    )
    {
        foreach (var requiredItem in remainingRequiredItems)
        {
            if (requiredItem.Count <= 0)
            {
                continue;
            }

            var inFlightMatches = 0;
            foreach (var inFlightItem in inFlightItems)
            {
                if (inFlightItem.def == requiredItem.ThingDef)
                {
                    inFlightMatches += inFlightItem.stackCount;
                }
            }

            var stillNeeded = requiredItem.Count - inFlightMatches;
            if (stillNeeded <= 0)
            {
                continue;
            }

            var totalColonyMatches = 0;
            var availableColonyMatches = 0;
            foreach (var colonyItem in colonyItems)
            {
                if (colonyItem.def != requiredItem.ThingDef)
                {
                    continue;
                }
                totalColonyMatches += colonyItem.stackCount;
                if (!isForbidden(colonyItem))
                {
                    availableColonyMatches += colonyItem.stackCount;
                }
            }

            if (totalColonyMatches >= stillNeeded && availableColonyMatches < stillNeeded)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// <paramref name="requiredItems"/> reduced by whatever matching stock is already sitting in
    /// <paramref name="loadedThings"/> (the transporter's own <c>innerContainer</c>) - mirrors
    /// vanilla <c>CompShuttle.CheckAutoload</c>'s own local <c>tmpRequiredItems</c> computation,
    /// which isn't exposed for reuse here.
    /// </summary>
    internal static List<ThingDefCount> CalculateRemainingGenericItems(
        IReadOnlyList<ThingDefCount> requiredItems,
        IEnumerable<Thing> loadedThings
    )
    {
        var remaining = new List<ThingDefCount>(requiredItems);

        foreach (var loadedThing in loadedThings)
        {
            if (loadedThing is Pawn)
            {
                continue;
            }

            var stackCount = loadedThing.stackCount;
            for (var i = 0; i < remaining.Count; i++)
            {
                if (remaining[i].ThingDef != loadedThing.def)
                {
                    continue;
                }

                var reduce = Math.Min(remaining[i].Count, stackCount);
                if (reduce > 0)
                {
                    remaining[i] = remaining[i].WithCount(remaining[i].Count - reduce);
                    stackCount -= reduce;
                }
            }
        }

        return remaining;
    }

    // Deliberately narrower than JobDriver_HaulMultipleToTransporter.FindCommittedTransferable:
    // that one falls all the way back to TransferableMatchingDesperate's third tier, which matches
    // by ThingDef alone once neither reference-containment nor TransferAsOne (quality/stuff/hit
    // points aware) match anything - fine for attributing an already-split, already-committed
    // carry back to its row (see that method's own doc comment), but wrong here: every call into
    // UpdateToLoadList is folding a freshly-built row for one specific requiredItem bucket (def +
    // stuff + quality, see ThingDefCountWithRequirements.Matches) into leftToLoad, and the
    // def-only fallback would silently merge two different quality/stuff buckets of the same
    // ThingDef (e.g. two tuques of different material or quality) into a single row the moment the
    // second one's autoload tick runs. That capped the merged row's CountToTransfer at whichever
    // requirement happened to be processed last and left every other quality/stuff variant with no
    // row of its own to ever be hauled from - "To load" showed one tuque while several more sat
    // right there unpicked, each requiring its own load-then-recompute cycle to eventually surface.
    internal static TransferableOneWay? FindExistingToLoadEntry(
        Thing thing,
        List<TransferableOneWay> leftToLoad
    )
    {
        foreach (var candidate in leftToLoad)
        {
            if (candidate.HasAnyThing && candidate.things.Contains(thing))
            {
                return candidate;
            }
        }
        foreach (var candidate in leftToLoad)
        {
            if (
                candidate.HasAnyThing
                && TransferableUtility.TransferAsOne(
                    thing,
                    candidate.AnyThing,
                    TransferAsOneMode.PodsOrCaravanPacking
                )
            )
            {
                return candidate;
            }
        }
        return null;
    }

    // CalculateRemainingRequiredItems always reports the *full* amount still owed, not merely
    // what's newly become available since the last run - CompTick re-runs CheckAutoload every
    // ~2 seconds while autoload is on, so an existing to-load entry must be corrected to that
    // figure rather than incremented by it. Vanilla's own CompTransporter.AddToTheToLoadList
    // merges into a matching existing entry via an additive AdjustBy, which is fine for its own
    // callers (each call reports a genuinely new-found stack), but calling it here with the same
    // aggregate "still needed" total on every autoload tick would keep inflating CountToTransfer
    // past the real requirement, capped only by how much matching stock physically exists.
    internal static void UpdateToLoadList(
        List<TransferableOneWay> leftToLoad,
        TransferableOneWay transferableOneWay,
        int count,
        Dictionary<TransferableOneWay, int> committed
    )
    {
        TransferableOneWay? existing = null;
        foreach (var thing in transferableOneWay.things)
        {
            existing = FindExistingToLoadEntry(thing, leftToLoad);
            if (existing != null)
            {
                break;
            }
        }
        if (existing == null)
        {
            leftToLoad.Add(transferableOneWay);
            transferableOneWay.AdjustTo(count);
            return;
        }

        foreach (var thing in transferableOneWay.things)
        {
            if (!existing.things.Contains(thing))
            {
                existing.things.Add(thing);
            }
        }
        existing.AdjustTo(Math.Max(count, committed.GetValueOrDefault(existing)));
    }

    // Same corrective-rather-than-additive fix as UpdateToLoadList, for the requiredThing-keyed
    // rows that don't go through TransferableMatching's stackability check.
    internal static void UpdateSpecificToLoadEntry(
        List<TransferableOneWay> leftToLoad,
        Thing requiredThing,
        TransferableOneWay transferableOneWay,
        int count,
        Dictionary<TransferableOneWay, int> committed
    )
    {
        var existing = leftToLoad.FirstOrDefault(t => t.things.Contains(requiredThing));
        if (existing == null)
        {
            leftToLoad.Add(transferableOneWay);
            transferableOneWay.AdjustTo(count);
            return;
        }

        existing.AdjustTo(Math.Max(count, committed.GetValueOrDefault(existing)));
    }

    public bool AllRequiredThingsLoaded
    {
        get
        {
            if (tradeAgreement is { RenegotiationRequired: true })
            {
                return false;
            }
            // Utils.TryBeginDepartureRenegotiation locks in the loaded amount as the new required
            // amount (via TradeAgreement.ApplyRenegotiation) before the renegotiation dialog even
            // opens, which would otherwise make this true immediately - and since the toTrader
            // shuttle's WaitForever ship job has leaveImmediatelyWhenSatisfied set, that makes it
            // auto-launch (see ShipJob_Wait.TickInterval/SendAway) before the player has seen or
            // resolved the dialog, regardless of whether they go on to accept or decline it.
            if (
                tradeAgreement != null
                && Utils.RenegotiatingBecauseDeparting
                && Utils.RenegotiatingAgreement == tradeAgreement
            )
            {
                return false;
            }
            CalculateRemainingRequiredItems();
            return tmpRequiredSpecificItems.Sum(t => t.count) == 0;
        }
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (selPawn == null)
        {
            throw new ArgumentNullException(nameof(selPawn));
        }

        if (cancelled)
        {
            yield break;
        }

        var failureReason = GetFailureReason(selPawn);
        if (failureReason != null)
        {
            yield return failureReason;
            yield break;
        }

        yield return tradeAgreement!.NegotiatorAvailable
            ? FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "RealisticOrbitalTrade.RenegotiateTrade".Translate(),
                    () =>
                    {
                        var job = JobMaker.MakeJob(JobDefOf.ROT_RenegotiateTrade, parent);
                        _ = selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    },
                    MenuOptionPriority.InitiateSocial
                ),
                selPawn,
                parent
            )
            : FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "RealisticOrbitalTrade.RegisterAsNegotiator".Translate(),
                    () =>
                    {
                        var job = JobMaker.MakeJob(JobDefOf.ROT_RegisterAsNegotiator, parent);
                        _ = selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    },
                    MenuOptionPriority.InitiateSocial
                ),
                selPawn,
                parent
            );

        if (extendDepartureTimeAction != null)
        {
            var cost = NextBuyMoreTimeCost;
            yield return FloatMenuUtility.DecoratePrioritizedTask(
                new FloatMenuOption(
                    "RealisticOrbitalTrade.BuyMoreTime".Translate(
                        Settings._buyMoreTimeTicksPerPurchase.ToStringTicksToPeriod(),
                        ((float)cost).ToStringMoney()
                    ),
                    () =>
                    {
                        var job = JobMaker.MakeJob(JobDefOf.ROT_BuyMoreTime, parent);
                        _ = selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                    },
                    MenuOptionPriority.InitiateSocial
                ),
                selPawn,
                parent
            );
        }

        FloatMenuOption? GetFailureReason(Pawn selPawn)
        {
            if (tradeAgreement == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "tradeAgreement is null in CompTradeShuttle. This is a bug, renegotiation impossible."
                );
                return new FloatMenuOption(
                    "tradeAgreement is null in CompTradeShuttle. This is a bug, renegotiation impossible.",
                    null
                );
            }
            return !selPawn.CanReach(parent, PathEndMode.Touch, Danger.Some)
                    ? new FloatMenuOption("CannotUseNoPath".Translate(), null)
                : !selPawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking)
                    ? new FloatMenuOption(
                        "CannotUseReason".Translate(
                            "IncapableOfCapacity".Translate(
                                PawnCapacityDefOf.Talking.label,
                                selPawn.Named("PAWN")
                            )
                        ),
                        null
                    )
                : null;
        }
    }
}

// A class rather than a struct: ExposeData() needs Scribe_References for requiredThing, and
// Scribe_References only works when Scribe.loader.curParent is a reference type - a struct
// list entry is boxed separately per access and always reports as a value type, which breaks
// cross-ref resolution (RimWorld logs "current node is a value type" and fails to resolve it).
internal class ThingDefCountWithRequirements : IExposable
{
    public ThingDef def = null!;
    internal ThingDef? stuffDef;
    public bool isInnerThing;
    internal int count;
    public bool healthAffectsPrice;
    public int hitPoints;
    public int maxHitPoints;
    public bool hasQuality;
    public QualityCategory quality;

    // Genepacks/xenogerms carry no quality or health-price info to tell them apart by, so instead
    // of criteria this holds the exact required Thing itself - Matches() then requires that exact
    // reference rather than treating any Thing of the same def as satisfying the requirement, and
    // Label can show the item's own distinguishing label (e.g. a genepack's gene set name) instead
    // of the generic def label.
    internal Thing? requiredThing;

    public string Label =>
        requiredThing != null
            ? requiredThing.LabelCap
            : GenLabel.ThingLabel(def, stuffDef, count) + LabelExtras();

    private string LabelExtras()
    {
        var text = string.Empty;
        var reducedHealth = healthAffectsPrice && hitPoints < maxHitPoints;
        if (reducedHealth || hasQuality)
        {
            text += " (";
            if (hasQuality)
            {
                text += quality.GetLabel();
            }
            if (reducedHealth)
            {
                if (hasQuality)
                {
                    text += " ";
                }
                text += ((float)hitPoints / maxHitPoints).ToStringPercent();
            }
            text += ")";
        }
        return text;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Defs.Look(ref stuffDef, "stuffDef");
        Scribe_Values.Look(ref isInnerThing, "isInnerThing");
        Scribe_Values.Look(ref count, "count");
        Scribe_Values.Look(ref healthAffectsPrice, "healthAffectsPrice");
        Scribe_Values.Look(ref hitPoints, "hitPoints");
        Scribe_Values.Look(ref maxHitPoints, "maxHitPoints");
        Scribe_Values.Look(ref hasQuality, "hasQuality");
        Scribe_Values.Look(ref quality, "quality");
        Scribe_References.Look(ref requiredThing, "requiredThing");
    }

    internal bool Matches(Thing thing)
    {
        if (isInnerThing)
        {
            if (thing is MinifiedThing minifiedThing)
            {
                thing = minifiedThing.InnerThing;
            }
            else
            {
                return false;
            }
        }
        _ = QualityUtility.TryGetQuality(thing, out var thingQuality);
        return def == thing.def
            && stuffDef == thing.Stuff
            && count != 0
            && (requiredThing == null || requiredThing == thing)
            && (
                !healthAffectsPrice
                || HitPointsWithinTolerance(
                    thing.HitPoints,
                    hitPoints,
                    maxHitPoints,
                    Settings._hitPointsTolerancePercent
                )
            )
            && (!hasQuality || thingQuality == quality);
    }

    /// <summary>
    /// Whether <paramref name="actualHitPoints"/> is close enough to the originally recorded
    /// <paramref name="recordedHitPoints"/> to still count as the same item, allowing for minor
    /// deterioration. The allowed drift is the larger of 5 hit points or
    /// <paramref name="tolerancePercent"/>% of <paramref name="maxHitPoints"/>, so small items
    /// keep a usable grace window and large items get one that scales with their size.
    /// </summary>
    internal static bool HitPointsWithinTolerance(
        int actualHitPoints,
        int recordedHitPoints,
        int maxHitPoints,
        int tolerancePercent
    )
    {
        var tolerance = Math.Max(5, (int)(maxHitPoints * (tolerancePercent / 100f)));
        return Math.Abs(actualHitPoints - recordedHitPoints) <= tolerance;
    }

    internal ThingDefCountWithRequirements WithCount(int count) =>
        new()
        {
            def = def,
            stuffDef = stuffDef,
            isInnerThing = isInnerThing,
            count = count,
            healthAffectsPrice = healthAffectsPrice,
            hitPoints = hitPoints,
            maxHitPoints = maxHitPoints,
            hasQuality = hasQuality,
            quality = quality,
            requiredThing = requiredThing,
        };

    /// <summary>
    /// Finds the first entry in <paramref name="requiredSpecificItems"/> matching
    /// <paramref name="thing"/> and reduces its count by <paramref name="leftToTakeOff"/>,
    /// removing the entry entirely if its count would be fully consumed.
    /// </summary>
    /// <returns><see langword="false"/> if no matching entry was found.</returns>
    internal static bool TryReduceRequiredCount(
        List<ThingDefCountWithRequirements> requiredSpecificItems,
        Thing thing,
        ref int leftToTakeOff
    )
    {
        var thingDefCount = requiredSpecificItems.FirstOrDefault(t => t.Matches(thing));
        if (thingDefCount == null)
        {
            return false;
        }
        if (thingDefCount.count > leftToTakeOff)
        {
            thingDefCount.count -= leftToTakeOff;
            leftToTakeOff = 0;
        }
        else
        {
            leftToTakeOff -= thingDefCount.count;
            _ = requiredSpecificItems.Remove(thingDefCount);
        }
        return true;
    }
}
