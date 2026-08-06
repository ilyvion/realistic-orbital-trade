using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class TryReduceRequiredItemsCountTests
{
    [Test]
    public static void PartialReductionIsWrittenBackToTheList()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        var leftToTakeOff = 4;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(0);
        Assert.That(requiredItems.Count).Is.EqualTo(1);
        Assert.That(requiredItems[0].Count).Is.EqualTo(6);
    }

    [Test]
    public static void FullReductionRemovesTheEntry()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 4)];
        var leftToTakeOff = 4;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(0);
        Assert.That(requiredItems.Count).Is.EqualTo(0);
    }

    [Test]
    public static void OverReductionRemovesTheEntryAndReturnsRemainder()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 4)];
        var leftToTakeOff = 6;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(2);
        Assert.That(requiredItems.Count).Is.EqualTo(0);
    }

    [Test]
    public static void NoMatchingEntryReturnsFalse()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var otherThingDef = new ThingDef { defName = "OtherTestThingDef" };
        List<ThingDefCount> requiredItems = [new(otherThingDef, 4)];
        var leftToTakeOff = 2;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Assert.That(found).Is.False();
        Assert.That(leftToTakeOff).Is.EqualTo(2);
    }
}
