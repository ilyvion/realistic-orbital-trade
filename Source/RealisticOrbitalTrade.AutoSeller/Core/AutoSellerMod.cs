namespace RealisticOrbitalTrade.AutoSeller;

/// <summary>
/// The main mod class for Realistic Orbital Trade's AutoSeller integration.
/// </summary>
public class AutoSellerMod : IlyvionMod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AutoSellerMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public AutoSellerMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage("\"AutoSeller\" interop loaded successfully!");
    }
}
