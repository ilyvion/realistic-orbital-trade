using RealisticOrbitalTrade.Core;
using RimWorld.Planet;
using RimWorld.QuestGen;
using RWShipJobDefOf = RimWorld.ShipJobDefOf;

namespace RealisticOrbitalTrade.Quests;

/// <summary>
/// Delivers what the player bought, waiting in <see cref="TradeAgreement.thingsSoldToPlayer"/>, by
/// shuttle: each shuttle takes as much as it can carry, lands, unloads and leaves, and the next one
/// arrives after it has gone, until everything has been delivered. Completes once the last one has
/// left.
/// </summary>
internal class QuestPart_DeliverBoughtItemsByShuttles : QuestPartActivable
{
    /// <summary>How long after the previous shuttle leaves (or after enabling) the next one arrives.</summary>
    internal const int TicksBetweenShuttles = 500;

    public TradeAgreement? tradeAgreement;
    public MapParent? mapParent;

    // Tags every delivery shuttle, so the quest's signals for the shuttle cover each of them.
    public string? shuttleQuestTag;

    // The quest's own tag, carried by every shuttle and transport ship it sends.
    public string? questTag;

    public TransportShip? currentTransportShip;
    public int nextShuttleTick = -1;
    public int shuttlesSent;

    private string ShuttleSentSignal =>
        QuestGenUtility.QuestTagSignal(shuttleQuestTag, "SentSatisfied");

    protected override void Enable(SignalArgs receivedArgs)
    {
        base.Enable(receivedArgs);
        ScheduleNextShuttle();
    }

    public override void QuestPartTick()
    {
        base.QuestPartTick();
        if (nextShuttleTick >= 0 && Find.TickManager.TicksGame >= nextShuttleTick)
        {
            nextShuttleTick = -1;
            SendNextShuttle();
        }
    }

    protected override void ProcessQuestSignal(Signal signal)
    {
        base.ProcessQuestSignal(signal);
        if (signal.tag != ShuttleSentSignal || currentTransportShip == null)
        {
            return;
        }

        currentTransportShip = null;
        if (tradeAgreement!.thingsSoldToPlayer.Any)
        {
            ScheduleNextShuttle();
        }
        else
        {
            Complete();
        }
    }

    private void ScheduleNextShuttle()
    {
        nextShuttleTick = Find.TickManager.TicksGame + TicksBetweenShuttles;
        KeepTraderInOrbit();
    }

    // The trader waits in orbit until every shuttle it sends has delivered its share.
    private void KeepTraderInOrbit()
    {
        var tradeShip = tradeAgreement!.tradeShip;
        tradeShip.ticksUntilDeparture =
            QuestPart_ExtendTradeShipDepartureIfVeryShort.ExtendIfVeryShort(
                tradeShip.ticksUntilDeparture
            );
    }

