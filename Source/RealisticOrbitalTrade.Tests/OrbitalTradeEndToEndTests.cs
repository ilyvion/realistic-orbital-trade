using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Drives one complete orbital trade on a live map: the trade dialog is opened and accepted, the
// trader's shuttle lands, colonists load the sold goods, the shuttle takes off, and the silver
// paid arrives on the second shuttle. Time is advanced by ticking the game directly, so the run
// doesn't depend on the game speed.
[TestFixture(TestType.Playing)]
internal sealed class OrbitalTradeEndToEndTests
{
    private const int SteelToSell = 40;
    private const int TicksPerFrame = 60;
    private const int TradeTimeoutTicks = 20000;

    [Test]
    public static IEnumerator SellingGoodsIsLoadedShippedAndPaidFor()
    {
        using var scene = new TradeScene(Find.CurrentMap);

        var steelHeldByTraderBefore = scene.SteelHeldByTrader;
        var steelOnMapBefore = scene.CountOnMap(scene.SteelDef);
        var silverOnMapBefore = scene.CountOnMap(scene.SilverDef);

        var silverToReceive = scene.OpenCommsAndSellSteel(SteelToSell);
        if (silverToReceive <= 0)
        {
            yield break;
        }
        var tradeAgreement = scene.TradeAgreement!;
        var quest = TradeScene.FindTradeQuest();
        Expect.IsNotNull(quest, "Accepting the trade didn't generate the shuttle quest");
        Expect.IsNotNull(tradeAgreement.toTraderTransportShip, "No shuttle for the sold goods");
        Expect.IsNotNull(tradeAgreement.tradeDeliveryMethod, "No delivery arranged for payment");
        if (quest == null)
        {
            yield break;
        }

        var ticked = 0;
        while (!scene.ShuttleHasLanded && ticked < TradeTimeoutTicks)
        {
            Tick(TicksPerFrame);
            ticked += TicksPerFrame;
            yield return null;
        }
        Expect.IsTrue(scene.ShuttleHasLanded, "Trade shuttle never landed");

        // Everything else follows from the colonists doing their work.
        while (quest.State == QuestState.Ongoing && ticked < TradeTimeoutTicks)
        {
            Tick(TicksPerFrame);
            ticked += TicksPerFrame;
            yield return null;
        }

        Expect.AreEqual(QuestState.EndedSuccess, quest.State, "Trade quest didn't succeed");
        Expect.IsNull(
            scene.TradeShip.GetData().activeTradeAgreement,
            "Trade agreement is still active after delivery"
        );
        Expect.AreEqual(
            steelHeldByTraderBefore + SteelToSell,
            scene.SteelHeldByTrader,
            "Trader didn't receive the sold steel"
        );

        Expect.AreEqual(
            steelOnMapBefore - SteelToSell,
            scene.CountOnMap(scene.SteelDef),
            "Sold steel is still in the colony"
        );
        Expect.AreEqual(
            silverOnMapBefore + silverToReceive,
            scene.CountOnMap(scene.SilverDef),
            "Payment didn't arrive"
        );
    }

    private static void Tick(int ticks)
    {
        for (var i = 0; i < ticks; i++)
        {
            Find.TickManager.DoSingleTick();
        }
    }

    // A colony with a powered trade beacon, steel to sell, a home area to load from and two
    // colonists, plus a bulk-goods trader in orbit. Disposing restores the settings the trade
    // depends on and clears everything the trade left on the map.
    private sealed class TradeScene : IDisposable
    {
        private readonly bool previousThreshold = Settings._useMinimumTradeThreshold;
        private readonly bool previousDropPods = Settings._deliverItemsViaDropPod;
        private readonly bool previousPauseTimer = Settings._activeTradePausesDepartureTimer;
        private readonly List<Thing> spawned = [];

