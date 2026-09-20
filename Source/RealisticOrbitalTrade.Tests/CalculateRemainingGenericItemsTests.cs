using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Coverage for CompTradeShuttle.CalculateRemainingGenericItems, which reimplements vanilla
// CompShuttle.CheckAutoload's own local tmpRequiredItems reduction (not exposed for reuse) so
// AnyGenericRequiredItemMissing can be checked against what's still actually owed, not the full
// original requirement.
[TestFixture(TestType.MainMenu)]
internal sealed class CalculateRemainingGenericItemsTests
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
    public static void NothingLoadedLeavesFullRequirement()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, []);

        Expect.AreEqual(75, remaining[0].Count);
    }

    [Test]
    public static void PartiallyLoadedReducesByLoadedAmount()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> loaded = [NewThing(thingDef, 30)];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, loaded);

        Expect.AreEqual(45, remaining[0].Count);
    }

    [Test]
    public static void FullyLoadedReducesToZero()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> loaded = [NewThing(thingDef, 75)];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, loaded);

        Expect.AreEqual(0, remaining[0].Count);
    }

    // Vanilla's own reduction floors at zero rather than letting a loaded surplus carry over to
    // reduce some other requirement's count.
    [Test]
    public static void OverLoadedDoesNotGoNegative()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> loaded = [NewThing(thingDef, 100)];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, loaded);

        Expect.AreEqual(0, remaining[0].Count);
    }

    [Test]
    public static void NonMatchingLoadedThingIsIgnored()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var otherDef = new ThingDef { defName = "OtherThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> loaded = [NewThing(otherDef, 75)];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, loaded);

        Expect.AreEqual(75, remaining[0].Count);
    }

    // A pawn riding along as required cargo (e.g. a required animal) must not be treated as a
    // stack of loaded items - CompShuttle.innerContainer holds both, and vanilla's own reduction
    // explicitly skips Pawn entries for this same reason.
    [Test]
    public static void PawnInLoadedThingsIsSkipped()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> required = [new(thingDef, 75)];
        List<Thing> loaded = [new Pawn()];

        var remaining = CompTradeShuttle.CalculateRemainingGenericItems(required, loaded);

        Expect.AreEqual(75, remaining[0].Count);
    }
}
