using RealisticOrbitalTrade.Patch;
using WeHadATrader;

namespace RealisticOrbitalTrade.TweaksGalore.Patch;

[HarmonyPatch(typeof(Alert_OrbitalTrader), nameof(Alert_OrbitalTrader.GetExplanation))]
internal static class Alert_OrbitalTrader_GetExplanation
{
    private static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    ) => Alert_OrbitalTrader_Shared.TranspileGetExplanation(instructions, "We Had a Trader?");
}