        internal Map Map { get; }
        internal ThingDef SteelDef { get; } = RimWorld.ThingDefOf.Steel;
        internal ThingDef SilverDef { get; } = RimWorld.ThingDefOf.Silver;
        internal TradeShip TradeShip { get; }
        internal TradeAgreement? TradeAgreement { get; private set; }

        internal int CountOnMap(ThingDef def) =>
            Map.listerThings.ThingsOfDef(def).Sum(t => t.stackCount);

        internal int SteelHeldByTrader =>
            TradeShip.Goods.Where(t => t.def == SteelDef).Sum(t => t.stackCount);

        internal bool ShuttleHasLanded =>
            TradeAgreement?.toTraderTransportShip?.shipThing.Spawned ?? false;

        internal TradeScene(Map map)
        {
            Map = map;
            Settings._useMinimumTradeThreshold = false;
            Settings._deliverItemsViaDropPod = false;
            Settings._activeTradePausesDepartureTimer = false;

            var center = map.Center;
            foreach (var cell in CellRect.CenteredOn(center, 12))
            {
                map.areaManager.Home[cell] = true;
            }

            var beacon = GenSpawn.Spawn(
                ThingMaker.MakeThing(RimWorld.ThingDefOf.OrbitalTradeBeacon),
                center,
                map
            );
            beacon.SetFaction(Faction.OfPlayer);
            beacon.TryGetComp<CompPowerTrader>().PowerOn = true;
            spawned.Add(beacon);

            var steel = ThingMaker.MakeThing(SteelDef);
            steel.stackCount = SteelToSell;
            _ = GenPlace.TryPlaceThing(
                steel,
                center + new IntVec3(2, 0, 0),
                map,
                ThingPlaceMode.Direct
            );
            spawned.Add(steel);

            for (var i = 0; i < 2; i++)
            {
                var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);
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

        // Opens comms through the real trade dialog, sells steel and accepts. Returns the silver
        // the colony is owed, or 0 after recording why the trade couldn't be made.
        internal int OpenCommsAndSellSteel(int count)
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
            var steelTradeable = deal.AllTradeables.FirstOrDefault(t =>
                t.ThingDef == SteelDef && t.CountHeldBy(Transactor.Colony) > 0
            );
            Expect.IsNotNull(steelTradeable, "Colony steel isn't offered for sale");
            if (steelTradeable == null)
            {
                return 0;
            }
            steelTradeable.ForceTo(-count);
            deal.UpdateCurrencyCount();
            var silverToReceive = deal.CurrencyTradeable.CountToTransfer;
            Expect.GreaterThan(silverToReceive, 0, "The sale doesn't pay any silver");

            Expect.IsTrue(deal.TryExecute(out var actuallyTraded), "Trade was rejected");
            Expect.IsTrue(actuallyTraded, "Trade didn't execute");
            dialog.Close(doCloseSound: false);
            return silverToReceive;
        }

        internal static Quest? FindTradeQuest() =>
            Find.QuestManager.QuestsListForReading.FirstOrDefault(q =>
                q.root == QuestScriptDefOf.ROT_TradeShipTransportShip
            );

        public void Dispose()
        {
            Settings._useMinimumTradeThreshold = previousThreshold;
            Settings._deliverItemsViaDropPod = previousDropPods;
            Settings._activeTradePausesDepartureTimer = previousPauseTimer;
            TradeShipData.tradeAgreementForQuest = null;

            if (!TradeShip.Departed)
            {
                TradeShip.Depart();
            }
            foreach (var quest in Find.QuestManager.QuestsListForReading.ToList())
            {
                if (
                    quest.root == QuestScriptDefOf.ROT_TradeShipTransportShip
                    && quest.State == QuestState.Ongoing
                )
                {
                    quest.End(QuestEndOutcome.Unknown, sendLetter: false);
                }
            }
            foreach (var thing in Map.listerThings.AllThings.ToList())
            {
                if (!thing.Destroyed && thing.def.category == ThingCategory.Item)
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
    }
}
