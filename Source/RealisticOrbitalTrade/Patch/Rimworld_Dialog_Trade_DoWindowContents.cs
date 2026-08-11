using System.Reflection.Emit;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(Dialog_Trade), nameof(Dialog_Trade.DoWindowContents))]
[HotSwappable]
internal static class Rimworld_Dialog_Trade_DoWindowContents
{
    // Exists only so we can make use of TransferableUIUtility.DrawTransferableInfo
    // which expects a Tradeable
    private class TradeValueTradeable : Tradeable
    {
        public override bool Interactive => false;

        public TradeValueTradeable(int count)
        {
            CountToTransfer = count;
        }

        public override TransferablePositiveCountDirection PositiveCountDirection =>
            TransferablePositiveCountDirection.Destination;

        public override bool IsThing => false;
        public override bool HasAnyThing => false;
        public override Thing? AnyThing => null;
        public override ThingDef? ThingDef => null;
        public override string Label => "RealisticOrbitalTrade.TradeValue".Translate();
        public override string TipDescription => "RealisticOrbitalTrade.TradeValueTip".Translate();

#if v1_6_OR_GREATER
        public override int GetHashCode() => Label.GetHashCode(StringComparison.Ordinal);
#else
        public override int GetHashCode() => Label.GetHashCode();
#endif
    }

    internal static float lastTradeValueFlashTime = -100f;
    internal static float lastMassFlashTime = -100f;
    internal static float lastIncomingMassFlashTime = -100f;

    private static readonly MethodInfo _methodDrawThresholdRow = AccessTools.Method(
        typeof(Rimworld_Dialog_Trade_DoWindowContents),
        nameof(DrawThresholdRow)
    );

    private static readonly FieldInfo _field_Dialog_Trade_cachedCurrencyTradeable =
        AccessTools.Field(typeof(Dialog_Trade), nameof(Dialog_Trade.cachedCurrencyTradeable));

