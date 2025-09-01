using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

[StaticConstructorOnStartup]
internal class QuestPart_CancelTrade : QuestPart_CancelShuttle
{
    protected override string DefaultLabel =>
        "RealisticOrbitalTrade.CancelTradeAgreementLabel".Translate();

    protected override string DefaultDesc =>
        "RealisticOrbitalTrade.CancelTradeAgreementDesc".Translate();
}

internal static class QuestGen_CancelTrade
{
    public static QuestPart_CancelTrade CancelTrade(
        this Quest quest,
        Thing shuttle,
        string? inSignalEnable = null,
        string? inSignalDisable = null,
        string? outSignalCancelled = null
    )
    {
        QuestPart_CancelTrade questPart = new()
        {
            inSignalEnable =
                QuestGenUtility.HardcodedSignalWithQuestID(inSignalEnable)
                ?? QuestGen.slate.Get<string>("inSignal"),
            inSignalDisable = QuestGenUtility.HardcodedSignalWithQuestID(inSignalDisable),
            shuttle = shuttle,
        };
        if (outSignalCancelled != null)
        {
            questPart.outSignalsCompleted.Add(outSignalCancelled);
        }
        quest.AddPart(questPart);
        return questPart;
    }
}
