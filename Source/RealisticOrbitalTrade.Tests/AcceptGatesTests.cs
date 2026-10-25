using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Rimworld_TradeDeal_TryExecute.EvaluateAcceptGates decides which check refuses a deal, in the
// order the checks run. Every scenario starts from a deal that passes and changes one input.
[TestFixture(TestType.MainMenu)]
internal sealed class AcceptGatesTests
{
    private static readonly List<List<(int lineId, int count)>> OneShuttle =
    [
        [],
    ];

    private static Rimworld_TradeDeal_TryExecute.AcceptRejection Evaluate(
        bool useThreshold = false,
        float combinedValue = 0f,
        float threshold = 0f,
        List<List<(int lineId, int count)>>? plan = null,
        int? shuttleLimit = null,
        List<List<(int lineId, int count)>>? incomingPlan = null,
        bool boughtItemTooHeavy = false,
        bool isRenegotiation = false,
        bool canAfford = true,
        bool departing = false,
        bool increasesLoadedGoods = false,
        bool isFullCancellation = false
    ) =>
        Rimworld_TradeDeal_TryExecute.EvaluateAcceptGates(
            useThreshold,
            combinedValue,
            threshold,
            plan ?? OneShuttle,
            shuttleLimit,
            boughtItemTooHeavy ? null : incomingPlan ?? OneShuttle,
            isRenegotiation,
            canAfford,
            departing,
            increasesLoadedGoods,
            isFullCancellation
        );

    [Test]
    public static void DealThatPassesEveryGateIsAccepted() =>
        Expect.AreEqual(Rimworld_TradeDeal_TryExecute.AcceptRejection.None, Evaluate());

    [Test]
    public static void ValueBelowThresholdIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.BelowThreshold,
            Evaluate(useThreshold: true, combinedValue: 99f, threshold: 100f)
        );

    [Test]
    public static void ValueEqualToThresholdIsAccepted() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(useThreshold: true, combinedValue: 100f, threshold: 100f)
        );

    [Test]
    public static void ThresholdIsIgnoredWhenNotInUse() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(useThreshold: false, combinedValue: 0f, threshold: 100f)
        );

    [Test]
    public static void ThresholdIsReportedBeforeCapacity() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.BelowThreshold,
            Evaluate(
                useThreshold: true,
                combinedValue: 0f,
                threshold: 100f,
                plan: null,
                boughtItemTooHeavy: true
            )
        );

    [Test]
    public static void ItemTooHeavyForAShuttleIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.OverOutgoingCapacity,
            Rimworld_TradeDeal_TryExecute.EvaluateAcceptGates(
                false,
                0f,
                0f,
                null,
                null,
                OneShuttle,
                false,
                true,
                false,
                false,
                false
            )
        );

    [Test]
    public static void RenegotiationNeedingMoreShuttlesThanWereSentIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.OverOutgoingCapacity,
            Evaluate(
                plan:
                [
                    [],
                    [],
                ],
                shuttleLimit: 1,
                isRenegotiation: true
            )
        );

    [Test]
    public static void RenegotiationUsingExactlyTheShuttlesSentIsAccepted() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(
                plan:
                [
                    [],
                    [],
                ],
                shuttleLimit: 2,
                isRenegotiation: true
            )
        );

    [Test]
    public static void OutgoingCapacityIsReportedBeforeIncomingCapacity() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.OverOutgoingCapacity,
            Evaluate(
                plan:
                [
                    [],
                    [],
                ],
                shuttleLimit: 1,
                boughtItemTooHeavy: true
            )
        );

    [Test]
    public static void BoughtItemTooHeavyForAShuttleIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.OverIncomingCapacity,
            Evaluate(boughtItemTooHeavy: true)
        );

    [Test]
    public static void PurchaseNeedingSeveralShuttlesIsAccepted() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(
                incomingPlan:
                [
                    [],
                    [],
                ]
            )
        );

    [Test]
    public static void RenegotiatingAPurchaseOntoMoreShuttlesIsAccepted() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(
                incomingPlan:
                [
                    [],
                    [],
                    [],
                ],
                isRenegotiation: true
            )
        );

    [Test]
    public static void UnaffordableRenegotiationIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.CannotAfford,
            Evaluate(isRenegotiation: true, canAfford: false)
        );

    [Test]
    public static void IncreasingLoadedGoodsAfterDepartureIsRefused() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.CantChangeLoadedGoods,
            Evaluate(isRenegotiation: true, departing: true, increasesLoadedGoods: true)
        );

    [Test]
    public static void ReducingLoadedGoodsAfterDepartureIsAccepted() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(isRenegotiation: true, departing: true, increasesLoadedGoods: false)
        );

    [Test]
    public static void IncreasingSoldGoodsIsAllowedWhenNotRenegotiatingBecauseOfDeparture() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(isRenegotiation: true, departing: false, increasesLoadedGoods: true)
        );

    [Test]
    public static void CancellingEverythingInARenegotiationAsksForConfirmation() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.FullCancellation,
            Evaluate(isRenegotiation: true, isFullCancellation: true)
        );

    [Test]
    public static void EmptyNewTradeIsNotTreatedAsACancellation() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(isRenegotiation: false, isFullCancellation: true)
        );

    [Test]
    public static void RenegotiationOnlyGatesAreSkippedForANewTrade() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.None,
            Evaluate(
                isRenegotiation: false,
                canAfford: false,
                departing: true,
                increasesLoadedGoods: true
            )
        );

    [Test]
    public static void AffordabilityIsReportedBeforeLoadedGoodsChange() =>
        Expect.AreEqual(
            Rimworld_TradeDeal_TryExecute.AcceptRejection.CannotAfford,
            Evaluate(
                isRenegotiation: true,
                canAfford: false,
                departing: true,
                increasesLoadedGoods: true,
                isFullCancellation: true
            )
        );
}
