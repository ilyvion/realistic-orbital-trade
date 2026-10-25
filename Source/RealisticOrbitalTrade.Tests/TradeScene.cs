using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// A colony with a powered trade beacon, a home area to load from and two colonists, plus a
// bulk-goods trader in orbit, for the end-to-end trade tests. Time is advanced by ticking the game
// directly, so a run doesn't depend on the game speed. Disposing puts back everything a test
// touched, so the next test on the same map and world sees none of it: settings, autosave interval,
// shuttle capacity, home area, quests, letters, items, shuttles, pawns and the traders.
#pragma warning disable CA1506 // CA1506: Avoid excessive class coupling
internal sealed class TradeScene : IDisposable
{
    private const int TicksPerFrame = 60;
    private const int TradeTimeoutTicks = 20000;

    // Far enough out that no autosave happens during a test. Saving the shared test world logs
    // warnings about what earlier tests removed, which would fail whichever test it lands in.
    private const float NoAutosaveIntervalDays = 1000f;

    private readonly SettingsSnapshot previousSettings = SettingsSnapshot.Capture();
    private readonly CompProperties_Transporter shuttleTransporter =
        TransportShipDefOf.Ship_Shuttle.shipThing.GetCompProperties<CompProperties_Transporter>();
    private readonly float previousShuttleCapacity;
    private readonly TimeSpeed previousTimeSpeed = Find.TickManager.CurTimeSpeed;
    private readonly float previousAutosaveIntervalDays = Prefs.AutosaveIntervalDays;
    private readonly List<Thing> spawned = [];
    private readonly List<IntVec3> homeCellsAdded = [];
    private readonly HashSet<TransportShip> shipsBefore =
    [
        .. Find.TransportShipManager.AllTransportShips,
    ];
    private readonly HashSet<Letter> lettersBefore = [.. Find.LetterStack.LettersListForReading];
    private readonly List<TradeShip> otherTraders = [];
    private int ticked;

    internal Map Map { get; }
    internal ThingDef SilverDef { get; } = RimWorld.ThingDefOf.Silver;
    internal TradeShip TradeShip { get; }
    internal TradeAgreement? TradeAgreement { get; private set; }
    internal Sale? CurrentSale { get; private set; }

    // The colonist who trades for the colony.
    internal Pawn Negotiator => spawned.OfType<Pawn>().First();

    internal IEnumerable<TransportShip> ToTraderShuttles =>
        TradeAgreement?.ToTraderTransportShips ?? [];

    internal bool AllToTraderShuttlesHaveLanded =>
        ToTraderShuttles.Any() && ToTraderShuttles.All(ship => ship.shipThing.Spawned);

