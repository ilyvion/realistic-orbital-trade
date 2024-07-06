namespace RealisticOrbitalTrade;

[HotSwappable]
internal class TradeAgreement : IExposable, ILoadReferenceable, IThingHolder
{
    private class Tradeable_Capped(int cap) : Tradeable
    {
        public int cap = cap;

        public override int CountHeldBy(Transactor trans)
        {
            if (trans == Transactor.Colony)
            {
                return cap;
            }
            else
            {
                return 0;
            }
        }
    }

    protected int loadID = -1;

    public TradeShip tradeShip;
    public Pawn negotiator;

    public ThingOwner<Thing> thingsSoldToPlayer;
    public List<ThingCountClass> thingsSoldToTrader = [];
    public List<Pawn> pawnsSoldToTrader = [];

    public TransportShip? toTraderTransportShip;
    public TransportShip? toPlayerTransportShip;

    public bool tradePausesDepartureTimer;

    public IThingHolder ParentHolder => tradeShip.Map;

    public List<Tradeable> AllTradeables
    {
        get
        {
            if (toPlayerTransportShip == null)
            {
                RealisticOrbitalTradeMod.Error("toPlayerTransportShip is null in TradeAgreement. This is a bug, can't calculate AllTradeables.");
                return [];
            }
            if (toTraderTransportShip == null)
            {
                RealisticOrbitalTradeMod.Error("toTraderTransportShip is null in TradeAgreement. This is a bug, can't calculate AllTradeables.");
                return [];
            }

            List<Tradeable> tradeables = [];
            foreach (var item in toPlayerTransportShip.TransporterComp.innerContainer)
            {
                Tradeable? tradeable = TransferableUtility.TradeableMatching(item, tradeables);
                if (tradeable == null)
                {
                    tradeable = (item is not Pawn)
                        ? new Tradeable()
                        : new Tradeable_Pawn();
                    tradeables.Add(tradeable);
                }
                tradeable.AddThing(item, Transactor.Trader);
                tradeable.ForceTo(tradeable.CountToTransfer + item.stackCount);
            }
            foreach (var item in thingsSoldToTrader)
            {
                Tradeable? tradeable = TransferableUtility.TradeableMatching(item.thing, tradeables);
                if (tradeable == null)
                {
                    tradeable = item.thing.def != RimWorld.ThingDefOf.Silver
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
                Tradeable? tradeable = TransferableUtility.TradeableMatching(item, tradeables);
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

    public IEnumerable<Thing> ColonyThingsTraderWillingToBuy()
    {
        return new Pawn_TraderTracker(negotiator).ColonyThingsWillingToBuy(negotiator);
    }

#pragma warning disable CS8618 // Required by savegame logic
    public TradeAgreement()
#pragma warning restore CS8618
    {
    }

    public TradeAgreement(TradeShip tradeShip, Pawn negotiator, bool tradePausesDepartureTimer)
    {
        this.tradeShip = tradeShip;
        this.negotiator = negotiator;
        this.tradePausesDepartureTimer = tradePausesDepartureTimer;

        thingsSoldToPlayer = new(this);

        loadID = RealisticOrbitalTradeGameComponent.Current.GetNextTradeId();
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref loadID, "loadID", 0);
        Scribe_References.Look(ref tradeShip, "tradeShip");
        Scribe_References.Look(ref negotiator, "negotiator");
        Scribe_Deep.Look(ref thingsSoldToPlayer, "thingsSoldToPlayer", this);
        Scribe_Collections.Look(ref thingsSoldToTrader, "thingsSoldToTrader", LookMode.Deep);
        Scribe_Collections.Look(ref pawnsSoldToTrader, "pawnsSoldToTrader", LookMode.Reference);
        Scribe_References.Look(ref toPlayerTransportShip, "toPlayerTransportShip");
        Scribe_References.Look(ref toTraderTransportShip, "toTraderTransportShip");
        if (Scribe.mode == LoadSaveMode.PostLoadInit &&
            (toPlayerTransportShip == null || toTraderTransportShip == null))
        {
            RealisticOrbitalTradeMod.WarningOnce("Detected an active trade agreement from before " +
                "trade renegotiation was added; this trade cannot be renegotiated.",
                Constants.OldTradeNonRenegotiableKey);
        }
    }

    public string GetUniqueLoadID()
    {
        return "TradeData_" + loadID;
    }

    public void GetChildHolders(List<IThingHolder> outChildren)
    {
        ThingOwnerUtility.AppendThingHoldersFromThings(outChildren, GetDirectlyHeldThings());
    }

    public ThingOwner GetDirectlyHeldThings()
    {
        return thingsSoldToPlayer;
    }
}
