using RealisticOrbitalTrade.Comps;
using RealisticOrbitalTrade.Core;
using RimWorld.Planet;
using Verse.AI;

namespace RealisticOrbitalTrade;

[HotSwappable]
internal class TradeAgreement : IExposable, ILoadReferenceable, IThingHolder
{
    private class Tradeable_Capped(int cap) : Tradeable
    {
        public int cap = cap;

        public override int CountHeldBy(Transactor trans) => trans == Transactor.Colony ? cap : 0;
    }

    protected int loadID = -1;

    public TradeShip tradeShip;
    public Pawn negotiator;

    public ThingOwner<Thing> thingsSoldToPlayer;
    public List<ThingCountClass> thingsSoldToTrader = [];
    public List<Pawn> pawnsSoldToTrader = [];

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
            foreach (var item in tradeDeliveryMethod.Things)
            {
                var tradeable = TransferableUtility.TradeableMatching(item, tradeables);
                if (tradeable == null)
                {
                    tradeable = (item is not Pawn) ? new Tradeable() : new Tradeable_Pawn();
                    tradeables.Add(tradeable);
                }
                tradeable.AddThing(item, Transactor.Trader);
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
                    tradeable =
                        item.thing.def != RimWorld.ThingDefOf.Silver
                            ? new Tradeable_Capped(item.Count)
                            : new Tradeable();
                    tradeables.Add(tradeable);
                }
                else if (tradeable is Tradeable_Capped capped)
                {
                    capped.cap += item.Count;
                }
                tradeable.AddThing(item.thing, Transactor.Colony);
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
                tradeable.AddThing(item, Transactor.Colony);
                tradeable.ForceTo(-1);
            }
            return tradeables;
        }
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
                thing.def,
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
}
