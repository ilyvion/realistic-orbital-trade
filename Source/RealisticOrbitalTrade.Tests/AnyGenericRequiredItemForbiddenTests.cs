using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Same coverage as AnyRequiredItemForbiddenTests, for the plain ThingDef-counted requirements
// vanilla's own CompShuttle.requiredItems tracks (wood, steel, meals, etc.).
[TestFixture(TestType.MainMenu)]
internal sealed class AnyGenericRequiredItemForbiddenTests
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
    public static void PartiallyForbiddenStockStillCoveringRequirementIsNotForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        var forbiddenThing = NewThing(thingDef, 20);
        List<Thing> colony = [NewThing(thingDef, 30), forbiddenThing];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            colony,
            [],
            thing => thing == forbiddenThing
        );

        Expect.IsFalse(forbidden);
    }

    [Test]
    public static void FullyForbiddenStockIsForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> colony = [NewThing(thingDef, 50)];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            colony,
            [],
            thing => true
        );

        Expect.IsTrue(forbidden);
    }

    [Test]
    public static void GenuinelyMissingStockIsNotReportedAsForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> colony = [NewThing(thingDef, 4)];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            colony,
            [],
            thing => true
        );

        Expect.IsFalse(forbidden);
    }

    [Test]
    public static void InFlightStockAlwaysCountsRegardlessOfForbiddenStatus()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> inFlight = [NewThing(thingDef, 10)];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            [],
            inFlight,
            thing => true
        );

        Expect.IsFalse(forbidden);
    }

    [Test]
    public static void AlreadyFullyLoadedRequirementIsNotForbidden()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 0)];
        List<Thing> colony = [];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            colony,
            [],
            thing => false
        );

        Expect.IsFalse(forbidden);
    }

    [Test]
    public static void OneForbiddenRequirementAmongOthersIsEnough()
    {
        var coveredDef = new ThingDef { defName = "CoveredThingDef" };
        var forbiddenDef = new ThingDef { defName = "ForbiddenThingDef" };
        List<ThingDefCount> required = [new(coveredDef, 5), new(forbiddenDef, 5)];
        List<Thing> colony = [NewThing(coveredDef, 5), NewThing(forbiddenDef, 5)];

        var forbidden = CompTradeShuttle.AnyGenericRequiredItemForbidden(
            required,
            colony,
            [],
            thing => thing.def == forbiddenDef
        );

        Expect.IsTrue(forbidden);
    }
}
