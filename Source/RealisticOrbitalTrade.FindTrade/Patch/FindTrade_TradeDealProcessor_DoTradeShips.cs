using MGAutoSell;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Find / Trade's auto barter job trades with every passing ship through
/// <see cref="TradeSession.SetupWith"/> and <see cref="TradeDeal.TryExecute"/> without ever
/// calling <see cref="TradeShip.TryOpenComms"/>, so no trade agreement would be started and the
/// deal would fall back to vanilla's instant delivery. While this runs,
/// <see cref="Rimworld_TradeSession_SetupWith"/> starts an agreement for each ship instead.
/// </summary>
[HarmonyPatch(typeof(TradeDealProcessor), nameof(TradeDealProcessor.DoTradeShips))]
internal static class FindTrade_TradeDealProcessor_DoTradeShips
{
    internal static bool IsTrading { get; private set; }

    private static bool Prefix(Pawn socialPawn)
    {
        // A renegotiation or an unfinished trade owns the static agreement slot; leave it alone
        if (Utils.RenegotiatingAgreement != null || TradeShipData.tradeAgreementForQuest != null)
        {
            return false;
        }

        // Ships that wouldn't answer comms are marked as already traded with, so Find / Trade
        // skips them instead of trading with them the vanilla way
        var traded = Current.Game.GetComponent<TradeRulesGameComp>().traders;
        var standing = RealisticOrbitalTradeGameComponent.Current.Standing;
        foreach (
            var tradeShip in socialPawn.Map.passingShipManager.passingShips.OfType<TradeShip>()
        )
        {
            if (traded.Contains(tradeShip))
            {
                continue;
            }

            var tradeShipData = tradeShip.GetData();
            var outcome = Rimworld_TradeShip_TryOpenComms.DecideComms(
                tradeShipData.ticksUntilCommsClosed,
                Utils.IsMakingAmends,
                standing,
                tradeShipData.HasActiveTradeAgreement
            );
            if (outcome != Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement)
            {
                _ = traded.Add(tradeShip);
            }
        }

        IsTrading = true;
        return true;
    }

    private static void Finalizer()
    {
        if (!IsTrading)
        {
            return;
        }
        IsTrading = false;

        // Left over when the last ship had nothing to trade or the deal was refused
        TradeShipData.EndTradeAgreementIfExists();
    }
}
