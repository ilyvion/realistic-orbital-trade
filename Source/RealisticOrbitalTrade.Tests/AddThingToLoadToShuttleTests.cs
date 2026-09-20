using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: each sold item with requirements (quality/health-tracked apparel, etc.) got
// its own requiredSpecificItems entry, even when several sold items were otherwise identical
// (same def/stuff/quality/health). CompTradeShuttle.CheckAutoload's UpdateToLoadList corrects a
// matching "To load" row to whichever single requirement entry it's currently processing rather
// than summing across every entry that maps onto that row, so several count-1 entries sharing the
// same matching stock collapsed into a single row capped at 1 - selling e.g. 7 identical-quality
// parkas showed and hauled only 1 at a time, needing 7 separate trips instead of one.
[TestFixture(TestType.MainMenu)]
internal sealed class AddThingToLoadToShuttleTests
{
    private static int _nextThingId;

    private static ThingWithComps NewApparel(ThingDef def, ThingDef stuff, QualityCategory quality)
    {
        ThingWithComps thing = new() { def = def, thingIDNumber = _nextThingId++ };
        thing.SetStuffDirect(stuff);
        CompQuality compQuality = new() { parent = thing };
        compQuality.SetQuality(quality, null);
        thing.compQuality = compQuality;
        return thing;
    }

    [Test]
    public static void IdenticalSoldItemsMergeIntoOneEntryWithSummedCount()
    {
        var apparelDef = RimWorld.ThingDefOf.Apparel_Tuque;
        var thingA = NewApparel(apparelDef, RimWorld.ThingDefOf.Cloth, QualityCategory.Normal);
        var thingB = NewApparel(apparelDef, RimWorld.ThingDefOf.Cloth, QualityCategory.Normal);
        CompTradeShuttle compTradeShuttle = new();
        CompShuttle compShuttle = new();

        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(thingA, 1),
            compTradeShuttle,
            compShuttle
        );
        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(thingB, 1),
            compTradeShuttle,
            compShuttle
        );

        Expect.AreEqual(1, compTradeShuttle.requiredSpecificItems.Count);
        Expect.AreEqual(2, compTradeShuttle.requiredSpecificItems[0].count);
    }

    [Test]
    public static void DifferingQualityKeepsSeparateEntries()
    {
        var apparelDef = RimWorld.ThingDefOf.Apparel_Tuque;
        var normal = NewApparel(apparelDef, RimWorld.ThingDefOf.Cloth, QualityCategory.Normal);
        var excellent = NewApparel(
            apparelDef,
            RimWorld.ThingDefOf.Cloth,
            QualityCategory.Excellent
        );
        CompTradeShuttle compTradeShuttle = new();
        CompShuttle compShuttle = new();

        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(normal, 1),
            compTradeShuttle,
            compShuttle
        );
        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(excellent, 1),
            compTradeShuttle,
            compShuttle
        );

        Expect.AreEqual(2, compTradeShuttle.requiredSpecificItems.Count);
        Expect.AreEqual(1, compTradeShuttle.requiredSpecificItems[0].count);
        Expect.AreEqual(1, compTradeShuttle.requiredSpecificItems[1].count);
    }

    [Test]
    public static void RequiredThingEntriesNeverMergeEvenWhenOtherwiseIdentical()
    {
        var genepackDef = new ThingDef { defName = "TestGenepackDefForMergeTest" };
        Genepack genepackA = new() { def = genepackDef, thingIDNumber = _nextThingId++ };
        Genepack genepackB = new() { def = genepackDef, thingIDNumber = _nextThingId++ };
        CompTradeShuttle compTradeShuttle = new();
        CompShuttle compShuttle = new();

        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(genepackA, 1),
            compTradeShuttle,
            compShuttle
        );
        Utils.AddThingToLoadToShuttle(
            new ThingCountClass(genepackB, 1),
            compTradeShuttle,
            compShuttle
        );

        Expect.AreEqual(2, compTradeShuttle.requiredSpecificItems.Count);
    }
}
