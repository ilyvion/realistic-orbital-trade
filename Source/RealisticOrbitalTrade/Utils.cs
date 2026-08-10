using System.Reflection.Emit;
using RealisticOrbitalTrade.Comps;

namespace RealisticOrbitalTrade;

internal static class Utils
{
    public static IEnumerable<CodeInstruction> InjectCallBeforeReturn(
        IEnumerable<CodeInstruction> instructions,
        MethodInfo methodToCall,
        Func<CodeInstruction, bool> matchInstruction,
        IEnumerable<CodeInstruction>? additionalInstructionsBeforeCall = null
    )
    {
        var codeMatcher = new CodeMatcher(instructions);
        _ = codeMatcher.End();
        if (codeMatcher.Instruction.opcode != OpCodes.Ret)
        {
            throw new InjectCallBeforeReturnException(codeMatcher.Instructions());
        }
        while (codeMatcher.Pos > 0)
        {
            _ = codeMatcher.Advance(-1);
            if (matchInstruction(codeMatcher.Instruction))
            {
                _ = codeMatcher.Insert(
                    (additionalInstructionsBeforeCall ?? []).Concat([
                        // new CodeInstruction(OpCodes.Ldarg_0),
                        // new CodeInstruction(OpCodes.Ldloc_0),
                        new CodeInstruction(OpCodes.Call, methodToCall),
                    ])
                );

                return codeMatcher.Instructions();
            }
        }

        throw new InjectCallBeforeReturnException(codeMatcher.Instructions());
    }

    public static Quest? AmendsQuest => RealisticOrbitalTradeGameComponent.Current.AmendsQuest;

    public static bool IsMakingAmends => AmendsQuest != null;

    /// <summary>
    /// The agreement currently being renegotiated (a real trade dialog reopened over an existing
    /// deal), or <see langword="null"/> outside of that flow. Set by
    /// <see cref="JobDriver_RenegotiateTrade"/> before reopening comms; read by
    /// <see cref="Patch.Rimworld_TradeShip_TryOpenComms"/> (to reuse the existing agreement
    /// instead of starting a new one) and by the close/accept patches (to know an in-progress
    /// trade shouldn't be discarded/replayed the way a fresh one would).
    /// </summary>
    public static TradeAgreement? RenegotiatingAgreement { get; private set; }

    /// <summary>
    /// Snapshot of <see cref="TradeAgreement.AllTradeables"/> taken the moment the reopened trade
    /// dialog's <see cref="TradeDeal"/> is first built, before the player could edit anything - the
    /// "original" side of the diff <see cref="TradeAgreement.ApplyRenegotiation"/> applies against.
    /// Captured by <see cref="Patch.Rimworld_TradeDeal_AddAllTradeables"/> rather than
    /// <see cref="BeginRenegotiation"/> itself, since <see cref="TradeAgreement.AllTradeables"/>
    /// dereferences <c>TradeSession.trader</c> (via <c>TransferableUtility.TradeableMatching</c>),
    /// which isn't assigned until comms are actually opened.
    /// </summary>
    public static List<Tradeable>? RenegotiationOriginalTradeables { get; internal set; }

    internal static void BeginRenegotiation(TradeAgreement tradeAgreement)
    {
        RenegotiatingAgreement = tradeAgreement;
        RenegotiationOriginalTradeables = null;
    }

    internal static void EndRenegotiation()
    {
        RenegotiatingAgreement = null;
        RenegotiationOriginalTradeables = null;
    }

    /// <summary>
    /// True while <see cref="JobDriver_BuyMoreTime"/> is handing the payment silver to the trade
    /// ship. That transfer runs outside any open trade dialog, so
    /// <see cref="TradeShipData.tradeAgreementForQuest"/> is legitimately unset at that point;
    /// <see cref="Patch.Rimworld_TradeShip_GiveSoldThingToTrader"/> checks this flag to avoid
    /// logging its missing-agreement warning for that expected case.
    /// </summary>
    public static bool IsBuyingMoreTime { get; internal set; }

