using MGAutoSell;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.FindTrade.Patch;

/// <summary>
/// Find / Trade's auto barter job trades with every passing ship through
/// <see cref="TradeSession.SetupWith"/> and <see cref="TradeDeal.TryExecute"/> without ever
/// calling <see cref="TradeShip.TryOpenComms"/>, so no trade agreement would be started and the
/// deal would fall back to vanilla's instant delivery. While this runs,
/// <see cref="Rimworld_TradeDeal_TryExecute"/> starts an agreement for each executed deal instead.
/// </summary>
[HarmonyPatch(typeof(TradeDealProcessor), nameof(TradeDealProcessor.DoTradeShips))]
internal static class FindTrade_TradeDealProcessor_DoTradeShips
{
    // A depth, not a flag, so a nested call ending doesn't end the outer call's trading
    private static int tradingDepth;

    internal static bool IsTrading => tradingDepth > 0;

    // __state holds the ships hidden for this pass only, or null when this call doesn't trade
    internal static bool Prefix(Pawn socialPawn, out List<ITrader>? __state)
    {
        __state = null;

        // A renegotiation or an unfinished trade owns the static agreement slot; leave it alone
        if (Utils.RenegotiatingAgreement != null)
        {
            RealisticOrbitalTradeMod.Instance.LogDevMessage(
                "Skipping Find / Trade barter with trade ships: a trade is being renegotiated"
            );
            return false;
        }
        if (TradeShipData.tradeAgreementForQuest is { } tradeAgreementForQuest)
        {
            // Trade windows pause the game, so one can only be open here if it opened this tick
            if (Find.WindowStack.WindowsForcePause)
            {
                RealisticOrbitalTradeMod.Instance.LogDevMessage(
                    "Skipping Find / Trade barter with trade ships: a trade window is open"
                );
                return false;
            }

            RealisticOrbitalTradeMod.Instance.LogWarning(
                $"Clearing trade agreement with {tradeAgreementForQuest.tradeShip.TraderName} "
                    + "left over from an earlier trade so Find / Trade can barter with trade ships"
            );
            // A renegotiated agreement belongs to a trade still in progress
            if (tradeAgreementForQuest.tradeDeliveryMethod != null)
            {
                TradeShipData.tradeAgreementForQuest = null;
            }
            else
            {
                TradeShipData.EndTradeAgreementIfExists();
            }
        }

        // Ships that wouldn't answer comms are hidden from Find / Trade so it doesn't trade with
        // them the vanilla way. Ships out of grace time stay hidden for good; the rest only for
        // this pass, so they are traded with once the colony can answer comms again
        var traded = Current.Game.GetComponent<TradeRulesGameComp>().traders;
        var standing = RealisticOrbitalTradeGameComponent.Current.Standing;
        var skippedForThisPass = __state = [];
        tradingDepth++;
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
            if (outcome == Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement)
            {
                continue;
            }

            _ = traded.Add(tradeShip);
            if (outcome != Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForGraceTime)
            {
                skippedForThisPass.Add(tradeShip);
            }
        }

        return true;
    }

    internal static void Finalizer(List<ITrader>? __state)
    {
        if (__state == null)
        {
            return;
        }

        var traded = Current.Game.GetComponent<TradeRulesGameComp>().traders;
        foreach (var tradeShip in __state)
        {
            _ = traded.Remove(tradeShip);
        }

        tradingDepth--;
        if (IsTrading)
        {
            return;
        }

        // Left over when a deal threw before it finished
        TradeShipData.EndTradeAgreementIfExists();
    }
}
