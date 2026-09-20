using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// A colony with a powered trade beacon, a home area to load from and two colonists, plus a
// bulk-goods trader in orbit, for the end-to-end trade tests. Time is advanced by ticking the game
// directly, so a run doesn't depend on the game speed. Disposing puts back everything a test
// touched, so the next test on the same map and world sees none of it: settings, shuttle capacity,
// home area, quests, letters, items, shuttles, pawns and the trader.
internal sealed class TradeScene : IDisposable
{
    private const int TicksPerFrame = 60;
    private const int TradeTimeoutTicks = 20000;

    private readonly bool previousThreshold = Settings._useMinimumTradeThreshold;
    private readonly bool previousDropPods = Settings._deliverItemsViaDropPod;
    private readonly bool previousPauseTimer = Settings._activeTradePausesDepartureTimer;
    private readonly CompProperties_Transporter shuttleTransporter =
        TransportShipDefOf.Ship_Shuttle.shipThing.GetCompProperties<CompProperties_Transporter>();
    private readonly float previousShuttleCapacity;
    private readonly List<Thing> spawned = [];
    private readonly List<IntVec3> homeCellsAdded = [];
    private readonly HashSet<Letter> lettersBefore = [.. Find.LetterStack.LettersListForReading];
    private int ticked;

    internal Map Map { get; }
    internal ThingDef SilverDef { get; } = RimWorld.ThingDefOf.Silver;
    internal TradeShip TradeShip { get; }
    internal TradeAgreement? TradeAgreement { get; private set; }
    internal Sale? CurrentSale { get; private set; }

    internal IEnumerable<TransportShip> ToTraderShuttles =>
        TradeAgreement?.ToTraderTransportShips ?? [];

    internal bool AllToTraderShuttlesHaveLanded =>
        ToTraderShuttles.Any() && ToTraderShuttles.All(ship => ship.shipThing.Spawned);

    internal TradeScene(Map map)
    {
        Map = map;
        Expect.IsNull(FindTradeQuest(), "An earlier test leaked a trade quest");
        previousShuttleCapacity = shuttleTransporter.massCapacity;
        Settings._useMinimumTradeThreshold = false;
        Settings._deliverItemsViaDropPod = false;
        Settings._activeTradePausesDepartureTimer = false;

        var center = map.Center;
        foreach (var cell in CellRect.CenteredOn(center, 12))
        {
            if (!map.areaManager.Home[cell])
            {
                map.areaManager.Home[cell] = true;
                homeCellsAdded.Add(cell);
            }
        }

        var beacon = GenSpawn.Spawn(
            ThingMaker.MakeThing(RimWorld.ThingDefOf.OrbitalTradeBeacon),
            center,
            map
        );
        beacon.SetFaction(Faction.OfPlayer);
        beacon.TryGetComp<CompPowerTrader>().PowerOn = true;
        spawned.Add(beacon);

        for (var i = 0; i < 2; i++)
        {
            var pawn = GenerateCapableColonist();
            _ = GenSpawn.Spawn(pawn, center + new IntVec3(-3 + i, 0, 3), map);
            for (var hour = 0; hour < 24; hour++)
            {
                pawn.timetable.SetAssignment(hour, TimeAssignmentDefOf.Anything);
            }
            spawned.Add(pawn);
        }

        TradeShip = new TradeShip(DefDatabase<TraderKindDef>.GetNamed("Orbital_BulkGoods"));
        map.passingShipManager.AddShip(TradeShip);
        TradeShip.GenerateThings();
    }

    // Random colonists can be unable to trade or haul, which would make the sale depend on luck.
    private static Pawn GenerateCapableColonist()
    {
        Pawn pawn;
        do
        {
            pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
        } while (
            StatDefOf.TradePriceImprovement.Worker.IsDisabledFor(pawn)
            || pawn.WorkTypeIsDisabled(WorkTypeDefOf.Hauling)
        );
        return pawn;
    }

    // Makes one shuttle able to carry only this much, so a modest sale needs several.
    internal void LimitShuttleCapacity(float kilograms) =>
        shuttleTransporter.massCapacity = kilograms;

    // Places the goods loose on the map next to the beacon, in as many stacks as they need.
    internal void PlaceOnMap(ThingDef def, int count)
    {
        var center = Map.Center + new IntVec3(2, 0, 0);
        while (count > 0)
        {
            var thing = ThingMaker.MakeThing(def);
            thing.stackCount = Math.Min(count, def.stackLimit);
            count -= thing.stackCount;
            _ = GenPlace.TryPlaceThing(thing, center, Map, ThingPlaceMode.Near);
            spawned.Add(thing);
        }
    }

