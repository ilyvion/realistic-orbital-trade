using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// TradeAgreement state as it changes while a sale is under way: what the trader counts as loaded,
// and what losing a sold stack does to the deal.
[TestFixture(TestType.Playing)]
internal sealed class TradeAgreementInFlightTests
{
    private const int SteelOnMap = 400;

    [Test]
    public static IEnumerator LoadedOnlyTradeablesCountsWhatIsAboardTheShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, SteelOnMap);
        yield return scene.WaitForPartialLoad();
        var loaded = scene.LoadedInShuttles(steel);
        var tradeables = scene.TradeAgreement!.LoadedOnlyTradeables();

        var steelRow = tradeables.First(t => t.ThingDef == steel);
        Expect.AreEqual(-loaded, steelRow.CountToTransfer);
        Expect.AreEqual(
            scene.TradeAgreement.AllTradeables.Count,
            tradeables.Count,
            "The loaded-only view changed which rows there are"
        );
    }

    [Test]
    public static IEnumerator DestroyingASoldStackNothingElseCoversRequiresRenegotiation()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, SteelOnMap);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var lost = agreement.thingsSoldToTrader.Last();
        var lostCount = lost.Count;
        var lostThing = lost.thing;

        lostThing.Destroy();

        Expect.IsTrue(
            agreement.RenegotiationRequired,
            "Losing sold goods should force a renegotiation"
        );
        Expect.AreEqual(1, agreement.lostSoldItems.Count);
        Expect.AreEqual(lostCount, agreement.lostSoldItems[0].count);
        Expect.IsFalse(agreement.thingsSoldToTrader.Any(t => t.thing == lostThing));
        Expect.IsFalse(
            agreement.ToTraderTransportShips.All(ship => ship.ShuttleComp.AllRequiredThingsLoaded),
            "A shuttle owed goods that no longer exist was cleared to leave"
        );
    }

    [Test]
    public static IEnumerator DestroyingSomethingUnrelatedChangesNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);
        scene.PlaceOnMap(RimWorld.ThingDefOf.Gold, 10);

        yield return scene.SellAndWaitForLanding(steel, SteelOnMap);
        var agreement = scene.TradeAgreement!;
        var soldBefore = agreement.thingsSoldToTrader.Count;

        Find.CurrentMap.listerThings.ThingsOfDef(RimWorld.ThingDefOf.Gold).First().Destroy();

        Expect.IsFalse(agreement.RenegotiationRequired);
        Expect.IsEmpty(agreement.lostSoldItems);
        Expect.AreEqual(soldBefore, agreement.thingsSoldToTrader.Count);
    }

    [Test]
    public static IEnumerator DestroyingASoldStackWithSpareStockIsSubstitutedWithoutALoss()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, 300);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;

        agreement.thingsSoldToTrader.First().thing.Destroy();

        Expect.IsFalse(agreement.RenegotiationRequired, "A covered loss forced a renegotiation");
        Expect.IsEmpty(agreement.lostSoldItems);
        Expect.AreEqual(300, agreement.thingsSoldToTrader.Sum(t => t.Count));
        Expect.IsFalse(agreement.thingsSoldToTrader.Any(t => t.thing.Destroyed));
    }

    [Test]
    public static IEnumerator DestroyingASoldPawnRemovesItFromTheShuttlesRequirements()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var pawn = scene.SpawnPawn();
        agreement.pawnsSoldToTrader.Add(pawn);
        Expect.IsTrue(agreement.toTraderTransportShip!.ShuttleComp.requiredPawns.Contains(pawn));

        pawn.Destroy();

        Expect.IsEmpty(agreement.pawnsSoldToTrader);
        Expect.IsEmpty(agreement.toTraderTransportShip.ShuttleComp.requiredPawns);
        Expect.IsTrue(agreement.RenegotiationRequired);
        Expect.AreEqual(1, agreement.lostSoldItems.Count);
    }

    [Test]
    public static IEnumerator DestroyingTwoStacksOfTheSameDefMergesTheirLossRecords()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, SteelOnMap);
        scene.StopColonistsHauling();
        var agreement = scene.TradeAgreement!;
        var first = agreement.thingsSoldToTrader[0];
        var second = agreement.thingsSoldToTrader[1];
        var expectedLost = first.Count + second.Count;

        first.thing.Destroy();
        second.thing.Destroy();

        Expect.AreEqual(1, agreement.lostSoldItems.Count);
        Expect.AreEqual(expectedLost, agreement.lostSoldItems[0].count);
    }

    [Test]
    public static IEnumerator PruningWhenNothingWasLostChangesNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        var agreement = scene.TradeAgreement!;
        var soldBefore = agreement.thingsSoldToTrader.Count;

        agreement.PruneDestroyedSoldItems();

        Expect.IsFalse(agreement.RenegotiationRequired);
        Expect.AreEqual(soldBefore, agreement.thingsSoldToTrader.Count);
    }

    [Test]
    public static IEnumerator PruningRealiasesASingleShuttlesPawnsToTheSoldList()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 100);
        var agreement = scene.TradeAgreement!;
        agreement.toTraderTransportShip!.ShuttleComp.requiredPawns = [];

        agreement.PruneDestroyedSoldItems();

        Expect.IsTrue(
            ReferenceEquals(
                agreement.toTraderTransportShip.ShuttleComp.requiredPawns,
                agreement.pawnsSoldToTrader
            )
        );
    }

    [Test]
    public static IEnumerator PruningLeavesEachOfSeveralShuttlesItsOwnPawnList()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.LimitShuttleCapacity(40f);
        scene.PlaceOnMap(steel, 120);

        yield return scene.SellAndWaitForLanding(steel, 120);
        var agreement = scene.TradeAgreement!;
        var ownLists = agreement
            .ToTraderTransportShips.Select(ship => ship.ShuttleComp.requiredPawns)
            .ToList();

        agreement.PruneDestroyedSoldItems();

        Expect.GreaterThan(ownLists.Count, 1, "The sale wasn't split across shuttles");
        Expect.IsFalse(
            agreement.ToTraderTransportShips.Any(ship =>
                ReferenceEquals(ship.ShuttleComp.requiredPawns, agreement.pawnsSoldToTrader)
            ),
            "A shuttle's pawns were aliased to the sold list"
        );
    }

    // Sells and waits for the shuttles to fly off, then puts them in the state saving leaves a
    // departed shuttle in: TransportShip.ExposeData nulls a destroyed shipThing.
    private static IEnumerator SoldAndDepartedAsAfterASave(TradeScene scene)
    {
        var steel = RimWorld.ThingDefOf.Steel;
        scene.PlaceOnMap(steel, 100);
        yield return scene.SellAndWaitForLanding(steel, 100);
        yield return scene.WaitForShuttlesToLeave();
        foreach (var ship in scene.ToTraderShuttles)
        {
            ship.shipThing = null;
        }
    }

    [Test]
    public static IEnumerator PruningAfterTheShuttleDepartedDoesNotRequireRenegotiation()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SoldAndDepartedAsAfterASave(scene);
        var agreement = scene.TradeAgreement!;
        Expect.IsTrue(agreement.ToTraderShuttlesDeparted, "The shuttle never departed");
        agreement.thingsSoldToTrader.Add(new ThingCountClass(null, 10));
        var lettersBefore = Find.LetterStack.LettersListForReading.Count;

        agreement.PruneDestroyedSoldItems();

        Expect.IsFalse(agreement.RenegotiationRequired, "Delivered goods forced a renegotiation");
        Expect.AreEqual(lettersBefore, Find.LetterStack.LettersListForReading.Count);
        Expect.IsFalse(agreement.thingsSoldToTrader.Any(t => t.thing == null));
    }

    [Test]
    public static IEnumerator DestroyingDeliveredGoodsAfterTheShuttleDepartedChangesNothing()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SoldAndDepartedAsAfterASave(scene);
        var agreement = scene.TradeAgreement!;
        var lettersBefore = Find.LetterStack.LettersListForReading.Count;

        agreement.thingsSoldToTrader.First().thing.Destroy();

        Expect.IsFalse(agreement.RenegotiationRequired, "Delivered goods forced a renegotiation");
        Expect.IsEmpty(agreement.lostSoldItems);
        Expect.AreEqual(lettersBefore, Find.LetterStack.LettersListForReading.Count);
    }

    [Test]
    public static IEnumerator UnsoldCountIsWhatIsLeftOfAStackAfterWhatWasSoldFromIt()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 100);

        yield return scene.SellAndWaitForLanding(steel, 30);
        var agreement = scene.TradeAgreement!;
        var sold = agreement.thingsSoldToTrader[0];

        Expect.AreEqual(sold.thing.stackCount - sold.Count, agreement.UnsoldCount(sold.thing));
    }
}
