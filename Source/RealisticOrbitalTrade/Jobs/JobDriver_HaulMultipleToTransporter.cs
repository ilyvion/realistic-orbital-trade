using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

/// <summary>
/// Carries several stacks bound for the same trade shuttle in one trip. Structured after Pick Up
/// And Haul's own two-phase design (see its <c>WorkGiver_HaulToInventory</c> /
/// <c>JobDriver_UnloadYourHauledInventory</c>) rather than PUAH's code directly, since PUAH has no
/// transporter-loading logic to hook into: picks up the primary item the same way vanilla's
/// <see cref="JobDriver_HaulToTransporter"/> does (via
/// <see cref="LoadTransportersJobUtility.FindThingToLoad"/>, which also accounts for what other
/// pawns already running vanilla's own hauling job are carrying), sweeps up nearby still-needed
/// stacks into inventory nearest-first the way PUAH's own search does, then walks to the shuttle
/// and unloads them into its hands one at a time with a short pause between each, instead of
/// teleporting the whole batch in at once. A live animal/prisoner/slave takes both hands to lead
/// and can't be batched with cargo, so that case is handled with vanilla's own single-item
/// carry-and-deposit toils instead.
/// </summary>
[HotSwappable]
internal class JobDriver_HaulMultipleToTransporter : JobDriver
{
    private const float SearchForMoreRadius = 12f;

    // Matches Pick Up And Haul's own default pause between drop-offs in its unload job.
    private const int UnloadIntervalTicks = 3;

    private List<Thing> _carriedThings = [];

    // How many items at the tail of targetQueueA make up the trip's hand-carried group (see
    // QueueItems) - everything before that goes to inventory. Always at least 1.
    private int _handCarryGroupCount = 1;

    // Everything picked up on this trip that's still pending delivery (already split off its
    // source stack and sitting in the pawn's inventory, waiting to be pulled into hand one at a
    // time - see pullNextCarriedThing in MakeNewToils) - not including whatever's currently in
    // pawn.carryTracker.CarriedThing, which is removed from this list the moment it's pulled out.
    internal IReadOnlyList<Thing> CarriedThings => _carriedThings;

    // takeThing's actual pickup (mass-capped for real, via MassUtility.CountToPickUpUntilOverEncumbered)
    // can come in under CommitToInventory's own up-front mass estimate (see CalculateCountToTake) -
    // when it does, the shortfall is still physically sitting in the very same, exclusively
    // reserved stack that QueueItems carries forward as the trip's hand-carry topper (see
    // QueueItems' `pending`/leftover handling), so it's recorded here keyed by that Thing and
    // added back on top of whatever queue entry hand-carries the rest of that same stack (see
    // carryGatheredThing) instead of being stranded on the map for a separate future trip.
    private Dictionary<Thing, int> _pendingMassShortfall = [];

    /// <summary>
    /// Everything a bulk hauling trip has physically split off colony stacks but not yet
    /// deposited: whatever's in the pawn's hands plus every stack still waiting in inventory.
    /// </summary>
    internal static IEnumerable<Thing> InFlightCargo(
        Thing? carriedThing,
        IReadOnlyList<Thing> pendingDeliveries
    )
    {
        if (carriedThing != null)
        {
            yield return carriedThing;
        }
        foreach (var pending in pendingDeliveries)
        {
            yield return pending;
        }
    }

    internal CompTransporter? Transporter =>
        job.GetTarget(TargetIndex.B).Thing?.TryGetComp<CompTransporter>();

    private TradeAgreement? TradeAgreement =>
        Transporter?.parent.TryGetComp<CompTradeShuttle>()?.tradeAgreement;

    // Releases count units of thing from the trade's reserved-for-protection list right before
    // asking the real ReservationManager to reserve them for hauling - see
    // TradeHaulReservationContext and Patch/Rimworld_ReservationManager.cs for why the two must
    // never both think they're responsible for the same units, and TradeAgreement.ReduceReservation
    // for why this only ever releases what's actually still tracked. Restores it if the real
    // reservation fails after all (e.g. another pawn's job claimed the thing in the same tick).
    // Callers still owe a TradeAgreement.ReconcileReservation call of their own afterwards - see
    // there for why it needs the caller's own view of what's still needed rather than being folded
    // in here.
    private bool ReserveForHaul(Thing thing, int count)
    {
        var tradeAgreement = TradeAgreement;
        var released = tradeAgreement?.ReduceReservation(thing, count) ?? 0;
        if (pawn.Reserve(thing, job))
        {
            return true;
        }
        if (released > 0)
        {
            tradeAgreement!.RestoreReservation(thing, released);
        }
        return false;
    }

    // Queues a candidate for the standalone hand-carry path (pawn, or no batchable transferable)
    // only once reserve() actually claims it - mirrors CommitToInventory/QueueHandCarryGroup,
    // which both drop their candidate outright on a failed reservation instead of queuing a pickup
    // nothing will ever protect. Takes reserve as a delegate (rather than calling ReserveForHaul
    // directly) so the queuing decision can be unit tested without a live pawn/map.
    internal static bool TryQueueStandaloneHandCarry(
        Thing thing,
        int count,
        Func<Thing, int, bool> reserve,
        List<LocalTargetInfo> targetQueue,
        List<int> countQueue
    )
    {
        if (!reserve(thing, count))
        {
            return false;
        }

        targetQueue.Add(thing);
        countQueue.Add(count);
        return true;
    }

    // Thing.SplitOff doesn't clamp: asking it to split off more than a thing's actual stackCount
    // logs an error and despawns/detaches the *original* thing instead of returning a valid
    // partial split, so the queued pickup amount must never exceed what's really left in the
    // stack - mirrors the equivalent clamp already applied in carryGatheredThing above.
    internal static int ComputeCountToPickUp(int jobCount, int massCappedCount, int stackCount) =>
        Math.Min(Math.Min(jobCount, massCappedCount), stackCount);

    // The gap between what a pickup was planned to take and what it actually took, bounded by
    // what was physically there to take in the first place - so a stack that's simply smaller
    // than planned (nothing left to give) never counts as a shortfall to carry forward.
    internal static int ComputeMassShortfall(
        int jobCount,
        int stackCountBeforeSplit,
        int countPickedUp
    ) => Math.Max(0, Math.Min(jobCount, stackCountBeforeSplit) - countPickedUp);

