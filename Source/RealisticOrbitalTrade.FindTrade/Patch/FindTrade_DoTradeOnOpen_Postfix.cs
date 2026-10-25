using MGAutoSell.HarmonyPatches;

namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Keeps Find / Trade from filling in the renegotiation dialog of a landed trade shuttle, which
/// opens with the agreed deal already in it, and from recording its ship as manually traded with.
/// </summary>
[HarmonyPatch(typeof(DoTradeOnOpen), nameof(DoTradeOnOpen.Postfix))]
internal static class FindTrade_DoTradeOnOpen_Postfix
{
    private static bool Prefix() => Utils.RenegotiatingAgreement == null;
}
