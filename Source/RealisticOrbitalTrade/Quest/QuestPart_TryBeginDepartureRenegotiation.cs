using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

/// <summary>
/// Runs <see cref="Utils.TryBeginDepartureRenegotiation"/> when <see cref="inSignal"/> is received
/// (the trade shuttle's departure timer actually expiring), and fires
/// <see cref="outSignalFailed"/> if it declines to renegotiate (nothing loaded to salvage, or no
/// negotiator available) - or, once the renegotiation dialog is open,
/// <see cref="Patch.Rimworld_Dialog_Trade_Close"/> fires it too if the player closes without
/// accepting - so the caller's usual dump-and-fail quest parts can run instead. Has to be a real
/// <see cref="QuestPart"/> - listening for a signal fired at gameplay time - rather than a plain
/// call inside a <c>complete</c> callback, since <c>QuestNode.RunInt</c> only builds the quest
/// graph once, at quest-generation time, long before the shuttle is loaded at all.
/// </summary>
internal class QuestPart_TryBeginDepartureRenegotiation : QuestPart
{
    public string? inSignal;
    public string? outSignalFailed;
    public TradeAgreement? tradeAgreement;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (
            signal.tag == inSignal
            && !Utils.TryBeginDepartureRenegotiation(tradeAgreement!, outSignalFailed!)
        )
        {
            Find.SignalManager.SendSignal(new Signal(outSignalFailed));
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_Values.Look(ref outSignalFailed, "outSignalFailed");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
    }
}

internal static class QuestGen_TryBeginDepartureRenegotiation
{
    public static QuestPart_TryBeginDepartureRenegotiation TryBeginDepartureRenegotiation(
        this Quest quest,
        TradeAgreement tradeAgreement,
        string outSignalFailed,
        string? inSignal = null
    )
    {
        QuestPart_TryBeginDepartureRenegotiation questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            outSignalFailed = outSignalFailed,
            tradeAgreement = tradeAgreement,
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