    // The hand-carry topper's own planned count, topped up with whatever shortfall an earlier
    // pickup from this same (exclusively reserved) stack left behind - still bounded by what's
    // actually left in the stack, same as every other pickup here.
    internal static int ComputeHandCarryCount(
        int jobCount,
        int carriedOverShortfall,
        int stackCount
    ) => Math.Min(jobCount + carriedOverShortfall, stackCount);

    // Verbose per-decision tracing of the pickup/hauling logic below, gated on the
    // dev-messages toggle so it's opt-in and doesn't spam normal play.
    private void DebugLog(string message)
    {
        if (Settings._printDevMessages)
        {
            Log.Message($"[ROT-Haul] {pawn.LabelShort}: {message}");
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Collections.Look(ref _carriedThings, "carriedThings", LookMode.Reference);
        _carriedThings ??= [];
        Scribe_Values.Look(ref _handCarryGroupCount, "handCarryGroupCount", 1);
        Scribe_Collections.Look(
            ref _pendingMassShortfall,
            "pendingMassShortfall",
            LookMode.Reference,
            LookMode.Value
        );
        _pendingMassShortfall ??= [];

        // AddFinishAction registrations (see Notify_Starting) don't survive a save/load - a job
        // resumed after loading a save needs EjectUndeliveredCargo re-armed here instead.
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            AddFinishAction(EjectUndeliveredCargo);
        }
    }

    // Safety net for a job that ends mid-trip (cancelled, pawn downed/drafted, reservation lost,
    // etc.): anything already split off a colony stack and stashed in the pawn's inventory for
    // later delivery (see takeThing/_carriedThings above) has no other job that will ever come
    // back for it, so left alone the pawn would carry it around indefinitely. Registered via
    // AddFinishAction, so this also runs on a normal finish, where _carriedThings is already
    // empty by construction (see pullNextCarriedThing) and this is a no-op.
    // JobDriver.AddFinishAction takes Action<JobCondition> from v1.5 onward, but plain Action on
    // v1.4 - the delegate signature registered here has to match whichever this target actually
    // builds against.
#if v1_5_OR_GREATER
    private void EjectUndeliveredCargo(JobCondition condition)
    {
        if (_carriedThings.Count == 0)
        {
            return;
        }

        DebugLog(
            $"EjectUndeliveredCargo: job ended ({condition}) with {_carriedThings.Count} "
                + "item(s) still in inventory - dropping them."
        );
        EjectUndeliveredCargoCore();
    }
#else
    private void EjectUndeliveredCargo()
    {
        if (_carriedThings.Count == 0)
        {
            return;
        }

        DebugLog(
            $"EjectUndeliveredCargo: job ended with {_carriedThings.Count} item(s) still in "
                + "inventory - dropping them."
        );
        EjectUndeliveredCargoCore();
    }
#endif

    private void EjectUndeliveredCargoCore()
    {
        foreach (var thing in _carriedThings)
        {
            if (thing.DestroyedOrNull() || !pawn.inventory.innerContainer.Contains(thing))
            {
                continue;
            }

            _ = pawn.inventory.innerContainer.TryDrop(
                thing,
                pawn.PositionHeld,
                pawn.MapHeld,
                ThingPlaceMode.Near,
                thing.stackCount,
                out _
            );
        }
        _carriedThings.Clear();
    }

    // Mirrors vanilla JobDriver_HaulToTransporter: at this point job.targetA/targetQueueA are
    // still empty (LoadTransportersJobUtility.JobOnTransporter hands out a job with an invalid
    // target), so there's nothing to reserve yet - the actual thing(s) to haul are only found and
    // reserved once Notify_Starting runs, right after this returns.
    public override bool TryMakePreToilReservations(bool errorOnFailed) => true;

    public override void Notify_Starting()
    {
        base.Notify_Starting();
        AddFinishAction(EjectUndeliveredCargo);

        var transporter = Transporter;
        if (transporter == null)
        {
            DebugLog("Notify_Starting: no transporter on targetB, bailing out.");
            return;
        }

        // Covers both the FindThingToLoad candidate search below and every pawn.Reserve call this
        // method (transitively, via ReserveForHaul/QueueItems/QueueHandCarryGroup) makes for this
        // trip - see TradeHaulReservationContext for why it needs to span both.
        using var haulReservationScope = TradeHaulReservationContext.Enter(pawn, transporter);

        var thingCount = LoadTransportersJobUtility.FindThingToLoad(pawn, transporter);
        DebugLog(
            $"Notify_Starting: FindThingToLoad -> "
                + $"{thingCount.Thing?.LabelCap ?? "null"} x{thingCount.Count} "
                + $"(FreeSpace={MassUtility.FreeSpace(pawn):F2}kg)"
        );
        if (thingCount.Thing == null)
        {
            return;
        }

        var leftToLoad = transporter.leftToLoad;
        var primaryTransferable = leftToLoad?.Find(t => t.things.Contains(thingCount.Thing));
        var committed = ComputeCommittedCounts(transporter, pawn);
        DebugLog(
            "Notify_Starting: committed counts from other pawns -> "
                + string.Join(", ", committed.Select(kv => $"{kv.Key.ThingDef.label}={kv.Value}"))
        );

        var primaryCount = thingCount.Count;
        if (primaryTransferable != null)
        {
            var alreadyCommitted = committed.GetValueOrDefault(primaryTransferable);
            primaryCount = Math.Min(
                primaryCount,
                Math.Max(0, primaryTransferable.CountToTransfer - alreadyCommitted)
            );
            DebugLog(
                $"Notify_Starting: primary transferable {primaryTransferable.ThingDef.label} "
                    + $"CountToTransfer={primaryTransferable.CountToTransfer} "
                    + $"alreadyCommitted={alreadyCommitted} -> primaryCount clamped to {primaryCount}"
            );
        }

        // Everyone else already has this requirement covered between them (e.g. two other
        // colonists' trips already add up to the full amount still needed) - nothing left for
        // this pawn to usefully pick up.
        if (primaryCount <= 0)
        {
            DebugLog("Notify_Starting: primaryCount <= 0 after clamping, bailing out.");
            return;
        }

        // A live animal/prisoner/slave takes both hands to lead, and without a matching
        // transferable there's no `remainingNeeded` bookkeeping to drive a sweep - either way,
        // this can't be batched with cargo, so hand off to vanilla's own single-item hand-carry
        // path (mirrors JobDriver_HaulToTransporter) instead of running QueueItems at all.
        if (
            thingCount.Thing is Pawn
            || primaryTransferable == null
            || transporter.leftToLoad == null
        )
        {
            job.countQueue ??= [];
            if (
                !TryQueueStandaloneHandCarry(
                    thingCount.Thing,
                    primaryCount,
                    ReserveForHaul,
                    job.GetTargetQueue(TargetIndex.A),
                    job.countQueue
                )
            )
            {
                DebugLog(
                    $"Notify_Starting: dropping primary {thingCount.Thing.LabelCap} - "
                        + "reservation failed."
                );
                return;
            }

            if (primaryTransferable != null)
            {
                var alreadyCommitted = committed.GetValueOrDefault(primaryTransferable);
                TradeAgreement?.ReconcileReservation(
                    primaryTransferable,
                    Math.Max(
                        0,
                        primaryTransferable.CountToTransfer - alreadyCommitted - primaryCount
                    )
                );
            }
            _handCarryGroupCount = 1;
            DebugLog(
                $"Notify_Starting: queued primary {thingCount.Thing.LabelCap} x{primaryCount} "
                    + "as a standalone hand-carry (pawn, or no batchable transferable)."
            );
            return;
        }

        QueueItems(transporter, thingCount.Thing, primaryTransferable, primaryCount, committed);

        DebugLog(
            $"Notify_Starting: final plan -> "
                + string.Join(
                    ", ",
                    job.GetTargetQueue(TargetIndex.A)
                        .Select(
                            (t, i) =>
                                $"{t.Thing?.LabelCap}x{(i < job.countQueue.Count ? job.countQueue[i] : -1)}"
                        )
                )
        );
    }

