#pragma warning disable IDE0060 // Remove unused parameter
#pragma warning disable IDE0062 // Make local function 'static'

using System.Reflection.Emit;
using System.Text;
using RealisticOrbitalTrade.Comps;
#if v1_5_OR_GREATER
using System.Diagnostics.CodeAnalysis;
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

// CompShuttle.CheckAutoload rebuilds Transporter.leftToLoad from scratch every autoload tick,
// via TransporterUtility.AllSendablePawns -> CaravanFormingUtility.AllSendablePawns, which only
// scans map.mapPawns.AllPawnsSpawned. A required animal currently being hand-led to the shuttle
// by vanilla's own JobDriver_HaulToTransporter is despawned into its handler's carryTracker for
// the whole walk there, so it's invisible to that scan - the rebuild then drops its "to load"
// row entirely (and with it, a whole merged entry like "Husky x2" once only one of several
// same-kind animals is still in transit) even though the animal is still being correctly
// delivered. Adding it back here mirrors what Utils.ThingsCarriedTowardTransporter already does
// for this mod's own item-hauling requirement checks.
[HarmonyPatch(typeof(TransporterUtility), nameof(TransporterUtility.AllSendablePawns))]
internal static class Rimworld_TransporterUtility_AllSendablePawns_CarriedPawns
{
    internal static void Postfix(
        List<CompTransporter> transporters,
        Map map,
        ref IEnumerable<Pawn> __result
    ) => __result = __result.Concat(CarriedPawnsEnRoute(transporters, map));

    private static IEnumerable<Pawn> CarriedPawnsEnRoute(
        List<CompTransporter> transporters,
        Map map
    )
    {
        var shuttle = transporters[0].parent.TryGetComp<CompShuttle>();
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (
                pawn.jobs.curDriver is JobDriver_HaulToTransporter driver
                && transporters.Contains(driver.Transporter)
                && pawn.carryTracker.CarriedThing is Pawn carried
                && (shuttle == null || shuttle.IsRequired(carried) || shuttle.IsAllowed(carried))
            )
            {
                yield return carried;
            }
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
        if (compTradeShuttle == null)
        {
            return;
        }
        foreach (var requiredItem in compTradeShuttle.RemainingRequiredItems())
        {
            _ = stringBuilder.AppendLine("  - " + requiredItem.Label.CapitalizeFirst());
        }
    }

    private static readonly MethodInfo _methodInjectThingsLabel = SymbolExtensions.GetMethodInfo(
        () =>
            InjectThingsLabel(new(), new())
    );

    internal static IEnumerable<CodeInstruction> Transpiler(
        IEnumerable<CodeInstruction> instructions
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            "patch CompShuttle.RequiredThingsLabel",
            RealisticOrbitalTradeMod.Instance.LogError,
            codeMatcher =>
            {
                _ = codeMatcher.ExpectForward(
                    i => i.CallsConstructorOf(typeof(StringBuilder)),
                    "[newobj StringBuilder]"
                );
                _ = codeMatcher.Advance(1);
                if (!codeMatcher.IsValid || !codeMatcher.Instruction.IsStloc())
                {
                    throw new TranspilerMismatchException(
                        "[stloc] of the new StringBuilder not found."
                    );
                }
                var storeStringBuilder = codeMatcher.Instruction;

                // Inject before the StringBuilder is turned into the returned label.
                _ = codeMatcher
                    .End()
                    .ExpectBackwards(
                        i => i.LoadsLocal(storeStringBuilder.LocalVariableIndex()),
                        "[ldloc] of the StringBuilder before the return"
                    );
                _ = codeMatcher.Insert([
                    new CodeInstruction(OpCodes.Ldarg_0).MoveLabelsFrom(codeMatcher.Instruction),
                    storeStringBuilder.LoadSameLocal(),
                    new CodeInstruction(OpCodes.Call, _methodInjectThingsLabel),
                ]);
            }
        );
}

// Vanilla's own "Required: " line (built inside CompInspectStringExtra, separate from the
// RequiredThingsLabel gizmo tooltip patched above) always shows requiredItems[i]'s full
// original Count, even once some of it has already been loaded - only whether to show the
// entry at all accounts for what's loaded, not the displayed amount. This replaces the call
// that turns each entry into its displayed label with one that first subtracts what's already
// in the transporter's innerContainer.
[HarmonyPatch(typeof(CompShuttle), nameof(CompShuttle.CompInspectStringExtra))]
internal static class Rimworld_CompShuttle_CompInspectStringExtra_RemainingAmounts
{
    private static string RemainingItemLabel(ref ThingDefCount item, CompShuttle __instance) =>
        GenLabel.ThingLabel(
            item.ThingDef,
            null,
            RemainingCount(
                item.Count,
                __instance.Transporter.innerContainer.TotalStackCountOfDef(item.ThingDef)
            )
        );

    internal static int RemainingCount(int required, int loaded) => Mathf.Max(0, required - loaded);

    private static readonly MethodInfo _methodThingDefCountLabelGetter = AccessTools.PropertyGetter(
        typeof(ThingDefCount),
        nameof(ThingDefCount.Label)
    );

