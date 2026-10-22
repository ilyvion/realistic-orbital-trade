namespace RealisticOrbitalTrade.WeHadATrader;

/// <summary>
/// The main mod class for Realistic Orbital Trade's We Had a Trader? integration.
/// </summary>
public partial class WeHadATraderInteropMod
{
    partial void Construct()
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"We Had a Trader?\" interop loaded successfully!"
        );
    }
}
