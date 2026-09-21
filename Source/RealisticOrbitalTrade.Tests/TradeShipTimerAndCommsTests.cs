using DevTools.Testing;
using RealisticOrbitalTrade.Patch;
using RealisticOrbitalTrade.Quests;

namespace RealisticOrbitalTrade.Tests;

// The trade ship's departure/comms-closed timers, the comms gate order, the trade dialog close
// decision and the departure extension floor.
[TestFixture(TestType.MainMenu)]
internal sealed class TradeShipTimerAndCommsTests
{
    private const int Extra = 2500;

    private static (int departure, int commsClosed) Advance(
        int before = 10000,
        int after = 9999,
        int commsClosed = -1,
        bool pauses = false,
        bool hasAgreement = false,
        bool amends = false
    ) =>
        Rimworld_TradeShip_PassingShipTick.AdvanceTradeShipTimers(
            before,
            after,
            commsClosed,
            pauses,
            hasAgreement,
            amends,
            Extra
        );

    [Test]
    public static void PausingAgreementRestoresDeparture()
    {
        var (departure, commsClosed) = Advance(commsClosed: 5, pauses: true, hasAgreement: true);

        Expect.AreEqual(10000, departure);
        Expect.AreEqual(5, commsClosed);
    }

    [Test]
    public static void AmendsRestoreDeparture()
    {
        var (departure, _) = Advance(amends: true);

        Expect.AreEqual(10000, departure);
    }

    [Test]
    public static void CommsClosedCountsDown() =>
        Expect.AreEqual(4, Advance(commsClosed: 5).commsClosed);

    [Test]
    public static void CommsClosedMinusOneStaysUntouched() =>
        Expect.AreEqual(-1, Advance(commsClosed: -1).commsClosed);

    [Test]
    public static void CommsClosedReachingZeroDoesNotSnapThatTick()
    {
        var (departure, commsClosed) = Advance(commsClosed: 1);

        Expect.AreEqual(0, commsClosed);
        Expect.AreEqual(9999, departure);
    }

    [Test]
    public static void CommsClosedZeroWithActiveAgreementDoesNotSnap() =>
        Expect.AreEqual(9999, Advance(commsClosed: 0, hasAgreement: true).departure);

    [Test]
    public static void CommsClosedZeroNeverExtendsDeparture()
    {
        Expect.AreEqual(Extra, Advance(after: Extra, commsClosed: 0).departure);
        Expect.AreEqual(100, Advance(after: 100, commsClosed: 0).departure);
    }

    [Test]
    public static void CommsClosedZeroWithoutAgreementSnapsDeparture() =>
        Expect.AreEqual(Extra, Advance(commsClosed: 0).departure);

    private static Rimworld_TradeShip_TryOpenComms.CommsOutcome Decide(
        int commsClosed = -1,
        bool amends = false,
        Standing standing = Standing.Good,
        bool hasAgreement = false
    ) => Rimworld_TradeShip_TryOpenComms.DecideComms(commsClosed, amends, standing, hasAgreement);

    [Test]
    public static void CommsOpenStartsAgreement() =>
        Expect.AreEqual(Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement, Decide());

    [Test]
    public static void ClosedCommsWinOverEverything() =>
        Expect.AreEqual(
            Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForGraceTime,
            Decide(0, true, Standing.Blacklisted, true)
        );

    [Test]
    public static void AmendsWinOverBlacklistAndActiveTrade() =>
        Expect.AreEqual(
            Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForActiveAmendment,
            Decide(amends: true, standing: Standing.Blacklisted, hasAgreement: true)
        );

    [Test]
    public static void BlacklistWinsOverActiveTrade() =>
        Expect.AreEqual(
            Rimworld_TradeShip_TryOpenComms.CommsOutcome.Blacklisted,
            Decide(standing: Standing.Blacklisted, hasAgreement: true)
        );

    [Test]
    public static void ActiveTradeBlocksNewAgreement() =>
        Expect.AreEqual(
            Rimworld_TradeShip_TryOpenComms.CommsOutcome.NotAnsweringForActiveTrade,
            Decide(hasAgreement: true)
        );

    [Test]
    public static void ForgivenStandingTrades() =>
        Expect.AreEqual(
            Rimworld_TradeShip_TryOpenComms.CommsOutcome.StartAgreement,
            Decide(standing: Standing.Forgiven)
        );

    [Test]
    public static void DepartingRenegotiationBlocksCloseUntilConfirmed() =>
        Expect.AreEqual(
            Rimworld_Dialog_Trade_Close.CloseAction.BlockAndConfirm,
            Rimworld_Dialog_Trade_Close.DecideClose(true, false, true)
        );

    [Test]
    public static void ConfirmedDepartingRenegotiationFailsTrade() =>
        Expect.AreEqual(
            Rimworld_Dialog_Trade_Close.CloseAction.FailTrade,
            Rimworld_Dialog_Trade_Close.DecideClose(true, true, true)
        );

    [Test]
    public static void OrdinaryRenegotiationCloseIsNoOp() =>
        Expect.AreEqual(
            Rimworld_Dialog_Trade_Close.CloseAction.NoOp,
            Rimworld_Dialog_Trade_Close.DecideClose(false, false, true)
        );

    [Test]
    public static void FreshTradeCloseEndsAgreement() =>
        Expect.AreEqual(
            Rimworld_Dialog_Trade_Close.CloseAction.EndAgreement,
            Rimworld_Dialog_Trade_Close.DecideClose(false, false, false)
        );

    [Test]
    public static void VeryShortDepartureIsExtendedToMinimum()
    {
        Expect.AreEqual(
            2500,
            QuestPart_ExtendTradeShipDepartureIfVeryShort.ExtendIfVeryShort(2499)
        );
        Expect.AreEqual(2500, QuestPart_ExtendTradeShipDepartureIfVeryShort.ExtendIfVeryShort(0));
    }

    [Test]
    public static void LongEnoughDepartureIsUnchanged()
    {
        Expect.AreEqual(
            2500,
            QuestPart_ExtendTradeShipDepartureIfVeryShort.ExtendIfVeryShort(2500)
        );
        Expect.AreEqual(
            100000,
            QuestPart_ExtendTradeShipDepartureIfVeryShort.ExtendIfVeryShort(100000)
        );
    }
}
