namespace RealisticOrbitalTrade.AutoSeller;

/// <summary>
/// The main mod class for Realistic Orbital Trade's AutoSeller integration.
/// </summary>
public partial class AutoSellerMod
{
    partial void Construct()
    {
        _ = PatchAll(Constants.Id);

        RealisticOrbitalTradeMod.Instance.LogMessage("\"AutoSeller\" interop loaded successfully!");
    }
}
