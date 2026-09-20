using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: a genepack has no quality, no
// healthAffectsPrice, and isn't minified, so without GeneSetHolderBase being special-cased here
// it fell through to HasRequirements() == false and got treated as interchangeable with any
// other genepack throughout ApplyRenegotiation's hard-case removal - unselling one specific
// genepack during renegotiation ejected every loaded genepack instead of just that one, and the
// reload afterward grabbed whichever genepacks happened to be nearest rather than the ones
// actually still owed.
[TestFixture(TestType.MainMenu)]
internal sealed class HasRequirementsTests
{
    [Test]
    public static void GenepackHasRequirements()
    {
        Genepack genepack = new()
        {
            def = new ThingDef { defName = "TestGenepack", healthAffectsPrice = false },
        };

        Expect.IsTrue(genepack.HasRequirements());
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

        Expect.IsTrue(result);
        Expect.IsFalse(healthAffectsPrice);
        Expect.IsFalse(qualityCategory.HasValue);
        Expect.IsFalse(hasInnerThing);
    }

    [Test]
    public static void PlainThingWithNoQualityOrHealthPriceHasNoRequirements()
    {
        Thing thing = new()
        {
            def = new ThingDef { defName = "TestPlainThing", healthAffectsPrice = false },
        };

        Expect.IsFalse(thing.HasRequirements());
    }
}
