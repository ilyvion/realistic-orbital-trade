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

    private static readonly Dictionary<OpCode, OpCode> ShortToLongBranchOpCodes = new()
    {
        [OpCodes.Br_S] = OpCodes.Br,
        [OpCodes.Brfalse_S] = OpCodes.Brfalse,
        [OpCodes.Brtrue_S] = OpCodes.Brtrue,
        [OpCodes.Beq_S] = OpCodes.Beq,
        [OpCodes.Bne_Un_S] = OpCodes.Bne_Un,
        [OpCodes.Bge_S] = OpCodes.Bge,
        [OpCodes.Bgt_S] = OpCodes.Bgt,
        [OpCodes.Ble_S] = OpCodes.Ble,
        [OpCodes.Blt_S] = OpCodes.Blt,
        [OpCodes.Bge_Un_S] = OpCodes.Bge_Un,
        [OpCodes.Bgt_Un_S] = OpCodes.Bgt_Un,
        [OpCodes.Ble_Un_S] = OpCodes.Ble_Un,
        [OpCodes.Blt_Un_S] = OpCodes.Blt_Un,
        [OpCodes.Leave_S] = OpCodes.Leave,
    };

    /// <summary>
    /// Widens every short-form branch instruction (e.g. <c>brfalse.s</c>) to its long-form
    /// equivalent. Harmony's IL emitter does not widen a short branch opcode on its own, so a
    /// pre-existing short branch that still fit its signed-byte range in the original method can
    /// silently overflow into a corrupt offset once a transpiler inserts instructions between it
    /// and its target, pushing the jump past that range. Call this on the incoming instructions
    /// before matching/inserting so branch distances become irrelevant.
    /// </summary>
    public static IEnumerable<CodeInstruction> WidenShortBranches(
        IEnumerable<CodeInstruction> instructions
    )
    {
        foreach (var instruction in instructions)
        {
            if (ShortToLongBranchOpCodes.TryGetValue(instruction.opcode, out var longOpCode))
            {
                instruction.opcode = longOpCode;
            }
            yield return instruction;
        }
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

    /// <summary>
    /// Set alongside <see cref="RenegotiatingAgreement"/> when the reopened trade dialog was
    /// triggered automatically because the trader is leaving with the shuttle only partially
    /// loaded (see <see cref="TryBeginDepartureRenegotiation"/>), rather than by the player
    /// choosing to renegotiate. Already-loaded/boarded sold goods can't be edited in this mode -
    /// see <see cref="Patch.Rimworld_TradeDeal_TryExecute"/> - and closing without accepting warns
    /// the player it will fail the trade instead of silently doing nothing - see
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/>.
    /// </summary>
    public static bool RenegotiatingBecauseDeparting { get; private set; }

    /// <summary>
    /// Set once the player has confirmed, via the warning shown by
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/>, that closing a departure-triggered
    /// renegotiation without accepting should actually fail the trade.
    /// </summary>
    public static bool DepartureRenegotiationCancelConfirmed { get; internal set; }

    /// <summary>
    /// The <see cref="Quests.QuestPart_TryBeginDepartureRenegotiation.outSignalFailed"/> tag for
    /// the renegotiation currently in progress, set alongside <see cref="RenegotiatingBecauseDeparting"/>.
    /// Declining a departure-triggered renegotiation - see
    /// <see cref="Patch.Rimworld_Dialog_Trade_Close"/> - fires this instead of duplicating the
    /// quest's own dump-and-fail logic, so there's exactly one place that logic lives regardless
    /// of which reason caused the renegotiation to not go through.
    /// </summary>
    public static string? DepartureRenegotiationFailedSignal { get; private set; }

    internal static void BeginRenegotiation(TradeAgreement tradeAgreement)
    {
        RenegotiatingAgreement = tradeAgreement;
        RenegotiationOriginalTradeables = null;
        RenegotiatingBecauseDeparting = false;
        DepartureRenegotiationCancelConfirmed = false;
        DepartureRenegotiationFailedSignal = null;
    }

    internal static void EndRenegotiation()
    {
        RenegotiatingAgreement = null;
        RenegotiationOriginalTradeables = null;
        RenegotiatingBecauseDeparting = false;
        DepartureRenegotiationCancelConfirmed = false;
        DepartureRenegotiationFailedSignal = null;
    }

    /// <summary>
    /// Called when the trade shuttle's departure timer expires with the shuttle only partially
    /// loaded. Locks in whatever's already physically loaded as the new sold-to-trader amount
    /// (see <see cref="TradeAgreement.ApplyRenegotiation"/>), then reopens the trade dialog so the
    /// player can adjust what they're buying to match, instead of dumping everything and failing
    /// the trade outright. Returns <see langword="false"/> without doing anything if there's
    /// nothing loaded to salvage, or no negotiator available to run the renegotiation - the caller
    /// is expected to fall back to failing the trade outright in that case.
    /// </summary>
    internal static bool TryBeginDepartureRenegotiation(
        TradeAgreement tradeAgreement,
        string outSignalFailed
    )
    {
        if (!tradeAgreement.NegotiatorAvailable)
        {
            RealisticOrbitalTradeMod.Instance.LogDevMessage(
                "TryBeginDepartureRenegotiation failing because there is no negotiator available"
            );
            return false;
        }

        // Tradeable.TraderWillTrade (hit while merging more than one already-committed
        // tradeable, including inside ApplyRenegotiation's own diffing) reads the static
        // TradeSession.trader, which is only populated while an actual trade dialog is open -
        // null here, since this runs from a quest signal with no dialog open yet, and would
        // otherwise NRE. Setting it to the correct trader for the duration of every tradeable
        // computation below is also semantically correct, not just crash avoidance: it's the
        // same trader TryOpenComms is about to open a real session with anyway.
        var previousTrader = TradeSession.trader;
        TradeSession.trader = tradeAgreement.tradeShip;
        try
        {
            var loadedOnlyTradeables = tradeAgreement.LoadedOnlyTradeables();
            if (TradeAgreement.IsFullCancellation(loadedOnlyTradeables))
            {
                RealisticOrbitalTradeMod.Instance.LogDevMessage(
                    "TryBeginDepartureRenegotiation failing because this is a full cancellation"
                );
                return false;
            }

            _ = tradeAgreement.ApplyRenegotiation(
                tradeAgreement.AllTradeables,
                loadedOnlyTradeables
            );
        }
        finally
        {
            TradeSession.trader = previousTrader;
        }

        BeginRenegotiation(tradeAgreement);
        RenegotiatingBecauseDeparting = true;
        DepartureRenegotiationFailedSignal = outSignalFailed;

        // A letter sent the same tick the dialog opens can't actually be read before the
        // renegotiation happens - shown as a blocking dialog instead so the player sees the
        // explanation before comms open, not buried behind it.
        Find.WindowStack.Add(
            new Dialog_MessageBox(
                "RealisticOrbitalTrade.DepartureRenegotiationExplanationText".Translate(
                    tradeAgreement.tradeShip.TraderName
                ),
                title: "RealisticOrbitalTrade.DepartureRenegotiationExplanationTitle".Translate(),
                buttonAAction: () =>
                    tradeAgreement.tradeShip.TryOpenComms(tradeAgreement.negotiator)
            )
        );
        return true;
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
            var requiredThing = thing is GeneSetHolderBase ? thing : null;

            // A requiredThing-tracked entry (genepack/xenogerm) needs its own row per exact
            // instance - Matches() keys those off reference identity, so merging would collapse
            // distinct genepacks into one row. Every other entry only distinguishes by
            // def/stuff/quality/health, and several sold items can share all of those (e.g.
            // several identical-quality tuques) - without merging, each becomes its own
            // count-1 entry that all match the same physical stock, and CheckAutoload's
            // UpdateToLoadList (which corrects a matching "To load" row to whichever single
            // entry it's currently processing, rather than summing across entries sharing that
            // row) then caps the row at 1 instead of the true combined total, forcing a separate
            // hauling trip per entry instead of one trip for the lot.
            var existingIndex =
                requiredThing == null
                    ? compTradeShuttle.requiredSpecificItems.FindIndex(t =>
                        t.requiredThing == null
                        && t.def == thing.def
                        && t.stuffDef == thing.Stuff
                        && t.isInnerThing == hasInnerThing
                        && t.healthAffectsPrice == healthAffectsPrice
                        && t.hitPoints == thing.HitPoints
                        && t.maxHitPoints == thing.MaxHitPoints
                        && t.hasQuality == qualityCategory.HasValue
                        && t.quality == (qualityCategory ?? QualityCategory.Normal)
                    )
                    : -1;
            if (existingIndex >= 0)
            {
                compTradeShuttle.requiredSpecificItems[existingIndex] = compTradeShuttle
                    .requiredSpecificItems[existingIndex]
                    .WithCount(
                        compTradeShuttle.requiredSpecificItems[existingIndex].count
                            + thingCount.Count
                    );
            }
            else
            {
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
                        requiredThing = requiredThing,
                    }
                );
            }
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
