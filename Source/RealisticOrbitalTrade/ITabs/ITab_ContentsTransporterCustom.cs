using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace RealisticOrbitalTrade.ITabs;

internal class ITab_ContentsTransporterCustom : ITab_ContentsTransporter
{
    public ITab_ContentsTransporterCustom()
    {
        canRemoveThings = false;
    }
}

// This transpiler makes it so that if you have two identical minified items,
// they're not just listed as "Minified thing" in the shuttle's "to be loaded"
// list in its "Content" tab.
[HarmonyPatch(typeof(ITab_ContentsBase), "DoThingRow")]
internal static class Rimworld_ITab_ContentsBase_DoThingRow
{
    private static bool FixInfoCardButton(float x, float y, Def def, ITab_ContentsBase instance, Thing thing)
    {
        // Only modify the behavior for our custom ITab
        if (instance is not ITab_ContentsTransporterCustom)
        {
            return Widgets.InfoCardButton(x, y, def);
        }

        // Always show an info card for the inner thing, not the minified thing
        if (thing is MinifiedThing minifiedThing)
        {
            return Widgets.InfoCardButton(x, y, minifiedThing.InnerThing);
        }
        else
        {
            return Widgets.InfoCardButton(x, y, thing);
        }
    }

    private static void FixThingIcon(Rect rect, ThingDef thingDef, ThingDef stuffDef, ThingStyleDef thingStyleDef, float scale, Color? color, int? graphicIndexOverride, ITab_ContentsBase instance, Thing thing)
    {
        // Only modify the behavior for our custom ITab
        if (instance is not ITab_ContentsTransporterCustom)
        {
            Widgets.ThingIcon(rect, thingDef, stuffDef, thingStyleDef, scale, color, graphicIndexOverride);
            return;
        }

        Widgets.ThingIcon(rect, thing);
    }

    private static string FixLabel(string originalLabel, ITab_ContentsBase instance, int countToTransfer, List<Thing> things)
    {
        // Only modify the behavior for our custom ITab
        if (instance is not ITab_ContentsTransporterCustom)
        {
            return originalLabel;
        }

        if (countToTransfer == 1)
        {
            return things[0].LabelCap;
        }
        else
        {
            return $"{things[0].LabelCapNoCount} x{countToTransfer}";
        }
    }

    private static readonly MethodInfo _methodWidgetsInfoCardButtonDef = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.InfoCardButton),
        new[] {
            typeof(float),
            typeof(float),
            typeof(Def)
        });
    private static readonly MethodInfo _methodWidgetsThingIconDef = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.ThingIcon),
        new[] {
            typeof(Rect),
            typeof(ThingDef),
            typeof(ThingDef),
            typeof(ThingStyleDef),
            typeof(float),
            typeof(Color?),
            typeof(int?),
        });
    private static readonly MethodInfo _methodTextWordWrap_set = AccessTools.PropertySetter(typeof(Text), nameof(Text.WordWrap));

    private static readonly MethodInfo _methodListOfThingItem_get = AccessTools.PropertyGetter(typeof(List<Thing>), "Item");

#pragma warning disable CS8625
    private static readonly MethodInfo _methodFixInfoCardButton = SymbolExtensions.GetMethodInfo(() => FixInfoCardButton(default, default, default, default, default));

    private static readonly MethodInfo _methodFixThingIcon = SymbolExtensions.GetMethodInfo(() => FixThingIcon(default, default, default, default, default, default, default, default, default));
    private static readonly MethodInfo _methodFixLabel = SymbolExtensions.GetMethodInfo(() => FixLabel(default, default, default, default));
#pragma warning restore CS8625

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var originalInstructionList = instructions.ToList();

        var codeMatcher = new CodeMatcher(originalInstructionList);

        // --- Override call to Widgets::InfoCardButton(Def)

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _methodWidgetsInfoCardButtonDef);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error("Could not patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to Widgets::InfoCardButton(Def) not found.");
            return originalInstructionList;
        }

        codeMatcher.RemoveInstruction();
        codeMatcher.Insert(new CodeInstruction[] {
            new(OpCodes.Ldarg_0),
            new(OpCodes.Ldarg_3),
            new(OpCodes.Ldc_I4_0),
            new(OpCodes.Callvirt, _methodListOfThingItem_get),
            new(OpCodes.Call, _methodFixInfoCardButton)
        });

        // --- Override call to Widgets::ThingIcon(Def)

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _methodWidgetsThingIconDef);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error("Could not patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to Widgets::ThingIcon(Def) not found.");
            return originalInstructionList;
        }

        codeMatcher.RemoveInstruction();
        codeMatcher.Insert(new CodeInstruction[] {
            // == this
            new(OpCodes.Ldarg_0),
            // == things[0]
            new(OpCodes.Ldarg_3),
            new(OpCodes.Ldc_I4_0),
            new(OpCodes.Callvirt, _methodListOfThingItem_get),
            // call patch method (FixThingIcon)
            new(OpCodes.Call, _methodFixThingIcon)
        });

        // --- Replace value of label text

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _methodTextWordWrap_set);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error("Could not patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to set value of Text::WordWrap not found.");
            return originalInstructionList;
        }
        codeMatcher.Advance(-1);

        // We need to move this label to our first instruction
        List<Label> labels = codeMatcher.Labels.ToList();
        codeMatcher.Labels.Clear();

        codeMatcher.Insert(new CodeInstruction[] {
            // Original value (used in pass-through)
            new(OpCodes.Ldloc_3) { labels = labels },
            // == this
            new(OpCodes.Ldarg_0),
            // == [int count] argument
            new(OpCodes.Ldarg_2),
            // == things
            new(OpCodes.Ldarg_3),
            // new(OpCodes.Ldc_I4_0),
            // new(OpCodes.Callvirt, _methodListOfThingItem_get),
            // call patch method (FixLabel)
            new(OpCodes.Call, _methodFixLabel),
            // Overwrite original value with patch method return value
            new(OpCodes.Stloc_3)
        });

        return codeMatcher.Instructions();
    }
}
