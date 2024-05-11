using HarmonyLib;
using RimWorld;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.PassingShipTick))]
internal static class Rimworld_TradeShip_PassingShipTick
{
    private static void Prefix(TradeShip __instance, out int __state)
    {
        __state = __instance.ticksUntilDeparture;
    }

    private static void Postfix(TradeShip __instance, int __state)
    {
        var tradeShipExtra = __instance.GetData();
        var activeTradeAgreement = tradeShipExtra.activeTradeAgreement;
        if ((activeTradeAgreement != null && activeTradeAgreement.tradePausesDepartureTimer) || Utils.IsMakingAmends)
        {
            __instance.ticksUntilDeparture = __state;
        }
        else
        {
            if (tradeShipExtra.ticksUntilCommsClosed > 0)
            {
                tradeShipExtra.ticksUntilCommsClosed--;
            }
        }
    }
}
