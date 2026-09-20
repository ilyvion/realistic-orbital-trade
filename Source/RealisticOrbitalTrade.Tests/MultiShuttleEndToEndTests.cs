using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// A sale too heavy for one shuttle is divided across several, which all land, get loaded and take
// off together before the trade is paid for. The shuttles' capacity is lowered so a modest sale
// is heavy enough, which keeps the colonists' hauling short.
[TestFixture(TestType.Playing)]
internal sealed class MultiShuttleEndToEndTests
{
    private const float ShuttleCapacityKg = 40f;
    private const int SteelToSell = 120;

    [Test]
    public static IEnumerator SaleTooHeavyForOneShuttleIsSplitAcrossShuttles()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.LimitShuttleCapacity(ShuttleCapacityKg);
        Expect.GreaterThan(
            SteelToSell * steel.BaseMass,
            ShuttleCapacityKg,
            "The sale isn't heavy enough to need a second shuttle"
        );
        scene.PlaceOnMap(steel, SteelToSell);

        yield return scene.SellAndWaitForCompletion(steel, SteelToSell);

        Expect.GreaterThan(
            scene.CurrentSale!.ShuttleCount,
            1,
            "A sale over the shuttle capacity wasn't split across shuttles"
        );
        scene.ExpectSaleCompleted();
    }
}
