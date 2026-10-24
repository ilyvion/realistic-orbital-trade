namespace RealisticOrbitalTrade.FindTrade;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Find / Trade integration.
/// </summary>
public partial class FindTradeInteropMod
{
    partial void Construct()
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Instance.LogMessage(
            "\"Find / Trade\" interop loaded successfully!"
        );
    }
}
