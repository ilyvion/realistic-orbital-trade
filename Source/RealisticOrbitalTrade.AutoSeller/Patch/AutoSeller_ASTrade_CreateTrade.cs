using HarmonyLib;
using RimWorld;
using System.Reflection;
using Verse;

namespace RealisticOrbitalTrade.AutoSeller.Patch;

[HarmonyPatch]
internal static class AutoSeller_ASTrade_CreateTrade
{
    private static MethodInfo TargetMethod()
    {
        return AccessTools.Method("RWAutoSell.ASTrade:CreateTrade");
    }

    private static bool Prefix(ITrader Ship, Pawn pawn, ref bool __result)
    {
        // Only modify behavior for trade ships
        if (Ship is not TradeShip tradeShip)
        {
            return true;
        }

        // Don't allow running for trade ships already in the middle of trading
        TradeShipData tradeShipData = tradeShip.GetData();
        if (tradeShipData.HasActiveTradeAgreement)
        {
            __result = false;
            return false;
        }

        // It shouldn't be possible to get this far if we're blacklisted, but as an extra safety check
        if (RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Blacklisted)
        {
            RealisticOrbitalTradeMod.Error("Auto-seller got all the way to CreateTrade when blacklisted. This is a bug.");
            __result = false;
            return false;
        }

        // Otherwise, go ahead
        tradeShipData.activeTradeAgreement = TradeShipData.tradeAgreementForQuest = RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(tradeShip, pawn);
        return true;
    }
}
