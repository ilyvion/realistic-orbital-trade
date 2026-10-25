using System.Reflection.Emit;

namespace RealisticOrbitalTrade.Patch;

// Mirrors Dialog_FormCaravan's per-row mass column and "M<"/">M" add-until-mass-limit buttons
// (TransferableOneWayWidget.DoRow/DrawMass), adapted for the trade dialog: selling draws down the
// outgoing shuttles' capacity, buying is only held to a single thing fitting on a shuttle (skipped,
// i.e. unconstrained, when bought items are set to arrive by drop pod instead of shuttle).
[HarmonyPatch(typeof(TradeUI), nameof(TradeUI.DrawTradeableRow))]
[HotSwappable]
internal static class Rimworld_TradeUI_DrawTradeableRow
{
    private const float MassColumnWidth = 75f;

    private static readonly MethodInfo _methodDrawMassColumnAndShrinkWidth = AccessTools.Method(
        typeof(Rimworld_TradeUI_DrawTradeableRow),
        nameof(DrawMassColumnAndShrinkWidth)
    );

    private static readonly MethodInfo _methodGetMassStoppingPoints = AccessTools.Method(
        typeof(Rimworld_TradeUI_DrawTradeableRow),
        nameof(GetMassStoppingPoints)
    );

    private static float DrawMassColumnAndShrinkWidth(Rect rowRect, Tradeable trad, float width)
    {
        if (TradeShipData.tradeAgreementForQuest == null || !trad.HasAnyThing)
        {
            return width;
        }

        var unitMass = trad.AnyThing.GetStatValue(StatDefOf.Mass);
        if (unitMass <= 0f)
        {
            return width;
        }

        width -= MassColumnWidth;
        var columnRect = new Rect(width, 0f, MassColumnWidth, rowRect.height);

        var availableMass = GetAvailableMassFor(trad);

        Text.Anchor = TextAnchor.MiddleRight;
        GUI.color = unitMass > availableMass ? Color.red : Color.grey;
        var labelRect = columnRect;
        labelRect.xMax -= 5f;
        Widgets.Label(labelRect, unitMass.ToStringMass());
        GUI.color = Color.white;
        if (Mouse.IsOver(columnRect))
        {
            TooltipHandler.TipRegionByKey(columnRect, "RealisticOrbitalTrade.ItemMassTip");
        }

        return width;
    }

    // The remaining shuttle capacity for whichever direction this tradeable currently moves in
    // (or would move in first, if it's not currently being traded at all): selling checks the
    // outgoing shuttle, buying the incoming one.
    private static float GetAvailableMassFor(Tradeable trad)
    {
        var deal = TradeSession.deal;
        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        var sellDirection =
            trad.CountToTransfer < 0
            || (trad.CountToTransfer == 0 && trad.CountHeldBy(Transactor.Colony) > 0);

        if (sellDirection)
        {
            // A new trade's sale is divided across as many shuttles as it needs, so all that's
            // left to fit is a single thing onto a shuttle.
            return Rimworld_Dialog_Trade_DoWindowContents.OutgoingShuttleLimit == null
                ? massCapacity
                : Rimworld_Dialog_Trade_DoWindowContents.OutgoingMassCapacity
                    - Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(deal);
        }

        // Bought goods arrive on as many shuttles as they need, so only a single thing has to fit
        // onto one.
        var tradeAgreement = TradeShipData.tradeAgreementForQuest!;
        return tradeAgreement.deliverItemsViaDropPod ? float.MaxValue : massCapacity;
    }

    // Mass-based CountToTransfer bounds for a single tradeable row, mirroring
    // TransferableOneWayWidget's per-row mass threshold in Dialog_FormCaravan. massMin is only
    // set (a negative sell-count bound) while the row is currently selling or undecided, and
    // massMax (a positive buy-count bound) only while it's currently buying or undecided - a
    // row that's actively selling never gets constrained by the (unrelated) incoming shuttle's
    // remaining capacity, and vice versa. Shared between the vanilla mass column/M-buttons and
    // the Dynamic Trade Interface integration's own count-adjust bounds.
    //
    // Changing this row's count also changes the deal's currency (silver) tradeable, which
    // TradeDeal.UpdateCurrencyCount rebalances after every count change so the trade stays fair -
    // buying more of something costs more silver, so less silver comes back to (or more is paid
    // by) the colony. Since silver itself has mass, the bound search below has to account for that
    // rebalancing rather than assuming every other tradeable's count/mass stays fixed while this
    // row's count changes.
    internal static (int? massMin, int? massMax) GetMassBounds(Tradeable trad) =>
        TradeShipData.tradeAgreementForQuest == null || !trad.HasAnyThing
            ? ((int? massMin, int? massMax))(null, null)
            : ComputeMassBounds(trad, trad.AnyThing.GetStatValue(StatDefOf.Mass));

