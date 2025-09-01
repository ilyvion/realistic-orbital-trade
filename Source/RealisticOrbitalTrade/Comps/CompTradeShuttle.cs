using System.Text;
using RimWorld.Planet;
using Verse.AI;

namespace RealisticOrbitalTrade.Comps;

[HotSwappable]
public class CompTradeShuttle : ThingComp
{
    internal bool cancelled;
    internal bool isToTrader;
    internal TradeAgreement? tradeAgreement;
    internal List<ThingDefCountWithRequirements> requiredSpecificItems = [];

    private CompShuttle? _cachedCompShuttle;
    private CompShuttle Shuttle
    {
        get
        {
            _cachedCompShuttle ??= parent.GetComp<CompShuttle>();
            return _cachedCompShuttle;
        }
    }
    internal bool ShuttleAutoLoad
    {
        get => Traverse.Create(Shuttle).Field<bool>("autoload").Value;
        set => Traverse.Create(Shuttle).Field<bool>("autoload").Value = value;
    }

    private CompTransporter? _cachedCompTransporter;
    internal Action? cancelTradeAction;

    private CompTransporter Transporter
    {
        get
        {
            _cachedCompTransporter ??= parent.GetComp<CompTransporter>();
            return _cachedCompTransporter;
        }
    }

    public override void CompTick()
    {
        base.CompTick();
        if (isToTrader && parent.IsHashIntervalTick(120))
        {
            if (!parent.Spawned || cancelled)
            {
                return;
            }

            ShuttleAutoLoad = true;
            if (!Transporter.LoadingInProgressOrReadyToLaunch)
            {
                TransporterUtility.InitiateLoading(Gen.YieldSingle(Transporter));
            }
        }
    }

    private static readonly List<string> tmpRequiredLabels = [];

    public override string? CompInspectStringExtra()
    {
        if (cancelled)
        {
            return null;
        }

        StringBuilder stringBuilder = new();

        CalculateRemainingRequiredItems();

        tmpRequiredLabels.Clear();
        foreach (var item in tmpRequiredSpecificItems.Where(i => i.count > 0))
        {
            tmpRequiredLabels.Add(item.Label);
        }
        if (tmpRequiredLabels.Any())
        {
            stringBuilder.AppendInNewLine(
                "RealisticOrbitalTrade.RequiredThings".Translate()
                    + ": "
                    + tmpRequiredLabels.ToCommaList().CapitalizeFirst()
            );
        }
        return stringBuilder.ToString();
    }

    private void CalculateRemainingRequiredItems()
    {
        tmpRequiredSpecificItems.Clear();
        tmpRequiredSpecificItems.AddRange(requiredSpecificItems);

        ThingOwner innerContainer = Transporter.innerContainer;
        foreach (var storedItem in innerContainer)
        {
            if (storedItem is Pawn)
            {
                continue;
            }
            int stackCount = storedItem.stackCount;
            for (int i = 0; i < tmpRequiredSpecificItems.Count; i++)
            {
                ThingDefCountWithRequirements requiredSpecificItem = tmpRequiredSpecificItems[i];
                if (requiredSpecificItem.Matches(storedItem))
                {
                    int additionalItemsNeeded = Mathf.Min(requiredSpecificItem.count, stackCount);
                    if (additionalItemsNeeded > 0)
                    {
                        tmpRequiredSpecificItems[i] = tmpRequiredSpecificItems[i]
                            .WithCount(tmpRequiredSpecificItems[i].count - additionalItemsNeeded);
                        stackCount -= additionalItemsNeeded;
                    }
                }
            }
        }
    }

    public override void PostExposeData()
    {
        base.PostExposeData();
        Scribe_Values.Look(ref isToTrader, "isToTrader");
        Scribe_References.Look(ref tradeAgreement, "tradeAgreement");
        Scribe_Collections.Look(ref requiredSpecificItems, "requiredSpecificItems", LookMode.Deep);
    }

    internal bool IsRequired(Thing thing)
    {
        if (!isToTrader)
        {
            return false;
        }
        foreach (var item in requiredSpecificItems)
        {
            if (item.Matches(thing))
            {
                return true;
            }
        }
        return false;
    }

    private static readonly List<ThingDefCountWithRequirements> tmpRequiredSpecificItems = [];
    private static readonly List<Thing> tmpAllSendableItems = [];

