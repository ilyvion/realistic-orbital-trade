using System;
using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_Blacklisted : QuestPartActivable
{

    public override void QuestPartTick()
    {
        base.QuestPartTick();
        if (Find.TickManager.TicksGame % 60 == 0 && RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Blacklisted)
        {
            // Player has become blacklisted, trigger completion
            Complete();
        }
    }
}

internal static class QuestGen_Blacklisted
{
    public static QuestPart_Blacklisted Blacklisted(
        this Quest quest,
        string? inSignalEnable = null,
        string? inSignalDisable = null,
        string? outSignalBlacklisted = null,
        Action? complete = null
    )
    {
        QuestPart_Blacklisted questPart = new()
        {
            inSignalEnable = QuestGenUtility.HardcodedSignalWithQuestID(inSignalEnable) ?? QuestGen.slate.Get<string>("inSignal"),
            inSignalDisable = inSignalDisable,
        };
        if (!outSignalBlacklisted.NullOrEmpty())
        {
            questPart.outSignalsCompleted.Add(outSignalBlacklisted);
        }
        if (complete != null)
        {
            string blacklistedSignal = QuestGen.GenerateNewSignal("Blacklisted");
            QuestGenUtility.RunInner(complete, blacklistedSignal);
            questPart.outSignalsCompleted.Add(blacklistedSignal);
        }
        quest.AddPart(questPart);
        return questPart;
    }
}
