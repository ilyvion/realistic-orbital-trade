using DevTools.Testing;
using RealisticOrbitalTrade.Core;
using RealisticOrbitalTrade.Quests;
using RealisticOrbitalTrade.ShipJobs;

namespace RealisticOrbitalTrade.Tests;

// The quest parts that move goods and pawns between the colony, the shuttles and the trader, the
// cancel-loading job's hand-back of sold goods, and the quest's own runnability check.
[TestFixture(TestType.Playing)]
internal sealed class QuestPartTests
{
    private const string Tag = "ROT.Test.Signal";
    private const int SteelToSell = 40;

    private static readonly Signal Right = new(Tag);
    private static readonly Signal Wrong = new("ROT.Test.OtherSignal");

    private static IEnumerator Landed(TradeScene scene)
    {
        var steel = RimWorld.ThingDefOf.Steel;
        scene.PlaceOnMap(steel, SteelToSell);
        scene.StopColonistsHauling();
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
    }

    private static Pawn MakeStrangerPawn() =>
        PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist, Faction.OfPlayer);

    private static void Discard(Pawn pawn)
    {
        if (Find.WorldPawns.Contains(pawn))
        {
            Find.WorldPawns.RemovePawn(pawn);
        }
    }

    [Test]
    public static IEnumerator ExtendPartRaisesAVeryShortDepartureOnItsSignal()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var part = new QuestPart_ExtendTradeShipDepartureIfVeryShort(Tag, scene.TradeShip);
        scene.TradeShip.ticksUntilDeparture = 2499;

        part.Notify_QuestSignalReceived(Right);

        Expect.AreEqual(2500, scene.TradeShip.ticksUntilDeparture);
        yield break;
    }

    [Test]
    public static IEnumerator ExtendPartLeavesLongerDeparturesAndOtherSignalsAlone()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var part = new QuestPart_ExtendTradeShipDepartureIfVeryShort(Tag, scene.TradeShip);

        scene.TradeShip.ticksUntilDeparture = 100000;
        part.Notify_QuestSignalReceived(Right);
        Expect.AreEqual(100000, scene.TradeShip.ticksUntilDeparture);

        scene.TradeShip.ticksUntilDeparture = 100;
        part.Notify_QuestSignalReceived(Wrong);
        Expect.AreEqual(100, scene.TradeShip.ticksUntilDeparture);
        yield break;
    }

    [Test]
    public static IEnumerator TransferPartGivesTheTraderWhatIsAboardAndRemembersSoldPrisoners()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        agreement.reservedTraderThings.Clear();
        var shuttle = scene.ToTraderShuttles.First();
        var pawn = MakeStrangerPawn();
        var silver = ThingMaker.MakeThing(RimWorld.ThingDefOf.Silver);
        silver.stackCount = 30;
        var silverBefore = scene.HeldByTrader(RimWorld.ThingDefOf.Silver);
        agreement.pawnsSoldToTrader.Add(pawn);
        _ = shuttle.TransporterComp.innerContainer.TryAdd(pawn);
        _ = shuttle.TransporterComp.innerContainer.TryAdd(silver);
        var part = new QuestPart_TransferItemsToTrader
        {
            inSignal = Tag,
            tradeAgreement = agreement,
            toTraderTransportShip = shuttle,
        };

        part.Notify_QuestSignalReceived(Right);

        Expect.IsTrue(agreement.tradeShip.soldPrisoners.Contains(pawn));
        Expect.IsTrue(agreement.tradeShip.things.Contains(pawn));
        Expect.AreEqual(silverBefore + 30, scene.HeldByTrader(RimWorld.ThingDefOf.Silver));
        Expect.IsEmpty(shuttle.TransporterComp.innerContainer);
        Expect.IsFalse(Find.WorldPawns.Contains(pawn), "Pawn is owned by both trader and world");

        _ = agreement.pawnsSoldToTrader.Remove(pawn);
        _ = agreement.tradeShip.soldPrisoners.Remove(pawn);
        _ = agreement.tradeShip.things.Remove(pawn);
        Discard(pawn);
    }

    [Test]
    public static IEnumerator TransferPartDropsAWorldPawnRegistrationAfterTheTransfer()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        agreement.reservedTraderThings.Clear();
        var shuttle = scene.ToTraderShuttles.First();
        var pawn = MakeStrangerPawn();
        Find.WorldPawns.PassToWorld(pawn);
        _ = shuttle.TransporterComp.innerContainer.TryAdd(pawn);
        var part = new QuestPart_TransferItemsToTrader
        {
            inSignal = Tag,
            tradeAgreement = agreement,
            toTraderTransportShip = shuttle,
        };

        part.Notify_QuestSignalReceived(Right);

        Expect.IsTrue(agreement.tradeShip.things.Contains(pawn));
        Expect.IsFalse(Find.WorldPawns.Contains(pawn), "Pawn is owned by both trader and world");

        _ = agreement.tradeShip.soldPrisoners.Remove(pawn);
        _ = agreement.tradeShip.things.Remove(pawn);
        Discard(pawn);
    }

    [Test]
    public static IEnumerator TransferPartIgnoresOtherSignals()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement;
        var shuttle = scene.ToTraderShuttles.First();
        var silver = ThingMaker.MakeThing(RimWorld.ThingDefOf.Silver);
        _ = shuttle.TransporterComp.innerContainer.TryAdd(silver);
        var part = new QuestPart_TransferItemsToTrader
        {
            inSignal = Tag,
            tradeAgreement = agreement,
            toTraderTransportShip = shuttle,
        };

        part.Notify_QuestSignalReceived(Wrong);

        Expect.IsTrue(shuttle.TransporterComp.innerContainer.Contains(silver));
        _ = shuttle.TransporterComp.innerContainer.Remove(silver);
        silver.Destroy();
    }

    [Test]
    public static IEnumerator ReturnPartMovesBoughtGoodsBackAndDropsWorldPawnRegistration()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        var delivery = agreement.tradeDeliveryMethod!.Things;
        var pawn = MakeStrangerPawn();
        Find.WorldPawns.PassToWorld(pawn);
        _ = delivery.TryAdd(pawn);
        var deliveredSilver = delivery
            .Where(t => t.def == RimWorld.ThingDefOf.Silver)
            .Sum(t => t.stackCount);
        var silverBefore = scene.HeldByTrader(RimWorld.ThingDefOf.Silver);
        var part = new QuestPart_ReturnBoughtItemsToTradeShip
        {
            inSignal = Tag,
            tradeAgreement = agreement,
        };

        part.Notify_QuestSignalReceived(Right);

        Expect.IsEmpty(delivery);
        Expect.IsTrue(agreement.tradeShip.things.Contains(pawn));
        Expect.IsFalse(Find.WorldPawns.Contains(pawn), "Pawn is owned by both trader and world");
        Expect.AreEqual(
            silverBefore + deliveredSilver,
            scene.HeldByTrader(RimWorld.ThingDefOf.Silver)
        );

        _ = agreement.tradeShip.things.Remove(pawn);
        Discard(pawn);
    }

    [Test]
    public static IEnumerator BoughtAnimalsSurviveTheWorldPawnCleanupWhileAwaitingDelivery()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        var delivery = agreement.tradeDeliveryMethod!.Things;
        var animal = PawnGenerator.GeneratePawn(PawnKindDefOf.Muffalo, Faction.OfPlayer);
        Find.WorldPawns.PassToWorld(animal);
        _ = delivery.TryAdd(animal);

        Find.WorldPawns.gc.RunGC();

        Expect.IsFalse(animal.Discarded, "The bought animal was discarded before delivery");
        _ = delivery.Remove(animal);
        Discard(animal);
    }

    // Saving nulls the shipThing of a to-player shuttle that has already delivered and flown off.
    [Test]
    public static IEnumerator ADepartedDeliveryShuttleReservesNothing()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var agreement = new TradeAgreement(scene.TradeShip, scene.SpawnPawn(), false, false)
        {
            tradeDeliveryMethod = new TradeDeliveryMethod_TransportShip(new TransportShip()),
        };
        var part = new QuestPart_ReserveBoughtPawns { tradeAgreement = agreement };

        Expect.IsEmpty(agreement.tradeDeliveryMethod.Things);
        Expect.IsFalse(part.QuestPartReserves(MakeStrangerPawn()));
        yield break;
    }

    private static int IncomingPods(Map map) =>
        map.listerThings.ThingsOfDef(RimWorld.ThingDefOf.DropPodIncoming).Count;

    private static IEnumerator DropTwoThingsAndCountPods(bool sameServer, Action<int> report)
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        // The sale's payment waits here too; only the two things added below are dropped.
        foreach (var payment in agreement.thingsSoldToPlayer.ToList())
        {
            _ = agreement.thingsSoldToPlayer.Remove(payment);
            payment.Destroy();
        }
        _ = agreement.thingsSoldToPlayer.TryAdd(MakeItem(RimWorld.ThingDefOf.Steel));
        _ = agreement.thingsSoldToPlayer.TryAdd(MakeItem(RimWorld.ThingDefOf.WoodLog));
        var part = new QuestPart_TradeDropPods
        {
            inSignal = Tag,
            tradeAgreement = agreement,
            dropSpot = scene.Map.Center + new IntVec3(6, 0, 6),
            dropAllInSamePod = sameServer,
        };
        var podsBefore = IncomingPods(scene.Map);

        part.Notify_QuestSignalReceived(Right);
        report(IncomingPods(scene.Map) - podsBefore);
        Expect.IsEmpty(agreement.thingsSoldToPlayer);

        yield return scene.TickFor(600);

        var leftovers = scene
            .Map.listerThings.AllThings.Where(t =>
                t.def == RimWorld.ThingDefOf.DropPodIncoming
                || t.def == RimWorld.ThingDefOf.ActiveDropPod
            )
            .ToList();
        Expect.IsEmpty(leftovers, "Drop pods are still on the map");
        foreach (var leftover in leftovers)
        {
            leftover.Destroy();
        }
    }

    private static Thing MakeItem(ThingDef def)
    {
        var thing = ThingMaker.MakeThing(def);
        thing.stackCount = 5;
        return thing;
    }

    [Test]
    public static IEnumerator DropPodsPartDropsEverythingInOnePodWhenGrouped()
    {
        var pods = -1;
        yield return DropTwoThingsAndCountPods(true, count => pods = count);
        Expect.AreEqual(1, pods);
    }

    [Test]
    public static IEnumerator DropPodsPartDropsSeparatePodsWhenNotGrouped()
    {
        var pods = -1;
        yield return DropTwoThingsAndCountPods(false, count => pods = count);
        Expect.AreEqual(2, pods);
    }

    [Test]
    public static IEnumerator DropPodsPartIgnoresOtherSignals()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);
        var agreement = scene.TradeAgreement!;
        var item = MakeItem(RimWorld.ThingDefOf.Steel);
        _ = agreement.thingsSoldToPlayer.TryAdd(item);
        var part = new QuestPart_TradeDropPods { inSignal = Tag, tradeAgreement = agreement };

        part.Notify_QuestSignalReceived(Wrong);

        Expect.IsTrue(agreement.thingsSoldToPlayer.Contains(item));
        _ = agreement.thingsSoldToPlayer.Remove(item);
        item.Destroy();
    }

    [Test]
    public static IEnumerator DropSoldItemsHandsBackSoldGoodsButKeepsOtherThings()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        var pawn = scene.SpawnPawn();
        var carried = MakeItem(steel);
        var stashed = MakeItem(steel);
        var other = MakeItem(RimWorld.ThingDefOf.WoodLog);
        _ = pawn.carryTracker.innerContainer.TryAdd(carried);
        _ = pawn.inventory.innerContainer.TryAdd(stashed);
        _ = pawn.inventory.innerContainer.TryAdd(other);

        ShipJob_CancelLoad.DropSoldItems(pawn, [steel]);

        Expect.IsNull(pawn.carryTracker.CarriedThing, "The pawn still carries sold goods");
        Expect.IsFalse(pawn.inventory.innerContainer.Contains(stashed));
        Expect.IsTrue(pawn.inventory.innerContainer.Contains(other), "A non-sold item was dropped");
        Expect.AreEqual(10, scene.CountOnMap(steel), "The sold goods aren't back on the map");
        yield break;
    }

    [Test]
    public static IEnumerator SoldDefsIsNullWithoutAnAgreement()
    {
        Expect.IsNull(ShipJob_CancelLoad.SoldDefs(null));
        yield break;
    }

    [Test]
    public static IEnumerator SoldDefsListsTheSoldKinds()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return Landed(scene);

        var defs = ShipJob_CancelLoad.SoldDefs(scene.TradeAgreement);

        Expect.IsNotNull(defs);
        Expect.IsTrue(defs!.Contains(RimWorld.ThingDefOf.Steel));
    }

    [Test]
    public static IEnumerator CancellingAPartlyLoadedTradeLeavesNothingProtectedOrCarried()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 400);
        yield return scene.SellAndWaitForLanding(steel, 400);
        yield return scene.WaitForPartialLoad();
        var agreement = scene.TradeAgreement;
        scene.CancelTradeFromShuttle();
        yield return scene.WaitForQuestToEnd();
        yield return scene.WaitForShuttlesToLeave();

        Expect.IsFalse(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Contains(agreement),
            "The cancelled agreement still protects the sold goods"
        );
        Expect.IsEmpty(
            Find.CurrentMap.mapPawns.FreeColonists.Where(p =>
                p.carryTracker.CarriedThing?.def == steel
                || p.inventory.innerContainer.Any(t => t.def == steel)
            ),
            "A colonist still holds sold goods"
        );
        scene.ExpectSaleCancelled();
    }

    [Test]
    public static IEnumerator QuestIsNotRunnableOnceCommsAreClosed()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var agreement = new TradeAgreement(scene.TradeShip, scene.SpawnPawn(), false, false);
        var node = new QuestNode_Root_TradeShipTransportShip();
        var slate = new RimWorld.QuestGen.Slate();
        slate.Set("tradeAgreement", agreement);

        scene.TradeShip.GetData().ticksUntilCommsClosed = -1;
        Expect.IsTrue(node.TestRun(slate));
        scene.TradeShip.GetData().ticksUntilCommsClosed = 500;
        Expect.IsTrue(node.TestRun(slate));
        scene.TradeShip.GetData().ticksUntilCommsClosed = 0;
        Expect.IsFalse(node.TestRun(slate));
        yield break;
    }

    [Test]
    public static IEnumerator PreTradePassesBoughtPawnsToTheWorld()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        var pawn = MakeStrangerPawn();
        var agreement = new TradeAgreement(scene.TradeShip, scene.SpawnPawn(), false, false);
        _ = agreement.thingsSoldToPlayer.TryAdd(pawn);

        QuestNode_Root_TradeShipTransportShip.PreTrade(agreement);

        Expect.IsTrue(Find.WorldPawns.Contains(pawn));
        _ = agreement.thingsSoldToPlayer.Remove(pawn);
        Discard(pawn);
        yield break;
    }
}
