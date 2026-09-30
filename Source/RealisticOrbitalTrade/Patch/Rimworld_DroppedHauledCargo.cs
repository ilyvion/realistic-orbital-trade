using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Finds the trade shuttle a pawn is hauling to while its hauling job is ending, for the drop
/// patches below: <see cref="Pawn_JobTracker"/> drops a carried thing only after clearing the job,
/// so the shuttle is recorded here before that happens.
/// </summary>
internal static class DroppedHauledCargo
{
    private static Pawn? _endingJobPawn;
    private static CompTransporter? _endingJobTransporter;

    private static CompTransporter? TransporterFor(Pawn pawn) =>
        JobDriver_HaulMultipleToTransporter.HaulingTransporter(pawn)?.TryGetComp<CompTransporter>()
        ?? (pawn == _endingJobPawn ? _endingJobTransporter : null);

    /// <summary>
    /// <paramref name="placedAction"/>, extended to protect whatever <paramref name="pawn"/> drops
    /// of the goods it is hauling to a trade shuttle (see
    /// <see cref="TradeAgreement.ProtectDroppedCargo"/>). Protecting each placement covers a drop
    /// that merges into several stacks on the ground.
    /// </summary>
    internal static Action<Thing, int>? ProtectingPlacedAction(
        Pawn pawn,
        Action<Thing, int>? placedAction
    )
    {
        var transporter = TransporterFor(pawn);
        var tradeAgreement = transporter?.parent.TryGetComp<CompTradeShuttle>()?.tradeAgreement;
        return tradeAgreement == null
            ? placedAction
            : (placed, count) =>
            {
                placedAction?.Invoke(placed, count);
                tradeAgreement.ProtectDroppedCargo(transporter!, placed, count);
            };
    }

    internal readonly struct EndingJobScope : IDisposable
    {
        private readonly Pawn? _previousPawn;
        private readonly CompTransporter? _previousTransporter;

        internal EndingJobScope(Pawn pawn)
        {
            _previousPawn = _endingJobPawn;
            _previousTransporter = _endingJobTransporter;
            _endingJobPawn = pawn;
            _endingJobTransporter = JobDriver_HaulMultipleToTransporter
                .HaulingTransporter(pawn)
                ?.TryGetComp<CompTransporter>();
        }

        public void Dispose()
        {
            _endingJobPawn = _previousPawn;
            _endingJobTransporter = _previousTransporter;
        }
    }
}

[HarmonyPatch(typeof(Pawn_JobTracker), "CleanupCurrentJob")]
internal static class Rimworld_Pawn_JobTracker_CleanupCurrentJob
{
    private static void Prefix(Pawn ___pawn, out DroppedHauledCargo.EndingJobScope __state) =>
        __state = new(___pawn);

    private static void Postfix(DroppedHauledCargo.EndingJobScope __state) => __state.Dispose();
}

/// <summary>
/// Protects sold goods a hauler drops from its hands without delivering them, e.g. when its job is
/// interrupted or it's downed.
/// </summary>
[HarmonyPatch]
internal static class Rimworld_Pawn_CarryTracker_TryDropCarriedThing
{
    private static IEnumerable<MethodBase> TargetMethods() =>
        AccessTools
            .GetDeclaredMethods(typeof(Pawn_CarryTracker))
            .Where(m => m.Name == nameof(Pawn_CarryTracker.TryDropCarriedThing));

    private static void Prefix(Pawn_CarryTracker __instance, ref Action<Thing, int>? placedAction)
    {
        if (__instance.CarriedThing is null or Pawn)
        {
            return;
        }

        placedAction = DroppedHauledCargo.ProtectingPlacedAction(__instance.pawn, placedAction);
    }
}

/// <summary>
/// Drops the sold goods a bulk hauler holds in its inventory itself, protected, before vanilla
/// drops the whole inventory (e.g. when the pawn is downed).
/// </summary>
[HarmonyPatch(typeof(Pawn_InventoryTracker), nameof(Pawn_InventoryTracker.DropAllNearPawn))]
internal static class Rimworld_Pawn_InventoryTracker_DropAllNearPawn
{
    private static void Prefix(Pawn_InventoryTracker __instance)
    {
        if (__instance.pawn.jobs?.curDriver is JobDriver_HaulMultipleToTransporter driver)
        {
            driver.DropUndeliveredCargo();
        }
    }
}
