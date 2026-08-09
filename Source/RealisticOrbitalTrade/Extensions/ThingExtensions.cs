namespace RealisticOrbitalTrade;

internal static class ThingExtensions
{
    // Genepacks/xenogerms (RimWorld.GeneSetHolderBase) have no quality, no health-affects-price,
    // and aren't minified - but each instance carries its own distinct, non-interchangeable gene
    // set (stackLimit is 1, so the game never lets two of them merge either), so they need the
    // same exact-Thing-reference tracking as quality/health-tracked items rather than being
    // treated as fungible with any other item sharing their def.
    public static bool HasRequirements(this Thing thing)
    {
        var healthAffectsPrice = thing.GetInnerIfMinified().def.healthAffectsPrice;
        var hasQuality = QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var _);
        return healthAffectsPrice || hasQuality || thing is MinifiedThing or GeneSetHolderBase;
    }

    public static bool HasRequirements(
        this Thing thing,
        out bool healthAffectsPrice,
        out QualityCategory? qualityCategory,
        out bool hasInnerThing
    )
    {
        healthAffectsPrice = thing.GetInnerIfMinified().def.healthAffectsPrice;
        qualityCategory = QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var category)
            ? category
            : null;
        hasInnerThing = thing is MinifiedThing;
        return healthAffectsPrice
            || qualityCategory.HasValue
            || hasInnerThing
            || thing is GeneSetHolderBase;
    }
}
