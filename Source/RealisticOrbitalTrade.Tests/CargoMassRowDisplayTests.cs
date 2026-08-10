using RealisticOrbitalTrade.Patch;
using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

// Regression/coverage guard for Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay: the
// label/tip-key/flash-time/over-capacity selection factored out of DrawCargoMassRow and its
// DynamicTradeInterface mirror so both UIs stay in sync instead of copy-pasting this
// logic.
[TestSuite]
internal static class CargoMassRowDisplayTests
{
    [AfterEach]
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

        Assert.That(labelKey).Is.EqualTo("RealisticOrbitalTrade.CargoMassLabel");
        Assert.That(tipKey).Is.EqualTo("RealisticOrbitalTrade.CargoMassTip");
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

        Assert.That(labelKey).Is.EqualTo("RealisticOrbitalTrade.IncomingCargoMassLabel");
        Assert.That(tipKey).Is.EqualTo("RealisticOrbitalTrade.IncomingCargoMassTip");
    }

    [Test]
    public static void MassAtCapacityIsNotOverCapacity()
    {
        var (overCapacity, _, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 20f,
            massCapacity: 20f
        );

        Assert.That(overCapacity).Is.False();
    }

    [Test]
    public static void MassAboveCapacityIsOverCapacity()
    {
        var (overCapacity, _, _, _) = Rimworld_Dialog_Trade_DoWindowContents.GetCargoMassRowDisplay(
            outgoing: true,
            mass: 20.1f,
            massCapacity: 20f
        );

        Assert.That(overCapacity).Is.True();
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

        Assert.That(flash).Is.True();
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

        Assert.That(flash).Is.True();
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

        Assert.That(flash).Is.False();
    }
}
