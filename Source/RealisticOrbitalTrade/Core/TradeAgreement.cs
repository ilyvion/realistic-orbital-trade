using RealisticOrbitalTrade.Core;
using RimWorld.Planet;

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
            foreach (var item in thingsSoldToTrader)
            {
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

    public IEnumerable<Thing> ColonyThingsTraderWillingToBuy() =>
        new Pawn_TraderTracker(negotiator).ColonyThingsWillingToBuy(negotiator);

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
        Scribe_Values.Look(ref tradePausesDepartureTimer, "tradePausesDepartureTimer", false);
        Scribe_Deep.Look(ref thingsSoldToPlayer, "thingsSoldToPlayer", this);
        Scribe_Collections.Look(ref tmpSavedPawns, "tmpSavedPawns", LookMode.Reference);
        Scribe_Values.Look(ref deliverItemsViaDropPod, "deliverItemsViaDropPod", false);
        Scribe_Collections.Look(ref thingsSoldToTrader, "thingsSoldToTrader", LookMode.Deep);
        Scribe_Collections.Look(ref pawnsSoldToTrader, "pawnsSoldToTrader", LookMode.Reference);
        Scribe_References.Look(ref toTraderTransportShip, "toTraderTransportShip");

        Scribe_Deep.Look(ref tradeDeliveryMethod, "tradeDeliveryMethod");

#pragma warning disable CS0612 // We still load it for backwards compatibility
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
        if (Scribe.mode == LoadSaveMode.PostLoadInit && (toPlayerTransportShip != null))
        {
            tradeDeliveryMethod = new TradeDeliveryMethod_TransportShip(toPlayerTransportShip);
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
    }
}
