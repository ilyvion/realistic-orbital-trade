using RealisticOrbitalTrade.Dialogs;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: AccumulateThingCounts (Dialog_PayBlacklistRemovalFee.GetThingsToReturn) used
// to build its Dictionary<ThingDef, int> via plain Dictionary.Add, which throws ArgumentException
// the moment the same ThingDef shows up twice - e.g. if ROT_TradeShuttle.killedLeavings ever grew
// a second entry resolving to a def already seen, or a parts payment happened to be silver.
[TestSuite]
internal static class AccumulateThingCountsTests
{
    [Test]
    public static void DuplicateThingDefCountsAreSummedInsteadOfThrowing()
    {
        var steel = new ThingDef { defName = "TestSteel" };
        var silver = new ThingDef { defName = "TestSilver" };

        var result = Dialog_PayBlacklistRemovalFee.AccumulateThingCounts(
            [(steel, 10), (steel, 5)],
            silver,
            100
        );

        Assert.That(result[steel]).Is.EqualTo(15);
        Assert.That(result[silver]).Is.EqualTo(100);
        Assert.That(result.Count).Is.EqualTo(2);
    }

    [Test]
    public static void SilverCountIsAddedToAnExistingPartsPaymentOfSilver()
    {
        var silver = new ThingDef { defName = "TestSilver" };

        var result = Dialog_PayBlacklistRemovalFee.AccumulateThingCounts(
            [(silver, 25)],
            silver,
            100
        );

        Assert.That(result[silver]).Is.EqualTo(125);
        Assert.That(result.Count).Is.EqualTo(1);
    }

    [Test]
    public static void DistinctThingDefsEachKeepTheirOwnCount()
    {
        var steel = new ThingDef { defName = "TestSteel" };
        var plasteel = new ThingDef { defName = "TestPlasteel" };
        var silver = new ThingDef { defName = "TestSilver" };

        var result = Dialog_PayBlacklistRemovalFee.AccumulateThingCounts(
            [(steel, 10), (plasteel, 3)],
            silver,
            100
        );

        Assert.That(result[steel]).Is.EqualTo(10);
        Assert.That(result[plasteel]).Is.EqualTo(3);
        Assert.That(result[silver]).Is.EqualTo(100);
        Assert.That(result.Count).Is.EqualTo(3);
    }
}
