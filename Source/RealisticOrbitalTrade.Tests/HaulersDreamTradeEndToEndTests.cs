using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// With Hauler's Dream active, only this mod's own loading job loads a trade shuttle. Needs
// Hauler's Dream active; see TestPlans/HaulersDream.xml.
[TestFixture(TestType.Playing)]
[LoadIfModsActive(PackageId)]
internal sealed class HaulersDreamTradeEndToEndTests
{
    internal const string PackageId = "giwaffed.haulersdream";

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
