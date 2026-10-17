namespace RealisticOrbitalTrade.SocialEXPFromTrade;

/// <summary>
/// The buy/sell values Social Experience from Trade's own <c>Patch1.Prefix</c> already computed
/// for one <c>TradeDeal.TryExecute</c> call, held back until ROT knows whether the trade actually
/// got delivered. Deferring more than once for the same <see cref="TradeAgreement"/> sums the
/// values; a renegotiation replaces them instead (see <see cref="SocialExpGrantTracker.Renegotiate"/>).
/// </summary>
internal readonly record struct PendingSocialExpGain(float BuyValue, float SellValue, bool Inspired)
{
    internal PendingSocialExpGain Accumulate(float buyValue, float sellValue, bool inspired) =>
        new(BuyValue + buyValue, SellValue + sellValue, Inspired || inspired);

    /// <summary>
    /// Mirrors Social Experience from Trade's own <c>Patch1.Postfix</c> experience formula.
    /// </summary>
    internal float ComputeExperience(
        float buyingMultiplier,
        float sellingMultiplier,
        float inspiredMultiplier
    )
    {
        var experience = (BuyValue * buyingMultiplier) + (SellValue * sellingMultiplier);
        if (Inspired)
        {
            experience *= inspiredMultiplier;
        }
        return experience;
    }
}