    // Runs on the mod's default settings unless the test passes its own.
    internal TradeScene(Map map, SettingsSnapshot? settings = null)
    {
        Map = map;
        Expect.IsNull(FindTradeQuest(), "An earlier test leaked a trade quest");
        Expect.IsEmpty(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements,
            "An earlier test leaked a trade agreement"
        );
        Expect.IsEmpty(
            map.listerThings.AllThings.Where(t => t.def.category == ThingCategory.Item),
            "An earlier test leaked items onto the map"
        );
        Expect.IsEmpty(
            map.listerThings.AllThings.Where(t => t.TryGetComp<CompTradeShuttle>() != null),
            "An earlier test leaked trade shuttles onto the map"
        );
        previousShuttleCapacity = shuttleTransporter.massCapacity;

        // Only the test advances time, so the game's own ticking can't land between two of its ticks.
        Find.TickManager.CurTimeSpeed = TimeSpeed.Paused;
        Prefs.AutosaveIntervalDays = NoAutosaveIntervalDays;
        (settings ?? SettingsSnapshot.Defaults).Apply();

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
            var cell = CellFinder.RandomClosewalkCellNear(
                center + new IntVec3(-3 + i, 0, 3),
                map,
                8
            );
            _ = GenSpawn.Spawn(pawn, cell, map);
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

    // Puts an extra colonist on the map, cleaned up with the rest of the scene.
    internal Pawn SpawnPawn()
    {
        var pawn = GenerateCapableColonist();
        var cell = CellFinder.RandomClosewalkCellNear(Map.Center + new IntVec3(3, 0, 3), Map, 8);
        _ = GenSpawn.Spawn(pawn, cell, Map);
        spawned.Add(pawn);
        return pawn;
    }

    // Puts a powered colony building this far from the beacon, cleaned up with the rest of the
    // scene.
    internal Thing SpawnBuilding(ThingDef def, IntVec3 offsetFromBeacon)
    {
        var building = GenSpawn.Spawn(
            ThingMaker.MakeThing(def),
            Map.Center + offsetFromBeacon,
            Map
        );
        building.SetFaction(Faction.OfPlayer);
        if (building.TryGetComp<CompPowerTrader>() is { } power)
        {
            power.PowerOn = true;
        }
        spawned.Add(building);
        return building;
    }

    // Puts a second bulk-goods trader in orbit, sent away with the rest of the scene.
    internal TradeShip AddTrader()
    {
        var trader = new TradeShip(DefDatabase<TraderKindDef>.GetNamed("Orbital_BulkGoods"));
        Map.passingShipManager.AddShip(trader);
        trader.GenerateThings();
        otherTraders.Add(trader);
        return trader;
    }

    // Opens comms with another trader and reports how much of the colony's stock of this def its
    // trade dialog offers for sale, then closes the dialog without trading.
    internal int OfferedForSaleTo(TradeShip trader, ThingDef def)
    {
        trader.TryOpenComms(spawned.OfType<Pawn>().First());
        return OfferedInOpenDialog(def);
    }

    // Opens the renegotiation dialog the way RenegotiateSale does and reports how much of the
    // colony's stock of this def it offers for sale, then closes it without changing the deal.
    internal int OfferedInRenegotiation(ThingDef def)
    {
        var agreement = TradeAgreement!;
        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        return OfferedInOpenDialog(def);
    }

    private static int OfferedInOpenDialog(ThingDef def)
    {
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
        if (dialog == null)
        {
            return 0;
        }
        var offered = TradeSession
            .deal.AllTradeables.Where(t => t.ThingDef == def)
            .Sum(t => t.CountHeldBy(Transactor.Colony));
        dialog.Close(doCloseSound: false);
        return offered;
    }

    // Sells this many to another trader through its trade dialog and accepts.
    internal void SellTo(TradeShip trader, ThingDef def, int count)
    {
        trader.TryOpenComms(spawned.OfType<Pawn>().First());
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
        if (dialog != null)
        {
            _ = SellInOpenDialog(dialog, def, count);
        }
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
        yield return SellAndWaitForLanding(def, count);
        if (CurrentSale?.Quest != null)
        {
            yield return WaitForQuestToEnd();
        }
    }

    // Sells as SellAndWaitForCompletion does, but stops once every shuttle for the trader has
    // landed, so a test can interfere with the trade while it is under way.
    internal IEnumerator SellAndWaitForLanding(ThingDef def, int count)
    {
        var sale = new Sale(def, count, HeldByTrader(def), CountOnMap(def), CountOnMap(SilverDef));
        CurrentSale = sale;

        sale.SilverOwed = OpenCommsAndSell(def, count);
        if (sale.SilverOwed <= 0)
        {
            yield break;
        }

        yield return WaitForShuttleQuest(sale);
    }

    // Sells as SellAndWaitForLanding does, but lets the trade action make the sale, for a sale that
    // doesn't go through the scene's trade dialog. The payment is what the trader set aside for the
    // colony.
    internal IEnumerator TradeAndWaitForLanding(ThingDef def, int count, Action trade) =>
        TradeAndWaitForLanding(def, count, Run(trade));

    // Sells as TradeAndWaitForLanding does, for a trade that takes time to make, such as a
    // colonist's job.
    internal IEnumerator TradeAndWaitForLanding(ThingDef def, int count, IEnumerator trade)
    {
        var sale = new Sale(def, count, HeldByTrader(def), CountOnMap(def), CountOnMap(SilverDef));
        CurrentSale = sale;

        yield return trade;
        TradeAgreement = TradeShip.GetData().activeTradeAgreement;
        Expect.IsNotNull(TradeAgreement, "The trade didn't start a trade agreement");
        if (TradeAgreement == null)
        {
            yield break;
        }
        sale.SilverOwed = TradeAgreement
            .thingsSoldToPlayer.Where(t => t.def == SilverDef)
            .Sum(t => t.stackCount);

        yield return WaitForShuttleQuest(sale);
    }

    // Records the shuttle quest accepting the trade generated, then ticks until every shuttle for
    // the trader has landed. Quests of earlier sales in the scene that have ended are skipped.
    private IEnumerator WaitForShuttleQuest(Sale sale)
    {
        var quest = Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
            q.root == QuestScriptDefOf.ROT_TradeShipTransportShip && !q.Historical
        );
        Expect.IsNotNull(quest, "Accepting the trade didn't generate the shuttle quest");
        Expect.IsNotNull(TradeAgreement!.tradeDeliveryMethod, "No delivery arranged for payment");
        if (quest == null)
        {
            yield break;
        }
        sale.Quest = quest;
        sale.Shuttles = [.. ToTraderShuttles];
        sale.ShuttleCount = sale.Shuttles.Count;

        yield return TickUntil(() => AllToTraderShuttlesHaveLanded);
        Expect.IsTrue(AllToTraderShuttlesHaveLanded, "Trade shuttles never all landed");
    }

