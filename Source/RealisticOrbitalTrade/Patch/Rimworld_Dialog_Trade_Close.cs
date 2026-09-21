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

    private static CloseAction CurrentAction() =>
        DecideClose(
            Utils.RenegotiatingBecauseDeparting,
            Utils.DepartureRenegotiationCancelConfirmed,
            Utils.RenegotiatingAgreement != null
        );

    private static bool Prefix(Dialog_Trade __instance)
    {
        if (CurrentAction() != CloseAction.BlockAndConfirm)
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
        return false;
    }

    private static void Postfix()
    {
        // Harmony runs Postfix even when Prefix returned false and skipped the original method -
        // when Prefix blocked this call to show the confirmation dialog above, the real Close()
        // never ran and nothing below should either; it'll run for real once the player confirms
        // and Prefix lets __instance.Close() through on its own.
        var action = CurrentAction();
        if (action == CloseAction.BlockAndConfirm)
        {
            return;
        }

        // Closing a departure-triggered renegotiation without accepting means the player chose
        // (and, per Prefix, confirmed) to let the trade fail rather than take partial payment -
        // firing the same outSignalFailed that Utils.TryBeginDepartureRenegotiation's other
        // decline paths use lets QuestPart_TryBeginDepartureRenegotiation's caller run its usual
        // dump-and-fail quest parts (properly unloading the shuttle, sending the expired-trade
        // letter, etc.) instead of duplicating that logic - and instead of the manual "cancel
        // trade" gizmo's action, which skips all of that and just lets the trader keep whatever's
        // already loaded.
        if (action == CloseAction.FailTrade)
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
        if (action == CloseAction.NoOp)
        {
            TradeShipData.tradeAgreementForQuest = null;
            Utils.EndRenegotiation();
            return;
        }

        TradeShipData.EndTradeAgreementIfExists();
    }
}
