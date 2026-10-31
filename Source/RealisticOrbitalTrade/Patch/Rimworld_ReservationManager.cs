using Verse.AI;

namespace RealisticOrbitalTrade.Patch;

/// <summary>
/// Makes things sold to a trader but not yet hauled to the shuttle unavailable for every other
/// colony job (eating, crafting, wearing, ...), closing the window between a deal being struck
/// and a hauler actually picking the item up during which vanilla would otherwise let any pawn
/// consume it. See <see cref="TradeHaulReservationContext"/> for how a hauling pawn's own
/// candidate scan/reservation is told apart from everyone else's.
/// </summary>
[HarmonyPatch(typeof(ReservationManager), nameof(ReservationManager.CanReserve))]
internal static class Rimworld_ReservationManager_CanReserve
{
    private static void Postfix(Pawn claimant, LocalTargetInfo target, ref bool __result)
    {
        if (!__result || !target.HasThing)
        {
            return;
        }

        if (
            TradeHaulReservationContext.IsBlocked(
                claimant,
                target.Thing,
                RealisticOrbitalTradeGameComponent.Current?.ActiveTradeAgreements
            )
        )
        {
            __result = false;
        }
    }
}
