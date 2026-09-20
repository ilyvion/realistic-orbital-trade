using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

// Regression guard: TradeAgreement.ColonyThingsTraderWillingToBuy() used to dereference the
// negotiator's Map unconditionally, so a dead/despawned negotiator during an active trade caused
// a recurring NullReferenceException in CompTradeShuttle.CheckAutoload. It now checks
// NegotiatorAvailable first and returns no items until the player registers a replacement
// negotiator (JobDriver_RegisterAsNegotiator), rather than picking one automatically.
[TestFixture(TestType.MainMenu)]
internal sealed class NegotiatorAvailabilityTests
{
    [Test]
    public static void NegotiatorAvailableIsFalseForDespawnedNegotiator()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        Expect.IsFalse(tradeAgreement.NegotiatorAvailable);
    }

    [Test]
    public static void ColonyThingsTraderWillingToBuyIsEmptyForDespawnedNegotiator()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        var result = tradeAgreement.ColonyThingsTraderWillingToBuy();

        Expect.IsEmpty(result);
    }

    [Test]
    public static void ShouldSendNegotiatorUnavailableLetterIsTrueOnFirstDetection()
    {
        var tradeAgreement = new TradeAgreement { negotiator = new Pawn() };

        Expect.IsTrue(tradeAgreement.ShouldSendNegotiatorUnavailableLetter);
    }

    [Test]
    public static void ShouldSendNegotiatorUnavailableLetterIsFalseOnceAlreadySent()
    {
        var tradeAgreement = new TradeAgreement
        {
            negotiator = new Pawn(),
            NegotiatorUnavailableLetterSent = true,
        };

        Expect.IsFalse(tradeAgreement.ShouldSendNegotiatorUnavailableLetter);
    }
}
