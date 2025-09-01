namespace RealisticOrbitalTrade.DynamicTradeInterface;

/// <summary>
/// The main mod class for Realistic Orbital Trade's Dynamic Trade Interface integration.
/// </summary>
public class DynamicTradeInterfaceMod : Mod
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DynamicTradeInterfaceMod"/> class.
    /// </summary>
    /// <param name="content">The mod content pack.</param>
    public DynamicTradeInterfaceMod(ModContentPack content)
        : base(content)
    {
        new Harmony(Constants.Id).PatchAll(Assembly.GetExecutingAssembly());

        RealisticOrbitalTradeMod.Message(
            "\"Dynamic Trade Interface\" interop loaded successfully!"
        );
    }
}