    /// <summary>
    /// Sums, per still-needed transferable, how much other pawns already have committed to
    /// hauling to this transporter (queued, currently held, or actively being carried) - whether
    /// they're running this same job or vanilla's own single-item <see
    /// cref="JobDriver_HaulToTransporter"/> - so a pawn computing its own pickups doesn't count
    /// stacks that other colonists have already claimed toward the same requirement. Without this,
    /// several pawns independently sweeping up "still needed" stacks at the same time each think
    /// the full remaining amount is theirs to grab, and the shuttle ends up loaded with more than
    /// was actually sold.
    /// </summary>
    internal static Dictionary<TransferableOneWay, int> ComputeCommittedCounts(
        CompTransporter transporter,
        Pawn excluding
    )
    {
        var committed = new Dictionary<TransferableOneWay, int>();
        var leftToLoad = transporter.leftToLoad;
        if (leftToLoad == null)
        {
            return committed;
        }

        void AddCommitted(Thing? thing, int count)
        {
            if (thing == null || count <= 0)
            {
                return;
            }

            var transferable = FindCommittedTransferable(thing, leftToLoad);
            if (transferable == null)
            {
                return;
            }

            committed[transferable] = committed.GetValueOrDefault(transferable) + count;
        }

        foreach (var otherPawn in transporter.parent.Map.mapPawns.AllPawnsSpawned)
        {
            if (otherPawn == excluding || HaulingTransporter(otherPawn) != transporter.parent)
            {
                continue;
            }

            foreach (var (thing, count) in ClaimedThings(otherPawn))
            {
                AddCommitted(thing, count);
            }
        }

        return committed;
    }

    /// <summary>
    /// The transporter <paramref name="pawn"/> is currently loading, through either this driver or
    /// vanilla's <see cref="JobDriver_HaulToTransporter"/>.
    /// </summary>
    internal static Thing? HaulingTransporter(Pawn pawn) =>
        pawn.jobs?.curDriver switch
        {
            JobDriver_HaulMultipleToTransporter driver => driver.Transporter?.parent,
            JobDriver_HaulToContainer vanillaDriver
                when pawn.CurJobDef == RimWorld.JobDefOf.HaulToTransporter => vanillaDriver
                .job.GetTarget(TargetIndex.B)
                .Thing,
            _ => null,
        };

    /// <summary>
    /// What <paramref name="pawn"/>'s current <see cref="HaulingTransporter"/> job has claimed and
    /// not yet deposited: stacks it has yet to pick up, with the count it will take from each, and
    /// what it already carries.
    /// </summary>
    internal static IEnumerable<(Thing? thing, int count)> ClaimedThings(Pawn pawn)
    {
        if (pawn.jobs?.curDriver is JobDriver_HaulMultipleToTransporter driver)
        {
            var job = pawn.CurJob;
            var targetThing = job.GetTarget(TargetIndex.A).Thing;
            var carriedThing = pawn.carryTracker?.CarriedThing;
            yield return (
                targetThing,
                CommittedCountForTarget(targetThing, carriedThing, job.count)
            );

            // Between hand-carry pickups the target is the next stack to gather, while what
            // is already in the pawn's hands still isn't in the transporter.
            if (carriedThing != null && carriedThing != targetThing)
            {
                yield return (carriedThing, carriedThing.stackCount);
            }

            var queueA = job.GetTargetQueue(TargetIndex.A);
            var countQueue = job.countQueue;
            if (queueA != null && countQueue != null)
            {
                for (var i = 0; i < queueA.Count && i < countQueue.Count; i++)
                {
                    yield return (queueA[i].Thing, countQueue[i]);
                }
            }

            foreach (var carried in driver._carriedThings)
            {
                yield return (carried, carried.stackCount);
            }
        }
        else if (pawn.jobs?.curDriver is JobDriver_HaulToContainer vanillaDriver)
        {
            yield return (
                vanillaDriver.job.GetTarget(TargetIndex.A).Thing,
                vanillaDriver.job.count
            );
        }
    }

    // Thing.SplitOff hands back a brand-new Thing instance whenever it splits off part (rather
    // than the whole) of a stack, so a thing that's already been picked up for this trip is no
    // longer the same object present in any transferable's `things` list - a reference-identity
    // lookup like `things.Contains(thing)` silently fails to attribute it to anything.
    // TransferableMatchingDesperate falls back to a stackability/def match (same as vanilla's own
    // CompTransporter.SubtractFromToLoadList uses to attribute a just-deposited thing back to its
    // transferable), so it still finds the right transferable for an already-split portion.
    internal static TransferableOneWay? FindCommittedTransferable(
        Thing thing,
        List<TransferableOneWay> leftToLoad
    ) =>
        TransferableUtility.TransferableMatchingDesperate(
            thing,
            leftToLoad,
            TransferAsOneMode.PodsOrCaravanPacking
        );

