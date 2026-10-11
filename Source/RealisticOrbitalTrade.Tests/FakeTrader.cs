namespace RealisticOrbitalTrade.Tests;

// TradeAgreement.MatchingTradeable and RimWorld.TransferableUtility.TradeableMatching dereference
// TradeSession.trader.TraderKind (via Tradeable.TraderWillTrade) even just to compare whether two
// things can stack into the same tradeable - a live TradeSession isn't otherwise needed for that
// comparison, but the static field still has to be non-null or it NREs. This fake exists purely
// to satisfy that dependency in tests; nothing here needs to behave like a real trader.
internal sealed class FakeTrader : ITrader
{
    public TraderKindDef TraderKind { get; } = new();

    public IEnumerable<Thing> Goods => [];

    public int RandomPriceFactorSeed => 0;

    public string TraderName => "FakeTrader";

    public bool CanTradeNow => true;

    public float TradePriceImprovementOffsetForPlayer => 0f;

    public Faction? Faction => null;

    public TradeCurrency TradeCurrency => TradeCurrency.Silver;

    public IEnumerable<Thing> ColonyThingsWillingToBuy(Pawn playerNegotiator) => [];

    public void GiveSoldThingToTrader(Thing toGive, int countToGive, Pawn playerNegotiator) { }

    public void GiveSoldThingToPlayer(Thing toGive, int countToGive, Pawn playerNegotiator) { }
}
