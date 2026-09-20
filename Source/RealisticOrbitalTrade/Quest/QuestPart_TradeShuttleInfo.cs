using RealisticOrbitalTrade.Comps;
using RimWorld.QuestGen;

namespace RealisticOrbitalTrade.Quests;

[StaticConstructorOnStartup]
internal class QuestPart_TradeShuttleLeaveDelay : QuestPart_ShuttleLeaveDelay
{
    public bool tradePausesDepartureTimer;
    public string? traderName;

    public override bool AlertCritical => !tradePausesDepartureTimer && base.AlertCritical;

    public override string AlertLabel =>
        !tradePausesDepartureTimer
            ? (string)
                "RealisticOrbitalTrade.QuestPartShuttleLeaveDelay".Translate(
                    TicksLeft.ToStringTicksToPeriodVerbose()
                )
            : (string)"RealisticOrbitalTrade.ActiveTradeShuttle".Translate();

    public override string AlertExplanation =>
        !tradePausesDepartureTimer
            ? quest.hidden
                ? (string)
                    "RealisticOrbitalTrade.QuestPartShuttleLeaveDelayDescHidden".Translate(
                        TicksLeft.ToStringTicksToPeriodVerbose().Colorize(ColoredText.DateTimeColor)
                    )
                : (string)
                    "RealisticOrbitalTrade.QuestPartShuttleLeaveDelayDesc".Translate(
                        quest.name,
                        TicksLeft
                            .ToStringTicksToPeriodVerbose()
                            .Colorize(ColoredText.DateTimeColor),
                        AllRequiredThingsLabel
                    )
            : (string)
                "RealisticOrbitalTrade.TradeShuttleRequires".Translate(
                    quest.name,
                    AllRequiredThingsLabel
                );

    // Every to-trader shuttle's outstanding cargo, not just this part's own shuttle's.
    private string AllRequiredThingsLabel =>
        string.Join(
            "\n",
            (shuttle.TryGetComp<CompTradeShuttle>()?.tradeAgreement?.ToTraderTransportShips ?? [])
                .Select(ship => (string)ship.ShuttleComp.RequiredThingsLabel)
                .Where(label => !label.NullOrEmpty())
        );

    public override string? ExpiryInfoPart =>
        !tradePausesDepartureTimer ? base.ExpiryInfoPart : null;

    public override string? ExtraInspectString(ISelectable target)
    {
        if (target == shuttle)
        {
            var text = "RealisticOrbitalTrade.InvolvedInTradeWithInspectString".Translate(
                traderName
            );
            if (!tradePausesDepartureTimer)
            {
                var baseText = base.ExtraInspectString(target);
                if (!baseText.NullOrEmpty())
                {
                    text += "\n" + baseText;
                }
            }
            return text;
        }
        return null;
    }

    public override void QuestPartTick()
    {
        if (!tradePausesDepartureTimer)
        {
            base.QuestPartTick();
        }
    }

    public override void PostQuestAdded()
    {
        base.PostQuestAdded();
        WireUpBuyMoreTimeAction();
    }

    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref tradePausesDepartureTimer, "tradePausesDepartureTimer");
        Scribe_Values.Look(ref traderName, "traderName");

        if (Scribe.mode == LoadSaveMode.PostLoadInit)
        {
            WireUpBuyMoreTimeAction();
        }
    }

    // If the departure timer is paused, there's nothing for "buy more time" to extend.
    private void WireUpBuyMoreTimeAction()
    {
        if (tradePausesDepartureTimer)
        {
            return;
        }
        if (shuttle?.TryGetComp<CompTradeShuttle>(out var compTradeShuttle) ?? false)
        {
            compTradeShuttle.extendDepartureTimeAction = ticks =>
                ExtendDepartureTime(
                    ref delayTicks,
                    compTradeShuttle.tradeAgreement!.tradeShip,
                    ticks
                );
        }
    }

    // The trade ship's own departure timer runs independently of the shuttle's leave delay -
    // without extending it too, the trade ship can depart (and get despawned) while the shuttle
    // it's still waiting on is left with a dangling reference to it.
    internal static void ExtendDepartureTime(ref int delayTicks, TradeShip tradeShip, int ticks)
    {
        delayTicks += ticks;
        tradeShip.ticksUntilDeparture += ticks;
    }
}

internal static class QuestGen_TradeShuttleLeaveDelay
{
    public static QuestPart_TradeShuttleLeaveDelay TradeShuttleLeaveDelay(
        this Quest quest,
        Thing shuttle,
        bool tradePausesDepartureTimer,
        string traderName,
        int delayTicks,
        string? inSignalEnable = null,
        IEnumerable<string>? inSignalsDisable = null,
        string? outSignalComplete = null,
        Action? complete = null
    )
    {
        QuestPart_TradeShuttleLeaveDelay questPart = new()
        {
            inSignalEnable =
                QuestGenUtility.HardcodedSignalWithQuestID(inSignalEnable)
                ?? QuestGen.slate.Get<string>("inSignal"),
            delayTicks = delayTicks,
            tradePausesDepartureTimer = tradePausesDepartureTimer,
            traderName = traderName,
            shuttle = shuttle,
            expiryInfoPart = "RealisticOrbitalTrade.TradeShuttleDepartsIn".Translate(),
            expiryInfoPartTip = "RealisticOrbitalTrade.TradeShuttleDepartsOn".Translate(traderName),
        };
        if (inSignalsDisable != null)
        {
            foreach (var item in inSignalsDisable)
            {
                questPart.inSignalsDisable.Add(item);
            }
        }
        if (!outSignalComplete.NullOrEmpty())
        {
            questPart.outSignalsCompleted.Add(outSignalComplete);
        }
        if (complete != null)
        {
            var shuttleLeaveDelaySignal = QuestGen.GenerateNewSignal("ShuttleLeaveDelay");
            QuestGenUtility.RunInner(complete, shuttleLeaveDelaySignal);
            questPart.outSignalsCompleted.Add(shuttleLeaveDelaySignal);
        }
        quest.AddPart(questPart);
        return questPart;
    }
}
