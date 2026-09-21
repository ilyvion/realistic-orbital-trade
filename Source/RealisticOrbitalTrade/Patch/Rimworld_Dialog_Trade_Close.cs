namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.Close))]
internal static class Rimworld_Dialog_Trade_Close
{
    // Closing a departure-triggered renegotiation without accepting fails the trade (see Postfix)
    // - block the close and ask for confirmation first, same as clicking "cancel trade" directly,
    // so it can't happen by accident from a stray Escape press or misclick.
    internal enum CloseAction
    {
        BlockAndConfirm,
        FailTrade,
        NoOp,
        EndAgreement,
    }

    internal static CloseAction DecideClose(
        bool departing,
        bool cancelConfirmed,
        bool renegotiating
    ) =>
        departing ? (cancelConfirmed ? CloseAction.FailTrade : CloseAction.BlockAndConfirm)
        : renegotiating ? CloseAction.NoOp
        : CloseAction.EndAgreement;

    internal static CloseAction CurrentAction() =>
        DecideClose(
            Utils.RenegotiatingBecauseDeparting,
            Utils.DepartureRenegotiationCancelConfirmed,
            Utils.RenegotiatingAgreement != null
        );

    // Shared with the DTI trade window's close patches.
    internal static void ShowCancelConfirmation(Window window) =>
        Find.WindowStack.Add(
            Dialog_MessageBox.CreateConfirmation(
                "RealisticOrbitalTrade.ConfirmDepartureRenegotiationCancel".Translate(),
                () =>
                {
                    Utils.DepartureRenegotiationCancelConfirmed = true;
                    window.Close();
                }
            )
        );

    private static bool Prefix(Dialog_Trade __instance)
    {
        if (CurrentAction() != CloseAction.BlockAndConfirm)
        {
            return true;
        }

        ShowCancelConfirmation(__instance);
        return false;
    }

    // Harmony runs Postfix even when Prefix returned false and skipped the original method;
    // ApplyClose ignores BlockAndConfirm, which is the case where the close never happened.
    private static void Postfix() => ApplyClose(CurrentAction());

    // Applies the trade-state consequences of a trade window having closed.
    internal static void ApplyClose(CloseAction action)
    {
        switch (action)
        {
            case CloseAction.BlockAndConfirm:
                return;
            case CloseAction.FailTrade:
                // Fires the same outSignalFailed that Utils.TryBeginDepartureRenegotiation's other
                // decline paths use, so the quest's usual dump-and-fail parts run (unloading the
                // shuttle, sending the expired-trade letter, etc.).
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
            case CloseAction.NoOp:
                // An ordinary renegotiation's agreement is a real, in-progress trade (possibly
                // with cargo already loaded), so closing only drops the renegotiation.
                TradeShipData.tradeAgreementForQuest = null;
                Utils.EndRenegotiation();
                return;
            case CloseAction.EndAgreement:
                TradeShipData.EndTradeAgreementIfExists();
                return;
            default:
                return;
        }
    }
}
