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
