using RimWorld;
using RimWorld.QuestGen;
using Verse;

using RWShipJobDefOf = RimWorld.ShipJobDefOf;

namespace RealisticOrbitalTrade.Quests;

internal class QuestUtils
{
    /// <summary>
    /// How much longer after the shuttle leaves the orbital trader will leave.
    /// This is just to give the illusion of shuttles taking a moment to get
    /// to orbit. 2,500 ticks is 1 in-game hour.
    /// </summary>
    private const int AdditionalTicksAfterDeparture = 2500;

    public static Thing SetupShuttle(
        string shuttleName,
        Slate slate,
        string questTag,
        out string signalshuttleSentSatisfied,
        out string signalshuttleKilled,
        bool isForAmends = false)
    {
        signalshuttleSentSatisfied = QuestGenUtility.HardcodedSignalWithQuestID($"{shuttleName}.SentSatisfied");
        signalshuttleKilled = QuestGenUtility.HardcodedSignalWithQuestID($"{shuttleName}.Killed");

        Thing shuttle;
        if (RealisticOrbitalTradeGameComponent.Current.Standing == Standing.Good)
        {
            shuttle = ThingMaker.MakeThing(ThingDefOf.ROT_TradeShuttle);
        }
        else if (!isForAmends)
        {
            shuttle = ThingMaker.MakeThing(ThingDefOf.ROT_ExplosiveRiggedTradeShuttle);
        }
        else
        {
            shuttle = ThingMaker.MakeThing(ThingDefOf.ROT_ExplosiveRiggedAmendmentTradeShuttle);
        }
        slate.Set(shuttleName, shuttle);
        QuestUtility.AddQuestTag(ref shuttle.questTags, questTag);

        return shuttle;
    }

    public static int CheckTradeShipRequiresGraceTime(Quest quest, Slate slate, TradeShip tradeShip)
    {
        // Possible scenarios:
        // * TUD is [some_value], TUCC is -1 => No trade agreements made in grace period yet
        // * TUD is [some_value], TUCC is < [some_value] => Grace period has already been extended;
        //                                                  it can be extended again, but the comms
        //                                                  should close just as early.

        int originalTicksUntilCommsClosed = tradeShip.GetData().ticksUntilCommsClosed;
        int originalTicksUntilDeparture = tradeShip.ticksUntilDeparture;
        bool tradeRequiresGraceTime = originalTicksUntilDeparture < Settings.MinTicksUntilDepartureBeforeGraceTime;
        bool tradeShipWasAlreadyInGraceTime = originalTicksUntilCommsClosed != -1;

        if (tradeRequiresGraceTime)
        {
            slate.Set("graceTimeTimeLeft", originalTicksUntilDeparture.ToStringTicksToPeriod());
            slate.Set("graceTimeTimeMinimum", Settings.MinTicksUntilDepartureBeforeGraceTime.ToStringTicksToPeriod());
            slate.Set("graceTimeTimeExtra", Settings.DepartureGraceTimeTicks.ToStringTicksToPeriod());
            quest.Letter(LetterDefOf.NeutralEvent, text: "[graceTimeLetterText]", label: "[graceTimeLetterLabel]");

            if (!tradeShipWasAlreadyInGraceTime)
            {
                tradeShip.GetData().ticksUntilCommsClosed = originalTicksUntilDeparture;
            }
            tradeShip.ticksUntilDeparture = Settings.DepartureGraceTimeTicks + AdditionalTicksAfterDeparture;
        }

        return tradeShip.ticksUntilDeparture - AdditionalTicksAfterDeparture;
    }

    public static void CancelTransportShip(Quest quest, TransportShip transportShip)
    {
        quest.AddShipJob(transportShip, ShipJobDefOf.ROT_CancelLoad, ShipJobStartMode.Instant);
        quest.AddShipJob(transportShip, RWShipJobDefOf.Unload, ShipJobStartMode.Queue);
        quest.AddShipJob(transportShip, RWShipJobDefOf.FlyAway, ShipJobStartMode.Queue);
    }

    public static IntVec3 FindLandingSpot(Map map)
    {
        return DropCellFinder.GetBestShuttleLandingSpot(map, null);
    }
}

internal static class QuestUtilsExtensions
{
    public static void CancelTransportShip(this Quest quest, TransportShip transportShip)
    {
        QuestUtils.CancelTransportShip(quest, transportShip);
    }
}
