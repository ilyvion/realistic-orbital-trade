using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.Playing)]
internal sealed class TradeShipDataTests
{
    private static TradeShip NewTradeShip() =>
        new(DefDatabase<TraderKindDef>.GetNamed("Orbital_BulkGoods"));

    [Test]
    public static void SetDataStoresTheData()
    {
        var tradeShip = NewTradeShip();
        var data = new TradeShipData(tradeShip, true);

        tradeShip.SetData(data);

        Expect.ReferencesAreEqual(data, tradeShip.GetData());
    }

    [Test]
    public static void SetDataReplacesExistingData()
    {
        var tradeShip = NewTradeShip();
        var original = tradeShip.GetData();
        var replacement = new TradeShipData(tradeShip, true);

        tradeShip.SetData(replacement);

        Expect.ReferencesAreNotEqual(original, tradeShip.GetData());
        Expect.ReferencesAreEqual(replacement, tradeShip.GetData());
    }
}
