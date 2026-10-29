using DevTools.Testing;

namespace RealisticOrbitalTrade.Tests;

[TestFixture(TestType.MainMenu)]
internal sealed class SettingsTests
{
    [Test]
    public static void LoadingSettingsRefreshesTheNumericFields()
    {
        var originalThreshold = Settings._minimumTradeThreshold.Value;
        var originalBaseCost = Settings._buyMoreTimeBaseSilverCost.Value;
        try
        {
            Settings._minimumTradeThreshold.Value = 1234;
            Settings._buyMoreTimeBaseSilverCost.Value = 567;

            ScribeRoundTrip.Run(
                new Settings(),
                new Settings(),
                () =>
                {
                    Settings._minimumTradeThreshold.Value = 600;
                    Settings._buyMoreTimeBaseSilverCost.Value = 200;
                    _ = Settings._minimumTradeThreshold.Buffer;
                    _ = Settings._buyMoreTimeBaseSilverCost.Buffer;
                }
            );

            Expect.AreEqual(1234, Settings._minimumTradeThreshold.Value);
            Expect.AreEqual(
                1234.ToString(CultureInfo.CurrentCulture),
                Settings._minimumTradeThreshold.Buffer
            );
            Expect.AreEqual(567, Settings._buyMoreTimeBaseSilverCost.Value);
            Expect.AreEqual(
                567.ToString(CultureInfo.CurrentCulture),
                Settings._buyMoreTimeBaseSilverCost.Buffer
            );
        }
        finally
        {
            Settings._minimumTradeThreshold.Value = originalThreshold;
            Settings._buyMoreTimeBaseSilverCost.Value = originalBaseCost;
        }
    }
}