    // Once a trip's hand-carry group has merged more than one queued pickup into a single carried
    // stack (see QueueHandCarryGroup), job.count only ever reflects the most recently extracted
    // queue entry's own amount, not the merged stack's true total - so once `targetThing` is
    // actually the pawn's CarriedThing, its own stackCount (which does reflect every merge) is the
    // only accurate figure. Before pickup, targetThing is still the original, unsplit stack still
    // sitting elsewhere, which can hold more than this pawn is actually entitled to - jobCount is
    // the right figure there instead.
    internal static int CommittedCountForTarget(
        Thing? targetThing,
        Thing? carriedThing,
        int jobCount
    ) => targetThing != null && targetThing == carriedThing ? targetThing.stackCount : jobCount;

    /// <summary>
    /// Queues the primary pickup plus any other stacks still needed by the transporter, sweeping
    /// outward from the primary and then from wherever the last queued pickup left off - mirrors
    /// Pick Up And Haul's own nearest-first search
    /// (<c>WorkGiver_HaulToInventory.GetClosestAndRemove</c>) so haulers sweep through a cluster
    /// of stacks instead of bouncing between far-apart ones. The primary is fed through the same
    /// sweep as every other candidate rather than being committed to inventory up front - it's
    /// just as eligible to end up as the trip's hand-carried item as anything else found along
    /// the way (see the `pending` comment below), which is what keeps a lone big primary stack
    /// from over-committing inventory space it doesn't actually have.
    /// </summary>
    private void QueueItems(
        CompTransporter transporter,
        Thing primaryThing,
        TransferableOneWay primaryTransferable,
        int primaryCount,
        Dictionary<TransferableOneWay, int> committed
    )
    {
        var remainingMassCapacity = MassUtility.FreeSpace(pawn);
        DebugLog(
            $"QueueItems: FreeSpace={remainingMassCapacity:F2}kg (primary not yet committed to "
                + "inventory - it's swept just like any other candidate)."
        );

        var remainingNeeded = new Dictionary<TransferableOneWay, int>();

        // Available, not just Thing/Transferable: how much of this stack is still up for grabs on
        // this trip. Starts at the physical stack size, but a candidate that only partially fits
        // into inventory (see CommitToInventory below) gets re-added with just its leftover
        // amount rather than being dropped outright - nothing's actually been split off it yet at
        // this point (that only happens once the toils run), so the live Thing.stackCount would
        // overstate what's still free to claim once part of it is already spoken for.
        var candidates = new List<(Thing Thing, TransferableOneWay Transferable, int Available)>();
        foreach (var transferable in transporter.leftToLoad)
        {
            if (transferable.CountToTransfer <= 0)
            {
                continue;
            }

            // Only accounts for other pawns' in-flight commitments - this pawn's own primary
            // pickup is deducted naturally once it's actually committed below, same as every
            // other candidate.
            remainingNeeded[transferable] = Math.Max(
                0,
                transferable.CountToTransfer - committed.GetValueOrDefault(transferable)
            );
            foreach (var candidate in transferable.things)
            {
                if (
                    candidate != primaryThing
                    && candidate is not Pawn
                    && CanQueueForPickup(candidate, transporter)
                )
                {
                    candidates.Add((candidate, transferable, candidate.stackCount));
                }
            }
        }

        DebugLog(
            $"QueueItems: {candidates.Count} additional candidate stack(s) found across "
                + $"{transporter.leftToLoad.Count} transferable(s) still needed; remainingNeeded -> "
                + string.Join(
                    ", ",
                    remainingNeeded.Select(kv => $"{kv.Key.ThingDef.label}={kv.Value}")
                )
        );

        // PositionHeld, not Position - a genepack sitting in a genebank is never Spawned, so its
        // own Position is invalid; PositionHeld resolves through its parent holder instead.
        var searchPosition = primaryThing.PositionHeld;

        // Queues a candidate into inventory, mass-capped as usual. Reservation and the actual
        // count are only decided here, at commit time, rather than when the candidate was first
        // selected - this is only ever called on the *previous* candidate found by the sweep
        // below, once a later candidate has proven it wasn't the last one after all. `ceiling`
        // additionally bounds the primary to `primaryCount` (which already reflects how much of
        // its stack this pawn is actually entitled to, per FindThingToLoad/CountToTransfer) -
        // for every other candidate it's just that candidate's own stack size.
        // Returns the leftover portion as a fresh candidate when mass cut the pickup short of what
        // was wanted, or null when the whole wanted amount was taken (or nothing was, e.g. a
        // failed reservation).
        (Thing Thing, TransferableOneWay Transferable, int Available)? CommitToInventory(
            Thing thing,
            TransferableOneWay transferable,
            int ceiling
        )
        {
            var stillNeeded = remainingNeeded.GetValueOrDefault(transferable);
            var unitMass = thing.GetStatValue(StatDefOf.Mass);
            var wantCount = Math.Min(ceiling, stillNeeded);
            var countToTake = CalculateCountToTake(
                ceiling,
                stillNeeded,
                remainingMassCapacity,
                unitMass
            );
            if (countToTake <= 0 || !ReserveForHaul(thing, countToTake))
            {
                DebugLog(
                    $"QueueItems: dropping {thing.LabelCap} - countToTake={countToTake}, "
                        + $"reserve={(countToTake > 0 ? pawn.CanReserve(thing) : (bool?)null)}."
                );
                return null;
            }

            job.GetTargetQueue(TargetIndex.A).Add(thing);
            job.countQueue ??= [];
            job.countQueue.Add(countToTake);
            remainingNeeded[transferable] = stillNeeded - countToTake;
            remainingMassCapacity -= countToTake * unitMass;
            TradeAgreement?.ReconcileReservation(transferable, remainingNeeded[transferable]);

            DebugLog(
                $"QueueItems: queued {thing.LabelCap} x{countToTake} (ceiling={ceiling}, "
                    + $"unitMass={unitMass:F2}kg) into inventory; remainingMassCapacity now "
                    + $"{remainingMassCapacity:F2}kg."
            );

            // Mass, not need or physical stock, is what cut this short - the rest of the stack is
            // still sitting right where it was and still needed, so it stays in play rather than
            // vanishing from consideration for the rest of this trip (e.g. the hand-carry topper).
            var leftover = wantCount - countToTake;
            if (leftover <= 0)
            {
                return null;
            }

            DebugLog(
                $"QueueItems: {leftover} unit(s) of {thing.LabelCap} didn't fit in inventory - "
                    + "it's exactly where the pawn already is, so it takes over as the pending "
                    + "hand-carry candidate immediately."
            );
            return (thing, transferable, leftover);
        }

        // Whichever candidate ends up last in the sweep is carried in hand for the walk to the
        // shuttle instead of going into inventory (see the hand-carry group toil in
        // MakeNewToils), and hand-carried items aren't subject to inventory mass limits at all -
        // same as vanilla's own single-item hauling, which never mass-checks what a pawn picks up
        // to carry. But which candidate ends up last isn't known until the sweep actually stops
        // (out of range, nothing left needed, or no candidates left) - it's NOT necessarily
        // whichever candidate happened to be examined right as the mass budget hit zero. So each
        // candidate (including the primary, which seeds this) is held as `pending` for one
        // iteration instead of committed immediately: once a further candidate is found, the
        // previous `pending` one is committed to inventory (mass-capped) since it's now known not
        // to be the last; whatever's still `pending` when the sweep ends becomes the trip's
        // hand-carried topper, uncapped by mass.
        (Thing Thing, TransferableOneWay Transferable, int Ceiling) pending = (
            primaryThing,
            primaryTransferable,
            primaryCount
        );
        while (candidates.Count > 0)
        {
            // Once inventory has no room left for even the pending candidate, there's nothing to
            // gain from continuing this sweep: every further candidate examined here would just
            // get dropped the same way (mass exhausted) and, worse, get permanently removed from
            // `candidates` in the process - taking potentially close, same-def stacks out of the
            // running for QueueHandCarryGroup's own top-off search below. Stop here instead and
            // let that search see the full remaining candidate list.
            var pendingUnitMass = pending.Thing.GetStatValue(StatDefOf.Mass);
            var pendingStillNeeded = remainingNeeded.GetValueOrDefault(pending.Transferable);
            if (
                CalculateCountToTake(
                    pending.Ceiling,
                    pendingStillNeeded,
                    remainingMassCapacity,
                    pendingUnitMass
                ) <= 0
            )
            {
                DebugLog(
                    $"QueueItems: inventory has no more room for {pending.Thing.LabelCap} "
                        + $"(remainingMassCapacity={remainingMassCapacity:F2}kg) - stopping the "
                        + "inventory sweep early so nearby candidates stay available for hand-carry top-off."
                );
                break;
            }

            var closestIndex = 0;
            var closestDistanceSquared = (
                candidates[0].Thing.PositionHeld - searchPosition
            ).LengthHorizontalSquared;
            for (var i = 1; i < candidates.Count; i++)
            {
                var distanceSquared = (
                    candidates[i].Thing.PositionHeld - searchPosition
                ).LengthHorizontalSquared;
                if (distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestIndex = i;
                }
            }

            var (candidate, transferable, available) = candidates[closestIndex];
            candidates.RemoveAt(closestIndex);
            var distance = Math.Sqrt(closestDistanceSquared);

            if (closestDistanceSquared > SearchForMoreRadius * SearchForMoreRadius)
            {
                // Nothing closer to the current sweep position is worth a detour for either.
                DebugLog(
                    $"QueueItems: nearest remaining candidate {candidate.LabelCap} is "
                        + $"{distance:F1} tiles away (> {SearchForMoreRadius}), stopping sweep."
                );
                break;
            }

            if (!remainingNeeded.TryGetValue(transferable, out var stillNeeded) || stillNeeded <= 0)
            {
                DebugLog(
                    $"QueueItems: skipping {candidate.LabelCap} ({transferable.ThingDef.label}) "
                        + "- nothing still needed for its transferable."
                );
                continue;
            }

            var leftoverPending = CommitToInventory(
                pending.Thing,
                pending.Transferable,
                pending.Ceiling
            );
            if (leftoverPending is { } leftover)
            {
                // The leftover sits at zero distance from searchPosition (same Thing, nothing's
                // moved) - always closer than whatever the sweep just found, so it immediately
                // takes over as pending instead. The candidate that was about to take over gets
                // put back for a later iteration rather than lost.
                candidates.Add((candidate, transferable, available));
                pending = leftover;
                DebugLog(
                    $"QueueItems: {leftover.Thing.LabelCap} leftover (distance=0.0) is now the "
                        + "pending hand-carry candidate, ahead of "
                        + $"{candidate.LabelCap} (distance={distance:F1})."
                );
            }
            else
            {
                pending = (candidate, transferable, available);
                searchPosition = candidate.PositionHeld;
                DebugLog(
                    $"QueueItems: {candidate.LabelCap} (distance={distance:F1}) is now the pending "
                        + "hand-carry candidate."
                );
            }
        }

        QueueHandCarryGroup(pending, candidates, remainingNeeded);

        DebugLog("QueueItems: sweep finished.");
    }