    // Draws the threshold row (if enabled) and the cargo mass rows (always for the outgoing/sold
    // side, since it's a hard shuttle capacity limit rather than a balance preference; for the
    // incoming/bought side only when items actually arrive via shuttle rather than drop pods)
    // stacked on top of each other, and returns the combined height so the transpiler-inserted
    // call site can shift the rest of the window down by however many of these rows actually got
    // drawn.
    private static float DrawThresholdRow(float rowWidth)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            return 0f;
        }

        var yOffset = 0f;

        if (Settings._useMinimumTradeThreshold)
        {
            DrawTradeValueThresholdRow(rowWidth, yOffset, tradeAgreement);
            yOffset += 30f;
        }

        DrawCargoMassRow(rowWidth, yOffset, outgoing: true);
        yOffset += 30f;

        if (!tradeAgreement.deliverItemsViaDropPod)
        {
            DrawCargoMassRow(rowWidth, yOffset, outgoing: false);
            yOffset += 30f;
        }

        if (Utils.RenegotiatingBecauseDeparting)
        {
            DrawSilverCeilingRow(rowWidth, yOffset, tradeAgreement);
            yOffset += 30f;
        }

        return yOffset;
    }

    private static void DrawTradeValueThresholdRow(
        float rowWidth,
        float yOffset,
        TradeAgreement tradeAgreement
    )
    {
        var rowRect = new Rect(0f, 58f + yOffset, rowWidth, 30f);

        Text.Font = GameFont.Small;
        Widgets.BeginGroup(rowRect);

        Rect tradersMinimumTradeThresholdRect = new(rowWidth - 75f, 0f, 75f, rowRect.height);
        if (Mouse.IsOver(tradersMinimumTradeThresholdRect))
        {
            Widgets.DrawHighlight(tradersMinimumTradeThresholdRect);
        }
        Text.Anchor = TextAnchor.MiddleRight;
        var tradersMinimumTradeThresholdLabelRect = tradersMinimumTradeThresholdRect;
        tradersMinimumTradeThresholdLabelRect.xMin += 5f;
        tradersMinimumTradeThresholdLabelRect.xMax -= 5f;
        var minimumTradeThreshold = tradeAgreement.tradeShip.GetData().minimumTradeThreshold;
        Widgets.Label(
            tradersMinimumTradeThresholdLabelRect,
            minimumTradeThreshold.ToString(CultureInfo.CurrentCulture)
        );
        TooltipHandler.TipRegionByKey(
            tradersMinimumTradeThresholdRect,
            "RealisticOrbitalTrade.TradersMinimumTradeThreshold"
        );
        rowWidth -= 175f;
        var combinedTradeValue = CalculateCombinedTradeValue();

        var (tradeValueDifference, valueColor, flash) = GetTradeValueThresholdRowDisplay(
            combinedTradeValue,
            minimumTradeThreshold
        );
        var trad = new TradeValueTradeable(tradeValueDifference);

        Rect rect5 = new(rowWidth - 240f, 0f, 240f, rowRect.height);
        var rect9 = rect5.Rounded();
        var tradeValueDifferenceLabelRect = new Rect(
            rect9.center.x - 45f,
            rect9.center.y - 12.5f,
            90f,
            25f
        ).Rounded();

        GUI.color = valueColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        if (Mouse.IsOver(tradeValueDifferenceLabelRect))
        {
            Widgets.DrawHighlight(tradeValueDifferenceLabelRect);
        }
        Widgets.Label(tradeValueDifferenceLabelRect, trad.CountToTransfer.ToStringCached());
        TooltipHandler.TipRegionByKey(
            tradeValueDifferenceLabelRect,
            "RealisticOrbitalTrade.TradeValueDifference"
        );
        GUI.color = Color.white;

        if (flash)
        {
            GUI.DrawTexture(tradeValueDifferenceLabelRect, TransferableUIUtility.FlashTex);
        }

        rowWidth -= 240f;

        Rect rect6 = new(rowWidth - 100f, 0f, 100f, rowRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Rect currentTradeValueRect = new(rect6.x - 75f, 0f, 75f, rowRect.height);
        if (Mouse.IsOver(currentTradeValueRect))
        {
            Widgets.DrawHighlight(currentTradeValueRect);
        }
        Text.Anchor = TextAnchor.MiddleLeft;
        var rect8 = currentTradeValueRect;
        rect8.xMin += 5f;
        rect8.xMax -= 5f;
        Widgets.Label(rect8, ((int)Math.Round(combinedTradeValue)).ToStringCached());
        TooltipHandler.TipRegionByKey(
            currentTradeValueRect,
            "RealisticOrbitalTrade.CurrentTradeValue"
        );
        rowWidth -= 175f;

        Rect idRect = new(0f, 0f, rowWidth, rowRect.height);
        TransferableUIUtility.DrawTransferableInfo(trad, idRect, Color.white);

        Widgets.EndGroup();
    }

    // The per-row content decisions (label/tip key, which flash-time field applies, over-capacity
    // color) that both this dialog's DrawCargoMassRow and the DynamicTradeInterface patch's mirror
    // need identically - only the Rect layout differs between the two UIs.
    internal static (
        bool overCapacity,
        bool flash,
        string labelKey,
        string tipKey
    ) GetCargoMassRowDisplay(bool outgoing, float mass, float massCapacity)
    {
        var flashTime = outgoing ? lastMassFlashTime : lastIncomingMassFlashTime;
        return (
            mass > massCapacity,
            Time.time - flashTime < 1f,
            outgoing
                ? "RealisticOrbitalTrade.CargoMassLabel"
                : "RealisticOrbitalTrade.IncomingCargoMassLabel",
            outgoing
                ? "RealisticOrbitalTrade.CargoMassTip"
                : "RealisticOrbitalTrade.IncomingCargoMassTip"
        );
    }

    // Mirrors the flash/color decision both DrawTradeValueThresholdRow and its DynamicTradeInterface
    // counterpart make from the same trade-value-vs-threshold difference.
    internal static (
        int tradeValueDifference,
        Color valueColor,
        bool flash
    ) GetTradeValueThresholdRowDisplay(float combinedTradeValue, int minimumTradeThreshold)
    {
        var tradeValueDifference = (int)Math.Round(combinedTradeValue) - minimumTradeThreshold;
        return (
            tradeValueDifference,
            tradeValueDifference < 0 ? Color.yellow : Color.green,
            Time.time - lastTradeValueFlashTime < 1f
        );
    }

    // Shared by DrawSilverCeilingRow and its DynamicTradeInterface counterpart - see that method
    // for why the ceiling can't be read off the currency tradeable itself.
    internal static int GetMaxPayableSilver(TradeAgreement tradeAgreement) =>
        TradeAgreement.MaxPayableSilver(
            Utils.RenegotiationOriginalTradeables ?? tradeAgreement.AllTradeables
        );

    // Mirrors CompTransporter.MassUsage's own calculation (same IgnorePawnsInventoryMode and
    // includePawnsMass arguments) so the number shown here before Accept matches what the shuttle
    // will actually compute once loading starts. Handles both the outgoing (sold-to-trader) and
    // incoming (bought-from-trader) shuttle, which are separate shuttles each capped at
    // ShuttleMassCapacity.
    private static void DrawCargoMassRow(float rowWidth, float yOffset, bool outgoing)
    {
        var rowRect = new Rect(0f, 58f + yOffset, rowWidth, 30f);

        Text.Font = GameFont.Small;
        Widgets.BeginGroup(rowRect);

        var massCapacity = ShuttleMassCapacity;
        var mass = outgoing
            ? CalculateOutgoingMass(TradeSession.deal)
            : CalculateIncomingMass(TradeSession.deal);
        var (overCapacity, flash, labelKey, tipKey) = GetCargoMassRowDisplay(
            outgoing,
            mass,
            massCapacity
        );

        Rect massValueRect = new(rowWidth - 175f, 0f, 175f, rowRect.height);
        if (Mouse.IsOver(massValueRect))
        {
            Widgets.DrawHighlight(massValueRect);
        }
        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = overCapacity ? Color.red : Color.white;
        var massValueLabelRect = massValueRect;
        massValueLabelRect.xMin += 5f;
        massValueLabelRect.xMax -= 5f;
        Widgets.Label(
            massValueLabelRect,
            mass.ToStringEnsureThreshold(massCapacity, 1)
                + " / "
                + massCapacity.ToString("F0", CultureInfo.CurrentCulture)
                + " "
                + "kg".Translate()
        );
        GUI.color = Color.white;
        TooltipHandler.TipRegionByKey(
            massValueRect,
            tipKey,
            mass.ToStringEnsureThreshold(massCapacity, 2),
            massCapacity.ToString("F2", CultureInfo.CurrentCulture)
        );

        if (flash)
        {
            GUI.DrawTexture(massValueRect, TransferableUIUtility.FlashTex);
        }

        // x=80 matches where TransferableUIUtility.DrawTransferableInfo starts a row's own label
        // text (after its 27px icon plus margin), so this lines up with the "Trade value" row
        // above it, which is drawn through that same method.
        Rect labelRect = new(80f, 0f, rowWidth - 175f - 80f, rowRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().CapitalizeFirst());

        Widgets.EndGroup();
    }

    // Departure renegotiations lock every already-loaded good, including silver, to what's
    // physically on the shuttle and forbid increasing it further (see
    // Rimworld_TradeDeal_TryExecute's ToTrader-increase block) - but unlike every other good,
    // silver's own row can't show that ceiling: vanilla's TradeDeal.UpdateCurrencyCount
    // recalculates its CountToTransfer every frame to auto-balance the deal, overwriting whatever
    // ClampSoldTradeablesToLoaded set it to the moment the dialog opened. This row surfaces the
    // ceiling directly instead, using the same original-tradeables snapshot the Accept-time check
    // itself compares against.
    private static void DrawSilverCeilingRow(
        float rowWidth,
        float yOffset,
        TradeAgreement tradeAgreement
    )
    {
        var rowRect = new Rect(0f, 58f + yOffset, rowWidth, 30f);

        Text.Font = GameFont.Small;
        Widgets.BeginGroup(rowRect);

        var maxPayableSilver = GetMaxPayableSilver(tradeAgreement);

        Rect valueRect = new(rowWidth - 175f, 0f, 175f, rowRect.height);
        if (Mouse.IsOver(valueRect))
        {
            Widgets.DrawHighlight(valueRect);
        }
        Text.Anchor = TextAnchor.MiddleRight;
        var valueLabelRect = valueRect;
        valueLabelRect.xMin += 5f;
        valueLabelRect.xMax -= 5f;
        Widgets.Label(valueLabelRect, maxPayableSilver.ToStringCached());
        TooltipHandler.TipRegionByKey(
            valueRect,
            "RealisticOrbitalTrade.MaxPayableSilverTip",
            maxPayableSilver.ToStringCached()
        );

        Rect labelRect = new(80f, 0f, rowWidth - 175f - 80f, rowRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(
            labelRect,
            "RealisticOrbitalTrade.MaxPayableSilverLabel".Translate().CapitalizeFirst()
        );

        Widgets.EndGroup();
    }

    // Same primitive CompTransporter.MassUsage/Dialog_LoadTransporters ultimately funnel through
    // (RimWorld.CollectionsMassCalculator.MassUsage), applied to only the items/pawns leaving the
    // colony for the trader (negative CountToTransfer) - the outgoing shuttle's would-be cargo.
    // Walks the tradeable's actual source stacks (thingsColony) rather than just AnyThing, since
    // a single tradeable can be backed by several stacks (the large multi-stack scenario this
    // exists for) and ThingCount would otherwise clamp/warn against any one stack's own count.
    //
    // overrideA/overrideB let a caller substitute a hypothetical CountToTransfer for up to two
    // specific tradeables (used by GetMassBounds to ask "what would outgoing mass be if this row
    // and the currency tradeable had these counts instead") without mutating the real deal.
    internal static float CalculateOutgoingMass(
        TradeDeal deal,
        Tradeable? overrideA = null,
        int overrideACount = 0,
        Tradeable? overrideB = null,
        int overrideBCount = 0
    )
    {
        List<ThingCount> thingCounts = [];
        foreach (var item in deal.AllTradeables)
        {
            var countToTransfer =
                item == overrideA ? overrideACount
                : item == overrideB ? overrideBCount
                : item.CountToTransfer;
            if (countToTransfer >= 0 || !item.HasAnyThing)
            {
                continue;
            }

            var remaining = -countToTransfer;
            foreach (var thing in item.thingsColony)
            {
                if (remaining <= 0)
                {
                    break;
                }
                var take = Mathf.Min(remaining, thing.stackCount);
                thingCounts.Add(new ThingCount(thing, take));
                remaining -= take;
            }
        }
        return CollectionsMassCalculator.MassUsage(
            thingCounts,
            IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
            includePawnsMass: true
        );
    }

    // Mirror of CalculateOutgoingMass for the other direction: items/pawns arriving from the
    // trader (positive CountToTransfer), backed by the trader's own stacks (thingsTrader) rather
    // than the colony's. See CalculateOutgoingMass for what overrideA/overrideB do.
    internal static float CalculateIncomingMass(
        TradeDeal deal,
        Tradeable? overrideA = null,
        int overrideACount = 0,
        Tradeable? overrideB = null,
        int overrideBCount = 0
    )
    {
        List<ThingCount> thingCounts = [];
        foreach (var item in deal.AllTradeables)
        {
            var countToTransfer =
                item == overrideA ? overrideACount
                : item == overrideB ? overrideBCount
                : item.CountToTransfer;
            if (countToTransfer <= 0 || !item.HasAnyThing)
            {
                continue;
            }

            var remaining = countToTransfer;
            foreach (var thing in item.thingsTrader)
            {
                if (remaining <= 0)
                {
                    break;
                }
                var take = Mathf.Min(remaining, thing.stackCount);
                thingCounts.Add(new ThingCount(thing, take));
                remaining -= take;
            }
        }
        return CollectionsMassCalculator.MassUsage(
            thingCounts,
            IgnorePawnsInventoryMode.IgnoreIfAssignedToUnload,
            includePawnsMass: true
        );
    }

    internal static float ShuttleMassCapacity =>
        TransportShipDefOf
            .Ship_Shuttle.shipThing.GetCompProperties<CompProperties_Transporter>()
            .massCapacity;

    internal static float CalculateCombinedTradeValue()
    {
        var combinedTradeValue = 0f;
        foreach (var item in TradeSession.deal.AllTradeables.Where(t => t.CountToTransfer != 0))
        {
            if (item.IsCurrency)
            {
                continue;
            }
            combinedTradeValue += Math.Abs(item.CurTotalCurrencyCostForDestination);
        }

        return combinedTradeValue;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        // Look for
        //   if (this.cachedCurrencyTradeable != null)
        //   {
        //       float width = inRect.width - 16f;
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldfld
            && i.operand is FieldInfo f
            && f == _field_Dialog_Trade_cachedCurrencyTradeable
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Dialog_Trade.DoWindowContents, IL does not match expectations ([ldfld Dialog_Trade.cachedCurrencyTradeable])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder l && l.LocalIndex == 8
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Dialog_Trade.DoWindowContents, IL does not match expectations ([stloc.s 8])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);

        var shiftAmount = generator.DeclareLocal(typeof(float));

        // Insert
        //   float shiftAmount = DrawThresholdRow(width);
        // directly after
        _ = codeMatcher.Insert([
            new(OpCodes.Ldloc_S, 8),
            new(OpCodes.Call, _methodDrawThresholdRow),
            new(OpCodes.Stloc_S, shiftAmount.LocalIndex),
        ]);

        // Look for `58f` in the context of
        //   TradeUI.DrawTradeableRow(new Rect(0f, 58f, width, 30f), this.cachedCurrencyTradeable, 1);
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldc_R4 && i.operand is float f && Math.Abs(f - 58f) < 0.1f
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Dialog_Trade.DoWindowContents, IL does not match expectations ([ldc.r4 58])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);

        // Change it to
        //   TradeUI.DrawTradeableRow(new Rect(0f, 58f + shiftAmount, width, 30f), this.cachedCurrencyTradeable, 1);
        _ = codeMatcher.Insert([new(OpCodes.Ldloc_S, shiftAmount.LocalIndex), new(OpCodes.Add)]);

        // Look for `87f` in the context of
        //   Widgets.DrawLineHorizontal(0f, 87f, width);
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldc_R4 && i.operand is float f && Math.Abs(f - 87f) < 0.1f
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Dialog_Trade.DoWindowContents, IL does not match expectations ([ldc.r4 87])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);

        // Change it to
        //   Widgets.DrawLineHorizontal(0f, 87f + shiftAmount, width);
        _ = codeMatcher.Insert([new(OpCodes.Ldloc_S, shiftAmount.LocalIndex), new(OpCodes.Add)]);

        // Look for `30f` in the context of
        //   currencyRowYShift = 30f;
        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Stloc_S && i.operand is LocalBuilder l && l.LocalIndex == 4
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Dialog_Trade.DoWindowContents, IL does not match expectations ([stloc.s 4])"
            );
            return originalInstructionList;
        }

        // Change it to
        //   currencyRowYShift = 30f + shiftAmount;
        _ = codeMatcher.Insert([new(OpCodes.Ldloc_S, shiftAmount.LocalIndex), new(OpCodes.Add)]);

        return codeMatcher.Instructions();
    }
}