    // Caps a max buy/sell count by the mass bounds; massMin is negative (a sell count), so it is
    // negated for PlayerSells.
    internal static int ClampMaxAmount(
        int result,
        TradeAction action,
        int? massMin,
        int? massMax
    ) =>
        action == TradeAction.PlayerBuys && massMax.HasValue ? Math.Min(result, massMax.Value)
        : action == TradeAction.PlayerSells && massMin.HasValue ? Math.Min(result, -massMin.Value)
        : result;

    // The direction-gating half of GetMassBounds, split out so it can be exercised with a
    // pre-computed unitMass instead of going through Thing.GetStatValue (which needs a live game
    // to resolve, unlike everything below).
    internal static (int? massMin, int? massMax) ComputeMassBounds(Tradeable trad, float unitMass)
    {
        if (unitMass <= 0f)
        {
            return (null, null);
        }

        var deal = TradeSession.deal;
        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;

        // The currency tradeable's own row isn't subject to this rebalancing effect from itself,
        // so it's simply excluded (as if there were no currency tradeable to rebalance).
        var currency = trad.IsCurrency ? null : deal.CurrencyTradeable;
        var unitMassCurrency =
            currency?.HasAnyThing == true ? currency.AnyThing.GetStatValue(StatDefOf.Mass) : 0f;

        // Every other (non-currency, non-this-row) tradeable's contribution to the deal's net
        // currency cost - the baseline TradeDeal.UpdateCurrencyCount adds this row's own
        // (candidate) cost to when deciding the new currency count.
        var otherCost = 0f;
        if (currency != null)
        {
            foreach (var item in deal.AllTradeables)
            {
                if (item.IsCurrency || item == trad)
                {
                    continue;
                }
                otherCost += item.CurTotalCurrencyCostForSource;
            }
        }

        int? massMin = null;
        if (trad.CountToTransfer <= 0 && trad.CountHeldBy(Transactor.Colony) > 0)
        {
            var otherOutgoingMass = Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(
                deal,
                trad,
                0,
                currency,
                0
            );
            // Nothing heavier than a shuttle can be sold at all, however many shuttles are sent.
            massMin =
                unitMass > massCapacity
                    ? 0
                    : -FindMaxSellCount(
                        trad,
                        currency,
                        unitMass,
                        unitMassCurrency,
                        otherCost,
                        otherOutgoingMass,
                        Rimworld_Dialog_Trade_DoWindowContents.OutgoingMassCapacity,
                        trad.CountHeldBy(Transactor.Colony)
                    );
        }

        int? massMax = null;
        var tradeAgreement = TradeShipData.tradeAgreementForQuest!;
        if (
            trad.CountToTransfer >= 0
            && trad.CountHeldBy(Transactor.Trader) > 0
            && !tradeAgreement.deliverItemsViaDropPod
        )
        {
            var otherIncomingMass = Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(
                deal,
                trad,
                0,
                currency,
                0
            );
            // Nothing heavier than a shuttle can be bought, however many shuttles deliver it.
            massMax =
                unitMass > massCapacity
                    ? 0
                    : FindMaxBuyCount(
                        trad,
                        currency,
                        unitMass,
                        unitMassCurrency,
                        otherCost,
                        otherIncomingMass,
                        float.PositiveInfinity,
                        trad.CountHeldBy(Transactor.Trader)
                    );
        }

        return (massMin, massMax);
    }

    // Largest buy count c (0..trueMax) for which otherIncomingMass, plus this row's own mass at
    // c, plus whatever the currency tradeable's mass contribution would be after
    // TradeDeal.UpdateCurrencyCount rebalances it for that c, stays within massCapacity. Assumes
    // that total is non-decreasing in c, which holds unless this row's mass-per-unit-value is
    // below silver's (silver is deliberately about as light-per-value as tradeables get) - in the
    // rare case that assumption doesn't hold, the search still only ever returns a c that's
    // actually within capacity, just not necessarily the largest one, and the mod's Accept-time
    // mass check remains the authoritative guard regardless.
    internal static int FindMaxBuyCount(
        Tradeable row,
        Tradeable? currency,
        float unitMass,
        float unitMassCurrency,
        float otherCost,
        float otherIncomingMass,
        float massCapacity,
        int trueMax
    )
    {
        float MassAt(int c)
        {
            var mass = otherIncomingMass + (unitMass * c);
            if (currency == null)
            {
                return mass;
            }
            var rowCost = c == 0 ? 0f : c * row.GetPriceFor(TradeAction.PlayerBuys);
            var newCurrencyCount = -currency.CostToInt(otherCost + rowCost);
            if (newCurrencyCount > 0)
            {
                mass += newCurrencyCount * unitMassCurrency;
            }
            return mass;
        }

        return BinarySearchMaxFeasible(MassAt, massCapacity, trueMax);
    }