    private static readonly MethodInfo _methodRemainingItemLabel = AccessTools.Method(
        typeof(Rimworld_CompShuttle_CompInspectStringExtra_RemainingAmounts),
        nameof(RemainingItemLabel)
    );

#pragma warning disable CA1859 // Use concrete types when possible for improved performance
    private static IEnumerable<CodeInstruction> Transpiler(
#pragma warning restore CA1859 // Use concrete types when possible for improved performance
        IEnumerable<CodeInstruction> instructions
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            "patch CompShuttle.CompInspectStringExtra",
            RealisticOrbitalTradeMod.Instance.LogError,
            codeMatcher =>
            {
                _ = codeMatcher.ExpectForward(
                    i =>
                        i.opcode == OpCodes.Call
                        && i.operand is MethodInfo m
                        && m == _methodThingDefCountLabelGetter,
                    "call to get value of ThingDefCount::Label"
                );

                _ = codeMatcher.Insert([new CodeInstruction(OpCodes.Ldarg_0)]);
                _ = codeMatcher.Advance(1);
                codeMatcher.Instruction.operand = _methodRemainingItemLabel;
            }
        );
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
    ) =>
        TranspilerUtility.PatchOrOriginal(
            TranspilerUtility.WidenShortBranches(instructions),
            "patch CompTransporter.SubtractFromToLoadList",
            RealisticOrbitalTradeMod.Instance.LogError,
            codeMatcher =>
            {
                _ = codeMatcher.ExpectForward(
                    i =>
                        i.opcode == OpCodes.Call
                        && i.operand is MethodInfo m
                        && m == _methodCompTransporterAnyInGroupHasAnythingLeftToLoad_get,
                    "call to get value of CompTransporter::AnyInGroupHasAnythingLeftToLoad"
                );
                _ = codeMatcher.Advance(1);
                var endLabel = codeMatcher.Operand;
                _ = codeMatcher.Advance(1);

                _ = codeMatcher.Insert([
                    // == this
                    new(OpCodes.Ldarg_0),
                    // call patch method (HideFinishedMessageForTradeShuttle)
                    new(OpCodes.Call, _methodHideFinishedMessageForTradeShuttle),
                    // if HideFinishedMessageForTradeShuttle returns true, skip showing message
                    new(OpCodes.Brtrue, endLabel),
                ]);
            }
        );

    // Runs for every deposit into the shuttle, whichever job or mod made it.
    private static void Postfix(CompTransporter __instance) =>
        __instance
            .parent.TryGetComp<CompTradeShuttle>()
            ?.tradeAgreement?.ReleaseReservationsBeyondNeed();
}

// A sold pawn drops what it carries, its weapon and its inventory onto the map as it boards, the way
// Pawn.PreTraded does for a pawn sold on the map. PreTraded runs again on takeoff, when the pawn is
// enclosed in the shuttle and would put them in the shuttle instead.
[HarmonyPatch(typeof(CompTransporter), nameof(CompTransporter.Notify_ThingAdded))]
internal static class Rimworld_CompTransporter_Notify_ThingAdded_SoldPawnDropsGear
{
    internal static void Postfix(CompTransporter __instance, Thing t)
    {
        var shuttle = __instance.parent;
        if (
            t is Pawn pawn
            && shuttle.Spawned
            && shuttle.TryGetComp<CompTradeShuttle>()?.tradeAgreement is { } tradeAgreement
            && tradeAgreement.pawnsSoldToTrader.Contains(pawn)
        )
        {
            DropGear(pawn, shuttle.Position);
        }
    }

    private static void DropGear(Pawn pawn, IntVec3 position)
    {
        if (pawn.carryTracker?.CarriedThing != null)
        {
            _ = pawn.carryTracker.TryDropCarriedThing(position, ThingPlaceMode.Near, out _);
        }
        pawn.equipment?.DropAllEquipment(position, forbid: true);
        if (pawn.inventory != null && pawn.inventory.innerContainer.TotalStackCount > 0)
        {
            pawn.inventory.DropAllNearPawn(position, forbid: true);
        }
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
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            "patch LoadTransportersJobUtility.FindThingToLoad",
            RealisticOrbitalTradeMod.Instance.LogWarning,
            codeMatcher =>
            {
                _ = codeMatcher.ExpectForward(
                    i =>
                        i.opcode == OpCodes.Call
                        && i.operand is MethodInfo m
                        && m == _method_GenClosest_ClosestThingReachable,
                    "[call GenClosest.ClosestThingReachable]"
                );

                codeMatcher.Operand = _method_GenClosest_ClosestThingReachable_NewTemp;

                _ = codeMatcher.Insert([new(OpCodes.Ldc_I4_1)]);
            }
        );
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
    ) =>
        TranspilerUtility.PatchOrOriginal(
            instructions,
            "patch GenClosest.ClosestThingReachable_NewTemp",
            RealisticOrbitalTradeMod.Instance.LogWarning,
            codeMatcher =>
            {
                _ = codeMatcher.ExpectForward(
                    i =>
                        i.opcode == OpCodes.Call
                        && i.operand is MethodInfo m
                        && m == _method_GenClosest_ClosestThing_Global,
                    "[call GenClosest.ClosestThing_Global]"
                );

                codeMatcher.Operand = _method_GenClosest_ClosestThing_Global_NewTemp;

                _ = codeMatcher.Insert([new(OpCodes.Ldarg_S, 13)]);
            }
        );
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
