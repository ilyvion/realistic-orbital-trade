using DevTools.Testing;
using RealisticOrbitalTrade.Dialogs;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: AccumulateThingCounts (Dialog_PayBlacklistRemovalFee.GetThingsToReturn) used
// to build its Dictionary<ThingDef, int> via plain Dictionary.Add, which throws ArgumentException
// the moment the same ThingDef shows up twice - e.g. if ROT_TradeShuttle.killedLeavings ever grew
// a second entry resolving to a def already seen, or a parts payment happened to be silver.
[TestFixture(TestType.MainMenu)]
internal sealed class AccumulateThingCountsTests
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

        Expect.AreEqual(15, result[steel]);
        Expect.AreEqual(100, result[silver]);
        Expect.AreEqual(2, result.Count);
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

        Expect.AreEqual(125, result[silver]);
        Expect.AreEqual(1, result.Count);
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

        Expect.AreEqual(10, result[steel]);
        Expect.AreEqual(3, result[plasteel]);
        Expect.AreEqual(100, result[silver]);
        Expect.AreEqual(3, result.Count);
    }
}
