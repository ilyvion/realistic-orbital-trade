using DevTools.Testing;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class WithCountTests
{
    // Regression guard: WithCount only copied def, count, healthAffectsPrice, hitPoints,
    // hasQuality, and quality, silently dropping stuffDef, isInnerThing, and maxHitPoints on the
    // returned copy. That degraded requirement labels (missing stuff name/health %) and broke
    // Matches() for stuffed stackables and minified things once a requirement entry had been
    // partially satisfied.
    [Test]
    public static void CopiesAllFields()
    {
        var thingDef = new ThingDef { defName = "TestThingDef" };
        var stuffDef = new ThingDef { defName = "TestStuffDef" };
        Thing requiredThing = new() { def = thingDef };
        ThingDefCountWithRequirements original = new()
        {
            def = thingDef,
            stuffDef = stuffDef,
            isInnerThing = true,
            count = 10,
            healthAffectsPrice = true,
            hitPoints = 50,
            maxHitPoints = 100,
            hasQuality = true,
            quality = QualityCategory.Excellent,
            requiredThing = requiredThing,
        };

        var copy = original.WithCount(4);

        Expect.IsTrue(copy.def == thingDef);
        Expect.IsTrue(copy.stuffDef == stuffDef);
        Expect.IsTrue(copy.isInnerThing);
        Expect.AreEqual(4, copy.count);
        Expect.IsTrue(copy.healthAffectsPrice);
        Expect.AreEqual(50, copy.hitPoints);
        Expect.AreEqual(100, copy.maxHitPoints);
        Expect.IsTrue(copy.hasQuality);
        Expect.AreEqual((int)QualityCategory.Excellent, (int)copy.quality);
        Expect.IsTrue(copy.requiredThing == requiredThing);
    }
}
