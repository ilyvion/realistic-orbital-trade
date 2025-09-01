namespace RealisticOrbitalTrade;

internal class TradeShipData : IExposable
{
    // Used by the various patch methods to get a trade agreement started
    public static TradeAgreement? tradeAgreementForQuest;

    public int ticksUntilCommsClosed = -1;
    public TradeAgreement? activeTradeAgreement;

    public int minimumTradeThreshold;

    public TradeShipData(TradeShip tradeShip, bool getting)
    {
        RealisticOrbitalTradeMod.Dev(() =>
            $"Instantiating new TradeShipData instance for {tradeShip} (getting? {getting})"
        );

        minimumTradeThreshold = Settings.GenerateDeviatingMinimumTradeThreshold();

        if (!getting)
        {
            tradeShip.SetData(this);
        }
    }

    public bool HasActiveTradeAgreement => activeTradeAgreement != null;

    public static void EndTradeAgreementIfExists()
    {
        if (tradeAgreementForQuest != null)
        {
            RealisticOrbitalTradeGameComponent.Current.EndTradeAgreement(tradeAgreementForQuest);
            tradeAgreementForQuest = null;
        }
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref ticksUntilCommsClosed, "ticksUntilCommsClosed", 0);
        Scribe_References.Look(ref activeTradeAgreement, "activeTradeAgreement");
        Scribe_Values.Look(ref minimumTradeThreshold, "minimumTradeThreshold", -1);

        if (Scribe.mode == LoadSaveMode.PostLoadInit && minimumTradeThreshold == -1)
        {
            minimumTradeThreshold = Settings.GenerateDeviatingMinimumTradeThreshold();
        }
    }

    public override string ToString()
    {
        const string Null = "<null>";
        return $"{{ticksUntilCommsClosed={ticksUntilCommsClosed}, activeTradeAgreement={activeTradeAgreement?.GetUniqueLoadID() ?? Null}}}";
    }
}

internal static class TradeShipExtensions
{
    internal static void SetData(this TradeShip tradeShip, TradeShipData tradeShipData)
    {
        if (_tradeShipExtra.TryGetValue(tradeShip, out _))
        {
            _ = _tradeShipExtra.Remove(tradeShip);
        }
        _tradeShipExtra.Add(tradeShip, tradeShipData);
    }

#pragma warning disable IDE0028 // Simplify collection initialization
    private static readonly ConditionalWeakTable<TradeShip, TradeShipData> _tradeShipExtra = new();
#pragma warning restore IDE0028 // Simplify collection initialization

    public static TradeShipData GetData(this TradeShip tradeShip) =>
        _tradeShipExtra.GetValue(
            tradeShip,
            (tradeShip) =>
            {
                RealisticOrbitalTradeMod.Dev(() =>
                    $"Generating new TradeShipData value for {tradeShip} ({tradeShip.GetHashCode()} -- {tradeShip.GetUniqueLoadID()})"
                );
                return new(tradeShip, true);
            }
        );
}
