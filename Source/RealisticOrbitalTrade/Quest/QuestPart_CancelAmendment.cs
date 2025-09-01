using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_CancelAmendment : QuestPart_CancelShuttle
{
    protected override string DefaultLabel =>
        "RealisticOrbitalTrade.CancelAmendmentLabel".Translate();

    protected override string DefaultDesc =>
        "RealisticOrbitalTrade.CancelAmendmentDesc".Translate();
}

internal static class QuestGen_CancelAmendment
{
    public static QuestPart_CancelAmendment CancelAmendment(
        this Quest quest,
        Thing shuttle,
        string? inSignalEnable = null,
        string? inSignalDisable = null,
        string? outSignalCancelled = null
    )
    {
        QuestPart_CancelAmendment questPart = new()
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
