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

        var map = tradeShip.Map;

        var notAlreadyCounted = ToTraderTransportShips
            .SelectMany(ship =>
                ship.TransporterComp.innerContainer.Concat(
                    Utils.ThingsCarriedTowardTransporter(ship.TransporterComp, map)
                )
            )
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

    // Snapshots of sold-to-trader things/pawns lost before delivery (see
    // Notify_OwnThingDestroyed), kept purely for display - CompTradeShuttle's inspect string and
    // the shuttle's contents tab use this to tell the player what's actually missing instead of
    // just that "something" is. Cleared once a renegotiation rebalances the deal (see
    // ApplyRenegotiation). Left empty (rather than populated with a generic placeholder) when a
    // loss is only discovered on load, since PruneDestroyedSoldItems no longer has the destroyed
    // thing/pawn's reference to describe it from.
    public List<LostTradeItemRecord> lostSoldItems = [];

    /// <summary>
    /// The shuttle every sold pawn boards, and the one whose quest parts drive the trade's
    /// timer - the only to-trader shuttle in trades made before a sale could span several.
    /// </summary>
    public TransportShip? toTraderTransportShip;

    /// <summary>
    /// The shuttles beyond <see cref="toTraderTransportShip"/> a sale too heavy for one shuttle is
    /// divided across; empty for a single-shuttle trade (including every trade saved before
    /// multi-shuttle trades existed).
    /// </summary>
    public List<TransportShip> additionalToTraderTransportShips = [];

    /// <summary>
    /// Latched once every to-trader shuttle has been loaded to satisfaction - only then are they
    /// allowed to take off, all together. Unused for a single-shuttle trade.
    /// </summary>
    public bool toTraderDepartureCleared;

    /// <summary>
    /// Quest signal sent when <see cref="toTraderDepartureCleared"/> latches; null for a
    /// single-shuttle trade.
    /// </summary>
    public string? toTraderShuttlesLoadedSignal;

    /// <summary>
    /// Every to-trader shuttle, <see cref="toTraderTransportShip"/> first.
    /// </summary>
    internal IEnumerable<TransportShip> ToTraderTransportShips
    {
        get
        {
            if (toTraderTransportShip == null)
            {
                yield break;
            }
            yield return toTraderTransportShip;
            foreach (var ship in additionalToTraderTransportShips)
            {
                yield return ship;
            }
        }
    }

    private IEnumerable<CompTradeShuttle> ToTraderTradeShuttleComps =>
        ToTraderTransportShips
            .Select(ship => ship.shipThing.TryGetComp<CompTradeShuttle>())
            .Where(comp => comp != null);

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
            ToTraderTransportShips.SelectMany(ship => ship.TransporterComp.innerContainer)
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
        Scribe_Collections.Look(ref lostSoldItems, "lostSoldItems", LookMode.Deep);
        lostSoldItems ??= [];
        Scribe_References.Look(ref toTraderTransportShip, "toTraderTransportShip");
        Scribe_Collections.Look(
            ref additionalToTraderTransportShips,
            "additionalToTraderTransportShips",
            LookMode.Reference
        );
        additionalToTraderTransportShips ??= [];
        Scribe_Values.Look(ref toTraderDepartureCleared, "toTraderDepartureCleared", false);
        Scribe_Values.Look(ref toTraderShuttlesLoadedSignal, "toTraderShuttlesLoadedSignal");

        Scribe_Deep.Look(ref tradeDeliveryMethod, "tradeDeliveryMethod");