    internal void CheckAutoload()
    {
        if (!isToTrader)
        {
            return;
        }

        if (tradeAgreement == null)
        {
            RealisticOrbitalTradeMod.Error(
                "tradeAgreement is null in CompTradeShuttle. This is a bug, autoloading impossible."
            );
            return;
        }

        if (
            !ShuttleAutoLoad
            || !Transporter.LoadingInProgressOrReadyToLaunch
            || !parent.Spawned
            || cancelled
        )
        {
            return;
        }

        CalculateRemainingRequiredItems();

        tmpAllSendableItems.Clear();
        tmpAllSendableItems.AddRange(tradeAgreement.ColonyThingsTraderWillingToBuy());
        tmpAllSendableItems.AddRange(
            TransporterUtility.ThingsBeingHauledTo(Shuttle.TransportersInGroup, parent.Map)
        );

        foreach (var requiredItem in tmpRequiredSpecificItems)
        {
            if (requiredItem.count <= 0)
            {
                continue;
            }

            int numberOfMatches = 0;
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    numberOfMatches += sendableItem.stackCount;
                }
            }
            if (numberOfMatches <= 0)
            {
                continue;
            }
            TransferableOneWay transferableOneWay = new();
            foreach (var sendableItem in tmpAllSendableItems)
            {
                if (requiredItem.Matches(sendableItem))
                {
                    transferableOneWay.things.Add(sendableItem);
                }
            }
            int count = Mathf.Min(requiredItem.count, numberOfMatches);
            Transporter.AddToTheToLoadList(transferableOneWay, count);
        }
    }

    public bool AllRequiredThingsLoaded
    {
        get
        {
            CalculateRemainingRequiredItems();
            return tmpRequiredSpecificItems.Sum(t => t.count) == 0;
        }
    }

    public override IEnumerable<FloatMenuOption> CompFloatMenuOptions(Pawn selPawn)
    {
        if (selPawn == null)
        {
            throw new ArgumentNullException(nameof(selPawn));
        }

        if (cancelled)
        {
            yield break;
        }

        var failureReason = GetFailureReason(selPawn);
        if (failureReason != null)
        {
            yield return failureReason;
            yield break;
        }

        yield return FloatMenuUtility.DecoratePrioritizedTask(
            new FloatMenuOption(
                "RealisticOrbitalTrade.RenegotiateTrade".Translate(),
                () =>
                {
                    Job job = JobMaker.MakeJob(JobDefOf.ROT_RenegotiateTrade, parent);
                    selPawn.jobs.TryTakeOrderedJob(job, JobTag.Misc);
                },
                MenuOptionPriority.InitiateSocial
            ),
            selPawn,
            parent
        );

        FloatMenuOption? GetFailureReason(Pawn selPawn)
        {
            if (tradeAgreement == null)
            {
                RealisticOrbitalTradeMod.Error(
                    "tradeAgreement is null in CompTradeShuttle. This is a bug, renegotiation impossible."
                );
                return new FloatMenuOption(
                    "tradeAgreement is null in CompTradeShuttle. This is a bug, renegotiation impossible.",
                    null
                );
            }
            if (
                tradeAgreement.toPlayerTransportShip == null
                || tradeAgreement.toTraderTransportShip == null
            )
            {
                return new FloatMenuOption(
                    "RealisticOrbitalTrade.RenegotiateTrade".Translate()
                        + " "
                        + "RealisticOrbitalTrade.OldTradeNonRenegotiable".Translate(),
                    null
                );
            }
            if (!selPawn.CanReach(parent, PathEndMode.Touch, Danger.Some))
            {
                return new FloatMenuOption("CannotUseNoPath".Translate(), null);
            }
            if (!selPawn.health.capacities.CapableOf(PawnCapacityDefOf.Talking))
            {
                return new FloatMenuOption(
                    "CannotUseReason".Translate(
                        "IncapableOfCapacity".Translate(
                            PawnCapacityDefOf.Talking.label,
                            selPawn.Named("PAWN")
                        )
                    ),
                    null
                );
            }
            return null;
        }
    }
}

internal struct ThingDefCountWithRequirements : IExposable
{
    public ThingDef def;
    internal ThingDef stuffDef;
    public bool isInnerThing;
    internal int count;
    public bool healthAffectsPrice;
    public int hitPoints;
    public int maxHitPoints;
    public bool hasQuality;
    public QualityCategory quality;

    public string Label
    {
        get { return GenLabel.ThingLabel(def, stuffDef, count) + LabelExtras(); }
    }

    private string LabelExtras()
    {
        string text = string.Empty;
        bool reducedHealth = healthAffectsPrice && hitPoints < maxHitPoints;
        if (reducedHealth || hasQuality)
        {
            text += " (";
            if (hasQuality)
            {
                text += quality.GetLabel();
            }
            if (reducedHealth)
            {
                if (hasQuality)
                {
                    text += " ";
                }
                text += ((float)hitPoints / maxHitPoints).ToStringPercent();
            }
            text += ")";
        }
        return text;
    }

    public void ExposeData()
    {
        Scribe_Defs.Look(ref def, "def");
        Scribe_Defs.Look(ref stuffDef, "stuffDef");
        Scribe_Values.Look(ref isInnerThing, "isInnerThing");
        Scribe_Values.Look(ref count, "count");
        Scribe_Values.Look(ref healthAffectsPrice, "healthAffectsPrice");
        Scribe_Values.Look(ref hitPoints, "hitPoints");
        Scribe_Values.Look(ref maxHitPoints, "maxHitPoints");
        Scribe_Values.Look(ref hasQuality, "hasQuality");
        Scribe_Values.Look(ref quality, "quality");
    }

    internal bool Matches(Thing thing)
    {
        if (isInnerThing)
        {
            if (thing is MinifiedThing minifiedThing)
            {
                thing = minifiedThing.InnerThing;
            }
            else
            {
                return false;
            }
        }
        QualityUtility.TryGetQuality(thing, out var thingQuality);
        return def == thing.def
            && stuffDef == thing.Stuff
            && count != 0
            && (!healthAffectsPrice || Math.Abs(thing.HitPoints - hitPoints) <= 5)
            && (!hasQuality || thingQuality == quality);
    }

    internal ThingDefCountWithRequirements WithCount(int count)
    {
        return new()
        {
            def = def,
            count = count,
            healthAffectsPrice = healthAffectsPrice,
            hitPoints = hitPoints,
            hasQuality = hasQuality,
            quality = quality,
        };
    }
}
