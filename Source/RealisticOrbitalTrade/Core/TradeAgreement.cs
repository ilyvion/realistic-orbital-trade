using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Core;
using RealisticOrbitalTrade.Extensions;
using RimWorld.Planet;
using Verse.AI;

namespace RealisticOrbitalTrade;

[HotSwappable]
internal class TradeAgreement : IExposable, ILoadReferenceable, IThingHolder
{
    private class Tradeable_Capped(TradeAgreement owner, int cap) : Tradeable
    {
        public int cap = cap;

        // Vanilla's own colony scan (TradeUtility.AllLaunchableThingsForTrade) only ever looks at
        // stock currently sitting within a trade beacon's range - it can't see the portion of an
        // already-sold stack that's since been split off and is now either loaded onto the
        // trader's shuttle or physically in a hauling pawn's hands/inventory en route to it, so
        // both are added back in here. cap (the cumulative amount actually agreed to be sold)
        // still floors the total on top of that, in case some of it is briefly invisible to all
        // three sources at once (e.g. mid-SplitOff).
        // Only the colony side is capped/floored here - thingsTrader (e.g. the trader's own
        // silver funds, for a currency tradeable) is reported normally, since TradeUI's row
        // (the "TraderCount" column) reads CountHeldBy(Trader) to show what the trader is
        // holding, unrelated to what this class exists to fix up on the colony side.
        public override int CountHeldBy(Transactor trans)
        {
            if (trans != Transactor.Colony)
            {
                return base.CountHeldBy(trans);
            }
            var total = base.CountHeldBy(trans) + owner.CountOfMatchingThingsInFlightToTrader(this);
            return Math.Max(total, cap);
        }
    }

    /// <summary>
    /// Sums the stack counts of everything matching <paramref name="tradeable"/> that's already
    /// loaded onto <see cref="toTraderTransportShip"/>'s cargo, or currently being carried toward
    /// it by a hauling pawn - the two places an already-sold item can be that vanilla's own colony
    /// scan won't find (see <see cref="Tradeable_Capped.CountHeldBy"/>).
    /// </summary>
    /// <remarks>
    /// A full-stack haul moves <c>item.thing</c>'s own reference into the pawn's hand/inventory or
    /// the shuttle's container rather than splitting a copy off it, so that same reference can
    /// already be sitting in <paramref name="tradeable"/>'s <c>thingsColony</c> (and so already
    /// counted by <c>base.CountHeldBy</c>) by the time it's also found here - anything already
    /// there is skipped to avoid counting it twice.
    /// </remarks>
    private int CountOfMatchingThingsInFlightToTrader(Tradeable tradeable)
    {
        if (toTraderTransportShip == null)
        {
            return 0;
        }

        var compTransporter = toTraderTransportShip.TransporterComp;
        var map = tradeShip.Map;

        var notAlreadyCounted = compTransporter
            .innerContainer.Concat(Utils.ThingsCarriedTowardTransporter(compTransporter, map))
            .Where(thing => !tradeable.thingsColony.Contains(thing));

        return SumMatchingStackCounts(tradeable, notAlreadyCounted);
    }

    /// <summary>
    /// Sums the stack counts of every entry in <paramref name="things"/> that belongs to
    /// <paramref name="tradeable"/> under the same matching rules
    /// <see cref="TransferableUtility.TradeableMatching"/> already uses everywhere else in this
    /// merge (same def/stuff/quality bucket).
    /// </summary>
    internal static int SumMatchingStackCounts(Tradeable tradeable, IEnumerable<Thing> things)
    {
        var count = 0;
        foreach (var thing in things)
        {
            if (TransferableUtility.TradeableMatching(thing, [tradeable]) == tradeable)
            {
                count += thing.stackCount;
            }
        }
        return count;
    }

    protected int loadID = -1;

    public TradeShip tradeShip;
    public Pawn negotiator;

    public ThingOwner<Thing> thingsSoldToPlayer;
    public List<ThingCountClass> thingsSoldToTrader = [];
    public List<Pawn> pawnsSoldToTrader = [];

    // Things sold to the trader but not yet claimed by a hauling job, kept protected from other
    // colony jobs (eating, crafting, wearing, etc.) by Patch/Rimworld_ReservationManager.cs. An
    // entry's count shrinks (and is removed once it hits 0) the moment a hauling job actually
    // reserves that portion of the stack for real - see
    // JobDriver_HaulMultipleToTransporter/Rimworld_JobDriver_HaulToTransporter_Notify_Starting -
    // so it only ever covers the portion still sitting unclaimed in the colony. Missing on saves
    // from before this feature existed; PostExposeData defaults it to empty, so trades already in
    // progress simply get no protection for items sold before the update, same as if the feature
    // silently failed to find anything to protect.
    public List<ThingCountClass> reservedTraderThings = [];

    public TransportShip? toTraderTransportShip;

    [Obsolete]
    public TransportShip? toPlayerTransportShip;

    public TradeDeliveryMethod? tradeDeliveryMethod;

    public bool tradePausesDepartureTimer;
    public bool deliverItemsViaDropPod;

    public IThingHolder ParentHolder => tradeShip.Map;

    public List<Tradeable> AllTradeables
    {
        get
        {
            if (tradeDeliveryMethod == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "tradeDeliveryMethod is null in TradeAgreement. This is a bug, can't calculate AllTradeables."
                );
                return [];
            }
            if (toTraderTransportShip == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "toTraderTransportShip is null in TradeAgreement. This is a bug, can't calculate AllTradeables."
                );
                return [];
            }