    /// <summary>
    /// Commits the sweep's final `pending` candidate as the trip's hand-carried item, uncapped by
    /// mass - then, since hand-carrying has no mass limit to stop at, keeps sweeping nearest-first
    /// for more stacks that could physically merge into the very same carried stack (<see
    /// cref="Thing.CanStackWith"/>: same def and stuff), topping it off up to its def's stack
    /// limit instead of settling for whatever the first stack alone happened to have. All of it
    /// ends up as one merged stack in hand (see the hand-carry group toil in MakeNewToils), so a
    /// leftover partial pile doesn't get stranded for lack of looking one step further.
    /// </summary>
    private void QueueHandCarryGroup(
        (Thing Thing, TransferableOneWay Transferable, int Ceiling) pending,
        List<(Thing Thing, TransferableOneWay Transferable, int Available)> candidates,
        Dictionary<TransferableOneWay, int> remainingNeeded
    )
    {
        var stillNeeded = remainingNeeded.GetValueOrDefault(pending.Transferable);
        var countToCarry = Math.Min(pending.Ceiling, stillNeeded);
        if (countToCarry <= 0 || !ReserveForHaul(pending.Thing, countToCarry))
        {
            DebugLog(
                $"QueueItems: dropping pending hand-carry topper {pending.Thing.LabelCap} - "
                    + $"countToCarry={countToCarry}, reserve="
                    + $"{(countToCarry > 0 ? pawn.CanReserve(pending.Thing) : (bool?)null)}."
            );
            _handCarryGroupCount = 0;
            return;
        }

        job.GetTargetQueue(TargetIndex.A).Add(pending.Thing);
        job.countQueue ??= [];
        job.countQueue.Add(countToCarry);
        remainingNeeded[pending.Transferable] = stillNeeded - countToCarry;
        TradeAgreement?.ReconcileReservation(
            pending.Transferable,
            remainingNeeded[pending.Transferable]
        );
        var groupCount = 1;
        var groupTotal = countToCarry;
        var stackLimit = pending.Thing.def.stackLimit;
        var searchPosition = pending.Thing.PositionHeld;
        DebugLog(
            $"QueueItems: queued {pending.Thing.LabelCap} x{countToCarry} as the trip's "
                + $"hand-carry topper, uncapped by mass (stackLimit={stackLimit})."
        );

        while (groupTotal < stackLimit && candidates.Count > 0)
        {
            var closestIndex = -1;
            var closestDistanceSquared = 0f;
            for (var i = 0; i < candidates.Count; i++)
            {
                if (!candidates[i].Thing.CanStackWith(pending.Thing))
                {
                    continue;
                }

                var distanceSquared = (
                    candidates[i].Thing.PositionHeld - searchPosition
                ).LengthHorizontalSquared;
                if (closestIndex == -1 || distanceSquared < closestDistanceSquared)
                {
                    closestDistanceSquared = distanceSquared;
                    closestIndex = i;
                }
            }

            if (closestIndex == -1)
            {
                DebugLog("QueueItems: no more mergeable stacks found to top off the hand-carry.");
                break;
            }

            var distance = Math.Sqrt(closestDistanceSquared);
            if (closestDistanceSquared > SearchForMoreRadius * SearchForMoreRadius)
            {
                DebugLog(
                    $"QueueItems: nearest mergeable stack {candidates[closestIndex].Thing.LabelCap} "
                        + $"is {distance:F1} tiles away (> {SearchForMoreRadius}), stopping top-off."
                );
                break;
            }

            var (mergeCandidate, mergeTransferable, mergeAvailable) = candidates[closestIndex];
            candidates.RemoveAt(closestIndex);

            var mergeStillNeeded = remainingNeeded.GetValueOrDefault(mergeTransferable);
            var roomLeft = stackLimit - groupTotal;
            var mergeCount = Math.Min(Math.Min(mergeAvailable, mergeStillNeeded), roomLeft);
            if (mergeCount <= 0 || !ReserveForHaul(mergeCandidate, mergeCount))
            {
                DebugLog(
                    $"QueueItems: skipping top-off candidate {mergeCandidate.LabelCap} - "
                        + $"mergeCount={mergeCount}, reserve="
                        + $"{(mergeCount > 0 ? pawn.CanReserve(mergeCandidate) : (bool?)null)}."
                );
                continue;
            }

            job.GetTargetQueue(TargetIndex.A).Add(mergeCandidate);
            job.countQueue.Add(mergeCount);
            remainingNeeded[mergeTransferable] = mergeStillNeeded - mergeCount;
            TradeAgreement?.ReconcileReservation(
                mergeTransferable,
                remainingNeeded[mergeTransferable]
            );
            groupTotal += mergeCount;
            groupCount++;
            searchPosition = mergeCandidate.PositionHeld;
            DebugLog(
                $"QueueItems: topped off hand-carry with {mergeCandidate.LabelCap} "
                    + $"x{mergeCount} (distance={distance:F1}); carried stack now "
                    + $"{groupTotal}/{stackLimit}."
            );
        }

        _handCarryGroupCount = groupCount;
    }

