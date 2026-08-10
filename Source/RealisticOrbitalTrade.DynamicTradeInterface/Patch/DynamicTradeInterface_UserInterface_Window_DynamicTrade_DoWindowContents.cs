using System.Reflection.Emit;
using DynamicTradeInterface.UserInterface;
using RealisticOrbitalTrade.Patch;

namespace RealisticOrbitalTrade.DynamicTradeInterface.Patch;

// We can't use Harmony's attributes here because the patching happens too early then. So instead we
// use StaticConstructorOnStartup to trigger the patching later.
[StaticConstructorOnStartup]
[HotSwappable]
internal static class DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents
{
    static DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents()
    {
        Harmony harmony = new(Constants.Id);
        _ = harmony.Patch(
            AccessTools.Method(
                typeof(Window_DynamicTrade),
                nameof(Window_DynamicTrade.DoWindowContents)
            ),
            prefix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
                    nameof(UpdateCurrencyCount)
                )
            ),
            transpiler: new HarmonyMethod(
                AccessTools.Method(
                    typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
                    nameof(Transpiler)
                )
            )
        );
        _ = harmony.Patch(
            _method_Window_DynamicTrade_DrawCurrencyRow,
            postfix: new HarmonyMethod(
                AccessTools.Method(
                    typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
                    nameof(DrawThresholdRow)
                )
            )
        );
    }

    // Vanilla's Dialog_Trade.DoWindowContents calls TradeSession.deal.UpdateCurrencyCount()
    // unconditionally on every frame, so the silver tradeable always reflects what the current
    // selection actually needs to balance by the time the player looks at it or clicks Accept.
    // Window_DynamicTrade only calls it inside its own throttled _refresh block (e.g. after a row
    // callback flips a dirty flag), not on every frame - so the currency row (and anything reading
    // CountToTransfer off of it, like Rimworld_TradeDeal_TryExecute's renegotiation balance check)
    // can still be showing whatever it was left at from before the window opened.
    private static void UpdateCurrencyCount() => TradeSession.deal.UpdateCurrencyCount();

    private static readonly FieldInfo _field_Window_DynamicTrade_currencyFont = AccessTools.Field(
        typeof(Window_DynamicTrade),
        nameof(Window_DynamicTrade._currencyFont)
    );

    private static readonly MethodInfo _method_Text_LineHeightOf = AccessTools.Method(
        typeof(Text),
        nameof(Text.LineHeightOf)
    );

    private static readonly MethodInfo _method_Window_DynamicTrade_DrawCurrencyRow =
        AccessTools.Method(
            typeof(Window_DynamicTrade),
            nameof(Window_DynamicTrade.DrawCurrencyRow)
        );

    private static readonly MethodInfo _methodExtraRowsCount = AccessTools.Method(
        typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
        nameof(ExtraRowsCount)
    );

    // Number of extra rows to draw below the currency row: the trade-value threshold row (only
    // when that setting is enabled), the outgoing cargo mass row (always, since it's a hard
    // shuttle capacity limit rather than a balance preference), the incoming cargo mass row (only
    // when bought items actually arrive by shuttle rather than drop pod), and the silver-ceiling
    // row (only during a departure renegotiation).
    private static int ExtraRowsCount()
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            return 0;
        }

        var count = 1;
        if (Settings._useMinimumTradeThreshold)
        {
            count++;
        }
        if (!tradeAgreement.deliverItemsViaDropPod)
        {
            count++;
        }
        if (Utils.RenegotiatingBecauseDeparting)
        {
            count++;
        }
        return count;
    }

    private static void DrawThresholdRow(Rect currencyRowRect, GameFont ____currencyFont)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (tradeAgreement == null)
        {
            return;
        }

        var currentFont = Text.Font;
        Text.Font = ____currencyFont;

        var rowRect = new Rect(currencyRowRect);

        if (Settings._useMinimumTradeThreshold)
        {
            rowRect.y += currencyRowRect.height;
            DrawTradeValueThresholdRow(rowRect, tradeAgreement);
        }

        rowRect.y += currencyRowRect.height;
        DrawCargoMassRow(rowRect, outgoing: true);

        if (!tradeAgreement.deliverItemsViaDropPod)
        {
            rowRect.y += currencyRowRect.height;
            DrawCargoMassRow(rowRect, outgoing: false);
        }

        if (Utils.RenegotiatingBecauseDeparting)
        {
            rowRect.y += currencyRowRect.height;
            DrawSilverCeilingRow(rowRect, tradeAgreement);
        }

        Text.Font = currentFont;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void DrawTradeValueThresholdRow(
        Rect thresholdRowRect,
        TradeAgreement tradeAgreement
    )
    {
        var flash = Time.time - Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime < 1f;
        if (flash)
        {
            GUI.DrawTexture(thresholdRowRect, TransferableUIUtility.FlashTex);
        }

        var curX = thresholdRowRect.x + 60f;
        Rect labelRect = new(curX, thresholdRowRect.y, 200f, thresholdRowRect.height);
        var tradeValueLabel = "RealisticOrbitalTrade.TradeValue".Translate().CapitalizeFirst();
        Widgets.Label(labelRect, tradeValueLabel);
        if (Mouse.IsOver(labelRect))
        {
            TooltipHandler.TipRegionByKey(labelRect, "RealisticOrbitalTrade.TradeValueTip");
        }

        var combinedTradeValue =
            Rimworld_Dialog_Trade_DoWindowContents.CalculateCombinedTradeValue();

        var centerX = thresholdRowRect.center.x;
        _ = thresholdRowRect.SplitVerticallyWithMargin(
            out var left,
            out var right,
            out _,
            100f,
            thresholdRowRect.width / 2f
        );

        var leftWidth = left.width;
        left.xMin += (leftWidth - 100f) / 2;
        left.xMax -= (leftWidth - 100f) / 2;

        var rightWidth = right.width;
        right.xMin += (rightWidth - 100f) / 2;
        right.xMax -= (rightWidth - 100f) / 2;

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(left, ((int)Math.Round(combinedTradeValue)).ToStringCached());
        TooltipHandler.TipRegionByKey(left, "RealisticOrbitalTrade.CurrentTradeValue");

        var minimumTradeThreshold = tradeAgreement.tradeShip.GetData().minimumTradeThreshold;
        var tradeValueDifference = (int)Math.Round(combinedTradeValue) - minimumTradeThreshold;
        GUI.color = tradeValueDifference < 0 ? Color.yellow : Color.green;

        Rect currencyLabelRect = new(
            centerX - 50f,
            thresholdRowRect.y,
            100f,
            thresholdRowRect.height
        );
        Widgets.Label(currencyLabelRect, tradeValueDifference.ToStringCached());
        TooltipHandler.TipRegionByKey(
            currencyLabelRect,
            "RealisticOrbitalTrade.TradeValueDifference"
        );
        GUI.color = Color.white;

        Widgets.Label(right, minimumTradeThreshold.ToString(CultureInfo.InvariantCulture));
        TooltipHandler.TipRegionByKey(right, "RealisticOrbitalTrade.TradersMinimumTradeThreshold");
    }

    // Mirrors the equivalent row in the vanilla trade dialog (Rimworld_Dialog_Trade_DoWindowContents
    // .DrawCargoMassRow) but laid out to match this window's currency-row style: label on the left,
    // value/capacity on the right, red once over capacity.
    private static void DrawCargoMassRow(Rect rowRect, bool outgoing)
    {
        var massCapacity = Rimworld_Dialog_Trade_DoWindowContents.ShuttleMassCapacity;
        var mass = outgoing
            ? Rimworld_Dialog_Trade_DoWindowContents.CalculateOutgoingMass(TradeSession.deal)
            : Rimworld_Dialog_Trade_DoWindowContents.CalculateIncomingMass(TradeSession.deal);
        var overCapacity = mass > massCapacity;
        var labelKey = outgoing
            ? "RealisticOrbitalTrade.CargoMassLabel"
            : "RealisticOrbitalTrade.IncomingCargoMassLabel";
        var tipKey = outgoing
            ? "RealisticOrbitalTrade.CargoMassTip"
            : "RealisticOrbitalTrade.IncomingCargoMassTip";
        var flashTime = outgoing
            ? Rimworld_Dialog_Trade_DoWindowContents.lastMassFlashTime
            : Rimworld_Dialog_Trade_DoWindowContents.lastIncomingMassFlashTime;

        var flash = Time.time - flashTime < 1f;
        if (flash)
        {
            GUI.DrawTexture(rowRect, TransferableUIUtility.FlashTex);
        }

        // The currency row's own font (____currencyFont) is sized for a handful of digits, not
        // a multi-word label like "outgoing cargo mass" - use GameFont.Small here instead so the
        // label reliably fits on one line, and size its rect to fill exactly the space between the
        // indent and the value column rather than a fixed width that may not fit every label.
        var currentFont = Text.Font;
        Text.Font = GameFont.Small;

        Rect valueRect = new(rowRect.xMax - 260f, rowRect.y, 200f, rowRect.height);

        var curX = rowRect.x + 60f;
        Rect labelRect = new(curX, rowRect.y, valueRect.x - curX - 10f, rowRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(labelRect, labelKey.Translate().CapitalizeFirst());
        if (Mouse.IsOver(labelRect))
        {
            TooltipHandler.TipRegionByKey(
                labelRect,
                tipKey,
                mass.ToStringEnsureThreshold(massCapacity, 2),
                massCapacity.ToString("F2", CultureInfo.CurrentCulture)
            );
        }

        GUI.color = overCapacity ? Color.red : Color.white;
        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(
            valueRect,
            mass.ToStringEnsureThreshold(massCapacity, 1)
                + " / "
                + massCapacity.ToString("F0", CultureInfo.CurrentCulture)
                + " "
                + "kg".Translate()
        );
        GUI.color = Color.white;
        if (Mouse.IsOver(valueRect))
        {
            TooltipHandler.TipRegionByKey(
                valueRect,
                tipKey,
                mass.ToStringEnsureThreshold(massCapacity, 2),
                massCapacity.ToString("F2", CultureInfo.CurrentCulture)
            );
        }

        Text.Font = currentFont;
    }

    // Mirrors the equivalent row in the vanilla trade dialog (Rimworld_Dialog_Trade_DoWindowContents
    // .DrawSilverCeilingRow) but laid out to match this window's currency-row style - see that
    // method for why this row exists at all (silver's own row can't show the ceiling itself).
    private static void DrawSilverCeilingRow(Rect rowRect, TradeAgreement tradeAgreement)
    {
        var currentFont = Text.Font;
        Text.Font = GameFont.Small;

        var maxPayableSilver = TradeAgreement.MaxPayableSilver(
            Utils.RenegotiationOriginalTradeables ?? tradeAgreement.AllTradeables
        );

        Rect valueRect = new(rowRect.xMax - 260f, rowRect.y, 200f, rowRect.height);

        var curX = rowRect.x + 60f;
        Rect labelRect = new(curX, rowRect.y, valueRect.x - curX - 10f, rowRect.height);
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(
            labelRect,
            "RealisticOrbitalTrade.MaxPayableSilverLabel".Translate().CapitalizeFirst()
        );
        if (Mouse.IsOver(labelRect))
        {
            TooltipHandler.TipRegionByKey(
                labelRect,
                "RealisticOrbitalTrade.MaxPayableSilverTip",
                maxPayableSilver.ToStringCached()
            );
        }

        Text.Anchor = TextAnchor.MiddleRight;
        Widgets.Label(valueRect, maxPayableSilver.ToStringCached());
        if (Mouse.IsOver(valueRect))
        {
            TooltipHandler.TipRegionByKey(
                valueRect,
                "RealisticOrbitalTrade.MaxPayableSilverTip",
                maxPayableSilver.ToStringCached()
            );
        }

        Text.Font = currentFont;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions,
        ILGenerator generator
    )
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Ldfld
            && i.operand is FieldInfo f
            && f == _field_Window_DynamicTrade_currencyFont
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([ldfld Window_DynamicTrade._currencyFont])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);
        if (
            codeMatcher.Opcode != OpCodes.Call
            || codeMatcher.Operand is not MethodInfo m
            || m != _method_Text_LineHeightOf
        )
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([call Text.LineHeightOf])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);
        if (
            codeMatcher.Opcode != OpCodes.Stloc_0
            && codeMatcher.Opcode != OpCodes.Stloc_1
            && codeMatcher.Opcode != OpCodes.Stloc_2
            && codeMatcher.Opcode != OpCodes.Stloc_3
            && codeMatcher.Opcode != OpCodes.Stloc_S
        )
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([stloc.*]), was {codeMatcher.Opcode}"
            );
            return originalInstructionList;
        }

        var storeOpcode = codeMatcher.Opcode;
        OpCode loadOpcode;
        LocalBuilder? loadOperand = null;
        if (storeOpcode == OpCodes.Stloc_0)
        {
            loadOpcode = OpCodes.Ldloc_0;
        }
        else if (storeOpcode == OpCodes.Stloc_1)
        {
            loadOpcode = OpCodes.Ldloc_1;
        }
        else if (storeOpcode == OpCodes.Stloc_2)
        {
            loadOpcode = OpCodes.Ldloc_2;
        }
        else if (storeOpcode == OpCodes.Stloc_3)
        {
            loadOpcode = OpCodes.Ldloc_3;
        }
        else
        {
            loadOpcode = OpCodes.Ldloc_S;
            loadOperand = codeMatcher.Operand as LocalBuilder;
        }

        _ = codeMatcher.SearchForward(i => i.opcode == loadOpcode && i.operand == loadOperand);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                $"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([ldloc.3])"
            );
            return originalInstructionList;
        }
        _ = codeMatcher.Advance(1);

        // Change (row height x) into x * (1 + ExtraRowsCount()), i.e. x + x * n, so the space
        // reserved below the currency row scales with however many of our rows actually get
        // drawn (0-3: threshold, outgoing mass, incoming mass).
        _ = codeMatcher.Insert([
            new(OpCodes.Dup),
            new(OpCodes.Call, _methodExtraRowsCount),
            new(OpCodes.Conv_R4),
            new(OpCodes.Mul),
            new(OpCodes.Add),
        ]);

        return codeMatcher.Instructions();
    }
}
