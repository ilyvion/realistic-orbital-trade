namespace RealisticOrbitalTrade.Extensions;

internal static class TransportShipExtensions
{
    public static IntVec3 GetDropLocation(this TransportShip transportShip)
    {
        if (transportShip == null)
        {
            throw new ArgumentNullException(nameof(transportShip));
        }
#if v1_6
        var dropLoc = transportShip.shipThing.InteractionCell;
#else
        var dropLoc = transportShip.shipThing.Position + ShipJob_Unload.DropoffSpotOffset;
#endif
        return dropLoc;
    }
}
