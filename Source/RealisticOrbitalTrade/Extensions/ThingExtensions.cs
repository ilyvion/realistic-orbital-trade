namespace RealisticOrbitalTrade;

internal static class ThingExtensions
{
    public static bool HasRequirements(this Thing thing)
    {
        bool healthAffectsPrice = thing.GetInnerIfMinified().def.healthAffectsPrice;
        bool hasQuality = QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var _);
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
        if (QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var category))
        {
            qualityCategory = category;
        }
        else
        {
            qualityCategory = null;
        }
        hasInnerThing = thing is MinifiedThing;
        return healthAffectsPrice || qualityCategory.HasValue || hasInnerThing;
    }
}
