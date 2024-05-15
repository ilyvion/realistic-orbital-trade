using System.Runtime.CompilerServices;
using RimWorld;
using Verse;

namespace RealisticOrbitalTrade;

internal class TradeShipData : IExposable
{
    // Used by the various patch methods to get a trade agreement started
    public static TradeAgreement? tradeAgreementForQuest;

    public int ticksUntilCommsClosed = -1;
    public TradeAgreement? activeTradeAgreement;

    public TradeShipData(TradeShip tradeShip, bool getting)
    {
        RealisticOrbitalTradeMod.Dev($"Instantiating new TradeShipData instance for {tradeShip} (getting? {getting})");
        if (!getting)
        {
            tradeShip.SetData(this);
        }
    }

    public bool HasActiveTradeAgreement { get => activeTradeAgreement != null; }

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
            _tradeShipExtra.Remove(tradeShip);
        }
        _tradeShipExtra.Add(tradeShip, tradeShipData);
    }
    private static ConditionalWeakTable<TradeShip, TradeShipData> _tradeShipExtra = new();

    public static TradeShipData GetData(this TradeShip tradeShip)
    {
        return _tradeShipExtra.GetValue(tradeShip, (tradeShip) =>
        {
            RealisticOrbitalTradeMod.Dev($"Generating new TradeShipData value for {tradeShip} ({tradeShip.GetHashCode()} -- {tradeShip.GetUniqueLoadID()})");
            return new(tradeShip, true);
        });
    }
}
