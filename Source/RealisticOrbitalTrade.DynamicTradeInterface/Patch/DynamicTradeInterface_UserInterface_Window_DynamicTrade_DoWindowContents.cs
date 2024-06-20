using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

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
        harmony.Patch(
            AccessTools.Method("DynamicTradeInterface.UserInterface.Window_DynamicTrade:DoWindowContents"),
            transpiler: new HarmonyMethod(AccessTools.Method(
                typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
                nameof(Transpiler)
            ))
        );
        harmony.Patch(
            _method_Window_DynamicTrade_DrawCurrencyRow,
            postfix: new HarmonyMethod(AccessTools.Method(
                typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
                nameof(DrawThresholdRow)
            ))
        );
    }

    private static readonly FieldInfo _field_Window_DynamicTrade_currencyFont = AccessTools.Field(
        "DynamicTradeInterface.UserInterface.Window_DynamicTrade:_currencyFont");

    private static readonly MethodInfo _method_Text_LineHeightOf = AccessTools.Method(
        typeof(Text),
        nameof(Text.LineHeightOf));

    private static readonly MethodInfo _method_Window_DynamicTrade_DrawCurrencyRow = AccessTools.Method(
        "DynamicTradeInterface.UserInterface.Window_DynamicTrade:DrawCurrencyRow");

    private static readonly MethodInfo _methodWillDrawThresholdRow = AccessTools.Method(
        typeof(DynamicTradeInterface_UserInterface_Window_DynamicTrade_DoWindowContents),
        nameof(WillDrawThresholdRow));

    private static bool WillDrawThresholdRow()
    {
        return Settings._useMinimumTradeThreshold && TradeShipData.tradeAgreementForQuest != null;
    }
    private static void DrawThresholdRow(Rect currencyRowRect, GameFont ____currencyFont)
    {
        TradeAgreement? tradeAgreement = TradeShipData.tradeAgreementForQuest;
        if (!Settings._useMinimumTradeThreshold || tradeAgreement == null)
        {
            return;
        }

        GameFont currentFont = Text.Font;
        Text.Font = ____currencyFont;

        var thresholdRowRect = new Rect(currencyRowRect);
        thresholdRowRect.y += currencyRowRect.height;

        bool flash = Time.time - Rimworld_Dialog_Trade_DoWindowContents.lastTradeValueFlashTime < 1f;
        if (flash)
        {
            GUI.DrawTexture(thresholdRowRect, TransferableUIUtility.FlashTex);
        }

        float curX = thresholdRowRect.x + 60f;
        Rect labelRect = new(curX, thresholdRowRect.y, 200f, thresholdRowRect.height);
        TaggedString tradeValueLabel = "RealisticOrbitalTrade.TradeValue".Translate().CapitalizeFirst();
        Widgets.Label(labelRect, tradeValueLabel);
        if (Mouse.IsOver(labelRect))
        {
            TooltipHandler.TipRegionByKey(labelRect, "RealisticOrbitalTrade.TradeValueTip");
        }

        float combinedTradeValue = Rimworld_Dialog_Trade_DoWindowContents.CalculateCombinedTradeValue();

        float centerX = thresholdRowRect.center.x;
        thresholdRowRect.SplitVerticallyWithMargin(out var left, out var right, out var _, 100f, thresholdRowRect.width / 2f);

        var leftWidth = left.width;
        left.xMin += (leftWidth - 100f) / 2;
        left.xMax -= (leftWidth - 100f) / 2;

        var rightWidth = right.width;
        right.xMin += (rightWidth - 100f) / 2;
        right.xMax -= (rightWidth - 100f) / 2;

        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(left, ((int)Math.Round(combinedTradeValue)).ToStringCached());
        TooltipHandler.TipRegionByKey(left, "RealisticOrbitalTrade.CurrentTradeValue");

        int minimumTradeThreshold = tradeAgreement.tradeShip.GetData().minimumTradeThreshold;
        int tradeValueDifference = (int)Math.Round(combinedTradeValue) - minimumTradeThreshold;
        GUI.color = tradeValueDifference < 0 ? Color.yellow : Color.green;

        Rect currencyLabelRect = new(centerX - 50f, thresholdRowRect.y, 100f, thresholdRowRect.height);
        Widgets.Label(currencyLabelRect, tradeValueDifference.ToStringCached());
        TooltipHandler.TipRegionByKey(currencyLabelRect, "RealisticOrbitalTrade.TradeValueDifference");
        GUI.color = Color.white;

        Widgets.Label(right, minimumTradeThreshold.ToString());
        TooltipHandler.TipRegionByKey(right, "RealisticOrbitalTrade.TradersMinimumTradeThreshold");

        Text.Font = currentFont;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    internal static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions, ILGenerator generator)
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList, generator);

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Ldfld && i.operand is FieldInfo f && f == _field_Window_DynamicTrade_currencyFont);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error($"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([ldfld Window_DynamicTrade._currencyFont])");
            return originalInstructionList;
        }
        codeMatcher.Advance(1);
        if (codeMatcher.Opcode != OpCodes.Call || codeMatcher.Operand is not MethodInfo m || m != _method_Text_LineHeightOf)
        {
            RealisticOrbitalTradeMod.Error($"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([call Text.LineHeightOf])");
            return originalInstructionList;
        }

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Ldloc_3);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error($"Could not patch Window_DynamicTrade.DoWindowContents, IL does not match expectations ([ldloc.3])");
            return originalInstructionList;
        }
        codeMatcher.Advance(1);

        codeMatcher.CreateLabel(out var skipDupLabel);

        codeMatcher.Insert([
            new(OpCodes.Call, _methodWillDrawThresholdRow),
            new(OpCodes.Brfalse_S, skipDupLabel),
            new(OpCodes.Dup),
            new(OpCodes.Add),
        ]);

        return codeMatcher.Instructions();
    }
}
