using DevTools.Testing;
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
