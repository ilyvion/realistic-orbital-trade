using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class ShouldShowBlftGenebankWarningTests
{
    [Test]
    public static void ShownWhenMultiItemLoadingOffAndBlftActive() =>
        Expect.IsTrue(Settings.ShouldShowBlftGenebankWarning(false, true));

    [Test]
    public static void HiddenWhenMultiItemLoadingOn() =>
        Expect.IsFalse(Settings.ShouldShowBlftGenebankWarning(true, true));

    [Test]
    public static void HiddenWhenBlftNotActive() =>
        Expect.IsFalse(Settings.ShouldShowBlftGenebankWarning(false, false));
}
