#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE0062 // Make local function 'static'
using System.Reflection.Emit;
using RealisticOrbitalTrade.ITabs;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(ITab_ContentsTransporter), "DoItemsLists")]
internal static class Rimworld_ITab_ContentsTransporter_DoItemsLists_Reverse
{
    private static readonly MethodInfo _method_ITab_ContentsBase_DoThingRow = AccessTools.Method(
        typeof(ITab_ContentsBase),
        "DoThingRow"
    );

    private static readonly MethodInfo _method_ITab_ContentsTransporterCustom_DoThingRow =
        AccessTools.Method(
            typeof(ITab_ContentsTransporterCustom),
            nameof(ITab_ContentsTransporterCustom.DoThingRow)
        );

    [HarmonyReversePatch]
    internal static void DoItemsLists(
        ITab_ContentsTransporter instance,
        Rect inRect,
        ref float curY
    )
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var originalInstructionList = instructions.ToList();

            var codeMatcher = new CodeMatcher(originalInstructionList);

            // --- Override call to ITab_ContentsBase:DoThingRow

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == _method_ITab_ContentsBase_DoThingRow
            );
            if (!codeMatcher.IsValid)
            {
                RealisticOrbitalTradeMod.Error(
                    "Could not reverse patch ITab_ContentsTransporter.DoItemsLists, IL does not match expectations: call to ITab_ContentsBase:DoThingRow not found."
                );
                return originalInstructionList;
            }

            _ = codeMatcher.RemoveInstruction();
            _ = codeMatcher.Insert(
                [new(OpCodes.Callvirt, _method_ITab_ContentsTransporterCustom_DoThingRow)]
            );

            return codeMatcher.Instructions();
        }

        // Make compiler happy. This gets patched out anyway.
        _ = inRect;
        _ = curY;
        _ = Transpiler(null!);
    }
}

// This transpiler makes it so that if you have two identical minified items,
// they're not just listed as "Minified thing" in the shuttle's "to be loaded"
// list in its "Content" tab.
[HarmonyPatch(typeof(ITab_ContentsBase), "DoThingRow")]
internal static class Rimworld_ITab_ContentsBase_DoThingRow_Reverse
{
    private static bool FixInfoCardButton(
        float x,
        float y,
        Def def,
        ITab_ContentsBase instance,
        Thing thing
    ) =>
        // Always show an info card for the inner thing, not the minified thing
        thing is MinifiedThing minifiedThing
            ? Widgets.InfoCardButton(x, y, minifiedThing.InnerThing)
            : Widgets.InfoCardButton(x, y, thing);

    private static void FixThingIcon(
        Rect rect,
        ThingDef thingDef,
        ThingDef stuffDef,
        ThingStyleDef thingStyleDef,
        float scale,
        Color? color,
        int? graphicIndexOverride,
#if v1_6
        float alpha,
#endif
        ITab_ContentsBase instance,
        Thing thing
    ) => Widgets.ThingIcon(rect, thing);

    private static string FixLabel(
        string originalLabel,
        ITab_ContentsBase instance,
        int countToTransfer,
        List<Thing> things
    ) =>
        countToTransfer == 1
            ? things[0].LabelCap
            : $"{things[0].LabelCapNoCount} x{countToTransfer}";

    private static readonly MethodInfo _methodWidgetsInfoCardButtonDef = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.InfoCardButton),
        [typeof(float), typeof(float), typeof(Def)]
    );
    private static readonly MethodInfo _methodWidgetsThingIconDef = AccessTools.Method(
        typeof(Widgets),
        nameof(Widgets.ThingIcon),
        [
            typeof(Rect),
            typeof(ThingDef),
            typeof(ThingDef),
            typeof(ThingStyleDef),
            typeof(float),
            typeof(Color?),
            typeof(int?),
#if v1_6
            typeof(float),
#endif
        ]
    );
    private static readonly MethodInfo _methodTextWordWrap_set = AccessTools.PropertySetter(
        typeof(Text),
        nameof(Text.WordWrap)
    );

    private static readonly MethodInfo _methodListOfThingItem_get = AccessTools.PropertyGetter(
        typeof(List<Thing>),
        "Item"
    );

