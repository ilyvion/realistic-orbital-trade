using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// A sale split across several shuttles: which shuttle loses required cargo first, how the cargo is
// rebuilt and assigned, and when the shuttles are cleared to take off together.
[TestFixture(TestType.Playing)]
internal sealed class SplitSaleRequiredCargoTests
{
    private const float ShuttleCapacityKg = 20f;
    private const int SteelToSell = 120;

    private static readonly FieldInfo CheckingDepartureClearanceField =
        typeof(TradeAgreement).GetField(
            "checkingDepartureClearance",
            BindingFlags.NonPublic | BindingFlags.Instance
        );

    private static readonly MethodInfo SetRenegotiatingBecauseDeparting = typeof(Utils)
        .GetProperty(nameof(Utils.RenegotiatingBecauseDeparting))
        .GetSetMethod(nonPublic: true);

    private static IEnumerator SellSplitAcrossShuttles(TradeScene scene)
    {
        var steel = RimWorld.ThingDefOf.Steel;
        scene.LimitShuttleCapacity(ShuttleCapacityKg);
        scene.PlaceOnMap(steel, SteelToSell);
        scene.StopColonistsHauling();
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        Expect.GreaterThan(
            scene.TradeAgreement!.ToTraderTransportShips.Count(),
            2,
            "The sale wasn't split across at least three shuttles"
        );
    }

    private static int RequiredSteel(TransportShip ship) =>
        ship
            .ShuttleComp.requiredItems.Where(i => i.ThingDef == RimWorld.ThingDefOf.Steel)
            .Sum(i => i.Count);

    private static string RequiredSnapshot(TradeAgreement agreement) =>
        string.Join(
            "|",
            agreement.ToTraderTransportShips.Select(ship =>
                string.Join(
                    ",",
                    ship.ShuttleComp.requiredItems.Select(i => $"{i.ThingDef.defName}x{i.Count}")
                )
                + ";"
                + string.Join(
                    ",",
                    ship.shipThing.TryGetComp<CompTradeShuttle>()
                        .requiredSpecificItems.Select(i => $"{i.def.defName}x{i.count}")
                )
                + ";"
                + ship.ShuttleComp.requiredPawns.Count
            )
        );

    private static void ClearAllRequirements(TradeAgreement agreement)
    {
        foreach (var ship in agreement.ToTraderTransportShips)
        {
            ship.ShuttleComp.requiredItems.Clear();
            ship.shipThing.TryGetComp<CompTradeShuttle>().requiredSpecificItems.Clear();
            ship.ShuttleComp.requiredPawns.Clear();
        }
    }

    // B4

    // Colonists may have loaded some steel before hauling stopped, so the order is read off what
    // is actually aboard once a marker stack has been added to the first shuttle.
    private static (Thing marker, List<TransportShip> leastLoadedFirst) LoadMarkerOntoFirstShuttle(
        TradeAgreement agreement
    )
    {
        var marker = ThingMaker.MakeThing(RimWorld.ThingDefOf.Steel);
        marker.stackCount = 5;
        _ = agreement.ToTraderTransportShips.First().TransporterComp.innerContainer.TryAdd(marker);
        return (
            marker,
            [
                .. agreement.ToTraderTransportShips.OrderBy(ship =>
                    TradeAgreement.LoadedCountOf(ship, marker)
                ),
            ]
        );
    }

    [Test]
    public static IEnumerator RequiredCargoIsReducedOnTheShuttleWithTheLeastLoadedFirst()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        var (marker, order) = LoadMarkerOntoFirstShuttle(agreement);
        var before = order.Select(RequiredSteel).ToList();
        var left = 4;

        var reduced = agreement.ReduceRequiredCargo(marker, ref left);

