using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Drives one complete orbital trade on a live map: the trade dialog is opened and accepted, the
// trader's shuttle lands, colonists load the sold goods, the shuttle takes off, and the silver
// paid arrives on the second shuttle.
[TestFixture(TestType.Playing)]
internal sealed class OrbitalTradeEndToEndTests
{
    private const int SteelToSell = 40;

    [Test]
    public static IEnumerator SellingGoodsIsLoadedShippedAndPaidFor()
    {
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(RimWorld.ThingDefOf.Steel, SteelToSell);

        yield return scene.SellAndWaitForCompletion(RimWorld.ThingDefOf.Steel, SteelToSell);

        Expect.AreEqual(1, scene.CurrentSale!.ShuttleCount, "A light sale needed several shuttles");
        scene.ExpectSaleCompleted();
    }
}
