using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// What applying a renegotiated deal does to the real trade state: sold goods and pawns, shuttle
// requirements and reservations, and what the trader can still pay.
[TestFixture(TestType.Playing)]
internal sealed class ApplyRenegotiationTests
{
    private const int SteelOnMap = 400;

    private static readonly FieldInfo TraderThingsField = typeof(TradeShip).GetField(
        "things",
        BindingFlags.NonPublic | BindingFlags.Instance
    );

    private static Tradeable PawnTradeable(Pawn pawn, Transactor owner, int count)
    {
        var tradeable = new Tradeable();
        tradeable.AddThing(pawn, owner);
        tradeable.ForceTo(count);
        return tradeable;
    }

    private static int RequiredSteel(TradeAgreement agreement) =>
        agreement
            .ToTraderTransportShips.SelectMany(ship => ship.ShuttleComp.requiredItems)
            .Where(i => i.ThingDef == RimWorld.ThingDefOf.Steel)
            .Sum(i => i.Count);

    [Test]
    public static IEnumerator SellingLessTrimsAcrossStacksAndKeepsTheRecordsConsistent()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, 300);
        scene.StopColonistsHauling();
        scene.RenegotiateSale(75);

        var agreement = scene.TradeAgreement!;
        Expect.AreEqual(75, agreement.thingsSoldToTrader.Sum(t => t.Count));
        Expect.AreEqual(75, RequiredSteel(agreement));
        Expect.AreEqual(75, agreement.reservedTraderThings.Sum(t => t.Count));
        Expect.IsFalse(agreement.RenegotiationRequired);
    }

    [Test]
    public static IEnumerator RenegotiatingForMoreSilverThanTheTraderHasPaysWhatIsLeft()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, 100);
        var silverBefore = scene.CountOnMap(scene.SilverDef);
        var traderSilver = scene.TradeShip.Goods.Where(t => t.def == scene.SilverDef).ToList();
        foreach (var silver in traderSilver)
        {
            silver.stackCount = Math.Min(silver.stackCount, 10);
        }
        scene.RenegotiateSale(300);
        yield return scene.WaitForQuestToEnd();

        Expect.AreEqual(QuestState.EndedSuccess, scene.CurrentSale!.Quest!.State);
        Expect.GreaterThan(scene.CountOnMap(scene.SilverDef), silverBefore);
    }

    [Test]
    public static IEnumerator SellingAnExtraPawnAddsItToTheSoldPawns()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var pawn = scene.SpawnPawn();

        var changed = agreement.ApplyRenegotiation(
            [],
            [PawnTradeable(pawn, Transactor.Colony, -1)]
        );

        Expect.IsTrue(changed);
        Expect.IsTrue(agreement.pawnsSoldToTrader.Contains(pawn));
        Expect.IsTrue(agreement.toTraderTransportShip!.ShuttleComp.requiredPawns.Contains(pawn));
    }

    [Test]
    public static IEnumerator SellingAPawnAcrossSeveralShuttlesGivesItToTheEmptiestOne()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.LimitShuttleCapacity(40f);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var pawn = scene.SpawnPawn();
        var emptiest = agreement
            .ToTraderTransportShips.OrderByDescending(agreement.FreeMass)
            .First();

        _ = agreement.ApplyRenegotiation([], [PawnTradeable(pawn, Transactor.Colony, -1)]);

        Expect.IsTrue(emptiest.ShuttleComp.requiredPawns.Contains(pawn));
        Expect.AreEqual(
            1,
            agreement.ToTraderTransportShips.Count(ship =>
                ship.ShuttleComp.requiredPawns.Contains(pawn)
            )
        );
    }

    [Test]
    public static IEnumerator UnsellingAPawnNotYetLoadedDropsItFromTheRequirements()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var pawn = scene.SpawnPawn();
        agreement.pawnsSoldToTrader.Add(pawn);

        _ = agreement.ApplyRenegotiation(
            [PawnTradeable(pawn, Transactor.Colony, -1)],
            [PawnTradeable(pawn, Transactor.Colony, 0)]
        );

        Expect.IsFalse(agreement.pawnsSoldToTrader.Contains(pawn));
        Expect.IsFalse(agreement.toTraderTransportShip!.ShuttleComp.requiredPawns.Contains(pawn));
    }

    [Test]
    public static IEnumerator UnsellingAPawnAlreadyAboardDropsItBackOut()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var pawn = scene.SpawnPawn();
        agreement.pawnsSoldToTrader.Add(pawn);
        var container = agreement.toTraderTransportShip!.TransporterComp.innerContainer;
        pawn.DeSpawn();
        _ = container.TryAdd(pawn);
        Expect.IsTrue(container.Contains(pawn), "The pawn didn't board the shuttle");

        _ = agreement.ApplyRenegotiation(
            [PawnTradeable(pawn, Transactor.Colony, -1)],
            [PawnTradeable(pawn, Transactor.Colony, 0)]
        );

        Expect.IsFalse(container.Contains(pawn), "The pawn stayed aboard");
        Expect.IsTrue(pawn.Spawned, "The pawn wasn't put back on the map");
    }

    [Test]
    public static IEnumerator BuyingAPawnDuringARenegotiationDeliversItToThePlayer()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        var agreement = scene.TradeAgreement!;
        var pawn = PawnGenerator.GeneratePawn(PawnKindDefOf.Colonist);
        _ = ((ThingOwner)TraderThingsField.GetValue(agreement.tradeShip)).TryAdd(pawn);

        _ = agreement.ApplyRenegotiation([], [PawnTradeable(pawn, Transactor.Trader, 1)]);

        Expect.IsTrue(agreement.tradeDeliveryMethod!.Things.Contains(pawn));
        Expect.AreEqual(Faction.OfPlayer, pawn.Faction, "The bought pawn didn't join the player");

        _ = agreement.tradeDeliveryMethod.Things.Remove(pawn);
        pawn.Destroy();
    }

    [Test]
    public static void SellingMoreThanIsUnsoldTakesWhatIsThereAndReportsTheRest()
    {
        var first = new Pawn { stackCount = 100 };
        var second = new Pawn { stackCount = 100 };

        var (allocation, shortfall) = TradeAgreement.AllocateUnsoldStock(
            [(first, 30), (second, 10)],
            50
        );

        Expect.AreEqual(2, allocation.Count);
        Expect.AreEqual(30, allocation[0].count);
        Expect.AreEqual(10, allocation[1].count);
        Expect.AreEqual(10, shortfall);
    }

    [Test]
    public static void SellingLessThanIsUnsoldStopsOnceCovered()
    {
        var first = new Pawn { stackCount = 100 };
        var second = new Pawn { stackCount = 100 };

        var (allocation, shortfall) = TradeAgreement.AllocateUnsoldStock(
            [(first, 30), (second, 10)],
            20
        );

        Expect.AreEqual(1, allocation.Count);
        Expect.AreEqual(20, allocation[0].count);
        Expect.AreEqual(0, shortfall);
    }

    [Test]
    public static void FullySoldStacksAreSkipped()
    {
        var first = new Pawn { stackCount = 100 };
        var second = new Pawn { stackCount = 100 };

        var (allocation, shortfall) = TradeAgreement.AllocateUnsoldStock(
            [(first, 0), (second, 10)],
            5
        );

        Expect.AreEqual(1, allocation.Count);
        Expect.IsTrue(ReferenceEquals(second, allocation[0].thing));
        Expect.AreEqual(0, shortfall);
    }
}
