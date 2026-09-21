using DevTools.Testing;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// EndHaulJobsFor ends only the hauling jobs that carry or are heading for a matching thing.
[TestFixture(TestType.Playing)]
internal sealed class HaulJobEndingEndToEndTests
{
    private const int SteelToSell = 400;

    private static Thing? HauledThing(Pawn pawn) =>
        pawn.carryTracker.CarriedThing
        ?? pawn.CurJob.targetA.Thing
        ?? (
            pawn.jobs.curDriver as JobDriver_HaulMultipleToTransporter
        )?.CarriedThings.FirstOrDefault();

    [Test]
    public static IEnumerator OnlyTheHaulerOfTheMatchingThingHasItsJobEnded()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        yield return scene.WaitForPartialLoad();

        var map = Find.CurrentMap;
        var transporter = scene.CurrentSale!.Shuttles[0].TransporterComp;
        var haulers = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();
        Expect.GreaterThan(haulers.Count, 0, "Nobody is hauling to the shuttle");
        if (haulers.Count == 0)
        {
            yield break;
        }

        var target = HauledThing(haulers[0]);
        Expect.IsNotNull(target, "Couldn't tell what the first hauler is hauling");
        if (target == null)
        {
            yield break;
        }
        bool Matches(Thing t) => t == target;
        var expectedEnded = haulers.Where(p => Utils.IsHauling(p, Matches)).ToList();
        var expectedKept = haulers.Except(expectedEnded).ToList();
        Expect.IsTrue(
            expectedEnded.Contains(haulers[0]),
            "IsHauling missed the hauler's own thing"
        );

        Utils.EndHaulJobsFor(transporter, map, Matches);

        var stillHauling = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();
        foreach (var pawn in expectedEnded)
        {
            Expect.IsFalse(stillHauling.Contains(pawn), $"{pawn} is still hauling the thing");
        }
        foreach (var pawn in expectedKept)
        {
            Expect.IsTrue(stillHauling.Contains(pawn), $"{pawn}'s unrelated haul was ended");
        }
    }

    [Test]
    public static IEnumerator NothingMatchingEndsNoJobs()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        yield return scene.WaitForPartialLoad();

        var map = Find.CurrentMap;
        var transporter = scene.CurrentSale!.Shuttles[0].TransporterComp;
        var before = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();

        Utils.EndHaulJobsFor(transporter, map, _ => false);

        var after = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();
        Expect.AreEqual(before.Count, after.Count);
    }
}
