using DevTools.Testing;
using RealisticOrbitalTrade.Dialogs;
using RealisticOrbitalTrade.FindTrade.Patch;
using RealisticOrbitalTrade.Tests;
using Verse.AI;

namespace RealisticOrbitalTrade.FindTrade.Tests;

// Find / Trade's auto barter trades with trade ships without opening comms, and still goes through
// a trade shuttle: the barter pass starts a trade agreement for the deal it executes, the shuttle
// lands, colonists load the sold goods, and the payment arrives on the second shuttle. Needs Find /
// Trade active; see TestPlans/FindTrade.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive("memegoddess.findandtrade")]
internal sealed class FindTradeEndToEndTests
{
    private const int SteelToSell = 100;
    private const int WoodToSell = 100;
    private const int ClothToSell = 10;
    private const int SteelToBuy = 10;
    private const int SilverOnHand = 200;

    // Below one steel's mass, above ten cloth's.
    private const float ShuttleCapacityBelowSteelKg = 0.45f;

    [Test]
    public static IEnumerator JunkSoldByAutoBarterGoesThroughATradeShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(steel, SteelToSell, Barter)
        );

        void Barter()
        {
            findTrade.Barter();
            ExpectBarterLeftGoodsAlone(scene);
        }
    }

    [Test]
    public static IEnumerator JunkSoldByTheBarterJobGoesThroughATradeShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        var console = scene.SpawnBuilding(RimWorld.ThingDefOf.CommsConsole, new IntVec3(0, 0, -6));
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(steel, SteelToSell, BarterAtConsole())
        );

        // Started directly, so the pass doesn't depend on the colonist's work settings.
        IEnumerator BarterAtConsole()
        {
            scene.Negotiator.jobs.StartJob(
                JobMaker.MakeJob(DefDatabase<JobDef>.GetNamed("MGJob_Barter"), console),
                JobCondition.InterruptForced
            );
            yield return scene.TickUntil(() => findTrade.Traders.Contains(scene.TradeShip));
            Expect.IsTrue(
                findTrade.Traders.Contains(scene.TradeShip),
                "The barter job never bartered with the trade ship"
            );
            ExpectBarterLeftGoodsAlone(scene);
        }
    }

    [Test]
    public static void BarteringWithNothingToTradeStartsNoTradeAgreement()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;

        var ended = AgreementsEndedDuring(findTrade.Barter);

        Expect.IsEmpty(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements,
            "Bartering with nothing to trade started a trade agreement"
        );
        Expect.IsNull(
            TradeScene.FindTradeQuest(),
            "Bartering with nothing to trade generated a shuttle quest"
        );
        Expect.IsEmpty(ended, "Bartering with nothing to trade ended a trade agreement");
        Expect.IsTrue(
            findTrade.Traders.Contains(scene.TradeShip),
            "Find / Trade didn't record bartering with the trade ship"
        );
    }

    [Test]
    public static void BarteringWithTwoShipsStartsATradeAgreementOnlyForTheOneThatTrades()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        var otherTrader = scene.AddTrader();
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        // The first ship buys all the junk, which leaves the second nothing to trade.
        var ended = AgreementsEndedDuring(findTrade.Barter);

        var agreements = RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements;
        Expect.AreEqual(1, agreements.Count, "Bartering didn't start exactly one trade agreement");
        TradeShip[] ships = [scene.TradeShip, otherTrader];
        if (agreements.Count == 1)
        {
            var agreement = agreements[0];
            Expect.IsTrue(
                ships.Contains(agreement.tradeShip),
                "The trade agreement isn't for either ship in orbit"
            );
            foreach (var ship in ships)
            {
                var expected = ship == agreement.tradeShip ? agreement : null;
                Expect.ReferencesAreEqual(
                    expected,
                    ship.GetData().activeTradeAgreement,
                    $"{ship.TraderName} has the wrong trade agreement"
                );
            }
            Expect.AreEqual(
                SteelToSell,
                agreement.thingsSoldToTrader.Where(t => t.thing.def == steel).Sum(t => t.Count),
                "The trade agreement didn't sell all the junk"
            );
        }
        Expect.IsEmpty(ended, "Bartering ended a trade agreement");
        foreach (var ship in ships)
        {
            Expect.IsTrue(
                findTrade.Traders.Contains(ship),
                $"Find / Trade didn't record bartering with {ship.TraderName}"
            );
        }
    }

    [Test]
    public static void BarteringSkipsAShipInGraceTimeForTheRestOfItsStay()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);
        scene.TradeShip.GetData().ticksUntilCommsClosed = 0;

        findTrade.Barter();

        ExpectNothingTraded(scene, "in grace time");
        Expect.IsTrue(
            findTrade.Traders.Contains(scene.TradeShip),
            "The ship in grace time isn't skipped for the rest of its stay"
        );
    }

    [Test]
    public static IEnumerator BarteringWhileBlacklistedSkipsShipsUntilTheColonyIsInGoodStanding()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);
        var gameComponent = RealisticOrbitalTradeGameComponent.Current;
        var previousStanding = gameComponent.Standing;
        gameComponent.Standing = Standing.Blacklisted;
        try
        {
            findTrade.Barter();

            ExpectNothingTraded(scene, "while blacklisted");
            Expect.IsFalse(
                findTrade.Traders.Contains(scene.TradeShip),
                "Bartering while blacklisted skipped the ship for the rest of its stay"
            );
            var feeDialog = Find.WindowStack.WindowOfType<Dialog_PayBlacklistRemovalFee>();
            Expect.IsNull(feeDialog, "Bartering while blacklisted asked for the removal fee");
            if (feeDialog != null)
            {
                _ = Find.WindowStack.TryRemove(feeDialog, doCloseSound: false);
            }

            gameComponent.Standing = Standing.Good;
            yield return ExpectSaleThroughATradeShuttle(
                scene,
                scene.TradeAndWaitForLanding(steel, SteelToSell, findTrade.Barter)
            );
        }
        finally
        {
            gameComponent.Standing = previousStanding;
        }
    }

    [Test]
    public static IEnumerator BarteringDuringATradeSkipsTheShipUntilTheTradeIsDone()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var wood = RimWorld.ThingDefOf.WoodLog;
        // A remembered manual trade would make Find / Trade skip the ship for its own reasons.
        using var findTrade = new FindTradeScene(
            Find.CurrentMap,
            findTradeSettings: FindTradeScene.FindTradeSettings.Defaults with
            {
                RememberManualTrade = false,
            }
        );
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var manualAgreement = scene.TradeAgreement;
        scene.PlaceOnMap(wood, WoodToSell);
        findTrade.MarkJunk(wood);

        findTrade.Barter();

        Expect.AreEqual(
            1,
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Count,
            "Bartering during a trade started another trade agreement"
        );
        Expect.ReferencesAreEqual(
            manualAgreement,
            scene.TradeShip.GetData().activeTradeAgreement,
            "Bartering during a trade replaced the ship's trade agreement"
        );
        Expect.AreEqual(WoodToSell, scene.CountOnMap(wood), "Bartering during a trade sold junk");
        Expect.IsFalse(
            findTrade.Traders.Contains(scene.TradeShip),
            "Bartering during a trade skipped the ship for the rest of its stay"
        );

        yield return scene.WaitForQuestToEnd();
        scene.ExpectSaleCompleted();

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(wood, WoodToSell, findTrade.Barter)
        );
    }

    [Test]
    public static void BarteringWhileMakingAmendsSkipsShipsOnlyForThatPass()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);
        var gameComponent = RealisticOrbitalTradeGameComponent.Current;
        var previousAmendsQuest = gameComponent.AmendsQuest;
        gameComponent.AmendsQuest = Quest.MakeRaw();
        try
        {
            findTrade.Barter();
        }
        finally
        {
            gameComponent.AmendsQuest = previousAmendsQuest;
        }

        ExpectNothingTraded(scene, "while making amends");
        Expect.IsFalse(
            findTrade.Traders.Contains(scene.TradeShip),
            "Bartering while making amends skipped the ship for the rest of its stay"
        );
    }

    [Test]
    public static void BarteringTradesWithTheShipOutOfGraceTimeAndSkipsTheOneInIt()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        // The ship in grace time is first in orbit, so it would get the junk if it weren't skipped.
        var graceTimeShip = scene.TradeShip;
        graceTimeShip.GetData().ticksUntilCommsClosed = 0;
        var otherTrader = scene.AddTrader();
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        findTrade.Barter();

        var agreements = RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements;
        Expect.AreEqual(1, agreements.Count, "Bartering didn't start exactly one trade agreement");
        Expect.IsNull(
            graceTimeShip.GetData().activeTradeAgreement,
            "Bartering traded with the ship in grace time"
        );
        var agreement = otherTrader.GetData().activeTradeAgreement;
        Expect.IsNotNull(agreement, "Bartering didn't trade with the ship out of grace time");
        if (agreement != null)
        {
            Expect.AreEqual(
                SteelToSell,
                agreement.thingsSoldToTrader.Where(t => t.thing.def == steel).Sum(t => t.Count),
                "The trade agreement didn't sell all the junk"
            );
        }
        Expect.IsTrue(
            findTrade.Traders.Contains(graceTimeShip),
            "The ship in grace time isn't skipped for the rest of its stay"
        );
    }

    [Test]
    public static IEnumerator BarteringLeavesOutJunkTooHeavyForAShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var cloth = RimWorld.ThingDefOf.Cloth;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        LimitShuttleCapacityBelowSteel(scene);
        Expect.LessThan(
            ClothToSell * cloth.BaseMass,
            ShuttleCapacityBelowSteelKg,
            "The cloth doesn't fit in one shuttle"
        );
        scene.PlaceOnMap(steel, SteelToSell);
        scene.PlaceOnMap(cloth, ClothToSell);
        findTrade.MarkJunk(steel);
        findTrade.MarkJunk(cloth);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(cloth, ClothToSell, Barter)
        );
        Expect.AreEqual(
            SteelToSell,
            scene.CountOnMap(steel),
            "Bartering sold junk too heavy for a shuttle"
        );

        void Barter()
        {
            ExpectNoRejectionMessage(findTrade.Barter);
            ExpectBarterLeftGoodsAlone(scene);
        }
    }

    [Test]
    public static void BarteringWithOnlyJunkTooHeavyForAShuttleTradesNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        LimitShuttleCapacityBelowSteel(scene);
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        ExpectNoRejectionMessage(findTrade.Barter);

        ExpectNothingTraded(scene, "with only junk too heavy for a shuttle");
        Expect.IsTrue(
            findTrade.Traders.Contains(scene.TradeShip),
            "Find / Trade didn't record bartering with the trade ship"
        );
    }

    [Test]
    public static void BarteringLeavesOutPurchasesTooHeavyForAShuttle()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        LimitShuttleCapacityBelowSteel(scene);
        scene.PlaceOnMap(scene.SilverDef, SilverOnHand);
        findTrade.AddImportRule(steel, SteelToBuy);

        ExpectNoRejectionMessage(findTrade.Barter);

        Expect.IsEmpty(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements,
            "Bartering for goods too heavy for a shuttle started a trade agreement"
        );
        Expect.IsNull(
            TradeScene.FindTradeQuest(),
            "Bartering for goods too heavy for a shuttle generated a shuttle quest"
        );
        Expect.AreEqual(
            SilverOnHand,
            scene.CountOnMap(scene.SilverDef),
            "Bartering paid for goods too heavy for a shuttle"
        );
        Expect.IsTrue(
            findTrade.Traders.Contains(scene.TradeShip),
            "Find / Trade didn't record bartering with the trade ship"
        );
    }

    [Test]
    public static IEnumerator BarteringKeepsPurchasesTooHeavyForAShuttleWhenDeliveredByDropPod()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(
            Find.CurrentMap,
            settings: TradeScene.SettingsSnapshot.Defaults with
            {
                DeliverItemsViaDropPod = true,
            }
        );
        var scene = findTrade.Scene;
        LimitShuttleCapacityBelowSteel(scene);
        Expect.IsTrue(
            scene.HeldByTrader(steel) >= SteelToBuy,
            "The trader doesn't have enough steel to sell"
        );
        scene.PlaceOnMap(scene.SilverDef, SilverOnHand);
        findTrade.AddImportRule(steel, SteelToBuy);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(
                steel,
                -SteelToBuy,
                () => ExpectNoRejectionMessage(findTrade.Barter)
            )
        );
    }

    private static void LimitShuttleCapacityBelowSteel(TradeScene scene)
    {
        scene.LimitShuttleCapacity(ShuttleCapacityBelowSteelKg);
        Expect.GreaterThan(
            RimWorld.ThingDefOf.Steel.BaseMass,
            ShuttleCapacityBelowSteelKg,
            "One steel isn't too heavy for a shuttle"
        );
    }

    // ROT's accept gates turn a deal down with a RejectInput message.
    private static void ExpectNoRejectionMessage(Action barter)
    {
        // A message still on screen isn't shown again.
        Messages.Clear();
        barter();
        Expect.IsFalse(
            Messages.liveMessages.Any(message => message.def == MessageTypeDefOf.RejectInput),
            "Bartering had a deal turned down"
        );
    }

    // No trade agreement, no shuttle quest, and the junk is still on the map.
    private static void ExpectNothingTraded(TradeScene scene, string when)
    {
        Expect.IsEmpty(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements,
            $"Bartering {when} started a trade agreement"
        );
        Expect.IsNull(TradeScene.FindTradeQuest(), $"Bartering {when} generated a shuttle quest");
        Expect.AreEqual(
            SteelToSell,
            scene.CountOnMap(RimWorld.ThingDefOf.Steel),
            $"Bartering {when} sold the junk"
        );
    }

    private static List<TradeAgreement> AgreementsEndedDuring(Action action)
    {
        var ended = new List<TradeAgreement>();
        void onEnded(TradeAgreement agreement, bool successful)
        {
            ended.Add(agreement);
        }
        RealisticOrbitalTradeGameComponent.TradeAgreementEnded += onEnded;
        try
        {
            action();
        }
        finally
        {
            RealisticOrbitalTradeGameComponent.TradeAgreementEnded -= onEnded;
        }
        return ended;
    }

    // Runs the sale through to delivery and checks that Find / Trade announced it, the ship kept
    // the same trade agreement until delivery, and the sale completed.
    private static IEnumerator ExpectSaleThroughATradeShuttle(TradeScene scene, IEnumerator sale)
    {
        var lettersBefore = Find.LetterStack.LettersListForReading.ToList();

        yield return sale;
        var agreement = scene.TradeAgreement;
        var barterLetterLabel = "MGAutoSell.LetterTitle"
            .Translate(scene.TradeShip.TraderName)
            .Resolve();
        Expect.AreEqual(
            1,
            Find.LetterStack.LettersListForReading.Except(lettersBefore)
                .Count(letter =>
                    letter.def == LetterDefOf.PositiveEvent
                    && letter.Label.Resolve().StripTags() == barterLetterLabel
                ),
            "Find / Trade didn't send its barter letter"
        );
        if (scene.CurrentSale?.Quest == null)
        {
            yield break;
        }

        yield return scene.WaitForQuestToEnd(() =>
        {
            if (scene.TradeShip.GetData().activeTradeAgreement is { } active)
            {
                Expect.ReferencesAreEqual(
                    agreement,
                    active,
                    "The ship's trade agreement changed before delivery"
                );
            }
        });

        scene.ExpectSaleCompleted();
    }

    private static void ExpectBarterLeftGoodsAlone(TradeScene scene)
    {
        Expect.IsFalse(
            FindTrade_TradeDealProcessor_DoTradeShips.IsTrading,
            "The barter pass is still trading after it returned"
        );
        Expect.IsNull(
            TradeShipData.tradeAgreementForQuest,
            "The barter pass left its trade agreement in the slot comms use"
        );
        Expect.AreEqual(
            SteelToSell,
            scene.CountOnMap(RimWorld.ThingDefOf.Steel),
            "The sold steel left the map without being loaded onto a shuttle"
        );
    }
}
