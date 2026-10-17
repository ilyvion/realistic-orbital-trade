namespace RealisticOrbitalTrade.Extensions;

internal static class TransportShipExtensions
{
    public static IntVec3 GetDropLocation(this TransportShip transportShip)
    {
        ArgumentNullException.ThrowIfNull(transportShip);
#if v1_6_OR_GREATER
        var dropLoc = transportShip.shipThing.InteractionCell;
#else
        var dropLoc = transportShip.shipThing.Position + ShipJob_Unload.DropoffSpotOffset;
#endif
        return dropLoc;
    }
}
