
using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.ExposeData))]
internal static class Rimworld_TradeShip_ExposeData
{
    private static void Postfix(TradeShip __instance)
    {
        var extra = __instance.GetData();
        Scribe_Deep.Look(ref extra, "realisticOrbitalTradeData");
    }
}
