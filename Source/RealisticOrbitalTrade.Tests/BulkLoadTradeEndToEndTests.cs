using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With Bulk Load For Transporters active, only this mod's own loading job loads a trade shuttle.
// Needs Bulk Load For Transporters and Pick Up And Haul active; see TestPlans/BulkLoad.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive(PackageId)]
internal sealed class BulkLoadTradeEndToEndTests
{
    internal const string PackageId = "ilarion.bulkloadfortransporters";

    [Test]
    public static IEnumerator ASaleIsLoadedOnlyByThisModsJob()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, 500);
        HashSet<JobDef> otherJobs = [];

        yield return scene.SellAndWatchShuttleJobs(steel, 400, otherJobs);

        scene.ExpectSaleCompleted();
        Expect.IsEmpty(otherJobs, "Another job worked on the trade shuttle");
    }
}
