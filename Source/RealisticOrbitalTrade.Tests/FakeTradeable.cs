namespace RealisticOrbitalTrade.Tests;

// Tradeable.GetPriceFor computes real market pricing off TradeSession.playerNegotiator's stats,
// which FindMaxBuyCount/FindMaxSellCount tests have no need to stand up - this stubs a fixed price
// so those tests can exercise the currency-rebalancing math in isolation.
internal sealed class FakeTradeable : Tradeable
{
    public float BuyPrice;
    public float SellPrice;

    public override float GetPriceFor(TradeAction action) =>
        action == TradeAction.PlayerBuys ? BuyPrice : SellPrice;
}
