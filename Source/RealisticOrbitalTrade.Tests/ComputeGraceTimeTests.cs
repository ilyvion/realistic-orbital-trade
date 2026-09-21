using DevTools.Testing;
using RealisticOrbitalTrade.Quests;

namespace RealisticOrbitalTrade.Tests;

// ComputeGraceTime is the arithmetic behind trade-ship grace time: the comms-close tick and the
// extended departure.
[TestFixture(TestType.MainMenu)]
internal sealed class ComputeGraceTimeTests
{
    private const int Min = 10000;
    private const int Grace = 5000;
    private const int Extra = 2500;

    [Test]
    public static void DepartureAtMinimumRequiresNoGrace()
    {
        var (departure, commsClosed, requiresGrace) = QuestUtils.ComputeGraceTime(
            Min,
            -1,
            Min,
            Grace,
            Extra
        );

        Expect.IsFalse(requiresGrace);
        Expect.AreEqual(Min, departure);
        Expect.AreEqual(-1, commsClosed);
    }

    [Test]
    public static void DepartureBelowMinimumExtendsAndRecordsCommsClose()
    {
        var (departure, commsClosed, requiresGrace) = QuestUtils.ComputeGraceTime(
            Min - 1,
            -1,
            Min,
            Grace,
            Extra
        );

        Expect.IsTrue(requiresGrace);
        Expect.AreEqual(Min - 1 + Grace + Extra, departure);
        Expect.AreEqual(Min - 1, commsClosed);
    }

    [Test]
    public static void AlreadyInGraceTimeKeepsCommsCloseButExtendsDepartureAgain()
    {
        var (departure, commsClosed, requiresGrace) = QuestUtils.ComputeGraceTime(
            3000,
            1200,
            Min,
            Grace,
            Extra
        );

        Expect.IsTrue(requiresGrace);
        Expect.AreEqual(3000 + Grace + Extra, departure);
        Expect.AreEqual(1200, commsClosed);
    }

    [Test]
    public static void ZeroGraceTicksStillAddsAdditionalTicks()
    {
        var (departure, _, requiresGrace) = QuestUtils.ComputeGraceTime(100, -1, Min, 0, Extra);

        Expect.IsTrue(requiresGrace);
        Expect.AreEqual(100 + Extra, departure);
    }

    [Test]
    public static void ZeroMinimumNeverRequiresGrace()
    {
        var (departure, _, requiresGrace) = QuestUtils.ComputeGraceTime(0, -1, 0, Grace, Extra);

        Expect.IsFalse(requiresGrace);
        Expect.AreEqual(0, departure);
    }
}
