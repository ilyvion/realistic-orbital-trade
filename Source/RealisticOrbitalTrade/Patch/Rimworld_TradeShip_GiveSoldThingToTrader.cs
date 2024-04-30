
using HarmonyLib;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(TradeShip), nameof(TradeShip.GiveSoldThingToTrader))]
internal static class Rimworld_TradeShip_GiveSoldThingToTrader
{
    private static bool Prefix(Thing toGive, int countToGive, Pawn playerNegotiator)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest!;
        if (toGive is Pawn pawn)
        {
            tradeAgreement.pawnsSoldToTrader.Add(pawn);
        }
        else
        {
            tradeAgreement.thingsSoldToTrader.Add(new(toGive, countToGive));
        }

        return false;
    }
}
