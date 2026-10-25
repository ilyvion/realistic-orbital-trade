namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Starts a trade agreement for each deal Find / Trade's auto barter job executes with a ship, the
/// same way ROT's own <see cref="TradeShip.TryOpenComms"/> prefix does. Find / Trade only executes
/// a deal that buys or sells something, so ships with nothing to trade get no agreement. It runs
/// before every other prefix, since ROT's and the Social Experience from Trade integration's read
/// the agreement.
/// </summary>
[HarmonyPatch(typeof(TradeDeal), nameof(TradeDeal.TryExecute))]
internal static class Rimworld_TradeDeal_TryExecute
{
    [HarmonyPriority(Priority.First)]
    private static void Prefix()
    {
        if (
            !FindTrade_TradeDealProcessor_DoTradeShips.IsTrading
            || TradeSession.trader is not TradeShip tradeShip
        )
        {
            return;
        }

        tradeShip.GetData().activeTradeAgreement = TradeShipData.tradeAgreementForQuest =
            RealisticOrbitalTradeGameComponent.Current.StartTradeAgreement(
                tradeShip,
                TradeSession.playerNegotiator
            );
    }

    internal static void Postfix(bool __result)
    {
        // A refused deal leaves its agreement behind
        if (FindTrade_TradeDealProcessor_DoTradeShips.IsTrading && !__result)
        {
            TradeShipData.EndTradeAgreementIfExists();
        }
    }
}