    // Opens comms through the real trade dialog, buys goods from the trader paying with the
    // colony's silver and accepts, then lets time pass until the trader's shuttles have landed. A
    // purchase is recorded as a sale of a negative count, paid a negative amount of silver, so
    // ExpectSaleCompleted checks it the same way.
    internal IEnumerator BuyAndWaitForLanding(ThingDef def, int count)
    {
        var sale = new Sale(def, -count, HeldByTrader(def), CountOnMap(def), CountOnMap(SilverDef));
        CurrentSale = sale;

        var negotiator = spawned.OfType<Pawn>().First();
        TradeShip.TryOpenComms(negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        TradeAgreement = TradeShipData.tradeAgreementForQuest;
        Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
        Expect.IsNotNull(TradeAgreement, "No trade agreement started");
        if (dialog == null || TradeAgreement == null)
        {
            yield break;
        }
        var tradeable = TradeSession.deal.AllTradeables.FirstOrDefault(t =>
            t.ThingDef == def && t.CountHeldBy(Transactor.Trader) > 0
        );
        Expect.IsNotNull(tradeable, $"The trader doesn't offer {def.label}");
        if (tradeable == null)
        {
            dialog.Close(doCloseSound: false);
            yield break;
        }
        sale.SilverOwed = TradeInOpenDialog(dialog, tradeable, count);
        Expect.LessThan(sale.SilverOwed, 0, "The purchase doesn't cost any silver");
        if (sale.SilverOwed >= 0)
        {
            yield break;
        }

        yield return WaitForShuttleQuest(sale);
    }

    // Ticks until the current sale's quest is no longer ongoing.
    internal IEnumerator WaitForQuestToEnd() => WaitForQuestToEnd(() => { });

    // Ticks until the current sale's quest is no longer ongoing, calling eachFrame between batches
    // of ticks so a test can watch the trade unfold.
    internal IEnumerator WaitForQuestToEnd(Action eachFrame)
    {
        var quest = CurrentSale!.Quest!;
        yield return TickUntil(() =>
        {
            eachFrame();
            return quest.State != QuestState.Ongoing;
        });
    }

    // How many of the trader's shuttles delivering bought goods are on the map right now.
    internal int DeliveryShuttlesOnMap =>
        Map.listerThings.AllThings.Count(thing =>
            thing.TryGetComp<CompTradeShuttle>() is { isToTrader: false }
        );

    // How many shuttles the current sale's quest has sent to deliver bought goods.
    internal int DeliveryShuttlesSent =>
        CurrentSale!
            .Quest!.PartsListForReading.OfType<Quests.QuestPart_DeliverBoughtItemsByShuttles>()
            .Sum(part => part.shuttlesSent);

    // Ticks until the trader's shuttles have all left the map.
    internal IEnumerator WaitForShuttlesToLeave()
    {
        var shuttles = CurrentSale!.Shuttles;
        yield return TickUntil(() => shuttles.All(ship => !ship.shipThing.Spawned));
    }

    // Ticks one tick at a time until some, but not all, of the sold goods are aboard the shuttles,
    // so a test can act on a half-loaded shuttle.
    internal IEnumerator WaitForPartialLoad()
    {
        var sale = CurrentSale!;
        while (LoadedInShuttles(sale.Def) == 0 && ticked < TradeTimeoutTicks)
        {
            Find.TickManager.DoSingleTick();
            ticked++;
            if (ticked % TicksPerFrame == 0)
            {
                yield return null;
            }
        }
        var loaded = LoadedInShuttles(sale.Def);
        Expect.GreaterThan(loaded, 0, "Nothing was ever loaded onto the shuttle");
        Expect.LessThan(
            loaded,
            sale.Count,
            "The shuttle was fully loaded before it could be cancelled"
        );
    }

    // Ticks until a colonist carries some of the sold goods in their inventory, as a bulk loader
    // does between picking them up and loading them.
    internal IEnumerator WaitForSoldGoodsInAnInventory()
    {
        var def = CurrentSale!.Def;
        bool inAnInventory()
        {
            return spawned
                .OfType<Pawn>()
                .Any(pawn => pawn.inventory.innerContainer.Any(thing => thing.def == def));
        }
        yield return TickUntil(inAnInventory);
        Expect.IsTrue(inAnInventory(), "No colonist ever carried the sold goods in inventory");
    }

    // Ticks one tick at a time until one of the scene's colonists meets the condition, and returns
    // it through the callback, so a test can act on a hauler mid-trip.
    internal IEnumerator WaitForColonist(Func<Pawn, bool> condition, Action<Pawn?> found)
    {
        Pawn? match()
        {
            return spawned.OfType<Pawn>().FirstOrDefault(condition);
        }
        while (match() == null && ticked < TradeTimeoutTicks)
        {
            Find.TickManager.DoSingleTick();
            ticked++;
            if (ticked % TicksPerFrame == 0)
            {
                yield return null;
            }
        }
        found(match());
    }

    // How much of this def lying on the map the trade protects from other colony jobs.
    internal int ProtectedOnMap(ThingDef def) =>
        Map.listerThings.ThingsOfDef(def).Sum(t => TradeAgreement!.ReservedCountFor(t));

    internal int LoadedInShuttles(ThingDef def) =>
        CurrentSale!
            .Shuttles.SelectMany(ship => ship.TransporterComp.innerContainer)
            .Where(t => t.def == def)
            .Sum(t => t.stackCount);

    // Does what the player's "renegotiate" button on a landed shuttle does, then changes the amount
    // sold to this many and accepts. The sale then expects the new amount and payment.
    internal void RenegotiateSale(int newCount)
    {
        var sale = CurrentSale!;
        var agreement = TradeAgreement!;
        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        Expect.IsNotNull(dialog, "Renegotiating didn't reopen the trade dialog");
        if (dialog == null)
        {
            Utils.EndRenegotiation();
            return;
        }

        var silverOwed = SellInOpenDialog(dialog, sale.Def, newCount);
        if (silverOwed > 0)
        {
            sale.Count = newCount;
            sale.SilverOwed = silverOwed;
        }
        else
        {
            dialog.Close(doCloseSound: false);
        }
    }

    // Makes the trader leave this many ticks from now, so the shuttles' departure timer, which
    // follows it, runs out within the test. Call before selling.
    internal void ShortenTraderStay(int ticks) =>
        TradeShip.ticksUntilDeparture = ticks + Constants.AdditionalTicksAfterDeparture;

    // Lets the shuttles' departure timer run down to a minute from now, so it runs out while the
    // shuttles are partly loaded.
    internal void RunDownDepartureTimer()
    {
        var delay = CurrentSale!
            .Quest!.PartsListForReading.OfType<Quests.QuestPart_TradeShuttleLeaveDelay>()
            .First();
        delay.delayTicks -= delay.TicksLeft - 60;
    }

    // Turns hauling off for the colonists, so nothing gets loaded.
    internal void StopColonistsHauling()
    {
        foreach (var pawn in spawned.OfType<Pawn>())
        {
            pawn.workSettings.SetPriority(WorkTypeDefOf.Hauling, 0);
        }
    }

    // Ticks until the shuttles' departure timer has run out and the trader is asking the player to
    // renegotiate down to what is loaded.
    internal IEnumerator WaitForDepartureRenegotiation()
    {
        yield return TickUntil(() => FindDepartureExplanation() != null);
        Expect.IsNotNull(
            FindDepartureExplanation(),
            "The expired departure timer didn't offer to renegotiate"
        );
    }

    // Does what the player does after the departure explanation: reopens comms with the deal cut
    // down to what is loaded, and accepts. The sale then expects only what was loaded.
    internal void AcceptDepartureRenegotiation()
    {
        var sale = CurrentSale!;
        var explanation = FindDepartureExplanation();
        if (explanation == null)
        {
            return;
        }
        var loaded = LoadedInShuttles(sale.Def);
        _ = Find.WindowStack.TryRemove(explanation, doCloseSound: false);
        TradeShip.TryOpenComms(TradeAgreement!.negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        Expect.IsNotNull(dialog, "The departure renegotiation didn't reopen the trade dialog");
        if (dialog == null)
        {
            return;
        }

        var deal = TradeSession.deal;
        deal.UpdateCurrencyCount();
        var silverOwed = deal.CurrencyTradeable.CountToTransfer;
        Expect.IsTrue(deal.TryExecute(out _), "The departure renegotiation was rejected");
        dialog.Close(doCloseSound: false);
        sale.Count = loaded;
        sale.SilverOwed = silverOwed;
    }

    private static Dialog_MessageBox? FindDepartureExplanation() =>
        Find.WindowStack.WindowOfType<Dialog_MessageBox>();

    // Does what the player's "cancel trade" button on a landed shuttle does.
    internal void CancelTradeFromShuttle()
    {
        var shuttle = CurrentSale!.Shuttles.First().shipThing;
        var cancel = shuttle.TryGetComp<CompTradeShuttle>()?.cancelTradeAction;
        Expect.IsNotNull(cancel, "The landed shuttle offers no way to cancel the trade");
        cancel?.Invoke();
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

    // The assertions every cancelled sale shares: the quest failed, nothing changed hands, and
    // the shuttles left.
    internal void ExpectSaleCancelled()
    {
        var sale = CurrentSale!;
        Expect.AreEqual(QuestState.EndedFailed, sale.Quest?.State, "Trade quest didn't fail");
        Expect.IsNull(
            TradeShip.GetData().activeTradeAgreement,
            "Trade agreement is still active after cancelling"
        );
        Expect.IsTrue(
            sale.Shuttles.All(ship => !ship.shipThing.Spawned),
            "A trade shuttle didn't leave"
        );
        Expect.AreEqual(
            sale.TraderCountBefore,
            HeldByTrader(sale.Def),
            "Trader kept goods from a cancelled trade"
        );
        Expect.AreEqual(
            sale.MapCountBefore,
            CountOnMap(sale.Def),
            "Goods from a cancelled trade weren't returned to the colony"
        );
        Expect.AreEqual(
            sale.SilverOnMapBefore,
            CountOnMap(SilverDef),
            "Silver arrived for a cancelled trade"
        );
    }

    // Lets this many ticks pass.
    internal IEnumerator TickFor(int ticks)
    {
        var until = ticked + ticks;
        yield return TickUntil(() => ticked >= until);
    }

    private static IEnumerator Run(Action action)
    {
        action();
        yield break;
    }

    // Ticks the game a frame's worth at a time until the condition holds or the trade times out.
    internal IEnumerator TickUntil(Func<bool> condition)
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
        return dialog == null || TradeAgreement == null ? 0 : SellInOpenDialog(dialog, def, count);
    }

    // Sets the open dialog's deal to sell exactly this many and accepts it. Returns the silver the
    // colony is owed, or 0 after recording why the deal wasn't made.
    private static int SellInOpenDialog(Dialog_Trade dialog, ThingDef def, int count)
    {
        var deal = TradeSession.deal;
        var tradeable = deal.AllTradeables.FirstOrDefault(t =>
            t.ThingDef == def && t.CountHeldBy(Transactor.Colony) > 0
        );
        Expect.IsNotNull(tradeable, $"Colony {def.label} isn't offered for sale");
        if (tradeable == null)
        {
            return 0;
        }
        var silverToReceive = TradeInOpenDialog(dialog, tradeable, -count);
        Expect.GreaterThan(silverToReceive, 0, "The sale doesn't pay any silver");
        return silverToReceive;
    }

    // Sets the open dialog's deal to transfer this many of the tradeable (negative to sell,
    // positive to buy) and accepts it. Returns the silver the colony receives, negative when it
    // pays.
    private static int TradeInOpenDialog(Dialog_Trade dialog, Tradeable tradeable, int count)
    {
        var deal = TradeSession.deal;
        tradeable.ForceTo(count);
        deal.UpdateCurrencyCount();
        var silverToReceive = deal.CurrencyTradeable.CountToTransfer;

        Expect.IsTrue(deal.TryExecute(out var actuallyTraded), "Trade was rejected");
        Expect.IsTrue(actuallyTraded, "Trade didn't execute");
        dialog.Close(doCloseSound: false);
        // Other mods close the session once a trade is made so the rest of the trade must not
        // depend on it.
        TradeSession.Close();
        return silverToReceive;
    }

    // The alerts readout only drops a quest part's alert while it can still find the quest in the
    // quest manager, so removing the quest first would leave the alert on screen for good.
    private static void RemoveQuestAlerts(Quest quest)
    {
        if (
            Find.UIRoot is not UIRoot_Play root
            || AccessTools.Field(typeof(AlertsReadout), "activeAlerts").GetValue(root.alerts)
                is not List<Alert> activeAlerts
        )
        {
            return;
        }

        foreach (var part in quest.PartsListForReading.OfType<QuestPartActivable>())
        {
            if (part.CachedAlert is { } alert)
            {
                _ = activeAlerts.Remove(alert);
            }
        }
    }

    private static void EndTradeQuestsAndAgreements()
    {
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
            RemoveQuestAlerts(quest);
            Find.QuestManager.Remove(quest);
        }
        // Ending a quest early skips the part that ends its trade agreement.
        var gameComponent = RealisticOrbitalTradeGameComponent.Current;
        foreach (var agreement in gameComponent.ActiveTradeAgreements.ToList())
        {
            gameComponent.EndTradeAgreement(agreement);
        }
    }

    public void Dispose()
    {
        previousSettings.Apply();
        shuttleTransporter.massCapacity = previousShuttleCapacity;
        Find.TickManager.CurTimeSpeed = previousTimeSpeed;
        Prefs.AutosaveIntervalDays = previousAutosaveIntervalDays;
        TradeShipData.tradeAgreementForQuest = null;
        Utils.EndRenegotiation();
        if (FindDepartureExplanation() is { } explanation)
        {
            _ = Find.WindowStack.TryRemove(explanation, doCloseSound: false);
        }

        List<TradeAgreement> agreements =
        [
            .. new[] { TradeAgreement }
                .Concat(otherTraders.Select(trader => trader.GetData().activeTradeAgreement))
                .OfType<TradeAgreement>(),
        ];
        foreach (var trader in otherTraders.Prepend(TradeShip))
        {
            if (!trader.Departed)
            {
                trader.Depart();
            }
        }
        EndTradeQuestsAndAgreements();
        foreach (var letter in Find.LetterStack.LettersListForReading.ToList())
        {
            if (!lettersBefore.Contains(letter))
            {
                Find.LetterStack.RemoveLetter(letter);
            }
        }
        // Nothing destroyed from here on is a loss to a trade that no longer exists.
        foreach (var agreement in agreements)
        {
            agreement.toTraderTransportShip = null;
            agreement.additionalToTraderTransportShips.Clear();
        }
        foreach (
            var ship in Find
                .TransportShipManager.AllTransportShips.Where(ship => !shipsBefore.Contains(ship))
                .ToList()
        )
        {
            if (!ship.Disposed)
            {
                ship.Dispose();
            }
        }
        foreach (var cell in homeCellsAdded)
        {
            Map.areaManager.Home[cell] = false;
        }
        foreach (var thing in spawned)
        {
            if (!thing.Destroyed)
            {
                thing.Destroy();
            }
        }
        // Destroying a shuttle or a pawn drops whatever it holds onto the map, so sweep until
        // nothing is left.
        var shuttleDef = TransportShipDefOf.Ship_Shuttle.shipThing;
        for (var pass = 0; pass < 5; pass++)
        {
            var leftovers = Map
                .listerThings.AllThings.Where(thing =>
                    !thing.Destroyed
                    && (
                        thing.def.category == ThingCategory.Item
                        || thing.def == shuttleDef
                        || thing.TryGetComp<CompTradeShuttle>() != null
                    )
                )
                .ToList();
            if (leftovers.Count == 0)
            {
                break;
            }
            foreach (var thing in leftovers)
            {
                if (!thing.Destroyed)
                {
                    thing.Destroy();
                }
            }
        }
        Expect.IsFalse(
            Map.listerThings.AllThings.Any(thing =>
                !thing.Destroyed
                && (
                    thing.def.category == ThingCategory.Item
                    || thing.def == shuttleDef
                    || thing.TryGetComp<CompTradeShuttle>() != null
                )
            ),
            "Could not clean leftover items from the map"
        );
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
        internal int Count { get; set; } = count;
        internal int TraderCountBefore { get; } = traderCountBefore;
        internal int MapCountBefore { get; } = mapCountBefore;
        internal int SilverOnMapBefore { get; } = silverOnMapBefore;
        internal int SilverOwed { get; set; }
        internal int ShuttleCount { get; set; }
        internal Quest? Quest { get; set; }
        internal List<TransportShip> Shuttles { get; set; } = [];
    }

    // Every mod setting, so a test runs on known values instead of whatever the player has saved
    // in the mod's config file, and the player's values come back afterwards.
    internal readonly record struct SettingsSnapshot(
        bool PrintDevMessages,
        bool RenegotiationWarningShown,
        bool ActiveTradePausesDepartureTimer,
        int MinTicksUntilDepartureBeforeGraceTime,
        int DepartureGraceTimeTicks,
        int MinimumTradeThreshold,
        bool UseMinimumTradeThreshold,
        int MinimumTradeDeviation,
        bool DeliverItemsViaDropPod,
        int BuyMoreTimeTicksPerPurchase,
        int BuyMoreTimeBaseSilverCost,
        float BuyMoreTimeCostGrowthFactor,
        bool MultiItemShuttleLoading,
        int HitPointsTolerancePercent
    )
    {
        // The mod's own defaults.
        internal static SettingsSnapshot Defaults { get; } =
            new(
                PrintDevMessages: false,
                RenegotiationWarningShown: false,
                ActiveTradePausesDepartureTimer: false,
                MinTicksUntilDepartureBeforeGraceTime: 20000,
                DepartureGraceTimeTicks: 40000,
                MinimumTradeThreshold: 600,
                UseMinimumTradeThreshold: false,
                MinimumTradeDeviation: 15,
                DeliverItemsViaDropPod: false,
                BuyMoreTimeTicksPerPurchase: 7500,
                BuyMoreTimeBaseSilverCost: 200,
                BuyMoreTimeCostGrowthFactor: 1.5f,
                MultiItemShuttleLoading: true,
                HitPointsTolerancePercent: 2
            );

        internal static SettingsSnapshot Capture() =>
            new(
                Settings._printDevMessages,
                Settings._renegotiationWarningShown,
                Settings._activeTradePausesDepartureTimer,
                Settings._minTicksUntilDepartureBeforeGraceTime,
                Settings._departureGraceTimeTicks,
                Settings._minimumTradeThreshold,
                Settings._useMinimumTradeThreshold,
                Settings._minimumTradeDeviation,
                Settings._deliverItemsViaDropPod,
                Settings._buyMoreTimeTicksPerPurchase,
                Settings._buyMoreTimeBaseSilverCost,
                Settings._buyMoreTimeCostGrowthFactor,
                Settings._multiItemShuttleLoading,
                Settings._hitPointsTolerancePercent
            );

        internal void Apply()
        {
            Settings._printDevMessages = PrintDevMessages;
            Settings._renegotiationWarningShown = RenegotiationWarningShown;
            Settings._activeTradePausesDepartureTimer = ActiveTradePausesDepartureTimer;
            Settings._minTicksUntilDepartureBeforeGraceTime = MinTicksUntilDepartureBeforeGraceTime;
            Settings._departureGraceTimeTicks = DepartureGraceTimeTicks;
            Settings._minimumTradeThreshold = MinimumTradeThreshold;
            Settings._useMinimumTradeThreshold = UseMinimumTradeThreshold;
            Settings._minimumTradeDeviation = MinimumTradeDeviation;
            Settings._deliverItemsViaDropPod = DeliverItemsViaDropPod;
            Settings._buyMoreTimeTicksPerPurchase = BuyMoreTimeTicksPerPurchase;
            Settings._buyMoreTimeBaseSilverCost = BuyMoreTimeBaseSilverCost;
            Settings._buyMoreTimeCostGrowthFactor = BuyMoreTimeCostGrowthFactor;
            Settings._multiItemShuttleLoading = MultiItemShuttleLoading;
            Settings._hitPointsTolerancePercent = HitPointsTolerancePercent;
        }
    }
}
#pragma warning restore CA1506
