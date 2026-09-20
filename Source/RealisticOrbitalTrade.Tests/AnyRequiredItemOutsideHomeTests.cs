using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// A requirement that colonists can't cover from home-area/storage stock, while matching items lie
// outside both, must surface the ROT_TradeOutsideHome overlay rather than only the generic
// 'missing' one.
[TestSuite]
internal static class AnyRequiredItemOutsideHomeTests
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
    public static void GenericShortfallWithStockOutsideHomeIsDetected()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 150)];

        var outside = CompTradeShuttle.AnyRequiredItemOutsideHome(
            [],
            required,
            [NewThing(thingDef, 10)],
            [NewThing(thingDef, 75), NewThing(thingDef, 75)]
        );

        Assert.That(outside).Is.True();
    }

    [Test]
    public static void GenericShortfallWithNoStockOutsideHomeIsNotDetected()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 150)];

        var outside = CompTradeShuttle.AnyRequiredItemOutsideHome(
            [],
            required,
            [NewThing(thingDef, 10)],
            [NewThing(new ThingDef { defName = "OtherDef" }, 500)]
        );

        Assert.That(outside).Is.False();
    }

    [Test]
    public static void CoveredRequirementIsNotDetectedEvenWithStockOutsideHome()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 150)];

        var outside = CompTradeShuttle.AnyRequiredItemOutsideHome(
            [],
            required,
            [NewThing(thingDef, 200)],
            [NewThing(thingDef, 75)]
        );

        Assert.That(outside).Is.False();
    }

    [Test]
    public static void SpecificShortfallWithMatchingStockOutsideHomeIsDetected()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 5 }];

        var outside = CompTradeShuttle.AnyRequiredItemOutsideHome(
            required,
            [],
            [],
            [NewThing(thingDef, 5)]
        );

        Assert.That(outside).Is.True();
    }
}
