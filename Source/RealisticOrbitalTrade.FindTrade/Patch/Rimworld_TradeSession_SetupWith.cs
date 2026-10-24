namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Starts a trade agreement for each ship Find / Trade's auto barter job sets up a session with,
/// the same way ROT's own <see cref="TradeShip.TryOpenComms"/> prefix does. It has to exist
/// before the deal adds its tradeables.
/// </summary>
[HarmonyPatch(typeof(TradeSession), nameof(TradeSession.SetupWith))]
internal static class Rimworld_TradeSession_SetupWith
{
    private static void Prefix(ITrader newTrader, Pawn newPlayerNegotiator)
    {
        if (
            !FindTrade_TradeDealProcessor_DoTradeShips.IsTrading
            || newTrader is not TradeShip tradeShip
        )
        {
            return;
        }

        // The previous ship's agreement is only still here if no trade went through
        TradeShipData.EndTradeAgreementIfExists();
        tradeShip.GetData().activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
            RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(
                tradeShip,
                newPlayerNegotiator
            );
    }
}
