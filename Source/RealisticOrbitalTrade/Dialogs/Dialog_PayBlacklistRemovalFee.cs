using RimWorld.QuestGen;
using RwThingDefOf = RimWorld.ThingDefOf;

namespace RealisticOrbitalTrade.Dialogs;

internal class Dialog_PayBlacklistRemovalFee : Dialog_NodeTree
{
    private enum PayWith
    {
        Parts,
        Silver,
    }

    private const float LaborCost = 350f;

    private readonly TradeShip _tradeShip;
    private readonly Pawn _negotiator;
    private readonly List<ThingDefCountClass> _thingDefCounts;
    private readonly Dictionary<ThingDef, PayWith> _payForPartsWith = [];

    private ThingDefCountClass? CurrentThing =>
        _thingDefCounts.FirstOrDefault(t => !_payForPartsWith.ContainsKey(t.thingDef));
    private IEnumerable<(ThingDefCountClass thingDefCount, PayWith payWith)> ThingDefPayments =>
        _thingDefCounts
            .Select(t =>
            {
                var contains = _payForPartsWith.TryGetValue(t.thingDef, out var payWith);
                return (t, contains, payWith);
            })
            .Where(c => c.contains)
            .Select(c => (c.t, c.payWith));

    public Dialog_PayBlacklistRemovalFee(TradeShip tradeShip, Pawn negotiator)
        : base(new(""))
    {
        _tradeShip = tradeShip;
        _negotiator = negotiator;

        _thingDefCounts =
        [
            .. ThingDefOf.ROT_TradeShuttle.killedLeavings.Select(i =>
            {
                return i.thingDef == RwThingDefOf.ChunkSlagSteel
                    ? new ThingDefCountClass(RwThingDefOf.Steel, i.count * 15)
                    : i;
            }),
        ];

        SetupNextText();
        SetupNextOptions();
    }

    private float CalculateCostOf(ThingDefCountClass thingDefCount)
    {
        var marketValue = thingDefCount.thingDef.BaseMarketValue;
        var totalCountMarketValue = marketValue * thingDefCount.count;
        var priceType = _tradeShip.TraderKind.PriceTypeFor(
            thingDefCount.thingDef,
            TradeAction.PlayerBuys
        );
        var priceGainNegotiator = _negotiator.GetStatValue(StatDefOf.TradePriceImprovement);

        var partsCost =
            totalCountMarketValue
            * 1.4f
            * priceType.PriceMultiplier()
            * (1f + Find.Storyteller.difficulty.tradePriceFactorLoss);
        partsCost *= 1f - priceGainNegotiator;
        partsCost = Mathf.Max(partsCost, 0.5f);
        if (partsCost > 99.5f)
        {
            partsCost = Mathf.Round(partsCost);
        }

        return partsCost;
    }

    private List<ThingDefCount> GetThingsToReturn()
    {
        var nominalFee = WealthUtility.PlayerWealth * 0.01f;
        var totalSilver = LaborCost + nominalFee;

        List<(ThingDef thingDef, int count)> partsToReturn = [];
        foreach (var (thingDefCount, payWith) in ThingDefPayments)
        {
            if (payWith == PayWith.Parts)
            {
                partsToReturn.Add((thingDefCount.thingDef, thingDefCount.count));
            }
            else
            {
                totalSilver += CalculateCostOf(thingDefCount);
            }
        }

        var thingsToReturn = AccumulateThingCounts(
            partsToReturn,
            RwThingDefOf.Silver,
            (int)totalSilver
        );

        return [.. thingsToReturn.Select(t => new ThingDefCount(t.Key, t.Value))];
    }

    // Additively upserts so a ThingDef appearing more than once (e.g. two
    // killedLeavings entries resolving to the same def, or a parts payment that
    // happens to be silver) accumulates instead of throwing on the duplicate key.
    internal static Dictionary<ThingDef, int> AccumulateThingCounts(
        IEnumerable<(ThingDef thingDef, int count)> counts,
        ThingDef silverDef,
        int totalSilverCount
    )
    {
        Dictionary<ThingDef, int> result = [];
        foreach (var (thingDef, count) in counts)
        {
            result[thingDef] = result.GetValueOrDefault(thingDef) + count;
        }
        result[silverDef] = result.GetValueOrDefault(silverDef) + totalSilverCount;

        return result;
    }

