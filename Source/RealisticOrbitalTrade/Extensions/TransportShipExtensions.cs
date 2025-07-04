public static class TransportShipExtensions
{
    public static IntVec3 GetDropLocation(this TransportShip transportShip)
    {
#if v1_6
        IntVec3 dropLoc = transportShip.shipThing.InteractionCell;
#else
        IntVec3 dropLoc = transportShip.shipThing.Position + ShipJob_Unload.DropoffSpotOffset;
#endif
        return dropLoc;
    }
}