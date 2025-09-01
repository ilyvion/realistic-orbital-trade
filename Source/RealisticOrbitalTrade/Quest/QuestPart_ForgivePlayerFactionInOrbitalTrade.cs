using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_ForgivePlayerFactionInOrbitalTrade : QuestPart
{
    public string? inSignal;

    public QuestPart_ForgivePlayerFactionInOrbitalTrade() { }

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (signal.tag == inSignal)
        {
            RealisticOrbitalTradeGameComponent.Current.Standing = Standing.Forgiven;
        }
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
    }
}

internal static class QuestGen_ForgivePlayerFactionInOrbitalTrade
{
    public static QuestPart_ForgivePlayerFactionInOrbitalTrade ForgivePlayerFactionInOrbitalTrade(
        this Quest quest,
        string? inSignal = null
    )
    {
        QuestPart_ForgivePlayerFactionInOrbitalTrade questPart = new()
        {
            inSignal = inSignal ?? QuestGen.slate.Get<string>("inSignal"),
        };
        quest.AddPart(questPart);
        return questPart;
    }
}
