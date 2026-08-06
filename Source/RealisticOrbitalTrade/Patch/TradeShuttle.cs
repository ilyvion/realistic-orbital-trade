#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE0062 // Make local function 'static'

using System.Diagnostics.CodeAnalysis;
using System.Reflection.Emit;
using System.Text;
using RealisticOrbitalTrade.Comps;
#if v1_5_OR_GREATER
using Verse.AI;
#endif

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

        _ = codeMatcher.Insert([
            // == this
            new(OpCodes.Ldarg_0),
            // call patch method (HideFinishedMessageForTradeShuttle)
            new(OpCodes.Call, _methodHideFinishedMessageForTradeShuttle),
            // if HideFinishedMessageForTradeShuttle returns true, skip showing message
            new(OpCodes.Brtrue_S, endLabel),
        ]);

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

// This fixes not hauling genepacks out of genebanks (vanilla or modded, as long as the
// container comp implements CompGenepackContainer, or a subclass thereof). Genebanks use the
// stock "Building" thingClass with just CompGenepackContainer attached, and vanilla's various
// haul-source-aware search paths (region/global thing search, ListerThings.GetAllThings,
// HaulAIUtility.IsInHaulableInventory) all gate on the spawned Thing itself being an
// IHaulSource via an `is IHaulSource` check - a wrapper object registered elsewhere doesn't
// satisfy any of those checks, since the check runs against the actual Building instance, not
// against whatever a Harmony patch separately registered. Since we can't retrofit the
// IHaulSource interface onto the existing CompGenepackContainer/Building types, we instead
// patch every place vanilla does that `is IHaulSource` check to also recognize a genebank
// building via its CompGenepackContainer.
#if v1_5_OR_GREATER
internal sealed class CompGenepackContainerHaulSource(CompGenepackContainer comp)
    : IHaulSource,
        IHaulDestination
{
    // Vanilla's haul-destination checks consult GetStoreSettings() to decide whether this
    // haul source accepts a given thing. A fresh StorageSettings() starts with an empty
    // filter (nothing allowed) and StoragePriority.Unstored (the lowest priority), so without
    // these, vanilla considers the genebank unwilling to hold genepacks at all: a pawn hauls
    // one out to other storage, and our own autoload logic immediately hauls it right back in,
    // forever. Critical priority keeps it from being treated as a candidate to haul *out of*
    // for any reason other than explicit ejection.
    private readonly StorageSettings _storeSettings = CreateStoreSettings();

    private static StorageSettings CreateStoreSettings()
    {
        var settings = new StorageSettings { Priority = StoragePriority.Critical };
        settings.filter.SetAllow(RimWorld.ThingDefOf.Genepack, true);
        return settings;
    }

    public Map Map => comp.parent.MapHeld;

    public IntVec3 Position => comp.parent.Position;

#pragma warning disable CA1822 // Mark members as static
    public bool HaulSourceEnabled => true;

    public bool HaulDestinationEnabled => true;
#pragma warning restore CA1822 // Mark members as static

    public bool Accepts(Thing t) => GetStoreSettings().AllowedToAccept(t);

    public StorageSettings GetStoreSettings() => _storeSettings;

    public StorageSettings? GetParentStoreSettings() => null;

    public void Notify_SettingsChanged() { }

    public bool StorageTabVisible => false;

    public void GetChildHolders(List<IThingHolder> outChildren) =>
        comp.GetChildHolders(outChildren);

    public ThingOwner GetDirectlyHeldThings() => comp.GetDirectlyHeldThings();

    public IThingHolder? ParentHolder => comp.ParentHolder;
}

internal static class GenepackContainerHaulSourceRegistry
{
#pragma warning disable IDE0028 // Collection initialization can be simplified (not constructible via [] on older target frameworks)
    private static readonly ConditionalWeakTable<
        CompGenepackContainer,
        CompGenepackContainerHaulSource
    > HaulSources = new();
#pragma warning restore IDE0028 // Collection initialization can be simplified

    private static readonly AccessTools.FieldRef<
        ListerThings,
        List<IHaulSource>
    > ListerThingsHaulSourcesRef = AccessTools.FieldRefAccess<ListerThings, List<IHaulSource>>(
        "haulSources"
    );

    internal static bool IsRegistered(CompGenepackContainer comp) =>
        HaulSources.TryGetValue(comp, out _);

    internal static bool TryGetHaulSource(
        CompGenepackContainer comp,
        [NotNullWhen(true)] out CompGenepackContainerHaulSource? haulSource
    ) => HaulSources.TryGetValue(comp, out haulSource);

    internal static void Register(CompGenepackContainer comp, Map map)
    {
        var haulSource = HaulSources.GetValue(
            comp,
            static c => new CompGenepackContainerHaulSource(c)
        );
        map.haulDestinationManager.AddHaulSource(haulSource);
        ListerThingsHaulSourcesRef(map.listerThings).Add(haulSource);
    }

