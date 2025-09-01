namespace RealisticOrbitalTrade;

internal static class ThingExtensions
{
    public static bool HasRequirements(this Thing thing)
    {
        var healthAffectsPrice = thing.GetInnerIfMinified().def.healthAffectsPrice;
        var hasQuality = QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var _);
        return healthAffectsPrice || hasQuality || thing is MinifiedThing;
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
        return healthAffectsPrice || qualityCategory.HasValue || hasInnerThing;
    }
}
