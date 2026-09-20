using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Coverage for the gating that decides when a trade's shuttles may leave and whether a sale can
// go ahead once it's divided across several shuttles (see ShuttleLoadPlanner).
[TestFixture(TestType.MainMenu)]
internal sealed class MultiShuttleTradeTests
{
    // Trades saved before sales could span several shuttles have no additional shuttles, and must
    // keep departing exactly as they always did.
    [Test]
    public static void SingleShuttleTradeIsAlwaysClearedForDeparture()
    {
        TradeAgreement tradeAgreement = new();

        Expect.IsTrue(tradeAgreement.IsToTraderDepartureCleared());
        Expect.IsFalse(tradeAgreement.toTraderDepartureCleared);
    }

    [Test]
    public static void LatchedDepartureClearanceStaysCleared()
    {
        TradeAgreement tradeAgreement = new() { toTraderDepartureCleared = true };

        Expect.IsTrue(tradeAgreement.IsToTraderDepartureCleared());
    }

    [Test]
    public static void TradeWithoutShuttlesListsNone() =>
        Expect.IsEmpty(new TradeAgreement().ToTraderTransportShips);

    [Test]
    public static void CargoNeedingMoreShuttlesThanWereSentIsOverCapacityWhenRenegotiating() =>
        Expect.IsTrue(
            Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                [
                    [],
                    [],
                    [],
                ],
                shuttleLimit: 2
            )
        );

    [Test]
    public static void CargoFittingTheShuttlesSentIsNotOverCapacity() =>
        Expect.IsFalse(
            Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                [
                    [],
                    [],
                ],
                shuttleLimit: 2
            )
        );

    // A new trade sends as many shuttles as it needs, so only something that can't be loaded at
    // all (no plan) is refused.
    [Test]
    public static void NewTradeIsNeverOverCapacityWhenItCanBePlanned() =>
        Expect.IsFalse(
            Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                [
                    [],
                    [],
                    [],
                    [],
                ],
                shuttleLimit: null
            )
        );

    [Test]
    public static void UnplannableCargoIsOverCapacity() =>
        Expect.IsTrue(
            Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(null, null)
        );

    [Test]
    public static void ClampToAvailableLimitsTheAmountToWhatTheTraderHolds()
    {
        var silver = new Thing { def = RimWorld.ThingDefOf.Silver, stackCount = 300 };

        Expect.AreEqual(300, TradeAgreement.ClampToAvailable(7195, [silver]));
        Expect.AreEqual(120, TradeAgreement.ClampToAvailable(120, [silver]));
        Expect.AreEqual(0, TradeAgreement.ClampToAvailable(50, []));
    }
}
