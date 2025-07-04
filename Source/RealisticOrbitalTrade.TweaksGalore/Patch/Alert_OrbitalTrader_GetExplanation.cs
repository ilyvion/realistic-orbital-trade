using System.Collections.Generic;
using RealisticOrbitalTrade.Patch;
using TweaksGalore;

namespace RealisticOrbitalTrade.TweaksGalore.Patch;

[HarmonyPatch(typeof(Alert_OrbitalTrader), nameof(Alert_OrbitalTrader.GetExplanation))]
internal static class Alert_OrbitalTrader_GetExplanation
{
    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        return Alert_OrbitalTrader_Shared.TranspileGetExplanation(instructions, "Tweaks Galore");
    }
}