    // thing.Spawned/thing.Map are false/null for something like a genepack sitting in a
    // genebank's CompGenepackContainer - use the *Held variants (as the rest of this file's
    // unspawned-parent handling does) so those don't get filtered out of the sweep entirely.
    private bool CanQueueForPickup(Thing thing, CompTransporter transporter) =>
        thing.SpawnedOrAnyParentSpawned
        && thing.MapHeld == transporter.parent.Map
        && !thing.IsForbidden(pawn)
        && pawn.CanReserve(thing)
        && pawn.Map.reachability.CanReach(
            pawn.Position,
            thing,
            PathEndMode.ClosestTouch,
            TraverseParms.For(pawn)
        );

    /// <summary>
    /// How much of a candidate stack to pick up, bounded by what's physically there, what the
    /// transporter still needs of it, and how much mass capacity the pawn has left to spend on it.
    /// This count is only a plan, computed once up front - the actual pickup re-checks mass for
    /// real against <see cref="MassUtility.CountToPickUpUntilOverEncumbered"/>, and float rounding
    /// between the two can disagree right at the edge. So when mass is the thing capping the count
    /// down to a single unit and more than one was wanted, that unit isn't worth the risk of the
    /// live check disagreeing and the trip coming back with nothing for this stack at all - only
    /// squeeze in the last unit when there's room to spare beyond it.
    /// </summary>
    internal static int CalculateCountToTake(
        int stackCount,
        int stillNeededCount,
        float remainingMassCapacity,
        float unitMass
    )
    {
        var wantCount = Math.Min(stackCount, stillNeededCount);
        if (wantCount <= 0)
        {
            return 0;
        }

        if (unitMass <= 0f)
        {
            return wantCount;
        }

        var maxByMass = (int)(remainingMassCapacity / unitMass);
        return maxByMass == 1 && wantCount > 1 ? 0 : Math.Max(0, Math.Min(wantCount, maxByMass));
    }