    private void SendNextShuttle()
    {
        if (mapParent is not { HasMap: true })
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "The map bought items were to be delivered to no longer exists; ending the delivery."
            );
            Complete();
            return;
        }

        var shuttle = QuestUtils.MakeTradeShuttle();
        QuestUtility.AddQuestTag(ref shuttle.questTags, questTag);
        QuestUtility.AddQuestTag(ref shuttle.questTags, shuttleQuestTag);
        var transportShip = TransportShipMaker.MakeTransportShip(
            TransportShipDefOf.Ship_Shuttle,
            null,
            shuttle
        );
        QuestUtility.AddQuestTag(ref transportShip.questTags, questTag);

        LoadNextShare(
            tradeAgreement!.thingsSoldToPlayer,
            transportShip.TransporterComp.innerContainer,
            Patch.Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity
        );

        var arrive = (ShipJob_Arrive)ShipJobMaker.MakeShipJob(RWShipJobDefOf.Arrive);
        arrive.mapParent = mapParent;
        arrive.cell = QuestUtils.FindLandingSpot(mapParent.Map);
        transportShip.AddJob(arrive);
        var unload = (ShipJob_Unload)ShipJobMaker.MakeShipJob(RWShipJobDefOf.Unload);
        unload.unforbidAll = true;
        transportShip.AddJob(unload);
        var wait = (ShipJob_WaitTime)ShipJobMaker.MakeShipJob(RWShipJobDefOf.WaitTime);
        wait.duration = 0;
        wait.leaveImmediatelyWhenSatisfied = true;
        transportShip.AddJob(wait);

        currentTransportShip = transportShip;
        shuttlesSent++;
        KeepTraderInOrbit();
        transportShip.Start();
    }

    /// <summary>
    /// Moves as much of <paramref name="pending"/> as one shuttle of <paramref name="capacity"/> kg
    /// can carry into <paramref name="shuttleContents"/>, or all of it if something in it is too
    /// heavy for a shuttle.
    /// </summary>
    internal static void LoadNextShare(
        ThingOwner pending,
        ThingOwner shuttleContents,
        float capacity
    )
    {
        var things = pending.ToList();
        foreach (var (lineId, count) in NextShare(things, capacity))
        {
            _ = pending.TryTransferToContainer(things[lineId], shuttleContents, count);
        }
    }

    /// <summary>
    /// Which of <paramref name="things"/> (by index) and how many of each the next shuttle of
    /// <paramref name="capacity"/> kg takes, or all of them if one is too heavy for a shuttle.
    /// </summary>
    internal static List<(int lineId, int count)> NextShare(
        IReadOnlyList<Thing> things,
        float capacity
    ) =>
        PlanShuttles(things, capacity)?[0]
        ?? [.. things.Select((thing, i) => (i, thing.stackCount))];

    /// <summary>
    /// How <paramref name="things"/> divide across shuttles of <paramref name="capacity"/> kg, with
    /// each planned line's id its index in <paramref name="things"/>, or <see langword="null"/> if
    /// one is too heavy for a shuttle.
    /// </summary>
    internal static List<List<(int lineId, int count)>>? PlanShuttles(
        IReadOnlyList<Thing> things,
        float capacity
    ) =>
        ShuttleLoadPlanner.Plan(
            capacity,
            0f,
            [
                .. things.Select(
                    (thing, i) =>
                        thing is Pawn
                            ? new ShuttleLoadPlanner.Line(i, TradeAgreement.PawnCargoMass(thing), 1)
                            : new ShuttleLoadPlanner.Line(
                                i,
                                thing.GetStatValue(StatDefOf.Mass),
                                thing.stackCount
                            )
                ),
            ]
        );

    public override bool QuestPartReserves(Pawn p) =>
        p != null && currentTransportShip?.TransporterComp?.innerContainer.Contains(p) == true;

    public override bool QuestPartReserves(TransportShip ship) =>
        ship != null && ship == currentTransportShip;

    public override void Cleanup()
    {
        base.Cleanup();
        tradeAgreement = null;
        mapParent = null;
        currentTransportShip = null;
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_References.Look(ref mapParent, "mapParent");
        Scribe_Values.Look(ref shuttleQuestTag, "shuttleQuestTag");
        Scribe_Values.Look(ref questTag, "questTag");
        Scribe_References.Look(ref currentTransportShip, "currentTransportShip");
        Scribe_Values.Look(ref nextShuttleTick, "nextShuttleTick", -1);
        Scribe_Values.Look(ref shuttlesSent, "shuttlesSent");
    }
}

internal static class QuestGen_DeliverBoughtItemsByShuttles
{
    public static QuestPart_DeliverBoughtItemsByShuttles DeliverBoughtItemsByShuttles(
        this Quest quest,
        TradeAgreement tradeAgreement,
        MapParent mapParent,
        string questTag,
        string shuttleQuestTag,
        string inSignalEnable,
        string outSignalDelivered
    )
    {
        QuestPart_DeliverBoughtItemsByShuttles questPart = new()
        {
            tradeAgreement = tradeAgreement,
            mapParent = mapParent,
            questTag = questTag,
            shuttleQuestTag = shuttleQuestTag,
            inSignalEnable = inSignalEnable,
        };
        questPart.outSignalsCompleted.Add(outSignalDelivered);
        quest.AddPart(questPart);
        return questPart;
    }
}
