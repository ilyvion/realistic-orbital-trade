using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

internal class QuestPart_TradeDropPods : QuestPart
{
    public string? inSignal;

    public TradeAgreement? tradeAgreement;

    public IntVec3 dropSpot = IntVec3.Invalid;

    public bool dropAllInSamePod;

    private readonly List<Thing> tmpThingsToDrop = [];

    public override void Notify_QuestSignalReceived(Signal signal)
    {
        base.Notify_QuestSignalReceived(signal);
        if (!(signal.tag == inSignal) || tradeAgreement == null)
        {
            return;
        }

        // Add things to drop to tmpThingsToDrop
        tmpThingsToDrop.Clear();
        tmpThingsToDrop.AddRange(tradeAgreement.thingsSoldToPlayer);

        // Then remove the things from the thingsSoldToPlayer ThingOwner.
        var list = (IList<Thing>)tradeAgreement.thingsSoldToPlayer;
        for (var i = list.Count - 1; i >= 0; i--)
        {
            var thing = list[i];
            _ = thing.holdingOwner.Remove(thing);
        }
        tradeAgreement.thingsSoldToPlayer.Clear();

        if (!tmpThingsToDrop.Any())
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "QuestPart_TradeDropPods: No things to drop for trade agreement."
            );
            return;
        }

        var map = tradeAgreement.tradeShip.Map;
        var intVec = dropSpot.IsValid ? dropSpot : GetRandomDropSpot();

        if (dropAllInSamePod)
        {
            DropPodUtility.DropThingGroupsNear(
                intVec,
                map,
                [tmpThingsToDrop],
                110,
                canRoofPunch: false,
                forbid: false,
                allowFogged: false
            );
        }
        else
        {
            DropPodUtility.DropThingsNear(
                intVec,
                map,
                tmpThingsToDrop,
                110,
                canRoofPunch: false,
                forbid: false,
                allowFogged: false
            );
        }
    }

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
    }

    private IntVec3 GetRandomDropSpot()
    {
        var map = tradeAgreement!.tradeShip.Map;
        return DropCellFinder.TradeDropSpot(map);
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref inSignal, "inSignal");
        Scribe_Values.Look(ref dropSpot, "dropSpot");
        Scribe_Values.Look(ref dropAllInSamePod, "dropAllInSamePod");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
    }
}

internal static class QuestGen_TradeDropPods
{
    public static QuestPart_TradeDropPods TradeDropPods(
        this Quest quest,
        TradeAgreement tradeAgreement,
        string? inSignal = null,
        QuestPart.SignalListenMode signalListenMode = QuestPart.SignalListenMode.OngoingOnly,
        IntVec3? dropSpot = null,
        bool dropAllInSamePod = false
    )
    {
        var dropPods = new QuestPart_TradeDropPods
        {
            tradeAgreement = tradeAgreement,
            inSignal =
                QuestGenUtility.HardcodedSignalWithQuestID(inSignal)
                ?? QuestGen.slate.Get<string>("inSignal"),
            signalListenMode = signalListenMode,
            dropAllInSamePod = dropAllInSamePod,
        };
        if (dropSpot.HasValue)
        {
            dropPods.dropSpot = dropSpot.Value;
        }
        quest.AddPart(dropPods);
        return dropPods;
    }
}
