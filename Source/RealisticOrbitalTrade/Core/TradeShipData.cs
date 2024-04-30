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
    private static ConditionalWeakTable<TradeShip, TradeShipData> _tradeShipExtra = new();

    public static TradeShipData GetData(this TradeShip tradeShip)
    {
        return _tradeShipExtra.GetValue(tradeShip, (tradeShip) => new());
    }
}
