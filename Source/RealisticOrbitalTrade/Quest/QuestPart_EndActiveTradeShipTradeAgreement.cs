using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_EndActiveTradeShipTradeAgreement : QuestPart
{
    public string? inSignal;

    public TradeAgreement? tradeAgreement;

    public bool successful;

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            if (tradeAgreement == null)
            {
                RealisticOrbitalTradeMod.Instance.LogError(
                    "tradeAgreement is null in QuestPart_EndActiveTradeShipTradeAgreement. This is a bug, can't end trade agreement."
                );
                return;
            }
            RealisticOrbitalTradeGameComponent.Current.EndTradeAgreement(
                tradeAgreement,
                successful
            );
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_Values.Look(ref successful, "successful", false);
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
    }
}

internal static class QuestGen_EndActiveTradeShipTradeAgreement
{
    public static QuestPart_EndActiveTradeShipTradeAgreement EndActiveTradeShipTradeAgreement(
        this Quest quest,
        TradeAgreement tradeAgreement,
        string? inSignal = null,
        bool successful = false
    )
    {
        QuestPart_EndActiveTradeShipTradeAgreement questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
            tradeAgreement = tradeAgreement,
            successful = successful,
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
