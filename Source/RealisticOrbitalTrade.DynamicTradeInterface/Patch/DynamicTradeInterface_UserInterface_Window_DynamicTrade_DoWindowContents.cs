using System.Reflection.Emit;

namespace RealisticOrbitalTrade.Patch;

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
                "DynamicTradeInterface.UserInterface.Window_DynamicTrade:DoWindowContents"
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

    private static readonly FieldInfo _field_Window_DynamicTrade_currencyFont = AccessTools.Field(
        "DynamicTradeInterface.UserInterface.Window_DynamicTrade:_currencyFont"
    );

    private static readonly MethodInfo _method_Text_LineHeightOf = AccessTools.Method(
        typeof(Text),
        nameof(Text.LineHeightOf)
    );

    private static readonly MethodInfo _method_Window_DynamicTrade_DrawCurrencyRow =
        AccessTools.Method(
            "DynamicTradeInterface.UserInterface.Window_DynamicTrade:DrawCurrencyRow"
        );

    private static readonly MethodInfo _methodWillDrawThresholdRow = AccessTools.Method(
        typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
        nameof(WillDrawThresholdRow)
    );

    private static bool WillDrawThresholdRow() =>
        Settings._useMinimumTradeThreshold && TradeShipData.tradeAgreementForQuest != null;

    private static void DrawThresholdRow(Rect currencyRowRect, GameFont ____currencyFont)
    {
        var tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (!Settings._useMinimumTradeThreshold || tradeAgreement == null)
        {
            return;
        }

        var currentFont = Text.Font;
        Text.Font = ____currencyFont;

        var thresholdRowRect = new Rect(currencyRowRect);
        thresholdRowRect.y += currencyRowRect.height;

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

        Text.Font = currentFont;
        Text.Anchor = TextAnchor.UpperLeft;
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

        _ = codeMatcher.CreateLabel(out var skipDupLabel);

        _ = codeMatcher.Insert(
            [
                new(OpCodes.Call, _methodWillDrawThresholdRow),
                new(OpCodes.Brfalse_S, skipDupLabel),
                new(OpCodes.Dup),
                new(OpCodes.Add),
            ]
        );

        return codeMatcher.Instructions();
    }
}
