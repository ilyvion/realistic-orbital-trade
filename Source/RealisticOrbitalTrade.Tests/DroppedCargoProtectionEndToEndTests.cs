using DevTools.Testing;
using Verse.AI;

namespace RealisticOrbitalTrade.Tests;

// Sold goods a hauler drops without delivering them are protected for the trade again, and still
// get loaded.
[TestFixture(TestType.Playing)]
internal sealed class DroppedCargoProtectionEndToEndTests
{
    private const int SteelToSell = 400;

    private static int InHand(Pawn pawn) =>
        pawn.carryTracker.CarriedThing is { } carried && carried.def == RimWorld.ThingDefOf.Steel
            ? carried.stackCount
            : 0;

    private static int InInventory(Pawn pawn) =>
        (pawn.jobs.curDriver as JobDriver_HaulMultipleToTransporter)
            ?.CarriedThings.Where(t => !t.Destroyed)
            .Sum(t => t.stackCount)
        ?? 0;

    private static bool IsHauling(Pawn pawn) =>
        JobDriver_HaulMultipleToTransporter.HaulingTransporter(pawn) != null;

    [Test]
    public static IEnumerator AnInterruptedHaulerDropsItsLoadProtected()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);

        Pawn? hauler = null;
        yield return scene.WaitForColonist(p => IsHauling(p) && InHand(p) > 0, p => hauler = p);
        Expect.IsNotNull(hauler, "No colonist ever carried sold steel to the shuttle");
        if (hauler == null)
        {
            yield break;
        }

        var carried = hauler.carryTracker.CarriedThing;
        var dropped = InHand(hauler) + InInventory(hauler);
        var protectedBefore = scene.ProtectedOnMap(steel);
        hauler.jobs.EndCurrentJob(JobCondition.InterruptForced, startNewJob: false);

        Expect.AreEqual(
            protectedBefore + dropped,
            scene.ProtectedOnMap(steel),
            "The dropped steel isn't protected for the trade"
        );
        if (carried is { Spawned: true })
        {
            Expect.IsFalse(
                scene.SpawnPawn().CanReserve(carried),
                "A bystander can reserve the dropped steel"
            );
        }

        yield return scene.WaitForQuestToEnd();
        scene.ExpectSaleCompleted();
    }

    [Test]
    public static IEnumerator ADownedHaulerDropsItsInventoryProtected()
    {
        var steel = RimWorld.ThingDefOf.Steel;
        using var scene = new TradeScene(Find.CurrentMap);
        scene.PlaceOnMap(steel, SteelToSell);
        yield return scene.SellAndWaitForLanding(steel, SteelToSell);

        Pawn? hauler = null;
        yield return scene.WaitForColonist(
            p => IsHauling(p) && InInventory(p) > 0,
            p => hauler = p
        );
        Expect.IsNotNull(hauler, "No colonist ever gathered sold steel into their inventory");
        if (hauler == null)
        {
            yield break;
        }

        var dropped = InHand(hauler) + InInventory(hauler);
        var protectedBefore = scene.ProtectedOnMap(steel);

        // What Pawn.DropAndForbidEverything does to a pawn being downed, short of its equipment.
        _ = hauler.carryTracker.TryDropCarriedThing(
            hauler.PositionHeld,
            ThingPlaceMode.Near,
            out _
        );
        hauler.inventory.DropAllNearPawn(hauler.PositionHeld, forbid: true);

        Expect.AreEqual(
            protectedBefore + dropped,
            scene.ProtectedOnMap(steel),
            "The dropped steel isn't protected for the trade"
        );

        yield return scene.WaitForQuestToEnd();
        scene.ExpectSaleCompleted();
    }
}
