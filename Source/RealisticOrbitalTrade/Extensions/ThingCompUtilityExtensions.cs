#if v1_4
namespace RealisticOrbitalTrade;

// Backport some 1.5 methods
public static class ThingCompUtilityExtensions
{
    public static bool TryGetComp<T>(
        this Thing thing,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? comp
    )
        where T : ThingComp
    {
        ThingWithComps? thingWithComps = thing as ThingWithComps;
        comp = ((thingWithComps != null) ? thingWithComps.GetComp<T>() : null);
        return comp != null;
    }

    public static bool TryGetComp<T>(
        this ThingWithComps thing,
        [System.Diagnostics.CodeAnalysis.NotNullWhen(returnValue: true)] out T? comp
    )
        where T : ThingComp
    {
        comp = thing.GetComp<T>();
        return comp != null;
    }
}
#endif