    internal int CountOnMap(ThingDef def) =>
        Map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);

    internal int HeldByTrader(ThingDef def) =>
        TradeShip.Goods.Where(t => t.def == def).Sum(t => t.stackCount);

    internal static Quest? FindTradeQuest() =>
        Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
            q.root == QuestScriptDefOf.ROT_TradeShipTransportShip
        );

    // Opens comms through the real trade dialog, sells goods and accepts, then lets time pass
    // until the quest finishes. Everything after accepting follows from the colonists doing their
    // work. Sets CurrentSale; check it with ExpectSaleCompleted.
    internal IEnumerator SellAndWaitForCompletion(ThingDef def, int count)
    {
        var sale = new Sale(
            def,
            count,
            HeldByTrader(def),
            CountOnMap(def),
            CountOnMap(SilverDef)
        );
        CurrentSale = sale;

        sale.SilverOwed = OpenCommsAndSell(def, count);
        if (sale.SilverOwed <= 0)
        {
            yield break;
        }
        var quest = FindTradeQuest();
        Expect.IsNotNull(quest, "Accepting the trade didn't generate the shuttle quest");
        Expect.IsNotNull(
            TradeAgreement!.tradeDeliveryMethod,
            "No delivery arranged for payment"
        );
        if (quest == null)
        {
            yield break;
        }
        sale.Quest = quest;
        sale.ShuttleCount = ToTraderShuttles.Count();

        yield return TickUntil(() => AllToTraderShuttlesHaveLanded);
        Expect.IsTrue(AllToTraderShuttlesHaveLanded, "Trade shuttles never all landed");

        yield return TickUntil(() => quest.State != QuestState.Ongoing);
    }

    // The assertions every completed sale shares: the quest succeeded, the goods left the colony
    // for the trader, and the payment arrived.
    internal void ExpectSaleCompleted()
    {
        var sale = CurrentSale!;
        Expect.AreEqual(QuestState.EndedSuccess, sale.Quest?.State, "Trade quest didn't succeed");
        Expect.IsNull(
            TradeShip.GetData().activeTradeAgreement,
            "Trade agreement is still active after delivery"
        );
        Expect.AreEqual(
            sale.TraderCountBefore + sale.Count,
            HeldByTrader(sale.Def),
            "Trader didn't receive the sold goods"
        );
        Expect.AreEqual(
            sale.MapCountBefore - sale.Count,
            CountOnMap(sale.Def),
            "Sold goods are still in the colony"
        );
        Expect.AreEqual(
            sale.SilverOnMapBefore + sale.SilverOwed,
            CountOnMap(SilverDef),
            "Payment didn't arrive"
        );
    }

    // Ticks the game a frame's worth at a time until the condition holds or the trade times out.
    private IEnumerator TickUntil(Func<bool> condition)
    {
        while (!condition() && ticked < TradeTimeoutTicks)
        {
            for (var i = 0; i < TicksPerFrame; i++)
            {
                Find.TickManager.DoSingleTick();
            }
            ticked += TicksPerFrame;
            yield return null;
        }
    }

    // Returns the silver the colony is owed, or 0 after recording why the trade couldn't be made.
    private int OpenCommsAndSell(ThingDef def, int count)
    {
        var negotiator = spawned.OfType<Pawn>().First();
        TradeShip.TryOpenComms(negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        TradeAgreement = TradeShipData.tradeAgreementForQuest;
        Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
        Expect.IsNotNull(TradeAgreement, "No trade agreement started");
        if (dialog == null || TradeAgreement == null)
        {
            return 0;
        }

        var deal = TradeSession.deal;
        var tradeable = deal.AllTradeables.FirstOrDefault(t =>
            t.ThingDef == def && t.CountHeldBy(Transactor.Colony) > 0
        );
        Expect.IsNotNull(tradeable, $"Colony {def.label} isn't offered for sale");
        if (tradeable == null)
        {
            return 0;
        }
        tradeable.ForceTo(-count);
        deal.UpdateCurrencyCount();
        var silverToReceive = deal.CurrencyTradeable.CountToTransfer;
        Expect.GreaterThan(silverToReceive, 0, "The sale doesn't pay any silver");

        Expect.IsTrue(deal.TryExecute(out var actuallyTraded), "Trade was rejected");
        Expect.IsTrue(actuallyTraded, "Trade didn't execute");
        dialog.Close(doCloseSound: false);
        return silverToReceive;
    }

    public void Dispose()
    {
        Settings._useMinimumTradeThreshold = previousThreshold;
        Settings._deliverItemsViaDropPod = previousDropPods;
        Settings._activeTradePausesDepartureTimer = previousPauseTimer;
        shuttleTransporter.massCapacity = previousShuttleCapacity;
        TradeShipData.tradeAgreementForQuest = null;

        if (!TradeShip.Departed)
        {
            TradeShip.Depart();
        }
        foreach (var quest in Find.QuestManager.QuestsListForReading.ToList())
        {
            if (quest.root != QuestScriptDefOf.ROT_TradeShipTransportShip)
            {
                continue;
            }
            if (quest.State == QuestState.Ongoing)
            {
                quest.End(QuestEndOutcome.Unknown, sendLetter: false);
            }
            Find.QuestManager.Remove(quest);
        }
        foreach (var letter in Find.LetterStack.LettersListForReading.ToList())
        {
            if (!lettersBefore.Contains(letter))
            {
                Find.LetterStack.RemoveLetter(letter);
            }
        }
        foreach (var cell in homeCellsAdded)
        {
            Map.areaManager.Home[cell] = false;
        }
        var shuttleDef = TransportShipDefOf.Ship_Shuttle.shipThing;
        foreach (var thing in Map.listerThings.AllThings.ToList())
        {
            if (
                !thing.Destroyed
                && (thing.def.category == ThingCategory.Item || thing.def == shuttleDef)
            )
            {
                thing.Destroy();
            }
        }
        foreach (var thing in spawned)
        {
            if (!thing.Destroyed)
            {
                thing.Destroy();
            }
        }
    }

    // What a sale started with, and what it was agreed to be worth.
    internal sealed class Sale(
        ThingDef def,
        int count,
        int traderCountBefore,
        int mapCountBefore,
        int silverOnMapBefore
    )
    {
        internal ThingDef Def { get; } = def;
        internal int Count { get; } = count;
        internal int TraderCountBefore { get; } = traderCountBefore;
        internal int MapCountBefore { get; } = mapCountBefore;
        internal int SilverOnMapBefore { get; } = silverOnMapBefore;
        internal int SilverOwed { get; set; }
        internal int ShuttleCount { get; set; }
        internal Quest? Quest { get; set; }
    }
}
