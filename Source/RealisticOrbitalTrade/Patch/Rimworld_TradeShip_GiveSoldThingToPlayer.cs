using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToPlayer))]
internal static class Rimworld_TradeShip_GiveSoldThingToPlayer
{
    private static bool Prefix(TradeShip __instance, Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.WarningOnce("Trade ship did not have a trade agreement arranged. Mod incompatibility?", Constants.MissingTradeAgreementKey);
            return true;
        }

        Thing thing = toGive.SplitOff(countToGive);
        if (thing is Pawn item)
        {
            Traverse.Create(__instance).Field("soldPrisoners").GetValue<List<Pawn>>().Remove(item);
        }
        tradeAgreement.thingsSoldToPlayer.TryAddOrTransfer(thing);

        return false;
    }
}
