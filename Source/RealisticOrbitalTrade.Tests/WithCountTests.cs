using RealisticOrbitalTrade.Comps;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class WithCountTests
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
        };

        var copy = original.WithCount(4);

        Assert.That(copy.def == thingDef).Is.True();
        Assert.That(copy.stuffDef == stuffDef).Is.True();
        Assert.That(copy.isInnerThing).Is.True();
        Assert.That(copy.count).Is.EqualTo(4);
        Assert.That(copy.healthAffectsPrice).Is.True();
        Assert.That(copy.hitPoints).Is.EqualTo(50);
        Assert.That(copy.maxHitPoints).Is.EqualTo(100);
        Assert.That(copy.hasQuality).Is.True();
        Assert.That((int)copy.quality).Is.EqualTo((int)QualityCategory.Excellent);
    }
}