    protected override IEnumerable<Toil> MakeNewToils()
    {
        // Unlike vanilla's JobDriver_HaulToTransporter, targetA starts out invalid: the primary
        // and any extra pickups all live in targetQueueA and only get extracted into targetA one
        // at a time (see the loop below), so a global fail-on-null for TargetIndex.A here would
        // trip before the first extraction ever runs.
        _ = this.FailOnDespawnedNullOrForbidden(TargetIndex.B);

        // Also doubles as the toil that's jumped to whenever there's nothing left to do -
        // yielded exactly once, at the very end, so a jump here always means "pause, then finish".
        var wait = Toils_General.Wait(2);

        // Held between pulling an item into the pawn's hands and actually depositing it, so the
        // carried-thing render has time to show up - without this, since every drop-off in this
        // job goes to the same spot the pawn is already standing at (the transporter), pulling and
        // depositing would otherwise happen close enough together to never visibly render as
        // "carrying" at all.
        var carryPause = Toils_General.Wait(UnloadIntervalTicks);

        var nextTarget = Toils_JobTransforms.ExtractNextTargetFromQueue(TargetIndex.A);

        // Every toil between nextTarget and the Jump(nextTarget) back to it completes
        // synchronously (instant completion, and gotoThing's StartPath -> PatherArrived also
        // completes synchronously whenever the pawn is already standing at the next queued
        // item's cell, e.g. several stacks piled in the same stockpile square) - so without a
        // real tick-deferred toil somewhere in that loop, JobDriver.JumpToToil recurses one more
        // native stack frame per queued item instead of ever returning control to the tick
        // scheduler. A long enough pickup queue then overflows the native stack outright, with
        // no bad data or corrupted Thing involved. One idle tick per item is imperceptible but
        // forces the loop back onto the tick scheduler, capping recursion depth at a constant.
        var tickBreak = Toils_General.Wait(1);

        // Some things (e.g. a genepack held in a genebank's CompGenepackContainer, v1.5+ only -
        // see Patch/TradeShuttle.cs) are never Spawned themselves even while sitting in a
        // reachable, spawned holder - mirrors vanilla JobDriver_HaulToContainer's own gotoThing
        // toil, which uses canGotoSpawnedParent/FailOnSelfAndParentsDespawnedOrNull for the same
        // reason instead of requiring the carried thing itself to be spawned.
#if v1_5_OR_GREATER
        var gotoThing = Toils_Goto.GotoThing(
            TargetIndex.A,
            PathEndMode.ClosestTouch,
            canGotoSpawnedParent: true
        );
        _ = gotoThing.FailOnSelfAndParentsDespawnedOrNull(TargetIndex.A);
#else
        var gotoThing = Toils_Goto.GotoThing(TargetIndex.A, PathEndMode.ClosestTouch);
#endif

        // Pawns are never EverStorable, so stuffing one into inventory like a regular haulable
        // would silently no-op (ThingOwner.TryAdd rejects it) and leave the job "succeeding"
        // without ever loading it. Hand off to the same carry-and-deposit toils vanilla uses.
        var startCarryPawn = Toils_Haul.StartCarryThing(TargetIndex.A);
        var gotoTransporterWithPawn = Toils_Goto.GotoThing(TargetIndex.B, PathEndMode.Touch);
        var depositPawn = Toils_Haul.DepositHauledThingInContainer(TargetIndex.B, TargetIndex.None);

        // The trailing `_handCarryGroupCount` items gathered on this trip go straight into the
        // pawn's hands instead of the backpack, merging into a single carried stack (see
        // QueueHandCarryGroup) - mirrors Pick Up And Haul's own gathering trips, where every
        // pickup except the final one goes into inventory and only the last one is visibly
        // carried the rest of the way. Can't use Toils_Haul.StartCarryThing here: it requires the
        // thing to be Spawned, which doesn't hold for something like a genepack sitting in a
        // genebank's CompGenepackContainer - split-and-TryAdd (as takeThing below does for
        // inventory) works regardless of the source thing's spawned state. No MassUtility check
        // either - hand-carried items aren't subject to inventory mass limits, same as vanilla's
        // own StartCarryThing/FindThingToLoad, which never mass-cap what a pawn picks up to carry;
        // job.count was already sized appropriately for this by QueueItems. Reads back
        // pawn.carryTracker.CarriedThing rather than trusting splitThing after the merge: when
        // ThingOwner.TryAdd merges into an already-carried compatible stack, it absorbs
        // splitThing's count into that existing stack and destroys splitThing itself, so holding
        // onto the pre-merge reference would point at a dead Thing.
        var carryGatheredThing = new Toil
        {
            initAction = () =>
            {
                var thing = job.GetTarget(TargetIndex.A).Thing;
                var shortfall = _pendingMassShortfall.GetValueOrDefault(thing);
                if (shortfall > 0)
                {
                    _ = _pendingMassShortfall.Remove(thing);
                }
                var countToCarry = ComputeHandCarryCount(job.count, shortfall, thing.stackCount);
                DebugLog(
                    $"carryGatheredThing: {thing.LabelCap} job.count={job.count} "
                        + (shortfall > 0 ? $"shortfallCarriedOver={shortfall} " : "")
                        + $"stackCount={thing.stackCount} -> countToCarry={countToCarry}"
                );
                if (countToCarry <= 0)
                {
                    return;
                }

                var splitThing = thing.SplitOff(countToCarry);
                _ = pawn.carryTracker.innerContainer.TryAdd(splitThing);
                job.SetTarget(TargetIndex.A, pawn.carryTracker.CarriedThing ?? splitThing);
            },
        };

        var takeThing = new Toil
        {
            initAction = () =>
            {
                var thing = job.GetTarget(TargetIndex.A).Thing;
                var massCappedCount = MassUtility.CountToPickUpUntilOverEncumbered(pawn, thing);
                var countToPickUp = ComputeCountToPickUp(
                    job.count,
                    massCappedCount,
                    thing.stackCount
                );
                DebugLog(
                    $"takeThing: {thing.LabelCap} job.count={job.count} "
                        + $"massCappedCount={massCappedCount} stackCount={thing.stackCount} "
                        + $"(FreeSpace={MassUtility.FreeSpace(pawn):F2}kg) -> "
                        + $"countToPickUp={countToPickUp}"
                );
                if (countToPickUp <= 0)
                {
                    return;
                }

                var shortfall = ComputeMassShortfall(job.count, thing.stackCount, countToPickUp);
                if (shortfall > 0)
                {
                    _pendingMassShortfall[thing] =
                        _pendingMassShortfall.GetValueOrDefault(thing) + shortfall;
                    DebugLog(
                        $"takeThing: {shortfall} unit(s) of {thing.LabelCap} short of plan due to "
                            + "mass - carrying the shortfall forward to whatever queue entry "
                            + "hand-carries the rest of this same stack."
                    );
                }

                var splitThing = thing.SplitOff(countToPickUp);

                // Must not merge with anything already in inventory (e.g. the pawn's own reserved
                // meal): a merge destroys splitThing and folds its count into the other stack, so
                // the reference in _carriedThings below would point at a dead Thing and this
                // portion would never get pulled back out for delivery - it'd sit there forever
                // mixed into the pawn's personal item.
                _ = pawn
                    .inventory.GetDirectlyHeldThings()
                    .TryAdd(splitThing, canMergeWithExistingStacks: false);
                _carriedThings.Add(splitThing);

                // Nothing downstream reads targetA again before the next queue extraction
                // overwrites it (or the job moves on to the hand-carry/delivery toils, which key
                // off pawn.carryTracker.CarriedThing instead) - but ComputeCommittedCounts, called
                // from *other* pawns' Notify_Starting, does inspect it. Left pointing at the
                // now-partially-consumed original stack, it would count this pickup a second time
                // on top of its entry in _carriedThings for as long as this item sits in inventory
                // awaiting delivery.
                job.SetTarget(TargetIndex.A, LocalTargetInfo.Invalid);
            },
        };

        // Pulls one carried item at a time out of inventory and into the pawn's hands, mirroring
        // Pick Up And Haul's own JobDriver_UnloadYourHauledInventory.PullItemFromInventory, so
        // items visibly get carried and dropped off one by one instead of vanishing into the
        // transporter all at once.
        var pullNextCarriedThing = new Toil
        {
            initAction = () =>
            {
                while (_carriedThings.Count > 0)
                {
                    var thing = _carriedThings[0];
                    _carriedThings.RemoveAt(0);
                    if (thing.DestroyedOrNull() || !pawn.inventory.innerContainer.Contains(thing))
                    {
                        continue;
                    }

                    _ = pawn.inventory.innerContainer.TryTransferToContainer(
                        thing,
                        pawn.carryTracker.innerContainer,
                        thing.stackCount,
                        out var transferredThing
                    );
                    job.SetTarget(TargetIndex.A, transferredThing);
                    return;
                }
            },
        };
        var carryToContainer = Toils_Haul.CarryHauledThingToContainer();
        var depositCarriedThing = Toils_Haul.DepositHauledThingInContainer(
            TargetIndex.B,
            TargetIndex.None
        );

        var logExtractedTarget = new Toil
        {
            initAction = () =>
                DebugLog(
                    $"MakeNewToils: extracted {TargetThingA?.LabelCap ?? "null"} "
                        + $"x{job.count} from queue "
                        + $"({job.GetTargetQueue(TargetIndex.A).Count} item(s) still queued)."
                ),
        };

        DebugLog(
            "MakeNewToils: starting job with queue -> "
                + string.Join(
                    ", ",
                    job.GetTargetQueue(TargetIndex.A)
                        .Select(
                            (t, i) =>
                                $"{t.Thing?.LabelCap}x{(i < job.countQueue.Count ? job.countQueue[i] : -1)}"
                        )
                )
        );

        yield return nextTarget;
        yield return logExtractedTarget;
        yield return tickBreak;

        // Notify_Starting couldn't find anything to load after all (e.g. another pawn claimed
        // it first) - nothing was queued, so there's nothing left to do.
        yield return Toils_Jump.JumpIf(wait, () => TargetThingA == null);

        yield return gotoThing;
        yield return Toils_Jump.JumpIf(startCarryPawn, () => TargetThingA is Pawn);

        // This extracted item is part of the trailing hand-carry group once what's left in the
        // queue drops below _handCarryGroupCount (everything remaining at that point is, by
        // construction, a hand-carry group item) - carry & merge it instead of stashing it in
        // inventory.
        yield return Toils_Jump.JumpIf(
            carryGatheredThing,
            () => job.GetTargetQueue(TargetIndex.A).Count < _handCarryGroupCount
        );

        // The hand-carry group is empty in hand (e.g. its stack(s) disappeared or were fully
        // claimed by someone else right at pickup, rare but possible), or nothing was ever
        // hand-carried on this trip - nothing's in hand, so skip straight to unloading whatever
        // already made it into inventory instead of pausing/depositing nothing.
        var checkCarriedHand = Toils_Jump.JumpIf(
            pullNextCarriedThing,
            () => pawn.carryTracker.CarriedThing == null
        );

        yield return takeThing;

        // ExtractNextTargetFromQueue is a no-op once the queue is empty - it does NOT clear
        // TargetIndex.A/job.count, so jumping back to it unconditionally here would leave
        // TargetThingA stuck on this same already-picked-up item forever instead of ever
        // reaching null. Only loop back while there's actually something left to extract.
        yield return Toils_Jump.JumpIf(
            nextTarget,
            () => !job.GetTargetQueue(TargetIndex.A).NullOrEmpty()
        );
        yield return Toils_Jump.Jump(checkCarriedHand);

        yield return carryGatheredThing;

        // More hand-carry group items remain to be merged into the same carried stack - loop back
        // for the next one instead of falling through to the deposit sequence early.
        yield return Toils_Jump.JumpIf(
            nextTarget,
            () => !job.GetTargetQueue(TargetIndex.A).NullOrEmpty()
        );

        yield return checkCarriedHand;
        yield return carryPause;

        // Walks to the transporter while visibly carrying whatever's currently in hand - matches
        // vanilla JobDriver_HaulToTransporter's own StartCarryThing -> CarryHauledThingToContainer
        // sequence (no separate empty-handed goto beforehand), so the pawn is never seen walking
        // the whole way there with nothing shown in their hands.
        yield return carryToContainer;
        yield return depositCarriedThing;
        yield return Toils_Jump.JumpIf(pullNextCarriedThing, () => _carriedThings.Count > 0);
        yield return Toils_Jump.Jump(wait);

        // Everything else gathered on this trip went into inventory - pull it out into hand one
        // stack at a time and deposit it, looping until inventory's empty.
        yield return pullNextCarriedThing;
        yield return Toils_Jump.JumpIf(wait, () => pawn.carryTracker.CarriedThing == null);
        yield return carryPause;
        yield return carryToContainer;
        yield return depositCarriedThing;
        yield return Toils_Jump.JumpIf(pullNextCarriedThing, () => _carriedThings.Count > 0);
        yield return Toils_Jump.Jump(wait);

        yield return startCarryPawn;
        yield return gotoTransporterWithPawn;
        yield return depositPawn;

        yield return wait;
    }
}
