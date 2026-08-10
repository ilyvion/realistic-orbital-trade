using DynamicTradeInterface.UserInterface;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// Window_DynamicTrade doesn't override OnCloseRequest/PostClose itself, so both patches below
// actually target the base Window method and must guard on __instance to stay scoped to DTI's
// trade window - mirrors Rimworld_Dialog_Trade_Close, which patches Dialog_Trade.Close directly
// instead, since vanilla's trade dialog does override that one.
[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_OnCloseRequest
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.OnCloseRequest));

    // Closing a departure-triggered renegotiation without accepting fails the trade (see the
    // PostClose patch below) - block the close and ask for confirmation first, same as clicking
    // "cancel trade" directly, so it can't happen by accident from a stray Escape press or misclick.
    internal static bool Prefix(Window __instance, ref bool __result)
    {
        if (
            __instance is not Window_DynamicTrade
            || !Utils.RenegotiatingBecauseDeparting
            || Utils.DepartureRenegotiationCancelConfirmed
        )
        {
            return true;
        }

        Find.WindowStack.Add(
            Dialog_MessageBox.CreateConfirmation(
                "RealisticOrbitalTrade.ConfirmDepartureRenegotiationCancel".Translate(),
                () =>
                {
                    Utils.DepartureRenegotiationCancelConfirmed = true;
                    __instance.Close();
                }
            )
        );
        __result = false;
        return false;
    }
}

[HarmonyPatch]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_PostClose
{
    internal static MethodInfo TargetMethod() =>
        AccessTools.Method(typeof(Window_DynamicTrade), nameof(Window_DynamicTrade.PostClose));

    internal static void Postfix(Window __instance)
    {
        if (__instance is not Window_DynamicTrade)
        {
            return;
        }

        // Closing a departure-triggered renegotiation without accepting means the player chose
        // (and, per the OnCloseRequest patch above, confirmed) to let the trade fail rather than
        // take partial payment - firing the same outSignalFailed that
        // Utils.TryBeginDepartureRenegotiation's other decline paths use lets
        // QuestPart_TryBeginDepartureRenegotiation's caller run its usual dump-and-fail quest parts
        // (properly unloading the shuttle, sending the expired-trade letter, etc.) - see
        // Rimworld_Dialog_Trade_Close, which this mirrors for DTI's own trade window.
        if (Utils.RenegotiatingBecauseDeparting)
        {
            var failedSignal = Utils.DepartureRenegotiationFailedSignal;
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();

            if (failedSignal != null)
            {
                Find.SignalManager.SendSignal(new Signal(failedSignal));
            }
            else
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "Can't fail trade after declining a departure renegotiation; DepartureRenegotiationFailedSignal is null!"
                );
            }
            return;
        }

        // Closing without accepting is a no-op for an ordinary renegotiation - unlike a fresh
        // trade, the agreement is already a real, in-progress trade (possibly with cargo already
        // loaded), not something safe to discard.
        if (Utils.RenegotiatingAgreement != null)
        {
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();
            return;
        }

        TradeShipData.EndTradeAgreementIfExists();
    }
}
