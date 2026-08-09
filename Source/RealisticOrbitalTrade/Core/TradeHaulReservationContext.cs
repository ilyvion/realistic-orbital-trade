using RealisticOrbitalTrade.Comps;

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
    /// <paramref name="thing"/> to a shuttle whose trade agreement has it reserved.
    /// </summary>
    internal static bool IsExempt(Pawn claimant, Thing thing)
    {
        if (ActivePawn != claimant || ActiveTransporter == null)
        {
            return false;
        }

        var tradeAgreement = ActiveTransporter
            .parent.TryGetComp<CompTradeShuttle>()
            ?.tradeAgreement;
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
