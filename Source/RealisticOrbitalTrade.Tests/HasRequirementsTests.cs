using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: a genepack has no quality, no
// healthAffectsPrice, and isn't minified, so without GeneSetHolderBase being special-cased here
// it fell through to HasRequirements() == false and got treated as interchangeable with any
// other genepack throughout ApplyRenegotiation's hard-case removal - unselling one specific
// genepack during renegotiation ejected every loaded genepack instead of just that one, and the
// reload afterward grabbed whichever genepacks happened to be nearest rather than the ones
// actually still owed.
[TestSuite]
internal static class HasRequirementsTests
{
    [Test]
    public static void GenepackHasRequirements()
    {
        Genepack genepack = new()
        {
            def = new ThingDef { defName = "TestGenepack", healthAffectsPrice = false },
        };

        Assert.That(genepack.HasRequirements()).Is.True();
    }

    [Test]
    public static void GenepackHasRequirementsOutParamsFlagsGeneSetHolder()
    {
        Genepack genepack = new()
        {
            def = new ThingDef { defName = "TestGenepack", healthAffectsPrice = false },
        };

        var result = genepack.HasRequirements(
            out var healthAffectsPrice,
            out var qualityCategory,
            out var hasInnerThing
        );

        Assert.That(result).Is.True();
        Assert.That(healthAffectsPrice).Is.False();
        Assert.That(qualityCategory.HasValue).Is.False();
        Assert.That(hasInnerThing).Is.False();
    }

    [Test]
    public static void PlainThingWithNoQualityOrHealthPriceHasNoRequirements()
    {
        Thing thing = new()
        {
            def = new ThingDef { defName = "TestPlainThing", healthAffectsPrice = false },
        };

        Assert.That(thing.HasRequirements()).Is.False();
    }
}