    private void SetupNextText()
    {
        var nominalFee = WealthUtility.PlayerWealth * 0.01f;

        var fixedCostsText =
            "RealisticOrbitalTrade.LaborCost".Translate(LaborCost.ToStringMoney())
            + "\n"
            + "RealisticOrbitalTrade.NominalFeeCost".Translate(nominalFee.ToStringMoney());

        curNode.text = new(
            "RealisticOrbitalTrade.PayBlacklistRemovalFeeDialogText".Translate(fixedCostsText)
        );

        var payments = ThingDefPayments.ToList();
        if (payments.Count > 0)
        {
            curNode.text += "\n\n" + "RealisticOrbitalTrade.CostOfParts".Translate();
            foreach (var (thingDefCount, payWith) in payments)
            {
                curNode.text +=
                    "\n - "
                    + "RealisticOrbitalTrade.PayForWith".Translate(
                        thingDefCount.LabelCap,
                        payWith == PayWith.Parts
                            ? "RealisticOrbitalTrade.PayWith.Parts".Translate()
                            : "RealisticOrbitalTrade.PayWith.Silver".Translate(
                                CalculateCostOf(thingDefCount).ToStringMoney()
                            )
                    );
            }
        }

        var currentThing = CurrentThing;
        if (currentThing != null)
        {
            curNode.text +=
                "\n\n"
                + "RealisticOrbitalTrade.HowPayForParts".Translate(currentThing.thingDef.label);
        }
        else
        {
            curNode.text += "\n\n" + "RealisticOrbitalTrade.AreYouReadyToPay".Translate();
        }
    }

    private void SetupNextOptions()
    {
        curNode.options.Clear();
        var currentThing = CurrentThing;
        if (currentThing == null)
        {
            curNode.options.Add(
                new("RealisticOrbitalTrade.IAmReadyToPay".Translate())
                {
                    action = () =>
                    {
                        Slate slate = new();
                        slate.Set("thingsToReturn", GetThingsToReturn());
                        slate.Set("tradeShip", _tradeShip);
                        slate.Set(
                            "tradePausesDepartureTimer",
                            Settings._activeTradePausesDepartureTimer
                        );

                        _ = QuestUtility.GenerateQuestAndMakeAvailable(
                            QuestScriptDefOf.ROT_TradeShipMakeAmends,
                            slate
                        );
                    },
                    resolveTree = true,
                    dialog = this,
                }
            );
            curNode.options.Add(
                new("RealisticOrbitalTrade.ResetPaymentChoices".Translate())
                {
                    action = () =>
                    {
                        _payForPartsWith.Clear();
                        SetupNextText();
                        SetupNextOptions();
                    },
                }
            );
        }
        else
        {
            curNode.options.Add(
                new("RealisticOrbitalTrade.PayForPartsWithParts".Translate(currentThing.Label))
                {
                    action = () =>
                    {
                        _payForPartsWith.Add(currentThing.thingDef, PayWith.Parts);
                        SetupNextText();
                        SetupNextOptions();
                    },
                }
            );
            curNode.options.Add(
                new(
                    "RealisticOrbitalTrade.PayForPartsWithSilver".Translate(
                        currentThing.Label,
                        CalculateCostOf(currentThing).ToStringMoney()
                    )
                )
                {
                    action = () =>
                    {
                        _payForPartsWith.Add(currentThing.thingDef, PayWith.Silver);
                        SetupNextText();
                        SetupNextOptions();
                    },
                }
            );
        }

        curNode.options.Add(
            new("(" + "Disconnect".Translate() + ")") { resolveTree = true, dialog = this }
        );
    }

    public override void DoWindowContents(Rect inRect)
    {
        Widgets.BeginGroup(inRect);
        Rect rect = new(0f, 0f, inRect.width / 2f, 70f);
        Rect rect3 = new(inRect.width / 2f, 0f, inRect.width / 2f, 70f);
        Text.Font = GameFont.Medium;
        Widgets.Label(rect, _negotiator.LabelCap);
        Text.Anchor = TextAnchor.UpperRight;
        Widgets.Label(rect3, _tradeShip.GetCallLabel());
        Text.Anchor = TextAnchor.UpperLeft;

        Rect rect5 = new(0f, 80f, inRect.width, inRect.height - 80f);
        DrawNode(rect5);
    }
}
