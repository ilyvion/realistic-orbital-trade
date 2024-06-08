namespace RealisticOrbitalTrade;

internal static class Constants
{
    internal const string Id = "RealisticOrbitalTrade";

    internal const int MessageKeyBase = 0x420;
    internal const int MissingTradeAgreementKey = 0;
    internal const int OldTradeNonRenegotiableKey = 1;


    /// <summary>
    /// How much longer after the shuttle leaves the orbital trader will leave.
    /// This is just to give the illusion of shuttles taking a moment to get
    /// to orbit. 2,500 ticks is 1 in-game hour.
    /// </summary>
    internal const int AdditionalTicksAfterDeparture = 2500;
}