        Expect.IsTrue(reduced);
        Expect.AreEqual(0, left);
        Expect.AreEqual(before[0] - 4, RequiredSteel(order[0]));
        Expect.AreEqual(before[1], RequiredSteel(order[1]));
        Expect.AreEqual(before[2], RequiredSteel(order[2]));
    }

    [Test]
    public static IEnumerator ReducingMoreThanOneShuttleRequiresSpillsToTheNextLeastLoaded()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        var (marker, order) = LoadMarkerOntoFirstShuttle(agreement);
        var before = order.Select(RequiredSteel).ToList();
        var left = before[0] + 3;

        _ = agreement.ReduceRequiredCargo(marker, ref left);

        Expect.AreEqual(0, left);
        Expect.AreEqual(0, RequiredSteel(order[0]));
        Expect.AreEqual(before[1] - 3, RequiredSteel(order[1]));
        Expect.AreEqual(before[2], RequiredSteel(order[2]));
    }

    [Test]
    public static IEnumerator ReducingSomethingNoShuttleRequiresChangesNothing()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        var snapshot = RequiredSnapshot(agreement);
        var gold = ThingMaker.MakeThing(RimWorld.ThingDefOf.Gold);
        var left = 4;

        var reduced = agreement.ReduceRequiredCargo(gold, ref left);

        Expect.IsFalse(reduced);
        Expect.AreEqual(4, left);
        Expect.AreEqual(snapshot, RequiredSnapshot(agreement));
    }

    [Test]
    public static IEnumerator LoadedCountCountsAMinifiedItemUnderItsInnerDef()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var ship = scene.TradeAgreement!.ToTraderTransportShips.First();
        var battery = ThingMaker.MakeThing(RimWorld.ThingDefOf.Battery);
        _ = ship.TransporterComp.innerContainer.TryAdd(battery.MakeMinified());
        var another = ThingMaker.MakeThing(RimWorld.ThingDefOf.Battery);

        Expect.AreEqual(1, TradeAgreement.LoadedCountOf(ship, another));
        Expect.AreEqual(
            0,
            TradeAgreement.LoadedCountOf(ship, ThingMaker.MakeThing(RimWorld.ThingDefOf.Gold))
        );
    }

    // B6

    [Test]
    public static IEnumerator RebuildingRequiredCargoWhenNothingWasLostChangesNothing()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        var before = RequiredSnapshot(agreement);

        Utils.RebuildRequiredCargo(agreement);
        var afterOnce = RequiredSnapshot(agreement);
        Utils.RebuildRequiredCargo(agreement);

        Expect.AreEqual(before, afterOnce);
        Expect.AreEqual(before, RequiredSnapshot(agreement), "Rebuilding twice doubled the cargo");
    }

    [Test]
    public static IEnumerator SharesBeyondTheLastShuttleLandOnIt()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        var ships = agreement.ToTraderTransportShips.ToList();
        ClearAllRequirements(agreement);
        var shares = ships.Count + 1;
        List<List<(int lineId, int count)>> plan = [];
        for (var i = 0; i < shares; i++)
        {
            plan.Add([(0, 1)]);
        }

        agreement.AssignRequiredCargo(plan);

        Expect.AreEqual(shares, ships.Sum(RequiredSteel));
        Expect.AreEqual(
            2,
            RequiredSteel(ships[^1]),
            "The extra share didn't land on the last shuttle"
        );
        Expect.AreEqual(1, RequiredSteel(ships[0]));
        Expect.IsTrue(agreement.thingsSoldToTrader[0].thing.def == steel);
    }

    // B7

    [Test]
    public static IEnumerator ShuttlesAreNotClearedWhileSomethingIsStillRequired()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;

        Expect.IsFalse(agreement.IsToTraderDepartureCleared());
        Expect.IsFalse(agreement.toTraderDepartureCleared);
    }

    [Test]
    public static IEnumerator ClearanceLatchesOnceAndSignalsOnce()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        agreement.toTraderShuttlesLoadedSignal = "RealisticOrbitalTrade.Tests.ShuttlesLoaded";
        var receiver = new CountingReceiver();
        Find.SignalManager.RegisterReceiver(receiver);
        try
        {
            ClearAllRequirements(agreement);

            Expect.IsTrue(agreement.IsToTraderDepartureCleared());
            Expect.IsTrue(agreement.toTraderDepartureCleared);
            Expect.IsTrue(agreement.IsToTraderDepartureCleared());

            Expect.AreEqual(1, receiver.Received);
        }
        finally
        {
            Find.SignalManager.DeregisterReceiver(receiver);
        }
    }

    [Test]
    public static IEnumerator ARequiredRenegotiationBlocksDepartureEvenWhenEverythingIsLoaded()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        ClearAllRequirements(agreement);
        agreement.RenegotiationRequired = true;

        Expect.IsFalse(
            agreement.ToTraderTransportShips.Any(ship => ship.ShuttleComp.AllRequiredThingsLoaded)
        );
        Expect.IsFalse(agreement.IsToTraderDepartureCleared());
        Expect.IsFalse(agreement.toTraderDepartureCleared);
    }

    [Test]
    public static IEnumerator ADepartureRenegotiationBlocksOnlyItsOwnAgreement()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        ClearAllRequirements(agreement);

        Utils.BeginRenegotiation(new TradeAgreement());
        _ = SetRenegotiatingBecauseDeparting.Invoke(null, [true]);
        Expect.IsTrue(
            agreement.ToTraderTransportShips.All(ship => ship.ShuttleComp.AllRequiredThingsLoaded),
            "Another agreement's departure renegotiation blocked this one"
        );

        Utils.BeginRenegotiation(agreement);
        _ = SetRenegotiatingBecauseDeparting.Invoke(null, [true]);
        Expect.IsFalse(
            agreement.ToTraderTransportShips.Any(ship => ship.ShuttleComp.AllRequiredThingsLoaded)
        );
    }

    [Test]
    public static IEnumerator ADespawnedShuttleBlocksClearance()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        ClearAllRequirements(agreement);
        var thing = agreement.ToTraderTransportShips.Last().shipThing;
        var position = thing.Position;
        var map = thing.Map;
        thing.DeSpawn();

        Expect.IsFalse(agreement.IsToTraderDepartureCleared());

        _ = GenSpawn.Spawn(thing, position, map);
    }

    [Test]
    public static IEnumerator ClearanceStaysLatchedWhenAShuttleLaterLeaves()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        ClearAllRequirements(agreement);
        Expect.IsTrue(agreement.IsToTraderDepartureCleared());
        var thing = agreement.ToTraderTransportShips.Last().shipThing;
        var position = thing.Position;
        var map = thing.Map;
        thing.DeSpawn();

        Expect.IsTrue(agreement.IsToTraderDepartureCleared());

        _ = GenSpawn.Spawn(thing, position, map);
    }

    [Test]
    public static IEnumerator AClearanceCheckAlreadyUnderWayDoesNotRecurse()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        yield return SellSplitAcrossShuttles(scene);
        var agreement = scene.TradeAgreement!;
        CheckingDepartureClearanceField.SetValue(agreement, true);

        Expect.IsTrue(agreement.IsToTraderDepartureCleared());
        Expect.IsFalse(agreement.toTraderDepartureCleared, "A re-entrant check latched clearance");

        CheckingDepartureClearanceField.SetValue(agreement, false);
    }

    private sealed class CountingReceiver : ISignalReceiver
    {
        internal int Received;

        public void Notify_SignalReceived(Signal signal)
        {
            if (signal.tag == "RealisticOrbitalTrade.Tests.ShuttlesLoaded")
            {
                Received++;
            }
        }
    }
}
