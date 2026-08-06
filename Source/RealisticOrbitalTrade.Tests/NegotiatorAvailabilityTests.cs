using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: TradeAgreement.ColonyThingsTraderWillingToBuy() used to dereference the
// negotiator's Map unconditionally, so a dead/despawned negotiator during an active trade caused
// a recurring NullReferenceException in CompTradeShuttle.CheckAutoload. It now checks
// NegotiatorAvailable first and returns no items until the player registers a replacement
// negotiator (JobDriver_RegisterAsNegotiator), rather than picking one automatically.
[TestSuite]
internal static class NegotiatorAvailabilityTests
{
    [Test]
    public static void NegotiatorAvailableIsFalseForDespawnedNegotiator()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        Assert.That(tradeAgreement.NegotiatorAvailable).Is.False();
    }

    [Test]
    public static void ColonyThingsTraderWillingToBuyIsEmptyForDespawnedNegotiator()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        var result = tradeAgreement.ColonyThingsTraderWillingToBuy();

        Assert.ThatCollection(result).Is.Empty();
    }

    [Test]
    public static void ShouldSendNegotiatorUnavailableLetterIsTrueOnFirstDetection()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        Assert.That(tradeAgreement.ShouldSendNegotiatorUnavailableLetter).Is.True();
    }

    [Test]
    public static void ShouldSendNegotiatorUnavailableLetterIsFalseOnceAlreadySent()
    {
        var tradeAgreement = new TradeAgreement
        {
            negotiator = new Pawn(),
            NegotiatorUnavailableLetterSent = true,
        };

        Assert.That(tradeAgreement.ShouldSendNegotiatorUnavailableLetter).Is.False();
    }
}
