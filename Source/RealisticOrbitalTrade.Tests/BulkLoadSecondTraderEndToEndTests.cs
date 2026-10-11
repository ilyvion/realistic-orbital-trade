using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With the mod's own multi-item loading off, Bulk Load For Transporters loads the trade shuttle.
// Goods sold to one trader still aren't offered to another while bulk loaders carry them. Needs
// Bulk Load For Transporters and Pick Up And Haul active; see TestPlans/BulkLoad.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive(Settings.BulkLoadForTransportersPackageId)]
internal sealed class BulkLoadSecondTraderEndToEndTests
{
    private const int SteelOnMap = 500;
    private const int SteelSold = 400;

    [Test]
    public static IEnumerator OnlyUnsoldGoodsAreOfferedWhileBulkLoadCarriesTheSoldOnes()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(
            Find.CurrentMap,
            TradeScene.SettingsSnapshot.Defaults with
            {
                MultiItemShuttleLoading = false,
            }
        );
        scene.PlaceOnMap(steel, SteelOnMap);
        var secondTrader = scene.AddTrader();

        yield return scene.SellAndWaitForLanding(steel, SteelSold);
        yield return scene.WaitForSoldGoodsInAnInventory();

        Expect.AreEqual(
            SteelOnMap - SteelSold,
            scene.OfferedForSaleTo(secondTrader, steel),
            "The second trader wasn't offered exactly the steel left unsold"
        );
    }
}