#pragma warning disable CS0612 // We still load it for backwards compatibility
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            _ = additionalToTraderTransportShips.RemoveAll(ship => ship == null);
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
                        RealisticOrbitalTradeMod.Instance.LogError(
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
    internal void PruneDestroyedSoldItems()
    {
        var lostAnything = RemoveUnresolvedSoldEntries();

        if (toTraderTransportShip is { } transportShip)
        {
            // A single shuttle has its requiredPawns aliased directly to pawnsSoldToTrader at deal
            // creation, so live pawn removals (Notify_OwnThingDestroyed) stay in sync
            // automatically. Scribe reconstructs each list independently on load, breaking that
            // aliasing - re-establish it. With several shuttles each keeps its own list of pawns.
            if (additionalToTraderTransportShips.Count == 0)
            {
                transportShip.ShuttleComp.requiredPawns = pawnsSoldToTrader;
            }

            if (lostAnything)
            {
                Utils.RebuildRequiredCargo(this);
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
        SendSoldItemsLostOnLoadLetter();
    }

    /// <summary>
    /// Drops sold things, sold pawns and reservations whose reference no longer resolves. Returns
    /// whether a sold thing or pawn was among them.
    /// </summary>
    internal bool RemoveUnresolvedSoldEntries()
    {
        var lostAnything = thingsSoldToTrader.RemoveAll(t => t.thing == null) > 0;
        lostAnything |= pawnsSoldToTrader.RemoveAll(p => p == null) > 0;
        _ = reservedTraderThings.RemoveAll(t => t.thing == null);
        return lostAnything;
    }

    public static void Notify_ThingDestroyed(Thing thing)
    {
        // Thing.Destroy fires constantly outside actual gameplay too (def loading, main-menu
        // scratch objects, the test runner's main-menu fixtures run without a real save loaded).
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

    internal void Notify_OwnThingMerged(Thing absorbedThing, Thing survivingThing)
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
            survivingReservation.Count = Math.Min(
                survivingThing.stackCount,
                survivingReservation.Count + absorbedReservation.Count
            );
        }
        else
        {
            absorbedReservation.thing = survivingThing;
            reservedTraderThings.Add(absorbedReservation);
        }
    }

    internal void Notify_OwnThingDestroyed(Thing thing)
    {
        if (toTraderTransportShip == null)
        {
            return;
        }

        bool removed;
        var actualShortfall = false;
        LostTradeItemRecord? lostItem = null;
        if (thing is Pawn pawn)
        {
            removed = pawnsSoldToTrader.Remove(pawn);
            if (removed)
            {
                foreach (var ship in ToTraderTransportShips)
                {
                    _ = ship.ShuttleComp.requiredPawns.Remove(pawn);
                }
                CancelHaulingJobFor(pawn);
                actualShortfall = true;
                lostItem = LostTradeItemRecord.ForPawn(pawn);
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
                CancelHaulingJobFor(thing);

                if (TrySubstituteFor(thing, thingCount!.Count))
                {
                    // Required cargo is left untouched - the substitute's count was already
                    // covered by it, see TrySubstituteFor - so autoload just needs re-running to
                    // pick up the newly-recorded stack(s) instead of the destroyed one.
                    CheckAutoloadOnAllShuttles();
                    return;
                }

                // Hauling treats same-def stacks as fungible (see ReconcileReservation), so this
                // exact stack's reservation can already have been released even though it was
                // never touched - the deal's need for it having been covered by other stock hauled
                // instead. In that case the shuttle's required cargo no longer has anything left
                // to reduce for this def, and destroying this now-uncommitted leftover has no
                // bearing on the deal.
                actualShortfall = ReduceRequiredCargoFor(thing, thingCount!.Count);
                if (actualShortfall)
                {
                    // thing.stackCount is already zeroed by the time this postfix runs (see
                    // Thing.Destroy), so the lost amount has to come from the sale record instead.
                    lostItem = LostTradeItemRecord.ForThing(thing, thingCount.Count);
                }
            }
        }

        if (!removed || !actualShortfall)
        {
            return;
        }

        LostTradeItemRecord.AddOrMerge(lostSoldItems, lostItem!);
        RenegotiationRequired = true;
        CheckAutoloadOnAllShuttles();
        SendSoldItemLostLetter(lostItem!.Label);
    }

    // Fungible required cargo (plain stackables, or quality/health-tracked items matched by
    // ThingDefCountWithRequirements.Matches's def+stuff+quality/hitpoints bucket) doesn't need the
    // specific Thing that was sold - only enough of a matching stack. If the colony still holds
    // enough spare (not already sold-and-reserved through this trade) stock to cover the
    // requirement without thing, losing it isn't a real shortfall - AllocateSubstitutes picks
    // replacement stack(s) to record as sold in thing's place. A requiredThing-pinned entry
    // (genepacks/xenogerms) only ever matches that exact, now-destroyed reference, so this
    // naturally reports those as unsubstitutable without special-casing them.
    //
    // Candidates are capped to each stack's spare capacity (stackCount minus whatever's already
    // reserved against that same reference via ReservedCountFor) so a substitute can never
    // double-claim stock this trade already committed elsewhere. Stock already physically split
    // off and in transit toward the shuttle (Utils.ThingsCarriedTowardTransporter) is excluded
    // entirely rather than capped: it's always a fragment of an already-recorded sale, and
    // reservedTraderThings never gets updated to point at that split-off reference (see
    // ReconcileReservation), so there'd be no way to tell how much of it is actually spare.
    private bool TrySubstituteFor(Thing thing, int lostCount)
    {
        var sendableItems = ColonyThingsTraderWillingToBuy()
            .Select(candidate =>
                (candidate, spareCount: candidate.stackCount - ReservedCountFor(candidate))
            );

        var allocation = AllocateSubstitutes(
            thing,
            lostCount,
            [.. ToTraderTradeShuttleComps.SelectMany(comp => comp.requiredSpecificItems)],
            [.. ToTraderTransportShips.SelectMany(ship => ship.ShuttleComp.requiredItems)],
            sendableItems
        );
        if (allocation == null)
        {
            return false;
        }

        foreach (var (candidate, count) in allocation)
        {
            var existing = thingsSoldToTrader.FirstOrDefault(t => t.thing == candidate);
            if (existing != null)
            {
                existing.Count += count;
            }
            else
            {
                thingsSoldToTrader.Add(new(candidate, count));
            }
            AddReservation(candidate, count);
        }
        return true;
    }

    /// <summary>
    /// Picks replacement stack(s) from <paramref name="sendableItems"/> to cover
    /// <paramref name="lostCount"/> units of <paramref name="thing"/>'s fungibility bucket (see
    /// <see cref="TrySubstituteFor"/>), greedily taking from each candidate's spare (unreserved)
    /// count - skipping <paramref name="thing"/> itself, whose stackCount is already zeroed by the
    /// time this runs - until covered. Returns <see langword="null"/> if no matching required-cargo
    /// entry exists or the matching spare stock isn't enough to cover the loss.
    /// </summary>
    internal static List<(Thing candidate, int count)>? AllocateSubstitutes(
        Thing thing,
        int lostCount,
        List<ThingDefCountWithRequirements> requiredSpecificItems,
        List<ThingDefCount> requiredItems,
        IEnumerable<(Thing candidate, int spareCount)> sendableItems
    )
    {
        Func<Thing, bool> matches;
        if (thing.HasRequirements())
        {
            var requiredItem = requiredSpecificItems.FirstOrDefault(t => t.Matches(thing));
            if (requiredItem == null)
            {
                return null;
            }
            matches = requiredItem.Matches;
        }
        else
        {
            var requiredGeneric = requiredItems.FirstOrDefault(t => t.ThingDef == thing.def);
            if (requiredGeneric == null)
            {
                return null;
            }
            matches = candidate => candidate.def == thing.def;
        }

        List<(Thing candidate, int count)> allocation = [];
        var remaining = lostCount;
        foreach (var (candidate, spareCount) in sendableItems)
        {
            if (remaining <= 0)
            {
                break;
            }
            if (candidate == thing || spareCount <= 0 || !matches(candidate))
            {
                continue;
            }

            var take = Math.Min(spareCount, remaining);
            allocation.Add((candidate, take));
            remaining -= take;
        }

        return remaining <= 0 ? allocation : null;
    }

    private bool ReduceRequiredCargoFor(Thing thing, int count)
    {
        var leftToTakeOff = count;
        var reduced = ReduceRequiredCargo(thing, ref leftToTakeOff);
        if (!reduced)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                $"Destroyed {thing} was tracked as sold to the trader, but no matching required-cargo entry was found to reduce. The deal's need for it was likely already covered by other stock, so this isn't treated as an actual loss."
            );
        }
        return reduced;
    }

    /// <summary>
    /// Takes up to <paramref name="leftToTakeOff"/> units of <paramref name="thing"/>'s
    /// requirement off the shuttles' required-cargo lists, starting with whichever shuttle has
    /// the least of it loaded already so loaded cargo is disturbed last. Returns whether any
    /// shuttle had a matching entry.
    /// </summary>
    internal bool ReduceRequiredCargo(Thing thing, ref int leftToTakeOff)
    {
        var ships = ToTraderTransportShips.OrderBy(ship => LoadedCountOf(ship, thing)).ToList();
        if (!thing.HasRequirements())
        {
            return ReduceRequiredItemsAcross(
                ships.Select(ship => ship.ShuttleComp.requiredItems),
                thing.def,
                ref leftToTakeOff
            );
        }

        var reduced = false;
        foreach (var ship in ships)
        {
            if (leftToTakeOff <= 0)
            {
                break;
            }
            reduced |= ThingDefCountWithRequirements.TryReduceRequiredCount(
                ship.shipThing.TryGetComp<CompTradeShuttle>().requiredSpecificItems,
                thing,
                ref leftToTakeOff
            );
        }
        return reduced;
    }

    /// <summary>
    /// Takes up to <paramref name="leftToTakeOff"/> units of <paramref name="def"/> off each
    /// shuttle's required items in turn, moving on once one is exhausted. Returns whether any
    /// shuttle had a matching entry.
    /// </summary>
    internal static bool ReduceRequiredItemsAcross(
        IEnumerable<List<ThingDefCount>> shuttleRequiredItems,
        ThingDef def,
        ref int leftToTakeOff
    )
    {
        var reduced = false;
        foreach (var requiredItems in shuttleRequiredItems)
        {
            if (leftToTakeOff <= 0)
            {
                break;
            }
            reduced |= Utils.TryReduceRequiredItemsCount(requiredItems, def, ref leftToTakeOff);
        }
        return reduced;
    }

    internal static int LoadedCountOf(TransportShip ship, Thing thing)
    {
        var def = thing.GetInnerIfMinified().def;
        var count = 0;
        foreach (var loaded in ship.TransporterComp.innerContainer)
        {
            if (loaded.GetInnerIfMinified().def == def)
            {
                count += loaded.stackCount;
            }
        }
        return count;
    }

    internal void CheckAutoloadOnAllShuttles()
    {
        foreach (var comp in ToTraderTradeShuttleComps)
        {
            comp.CheckAutoload();
        }
    }

    private void CancelHaulingJobFor(Thing thing)
    {
        foreach (var ship in ToTraderTransportShips)
        {
            var map = ship.shipThing.Map;
            if (map == null)
            {
                continue;
            }

            Utils.EndHaulJobsFor(ship.TransporterComp, map, hauled => hauled == thing);
        }
    }

    private void SendSoldItemLostLetter(string itemDescription)
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(
            "Sending sold item lost letter\n" + StackTraceUtility.ExtractStackTrace()
        );
        Find.LetterStack.ReceiveLetter(
            "RealisticOrbitalTrade.SoldItemLostLetterLabel".Translate(),
            "RealisticOrbitalTrade.SoldItemLostLetterText".Translate(
                itemDescription,
                tradeShip.TraderName
            ),
            LetterDefOf.NegativeEvent,
            toTraderTransportShip?.shipThing
        );
    }

    // Used instead of SendSoldItemLostLetter when a loss is only discovered on load
    // (PruneDestroyedSoldItems): the destroyed thing/pawn's reference is already gone by then, so
    // there's nothing to describe it with.
    private void SendSoldItemsLostOnLoadLetter()
    {
        RealisticOrbitalTradeMod.Instance.LogDevMessage(
            "Sending sold item(s) lost (on load) letter\n" + StackTraceUtility.ExtractStackTrace()
        );
        Find.LetterStack.ReceiveLetter(
            "RealisticOrbitalTrade.SoldItemLostLetterLabel".Translate(),
            "RealisticOrbitalTrade.SoldItemsLostOnLoadLetterText".Translate(tradeShip.TraderName),
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

        // Hauling pawns take parts of the stack, so what's left of it can be less than what's
        // still tracked.
        existing.Count = Math.Min(existing.Count - reduced, thing.stackCount);
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
    /// Releases reservations on the things <paramref name="matches"/> selects until no more than
    /// <paramref name="stillNeeded"/> units remain reserved across them.
    /// </summary>
    private void ReleaseReservationsBeyond(Func<Thing, bool> matches, int stillNeeded)
    {
        var excess =
            reservedTraderThings.Where(t => matches(t.thing)).Sum(t => t.Count) - stillNeeded;
        foreach (var entry in reservedTraderThings.Where(t => matches(t.thing)).ToList())
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
        var excess =
            entries.Sum(t => t.Count)
            - Math.Max(0, stillNeeded)
            - StillNeededByOtherShuttles(transferable);
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

    // Reservations cover the whole deal, but a hauling pawn only knows the need of the shuttle it's
    // loading, so what the deal's other shuttles still need has to be held back from release too.
    private int StillNeededByOtherShuttles(TransferableOneWay transferable)
    {
        if (additionalToTraderTransportShips.Count == 0 || transferable.AnyThing == null)
        {
            return 0;
        }

        var needed = 0;
        bool isOwnShuttle(TransportShip ship)
        {
            return ship.TransporterComp.leftToLoad?.Contains(transferable) == true;
        }
        foreach (var ship in ToTraderTransportShips.Where(s => !isOwnShuttle(s)))
        {
            var leftToLoad = ship.TransporterComp.leftToLoad;
            if (leftToLoad == null)
            {
                continue;
            }
            var match = JobDriver_HaulMultipleToTransporter.FindCommittedTransferable(
                transferable.AnyThing,
                leftToLoad
            );
            needed += Math.Max(0, match?.CountToTransfer ?? 0);
        }
        return needed;
    }

    internal const int PawnLineIdBase = 1_000_000;

    /// <summary>The mass a sold pawn takes up on a shuttle, carried inventory included.</summary>
    internal static float PawnCargoMass(Thing pawn) =>
        CollectionsMassCalculator.MassUsage(
            [new ThingCount(pawn, 1)],
            IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
            includePawnsMass: true
        );

    /// <summary>
    /// Divides what's sold to the trader (see <see cref="ShuttleLoadPlanner.Plan"/>) across
    /// shuttles of <paramref name="capacity"/> kg each. A planned line's id is its index in
    /// <see cref="thingsSoldToTrader"/>, or <see cref="PawnLineIdBase"/> plus its index in
    /// <see cref="pawnsSoldToTrader"/> for a sold pawn.
    /// <see langword="null"/> if something sold can't fit on a shuttle at all.
    /// </summary>
    internal List<List<(int lineId, int count)>>? PlanShuttleLoads(float capacity) =>
        ShuttleLoadPlanner.Plan(
            capacity,
            0f,
            BuildLines(
                [
                    .. thingsSoldToTrader.Select(t =>
                        (
                            unitMass: IsSoldThingLost(t)
                                ? 0f
                                : t.thing.GetStatValue(StatDefOf.Mass),
                            count: t.Count,
                            lost: IsSoldThingLost(t)
                        )
                    ),
                ],
                [
                    .. pawnsSoldToTrader.Select(p =>
                        (mass: IsSoldPawnLost(p) ? 0f : PawnCargoMass(p), lost: IsSoldPawnLost(p))
                    ),
                ]
            )
        );

    /// <summary>
    /// The planner's lines for what's sold: each surviving thing under its index, each surviving
    /// pawn under <see cref="PawnLineIdBase"/> plus its index.
    /// </summary>
    internal static List<ShuttleLoadPlanner.Line> BuildLines(
        IReadOnlyList<(float unitMass, int count, bool lost)> things,
        IReadOnlyList<(float mass, bool lost)> pawns
    )
    {
        List<ShuttleLoadPlanner.Line> lines = [];
        for (var i = 0; i < things.Count; i++)
        {
            if (!things[i].lost)
            {
                lines.Add(new(i, things[i].unitMass, things[i].count));
            }
        }

        for (var i = 0; i < pawns.Count; i++)
        {
            if (!pawns[i].lost)
            {
                lines.Add(new(PawnLineIdBase + i, pawns[i].mass, 1));
            }
        }

        return lines;
    }

    /// <summary>
    /// <see cref="PlanShuttleLoads"/>, falling back to everything on a single shuttle when the
    /// sale can't be divided (nothing legitimately reaches that, since the trade dialog refuses
    /// a sale that can't be) so the trade still gets its requirements.
    /// </summary>
    internal List<List<(int lineId, int count)>> PlanShuttleLoadsOrSingleShuttle(float capacity) =>
        PlanShuttleLoads(capacity)
        ??
        [
            [
                .. thingsSoldToTrader
                    .Select((t, i) => (i, t.Count))
                    .Where(l => !IsSoldThingLost(thingsSoldToTrader[l.i])),
            ],
        ];

    /// <summary>
    /// Writes each planned shuttle's share into the shuttles' required-cargo lists, in the order
    /// <see cref="ToTraderTransportShips"/> yields them. Shares beyond the last shuttle land on it.
    /// </summary>
    internal void AssignRequiredCargo(List<List<(int lineId, int count)>> plan)
    {
        var ships = ToTraderTransportShips.ToList();
        var divideAmongShuttles = ships.Count > 1;
        if (divideAmongShuttles)
        {
            // With one shuttle its requiredPawns is aliased to pawnsSoldToTrader instead.
            foreach (var ship in ships)
            {
                ship.ShuttleComp.requiredPawns = [];
            }
        }

        for (var i = 0; i < plan.Count; i++)
        {
            var ship = ships[Math.Min(i, ships.Count - 1)];
            var compTradeShuttle = ship.shipThing.TryGetComp<CompTradeShuttle>();
            foreach (var (lineId, count) in plan[i])
            {
                if (lineId >= PawnLineIdBase)
                {
                    if (divideAmongShuttles)
                    {
                        ship.ShuttleComp.requiredPawns.Add(
                            pawnsSoldToTrader[lineId - PawnLineIdBase]
                        );
                    }
                    continue;
                }

                Utils.AddThingToLoadToShuttle(
                    new ThingCountClass(thingsSoldToTrader[lineId].thing, count),
                    compTradeShuttle,
                    ship.ShuttleComp
                );
            }
        }
    }

    // Renegotiation can add to the sale after the shuttles are already assigned their cargo. The
    // shuttles can't be added to at that point, so the addition fills whatever room the existing
    // ones have left, spilling whatever doesn't fit onto the last shuttle.
    private void AddRequiredCargoToShuttles(ThingCountClass thingCount)
    {
        var ships = ToTraderTransportShips.ToList();
        var shares = DistributeByFreeMass(
            [.. ships.Select(FreeMass)],
            thingCount.thing.GetStatValue(StatDefOf.Mass),
            thingCount.Count
        );
        for (var i = 0; i < ships.Count; i++)
        {
            if (shares[i] <= 0)
            {
                continue;
            }

            Utils.AddThingToLoadToShuttle(
                new ThingCountClass(thingCount.thing, shares[i]),
                ships[i].shipThing.TryGetComp<CompTradeShuttle>(),
                ships[i].ShuttleComp
            );
        }
    }

    /// <summary>
    /// Splits <paramref name="count"/> units of <paramref name="unitMass"/> kg across shuttles with
    /// the given free mass: each takes what fits, and the last takes everything left. A
    /// weightless unit all goes on the first shuttle.
    /// </summary>
    internal static List<int> DistributeByFreeMass(
        IReadOnlyList<float> freeMass,
        float unitMass,
        int count
    )
    {
        List<int> shares = [.. freeMass.Select(_ => 0)];
        var remaining = count;
        for (var i = 0; i < freeMass.Count && remaining > 0; i++)
        {
            var take =
                i == freeMass.Count - 1 || unitMass <= 0f
                    ? remaining
                    : Math.Min(
                        remaining,
                        (int)Math.Floor((Math.Max(0f, freeMass[i]) + 0.0001f) / unitMass)
                    );
            shares[i] = take;
            remaining -= take;
        }
        return shares;
    }

    internal float FreeMass(TransportShip ship)
    {
        var compTradeShuttle = ship.shipThing.TryGetComp<CompTradeShuttle>();
        var required =
            ship.ShuttleComp.requiredItems.Sum(i =>
                i.Count * i.ThingDef.GetStatValueAbstract(StatDefOf.Mass)
            )
            + compTradeShuttle.requiredSpecificItems.Sum(i =>
                i.count * i.def.GetStatValueAbstract(StatDefOf.Mass)
            );
        required += ship.ShuttleComp.requiredPawns.Sum(PawnCargoMass);
        return Patch.Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity - required;
    }

    private static int RequiredCountOf(TransportShip ship, Thing thing) =>
        thing.HasRequirements()
            ? ship
                .shipThing.TryGetComp<CompTradeShuttle>()
                .requiredSpecificItems.Where(r => r.def == thing.GetInnerIfMinified().def)
                .Sum(r => r.count)
            : ship.ShuttleComp.requiredItems.Where(r => r.ThingDef == thing.def).Sum(r => r.Count);

    // Set while IsToTraderDepartureCleared asks each shuttle whether it's loaded, since that ask
    // routes back through the shuttle's own departure check.
    private bool checkingDepartureClearance;

    /// <summary>
    /// Whether the to-trader shuttles may take off: always true for a single shuttle, otherwise
    /// only once every shuttle is loaded to satisfaction - which then latches, so shuttles that
    /// have already left don't hold back the ones still on the ground.
    /// </summary>
    internal bool IsToTraderDepartureCleared()
    {
        if (
            toTraderDepartureCleared
            || additionalToTraderTransportShips.Count == 0
            || checkingDepartureClearance
        )
        {
            return true;
        }

        checkingDepartureClearance = true;
        try
        {
            foreach (var ship in ToTraderTransportShips)
            {
                if (
                    ship.shipThing is not { Spawned: true }
                    || !ship.ShuttleComp.AllRequiredThingsLoaded
                )
                {
                    return false;
                }
            }
        }
        finally
        {
            checkingDepartureClearance = false;
        }

        toTraderDepartureCleared = true;
        if (toTraderShuttlesLoadedSignal != null)
        {
            Find.SignalManager.SendSignal(new Signal(toTraderShuttlesLoadedSignal));
        }
        return true;
    }

    /// <summary>
    /// <paramref name="amount"/>, limited to what <paramref name="things"/> actually hold in total.
    /// </summary>
    internal static int ClampToAvailable(int amount, IEnumerable<Thing> things) =>
        Math.Min(amount, things.Sum(t => t.stackCount));

    internal enum TransferDirection
    {
        ToPlayer,
        ToTrader,
    }

    /// <summary>
    /// Whether <paramref name="newTradeables"/> (a renegotiation's edited deal) amounts to
    /// reducing everything to zero - offered to the player as "did you mean to cancel the trade?"
    /// rather than silently applying a no-op-looking deal. Silver only balances the deal, so it doesn't
    /// count.
    /// </summary>
    internal static bool IsFullCancellation(List<Tradeable> newTradeables) =>
        newTradeables.Where(t => !t.IsCurrency).Sum(t => Math.Abs(t.CountToTransfer)) == 0;

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
        var tradeShipThings = tradeShip.things;
        var toPlayerThingOwner = tradeDeliveryMethod!.Things;

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
                        // Move things out of the trade ship and to the items meant for the player.
                        // The trader may hold less than the deal owes (e.g. silver, when the
                        // player accepted a trade the trader can't fully pay for), in which case
                        // the player gets whatever there is.
                        TransferableUtility.TransferNoSplit(
                            tradeable.thingsTrader,
                            ClampToAvailable(amount, tradeable.thingsTrader),
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
                        SellUnsoldStock(
                            tradeable,
                            amount,
                            (toGive, countToGive) =>
                            {
                                // A pawn sold to the trader during a fresh trade walks itself onto
                                // a shuttle via requiredPawns - it isn't hauled/reserved cargo
                                // like an ordinary item, see
                                // Rimworld_TradeShip_GiveSoldThingToTrader's prefix. Routing it
                                // through the ordinary-item path below instead would try to have it
                                // hauled and reserved like a crate of steel.
                                if (toGive is Pawn soldPawn)
                                {
                                    // A single shuttle's requiredPawns is this same list
                                    // (QuestNode_Root_TradeShipTransportShip.RunInt), so appending
                                    // is picked up automatically; several shuttles each keep their
                                    // own, so the pawn is assigned to whichever has the most room.
                                    pawnsSoldToTrader.Add(soldPawn);
                                    if (additionalToTraderTransportShips.Count > 0)
                                    {
                                        ToTraderTransportShips
                                            .OrderByDescending(FreeMass)
                                            .First()
                                            .ShuttleComp.requiredPawns.Add(soldPawn);
                                    }
                                    return;
                                }
                                var alreadySold = thingsSoldToTrader.FirstOrDefault(t =>
                                    t.thing == toGive
                                );
                                if (alreadySold != null)
                                {
                                    alreadySold.Count += countToGive;
                                }
                                else
                                {
                                    thingsSoldToTrader.Add(new(toGive, countToGive));
                                }
                                AddReservation(toGive, countToGive);
                                AddRequiredCargoToShuttles(new(toGive, countToGive));
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
                            foreach (var ship in ToTraderTransportShips)
                            {
                                // Remove from shuttle's required cargo list
                                _ = ship.ShuttleComp.requiredPawns.Remove(pawn);

                                // If already loaded onto the shuttle, make the shuttle drop
                                // the pawn back out.
                                var shipContainer = ship.TransporterComp.innerContainer;
                                if (shipContainer.Contains(pawn))
                                {
                                    var unloaded = shipContainer.TryDrop(
                                        pawn,
                                        ship.GetDropLocation(),
                                        ship.shipThing.Map,
                                        ThingPlaceMode.Near,
                                        out _,
                                        null,
                                        c =>
                                        {
                                            return !c.Fogged(ship.shipThing.Map) && !pawn.Downed;
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
                                Utils.EndHaulJobsFor(
                                    ship.TransporterComp,
                                    ship.shipThing.Map,
                                    hauled => hauled == pawn
                                );
                            }
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
                                    thing =>
                                        ToTraderTransportShips.Any(ship =>
                                            ship.TransporterComp.innerContainer.Contains(thing)
                                        )
                                );
                                if (thingCount == null)
                                {
                                    break;
                                }
                                if (thingCount.Count > leftToTakeOff)
                                {
                                    // Hauling pawns take parts of the stack, so what's left of it can
                                    // be less than what's still sold from it.
                                    thingCount.Count = Math.Min(
                                        thingCount.Count - leftToTakeOff,
                                        thingCount.thing.stackCount
                                    );
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

                            // Take things off the to-be-loaded list. First, remove things from
                            // there - if we can get away with just reducing that, we won't need to
                            // unload anything.
                            leftToTakeOff = -amount;
                            if (
                                !ReduceRequiredCargo(
                                    tradeable.AnyThing.HasRequirements()
                                        ? tradeable.AnyThing.GetInnerIfMinified()
                                        : tradeable.AnyThing,
                                    ref leftToTakeOff
                                )
                            )
                            {
                                RealisticOrbitalTradeMod.Instance.LogError(
                                    $"Needed to take {-amount} off the 'requiredItems' list, but could only remove {-amount - leftToTakeOff}"
                                );
                            }

                            // Calculate how much has been loaded already
                            var hasRequirements = tradeable.AnyThing.HasRequirements();
                            bool IsThisTradeable(Thing t)
                            {
                                return hasRequirements
                                    ? t.GetInnerIfMinified() == tradeable.AnyThing
                                    : t.GetInnerIfMinified().def == tradeable.ThingDef;
                            }
                            var loadedByShip = ToTraderTransportShips
                                .Where(ship => ship.shipThing.Map != null)
                                .Select(ship =>
                                    (
                                        ship,
                                        things: ship.TransporterComp.innerContainer.Where(
                                                IsThisTradeable
                                            )
                                            .ToList()
                                    )
                                )
                                .ToList();
                            var countActuallyLoaded = loadedByShip.Sum(l =>
                                l.things.Sum(t => t.stackCount)
                            );

                            var countRequiredLoaded = tradeable.CountToTransferToDestination;
                            if (countActuallyLoaded > countRequiredLoaded)
                            {
                                // More items loaded than we've sold now; unload some, starting
                                // with the shuttle holding the most beyond what it still requires
                                var leftToUnload = countActuallyLoaded - countRequiredLoaded;

                                foreach (
                                    var (ship, loadedThings) in loadedByShip.OrderByDescending(l =>
                                        l.things.Sum(t => t.stackCount)
                                        - RequiredCountOf(l.ship, tradeable.AnyThing)
                                    )
                                )
                                {
                                    var shipContainer = ship.TransporterComp.innerContainer;
                                    var shipMap = ship.shipThing.Map;
                                    var dropLoc = ship.GetDropLocation();
                                    foreach (var loaded in loadedThings)
                                    {
                                        var unloadAmount = Math.Min(
                                            leftToUnload,
                                            loaded.stackCount
                                        );
                                        var unloaded = shipContainer.TryDrop(
                                            loaded,
                                            dropLoc,
                                            shipMap,
                                            ThingPlaceMode.Near,
                                            unloadAmount,
                                            out _,
                                            null,
                                            c =>
                                            {
                                                return !c.Fogged(shipMap)
                                                    && c.GetFirstPawn(shipMap) == null;
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
                                    if (leftToUnload == 0)
                                    {
                                        break;
                                    }
                                }
                            }

                            // Also cancel any hauling jobs featuring the item
                            foreach (var ship in ToTraderTransportShips)
                            {
                                var shipMap = ship.shipThing.Map;
                                if (shipMap == null)
                                {
                                    continue;
                                }
                                Utils.EndHaulJobsFor(
                                    ship.TransporterComp,
                                    shipMap,
                                    IsThisTradeable
                                );
                            }

                            // Hauling treats same-def stacks as fungible, so the reservations left
                            // may sit on different stacks than the ones just taken off the sale.
                            ReleaseReservationsBeyond(
                                t => IsThisTradeable(t),
                                Math.Max(0, countRequiredLoaded - countActuallyLoaded)
                            );
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
        lostSoldItems.Clear();

        // Immediately re-evaluate the auto-load list on the shuttles
        foreach (var ship in ToTraderTransportShips)
        {
            ship.ShuttleComp.CheckAutoload();
        }

        return true;
    }

    // Hands <paramref name="sell"/> up to <paramref name="amount"/> of the tradeable's colony stock
    // that isn't already part of this deal, stack by stack.
    private void SellUnsoldStock(Tradeable tradeable, int amount, Action<Thing, int> sell)
    {
        var (allocation, shortfall) = AllocateUnsoldStock(
            tradeable.thingsColony.Select(thing => (thing, UnsoldCount(thing))),
            amount
        );
        foreach (var (thing, count) in allocation)
        {
            sell(thing, count);
        }

        if (shortfall > 0)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Needed to sell {amount} {tradeable.LabelCap} in the renegotiation, but only {amount - shortfall} was available."
            );
        }
    }

    /// <summary>
    /// Takes <paramref name="amount"/> from the stacks' unsold counts, stack by stack, and reports
    /// how much of it no stack could supply.
    /// </summary>
    internal static (List<(Thing thing, int count)> allocation, int shortfall) AllocateUnsoldStock(
        IEnumerable<(Thing thing, int unsold)> stock,
        int amount
    )
    {
        List<(Thing thing, int count)> allocation = [];
        var left = amount;
        foreach (var (thing, unsold) in stock)
        {
            if (left <= 0)
            {
                break;
            }

            var count = Math.Min(left, unsold);
            if (count > 0)
            {
                allocation.Add((thing, count));
                left -= count;
            }
        }
        return (allocation, left);
    }

    internal int UnsoldCount(Thing thing) =>
        thing is Pawn pawn
            ? (pawnsSoldToTrader.Contains(pawn) ? 0 : 1)
            : Math.Max(
                0,
                thing.stackCount - thingsSoldToTrader.Where(t => t.thing == thing).Sum(t => t.Count)
            );

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

    /// <summary>
    /// A savegame-safe snapshot of a single sold-to-trader thing/pawn lost before delivery,
    /// captured at the moment of loss (see <see cref="Notify_OwnThingDestroyed"/>). Kept purely
    /// for display in <see cref="CompTradeShuttle.CompInspectStringExtra"/> and the
    /// shuttle's contents tab - the real <see cref="Thing"/>/<see cref="Pawn"/> reference can't be
    /// used for that once destroyed (its stackCount is already zeroed by the time this runs, see
    /// <see cref="Notify_OwnThingDestroyed"/>'s remark, and a Scribe reference to it resolves to
    /// null on reload).
    /// </summary>
    internal class LostTradeItemRecord : IExposable
    {
        public ThingDef? def;
        public ThingDef? stuffDef;
        public bool hasQuality;
        public QualityCategory quality;
        public string? pawnLabel;
        public int count = 1;

        public string Label =>
            pawnLabel
            ?? (
                GenLabel.ThingLabel(def, stuffDef, count)
                + (hasQuality ? $" ({quality.GetLabel()})" : string.Empty)
            );

        public void ExposeData()
        {
            Scribe_Defs.Look(ref def, "def");
            Scribe_Defs.Look(ref stuffDef, "stuffDef");
            Scribe_Values.Look(ref hasQuality, "hasQuality");
            Scribe_Values.Look(ref quality, "quality");
            Scribe_Values.Look(ref pawnLabel, "pawnLabel");
            Scribe_Values.Look(ref count, "count", 1);
        }

        internal static LostTradeItemRecord ForThing(Thing thing, int count)
        {
            var hasQuality = QualityUtility.TryGetQuality(thing, out var quality);
            return new()
            {
                def = thing.def,
                stuffDef = thing.Stuff,
                hasQuality = hasQuality,
                quality = quality,
                count = count,
            };
        }

        internal static LostTradeItemRecord ForPawn(Pawn pawn) =>
            new() { pawnLabel = pawn.LabelShortCap };

        /// <summary>
        /// Adds <paramref name="record"/> to <paramref name="records"/>, merging its count into a
        /// matching existing entry (same def/stuff/quality bucket) if one is already present.
        /// Pawn records (<see cref="pawnLabel"/> set) are never merged into each other - two
        /// different pawns can share a label, and folding them into one counted entry would
        /// misrepresent them as a single stack.
        /// </summary>
        internal static void AddOrMerge(
            List<LostTradeItemRecord> records,
            LostTradeItemRecord record
        )
        {
            if (record.pawnLabel == null)
            {
                var existing = records.FirstOrDefault(r =>
                    r.pawnLabel == null
                    && r.def == record.def
                    && r.stuffDef == record.stuffDef
                    && r.hasQuality == record.hasQuality
                    && r.quality == record.quality
                );
                if (existing != null)
                {
                    existing.count += record.count;
                    return;
                }
            }
            records.Add(record);
        }
    }
}
