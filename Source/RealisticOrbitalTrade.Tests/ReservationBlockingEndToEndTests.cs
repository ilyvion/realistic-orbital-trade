using DevTools.Testing;
using RealisticOrbitalTrade.Patch;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// Sold goods still on the map are reserved for the trade: only a pawn hauling them to the shuttle
// may reserve them, and ordered jobs are rejected when any of their targets is reserved.
[TestFixture(TestType.Playing)]
internal sealed class ReservationBlockingEndToEndTests
{
    private const int SteelToSell = 400;

    private static Thing? FindReservedSteel(TradeScene scene) =>
        Find
            .CurrentMap.listerThings.ThingsOfDef(RimWorld.ThingDefOf.Steel)
            .FirstOrDefault(t => scene.TradeAgreement!.ReservedCountFor(t) > 0);

    [Test]
    public static IEnumerator OnlyTheHaulerCanReserveASoldItem()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        scene.StopColonistsHauling();
        foreach (var pawn in Find.CurrentMap.mapPawns.FreeColonistsSpawned.ToList())
        {
            pawn.jobs.EndCurrentJob(JobCondition.InterruptForced);
        }

        var reserved = FindReservedSteel(scene);
        Expect.IsNotNull(reserved, "No sold steel is still reserved for the trade");
        if (reserved == null)
        {
            yield break;
        }
        var bystander = scene.SpawnPawn();
        var transporter = scene.CurrentSale!.Shuttles[0].TransporterComp;

        Expect.IsFalse(bystander.CanReserve(reserved), "A bystander can reserve the sold item");
        using (TradeHaulReservationContext.Enter(bystander, transporter))
        {
            Expect.IsTrue(
                bystander.CanReserve(reserved),
                "A pawn scanning to haul it to the shuttle can't reserve it"
            );
        }
        Expect.IsFalse(bystander.CanReserve(reserved), "The haul scope leaked past its end");
    }

    [Test]
    public static IEnumerator OrderedJobsAreCheckedOnAllThreeTargets()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);
        scene.StopColonistsHauling();

        var reserved = FindReservedSteel(scene);
        Expect.IsNotNull(reserved, "No sold steel is still reserved for the trade");
        if (reserved == null)
        {
            yield break;
        }
        var bystander = scene.SpawnPawn();
        var harmless = scene.CurrentSale!.Shuttles[0].shipThing;

        foreach (var index in new[] { TargetIndex.A, TargetIndex.B, TargetIndex.C })
        {
            var job = JobMaker.MakeJob(RimWorld.JobDefOf.Wear);
            job.SetTarget(TargetIndex.A, harmless);
            job.SetTarget(TargetIndex.B, harmless);
            job.SetTarget(TargetIndex.C, harmless);
            Expect.IsNull(
                Rimworld_Pawn_JobTracker_TryTakeOrderedJob.FindBlockedTarget(bystander, job),
                "A job on unreserved things was blocked"
            );
            job.SetTarget(index, reserved);
            Expect.AreEqual(
                reserved,
                Rimworld_Pawn_JobTracker_TryTakeOrderedJob.FindBlockedTarget(bystander, job),
                $"A reserved item in target {index} wasn't blocked"
            );
        }
    }
}