    // Mirror of FindMaxBuyCount for selling: largest sell magnitude s (0..trueMax, representing a
    // CountToTransfer of -s) for which otherOutgoingMass plus this row's and the rebalanced
    // currency tradeable's mass at that s stays within massCapacity.
    internal static int FindMaxSellCount(
        Tradeable row,
        Tradeable? currency,
        float unitMass,
        float unitMassCurrency,
        float otherCost,
        float otherOutgoingMass,
        float massCapacity,
        int trueMax
    )
    {
        float MassAt(int s)
        {
            var mass = otherOutgoingMass + (unitMass * s);
            if (currency == null)
            {
                return mass;
            }
            var rowCost = s == 0 ? 0f : -s * row.GetPriceFor(TradeAction.PlayerSells);
            var newCurrencyCount = -currency.CostToInt(otherCost + rowCost);
            if (newCurrencyCount < 0)
            {
                mass += -newCurrencyCount * unitMassCurrency;
            }
            return mass;
        }

        return BinarySearchMaxFeasible(MassAt, massCapacity, trueMax);
    }

    /// <summary>
    ///  Largest x in [0, trueMax] with massAt(x) &lt;= massCapacity, assuming massAt is non-decreasing.
    /// Always returns a feasible x (massAt(x) &lt;= massCapacity) even when that assumption doesn't
    /// hold; it just isn't guaranteed to be the largest one in that case.
    /// </summary>
    internal static int BinarySearchMaxFeasible(
        Func<int, float> massAt,
        float massCapacity,
        int trueMax
    )
    {
        if (trueMax <= 0 || massAt(0) > massCapacity)
        {
            return 0;
        }
        if (massAt(trueMax) <= massCapacity)
        {
            return trueMax;
        }

        var lo = 0;
        var hi = trueMax;
        while (lo < hi)
        {
            var mid = lo + ((hi - lo + 1) / 2);
            if (massAt(mid) <= massCapacity)
            {
                lo = mid;
            }
            else
            {
                hi = mid - 1;
            }
        }
        return lo;
    }

    private static List<TransferableCountToTransferStoppingPoint> GetMassStoppingPoints(
        Tradeable trad
    )
    {
        List<TransferableCountToTransferStoppingPoint> points = [];

        var (massMin, massMax) = GetMassBounds(trad);
        if (massMin.HasValue)
        {
            points.Add(new TransferableCountToTransferStoppingPoint(massMin.Value, "M<", ">M"));
        }
        if (massMax.HasValue)
        {
            points.Add(new TransferableCountToTransferStoppingPoint(massMax.Value, "M<", ">M"));
        }

        return points;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var originalInstructionList = instructions.ToList();
        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        // Replace the `null` extraStoppingPoints argument to DoCountAdjustInterface with our
        // mass-based "M<"/">M" stopping points.
        _ = codeMatcher.SearchForward(i => i.opcode == OpCodes.Ldnull);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                "Could not patch TradeUI.DrawTradeableRow, IL does not match expectations ([ldnull])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.SetInstruction(new CodeInstruction(OpCodes.Ldarg_1));
        _ = codeMatcher.Advance(1);
        _ = codeMatcher.Insert([new CodeInstruction(OpCodes.Call, _methodGetMassStoppingPoints)]);

        // Carve out a mass column from the id/label block's width, right before it's constructed
        // as `new Rect(0f, 0f, width, rect.height)` - the only uniquely identifiable spot where
        // `width` is used directly (unlike every other Rect column, which offsets it first).
        _ = codeMatcher.MatchStartForward(
            new CodeMatch(i =>
                i.opcode == OpCodes.Ldc_R4 && i.operand is float f0 && Math.Abs(f0) < 0.001f
            ),
            new CodeMatch(i =>
                i.opcode == OpCodes.Ldc_R4 && i.operand is float f1 && Math.Abs(f1) < 0.001f
            ),
            new CodeMatch(OpCodes.Ldloc_0)
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                "Could not patch TradeUI.DrawTradeableRow, IL does not match expectations ([ldc.r4 0][ldc.r4 0][ldloc.0])"
            );
            return originalInstructionList;
        }

        _ = codeMatcher.Insert([
            new CodeInstruction(OpCodes.Ldarg_0),
            new CodeInstruction(OpCodes.Ldarg_1),
            new CodeInstruction(OpCodes.Ldloc_0),
            new CodeInstruction(OpCodes.Call, _methodDrawMassColumnAndShrinkWidth),
            new CodeInstruction(OpCodes.Stloc_0),
        ]);

        return codeMatcher.Instructions();
    }
}
