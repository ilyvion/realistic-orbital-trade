using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class TryReduceRequiredCountTests
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

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(0);
        Assert.That(requiredSpecificItems.Count).Is.EqualTo(1);
        Assert.That(requiredSpecificItems[0].count).Is.EqualTo(6);
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

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(0);
        Assert.That(requiredSpecificItems.Count).Is.EqualTo(0);
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

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(2);
        Assert.That(requiredSpecificItems.Count).Is.EqualTo(0);
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

        Assert.That(found).Is.False();
        Assert.That(leftToTakeOff).Is.EqualTo(2);
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
        Assert.That(remaining.count).Is.EqualTo(7);
        Assert.That(remaining.healthAffectsPrice).Is.True();
        Assert.That(remaining.hitPoints).Is.EqualTo(50);
        Assert.That(remaining.maxHitPoints).Is.EqualTo(100);
        Assert.That(remaining.hasQuality).Is.True();
        Assert.That((int)remaining.quality).Is.EqualTo((int)QualityCategory.Excellent);
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

        Assert.That(found).Is.True();
        Assert.That(leftToTakeOff).Is.EqualTo(0);
        Assert.That(requiredSpecificItems.Count).Is.EqualTo(1);
        Assert.That(requiredSpecificItems[0].requiredThing == thingA).Is.True();
    }
}
