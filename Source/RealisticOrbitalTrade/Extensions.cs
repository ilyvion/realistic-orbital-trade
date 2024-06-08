using RimWorld;
using Verse;

namespace RealisticOrbitalTrade;

internal static class ThingExtensions
{
    public static bool HasRequirements(this Thing thing)
    {
        bool healthAffectsPrice = thing.GetInnerIfMinified().def.healthAffectsPrice;
        bool hasQuality = QualityUtility.TryGetQuality(thing.GetInnerIfMinified(), out var _);
        return healthAffectsPrice || hasQuality || thing is MinifiedThing;
    }
    public static bool HasRequirements(this Thing thing, out bool healthAffectsPrice, out QualityCategory? qualityCategory, out bool hasInnerThing)
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

#if v1_4
// Backport some 1.5 methods
public static class ThingCompUtility
{
    public static bool TryGetComp<T>(this Thing thing, [System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? comp) where T : ThingComp
    {
        ThingWithComps? thingWithComps = thing as ThingWithComps;
        comp = ((thingWithComps != null) ? thingWithComps.GetComp<T>() : null);
        return comp != null;
    }

    public static bool TryGetComp<T>(this ThingWithComps thing, [System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? comp) where T : ThingComp
    {
        comp = thing.GetComp<T>();
        return comp != null;
    }
}
#endif
