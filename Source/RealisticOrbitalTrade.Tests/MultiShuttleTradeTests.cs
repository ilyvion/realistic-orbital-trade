using RealisticOrbitalTrade.Patch;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Coverage for the gating that decides when a trade's shuttles may leave and whether a sale can
// go ahead once it's divided across several shuttles (see ShuttleLoadPlanner).
[TestSuite]
internal static class MultiShuttleTradeTests
{
    // Trades saved before sales could span several shuttles have no additional shuttles, and must
    // keep departing exactly as they always did.
    [Test]
    public static void SingleShuttleTradeIsAlwaysClearedForDeparture()
    {
        TradeAgreement tradeAgreement = new();

        Assert.That(tradeAgreement.IsToTraderDepartureCleared()).Is.True();
        Assert.That(tradeAgreement.toTraderDepartureCleared).Is.False();
    }

    [Test]
    public static void LatchedDepartureClearanceStaysCleared()
    {
        TradeAgreement tradeAgreement = new() { toTraderDepartureCleared = true };

        Assert.That(tradeAgreement.IsToTraderDepartureCleared()).Is.True();
    }

    [Test]
    public static void TradeWithoutShuttlesListsNone() =>
        Assert.ThatCollection(new TradeAgreement().ToTraderTransportShips).Is.Empty();

    [Test]
    public static void CargoNeedingMoreShuttlesThanWereSentIsOverCapacityWhenRenegotiating() =>
        Assert
            .That(
                Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                    [
                        [],
                        [],
                        [],
                    ],
                    shuttleLimit: 2
                )
            )
            .Is.True();

    [Test]
    public static void CargoFittingTheShuttlesSentIsNotOverCapacity() =>
        Assert
            .That(
                Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                    [
                        [],
                        [],
                    ],
                    shuttleLimit: 2
                )
            )
            .Is.False();

    // A new trade sends as many shuttles as it needs, so only something that can't be loaded at
    // all (no plan) is refused.
    [Test]
    public static void NewTradeIsNeverOverCapacityWhenItCanBePlanned() =>
        Assert
            .That(
                Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(
                    [
                        [],
                        [],
                        [],
                        [],
                    ],
                    shuttleLimit: null
                )
            )
            .Is.False();

    [Test]
    public static void UnplannableCargoIsOverCapacity() =>
        Assert
            .That(Rimworld_Dialog_Trade_DoWindowContents.IsOutgoingCargoOverCapacity(null, null))
            .Is.True();

    [Test]
    public static void ClampToAvailableLimitsTheAmountToWhatTheTraderHolds()
    {
        var silver = new Thing { def = RimWorld.ThingDefOf.Silver, stackCount = 300 };

        Assert.That(TradeAgreement.ClampToAvailable(7195, [silver])).Is.EqualTo(300);
        Assert.That(TradeAgreement.ClampToAvailable(120, [silver])).Is.EqualTo(120);
        Assert.That(TradeAgreement.ClampToAvailable(50, [])).Is.EqualTo(0);
    }
}
