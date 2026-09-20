using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class TryReduceRequiredCountTests
{
    // Regression guard: ThingDefCountWithRequirements is a struct, so mutating the
    // `count` field of a value pulled out of the list via LINQ only changed a local copy and was
    // never written back, silently losing a partial reduction of a quality/health/minified
    // requirement entry.
    [Test]
    public static void PartialReductionIsWrittenBackToTheList()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        Thing thing = new() { def = thingDef };
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new() { def = thingDef, count = 10 },
        ];
        var leftToTakeOff = 4;

        var found = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thing,
            ref leftToTakeOff
        );

        Expect.IsTrue(found);
        Expect.AreEqual(0, leftToTakeOff);
        Expect.AreEqual(1, requiredSpecificItems.Count);
        Expect.AreEqual(6, requiredSpecificItems[0].count);
    }

    [Test]
    public static void FullReductionRemovesTheEntry()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        Thing thing = new() { def = thingDef };
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new() { def = thingDef, count = 4 },
        ];
        var leftToTakeOff = 4;

        var found = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thing,
            ref leftToTakeOff
        );

        Expect.IsTrue(found);
        Expect.AreEqual(0, leftToTakeOff);
        Expect.AreEqual(0, requiredSpecificItems.Count);
    }

    [Test]
    public static void OverReductionRemovesTheEntryAndReturnsRemainder()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        Thing thing = new() { def = thingDef };
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new() { def = thingDef, count = 4 },
        ];
        var leftToTakeOff = 6;

        var found = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thing,
            ref leftToTakeOff
        );

        Expect.IsTrue(found);
        Expect.AreEqual(2, leftToTakeOff);
        Expect.AreEqual(0, requiredSpecificItems.Count);
    }

    [Test]
    public static void NoMatchingEntryReturnsFalse()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var otherThingDef = new ThingDef { defName = "OtherTestThingDef" };
        Thing thing = new() { def = thingDef };
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new() { def = otherThingDef, count = 4 },
        ];
        var leftToTakeOff = 2;

        var found = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thing,
            ref leftToTakeOff
        );

        Expect.IsFalse(found);
        Expect.AreEqual(2, leftToTakeOff);
    }

    // Regression guard: the partial-reduction path preserves the requirement fields
    // (healthAffectsPrice, hitPoints, hasQuality, quality) on the written-back entry, since the
    // fix mutates the struct copy in place rather than reconstructing it via WithCount (which
    // drops some fields).
    [Test]
    public static void PartialReductionPreservesRequirementFields()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        ThingWithComps thing = new() { def = thingDef, HitPoints = 50 };
        CompQuality compQuality = new() { parent = thing };
        compQuality.SetQuality(QualityCategory.Excellent, null);
        thing.compQuality = compQuality;

        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new()
            {
                def = thingDef,
                count = 10,
                healthAffectsPrice = true,
                hitPoints = 50,
                maxHitPoints = 100,
                hasQuality = true,
                quality = QualityCategory.Excellent,
            },
        ];
        var leftToTakeOff = 3;

        _ = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thing,
            ref leftToTakeOff
        );

        var remaining = requiredSpecificItems[0];
        Expect.AreEqual(7, remaining.count);
        Expect.IsTrue(remaining.healthAffectsPrice);
        Expect.AreEqual(50, remaining.hitPoints);
        Expect.AreEqual(100, remaining.maxHitPoints);
        Expect.IsTrue(remaining.hasQuality);
        Expect.AreEqual((int)QualityCategory.Excellent, (int)remaining.quality);
    }

    // Regression guard: with genepacks bound to their exact Thing via
    // requiredThing, reducing must target the entry bound to the specific Thing being removed,
    // not merely the first entry sharing the same def - otherwise unselling one genepack during
    // renegotiation could eject/keep the wrong physical genepack.
    [Test]
    public static void RequiredThingBindingOnlyReducesTheEntryBoundToThatExactThing()
    {
        var thingDef = new ThingDef { defName = "TestGenepackDef" };
        Thing thingA = new() { def = thingDef };
        Thing thingB = new() { def = thingDef };
        List<ThingDefCountWithRequirements> requiredSpecificItems =
        [
            new()
            {
                def = thingDef,
                count = 1,
                requiredThing = thingA,
            },
            new()
            {
                def = thingDef,
                count = 1,
                requiredThing = thingB,
            },
        ];
        var leftToTakeOff = 1;

        var found = ThingDefCountWithRequirements.TryReduceRequiredCount(
            requiredSpecificItems,
            thingB,
            ref leftToTakeOff
        );

        Expect.IsTrue(found);
        Expect.AreEqual(0, leftToTakeOff);
        Expect.AreEqual(1, requiredSpecificItems.Count);
        Expect.IsTrue(requiredSpecificItems[0].requiredThing == thingA);
    }
}
