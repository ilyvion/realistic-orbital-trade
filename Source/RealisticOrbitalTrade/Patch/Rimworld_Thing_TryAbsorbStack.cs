namespace RealisticOrbitalTrade.Patch;

// Thing.TryAbsorbStack merges `other` into `__instance` and, when `other` is fully
// absorbed, calls other.Destroy() internally. That Destroy call reaches
// Rimworld_Thing_Destroy's postfix indistinguishably from a "real" destruction, so this
// patch records which thing is being absorbed into which survivor around the call, letting
// the Destroy patch tell a stack-merge apart from an actual loss.
[HarmonyPatch(typeof(Thing), nameof(Thing.TryAbsorbStack))]
internal static class Rimworld_Thing_TryAbsorbStack
{
    internal static Thing? AbsorbingThing;
    internal static Thing? AbsorbedThing;

    private static void Prefix(Thing __instance, Thing other)
    {
        AbsorbingThing = __instance;
        AbsorbedThing = other;
    }

    private static void Postfix()
    {
        AbsorbingThing = null;
        AbsorbedThing = null;
    }
}
