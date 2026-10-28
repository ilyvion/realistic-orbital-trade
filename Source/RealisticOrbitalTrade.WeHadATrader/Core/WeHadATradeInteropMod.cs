namespace RealisticOrbitalTrade.WeHadATrader;

/// <summary>
/// The main mod class for Realistic Orbital Trade's We Had a Trader? integration.
/// </summary>
public partial class WeHadATraderInteropMod
{
    partial void Construct()
    {
        _ = PatchAll(Constants.Id);

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"We Had a Trader?\" interop loaded successfully!"
        );
    }
}
