#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE0062 // Make local function 'static'

using System.Reflection.Emit;
using System.Text;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.IsRequired))]
internal static class Rimworld_CompShuttle_IsRequired
{
    internal static void Postfix(CompShuttle __instance, Thing thing, ref bool __result)
    {
        var compTradeShuttle = __instance.parent?.TryGetComp<CompTradeShuttle>();
        if (!__result && compTradeShuttle != null)
        {
            __result = compTradeShuttle.IsRequired(thing);
        }
    }
}

[HarmonyPatch(typeof(CompShuttle), "CheckAutoload")]
internal static class Rimworld_CompShuttle_CheckAutoload_SpecificItems
{
    internal static void Postfix(CompShuttle __instance)
    {
        var compTradeShuttle = __instance.parent?.TryGetComp<CompTradeShuttle>();
        compTradeShuttle?.CheckAutoload();
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.RequiredThingsLabel), MethodType.Getter)]
internal static class Rimworld_CompShuttle_RequiredThingsLabel
{
    private static void InjectThingsLabel(CompShuttle __instance, StringBuilder stringBuilder)
    {
        var compTradeShuttle = __instance.parent?.TryGetComp<CompTradeShuttle>();
        if (compTradeShuttle != null && compTradeShuttle.requiredSpecificItems.Count > 0)
        {
            foreach (var requiredItem in compTradeShuttle.requiredSpecificItems)
            {
                _ = stringBuilder.AppendLine("  - " + requiredItem.Label.CapitalizeFirst());
            }
        }
    }

    private static readonly MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(
        () =>
            InjectThingsLabel(new(), new())
    );

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        try
        {
            return Utils.InjectCallBeforeReturn(
                instructions,
                _methodInjectThingsLabel,
                i => i.IsLdloc(),
                [new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldloc_0)]
            );
        }
        catch (InjectCallBeforeReturnException e)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                "Could not patch CompShuttle.RequiredThingsLabel, IL does not match expectations"
            );
            return e.Instructions;
        }
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.AllRequiredThingsLoaded), MethodType.Getter)]
internal static class Rimworld_CompShuttle_AllRequiredThingsLoaded
{
    internal static void Postfix(CompShuttle __instance, ref bool __result)
    {
        var compTradeShuttle = __instance.parent?.TryGetComp<CompTradeShuttle>();
        if (__result && compTradeShuttle != null)
        {
            __result = compTradeShuttle.AllRequiredThingsLoaded;
        }
    }
}

[HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.SubtractFromToLoadList))]
internal static class Rimworld_CompTransporter_SubtractFromToLoadList
{
    private static bool HideFinishedMessageForTradeShuttle(CompTransporter compTransporter) =>
        compTransporter?.parent.TryGetComp<CompTradeShuttle>() != null;

#pragma warning disable CS8625
    private static readonly MethodInfo _methodHideFinishedMessageForTradeShuttle =
        SymbolExtensions.GetMethodInfo(() => HideFinishedMessageForTradeShuttle(default));
#pragma warning restore CS8625

    private static readonly MethodInfo _methodCompTransporterAnyInGroupHasAnythingLeftToLoad_get =
        AccessTools.PropertyGetter(
            typeof(CompTransporter),
            nameof(CompTransporter.AnyInGroupHasAnythingLeftToLoad)
        );

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codeMatcher = new CodeMatcher(instructions);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m == _methodCompTransporterAnyInGroupHasAnythingLeftToLoad_get
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogError(
                "Could not patch CompTransporter.SubtractFromToLoadList, IL does not match expectations: call to get value of CompTransporter::AnyInGroupHasAnythingLeftToLoad not found."
            );
            return codeMatcher.Instructions();
        }
        _ = codeMatcher.Advance(1);
        var endLabel = codeMatcher.Operand;
        _ = codeMatcher.Advance(1);

        _ = codeMatcher.Insert(
            [
                // == this
                new(OpCodes.Ldarg_0),
                // call patch method (HideFinishedMessageForTradeShuttle)
                new(OpCodes.Call, _methodHideFinishedMessageForTradeShuttle),
                // if HideFinishedMessageForTradeShuttle returns true, skip showing message
                new(OpCodes.Brtrue_S, endLabel),
            ]
        );

        return codeMatcher.Instructions();
    }
}

// These two fix not hauling books out of book cases:
#if v1_5
[HarmonyPatch(
    typeof(LoadTransportersJobUtility),
    nameof(LoadTransportersJobUtility.FindThingToLoad)
)]
internal static class Rimworld_LoadTransportersJobUtility_FindThingToLoad
{
    private static readonly MethodInfo _method_GenClosest_ClosestThingReachable =
        AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
    private static readonly MethodInfo _method_GenClosest_ClosestThingReachable_NewTemp =
        AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable_NewTemp));

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codeMatcher = new CodeMatcher(instructions);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m == _method_GenClosest_ClosestThingReachable
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Could not patch LoadTransportersJobUtility.FindThingToLoad, IL does not match expectations: [call GenClosest.ClosestThingReachable] not found."
            );
            return codeMatcher.Instructions();
        }

        codeMatcher.Operand = _method_GenClosest_ClosestThingReachable_NewTemp;

        _ = codeMatcher.Insert([new(OpCodes.Ldc_I4_1)]);

        return codeMatcher.Instructions();
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable_NewTemp))]
internal static class Rimworld_GenClosest_ClosestThingReachable_NewTemp
{
    private static readonly MethodInfo _method_GenClosest_ClosestThing_Global = AccessTools.Method(
        typeof(GenClosest),
        nameof(GenClosest.ClosestThing_Global)
    );
    private static readonly MethodInfo _method_GenClosest_ClosestThing_Global_NewTemp =
        AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_NewTemp));

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    )
    {
        var codeMatcher = new CodeMatcher(instructions);

        _ = codeMatcher.SearchForward(i =>
            i.opcode == OpCodes.Call
            && i.operand is MethodInfo m
            && m == _method_GenClosest_ClosestThing_Global
        );
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Instance.LogWarning(
                "Could not patch GenClosest.ClosestThingReachable_NewTemp, IL does not match expectations: [call GenClosest.ClosestThing_Global] not found."
            );
            return codeMatcher.Instructions();
        }

        codeMatcher.Operand = _method_GenClosest_ClosestThing_Global_NewTemp;

        _ = codeMatcher.Insert([new(OpCodes.Ldarg_S, 13)]);

        return codeMatcher.Instructions();
    }
}
#endif
