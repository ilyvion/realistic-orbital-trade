using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With the mod's own multi-item loading off, Bulk Load For Transporters loads the trade shuttle.
// Goods its loaders carry in their inventories still count as the colony's. Needs Bulk Load For
// Transporters and Pick Up And Haul active; see TestPlans/BulkLoad.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive(Settings.BulkLoadForTransportersPackageId)]
internal sealed class BulkLoadTradeEndToEndTests
{
    private const int SteelOnMap = 500;
    private const int SteelSold = 400;

    private static TradeScene NewScene() =>
        new(
            Find.CurrentMap,
            TradeScene.SettingsSnapshot.Defaults with
            {
                MultiItemShuttleLoading = false,
            }
        );

    [Test]
    public static IEnumerator RenegotiatingOffersGoodsBulkLoadIsCarrying()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = NewScene();
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, SteelSold);
        yield return scene.WaitForSoldGoodsInAnInventory();

        Expect.AreEqual(
            SteelOnMap,
            scene.OfferedInRenegotiation(steel),
            "Renegotiating didn't offer all of the colony's steel"
        );
    }
}
