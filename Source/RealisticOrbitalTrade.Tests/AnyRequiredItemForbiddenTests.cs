using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for forbidden-items detection: CompTradeShuttle.CheckAutoload must
// distinguish "enough of the item exists, some of it is just forbidden" (fine, as long as the
// non-forbidden remainder still covers the requirement) from "so much of it is forbidden that
// colonists can't actually haul enough" (should surface the ROT_TradeForbidden overlay), and must never
// penalize a requirement for stock that's already been split off and is physically in flight toward
// the transporter - that keeps getting delivered regardless of forbidden status.
[TestSuite]
internal static class AnyRequiredItemForbiddenTests
{
    private static int _nextThingId;

    private static Thing NewThing(ThingDef def, int stackCount) =>
        new()
        {
            def = def,
            stackCount = stackCount,
            thingIDNumber = _nextThingId++,
        };

    private static bool NeverForbidden(Thing thing) => false;

    [Test]
    public static void PartiallyForbiddenStockStillCoveringRequirementIsNotForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        var forbiddenThing = NewThing(thingDef, 20);
        List<Thing> colony = [NewThing(thingDef, 30), forbiddenThing];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            thing => thing == forbiddenThing
        );

        Assert.That(forbidden).Is.False();
    }

    [Test]
    public static void FullyForbiddenStockIsForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> colony = [NewThing(thingDef, 50)];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            thing => true
        );

        Assert.That(forbidden).Is.True();
    }

    // Exactly enough non-forbidden stock is not a shortfall.
    [Test]
    public static void ExactlyEnoughUnforbiddenStockIsNotForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        var forbiddenThing = NewThing(thingDef, 40);
        List<Thing> colony = [NewThing(thingDef, 10), forbiddenThing];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            thing => thing == forbiddenThing
        );

        Assert.That(forbidden).Is.False();
    }

    // Genuinely missing (not enough stock even counting the forbidden portion) is
    // AnyRequiredItemMissing's job, not this one - it must not also report "forbidden" here.
    [Test]
    public static void GenuinelyMissingStockIsNotReportedAsForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> colony = [NewThing(thingDef, 4)];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            thing => true
        );

        Assert.That(forbidden).Is.False();
    }

    // Stock already split off and being hauled toward the transporter keeps getting delivered
    // even if it's forbidden mid-haul (JobDriver_HaulMultipleToTransporter's job.ignoreForbidden) -
    // it must always count toward the requirement and never trigger this warning by itself.
    [Test]
    public static void InFlightStockAlwaysCountsRegardlessOfForbiddenStatus()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> inFlight = [NewThing(thingDef, 10)];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            [],
            inFlight,
            thing => true
        );

        Assert.That(forbidden).Is.False();
    }

    // In-flight stock reduces what's still needed from the colony - only the remainder has to
    // clear the forbidden check.
    [Test]
    public static void InFlightStockReducesWhatMustSurviveTheForbiddenCheck()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> inFlight = [NewThing(thingDef, 8)];
        var forbiddenThing = NewThing(thingDef, 5);
        List<Thing> colony = [NewThing(thingDef, 2), forbiddenThing];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            inFlight,
            thing => thing == forbiddenThing
        );

        Assert.That(forbidden).Is.False();
    }

    [Test]
    public static void AlreadyFullyLoadedRequirementIsNotForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 0 }];
        List<Thing> colony = [];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            NeverForbidden
        );

        Assert.That(forbidden).Is.False();
    }

    [Test]
    public static void OneForbiddenRequirementAmongOthersIsEnough()
    {
        var coveredDef = new ThingDef { defName = "CoveredThingDef" };
        var forbiddenDef = new ThingDef { defName = "ForbiddenThingDef" };
        List<ThingDefCountWithRequirements> required =
        [
            new() { def = coveredDef, count = 5 },
            new() { def = forbiddenDef, count = 5 },
        ];
        List<Thing> colony = [NewThing(coveredDef, 5), NewThing(forbiddenDef, 5)];

        var forbidden = CompTradeShuttle.AnyRequiredItemForbidden(
            required,
            colony,
            [],
            thing => thing.def == forbiddenDef
        );

        Assert.That(forbidden).Is.True();
    }
}
