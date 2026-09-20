using DevTools.Testing;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay: the
// label/tip-key/flash-time/over-capacity selection factored out of DrawCargoMassRow and its
// DynamicTradeInterface mirror so both UIs stay in sync instead of copy-pasting this
// logic.
[TestFixture(TestType.MainMenu)]
internal sealed class CargoMassRowDisplayTests
{
    [TearDown]
    public static void ResetFlashTimes()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = -100f;
        Rimworld_Dialog_Trade_DoWindowContents.lastIncomingMassFlashTime = -100f;
    }

    [Test]
    public static void OutgoingUsesOutgoingLabelAndTipKeys()
    {
        var (_, _, labelKey, tipKey) =
            Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
                outgoing: true,
                mass: 10f,
                massCapacity: 20f
            );

        Expect.AreEqual("RealisticOrbitalTrade.CargoMassLabel", labelKey);
        Expect.AreEqual("RealisticOrbitalTrade.CargoMassTip", tipKey);
    }

    [Test]
    public static void IncomingUsesIncomingLabelAndTipKeys()
    {
        var (_, _, labelKey, tipKey) =
            Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
                outgoing: false,
                mass: 10f,
                massCapacity: 20f
            );

        Expect.AreEqual("RealisticOrbitalTrade.IncomingCargoMassLabel", labelKey);
        Expect.AreEqual("RealisticOrbitalTrade.IncomingCargoMassTip", tipKey);
    }

    [Test]
    public static void MassAtCapacityIsNotOverCapacity()
    {
        var (overCapacity, _, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 20f,
            massCapacity: 20f
        );

        Expect.IsFalse(overCapacity);
    }

    [Test]
    public static void MassAboveCapacityIsOverCapacity()
    {
        var (overCapacity, _, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 20.1f,
            massCapacity: 20f
        );

        Expect.IsTrue(overCapacity);
    }

    [Test]
    public static void OutgoingFlashReadsTheOutgoingFlashTimeField()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = Time.time;
        Rimworld_Dialog_Trade_DoWindowContents.lastIncomingMassFlashTime = Time.time - 10f;

        var (_, flash, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 10f,
            massCapacity: 20f
        );

        Expect.IsTrue(flash);
    }

    [Test]
    public static void IncomingFlashReadsTheIncomingFlashTimeField()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = Time.time - 10f;
        Rimworld_Dialog_Trade_DoWindowContents.lastIncomingMassFlashTime = Time.time;

        var (_, flash, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: false,
            mass: 10f,
            massCapacity: 20f
        );

        Expect.IsTrue(flash);
    }

    [Test]
    public static void FlashExpiresAfterOneSecond()
    {
        Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime = Time.time - 10f;

        var (_, flash, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 10f,
            massCapacity: 20f
        );

        Expect.IsFalse(flash);
    }
}
