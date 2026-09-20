using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class TryReduceRequiredItemsCountTests
{
    [Test]
    public static void PartialReductionIsWrittenBackToTheList()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 10)];
        var leftToTakeOff = 4;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Expect.IsTrue(found);
        Expect.AreEqual(0, leftToTakeOff);
        Expect.AreEqual(1, requiredItems.Count);
        Expect.AreEqual(6, requiredItems[0].Count);
    }

    [Test]
    public static void FullReductionRemovesTheEntry()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 4)];
        var leftToTakeOff = 4;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Expect.IsTrue(found);
        Expect.AreEqual(0, leftToTakeOff);
        Expect.AreEqual(0, requiredItems.Count);
    }

    [Test]
    public static void OverReductionRemovesTheEntryAndReturnsRemainder()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<ThingDefCount> requiredItems = [new(thingDef, 4)];
        var leftToTakeOff = 6;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Expect.IsTrue(found);
        Expect.AreEqual(2, leftToTakeOff);
        Expect.AreEqual(0, requiredItems.Count);
    }

    [Test]
    public static void NoMatchingEntryReturnsFalse()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var otherThingDef = new ThingDef { defName = "OtherTestThingDef" };
        List<ThingDefCount> requiredItems = [new(otherThingDef, 4)];
        var leftToTakeOff = 2;

        var found = Utils.TryReduceRequiredItemsCount(requiredItems, thingDef, ref leftToTakeOff);

        Expect.IsFalse(found);
        Expect.AreEqual(2, leftToTakeOff);
    }
}
