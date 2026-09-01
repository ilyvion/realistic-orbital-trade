namespace RealisticOrbitalTrade.SocialEXPFromTrade;

/// <summary>
/// The buy/sell values Social Experience from Trade's own <c>Patch1.Prefix</c> already computed
/// for one <c>TradeDeal.TryExecute</c> call, held back until ROT knows whether the trade actually
/// got delivered. A single <see cref="TradeAgreement"/> can accumulate more
/// than one of these (e.g. a departure renegotiation runs <c>TryExecute</c> again for the same
/// agreement), so values are summed rather than replaced.
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