            List<Tradeable> tradeables = [];
            MergeAgreedTradeablesInto(tradeables);
            return tradeables;
        }
    }

    /// <summary>
    /// Merges this agreement's already-committed state (bought-from-trader things, sold-to-trader
    /// things/pawns) into <paramref name="tradeables"/>, forcing each matched/injected
    /// <see cref="Tradeable.CountToTransfer"/> to the agreed amount. Used both to build
    /// <see cref="AllTradeables"/> from scratch and, during renegotiation, to seed a live
    /// <see cref="TradeDeal"/>'s own tradeables list (already populated by vanilla's own
    /// currently-sellable/buyable scan) with the current deal on top.
    /// </summary>
    /// <remarks>
    /// When merging into a list a vanilla scan already populated, the same physical
    /// <see cref="Thing"/> can already be present in a matched tradeable's <c>thingsColony</c>/
    /// <c>thingsTrader</c> (e.g. an already-sold item still sitting untouched in a stockpile is
    /// both "currently sellable stock" and "already agreed to be sold"). Adding it again would
    /// double-count its stack count in <see cref="Tradeable.CountHeldBy"/> and mass calculations,
    /// so each thing is only ever added once per tradeable regardless of how many times it's
    /// encountered here.
    /// </remarks>
    internal void MergeAgreedTradeablesInto(List<Tradeable> tradeables)
    {
        foreach (var item in tradeDeliveryMethod!.Things)
        {
            var tradeable = TransferableUtility.TradeableMatching(item, tradeables);
            if (tradeable == null)
            {
                tradeable = (item is not Pawn) ? new Tradeable() : new Tradeable_Pawn();
                tradeables.Add(tradeable);
            }
            if (!tradeable.thingsTrader.Contains(item))
            {
                tradeable.AddThing(item, Transactor.Trader);
            }
            tradeable.ForceTo(tradeable.CountToTransfer + item.stackCount);
        }
        // A sold thing/pawn can be lost (thing/pawn reference null, a destroyed thing's
        // Scribe_References reference resolves to null on load) or destroyed in the same
        // session (reference still valid, Destroyed == true). Both mean it's gone and can't
        // be delivered, so it's excluded here.
        foreach (var item in thingsSoldToTrader)
        {
            if (IsSoldThingLost(item))
            {
                continue;
            }

            var tradeable = TransferableUtility.TradeableMatching(item.thing, tradeables);
            if (tradeable == null)
            {
                tradeable = new Tradeable_Capped(this, item.Count);
                tradeables.Add(tradeable);
            }
            else if (tradeable is Tradeable_Capped capped)
            {
                capped.cap += item.Count;
            }
            else
            {
                // Vanilla's own scan already created a plain Tradeable for this def (e.g. other
                // loose stock of the same item elsewhere on the map, or - for silver - the single
                // currency tradeable TradeDeal always seeds) - upgrade it in place to a capped one
                // so the amount already committed here isn't lost to whatever vanilla happened to
                // find reachable. TradeableMatching matches purely by AnyThing, not by Tradeable
                // subtype, so later items sharing this def still find this same upgraded entry
                // rather than creating a duplicate.
                var index = tradeables.IndexOf(tradeable);
                var upgraded = new Tradeable_Capped(this, item.Count)
                {
                    thingsColony = tradeable.thingsColony,
                    thingsTrader = tradeable.thingsTrader,
                };
                upgraded.ForceTo(tradeable.CountToTransfer);
                tradeables[index] = upgraded;
                tradeable = upgraded;
            }
            if (!tradeable.thingsColony.Contains(item.thing))
            {
                tradeable.AddThing(item.thing, Transactor.Colony);
            }
            tradeable.ForceTo(tradeable.CountToTransfer - item.Count);
        }
        foreach (var item in pawnsSoldToTrader)
        {
            if (IsSoldPawnLost(item))
            {
                continue;
            }

            var tradeable = TransferableUtility.TradeableMatching(item, tradeables);
            if (tradeable == null)
            {
                tradeable = new Tradeable_Pawn();
                tradeables.Add(tradeable);
            }
            if (!tradeable.thingsColony.Contains(item))
            {
                tradeable.AddThing(item, Transactor.Colony);
            }
            tradeable.ForceTo(-1);
        }
    }

    /// <summary>
    /// Force-reduces every sold-to-trader <paramref name="tradeables"/> entry (negative
    /// <see cref="Tradeable.CountToTransfer"/>) to, at most, however much of it is present in
    /// <paramref name="loadedThings"/> - anything sold but not actually loaded yet is dropped from
    /// the deal entirely, as if it had never been sold. Bought-from-trader entries are left
    /// untouched. Used to lock in only the already-loaded portion of a sale when the trader is
    /// leaving before loading finished - see <see cref="Utils.TryBeginDepartureRenegotiation"/>.
    /// </summary>
    internal static void ClampSoldTradeablesToLoaded(
        List<Tradeable> tradeables,
        IEnumerable<Thing> loadedThings
    )
    {
        var loaded = loadedThings as ICollection<Thing> ?? [.. loadedThings];
        foreach (var tradeable in tradeables)
        {
            if (tradeable.CountToTransfer >= 0)
            {
                continue;
            }

            if (tradeable.AnyThing is Pawn pawn)
            {
                tradeable.ForceTo(loaded.Contains(pawn) ? tradeable.CountToTransfer : 0);
                continue;
            }

            var loadedCount = SumMatchingStackCounts(tradeable, loaded);
            tradeable.ForceTo(-Math.Min(loadedCount, -tradeable.CountToTransfer));
        }
    }

    /// <summary>
    /// Same as <see cref="AllTradeables"/>, except every sold-to-trader tradeable is clamped to
    /// what's physically loaded onto <see cref="toTraderTransportShip"/> right now - see
    /// <see cref="ClampSoldTradeablesToLoaded"/>.
    /// </summary>
    internal List<Tradeable> LoadedOnlyTradeables()
    {
        var tradeables = AllTradeables;
        ClampSoldTradeablesToLoaded(
            tradeables,
            toTraderTransportShip!.TransporterComp.innerContainer
        );
        return tradeables;
    }

    internal static bool IsSoldThingLost(ThingCountClass thingCount) =>
        thingCount.thing == null || thingCount.thing.Destroyed;

    internal static bool IsSoldPawnLost(Pawn? pawn) => pawn == null || pawn.Destroyed;

    // Pawn_TraderTracker.ColonyThingsWillingToBuy() dereferences negotiator.Map, so it can't run
    // once the negotiator has died or otherwise left the map. The player has to register a
    // replacement negotiator with the shuttle (see JobDriver_RegisterAsNegotiator) before
    // autoload/renegotiation can resume, rather than one being picked automatically, since who
    // negotiates affects trade prices.
    public bool NegotiatorAvailable => negotiator.Spawned;

    // Set once the player has been notified their negotiator is unavailable, so the letter is
    // only sent once per trade even though availability is checked repeatedly. Cleared when a
    // new negotiator registers, so a later death during the same trade sends a fresh letter.
    public bool NegotiatorUnavailableLetterSent;

    public bool ShouldSendNegotiatorUnavailableLetter =>
        !NegotiatorAvailable && !NegotiatorUnavailableLetterSent;

    // Set when a thing/pawn already sold to the trader is lost before delivery, which leaves the
    // trade out of balance (the trader would be shortchanged). Blocks shuttle departure (see
    // CompTradeShuttle.AllRequiredThingsLoaded) until a renegotiation re-balances the deal.
    public bool RenegotiationRequired;

    public IEnumerable<Thing> ColonyThingsTraderWillingToBuy() =>
        NegotiatorAvailable
            ? new Pawn_TraderTracker(negotiator).ColonyThingsWillingToBuy(negotiator)
            : [];

