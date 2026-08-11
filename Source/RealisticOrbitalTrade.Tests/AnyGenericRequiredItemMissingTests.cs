using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for the bug where a shortfall in Shuttle's own plain ThingDef-counted
// requiredItems (wood, steel, meals, etc. - as opposed to this mod's own per-instance-tracked
// requiredSpecificItems, covered by AnyRequiredItemMissingTests) never surfaced the ROT_TradeIssue
// overlay: CompTradeShuttle.CheckAutoload only ever ran AnyRequiredItemMissing against
// requiredSpecificItems, so a def-counted requirement that became genuinely unfindable (e.g. a
// required stack picked up into a non-hauling colonist's inventory) was silently never flagged.
[TestSuite]
internal static class AnyGenericRequiredItemMissingTests
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
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> sendable = [NewThing(thingDef, 10)];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.False();
    }

    [Test]
    public static void RequirementCoveredAcrossMultipleStacksIsNotMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> sendable = [NewThing(thingDef, 4), NewThing(thingDef, 6)];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.False();
    }

    [Test]
    public static void RequirementWithNoMatchingStockAtAllIsMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> sendable = [];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.True();
    }

    [Test]
    public static void RequirementPartiallyShortIsMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 10)];
        List<Thing> sendable = [NewThing(thingDef, 4)];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.True();
    }

    // The exact reported scenario: a stack matching a required def exists, but only inside a
    // non-hauling pawn's inventory - it never enters sendableItems (ColonyThingsTraderWillingToBuy
    // doesn't scan pawn inventories, and ThingsCarriedTowardTransporter only follows pawns actively
    // hauling to this transporter), so the requirement is short even though the def matches.
    [Test]
    public static void StockHeldByNonHaulingPawnIsMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> sendable = [];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.True();
    }

    [Test]
    public static void AlreadyFullyLoadedRequirementIsNotMissing()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 0)];
        List<Thing> sendable = [];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.False();
    }

    [Test]
    public static void OneMissingRequirementAmongOthersIsEnough()
    {
        var coveredDef = new ThingDef { defName = "CoveredThingDef" };
        var missingDef = new ThingDef { defName = "MissingThingDef" };
        List<ThingDefCount> required = [new(coveredDef, 5), new(missingDef, 5)];
        List<Thing> sendable = [NewThing(coveredDef, 5)];

        var missing = CompTradeShuttle.AnyGenericRequiredItemMissing(required, sendable);

        Assert.That(missing).Is.True();
    }
}
