using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: vanilla's CompShuttle.CheckAutoload rebuilds leftToLoad every autoload tick,
// clamping each entry to the stock it can see via TransporterUtility.ThingsBeingHauledTo - which
// only recognizes vanilla's own JobDriver_HaulToTransporter. Cargo already split off and riding
// with the bulk hauling job was invisible, so rebuilt entries came up short by everything in
// flight and CountToTransfer swung wildly between autoload ticks (caught live via Player.log
// showing a trade's remaining-needed counts bouncing up and down while pawns hauled undisturbed).
// InFlightCargo is what Rimworld_TransporterUtility_ThingsBeingHauledTo's postfix reports per
// bulk-hauling pawn to close that gap - it must cover both the hand-carried stack and every
// stack still waiting in the pawn's inventory.
[TestFixture(TestType.MainMenu)]
internal sealed class InFlightCargoTests
{
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    [Test]
    public static void InFlightCargoIncludesBothHandCarriedAndInventoryStacks()
    {
        var carried = NewThing(RimWorld.ThingDefOf.Steel, 75);
        var pendingWood = NewThing(RimWorld.ThingDefOf.WoodLog, 31);
        var pendingMeal = NewThing(RimWorld.ThingDefOf.MealSurvivalPack, 8);

        var cargo = JobDriver_HaulMultipleToTransporter
            .InFlightCargo(carried, [pendingWood, pendingMeal])
            .ToList();

        Expect.AreEqual(3, cargo.Count);
        Expect.IsTrue(cargo.Contains(carried));
        Expect.IsTrue(cargo.Contains(pendingWood));
        Expect.IsTrue(cargo.Contains(pendingMeal));
    }

    [Test]
    public static void InFlightCargoWithNothingInHandStillReportsInventoryStacks()
    {
        var pendingWood = NewThing(RimWorld.ThingDefOf.WoodLog, 31);

        var cargo = JobDriver_HaulMultipleToTransporter
            .InFlightCargo(carriedThing: null, [pendingWood])
            .ToList();

        Expect.AreEqual(1, cargo.Count);
        Expect.IsTrue(cargo.Contains(pendingWood));
    }
}