#pragma warning disable CS8618 // Required by savegame logic
    public TradeAgreement()
#pragma warning restore CS8618
    { }

    public TradeAgreement(
        TradeShip tradeShip,
        Pawn negotiator,
        bool tradePausesDepartureTimer,
        bool deliverItemsViaDropPod
    )
    {
        this.tradeShip = tradeShip;
        this.negotiator = negotiator;
        this.tradePausesDepartureTimer = tradePausesDepartureTimer;
        this.deliverItemsViaDropPod = deliverItemsViaDropPod;

        thingsSoldToPlayer = new(this);

        loadID = RealisticOrbitalTradeGameComponent.Current.GetNextTradeId();
    }

    public void ExposeData()
    {
        PreExposeData();

        Scribe_Values.Look(ref loadID, "loadID", 0);
        Scribe_References.Look(ref tradeShip, "tradeShip");
        Scribe_References.Look(ref negotiator, "negotiator");
        Scribe_Values.Look(
            ref NegotiatorUnavailableLetterSent,
            "negotiatorUnavailableLetterSent",
            false
        );
        Scribe_Values.Look(ref tradePausesDepartureTimer, "tradePausesDepartureTimer", false);
        Scribe_Values.Look(ref RenegotiationRequired, "renegotiationRequired", false);
        Scribe_Deep.Look(ref thingsSoldToPlayer, "thingsSoldToPlayer", this);
        Scribe_Collections.Look(ref tmpSavedPawns, "tmpSavedPawns", LookMode.Reference);
        Scribe_Values.Look(ref deliverItemsViaDropPod, "deliverItemsViaDropPod", false);
        Scribe_Collections.Look(ref thingsSoldToTrader, "thingsSoldToTrader", LookMode.Deep);
        Scribe_Collections.Look(ref pawnsSoldToTrader, "pawnsSoldToTrader", LookMode.Reference);
        Scribe_Collections.Look(ref reservedTraderThings, "reservedTraderThings", LookMode.Deep);
        reservedTraderThings ??= [];
        Scribe_References.Look(ref toTraderTransportShip, "toTraderTransportShip");

        Scribe_Deep.Look(ref tradeDeliveryMethod, "tradeDeliveryMethod");

#pragma warning disable CS0612 // We still load it for backwards compatibility
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            if (toPlayerTransportShip != null)
            {
                tradeDeliveryMethod = new TradeDeliveryMethod_TransportShip(toPlayerTransportShip);
            }
            if (toTraderTransportShip == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "A trade agreement was loaded with a null toTraderTransportShip. This is a bug, please report it with a full log."
                );
            }
        }
