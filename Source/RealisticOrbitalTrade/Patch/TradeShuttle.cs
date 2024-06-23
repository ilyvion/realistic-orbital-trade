using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using HarmonyLib;
using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.IsRequired))]
internal static class Rimworld_CompShuttle_IsRequired
{
    private static void Postfix(CompShuttle __instance, Thing thing, ref bool __result)
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
    private static void Postfix(CompShuttle __instance)
    {
        var compTradeShuttle = __instance.parent?.TryGetComp<CompTradeShuttle>();
        if (compTradeShuttle != null)
        {
            compTradeShuttle.CheckAutoload();
        }
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
                stringBuilder.AppendLine("  - " + requiredItem.Label.CapitalizeFirst());
            }
        }
    }

    private static readonly MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(() => InjectThingsLabel(new(), new()));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        try
        {
            return Utils.InjectCallBeforeReturn(instructions, _methodInjectThingsLabel, i => i.IsLdloc(), [
                new CodeInstruction(OpCodes.Ldarg_0),
                new CodeInstruction(OpCodes.Ldloc_0),
            ]);
        }
        catch (InjectCallBeforeReturnException e)
        {
            RealisticOrbitalTradeMod.Error("Could not patch CompShuttle.RequiredThingsLabel, IL does not match expectations");
            return e.Instructions;
        }
    }
}

[HarmonyPatch(typeof(CompShuttle))]
[HarmonyPatch(nameof(CompShuttle.AllRequiredThingsLoaded), MethodType.Getter)]
internal static class Rimworld_CompShuttle_AllRequiredThingsLoaded
{
    private static void Postfix(CompShuttle __instance, ref bool __result)
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
    private static bool HideFinishedMessageForTradeShuttle(CompTransporter compTransporter)
    {
        return compTransporter?.parent.TryGetComp<CompTradeShuttle>() != null;
    }

#pragma warning disable CS8625
    private static readonly MethodInfo _methodHideFinishedMessageForTradeShuttle = SymbolExtensions.GetMethodInfo(() => HideFinishedMessageForTradeShuttle(default));
#pragma warning restore CS8625

    private static readonly MethodInfo _methodCompTransporterAnyInGroupHasAnythingLeftToLoad_get = AccessTools.PropertyGetter(typeof(CompTransporter), nameof(CompTransporter.AnyInGroupHasAnythingLeftToLoad));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _methodCompTransporterAnyInGroupHasAnythingLeftToLoad_get);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Error("Could not patch CompTransporter.SubtractFromToLoadList, IL does not match expectations: call to get value of CompTransporter::AnyInGroupHasAnythingLeftToLoad not found.");
            return codeMatcher.Instructions();
        }
        codeMatcher.Advance(1);
        var endLabel = codeMatcher.Operand;
        codeMatcher.Advance(1);

        codeMatcher.Insert([
            // == this
            new(OpCodes.Ldarg_0),
            // call patch method (HideFinishedMessageForTradeShuttle)
            new(OpCodes.Call, _methodHideFinishedMessageForTradeShuttle),
            // if HideFinishedMessageForTradeShuttle returns true, skip showing message
            new(OpCodes.Brtrue_S, endLabel)
        ]);

        return codeMatcher.Instructions();
    }
}

// These two fix not hauling books out of book cases:
#if v1_5
[HarmonyPatch(typeof(LoadTransportersJobUtility), nameof(LoadTransportersJobUtility.FindThingToLoad))]
internal static class Rimworld_LoadTransportersJobUtility_FindThingToLoad
{
    private static readonly MethodInfo _method_GenClosest_ClosestThingReachable = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable));
    private static readonly MethodInfo _method_GenClosest_ClosestThingReachable_NewTemp = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable_NewTemp));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _method_GenClosest_ClosestThingReachable);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Warning("Could not patch LoadTransportersJobUtility.FindThingToLoad, IL does not match expectations: [call GenClosest.ClosestThingReachable] not found.");
            return codeMatcher.Instructions();
        }

        codeMatcher.Operand = _method_GenClosest_ClosestThingReachable_NewTemp;

        codeMatcher.Insert([
            new(OpCodes.Ldc_I4_1)
        ]);

        return codeMatcher.Instructions();
    }
}

[HarmonyPatch(typeof(GenClosest), nameof(GenClosest.ClosestThingReachable_NewTemp))]
internal static class Rimworld_GenClosest_ClosestThingReachable_NewTemp
{
    private static readonly MethodInfo _method_GenClosest_ClosestThing_Global = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global));
    private static readonly MethodInfo _method_GenClosest_ClosestThing_Global_NewTemp = AccessTools.Method(typeof(GenClosest), nameof(GenClosest.ClosestThing_Global_NewTemp));

    private static IEnumerable<CodeInstruction> Transpiler(IEnumerable<CodeInstruction> instructions)
    {
        var codeMatcher = new CodeMatcher(instructions);

        codeMatcher.SearchForward(i => i.opcode == OpCodes.Call && i.operand is MethodInfo m && m == _method_GenClosest_ClosestThing_Global);
        if (!codeMatcher.IsValid)
        {
            RealisticOrbitalTradeMod.Warning("Could not patch GenClosest.ClosestThingReachable_NewTemp, IL does not match expectations: [call GenClosest.ClosestThing_Global] not found.");
            return codeMatcher.Instructions();
        }

        codeMatcher.Operand = _method_GenClosest_ClosestThing_Global_NewTemp;

        codeMatcher.Insert([
            new(OpCodes.Ldarg_S, 13)
        ]);

        return codeMatcher.Instructions();
    }
}
#endif
