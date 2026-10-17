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
        var renegotiatedDeal = Rimworld_TradeDeal_TryExecute.PendingRenegotiatedDeal;
        Rimworld_TradeDeal_TryExecute.PendingTradeAgreement = null;
        Rimworld_TradeDeal_TryExecute.PendingRenegotiatedDeal = null;

        // Their prefix didn't run, so there's no state for their postfix to read: ROT's own
        // TryExecute prefix skipped the original (a renegotiation or a refused accept), and Harmony
        // skips every later prefix that takes a by-ref argument, as theirs does, along with it -
        // when theirs is ordered after ROT's. An accepted renegotiation is valued here instead.
        if (state == null)
        {
            if (result && renegotiatedDeal != null)
            {
                state = ComputeTradeValues(renegotiatedDeal);
                if (ShouldDefer(tradeAgreement, result, (bool)state[3]))
                {
                    HoldBack(tradeAgreement, true, state);
                }
            }
            return false;
        }

        // Not a ROT-managed trade (e.g. ordinary caravan trading), the trade failed outright, or
        // Social Experience from Trade itself determined nothing should be granted (state[3]:
        // trading with favor currency) - let it run its own postfix unmodified in every case.
        if (!ShouldDefer(tradeAgreement, result, (bool)state[3]))
        {
            return true;
        }

        HoldBack(tradeAgreement, renegotiatedDeal != null, state);
        return false;
    }

    /// <summary>
    /// Holds back the values in <paramref name="state"/> (laid out as Social Experience from
    /// Trade's <c>__state</c>). A renegotiated deal's tradeables hold the whole new deal, so its
    /// values replace what was held back rather than adding to it.
    /// </summary>
    internal static void HoldBack(TradeAgreement tradeAgreement, bool renegotiation, object[] state)
    {
        if (renegotiation)
        {
            SocialExpGrantTracker.Renegotiate(tradeAgreement, (float)state[0], (float)state[1]);
        }
        else
        {
            SocialExpGrantTracker.Defer(
                tradeAgreement,
                (float)state[0],
                (float)state[1],
                (bool)state[2]
            );
        }
    }

    private delegate void TheirPrefix(
        ref TradeDeal tradeDeal,
        ref List<Tradeable> tradeables,
        ref object[] state
    );

    private static TheirPrefix? _theirPrefix;

    /// <summary>
    /// Runs Social Experience from Trade's own <c>Patch1.Prefix</c> on <paramref name="tradeDeal"/>
    /// and returns the <c>__state</c> it computes.
    /// </summary>
    private static object[] ComputeTradeValues(TradeDeal tradeDeal)
    {
        _theirPrefix ??= AccessTools.MethodDelegate<TheirPrefix>(
            AccessTools.Method(typeof(Patch1), "Prefix")
        );

        var tradeables = tradeDeal.AllTradeables;
        object[] state = null!;
        _theirPrefix(ref tradeDeal, ref tradeables, ref state);
        return state;
    }

    internal static bool ShouldDefer(
        [NotNullWhen(true)] TradeAgreement? tradeAgreement,
        bool result,
        bool favorCurrency
    ) => tradeAgreement != null && result && !favorCurrency;
}
