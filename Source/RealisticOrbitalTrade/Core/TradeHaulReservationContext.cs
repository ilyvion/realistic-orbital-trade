using RealisticOrbitalTrade.Comps;
using Verse.AI;

namespace RealisticOrbitalTrade;

/// <summary>
/// Tracks which pawn/transporter pair is currently running the trade-shuttle haul-candidate scan
/// (<see cref="LoadTransportersJobUtility.FindThingToLoad"/>, reached both from a
/// pre-job <c>WorkGiver</c> scan and from a hauling job's own <c>Notify_Starting</c>), so
/// <see cref="Patch.Rimworld_ReservationManager_CanReserve"/> can tell "this pawn is asking
/// whether it may haul this specific sold item to this specific shuttle" apart from every other
/// reason a pawn might ask to reserve the same item (eating, crafting, wearing, ...) - a
/// distinction <see cref="Verse.AI.ReservationManager.CanReserve"/> has no other way to make,
/// since it isn't given the caller's job or intent. Entering/exiting nests safely (see
/// <see cref="Scope"/>), since the scan can run one level deep inside another active scope (e.g.
/// our own <c>JobDriver_HaulMultipleToTransporter.Notify_Starting</c> wraps the whole method,
/// including its own inner call to <c>FindThingToLoad</c>).
///
/// For the pre-job scan phase there's no job yet to read the transporter off of, so that phase
/// still relies on the explicit <see cref="Scope"/> above. Once a job exists, <see cref="IsExempt"/>
/// also falls back to reading the transporter straight off the claimant's own current job (target
/// B is the convention transporter-loading jobs use for the transporter), so a job's execution
/// stays exempt for as long as it runs, even long after whichever <see cref="Scope"/> built the
/// job has closed.
/// </summary>
internal static class TradeHaulReservationContext
{
    internal static Pawn? ActivePawn { get; private set; }
    internal static CompTransporter? ActiveTransporter { get; private set; }

    internal readonly struct Scope : IDisposable
    {
        private readonly Pawn? _previousPawn;
        private readonly CompTransporter? _previousTransporter;

        internal Scope(Pawn pawn, CompTransporter transporter)
        {
            _previousPawn = ActivePawn;
            _previousTransporter = ActiveTransporter;
            ActivePawn = pawn;
            ActiveTransporter = transporter;
        }

        public void Dispose()
        {
            ActivePawn = _previousPawn;
            ActiveTransporter = _previousTransporter;
        }
    }

    internal static Scope Enter(Pawn pawn, CompTransporter transporter) => new(pawn, transporter);

    /// <summary>
    /// Whether <paramref name="claimant"/> is currently mid-scan/mid-reservation for hauling
    /// <paramref name="thing"/> to a shuttle whose trade agreement has it reserved, or is
    /// currently running a job that is itself hauling to such a shuttle.
    /// </summary>
    internal static bool IsExempt(Pawn claimant, Thing thing) =>
        IsExemptForTransporter(ResolveTransporter(claimant), thing);

    private static CompTransporter? ResolveTransporter(Pawn claimant) =>
        ActivePawn == claimant && ActiveTransporter != null
            ? ActiveTransporter
            : claimant.jobs?.curJob?.GetTarget(TargetIndex.B).Thing?.TryGetComp<CompTransporter>();

    private static bool IsExemptForTransporter(CompTransporter? transporter, Thing thing)
    {
        if (transporter == null)
        {
            return false;
        }

        var tradeAgreement = transporter.parent.TryGetComp<CompTradeShuttle>()?.tradeAgreement;
        return tradeAgreement != null && tradeAgreement.ReservedCountFor(thing) > 0;
    }

    /// <summary>
    /// Whether <paramref name="thing"/> is claimed by any of <paramref name="tradeAgreements"/>.
    /// </summary>
    internal static bool IsReservedByATrade(
        Thing thing,
        IReadOnlyList<TradeAgreement>? tradeAgreements
    )
    {
        if (tradeAgreements == null)
        {
            return false;
        }

        for (var i = 0; i < tradeAgreements.Count; i++)
        {
            if (tradeAgreements[i].ReservedCountFor(thing) > 0)
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Whether <paramref name="claimant"/> should be prevented from reserving/taking
    /// <paramref name="thing"/> because a trade agreement in <paramref name="tradeAgreements"/>
    /// has it earmarked for hauling and <paramref name="claimant"/> isn't the pawn currently
    /// entitled to do that specific haul (see <see cref="IsExempt"/>).
    /// </summary>
    internal static bool IsBlocked(
        Pawn claimant,
        Thing thing,
        IReadOnlyList<TradeAgreement>? tradeAgreements
    ) => IsReservedByATrade(thing, tradeAgreements) && !IsExempt(claimant, thing);
}
