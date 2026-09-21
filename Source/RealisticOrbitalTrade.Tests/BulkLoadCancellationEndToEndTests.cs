using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With the mod's own multi-item loading off, Bulk Load For Transporters loads the trade shuttle.
// Cancelling a half-loaded trade must still leave every sold item in the colony. Needs Bulk Load
// For Transporters and Pick Up And Haul active; see TestPlans/BulkLoad.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive(Settings.BulkLoadForTransportersPackageId)]
internal sealed class BulkLoadCancellationEndToEndTests
{
    private const int SteelToSell = 400;

    [Test]
    public static IEnumerator CancellingAPartlyLoadedShuttleReturnsGoodsLoadedByBulkLoad()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(
            Find.CurrentMap,
            TradeScene.SettingsSnapshot.Defaults with
            {
                MultiItemShuttleLoading = false,
            }
        );
        scene.PlaceOnMap(steel, SteelToSell);

        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
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
