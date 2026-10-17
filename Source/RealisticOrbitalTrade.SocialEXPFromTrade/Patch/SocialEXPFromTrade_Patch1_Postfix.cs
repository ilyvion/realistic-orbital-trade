using System.Diagnostics.CodeAnalysis;
using SocialEXPFromTrade;

namespace RealisticOrbitalTrade.SocialEXPFromTrade.Patch;

/// <summary>
/// Social Experience from Trade's own postfix on <c>TradeDeal.TryExecute</c> (its private
/// <c>Patch1.Postfix</c>) grants experience the instant the trade dialog's "Accept" succeeds -
/// under ROT, that only starts a shuttle delivery that can still fail or be cancelled afterward,
/// which would let a player farm experience by repeatedly accepting and cancelling a trade. This
/// patches their postfix directly (rather than <c>TradeDeal.TryExecute</c> again) so it can reuse
/// their own buy/sell value computation (<c>__state</c>) instead of duplicating it, and defer the
/// actual grant via <see cref="SocialExpGrantTracker"/> until ROT knows the trade was delivered.
/// </summary>
[HarmonyPatch(typeof(Patch1), "Postfix")]
internal static class SocialEXPFromTrade_Patch1_Postfix
{
    // The target method's own parameters happen to be named __state and __result, which are also
    // Harmony's reserved special-parameter names; that reserved-name recognition is based purely
    // on the literal name of OUR patch parameter, so renaming to anything else just gets matched
    // by name against the original's parameters (whose real names are the very tokens we're
    // trying to avoid). HarmonyArgument lets the parameter keep a readable name here while
    // pointing Harmony at the original's positional special names (__0, __1, ...) instead, which
    // bind by index rather than by name.
    internal static bool Prefix(
        [HarmonyArgument("__0")] object[]? state,
        [HarmonyArgument("__1")] ref bool result
    )
    {
        var tradeAgreement = Rimworld_TradeDeal_TryExecute.PendingTradeAgreement;
        Rimworld_TradeDeal_TryExecute.PendingTradeAgreement = null;

        // Their prefix didn't run, so there's no state for their postfix to read: ROT's own
        // TryExecute prefix skipped the original (a renegotiation or a refused accept), and Harmony
        // skips every later prefix that takes a by-ref argument, as theirs does, along with it.
        if (state == null)
        {
            return false;
        }

        // Not a ROT-managed trade (e.g. ordinary caravan trading), the trade failed outright, or
        // Social Experience from Trade itself determined nothing should be granted (state[3]:
        // trading with favor currency) - let it run its own postfix unmodified in every case.
        if (!ShouldDefer(tradeAgreement, result, (bool)state[3]))
        {
            return true;
        }

        SocialExpGrantTracker.Defer(
            tradeAgreement,
            (float)state[0],
            (float)state[1],
            (bool)state[2]
        );
        return false;
    }

    internal static bool ShouldDefer(
        [NotNullWhen(true)] TradeAgreement? tradeAgreement,
        bool result,
        bool favorCurrency
    ) => tradeAgreement != null && result && !favorCurrency;
}