    internal static void Unregister(CompGenepackContainer comp, Map map)
    {
        if (!HaulSources.TryGetValue(comp, out var haulSource))
        {
            return;
        }

        map.haulDestinationManager.RemoveHaulSource(haulSource);
        _ = ListerThingsHaulSourcesRef(map.listerThings).Remove(haulSource);
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.SpawnSetup))]
internal static class Rimworld_Thing_SpawnSetup_RegisterGenepackContainerHaulSource
{
    internal static void Postfix(Thing __instance, Map map)
    {
        var comp = (__instance as ThingWithComps)?.GetComp<CompGenepackContainer>();
        if (comp == null)
        {
            return;
        }

        GenepackContainerHaulSourceRegistry.Register(comp, map);
    }
}

[HarmonyPatch(typeof(Thing), nameof(Thing.DeSpawn))]
internal static class Rimworld_Thing_DeSpawn_UnregisterGenepackContainerHaulSource
{
    internal static void Prefix(Thing __instance)
    {
        var comp = (__instance as ThingWithComps)?.GetComp<CompGenepackContainer>();
        if (comp == null)
        {
            return;
        }

        GenepackContainerHaulSourceRegistry.Unregister(comp, __instance.Map);
    }
}

// HaulAIUtility.IsInHaulableInventory(thing) gates whether an unspawned thing (e.g. a genepack
// sitting in a genebank's inner container) counts as reachable for hauling purposes; vanilla
// only recognizes it if `thing.ParentHolder is IHaulSource`, which a genepack's ParentHolder
// (the CompGenepackContainer itself) never satisfies.
[HarmonyPatch(typeof(HaulAIUtility), nameof(HaulAIUtility.IsInHaulableInventory))]
internal static class Rimworld_HaulAIUtility_IsInHaulableInventory_GenepackContainer
{
    internal static void Postfix(Thing thing, ref bool __result)
    {
        if (
            !__result
            && thing.ParentHolder is CompGenepackContainer comp
            && GenepackContainerHaulSourceRegistry.IsRegistered(comp)
        )
        {
            __result = true;
        }
    }
}

// StoreUtility.CurrentHaulDestinationOf resolves an unspawned thing's haul destination via
// `thing.ParentHolder as IHaulDestination`, which is null for a genepack held by a genebank
// since CompGenepackContainer itself doesn't implement IHaulDestination. That makes
// IsInValidBestStorage()/ListerHaulables.ShouldBeHaulable() treat every contained genepack as
// not being in valid storage at all, so vanilla's general hauling (WorkGiver_HaulGeneral)
// keeps hauling it out to other storage, only for the genebank's own vanilla auto-absorb logic
// to immediately haul it right back in - an endless loop. Resolving our registered haul source
// (which also implements IHaulDestination) here lets IsInValidBestStorage see its
// StorageSettings (Genepack-only filter, Critical priority) and correctly conclude the
// genepack is already exactly where it should be.
[HarmonyPatch(typeof(StoreUtility), nameof(StoreUtility.CurrentHaulDestinationOf))]
internal static class Rimworld_StoreUtility_CurrentHaulDestinationOf_GenepackContainer
{
    internal static void Postfix(Thing t, ref IHaulDestination __result)
    {
        if (
            __result == null
            && t.ParentHolder is CompGenepackContainer comp
            && GenepackContainerHaulSourceRegistry.TryGetHaulSource(comp, out var haulSource)
        )
        {
            __result = haulSource;
        }
    }
}

// RegionProcessorClosestThingReachable.RegionProcessor is what LoadTransportersJobUtility's
// pawn job search (via GenClosest's region-wise BFS) actually uses to find a haulable thing to
// carry to the transporter. Its own haul-source expansion only considers buildings that are
// themselves `is IHaulSource`, so genebanks are invisible to it; we separately expand any
// genebank buildings found in the same region here, reusing vanilla's own ProcessThing for the
// distance/priority/reachability bookkeeping so this stays in sync with vanilla's behavior.
[HarmonyPatch(typeof(RegionProcessorClosestThingReachable), "RegionProcessor")]
internal static class Rimworld_RegionProcessorClosestThingReachable_RegionProcessor_GenepackContainer
{
    private static readonly MethodInfo _methodProcessThing = AccessTools.Method(
        typeof(RegionProcessorClosestThingReachable),
        "ProcessThing",
        [typeof(Region), typeof(Thing)]
    );

    internal static void Postfix(
        RegionProcessorClosestThingReachable __instance,
        Region reg,
        bool ___lookInHaulSources,
        ThingRequest ___req
    )
    {
        if (!___lookInHaulSources)
        {
            return;
        }

        foreach (var building in reg.ListerThings.GetThingsOfType<Building>())
        {
            var comp = (building as ThingWithComps)?.GetComp<CompGenepackContainer>();
            if (comp == null || !GenepackContainerHaulSourceRegistry.IsRegistered(comp))
            {
                continue;
            }

            foreach (var item in comp.GetDirectlyHeldThings())
            {
                if (___req.Accepts(item))
                {
                    _ = _methodProcessThing.Invoke(__instance, [reg, item]);
                }
            }
        }
    }
}
#endif
