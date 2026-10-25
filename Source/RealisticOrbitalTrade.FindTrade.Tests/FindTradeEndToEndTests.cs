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
    private const int SteelLeftUnsold = 50;
    private const int WoodToSell = 100;
    private const int ClothToSell = 10;
    private const int SteelToBuy = 10;
    private const int SilverOnHand = 200;

    // Below one steel's mass, above ten cloth's.
    private const float ShuttleCapacityBelowSteelKg = 0.45f;

    // Far above and below what the trader pays for the steel junk.
    private const int ThresholdAboveTheJunk = 100_000;
    private const int ThresholdBelowTheJunk = 1;

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

    [Test]
    public static void BarteringSkipsJunkWorthLessThanTheMinimumTradeThreshold()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(
            Find.CurrentMap,
            settings: TradeScene.SettingsSnapshot.Defaults with
            {
                UseMinimumTradeThreshold = true,
            }
        );
        var scene = findTrade.Scene;
        scene.TradeShip.GetData().minimumTradeThreshold = ThresholdAboveTheJunk;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        ExpectNoRejectionMessage(findTrade.Barter);

        ExpectNothingTraded(scene, "with junk worth less than the minimum trade threshold");
        Expect.IsTrue(
            findTrade.Traders.Contains(scene.TradeShip),
            "Find / Trade didn't record bartering with the trade ship"
        );
    }

    [Test]
    public static IEnumerator BarteringSellsJunkWorthAtLeastTheMinimumTradeThreshold()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(
            Find.CurrentMap,
            settings: TradeScene.SettingsSnapshot.Defaults with
            {
                UseMinimumTradeThreshold = true,
            }
        );
        var scene = findTrade.Scene;
        scene.TradeShip.GetData().minimumTradeThreshold = ThresholdBelowTheJunk;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(steel, SteelToSell, Barter)
        );

        void Barter()
        {
            ExpectNoRejectionMessage(findTrade.Barter);
            ExpectBarterLeftGoodsAlone(scene);
        }
    }

    [Test]
    public static IEnumerator BarteringIgnoresTheMinimumTradeThresholdWhenItIsOff()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(
            Find.CurrentMap,
            settings: TradeScene.SettingsSnapshot.Defaults with
            {
                UseMinimumTradeThreshold = false,
            }
        );
        var scene = findTrade.Scene;
        scene.TradeShip.GetData().minimumTradeThreshold = ThresholdAboveTheJunk;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);

        yield return ExpectSaleThroughATradeShuttle(
            scene,
            scene.TradeAndWaitForLanding(steel, SteelToSell, Barter)
        );

        void Barter()
        {
            ExpectNoRejectionMessage(findTrade.Barter);
            ExpectBarterLeftGoodsAlone(scene);
        }
    }

    [Test]
    [WarningsAllowed("Clearing trade agreement")]
    public static IEnumerator BarteringEndsANewTradeAgreementLeftOverFromAnEarlierTrade()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        var leftover = RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(
            scene.TradeShip,
            scene.Negotiator
        );
        scene.TradeShip.GetData().activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
            leftover;
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
            Expect.IsFalse(
                RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Contains(leftover),
                "Bartering didn't end the leftover trade agreement"
            );
        }
    }

    [Test]
    [WarningsAllowed("Clearing trade agreement")]
    public static IEnumerator BarteringKeepsARenegotiatedTradeAgreementLeftOverFromAnEarlierTrade()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement;
        TradeShipData.tradeAgreementForQuest = agreement;

        findTrade.Barter();

        Expect.IsNull(
            TradeShipData.tradeAgreementForQuest,
            "Bartering didn't clear the leftover trade agreement"
        );
        Expect.IsTrue(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Contains(agreement),
            "Bartering ended a trade agreement still in progress"
        );
        Expect.AreEqual(
            QuestState.Ongoing,
            scene.CurrentSale?.Quest?.State,
            "Bartering ended the shuttle quest of a trade still in progress"
        );

        yield return scene.WaitForQuestToEnd();
        scene.ExpectSaleCompleted();
    }

    [Test]
    public static IEnumerator BarteringDuringARenegotiationTradesNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        var wood = RimWorld.ThingDefOf.WoodLog;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        var agreement = scene.TradeAgreement!;
        // A ship free to trade, so a pass that went ahead would sell it the junk.
        var otherTrader = scene.AddTrader();
        scene.PlaceOnMap(wood, WoodToSell);
        findTrade.MarkJunk(wood);
        HashSet<ITrader> tradersBefore = [.. findTrade.Traders];

        Utils.BeginRenegotiation(agreement);
        try
        {
            findTrade.Barter();
        }
        finally
        {
            Utils.EndRenegotiation();
        }

        Expect.AreEqual(
            1,
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Count,
            "Bartering during a renegotiation started another trade agreement"
        );
        Expect.IsNull(
            otherTrader.GetData().activeTradeAgreement,
            "Bartering during a renegotiation traded with another ship"
        );
        Expect.ReferencesAreEqual(
            agreement,
            scene.TradeShip.GetData().activeTradeAgreement,
            "Bartering during a renegotiation replaced the ship's trade agreement"
        );
        Expect.AreEqual(
            QuestState.Ongoing,
            scene.CurrentSale?.Quest?.State,
            "Bartering during a renegotiation ended the shuttle quest"
        );
        Expect.AreEqual(
            WoodToSell,
            scene.CountOnMap(wood),
            "Bartering during a renegotiation sold junk"
        );
        Expect.IsTrue(
            findTrade.Traders.SetEquals(tradersBefore),
            "Bartering during a renegotiation changed which ships Find / Trade bartered with"
        );
    }

    [Test]
    public static void BarteringWhileATradeWindowIsOpenTradesNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
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
        findTrade.MarkJunk(steel);

        scene.TradeShip.TryOpenComms(scene.Negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        var agreement = TradeShipData.tradeAgreementForQuest;
        try
        {
            Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
            Expect.IsNotNull(agreement, "Comms didn't start a trade agreement");

            findTrade.Barter();

            Expect.ReferencesAreEqual(
                agreement,
                TradeShipData.tradeAgreementForQuest,
                "Bartering while a trade window is open took its trade agreement"
            );
            Expect.AreEqual(
                1,
                RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Count,
                "Bartering while a trade window is open started another trade agreement"
            );
            Expect.IsNull(
                TradeScene.FindTradeQuest(),
                "Bartering while a trade window is open generated a shuttle quest"
            );
            Expect.AreEqual(
                SteelToSell,
                scene.CountOnMap(steel),
                "Bartering while a trade window is open sold the junk"
            );
        }
        finally
        {
            dialog?.Close(doCloseSound: false);
        }
    }

    // Find / Trade fills in every trade dialog it sees, the renegotiation dialog included.
    [Test]
    public static IEnumerator OpeningARenegotiationKeepsTheAgreedDeal()
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
        var agreement = scene.TradeAgreement!;
        scene.PlaceOnMap(wood, WoodToSell);
        findTrade.MarkJunk(wood);

        Utils.BeginRenegotiation(agreement);
        agreement.tradeShip.TryOpenComms(agreement.negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        try
        {
            Expect.IsNotNull(dialog, "Renegotiating didn't reopen the trade dialog");
            Expect.AreEqual(
                -SteelToSell,
                CountToTransfer(steel),
                "Opening the renegotiation changed the agreed steel"
            );
            Expect.AreEqual(0, CountToTransfer(wood), "Opening the renegotiation added the junk");
        }
        finally
        {
            if (dialog != null)
            {
                dialog.Close(doCloseSound: false);
            }
            else
            {
                Utils.EndRenegotiation();
            }
        }
    }

    [Test]
    public static IEnumerator OpeningARenegotiationIsNotRememberedAsAManualTrade()
    {
        var steel = RimWorld.ThingDefOf.Steel;
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
        // Remembered from here on, so only the renegotiation could record the ship.
        FindTradeScene.FindTradeSettings.Defaults.Apply();

        _ = scene.OfferedInRenegotiation(steel);

        Expect.IsFalse(
            findTrade.Traders.Contains(scene.TradeShip),
            "Opening the renegotiation made Find / Trade skip the ship for the rest of its stay"
        );
    }

    // Find / Trade fills in every trade dialog it sees with whole junk stacks.
    [Test]
    public static void JunkSoldByAutoBarterIsntOfferedToAnotherShip()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);
        findTrade.Barter();
        // In orbit only after the pass, so the pass doesn't barter with it.
        var otherTrader = scene.AddTrader();

        otherTrader.TryOpenComms(scene.Negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        try
        {
            Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
            Expect.AreEqual(
                0,
                CountToTransfer(steel),
                "The other ship's trade dialog sells junk already sold by auto barter"
            );
        }
        finally
        {
            dialog?.Close(doCloseSound: false);
        }
    }

    [Test]
    public static void AcceptingAnotherShipsTradeSellsOnlyJunkNotSoldByAutoBarter()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        scene.PlaceOnMap(steel, SteelToSell);
        findTrade.MarkJunk(steel);
        findTrade.Barter();
        var barterAgreement = scene.TradeShip.GetData().activeTradeAgreement;
        Expect.IsNotNull(barterAgreement, "Bartering didn't start a trade agreement");
        var otherTrader = scene.AddTrader();
        scene.PlaceOnMap(steel, SteelLeftUnsold);

        otherTrader.TryOpenComms(scene.Negotiator);
        var dialog = Find.WindowStack.WindowOfType<Dialog_Trade>();
        try
        {
            Expect.IsNotNull(dialog, "Comms didn't open the trade dialog");
            Expect.AreEqual(
                -SteelLeftUnsold,
                CountToTransfer(steel),
                "The other ship's trade dialog doesn't sell exactly the junk left unsold"
            );
            Expect.IsTrue(
                TradeSession.deal.TryExecute(out var actuallyTraded),
                "Trade was rejected"
            );
            Expect.IsTrue(actuallyTraded, "Trade didn't execute");
        }
        finally
        {
            dialog?.Close(doCloseSound: false);
            TradeSession.Close();
        }

        var otherAgreement = otherTrader.GetData().activeTradeAgreement;
        Expect.IsNotNull(otherAgreement, "No trade agreement with the other ship");
        Expect.AreEqual(
            SteelLeftUnsold,
            otherAgreement?.thingsSoldToTrader.Where(t => t.thing.def == steel).Sum(t => t.Count)
                ?? 0,
            "The other ship wasn't sold exactly the junk left unsold"
        );
        Expect.AreEqual(
            SteelToSell,
            barterAgreement?.thingsSoldToTrader.Where(t => t.thing.def == steel).Sum(t => t.Count)
                ?? 0,
            "The trade with the other ship changed what auto barter sold"
        );
        foreach (var stack in scene.Map.listerThings.ThingsOfDef(steel))
        {
            Expect.LessThanOrEqualTo(
                (barterAgreement?.ReservedCountFor(stack) ?? 0)
                    + (otherAgreement?.ReservedCountFor(stack) ?? 0),
                stack.stackCount,
                "Both ships were sold the same steel"
            );
        }
    }

    // A pass started by another mod while one is running, entered through the patch directly.
    [Test]
    public static void ANestedBarterPassLeavesTheOuterPassItsTradeAgreement()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        var gameComponent = RealisticOrbitalTradeGameComponent.Current;

        Expect.IsTrue(
            FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var outer),
            "The outer barter pass was skipped"
        );
        Expect.IsTrue(
            FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var inner),
            "The nested barter pass was skipped"
        );
        var agreement = StartDealTradeAgreement(scene);

        FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(inner);
        Expect.IsTrue(
            FindTrade_TradeDealProcessor_DoTradeShips.IsTrading,
            "Ending the nested barter pass stopped the outer pass trading"
        );
        Expect.IsTrue(
            gameComponent.ActiveTradeAgreements.Contains(agreement),
            "Ending the nested barter pass ended the outer pass's trade agreement"
        );
        Expect.ReferencesAreEqual(
            agreement,
            TradeShipData.tradeAgreementForQuest,
            "Ending the nested barter pass took the outer pass's trade agreement"
        );

        FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(outer);
        Expect.IsFalse(
            FindTrade_TradeDealProcessor_DoTradeShips.IsTrading,
            "The barter pass is still trading after the outer pass ended"
        );
        Expect.IsFalse(
            gameComponent.ActiveTradeAgreements.Contains(agreement),
            "Ending the outer barter pass didn't end the trade agreement left behind"
        );
        Expect.IsNull(
            TradeShipData.tradeAgreementForQuest,
            "Ending the outer barter pass left a trade agreement in the slot comms use"
        );
    }

    [Test]
    public static void NestedBarterPassesEachStopSkippingOnlyTheShipsTheySkipped()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;
        var gameComponent = RealisticOrbitalTradeGameComponent.Current;
        var outerShip = scene.TradeShip;
        var innerShip = scene.AddTrader();

        // A ship with an active trade won't answer comms, so the pass skips it.
        outerShip.GetData().activeTradeAgreement = gameComponent.StartTradeAgreement(
            outerShip,
            scene.Negotiator
        );
        _ = FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var outer);
        innerShip.GetData().activeTradeAgreement = gameComponent.StartTradeAgreement(
            innerShip,
            scene.Negotiator
        );
        _ = FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var inner);
        Expect.IsTrue(
            findTrade.Traders.Contains(outerShip),
            "The outer barter pass didn't skip the ship with an active trade"
        );
        Expect.IsTrue(
            findTrade.Traders.Contains(innerShip),
            "The nested barter pass didn't skip the ship with an active trade"
        );

        FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(inner);
        Expect.IsFalse(
            findTrade.Traders.Contains(innerShip),
            "Ending the nested barter pass still skips the ship it skipped"
        );
        Expect.IsTrue(
            findTrade.Traders.Contains(outerShip),
            "Ending the nested barter pass stopped skipping the outer pass's ship"
        );

        FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(outer);
        Expect.IsFalse(
            findTrade.Traders.Contains(outerShip),
            "Ending the outer barter pass still skips the ship it skipped"
        );
    }

    // A deal that throws never reaches the TryExecute postfix, so its agreement stays in the slot.
    [Test]
    public static void ABarterPassEndsTheTradeAgreementOfADealThatThrew()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;

        Expect.IsTrue(
            FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var state),
            "The barter pass was skipped"
        );
        var agreement = StartDealTradeAgreement(scene);
        FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(state);

        Expect.IsFalse(
            FindTrade_TradeDealProcessor_DoTradeShips.IsTrading,
            "The barter pass is still trading after it ended"
        );
        Expect.IsFalse(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Contains(agreement),
            "The barter pass didn't end the trade agreement of the deal that threw"
        );
        Expect.IsNull(
            scene.TradeShip.GetData().activeTradeAgreement,
            "The ship still has the trade agreement of the deal that threw"
        );
        Expect.IsNull(
            TradeShipData.tradeAgreementForQuest,
            "The barter pass left a trade agreement in the slot comms use"
        );
    }

    // The DoTradeDeal patches fit a deal to ROT's own accept gates, so only vanilla's (a negotiator
    // whose ideoligion forbids trading organs) or another mod's can refuse one; the postfix is
    // called directly with the refusal.
    [Test]
    public static void ABarterPassEndsTheTradeAgreementOfARefusedDeal()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;

        Expect.IsTrue(
            FindTrade_TradeDealProcessor_DoTradeShips.Prefix(scene.Negotiator, out var state),
            "The barter pass was skipped"
        );
        List<TradeAgreement> ended;
        TradeAgreement agreement;
        try
        {
            agreement = StartDealTradeAgreement(scene);
            ended = AgreementsEndedDuring(() => Rimworld_TradeDeal_TryExecute.Postfix(false));
        }
        finally
        {
            FindTrade_TradeDealProcessor_DoTradeShips.Finalizer(state);
        }

        Expect.AreEqual(1, ended.Count, "The refused deal didn't end exactly one trade agreement");
        Expect.ReferencesAreEqual(
            agreement,
            ended[0],
            "The refused deal ended a trade agreement other than its own"
        );
        Expect.IsFalse(
            RealisticOrbitalTradeGameComponent.Current.ActiveTradeAgreements.Contains(agreement),
            "The refused deal's trade agreement is still active"
        );
        Expect.IsNull(
            scene.TradeShip.GetData().activeTradeAgreement,
            "The ship still has the refused deal's trade agreement"
        );
        Expect.IsNull(
            TradeShipData.tradeAgreementForQuest,
            "The refused deal left its trade agreement in the slot comms use"
        );
    }

    // A refused manual trade keeps its window and agreement open for the player to adjust the deal.
    [Test]
    public static void ARefusedDealOutsideABarterPassKeepsItsTradeAgreement()
    {
        using var findTrade = new FindTradeScene(Find.CurrentMap);
        var scene = findTrade.Scene;

        var agreement = StartDealTradeAgreement(scene);
        try
        {
            var ended = AgreementsEndedDuring(() => Rimworld_TradeDeal_TryExecute.Postfix(false));

            Expect.IsEmpty(ended, "A refused deal outside a barter pass ended a trade agreement");
            Expect.ReferencesAreEqual(
                agreement,
                TradeShipData.tradeAgreementForQuest,
                "A refused deal outside a barter pass took the trade agreement out of its slot"
            );
        }
        finally
        {
            TradeShipData.EndTradeAgreementIfExists();
        }
    }

    // How much of the def the open trade dialog's deal trades.
    private static int CountToTransfer(ThingDef def) =>
        TradeSession.deal.AllTradeables.Where(t => t.ThingDef == def).Sum(t => t.CountToTransfer);

    // Starts a trade agreement the way the TryExecute prefix does for a deal in a barter pass.
    private static TradeAgreement StartDealTradeAgreement(TradeScene scene)
    {
        var agreement = RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(
            scene.TradeShip,
            scene.Negotiator
        );
        scene.TradeShip.GetData().activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
            agreement;
        return agreement;
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
