using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// The player reopens comms on a landed shuttle and changes the amount sold: the trade then
// completes on the new amount, with the goods and the payment matching it, including when the
// reduction is below what was already aboard.
[TestFixture(TestType.Playing)]
internal sealed class TradeRenegotiationEndToEndTests
{
    private const int SteelOnMap = 400;

    [Test]
    public static IEnumerator ReducingASaleSendsOnlyTheReducedAmount()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, 300);
        scene.RenegotiateSale(100);
        yield return scene.WaitForQuestToEnd();

        scene.ExpectSaleCompleted();
    }

    [Test]
    public static IEnumerator ReducingASaleBelowWhatIsLoadedReturnsTheExcess()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, SteelOnMap);
        yield return scene.WaitForPartialLoad();
        var loaded = scene.LoadedInShuttles(steel);
        scene.RenegotiateSale(Math.Max(1, loaded / 2));
        yield return scene.WaitForQuestToEnd();

        scene.ExpectSaleCompleted();
    }

    [Test]
    public static IEnumerator IncreasingASaleLoadsTheExtra()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelOnMap);

        yield return scene.SellAndWaitForLanding(steel, 100);
        scene.RenegotiateSale(300);
        yield return scene.WaitForQuestToEnd();

        scene.ExpectSaleCompleted();
    }
}