    /// <summary>
    /// Returns <paramref name="cache"/> unless <paramref name="isDone"/> says it's done, in which
    /// case the cache is cleared (set to <see langword="null"/>) first. Used to lazily invalidate
    /// a cached reference to something that starts "live" and eventually finishes, without
    /// re-deriving it on every read.
    /// </summary>
    internal static T? GetLiveOrClear<T>(ref T? cache, Func<T, bool> isDone)
        where T : class
    {
        if (cache != null && isDone(cache))
        {
            cache = null;
        }
        return cache;
    }

    public static void AddThingToLoadToShuttle(
        ThingCountClass thingCount,
        CompTradeShuttle compTradeShuttle,
        CompShuttle compShuttle
    )
    {
        if (
            thingCount.thing.HasRequirements(
                out var healthAffectsPrice,
                out var qualityCategory,
                out var hasInnerThing
            )
        )
        {
            var thing = thingCount.thing.GetInnerIfMinified();
            compTradeShuttle.requiredSpecificItems.Add(
                new ThingDefCountWithRequirements
                {
                    def = thing.def,
                    stuffDef = thing.Stuff,
                    isInnerThing = hasInnerThing,
                    count = thingCount.Count,
                    healthAffectsPrice = healthAffectsPrice,
                    hitPoints = thing.HitPoints,
                    maxHitPoints = thing.MaxHitPoints,
                    hasQuality = qualityCategory.HasValue,
                    quality = qualityCategory ?? QualityCategory.Normal,
                    requiredThing = thing is GeneSetHolderBase ? thing : null,
                }
            );
        }
        else
        {
            // Vanilla's CompShuttle.CompInspectStringExtra treats each requiredItems entry as
            // the sole/total requirement for its ThingDef (comparing innerContainer's total
            // stack count of that def against the entry's Count), so multiple entries sharing a
            // def must be merged into one - otherwise loading enough to satisfy just one entry
            // makes it (and every other entry for that same def) vanish from "Required" even
            // though the combined requirement isn't met yet.
            var existingIndex = compShuttle.requiredItems.FindIndex(t =>
                t.ThingDef == thingCount.thing.def
            );
            if (existingIndex >= 0)
            {
                compShuttle.requiredItems[existingIndex] = compShuttle
                    .requiredItems[existingIndex]
                    .WithCount(compShuttle.requiredItems[existingIndex].Count + thingCount.Count);
            }
            else
            {
                compShuttle.requiredItems.Add(
                    new ThingDefCount(thingCount.thing.def, thingCount.Count)
                );
            }
        }
    }

    /// <summary>
    /// Symmetric inverse of the plain (non-requirements) branch of
    /// <see cref="AddThingToLoadToShuttle"/>: reduces the entry matching <paramref name="def"/> by
    /// <paramref name="leftToTakeOff"/>, removing it entirely if fully consumed. Mirrors
    /// <see cref="ThingDefCountWithRequirements.TryReduceRequiredCount"/>, which covers the
    /// requirements-based list.
    /// </summary>
    public static bool TryReduceRequiredItemsCount(
        List<ThingDefCount> requiredItems,
        ThingDef def,
        ref int leftToTakeOff
    )
    {
        var thingDefCount = requiredItems.FirstOrDefault(t => t.ThingDef == def);
        if (thingDefCount == null)
        {
            return false;
        }
        if (thingDefCount.Count > leftToTakeOff)
        {
            var index = requiredItems.IndexOf(thingDefCount);
            requiredItems[index] = thingDefCount.WithCount(thingDefCount.Count - leftToTakeOff);
            leftToTakeOff = 0;
        }
        else
        {
            leftToTakeOff -= thingDefCount.Count;
            _ = requiredItems.Remove(thingDefCount);
        }
        return true;
    }

