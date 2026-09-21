using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// The player calls off an accepted trade once the trader's shuttle has landed: the trade fails,
// the shuttle leaves, and the sold goods and the payment stay where they were, even when part of
// the goods were already aboard.
[TestFixture(TestType.Playing)]
internal sealed class TradeCancellationEndToEndTests
{
    private const int SteelToSell = 40;
    private const int PartlyLoadedSteelToSell = 400;

    [Test]
    public static IEnumerator CancellingALandedTradeKeepsGoodsAndPaysNothing()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);

        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        scene.CancelTradeFromShuttle();
        yield return scene.WaitForQuestToEnd();
        yield return scene.WaitForShuttlesToLeave();

        scene.ExpectSaleCancelled();
    }

    [Test]
    public static IEnumerator CancellingAPartlyLoadedShuttleUnloadsItBeforeLeaving()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, PartlyLoadedSteelToSell);

        yield return scene.SellAndWaitForLanding(steel, PartlyLoadedSteelToSell);
        yield return scene.WaitForPartialLoad();
        scene.CancelTradeFromShuttle();
        yield return scene.WaitForQuestToEnd();
        yield return scene.WaitForShuttlesToLeave();

        Expect.AreEqual(
            0,
            scene.LoadedInShuttles(steel),
            "The shuttle left with sold goods aboard"
        );
        scene.ExpectSaleCancelled();
    }
}
