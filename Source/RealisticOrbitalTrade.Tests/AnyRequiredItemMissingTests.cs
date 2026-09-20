using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for missing-items detection: CompTradeShuttle.CheckAutoload must
// distinguish "still needed but not yet hauled" (sendableItems already covers the requirement,
// via colony stock or an in-flight haul) from "genuinely can't be found anywhere" (consumed,
// worn, hidden away, etc.), since only the latter should surface the ROT_TradeMissing overlay.
[TestFixture(TestType.MainMenu)]
internal sealed class AnyRequiredItemMissingTests
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
    public static void FullyCoveredRequirementIsNotMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> sendable = [NewThing(thingDef, 10)];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsFalse(missing);
    }

    // A requirement not yet hauled still shows up as available stock (or as something already
    // split off and carried toward the transporter) in sendableItems - that's not "missing", just
    // not yet picked up.
    [Test]
    public static void RequirementCoveredAcrossMultipleStacksIsNotMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> sendable = [NewThing(thingDef, 4), NewThing(thingDef, 6)];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsFalse(missing);
    }

    [Test]
    public static void RequirementWithNoMatchingStockAtAllIsMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> sendable = [];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsTrue(missing);
    }

    [Test]
    public static void RequirementPartiallyShortIsMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 10 }];
        List<Thing> sendable = [NewThing(thingDef, 4)];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsTrue(missing);
    }

    // A requirement already fully satisfied (count reduced to 0 by CalculateRemainingRequiredItems
    // before this runs) must not be treated as missing just because nothing sendable matches it
    // anymore.
    [Test]
    public static void AlreadyFullyLoadedRequirementIsNotMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCountWithRequirements> required = [new() { def = thingDef, count = 0 }];
        List<Thing> sendable = [];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsFalse(missing);
    }

    [Test]
    public static void OneMissingRequirementAmongOthersIsEnough()
    {
        var coveredDef = new ThingDef { defName = "CoveredThingDef" };
        var missingDef = new ThingDef { defName = "MissingThingDef" };
        List<ThingDefCountWithRequirements> required =
        [
            new() { def = coveredDef, count = 5 },
            new() { def = missingDef, count = 5 },
        ];
        List<Thing> sendable = [NewThing(coveredDef, 5)];

        var missing = CompTradeShuttle.AnyRequiredItemMissing(required, sendable);

        Expect.IsTrue(missing);
    }
}