    /// <summary>
    /// Fully rebuilds the shuttle's required-cargo lists (<c>requiredSpecificItems</c>/
    /// <c>requiredItems</c>) from <paramref name="tradeAgreement"/>'s current
    /// <c>thingsSoldToTrader</c>, discarding whatever was there before. Used after
    /// <c>thingsSoldToTrader</c> was pruned of entries whose thing no longer resolves (e.g. a
    /// sold item destroyed and saved before that pruning existed), since at that point there's no
    /// way to identify which specific required-cargo entry to reduce.
    /// </summary>
    internal static void RebuildRequiredCargo(
        TradeAgreement tradeAgreement,
        CompTradeShuttle compTradeShuttle,
        CompShuttle compShuttle
    )
    {
        compTradeShuttle.requiredSpecificItems.Clear();
        compShuttle.requiredItems.Clear();
        foreach (var thingCount in tradeAgreement.thingsSoldToTrader)
        {
            AddThingToLoadToShuttle(thingCount, compTradeShuttle, compShuttle);
        }
    }

    public static IEnumerable<Pawn> PawnsWithHaulToTransporterJob(
        CompTransporter compTransporter,
        Map map
    )
    {
        var pawns = map.mapPawns.AllPawnsSpawned;
        foreach (var pawn in pawns)
        {
            if (
                pawn.CurJobDef == RimWorld.JobDefOf.HaulToTransporter
                && pawn.jobs.curDriver is JobDriver_HaulToTransporter jobDriver
                && compTransporter == jobDriver.Transporter
            )
            {
                yield return pawn;
            }
        }
    }

    /// <summary>
    /// Everything currently physically split off a colony stack and in a pawn's hands or
    /// inventory en route to <paramref name="compTransporter"/> - already gone from the map's
    /// reachable stockpiles, but not yet inside the transporter either, so neither vanilla's own
    /// colony scan nor a straight read of the transporter's contents will find it.
    /// </summary>
    public static IEnumerable<Thing> ThingsCarriedTowardTransporter(
        CompTransporter compTransporter,
        Map map
    )
    {
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            if (
                pawn.jobs.curDriver is JobDriver_HaulToTransporter vanillaDriver
                && vanillaDriver.Transporter == compTransporter
            )
            {
                if (pawn.carryTracker.CarriedThing is { } carried)
                {
                    yield return carried;
                }
            }
            else
            {
                foreach (var thing in BulkHauledThingsCarriedBy(pawn, compTransporter))
                {
                    yield return thing;
                }
            }
        }
    }

    /// <summary>
    /// The subset of <see cref="ThingsCarriedTowardTransporter"/> riding with pawns running this
    /// mod's own bulk hauling job - the part vanilla's
    /// <see cref="TransporterUtility.ThingsBeingHauledTo"/> cannot see (see
    /// Patch/Rimworld_TransporterUtility_ThingsBeingHauledTo.cs).
    /// </summary>
    public static IEnumerable<Thing> BulkHauledThingsTowardTransporter(
        CompTransporter compTransporter,
        Map map
    )
    {
        foreach (var pawn in map.mapPawns.AllPawnsSpawned)
        {
            foreach (var thing in BulkHauledThingsCarriedBy(pawn, compTransporter))
            {
                yield return thing;
            }
        }
    }

    private static IEnumerable<Thing> BulkHauledThingsCarriedBy(
        Pawn pawn,
        CompTransporter compTransporter
    ) =>
        pawn.jobs.curDriver is JobDriver_HaulMultipleToTransporter bulkDriver
        && bulkDriver.Transporter == compTransporter
            ? JobDriver_HaulMultipleToTransporter.InFlightCargo(
                pawn.carryTracker.CarriedThing,
                bulkDriver.CarriedThings
            )
            : [];
}

#pragma warning disable CA1032 // Implement standard exception constructors
/// <summary>
/// Exception thrown when injecting a call before a return instruction fails.
/// </summary>
/// <remarks>
/// Initializes a new instance of the <see cref="InjectCallBeforeReturnException"/> class.
/// </remarks>
/// <param name="instructions">The instructions at the point the exception was thrown.</param>
[Serializable]
public class InjectCallBeforeReturnException(IEnumerable<CodeInstruction> instructions)
    : Exception()
{
    /// <summary>
    /// Gets the instructions at the point the exception was thrown.
    /// </summary>
    public IEnumerable<CodeInstruction> Instructions { get; } = instructions;
}
#pragma warning restore CA1032 // Implement standard exception constructors
