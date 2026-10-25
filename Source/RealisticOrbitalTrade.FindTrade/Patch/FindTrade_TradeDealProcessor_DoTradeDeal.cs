using MGAutoSell;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Fits the deal Find / Trade's auto barter job builds to ROT's accept gates in
/// <see cref="TradeDeal.TryExecute"/>, which would otherwise reject it with an on-screen message
/// and leave the ship untraded for its stay: things too heavy for a shuttle are left out of the
/// deal before Find / Trade picks what to trade, and a deal below the minimum trade threshold is
/// emptied so Find / Trade doesn't execute it.
/// </summary>
[HarmonyPatch(typeof(TradeDealProcessor), nameof(TradeDealProcessor.DoTradeDeal))]
internal static class FindTrade_TradeDealProcessor_DoTradeDeal
{
    private static void Prefix(TradeDeal deal)
    {
        if (
            !FindTrade_TradeDealProcessor_DoTradeShips.IsTrading
            || TradeShipData.tradeAgreementForQuest is not { } tradeAgreement
        )
        {
            return;
        }

        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        var boughtByShuttle = !tradeAgreement.deliverItemsViaDropPod;
        _ = deal.AllTradeables.RemoveAll(tradeable =>
            !tradeable.IsCurrency
            && (
                tradeable.thingsColony.Any(thing => CargoMass(thing) > massCapacity)
                || (
                    boughtByShuttle
                    && tradeable.thingsTrader.Any(thing => CargoMass(thing) > massCapacity)
                )
            )
        );
    }

    private static void Postfix(TradeDeal deal)
    {
        if (
            !FindTrade_TradeDealProcessor_DoTradeShips.IsTrading
            || TradeShipData.tradeAgreementForQuest is not { } tradeAgreement
            || !Settings._useMinimumTradeThreshold
        )
        {
            return;
        }

        var combinedTradeValue = Rimworld_Dialog_Trade_DoWindowContents.CalculateCombinedTradeValue(
            deal
        );
        var minimumTradeThreshold = tradeAgreement.tradeShip.GetData().minimumTradeThreshold;
        if (combinedTradeValue == 0f || combinedTradeValue >= minimumTradeThreshold)
        {
            return;
        }

        RealisticOrbitalTradeMod.Instance.LogDevMessage(
            $"Skipping Find / Trade barter with {tradeAgreement.tradeShip.TraderName}: trade value "
                + $"{combinedTradeValue} is below the minimum trade threshold {minimumTradeThreshold}"
        );
        foreach (var tradeable in deal.AllTradeables)
        {
            tradeable.ForceTo(0);
        }
    }

    private static float CargoMass(Thing thing) =>
        thing is Pawn ? TradeAgreement.PawnCargoMass(thing) : thing.GetStatValue(StatDefOf.Mass);
}