#pragma warning restore CS0612

        PostExposeData();
    }

    public string GetUniqueLoadID() => "TradeData_" + loadID;

    public void GetChildHolders(List<IThingHolder> outChildren) =>
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());

    public ThingOwner GetDirectlyHeldThings() => thingsSoldToPlayer;

    // We need the below because you can't serialize world Pawns deeply

    private readonly List<Thing> tmpThings = [];
    private List<Pawn> tmpSavedPawns = [];

    private void PreExposeData()
    {
        if (Scribe.mode == LoadSaveMode.Saving)
        {
            tmpThings.Clear();
            tmpThings.AddRange(thingsSoldToPlayer);
            tmpSavedPawns.Clear();
            for (var i = 0; i < tmpThings.Count; i++)
            {
                if (tmpThings[i] is Pawn pawn)
                {
                    _ = thingsSoldToPlayer.Remove(pawn);
                    tmpSavedPawns.Add(pawn);
                    if (!pawn.IsWorldPawn())
                    {
                        Log.Error(
                            "Trying to save a non-world pawn ("
                                + pawn.ToString()
                                + ") as a reference in a trade agreement."
                        );
                    }
                }
            }
            tmpThings.Clear();
        }
    }

    private void PostExposeData()
    {
        if (Scribe.mode is LoadSaveMode.PostLoadInit or LoadSaveMode.Saving)
        {
            for (var j = 0; j < tmpSavedPawns.Count; j++)
            {
                _ = thingsSoldToPlayer.TryAdd(tmpSavedPawns[j]);
            }
            tmpSavedPawns.Clear();
        }

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            PruneDestroyedSoldItems();
        }
    }

    // Things destroyed while sold to the trader but not yet delivered resolve to a null
    // Scribe_References reference on load (they aren't reachable anywhere else in the save).
    // Unlike Notify_ThingDestroyed, we can no longer identify which specific required-cargo entry
    // corresponds to what was lost, so instead of a targeted reduction, the shuttle's required
    // cargo is fully rebuilt from what remains of thingsSoldToTrader. The player is told to
    // renegotiate, since the deal itself (e.g. silver owed) may still need rebalancing even though
    // the shuttle can now depart without the impossible-to-fulfill requirement.
    private void PruneDestroyedSoldItems()
    {
        var lostAnything = thingsSoldToTrader.RemoveAll(t => t.thing == null) > 0;
        lostAnything |= pawnsSoldToTrader.RemoveAll(p => p == null) > 0;
        _ = reservedTraderThings.RemoveAll(t => t.thing == null);

        if (toTraderTransportShip is { } transportShip)
        {
            // QuestNode_Root_TradeShipTransportShip aliases requiredPawns directly to
            // pawnsSoldToTrader at deal creation, so live pawn removals
            // (Notify_OwnThingDestroyed) stay in sync automatically. Scribe reconstructs each
            // list independently on load, breaking that aliasing - re-establish it.
            transportShip.ShuttleComp.requiredPawns = pawnsSoldToTrader;

            if (lostAnything)
            {
                var compTradeShuttle = transportShip.shipThing.TryGetComp<CompTradeShuttle>();
                Utils.RebuildRequiredCargo(this, compTradeShuttle, transportShip.ShuttleComp);
            }
        }

        if (!lostAnything)
        {
            return;
        }

        RealisticOrbitalTradeMod.Instance.LogWarning(
            $"Trade agreement {GetUniqueLoadID()} had one or more sold-to-trader entries whose thing/pawn no longer exists (destroyed before the game was saved). Removed them and rebuilt the shuttle's required cargo to match; the trade may still require renegotiation to rebalance."
        );
        RenegotiationRequired = true;
        SendSoldItemLostLetter();
    }

    public static void Notify_ThingDestroyed(Thing thing)
    {
        // Thing.Destroy fires constantly outside actual gameplay too (def loading, main-menu
        // scratch objects, RimTest Redux's in-game test runs before/without a real save loaded).
        // Game.GetComponent<T> indexes into Game.components, which is only populated once a real
        // game is playing, so it's not enough to check Current.Game != null - a non-playing Game
        // instance can still exist with components left uninitialized.
        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        var tradeAgreements = RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements;
        if (tradeAgreements == null)
        {
            return;
        }

        for (var i = 0; i < tradeAgreements.Count; i++)
        {
            tradeAgreements[i].Notify_OwnThingDestroyed(thing);
        }
    }

    // Thing.TryAbsorbStack destroys a fully-merged-in thing as an implementation detail of
    // stacking, not an actual loss - the sold items are still physically present, just
    // folded into the surviving stack. Re-point tracking there instead of treating it as
    // Notify_ThingDestroyed would.
    public static void Notify_ThingMerged(Thing absorbedThing, Thing survivingThing)
    {
        if (Current.ProgramState != ProgramState.Playing)
        {
            return;
        }

        var tradeAgreements = RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements;
        if (tradeAgreements == null)
        {
            return;
        }

        for (var i = 0; i < tradeAgreements.Count; i++)
        {
            tradeAgreements[i].Notify_OwnThingMerged(absorbedThing, survivingThing);
        }
    }

    private void Notify_OwnThingMerged(Thing absorbedThing, Thing survivingThing)
    {
        foreach (var thingCount in thingsSoldToTrader.Where(t => t.thing == absorbedThing))
        {
            thingCount.thing = survivingThing;
        }

        var absorbedReservation = reservedTraderThings.FirstOrDefault(t =>
            t.thing == absorbedThing
        );
        if (absorbedReservation == null)
        {
            return;
        }
        _ = reservedTraderThings.Remove(absorbedReservation);
        var survivingReservation = reservedTraderThings.FirstOrDefault(t =>
            t.thing == survivingThing
        );
        if (survivingReservation != null)
        {
            survivingReservation.Count += absorbedReservation.Count;
        }
        else
        {
            absorbedReservation.thing = survivingThing;
            reservedTraderThings.Add(absorbedReservation);
        }
    }

    private void Notify_OwnThingDestroyed(Thing thing)
    {
        if (toTraderTransportShip == null)
        {
            return;
        }

        bool removed;
        if (thing is Pawn pawn)
        {
            removed = pawnsSoldToTrader.Remove(pawn);
            if (removed)
            {
                _ = toTraderTransportShip.ShuttleComp.requiredPawns.Remove(pawn);
                CancelHaulingJobFor(pawn);
            }
        }
        else
        {
            var thingCount = thingsSoldToTrader.FirstOrDefault(t => t.thing == thing);
            removed = thingCount != null;
            if (removed)
            {
                _ = thingsSoldToTrader.Remove(thingCount!);
                var reservedCount = reservedTraderThings.FirstOrDefault(t => t.thing == thing);
                if (reservedCount != null)
                {
                    _ = reservedTraderThings.Remove(reservedCount);
                }
                ReduceRequiredCargoFor(thing, thingCount!.Count);
                CancelHaulingJobFor(thing);
            }
        }

        if (!removed)
        {
            return;
        }

        RenegotiationRequired = true;
        toTraderTransportShip.shipThing.TryGetComp<CompTradeShuttle>()?.CheckAutoload();
        SendSoldItemLostLetter();
    }

    private void ReduceRequiredCargoFor(Thing thing, int count)
    {
        var leftToTakeOff = count;
        var compTradeShuttle = toTraderTransportShip!.shipThing.TryGetComp<CompTradeShuttle>();
        var reduced = thing.HasRequirements()
            ? ThingDefCountWithRequirements.TryReduceRequiredCount(
                compTradeShuttle.requiredSpecificItems,
                thing,
                ref leftToTakeOff
            )
            : Utils.TryReduceRequiredItemsCount(
                toTraderTransportShip.ShuttleComp.requiredItems,
                thing.def,
                ref leftToTakeOff
            );
        if (!reduced)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                $"Destroyed {thing} was tracked as sold to the trader, but no matching required-cargo entry was found to reduce."
            );
        }
    }

    private void CancelHaulingJobFor(Thing thing)
    {
        var compTransporter = toTraderTransportShip!.TransporterComp;
        var map = toTraderTransportShip.shipThing.Map;
        if (map == null)
        {
            return;
        }

        foreach (
            var jobPawn in Utils
                .PawnsWithHaulToTransporterJob(compTransporter, map)
                .Where(jp =>
                    jp.carryTracker.CarriedThing == thing || jp.CurJob.targetA.Thing == thing
                )
                .ToList()
        )
        {
            jobPawn.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
        }
    }

    private void SendSoldItemLostLetter()
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(
            "Sending sold item lost letter\n" + StackTraceUtility.ExtractStackTrace()
        );
        Find.LetterStack.ReceiveLetter(
            "RealisticOrbitalTrade.SoldItemLostLetterLabel".Translate(),
            "RealisticOrbitalTrade.SoldItemLostLetterText".Translate(tradeShip.TraderName),
            LetterDefOf.NegativeEvent,
            toTraderTransportShip?.shipThing
        );
    }

    /// <summary>
    /// How much of <paramref name="thing"/> is still tracked as reserved-for-protection (i.e. not
    /// yet claimed by a hauling job).
    /// </summary>
    internal int ReservedCountFor(Thing thing) =>
        reservedTraderThings.FirstOrDefault(t => t.thing == thing)?.Count ?? 0;

    /// <summary>
    /// Starts protecting <paramref name="count"/> units of <paramref name="thing"/> from other
    /// colony jobs. Called when a thing/count is newly sold to the trader.
    /// </summary>
    internal void AddReservation(Thing thing, int count)
    {
        var existing = reservedTraderThings.FirstOrDefault(t => t.thing == thing);
        if (existing != null)
        {
            existing.Count += count;
        }
        else
        {
            reservedTraderThings.Add(new(thing, count));
        }
    }

    /// <summary>
    /// Stops protecting up to <paramref name="count"/> units of <paramref name="thing"/>, clamped
    /// to however much is still tracked - called right before a hauling job takes out a real
    /// <see cref="ReservationManager"/> reservation on that portion, so the two never both think
    /// they're responsible for the same units at once. Returns how much was actually released, so
    /// a failed real reservation can restore exactly that amount via <see cref="RestoreReservation"/>.
    /// </summary>
    internal int ReduceReservation(Thing thing, int count)
    {
        var existing = reservedTraderThings.FirstOrDefault(t => t.thing == thing);
        if (existing == null || count <= 0)
        {
            return 0;
        }

        var reduced = Math.Min(existing.Count, count);
        existing.Count -= reduced;
        if (existing.Count <= 0)
        {
            _ = reservedTraderThings.Remove(existing);
        }
        return reduced;
    }

    /// <summary>
    /// Re-protects <paramref name="count"/> units of <paramref name="thing"/> previously released
    /// via <see cref="ReduceReservation"/>, for when the real reservation it was making way for
    /// failed after all.
    /// </summary>
    internal void RestoreReservation(Thing thing, int count)
    {
        if (count > 0)
        {
            AddReservation(thing, count);
        }
    }

    /// <summary>
    /// Reserved-for-protection things still sitting untouched on the map - anything left here once
    /// the shuttle departs means a sold item never got claimed by a hauling job, which shouldn't
    /// happen if the mod's own hauling logic ran to completion.
    /// </summary>
    internal IEnumerable<Thing> StillReservedSpawnedThings() =>
        reservedTraderThings.Where(t => t.thing is { Spawned: true }).Select(t => t.thing);

    /// <summary>
    /// Releases whatever's left reserved across <paramref name="transferable"/>'s stacks beyond
    /// <paramref name="stillNeeded"/> - the amount hauling jobs (this pawn's included) haven't
    /// already claimed and still intend to come back for. The hauling algorithms treat same-def
    /// stacks as fungible - whichever's closest gets grabbed, not necessarily the specific stack
    /// <see cref="AddReservation"/> was originally called on for the sale - so a stack can easily
    /// end up with only part of its own reservation ever claimed by a haul, while the rest of the
    /// transferable's need gets satisfied entirely out of other stacks. <see
    /// cref="Transferable.CountToTransfer"/> itself can't be used for this: it only drops once
    /// items are actually delivered, which lags behind hauling jobs claiming/reserving them, so it
    /// always reads too high while a haul is still in flight. The caller is expected to pass the
    /// amount still left to claim from here on (accounting for every pawn's current claims,
    /// including the one that just succeeded), which is the only number that can legitimately go
    /// to zero while stock is still undelivered.
    /// </summary>
    internal void ReconcileReservation(TransferableOneWay? transferable, int stillNeeded)
    {
        if (transferable == null)
        {
            return;
        }

        var entries = reservedTraderThings
            .Where(t => transferable.things.Contains(t.thing))
            .ToList();
        var excess = entries.Sum(t => t.Count) - Math.Max(0, stillNeeded);
        foreach (var entry in entries)
        {
            if (excess <= 0)
            {
                break;
            }

            var release = Math.Min(entry.Count, excess);
            entry.Count -= release;
            excess -= release;
            if (entry.Count <= 0)
            {
                _ = reservedTraderThings.Remove(entry);
            }
        }
    }

    internal enum TransferDirection
    {
        ToPlayer,
        ToTrader,
    }

    /// <summary>
    /// Whether <paramref name="newTradeables"/> (a renegotiation's edited deal) amounts to
    /// reducing everything to zero - offered to the player as "did you mean to cancel the trade?"
    /// rather than silently applying a no-op-looking deal.
    /// </summary>
    internal static bool IsFullCancellation(List<Tradeable> newTradeables) =>
        newTradeables.Sum(t => Math.Abs(t.CountToTransfer)) == 0;

    /// <summary>
    /// The most silver the colony can still pay the trader during a departure renegotiation - like
    /// any other already-loaded good (see <see cref="ClampSoldTradeablesToLoaded"/>), silver that's
    /// physically on the shuttle can't be added to further, only reduced. Unlike other goods this
    /// never shows up as a tradeable's own <see cref="Tradeable.CountToTransfer"/> in the UI, since
    /// vanilla's <c>TradeDeal.UpdateCurrencyCount</c> recalculates the currency tradeable every
    /// frame to auto-balance the deal, overwriting it - see
    /// <see cref="Patch.Rimworld_Dialog_Trade_DoWindowContents"/>'s silver-ceiling row.
    /// </summary>
    internal static int MaxPayableSilver(IEnumerable<Tradeable> originalTradeables) =>
        originalTradeables.FirstOrDefault(t => t.IsCurrency) is { CountToTransfer: < 0 } currency
            ? -currency.CountToTransfer
            : 0;

    /// <summary>
    /// Pure diff: compares <paramref name="originalTradeables"/> (the deal as agreed before the
    /// renegotiation dialog opened, see <see cref="AllTradeables"/>) against
    /// <paramref name="newTradeables"/> (the live <see cref="TradeDeal"/>'s tradeables after the
    /// player edited it) and returns only the delta - both reductions/increases on already-agreed
    /// tradeables and brand-new additions (a tradeable present only in
    /// <paramref name="newTradeables"/>). No side effects; <see cref="ApplyRenegotiation"/> is
    /// what actually moves things around based on this.
    /// </summary>
    internal static List<(
        TransferDirection direction,
        Tradeable tradeable,
        int amount
    )> ComputeRenegotiationChanges(
        List<Tradeable> originalTradeables,
        List<Tradeable> newTradeables
    )
    {
        List<(TransferDirection direction, Tradeable tradeable, int amount)> changes = [];
        HashSet<Tradeable> matchedNewTradeables = [];
        foreach (var originalTradeable in originalTradeables)
        {
            var tradedThingDef = originalTradeable.ThingDef;
            var newTradeable =
                tradedThingDef != RimWorld.ThingDefOf.Silver
                    ? TransferableUtility.TradeableMatching(
                        originalTradeable.AnyThing,
                        newTradeables
                    )
                    : newTradeables.FirstOrDefault(t => t.IsCurrency);
            if (newTradeable == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    $"Could not find a matching tradeable for {originalTradeable.AnyThing?.LabelCap} during renegotiation; skipping."
                );
                continue;
            }
            _ = matchedNewTradeables.Add(newTradeable);

            if (originalTradeable.CountToTransfer == newTradeable.CountToTransfer)
            {
                // This trade was not modified
                continue;
            }

            // negative number: colony gets less
            // positive number: trader gets less
            var change = newTradeable.CountToTransfer - originalTradeable.CountToTransfer;
            var stillSameDirection =
                ((originalTradeable.CountToTransfer < 0) && (newTradeable.CountToTransfer < 0))
                || ((originalTradeable.CountToTransfer > 0) && (newTradeable.CountToTransfer > 0))
                || originalTradeable.CountToTransfer == 0
                || newTradeable.CountToTransfer == 0;
            if (stillSameDirection)
            {
                // The change is a mere reduction
                if (change > 0 && originalTradeable.CountToTransfer < 0)
                {
                    changes.Add((TransferDirection.ToTrader, newTradeable, -change));
                }
                else if (change < 0 && originalTradeable.CountToTransfer < 0)
                {
                    changes.Add((TransferDirection.ToTrader, newTradeable, -change));
                }
                else if (change > 0 && originalTradeable.CountToTransfer > 0)
                {
                    changes.Add((TransferDirection.ToPlayer, newTradeable, change));
                }
                else if (change < 0 && originalTradeable.CountToTransfer > 0)
                {
                    changes.Add((TransferDirection.ToPlayer, newTradeable, change));
                }
            }
            else
            {
                // The change "switched sides" and requires both reduction and increase
                if (change > 0)
                {
                    changes.Add(
                        (
                            TransferDirection.ToTrader,
                            newTradeable,
                            originalTradeable.CountToTransfer
                        )
                    );
                    changes.Add(
                        (TransferDirection.ToPlayer, newTradeable, newTradeable.CountToTransfer)
                    );
                }
                else
                {
                    changes.Add(
                        (
                            TransferDirection.ToPlayer,
                            newTradeable,
                            -originalTradeable.CountToTransfer
                        )
                    );
                    changes.Add(
                        (TransferDirection.ToTrader, newTradeable, -newTradeable.CountToTransfer)
                    );
                }
            }
        }

        // Tradeables that only exist on the new side - brand-new additions with no counterpart in
        // the original deal.
        foreach (var newTradeable in newTradeables)
        {
            if (matchedNewTradeables.Contains(newTradeable) || newTradeable.CountToTransfer == 0)
            {
                continue;
            }

            changes.Add(
                newTradeable.CountToTransfer < 0
                    ? (TransferDirection.ToTrader, newTradeable, -newTradeable.CountToTransfer)
                    : (TransferDirection.ToPlayer, newTradeable, newTradeable.CountToTransfer)
            );
        }

        return changes;
    }

    /// <summary>
    /// Picks which <paramref name="candidates"/> entry <see cref="ApplyRenegotiation"/> should trim
    /// first when a sold amount is reduced - whichever isn't loaded yet
    /// (<paramref name="isLoaded"/> false), so an already-loaded stack is left alone and a stack
    /// still sitting on the map (no longer needed) gets dropped instead. Falls back to whatever's
    /// first among equally-loaded/unloaded candidates otherwise.
    /// </summary>
    internal static ThingCountClass? PreferUnloadedThingCount(
        IEnumerable<ThingCountClass> candidates,
        Func<Thing, bool> isLoaded
    ) => candidates.OrderBy(t => isLoaded(t.thing) ? 1 : 0).FirstOrDefault();

    /// <summary>
    /// Applies <see cref="ComputeRenegotiationChanges"/>'s delta between the deal as originally
    /// agreed and its edited state to the real shuttle/reservation/hauling state. Vanilla's own
    /// <see cref="TradeDeal.TryExecute"/>/<see cref="Tradeable.ResolveTrade"/> can't be reused for
    /// this: they always transfer a tradeable's absolute <c>CountToTransfer</c> from current
    /// availability, with no concept of "part of this was already delivered."
    /// </summary>
    /// <returns><see langword="true"/> if anything was actually changed.</returns>
    internal bool ApplyRenegotiation(
        List<Tradeable> originalTradeables,
        List<Tradeable> newTradeables
    )
    {
        var toTraderTransportShip = this.toTraderTransportShip!;
        var toTraderCompShuttle = toTraderTransportShip.ShuttleComp;
        var toTraderCompTransporter = toTraderTransportShip.TransporterComp;

        var tradeShipThings = tradeShip.things;
        var toPlayerThingOwner = tradeDeliveryMethod!.Things;
        var toTraderTransportShipContainer = toTraderCompTransporter.innerContainer;

        var toTraderShipThing = toTraderTransportShip.shipThing;
        var toTraderCompTradeShuttle = toTraderShipThing.TryGetComp<CompTradeShuttle>();

        var changes = ComputeRenegotiationChanges(originalTradeables, newTradeables);
        if (changes.Count == 0)
        {
            return false;
        }

        foreach (var (direction, tradeable, amount) in changes)
        {
            switch (direction)
            {
                case TransferDirection.ToPlayer:
                    if (amount > 0)
                    {
                        // Move things out of the trade ship and to the items meant for the player
                        TransferableUtility.TransferNoSplit(
                            tradeable.thingsTrader,
                            amount,
                            (toGive, countToGive) =>
                            {
                                // A fresh trade routes every bought pawn through this same
                                // PassToWorld/PreTraded/SetInitialPsyfocusLevel sequence exactly
                                // once, in QuestNode_Root_TradeShipTransportShip.PreTrade - but
                                // that only runs at initial shuttle setup, before any
                                // renegotiation. A pawn bought during renegotiation is never seen
                                // by that loop, so without doing it here too it would arrive on
                                // the player's shuttle still owned by no one (PreTraded is what
                                // calls SetFaction(Faction.OfPlayer)).
                                if (toGive is Pawn boughtPawn)
                                {
                                    if (!boughtPawn.IsWorldPawn())
                                    {
                                        Find.WorldPawns.PassToWorld(boughtPawn);
                                    }
                                    boughtPawn.PreTraded(
                                        TradeAction.PlayerBuys,
                                        negotiator,
                                        tradeShip
                                    );
                                    boughtPawn.psychicEntropy?.SetInitialPsyfocusLevel();
                                }
                                _ = toPlayerThingOwner.TryAddOrTransfer(toGive, countToGive);
                            }
                        );
                    }
                    else
                    {
                        // Move things out of the items meant for the player and back to the trade ship
                        var things = tradeable.thingsTrader;
                        var leftToTransfer = -amount;
                        foreach (
                            var thing in things.Where(t => t.holdingOwner == toPlayerThingOwner)
                        )
                        {
                            var (countToTransfer, remainingToTransfer, done) =
                                ComputeStackTransferAmount(leftToTransfer, thing.stackCount);
                            _ = tradeShipThings.TryAddOrTransfer(thing, countToTransfer);
                            leftToTransfer = remainingToTransfer;
                            if (done)
                            {
                                break;
                            }
                        }
                    }
                    break;

                case TransferDirection.ToTrader:
                    if (amount > 0)
                    {
                        TransferableUtility.TransferNoSplit(
                            tradeable.thingsColony,
                            amount,
                            (toGive, countToGive) =>
                            {
                                // A pawn sold to the trader during a fresh trade walks itself onto
                                // the shuttle via requiredPawns
                                // (QuestNode_Root_TradeShipTransportShip.RunInt sets
                                // toTraderCompShuttle.requiredPawns to this same pawnsSoldToTrader
                                // list reference, so appending here is picked up automatically) -
                                // it isn't hauled/reserved cargo like an ordinary item, see
                                // Rimworld_TradeShip_GiveSoldThingToTrader's prefix. Routing it
                                // through the ordinary-item path below instead would try to have it
                                // hauled and reserved like a crate of steel.
                                if (toGive is Pawn soldPawn)
                                {
                                    pawnsSoldToTrader.Add(soldPawn);
                                    return;
                                }
                                ThingCountClass thingCount = new(toGive, countToGive);
                                thingsSoldToTrader.Add(thingCount);
                                AddReservation(toGive, countToGive);
                                Utils.AddThingToLoadToShuttle(
                                    thingCount,
                                    toTraderCompTradeShuttle,
                                    toTraderCompShuttle
                                );
                            }
                        );
                    }
                    else
                    {
                        // This is the hard one. Things can be in three different states
                        // - Required to be loaded, but not loaded or being hauled yet
                        // - On the way to being loaded (i.e. being hauled)
                        // - Already loaded

                        if (tradeable.AnyThing is Pawn pawn)
                        {
                            // Remove from sold list
                            _ = pawnsSoldToTrader.Remove(pawn);
                            // Remove from shuttle's required cargo list
                            _ = toTraderCompShuttle.requiredPawns.Remove(pawn);

                            // If already loaded onto the shuttle, make the shuttle drop
                            // the pawn back out.
                            if (toTraderTransportShipContainer.Contains(pawn))
                            {
                                var unloaded = toTraderTransportShipContainer.TryDrop(
                                    pawn,
                                    toTraderTransportShip.GetDropLocation(),
                                    toTraderShipThing.Map,
                                    ThingPlaceMode.Near,
                                    out _,
                                    null,
                                    c =>
                                    {
                                        return !c.Fogged(toTraderShipThing.Map) && !pawn.Downed;
                                    },
                                    false
                                );
                                if (!unloaded)
                                {
                                    RealisticOrbitalTradeMod.Instance.LogError(
                                        $"Could not unload {pawn} from transport ship after renegotiation!"
                                    );
                                }
                            }

                            // Also cancel any hauling jobs featuring the pawn
                            var p = Utils
                                .PawnsWithHaulToTransporterJob(
                                    toTraderCompTransporter,
                                    toTraderShipThing.Map
                                )
                                .SingleOrDefault(jp =>
                                    jp.carryTracker.CarriedThing == pawn
                                    || jp.CurJob.targetA.Thing == pawn
                                );
                            p?.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                        }
                        else
                        {
                            var tradedThingDef = tradeable.ThingDef;

                            // Take things off the soldToTrader list. A sold def can span several
                            // stacks - some already loaded into the shuttle's container, some
                            // still sitting on the map waiting for a hauling job - so entries not
                            // yet loaded are taken off first (see PreferUnloadedThingCount).
                            // Otherwise this could just as easily trim an already-loaded stack
                            // instead, leaving the untouched one's now-stale reservation dangling
                            // on the map forever (it's no longer part of the sale, but nothing
                            // would ever release it).
                            var leftToTakeOff = -amount;
                            do
                            {
                                var thingCount = PreferUnloadedThingCount(
                                    tradeable.AnyThing.HasRequirements()
                                        ? thingsSoldToTrader.Where(t =>
                                            t.thing.GetInnerIfMinified() == tradeable.AnyThing
                                        )
                                        : thingsSoldToTrader.Where(t =>
                                            t.thing.def == tradedThingDef
                                        ),
                                    toTraderTransportShipContainer.Contains
                                );
                                if (thingCount == null)
                                {
                                    break;
                                }
                                if (thingCount.Count > leftToTakeOff)
                                {
                                    thingCount.Count -= leftToTakeOff;
                                    _ = ReduceReservation(thingCount.thing, leftToTakeOff);
                                    leftToTakeOff = 0;
                                }
                                else
                                {
                                    leftToTakeOff -= thingCount.Count;
                                    _ = thingsSoldToTrader.Remove(thingCount);
                                    _ = ReduceReservation(thingCount.thing, thingCount.Count);
                                }
                            } while (leftToTakeOff > 0);

                            // Take things off the to-be-loaded list
                            leftToTakeOff = -amount;
                            do
                            {
                                // First, remove things from the to-be-loaded list. If we can get away
                                // with just reducing that, we won't need to unload anything.
                                if (tradeable.AnyThing.HasRequirements())
                                {
                                    if (
                                        !ThingDefCountWithRequirements.TryReduceRequiredCount(
                                            toTraderCompTradeShuttle.requiredSpecificItems,
                                            tradeable.AnyThing.GetInnerIfMinified(),
                                            ref leftToTakeOff
                                        )
                                    )
                                    {
                                        RealisticOrbitalTradeMod.Instance.LogError(
                                            $"Needed to take {-amount} off the 'requiredItems' list, but could only remove {-amount - leftToTakeOff}"
                                        );
                                        break;
                                    }
                                }
                                else if (
                                    !Utils.TryReduceRequiredItemsCount(
                                        toTraderCompShuttle.requiredItems,
                                        tradeable.ThingDef,
                                        ref leftToTakeOff
                                    )
                                )
                                {
                                    RealisticOrbitalTradeMod.Instance.LogError(
                                        $"Needed to take {-amount} off the 'requiredItems' list, but could only remove {-amount - leftToTakeOff}"
                                    );
                                    break;
                                }
                            } while (leftToTakeOff > 0);

                            // Calculate how much has been loaded already
                            List<Thing> actuallyLoaded;
                            int countActuallyLoaded;
                            if (tradeable.AnyThing.HasRequirements())
                            {
                                actuallyLoaded =
                                [
                                    .. toTraderTransportShipContainer.Where(t =>
                                        t.GetInnerIfMinified() == tradeable.AnyThing
                                    ),
                                ];
                                countActuallyLoaded = actuallyLoaded.Sum(t => t.stackCount);
                            }
                            else
                            {
                                actuallyLoaded =
                                [
                                    .. toTraderTransportShipContainer.Where(t =>
                                        t.GetInnerIfMinified().def == tradeable.ThingDef
                                    ),
                                ];
                                countActuallyLoaded = actuallyLoaded.Sum(t => t.stackCount);
                            }

                            var countRequiredLoaded = tradeable.CountToTransferToDestination;
                            if (countActuallyLoaded > countRequiredLoaded)
                            {
                                // More items loaded than we've sold now; unload some
                                var leftToUnload = countActuallyLoaded - countRequiredLoaded;

                                var dropLoc = toTraderTransportShip.GetDropLocation();
                                foreach (var loaded in actuallyLoaded)
                                {
                                    var unloadAmount = Math.Min(leftToUnload, loaded.stackCount);
                                    var unloaded = toTraderTransportShipContainer.TryDrop(
                                        loaded,
                                        dropLoc,
                                        toTraderShipThing.Map,
                                        ThingPlaceMode.Near,
                                        unloadAmount,
                                        out _,
                                        null,
                                        c =>
                                        {
                                            return !c.Fogged(toTraderShipThing.Map)
                                                && c.GetFirstPawn(toTraderShipThing.Map) == null;
                                        }
                                    );
                                    if (!unloaded)
                                    {
                                        RealisticOrbitalTradeMod.Instance.LogError(
                                            $"Could not (fully) unload {tradedThingDef.label} from transport ship after renegotiation!"
                                        );
                                    }
                                    leftToUnload -= unloadAmount;
                                    if (leftToUnload == 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            // Also cancel any hauling jobs featuring the item
                            if (tradeable.AnyThing.HasRequirements())
                            {
                                foreach (
                                    var p in Utils
                                        .PawnsWithHaulToTransporterJob(
                                            toTraderCompTransporter,
                                            toTraderShipThing.Map
                                        )
                                        .Where(jp =>
                                            jp.carryTracker.CarriedThing == tradeable.AnyThing
                                            || jp.CurJob.targetA.Thing.GetInnerIfMinified()
                                                == tradeable.AnyThing
                                        )
                                )
                                {
                                    p.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                                }
                            }
                            else
                            {
                                foreach (
                                    var p in Utils
                                        .PawnsWithHaulToTransporterJob(
                                            toTraderCompTransporter,
                                            toTraderShipThing.Map
                                        )
                                        .Where(jp =>
                                            jp.carryTracker.CarriedThing?.def == tradeable.ThingDef
                                            || jp.CurJob.targetA.Thing.def == tradeable.ThingDef
                                        )
                                )
                                {
                                    p.jobs.EndCurrentJob(JobCondition.Incompletable, false, true);
                                }
                            }
                        }
                    }
                    break;
                default:
                    RealisticOrbitalTradeMod.Instance.LogError(
                        $"Unknown tradeable direction: {tradeable.AnyThing.LabelNoCount}"
                    );
                    break;
            }
        }

        // A completed renegotiation rebuilds the deal from what's currently physically available,
        // so it inherently re-balances a trade that a destroyed sold item put into discrepancy.
        RenegotiationRequired = false;

        // Immediately re-evaluate the auto-load list on the shuttle
        toTraderCompShuttle.CheckAutoload();

        return true;
    }

    // Computes how much of a stack to pull back onto the trade ship when returning silver/goods to
    // the trader: at most the amount still owed, and never the full stack count when it exceeds
    // that amount.
    internal static (
        int countToTransfer,
        int remainingToTransfer,
        bool done
    ) ComputeStackTransferAmount(int leftToTransfer, int stackCount)
    {
        var countToTransfer = Math.Min(leftToTransfer, stackCount);
        var remainingToTransfer = leftToTransfer - countToTransfer;
        return (countToTransfer, remainingToTransfer, remainingToTransfer <= 0);
    }
}
