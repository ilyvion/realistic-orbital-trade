
using HarmonyLib;
using RimWorld;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.PassingShipTick))]
internal static class Rimworld_TradeShip_PassingShipTick
{
    private static void Postfix(TradeShip __instance)
    {
        var tradeShipExtra = __instance.GetData();
        if (tradeShipExtra.ticksUntilCommsClosed > 0)
        {
            tradeShipExtra.ticksUntilCommsClosed--;
        }
    }
}
