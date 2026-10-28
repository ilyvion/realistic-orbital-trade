namespace RealisticOrbitalTrade.DynamicTradeInterface;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Dynamic Trade Interface integration.
/// </summary>
public partial class DynamicTradeInterfaceMod
{
    partial void Construct()
    {
        _ = PatchAll(Constants.Id);

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"Dynamic Trade Interface\" interop loaded successfully!"
        );
    }
}
