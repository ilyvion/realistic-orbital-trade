using RimTestRedux;

namespace RealisticOrbitalTrade.Tests;

[TestSuite]
internal static class ShouldShowBlftGenebankWarningTests
{
    [Test]
    public static void ShownWhenMultiItemLoadingOffAndBlftActive() =>
        Assert.That(Settings.ShouldShowBlftGenebankWarning(false, true)).Is.True();

    [Test]
    public static void HiddenWhenMultiItemLoadingOn() =>
        Assert.That(Settings.ShouldShowBlftGenebankWarning(true, true)).Is.False();

    [Test]
    public static void HiddenWhenBlftNotActive() =>
        Assert.That(Settings.ShouldShowBlftGenebankWarning(false, false)).Is.False();
}
