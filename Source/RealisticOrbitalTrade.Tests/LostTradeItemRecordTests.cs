using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard for TradeAgreement.lostSoldItems, which lets CompTradeShuttle's inspect string
// and the shuttle's contents tab tell the player *what* was lost from a sold-to-trader deal
// instead of just that something was (see TradeAgreement.Notify_OwnThingDestroyed).
[TestFixture(TestType.MainMenu)]
internal sealed class LostTradeItemRecordTests
{
    [Test]
    public static void ForThingCapturesDefStuffQualityAndTheGivenCount()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var stuffDef = new ThingDef { defName = "TestStuffDef" };
        ThingWithComps thing = new() { def = thingDef };
        thing.SetStuffDirect(stuffDef);
        CompQuality compQuality = new() { parent = thing };
        compQuality.SetQuality(QualityCategory.Excellent, null);
        thing.compQuality = compQuality;

        // The lost amount has to be passed in explicitly rather than read off the thing: by the
        // time Notify_OwnThingDestroyed's postfix runs, Thing.Destroy has already zeroed
        // thing.stackCount (see that method's remark).
        var record = TradeAgreement.LostTradeItemRecord.ForThing(thing, 7);

        Expect.IsTrue(record.def == thingDef);
        Expect.IsTrue(record.stuffDef == stuffDef);
        Expect.IsTrue(record.hasQuality);
        Expect.AreEqual((int)QualityCategory.Excellent, (int)record.quality);
        Expect.AreEqual(7, record.count);
        Expect.IsTrue(record.pawnLabel == null);
    }

    [Test]
    public static void ForThingWithoutAQualityCompReportsNoQuality()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        Thing thing = new() { def = thingDef };

        var record = TradeAgreement.LostTradeItemRecord.ForThing(thing, 3);

        Expect.IsFalse(record.hasQuality);
    }

    [Test]
    public static void AddOrMergeSumsCountsForMatchingDefStuffAndQuality()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<TradeAgreement.LostTradeItemRecord> records = [new() { def = thingDef, count = 5 }];

        TradeAgreement.LostTradeItemRecord.AddOrMerge(records, new() { def = thingDef, count = 3 });

        Expect.AreEqual(1, records.Count);
        Expect.AreEqual(8, records[0].count);
    }

    // Regression guard: two different stuff/quality variants of the same def losing items in the
    // same trade must stay as separate rows - merging them would hide that, e.g., both a normal
    // and an excellent-quality copy of the same item were lost.
    [Test]
    public static void AddOrMergeKeepsDifferingStuffAsSeparateEntries()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var stuffDefA = new ThingDef { defName = "StuffA" };
        var stuffDefB = new ThingDef { defName = "StuffB" };
        List<TradeAgreement.LostTradeItemRecord> records =
        [
            new()
            {
                def = thingDef,
                stuffDef = stuffDefA,
                count = 1,
            },
        ];

        TradeAgreement.LostTradeItemRecord.AddOrMerge(
            records,
            new()
            {
                def = thingDef,
                stuffDef = stuffDefB,
                count = 1,
            }
        );

        Expect.AreEqual(2, records.Count);
    }

    [Test]
    public static void AddOrMergeKeepsDifferingQualityAsSeparateEntries()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        List<TradeAgreement.LostTradeItemRecord> records =
        [
            new()
            {
                def = thingDef,
                hasQuality = true,
                quality = QualityCategory.Normal,
                count = 1,
            },
        ];

        TradeAgreement.LostTradeItemRecord.AddOrMerge(
            records,
            new()
            {
                def = thingDef,
                hasQuality = true,
                quality = QualityCategory.Excellent,
                count = 1,
            }
        );

        Expect.AreEqual(2, records.Count);
    }

    // Regression guard: two different pawns can coincidentally share a label (e.g. two animals
    // both named "Rex") - merging their records by label would misrepresent them as a single
    // stack of count 2 instead of two distinct lost creatures.
    [Test]
    public static void AddOrMergeNeverMergesPawnRecordsEvenWithTheSameLabel()
    {
        List<TradeAgreement.LostTradeItemRecord> records = [new() { pawnLabel = "Rex" }];

        TradeAgreement.LostTradeItemRecord.AddOrMerge(records, new() { pawnLabel = "Rex" });

        Expect.AreEqual(2, records.Count);
    }

    [Test]
    public static void AddOrMergeAddsANewEntryWhenNothingMatches()
    {
        List<TradeAgreement.LostTradeItemRecord> records = [];
        var thingDef = new ThingDef { defName = "TestThingDef" };

        TradeAgreement.LostTradeItemRecord.AddOrMerge(records, new() { def = thingDef, count = 2 });

        Expect.AreEqual(1, records.Count);
        Expect.AreEqual(2, records[0].count);
    }
}
