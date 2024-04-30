using RimWorld;
using RimWorld.QuestGen;
using Verse;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_BlacklistPlayerFactionInOrbitalTrade : QuestPart
{
    public string? inSignal;

    public QuestPart_BlacklistPlayerFactionInOrbitalTrade()
    {
    }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            RealisticOrbitalTradeGameComponent.Current.Standing = Standing.Blacklisted;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
    }
}

internal static class QuestGen_BlacklistPlayerFactionInOrbitalTrade
{
    public static QuestPart_BlacklistPlayerFactionInOrbitalTrade BlacklistPlayerFactionInOrbitalTrade(this Quest quest, string? inSignal = null)
    {
        QuestPart_BlacklistPlayerFactionInOrbitalTrade questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal")
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