#pragma warning disable CS8625
    private static readonly MethodInfo _methodFixInfoCardButton = SymbolExtensions.GetMethodInfo(
        () =>
            FixInfoCardButton(default, default, default, default, default)
    );

    private static readonly MethodInfo _methodFixThingIcon = SymbolExtensions.GetMethodInfo(() =>
        FixThingIcon(
            default,
            default,
            default,
            default,
            default,
            default,
            default,
#if v1_6
            default,
#endif
            default,
            default
        )
    );
    private static readonly MethodInfo _methodFixLabel = SymbolExtensions.GetMethodInfo(() =>
        FixLabel(default, default, default, default)
    );
#pragma warning restore CS8625

    [HarmonyReversePatch]
    internal static void DoThingRow(
        ITab_ContentsBase instance,
        ThingDef thingDef,
        int count,
        List<Thing> things,
        float width,
        ref float curY,
        Action<int> discardAction
    )
    {
        IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
        {
            var originalInstructionList = instructions.ToList();

            var codeMatcher = new CodeMatcher(originalInstructionList);

            // --- Override call to Widgets::InfoCardButton(Def)

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == _methodWidgetsInfoCardButtonDef
            );
            if (!codeMatcher.IsValid)
            {
                RealisticOrbitalTradeMod.Error(
                    "Could not reverse patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to Widgets::InfoCardButton(Def) not found."
                );
                return originalInstructionList;
            }

            _ = codeMatcher.RemoveInstruction();
            _ = codeMatcher.Insert(
                [
                    new(OpCodes.Ldarg_0),
                    new(OpCodes.Ldarg_3),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Callvirt, _methodListOfThingItem_get),
                    new(OpCodes.Call, _methodFixInfoCardButton),
                ]
            );

            // --- Override call to Widgets::ThingIcon(Def)

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == _methodWidgetsThingIconDef
            );
            if (!codeMatcher.IsValid)
            {
                RealisticOrbitalTradeMod.Error(
                    "Could not reverse patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to Widgets::ThingIcon(Def) not found."
                );
                return originalInstructionList;
            }

            _ = codeMatcher.RemoveInstruction();
            _ = codeMatcher.Insert(
                [
                    // == this
                    new(OpCodes.Ldarg_0),
                    // == things[0]
                    new(OpCodes.Ldarg_3),
                    new(OpCodes.Ldc_I4_0),
                    new(OpCodes.Callvirt, _methodListOfThingItem_get),
                    // call patch method (FixThingIcon)
                    new(OpCodes.Call, _methodFixThingIcon),
                ]
            );

            // --- Replace value of label text

            _ = codeMatcher.SearchForward(i =>
                i.opcode == OpCodes.Call
                && i.operand is MethodInfo m
                && m == _methodTextWordWrap_set
            );
            if (!codeMatcher.IsValid)
            {
                RealisticOrbitalTradeMod.Error(
                    "Could not reverse patch ITab_ContentsBase.DoThingRow, IL does not match expectations: call to set value of Text::WordWrap not found."
                );
                return originalInstructionList;
            }
            _ = codeMatcher.Advance(-1);

            // We need to move this label to our first instruction
            var labels = codeMatcher.Labels.ToList();
            codeMatcher.Labels.Clear();

            _ = codeMatcher.Insert(
                [
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
                    new(OpCodes.Stloc_3),
                ]
            );

            return codeMatcher.Instructions();
        }

        // Make compiler happy. This gets patched out anyway.
        _ = instance;
        _ = thingDef;
        _ = count;
        _ = things;
        _ = width;
        _ = curY;
        _ = discardAction;
        _ = Transpiler(null!);
    }
}
