using RimTestRedux;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Tests;

// Regression/coverage guard for PendingSocialExpGain: Social Experience from Trade's own postfix
// on TradeDeal.TryExecute grants experience the instant a trade is accepted, which under ROT is
// before delivery actually happens - see SocialEXPFromTrade_Patch1_Postfix for where this holds
// the buy/sell values back instead of granting immediately. This is the pure accumulation/formula
// logic that deferral relies on, mirroring Social Experience from Trade's own Patch1.Postfix math.
[TestSuite]
internal static class PendingSocialExpGainTests
{
    [Test]
    public static void AccumulateSumsBuyAndSellValuesSeparately()
    {
        var first = new PendingSocialExpGain(10f, 20f, false);

        var result = first.Accumulate(5f, 7f, false);

        Assert.That(result.BuyValue).Is.EqualTo(15f);
        Assert.That(result.SellValue).Is.EqualTo(27f);
    }

    [Test]
    public static void AccumulateStickyKeepsInspiredOnceTrue()
    {
        var first = new PendingSocialExpGain(0f, 0f, true);

        var result = first.Accumulate(0f, 0f, false);

        Assert.That(result.Inspired).Is.EqualTo(true);
    }

    [Test]
    public static void AccumulateSetsInspiredWhenTheNewContributionIsInspired()
    {
        var first = new PendingSocialExpGain(0f, 0f, false);

        var result = first.Accumulate(0f, 0f, true);

        Assert.That(result.Inspired).Is.EqualTo(true);
    }

    [Test]
    public static void ComputeExperienceAppliesBuyingAndSellingMultipliersSeparately()
    {
        var pending = new PendingSocialExpGain(BuyValue: 10f, SellValue: 20f, Inspired: false);

        var result = pending.ComputeExperience(
            buyingMultiplier: 2f,
            sellingMultiplier: 3f,
            inspiredMultiplier: 5f
        );

        // (10 * 2) + (20 * 3) = 80; inspiredMultiplier is not applied since Inspired is false.
        Assert.That(result).Is.EqualTo(80f);
    }

    [Test]
    public static void ComputeExperienceAppliesInspiredMultiplierToTheCombinedTotal()
    {
        var pending = new PendingSocialExpGain(BuyValue: 10f, SellValue: 20f, Inspired: true);

        var result = pending.ComputeExperience(
            buyingMultiplier: 2f,
            sellingMultiplier: 3f,
            inspiredMultiplier: 5f
        );

        // ((10 * 2) + (20 * 3)) * 5 = 400
        Assert.That(result).Is.EqualTo(400f);
    }
}
