using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// EndHaulJobsFor ends only the hauling jobs that carry or are heading for a matching thing.
[TestFixture(TestType.Playing)]
internal sealed class HaulJobEndingEndToEndTests
{
    private const int SteelToSell = 400;

    private static Thing? HauledThing(Pawn pawn)
    {
        var carried = (pawn.jobs.curDriver as JobDriver_HaulMultipleToTransporter)?.CarriedThings;
        return pawn.carryTracker.CarriedThing
            ?? pawn.CurJob.targetA.Thing
            ?? (carried is { Count: > 0 } ? carried[0] : null);
    }

    [Test]
    public static IEnumerator OnlyTheHaulerOfTheMatchingThingHasItsJobEnded()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);

        var map = Find.CurrentMap;
        var transporter = scene.CurrentSale!.Shuttles[0].TransporterComp;
        List<Pawn> haulers = [];
        Pawn? hauler = null;
        yield return scene.TickUntil(() =>
        {
            haulers = [.. Utils.PawnsWithHaulToTransporterJob(transporter, map)];
            hauler = haulers.FirstOrDefault(p => HauledThing(p) != null);
            return hauler != null;
        });
        Expect.IsNotNull(hauler, "Nobody ever hauled to the shuttle");
        if (hauler == null)
        {
            yield break;
        }

        var target = HauledThing(hauler);
        bool Matches(Thing t)
        {
            return t == target;
        }
        var expectedEnded = haulers.Where(p => Utils.IsHauling(p, Matches)).ToList();
        var expectedKept = haulers.Except(expectedEnded).ToList();
        Expect.IsTrue(expectedEnded.Contains(hauler), "IsHauling missed the hauler's own thing");

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

        var map = Find.CurrentMap;
        var transporter = scene.CurrentSale!.Shuttles[0].TransporterComp;
        yield return scene.TickUntil(() =>
            Utils.PawnsWithHaulToTransporterJob(transporter, map).Any()
        );
        var before = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();
        Expect.GreaterThan(before.Count, 0, "Nobody ever hauled to the shuttle");

        Utils.EndHaulJobsFor(transporter, map, _ => false);

        var after = Utils.PawnsWithHaulToTransporterJob(transporter, map).ToList();
        Expect.AreEqual(before.Count, after.Count);
    }
}
