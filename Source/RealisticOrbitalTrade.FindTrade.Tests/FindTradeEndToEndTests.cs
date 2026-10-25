using DevTools.Testing;
using RealisticOrbitalTrade.FindTrade.Patch;

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
        var lettersBefore = Find.LetterStack.LettersListForReading.ToList();

        yield return scene.TradeAndWaitForLanding(steel, SteelToSell, ExpectBarterLeftGoodsAlone);
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

        void ExpectBarterLeftGoodsAlone()
        {
            findTrade.Barter();
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
                scene.CountOnMap(steel),
                "The sold steel left the map without being loaded onto a shuttle"
            );
        }
    }
}
