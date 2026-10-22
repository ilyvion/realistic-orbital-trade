namespace RealisticOrbitalTrade.AutoSeller;

/// <summary>
/// The main mod class for Realistic Orbital Trade's AutoSeller integration.
/// </summary>
public partial class AutoSellerMod
{
    partial void Construct()
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage("\"AutoSeller\" interop loaded successfully!");
    }
}
